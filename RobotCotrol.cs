using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

public class RobotControl : MonoBehaviour
{
    [Header("Mode Selection")]
    public bool inferenceMode = true;

    [Header("Episode Management")]
    public int nr_episodes = 50;
    private int current_episode = 0;
    public SceneController sceneController;
    private Quaternion[] initialJointRotations;

    public JointController[] jointControllers;
    public DatasetRecorder recorder;

    [Tooltip("Control speeds for each joint. Values from -1 to 1.")]
    //public float[] actionVector;

    public Camera PistolCam;
    public GameObject projectilePrefab;
    public Transform barrel;
    public Transform target;
    public string taskName = "Shoot red paper bottle with straw";

    float maxDegreesPerSecond = 5.0f;
    float[] state = new float[3];
    float[] actionVector = new float[4];

    // gRPC client configuration (for inference mode)
    private SocketPolicyClient policyClient;
    private string serverHost = "127.0.0.1";
    private int serverPort = 9000;
    
    // Control timing variables
    public float controlFPS = 10f;
    private float timeSinceLastControl = 0f;
    private float controlInterval;
    

    [NonSerialized] public bool episodeComplete = false;
    bool dataCollectionComplete = false;
    private float episodeStartTime;
    float episodeDuration = 5.0f;
    int jpgQuality = 80;

    bool shot_already = false;

    private Texture2D screenshotTexture;
    private RenderTexture renderTexture;



    void Start()
    {
        controlInterval = 1f / controlFPS;
        episodeStartTime = Time.time;
        current_episode = 0;

        // Store initial joint rotations
        initialJointRotations = new Quaternion[jointControllers.Length];
        for (int i = 0; i < jointControllers.Length; i++)
        {
            initialJointRotations[i] = jointControllers[i].transform.localRotation;
        }

        renderTexture = new RenderTexture(recorder.videoWidth, recorder.videoHeight, 24);
        screenshotTexture = new Texture2D(recorder.videoWidth, recorder.videoHeight, TextureFormat.RGB24, false);

        // Initialize based on mode
        if (inferenceMode)
        {
            policyClient = new SocketPolicyClient(serverHost, serverPort);
        }
        else
        {
            recorder.StartEpisode(controlFPS);
        }
    }
    void OnDestroy()
    {
        // Ensure socket is properly closed when the script is destroyed
        if (inferenceMode && policyClient != null)
        {
            policyClient.Dispose();
        }
    }

    // Update is called once per frame, perfect for continuous movement.
    void Update()
    {
        // Accumulate time
        timeSinceLastControl += Time.deltaTime;
        
        // Check if it's time to run control logic
        if ((timeSinceLastControl >= controlInterval) && !dataCollectionComplete)
        {
            // Run existing control logic
            if (inferenceMode)
            {
                // Get current observation first without applying any action
                var (currentState, imageDataBefore, _) = GetCurrentObservation();
                actionVector = policyClient.GetAction(imageDataBefore, currentState, Time.time, taskName);
            }
            else
            {
                actionVector = AutoAimPolicy();
            }

            var (state, imageData, success) = DoSimulationStep(actionVector);
            float timestampInEpisode = Time.time - episodeStartTime;

            if (!inferenceMode)
            {
                recorder.RecordStep(actionVector, state, timestampInEpisode, imageData);
            }

            // Reset timer
            timeSinceLastControl -= controlInterval;
        }
        
        // Check for episode timeout
        if (!inferenceMode && Time.time - episodeStartTime >= episodeDuration)
        {
            episodeComplete = true;
        }
        // Check for episode completion
        if (episodeComplete && !inferenceMode)
        {
            recorder.FinalizeEpisode();
            current_episode++;
            
            if (current_episode < nr_episodes)
            {
                ResetForNewEpisode();
            }
            else
            {
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
            }
        }
    }
    private void ResetForNewEpisode()
    {
        // Reset scene using the unified method
        if (sceneController != null)
        {
            sceneController.RandomizeScene();
        }
        
        // Reset robot state and joint positions
        for (int i = 0; i < jointControllers.Length; i++)
        {
            jointControllers[i].rotationSpeed = 0f;
            jointControllers[i].transform.localRotation = initialJointRotations[i];
        }
        
        // Reset timing variables
        episodeStartTime = Time.time;
        timeSinceLastControl = 0f;
        episodeComplete = false;
        dataCollectionComplete = false;
        shot_already = false;

        Debug.Log($"Starting episode {current_episode + 1}/{nr_episodes}");
        // Start new episode recording
        recorder.StartEpisode(controlFPS);
    }

