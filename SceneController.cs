using UnityEngine;
using System.Collections.Generic;

public class SceneController : MonoBehaviour
{
    [Header("Lighting Settings")]
    [SerializeField] private Light[] sceneLights;
    [SerializeField] private float minLightIntensity = 1.0f;
    [SerializeField] private float maxLightIntensity = 2.0f;
    [SerializeField] private Color[] lightColors = { Color.white, Color.yellow, new Color(1f, 0.9f, 0.8f), new Color(0.9f, 0.9f, 1f) };
    
    [Header("Background Objects Settings")]
    [SerializeField] private GameObject[] backgroundObjectPrefabs;
    [SerializeField] private Vector3 backgroundAreaMin = new Vector3(-5f, 0f, -5f);
    [SerializeField] private Vector3 backgroundAreaMax = new Vector3(5f, 0f, 5f);
    [SerializeField] private bool randomizeRotation = true;
    
    private List<GameObject> spawnedBackgroundObjects = new List<GameObject>();
    
    [Header("Items Settings")]
    [SerializeField] private Rigidbody[] itemObjects;
    [SerializeField] private Vector3 itemAreaMin = new Vector3(-3f, 1f, -3f);
    [SerializeField] private Vector3 itemAreaMax = new Vector3(3f, 3f, 3f);
    
    private List<GameObject> spawnedItems = new List<GameObject>();

    void Start()
    {
        RandomizeScene();

    }

    public void RandomizeLighting()
    {
        if (sceneLights == null || sceneLights.Length == 0) return;

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight == null) continue;

            // Randomize intensity
            sceneLight.intensity = Random.Range(minLightIntensity, maxLightIntensity);
            
            // Randomize color from predefined colors
            if (lightColors.Length > 0)
            {
                sceneLight.color = lightColors[Random.Range(0, lightColors.Length)];
            }
            
            // Randomize directional light rotation
            if (sceneLight.type == LightType.Directional)
            {
                Vector3 randomRotation = new Vector3(
                    Random.Range(10f, 80f),   // Pitch variation (always from above, 10-80 degrees from horizontal)
                    Random.Range(0f, 360f),   // Yaw full rotation
                    0f                        // No roll
                );
                sceneLight.transform.rotation = Quaternion.Euler(randomRotation);
            }
        }
    }

    public void RandomizeFurniture()
    {
        // Clear previously spawned background objects
        foreach (GameObject obj in spawnedBackgroundObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedBackgroundObjects.Clear();

        if (backgroundObjectPrefabs == null || backgroundObjectPrefabs.Length == 0) return;

        // Spawn random background objects
        for (int i = 0; i < 5; i++)
        {
            // Select random prefab
            GameObject randomPrefab = backgroundObjectPrefabs[Random.Range(0, backgroundObjectPrefabs.Length)];

            // Random position within defined area
            Vector3 randomPosition = new Vector3(
                Random.Range(backgroundAreaMin.x, backgroundAreaMax.x),
                Random.Range(backgroundAreaMin.y, backgroundAreaMax.y),
                Random.Range(backgroundAreaMin.z, backgroundAreaMax.z)
            );

            // Spawn the object
            GameObject spawnedObject = Instantiate(randomPrefab, randomPosition, Quaternion.identity);
            spawnedBackgroundObjects.Add(spawnedObject);

            // Randomize rotation if enabled
            if (randomizeRotation)
            {
                Vector3 randomRotation = new Vector3(
                    0f,                           // No pitch rotation for furniture
                    Random.Range(0f, 360f),       // Random yaw rotation
                    0f                            // No roll rotation
                );
                spawnedObject.transform.rotation = Quaternion.Euler(randomRotation);
            }
        }
    }
    public void RandomizeItems()
    {
        if (itemObjects == null || itemObjects.Length == 0) return;

        foreach (Rigidbody item in itemObjects)
        {
            if (item == null) continue;

            // Randomize position within defined area
            Vector3 randomPosition = new Vector3(
                Random.Range(itemAreaMin.x, itemAreaMax.x),
                Random.Range(itemAreaMin.y, itemAreaMax.y),
                Random.Range(itemAreaMin.z, itemAreaMax.z)
            );
            item.transform.position = randomPosition;
            item.transform.rotation = new Quaternion(0, 0, 0, 1);
            
            // Reset velocities
            item.linearVelocity = Vector3.zero;
            item.angularVelocity = Vector3.zero;
        }
    }

    public void RandomizeScene()
    {
        //RandomizeLighting();
        RandomizeFurniture();
        RandomizeItems();
    }
}
