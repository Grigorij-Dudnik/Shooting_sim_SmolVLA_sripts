using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

public class RobotControl : MonoBehaviour
{
    public JointController[] jointControllers;
    public DatasetRecorder recorder;

    [Tooltip("Control speeds for each joint. Values from -1 to 1.")]
    //public float[] actionVector;

    public Camera PistolCam;
    public GameObject projectilePrefab;
    public Transform barrel;
    public Transform target;

    float maxDegreesPerSecond = 5.0f;
    float[] state = new float[3];
    float[] actionVector = new float[3];
    
    // Control timing variables
    public float controlFPS = 10f;
    private float timeSinceLastControl = 0f;
    private float controlInterval;
    

    public bool episodeComplete = false;
    bool dataCollectionComplete = false;
    private float episodeStartTime;
    float episodeDuration = 5.0f;
    float videoMargin = 0.5f; // seconds

    int imageWidth;
    int imageHeight;

    bool shot_already = false;


    void Start()
    {
        controlInterval = 1f / controlFPS;
        episodeStartTime = Time.time;
        
        // Get video dimensions from DatasetRecorder
        imageWidth = recorder.videoWidth;
        imageHeight = recorder.videoHeight;
        
        recorder.StartEpisode();

        // place target randomly
        target.position = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0.3f, UnityEngine.Random.Range(-9f, -10f));

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
            actionVector = AutoAimPolicy();
            var (state, imageData, success) = DoSimulationStep(actionVector);
            float timestampInEpisode = Time.time - episodeStartTime;
            recorder.RecordStep(actionVector, state, timestampInEpisode, imageData.jpgData);
            
            // Reset timer
            timeSinceLastControl -= controlInterval;
        }
        
        // Check for episode timeout
        if (Time.time - episodeStartTime >= episodeDuration)
        {
            dataCollectionComplete = true;
        }
        // Check for episode timeout
        if (Time.time - episodeStartTime >= episodeDuration + videoMargin)
        {
            episodeComplete = true;
        }
        // Check for episode completion
        if (episodeComplete)
        {
            recorder.FinalizeEpisode();
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }

    public (float[] state, (byte[] jpgData, int width, int height), bool episode_succeed) DoSimulationStep(float[] actionVector)
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
        var (jpgData, imWidth, imHeight) = GetCameraImage();

        return (state, (jpgData, imWidth, imHeight), episode_succeed);
    }

    public (byte[] jpgData, int width, int height) GetCameraImage()
    {
        // Create a temporary RenderTexture
        RenderTexture renderTexture = new RenderTexture(imageWidth, imageHeight, 24);
        
        // Set the camera to render to our RenderTexture
        RenderTexture previousTarget = PistolCam.targetTexture;
        PistolCam.targetTexture = renderTexture;
        
        // Render the camera
        PistolCam.Render();
        
        // Create a Texture2D and read the RenderTexture into it
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        screenshot.Apply();
        
        // Restore camera and RenderTexture settings
        PistolCam.targetTexture = previousTarget;
        RenderTexture.active = null;

        // Convert to PNG byte array
        int quality = 80;
        byte[] jpgData = screenshot.EncodeToJPG(quality);
        
        // Clean up
        DestroyImmediate(screenshot);
        DestroyImmediate(renderTexture);
        
        // Return the image data with dimensions
        return (jpgData, imageWidth, imageHeight);
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
        float shoting_threshold = 0.05f;
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
        
        // Destroy it after 0.5 seconds
        Destroy(newProjectile, 0.5f);
    }
}