    public (float[] state, byte[] jpgData, bool episode_succeed) DoSimulationStep(float[] actionVector)
    {

        for (int i = 0; i < 3; i++)
        {
            jointControllers[i].rotationSpeed = actionVector[i] * maxDegreesPerSecond;
            state[i] = jointControllers[i].GetNormalizedAngle();
        }
        if (actionVector[3] > 0.9f)
        {
            Shoot();
        }


        bool episode_succeed = false;

        //Debug.Log(string.Join(", ", state));
        var jpgData = GetCameraImage();

        return (state, jpgData, episode_succeed);
    }

    public (float[] state, byte[] jpgData, bool episode_succeed) GetCurrentObservation()
    {
        // Get current state without applying any action
        for (int i = 0; i < 3; i++)
        {
            state[i] = jointControllers[i].GetNormalizedAngle();
        }

        bool episode_succeed = false;
        var jpgData = GetCameraImage();

        return (state, jpgData, episode_succeed);
    }

    public byte[] GetCameraImage()
    {
        PistolCam.targetTexture = renderTexture;
        
        // Render the camera
        PistolCam.Render();
        
        // Create a Texture2D and read the RenderTexture into it
        RenderTexture.active = renderTexture;
        screenshotTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        screenshotTexture.Apply();


        // Restore camera and RenderTexture settings
        PistolCam.targetTexture = null;
        RenderTexture.active = null;

        // Convert to PNG byte array
        byte[] jpgData = screenshotTexture.EncodeToJPG(jpgQuality);

        // Return the image data with dimensions
        return jpgData;
    }

    float[] AutoAimPolicy()
    {
        float[] actions = new float[4];

        // Get direction to target
        Vector3 directionToTarget = (target.position - barrel.position).normalized;

        // Calculate the rotation needed to point barrel toward target
        Quaternion currentBarrelRotation = barrel.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
        
        // Get the difference between current and target rotation
        Quaternion rotationDifference = targetRotation * Quaternion.Inverse(currentBarrelRotation);
        Vector3 angleDifference = rotationDifference.eulerAngles;
        
        // Convert to -180 to 180 range properly
        float targetAzimuth = angleDifference.y;
        if (targetAzimuth > 180) targetAzimuth -= 360;
        
        float targetElevation = angleDifference.x;
        if (targetElevation > 180) targetElevation -= 360;

        //Debug.Log($"Azimuth: {targetAzimuth:F1}, Elevation: {targetElevation:F1}");

        float angleMoveCoefficient = 0.5f;
        float shoting_threshold = 0.1f;
        actions[0] = Math.Clamp(targetAzimuth * angleMoveCoefficient, -1, 1);
        actions[1] = Math.Clamp(-targetElevation * angleMoveCoefficient/2, -1, 1);
        actions[2] = Math.Clamp(-targetElevation * angleMoveCoefficient/2, -1, 1);
        if (Math.Abs(targetAzimuth) < shoting_threshold && Math.Abs(targetElevation) < shoting_threshold && !shot_already)
        {
            actions[3] = 1; // Trigger shoot action
            shot_already = true;
        }
        else
        {
            actions[3] = 0; // No shoot action
        }
        return actions;
    }

    void Shoot()
    {
        float projectileSpeed = 60f;
        // Instantiate the projectile
        GameObject newProjectile = Instantiate(projectilePrefab, barrel.position + barrel.forward * 1, barrel.rotation);
        Rigidbody rb = newProjectile.GetComponent<Rigidbody>();
        rb.linearVelocity = barrel.forward * projectileSpeed;
        
        // Destroy it after 0.8 seconds
        Destroy(newProjectile, 0.8f);
    }
    public void HandleBadEpisode()
    {
        if (inferenceMode) return;
        recorder?.DiscardEpisode();
        ResetForNewEpisode();
    }
}
