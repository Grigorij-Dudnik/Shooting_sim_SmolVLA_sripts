using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;


public class DatasetRecorder : MonoBehaviour
{
    // Configuration
    [SerializeField] Camera captureCamera;
    [SerializeField] RobotControl robotControl;
    [SerializeField] public int videoWidth = 640;
    [SerializeField] public int videoHeight = 480;
    private string datasetName = "Robot_shooting_dataset";
    private string datasetPath;
    private string dataPath;
    private string videosPath;
    private string metaPath;
    private string mainCameraKey = "observation.images.main";

    // Episode State
    private int episodeIndex = 0;
    private int frameIndex;
    private int globalFrameIndex;
    private string tempImageFolderPath;
    private List<float[]> episodeActions = new List<float[]>();
    private List<float[]> episodeStates = new List<float[]>();
    private List<float> episodeTimestamps = new List<float>();

    void Awake()
    {
        datasetPath = Path.Combine(Application.dataPath, datasetName);
        dataPath = Path.Combine(datasetPath, "data", "chunk-000");
        videosPath = Path.Combine(datasetPath, "videos", "chunk-000", mainCameraKey);
        metaPath = Path.Combine(datasetPath, "meta");

        CreateDirectoryIfNotExists(dataPath);
        CreateDirectoryIfNotExists(videosPath);
    }

    public void StartEpisode(float fps)
    {
        frameIndex = 0;
        episodeActions.Clear();
        episodeStates.Clear();
        episodeTimestamps.Clear();

        episodeIndex = GetEpisodeIndex();
        
        // Create or update metadata with current task and fps
        CreateOrUpdateMetadata(fps);
        
        tempImageFolderPath = Path.Combine(Application.temporaryCachePath, "episode_" + episodeIndex);
        CreateDirectoryIfNotExists(tempImageFolderPath);
    }

    public void RecordStep(float[] action, float[] state, float timestamp, byte[] imageData)
    {
        episodeActions.Add((float[])action.Clone());
        episodeStates.Add((float[])state.Clone());
        episodeTimestamps.Add(timestamp);

        string imagePath = Path.Combine(tempImageFolderPath, "frame_" + frameIndex.ToString("D5") + ".jpg");
        File.WriteAllBytes(imagePath, imageData);

        frameIndex++;
    }

    public void FinalizeEpisode()
    {
        WriteDataFile();
        ProcessVideoFrames();
    }

    private void CreateDirectoryIfNotExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private int GetEpisodeIndex()
    {
        var jsonlFiles = Directory.GetFiles(dataPath, "episode_*.jsonl");

        if (jsonlFiles.Length == 0)
        {
            return 0;
        }

        int lastIndex = jsonlFiles
            .Select(path => int.Parse(Path.GetFileNameWithoutExtension(path).Split('_').Last()))
            .Max();
        
        return lastIndex + 1;
    }

    private void WriteDataFile()
    {
        string dataFilePath = Path.Combine(dataPath, "episode_" + episodeIndex.ToString("D6") + ".jsonl");
        var allFrameData = new List<string>();
        for (int i = 0; i < frameIndex; i++)
        {
            JObject frameData = new JObject
            {
                ["timestamp"] = episodeTimestamps[i],
                ["action"] = JArray.FromObject(episodeActions[i]),
                ["observation.state"] = JArray.FromObject(episodeStates[i]),
                ["task"] = robotControl.taskName
            };
            allFrameData.Add(JsonConvert.SerializeObject(frameData));
        }
            string fileContent = string.Join("\n", allFrameData);
            File.WriteAllText(dataFilePath, fileContent);

    }

    private void ProcessVideoFrames()
    {
        string finalFramesPath = Path.Combine(videosPath, "episode_" + episodeIndex.ToString("D6") + "_frames");
        Directory.Move(tempImageFolderPath, finalFramesPath);
    }
    public void DiscardEpisode()
    {
        Debug.Log("Discarding bad episode.");
        if (!string.IsNullOrEmpty(tempImageFolderPath) && Directory.Exists(tempImageFolderPath))
            Directory.Delete(tempImageFolderPath, true);
    }
    
    private void CreateOrUpdateMetadata(float fps)
    {
        CreateDirectoryIfNotExists(metaPath);

        string metaFilePath = Path.Combine(metaPath, "meta.json");
        JObject metadata;

        // Load existing metadata or create new
        if (File.Exists(metaFilePath))
        {
            metadata = JObject.Parse(File.ReadAllText(metaFilePath));
        }
        else
        {
            metadata = new JObject
            {
                ["dataset_name"] = datasetName,
                ["fps"] = fps,
                ["video_width"] = videoWidth,
                ["video_height"] = videoHeight,
                ["tasks"] = new JArray()
            };
        }

        // Add current task if not already present
        JArray tasks = (JArray)metadata["tasks"];
        if (!tasks.Any(t => t["task_name"]?.ToString() == robotControl.taskName))
        {
            // Calculate the next task index based on existing tasks
            int nextTaskIndex = tasks.Count > 0 ? tasks.Max(t => (int)t["task_index"]) + 1 : 0;

            tasks.Add(new JObject
            {
                ["task_name"] = robotControl.taskName,
                ["task_index"] = nextTaskIndex
            });
        }

        File.WriteAllText(metaFilePath, metadata.ToString(Formatting.Indented));
    }
}