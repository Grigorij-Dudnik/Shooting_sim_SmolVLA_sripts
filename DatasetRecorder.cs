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
    [SerializeField] public int videoWidth = 640;
    [SerializeField] public int videoHeight = 480;
    private const string taskName = "Shoot the can";
    private const int taskIndex = 0;
    private string datasetName = "Robot_shooting_dataset";
    private string datasetPath;
    private string dataPath;
    private string videosPath;
    private string metaPath;
    private string mainCameraKey = "observation.images.main";

    // Episode State
    private int episodeIndex;
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
        CreateDirectoryIfNotExists(metaPath);

        LoadOrCreateMetadata();
    }

    public void StartEpisode()
    {
        frameIndex = 0;
        episodeActions.Clear();
        episodeStates.Clear();
        episodeTimestamps.Clear();

        tempImageFolderPath = Path.Combine(Application.temporaryCachePath, "episode_" + episodeIndex);
        CreateDirectoryIfNotExists(tempImageFolderPath);

        var cameraCapture = captureCamera.GetComponent<FFmpegOut.CameraCapture>();
        cameraCapture.episodeIndex = episodeIndex;
        cameraCapture.mainCameraKey = mainCameraKey;
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
        UpdateMetadataFiles();
        var cameraCaptureScript = captureCamera.GetComponent<FFmpegOut.CameraCapture>();
        cameraCaptureScript.FinalizeCapture();
        episodeIndex++;
    }

    private void CreateDirectoryIfNotExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    private void LoadOrCreateMetadata()
    {
        string infoFilePath = Path.Combine(metaPath, "info.json");
        string tasksFilePath = Path.Combine(metaPath, "tasks.jsonl");
        
        if (File.Exists(infoFilePath))
        {
            string json = File.ReadAllText(infoFilePath);
            JObject infoData = JsonConvert.DeserializeObject<JObject>(json);
            episodeIndex = infoData["total_episodes"].Value<int>();
            globalFrameIndex = infoData["total_frames"].Value<int>();
        }
        else
        {
            episodeIndex = 0;
            globalFrameIndex = 0;
            WriteInitialInfoFile();
        }
        
        if (!File.Exists(tasksFilePath))
        {
            JObject taskInfo = new JObject
            {
                ["task_index"] = taskIndex,
                ["task"] = taskName
            };
            File.WriteAllText(tasksFilePath, JsonConvert.SerializeObject(taskInfo) + "\n");
        }
    }
    }

    private void WriteInitialInfoFile()
    {
        JObject infoData = new JObject
        {
            ["codebase_version"] = "v2.1",
            ["robot_type"] = "shooting_robot",
            ["fps"] = 10,
            ["total_episodes"] = 0,
            ["total_frames"] = 0,
            ["total_tasks"] = 1,
            ["total_videos"] = 0,
            ["chunks_size"] = 1000,
            ["splits"] = new JObject { ["train"] = "0:0" },
            ["data_path"] = "data/chunk-{episode_chunk:03d}/episode_{episode_index:06d}.jsonl",
            ["video_path"] = "videos/chunk-{episode_chunk:03d}/{video_key}/episode_{episode_index:06d}.mp4",
            ["features"] = new JObject
            {
                ["action"] = new JObject
                {
                    ["dtype"] = "float32",
                    ["shape"] = new JArray { 4 },
                    ["names"] = new JArray { "azimuth", "elevation_1", "elevation_2", "shoot" }
                },
                ["observation.state"] = new JObject
                {
                    ["dtype"] = "float32",
                    ["shape"] = new JArray { 3 },
                    ["names"] = new JArray { "azimuth_angle", "elevation_angle_1", "elevation_angle_2" }
                },
                [mainCameraKey] = new JObject
                {
                    ["dtype"] = "video",
                    ["shape"] = new JArray { videoHeight, videoWidth, 3 },
                    ["names"] = new JArray { "height", "width", "channels" },
                    ["info"] = new JObject
                    {
                        ["video.height"] = videoHeight,
                        ["video.width"] = videoWidth,
                        ["video.codec"] = "av1",
                        ["video.pix_fmt"] = "yuv420p",
                        ["video.is_depth_map"] = false,
                        ["video.fps"] = 10,
                        ["video.channels"] = 3,
                        ["has_audio"] = false
                    }
                },
                ["timestamp"] = new JObject
                {
                    ["dtype"] = "float32",
                    ["shape"] = new JArray { 1 },
                    ["names"] = null
                },
                ["frame_index"] = new JObject
                {
                    ["dtype"] = "int64",
                    ["shape"] = new JArray { 1 },
                    ["names"] = null
                },
                ["episode_index"] = new JObject
                {
                    ["dtype"] = "int64",
                    ["shape"] = new JArray { 1 },
                    ["names"] = null
                },
                ["index"] = new JObject
                {
                    ["dtype"] = "int64",
                    ["shape"] = new JArray { 1 },
                    ["names"] = null
                },
                ["task_index"] = new JObject
                {
                    ["dtype"] = "int64",
                    ["shape"] = new JArray { 1 },
                    ["names"] = null
                }
            },
            ["camera_keys"] = new JArray { mainCameraKey }
        };

        string infoFilePath = Path.Combine(metaPath, "info.json");
        File.WriteAllText(infoFilePath, JsonConvert.SerializeObject(infoData, Formatting.Indented));
    }


    private void WriteDataFile()
    {
        string dataFilePath = Path.Combine(dataPath, "episode_" + episodeIndex.ToString("D6") + ".jsonl");
        StringBuilder fileContent = new StringBuilder();
        for (int i = 0; i < frameIndex; i++)
        {
            JObject frameData = new JObject
            {
                ["timestamp"] = episodeTimestamps[i],
                ["action"] = JArray.FromObject(episodeActions[i]),
                ["observation.state"] = JArray.FromObject(episodeStates[i])
            };
            fileContent.AppendLine(JsonConvert.SerializeObject(frameData));
        }
        File.WriteAllText(dataFilePath, fileContent.ToString());
    }

    private void ProcessVideoFrames()
    {
        string finalFramesPath = Path.Combine(videosPath, "episode_" + episodeIndex.ToString("D6") + "_frames");
        Directory.Move(tempImageFolderPath, finalFramesPath);
    }

    private void UpdateMetadataFiles()
    {
        // Update episodes.jsonl
        // Update episodes.jsonl
        string episodesFilePath = Path.Combine(metaPath, "episodes.jsonl");
        JObject episodeInfo = new JObject
        {
            ["episode_index"] = episodeIndex,
            ["tasks"] = new JArray { taskName },
            ["length"] = frameIndex
        };
        File.AppendAllText(episodesFilePath, JsonConvert.SerializeObject(episodeInfo) + "\n");

        // Update episodes_stats.jsonl
        string statsFilePath = Path.Combine(metaPath, "episodes_stats.jsonl");

        // Prepare data for standard fields
        List<float[]> timestampData = episodeTimestamps.Select(t => new float[] { t }).ToList();
        List<float[]> frameIndexData = Enumerable.Range(0, frameIndex).Select(i => new float[] { i }).ToList();
        List<float[]> episodeIndexData = Enumerable.Repeat(episodeIndex, frameIndex).Select(e => new float[] { e }).ToList();
        List<float[]> globalIndexData = Enumerable.Range(globalFrameIndex, frameIndex).Select(i => new float[] { i }).ToList();
        List<float[]> taskIndexData = Enumerable.Repeat(0, frameIndex).Select(t => new float[] { t }).ToList();

        JObject statsInfo = new JObject
        {
            ["episode_index"] = episodeIndex,
            ["stats"] = new JObject
            {
                ["action"] = CalculateStats(episodeActions),
                ["observation.state"] = CalculateStats(episodeStates),
                ["timestamp"] = CalculateStats(timestampData),
                ["frame_index"] = CalculateStats(frameIndexData),
                ["episode_index"] = CalculateStats(episodeIndexData),
                ["index"] = CalculateStats(globalIndexData),
                ["task_index"] = CalculateStats(taskIndexData)
            }
        };
        File.AppendAllText(statsFilePath, JsonConvert.SerializeObject(statsInfo) + "\n");

        // Update info.json
        string infoFilePath = Path.Combine(metaPath, "info.json");
        string json = File.ReadAllText(infoFilePath);
        JObject infoData = JsonConvert.DeserializeObject<JObject>(json);
        infoData["total_episodes"] = infoData["total_episodes"].Value<int>() + 1;
        infoData["total_frames"] = infoData["total_frames"].Value<int>() + frameIndex;
        globalFrameIndex += frameIndex;
        File.WriteAllText(infoFilePath, JsonConvert.SerializeObject(infoData, Formatting.Indented));
    }

    private JObject CalculateStats(List<float[]> data)
    {
        if (data.Count == 0) return new JObject();

        int dimensions = data[0].Length;
        JObject stats = new JObject();

        if (dimensions == 1)
        {
            // For single dimension fields like timestamp
            var values = data.Select(arr => arr[0]).ToList();
            stats = new JObject
            {
                ["min"] = new JArray { values.Min() },
                ["max"] = new JArray { values.Max() },
                ["mean"] = new JArray { values.Average() },
                ["std"] = new JArray { Math.Sqrt(values.Select(x => Math.Pow(x - values.Average(), 2)).Average()) },
                ["count"] = new JArray { data.Count }
            };
        }
        else
        {
            // For multi-dimensional fields like action, state
            JArray minArray = new JArray();
            JArray maxArray = new JArray();
            JArray meanArray = new JArray();
            JArray stdArray = new JArray();

            for (int dim = 0; dim < dimensions; dim++)
            {
                var values = data.Select(arr => arr[dim]).ToList();
                minArray.Add(values.Min());
                maxArray.Add(values.Max());
                meanArray.Add(values.Average());
                stdArray.Add(Math.Sqrt(values.Select(x => Math.Pow(x - values.Average(), 2)).Average()));
            }

            stats = new JObject
            {
                ["min"] = minArray,
                ["max"] = maxArray,
                ["mean"] = meanArray,
                ["std"] = stdArray,
                ["count"] = new JArray { data.Count }
            };
        }

        return stats;
    }
}