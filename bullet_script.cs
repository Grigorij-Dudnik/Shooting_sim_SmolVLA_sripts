using UnityEngine;


public class bullet_script : MonoBehaviour
{
    private Transform target;
    private RobotControl robotControl;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        robotControl = FindAnyObjectByType<RobotControl>();
        target = robotControl.target;
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Add this to your projectile script
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Projectile hit: " + other.name);
        if (other.transform == target)
        {
            // Projectile hit the target
            Debug.Log("Direct hit!");
            if (robotControl != null)
            {
                robotControl.episodeComplete = true;
            }
            Destroy(gameObject); // Destroy projectile
        }
    }
    // Should be here. Without that function OnTriggerEnter not works
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Projectile hit: " + collision.gameObject.name);
        if (collision.transform == target)
        {
            // Projectile hit the target
            Debug.Log("Direct hit!");
            if (robotControl != null)
            {
                robotControl.episodeComplete = true;
            }
            Destroy(gameObject); // Destroy projectile
        }
    }
}

