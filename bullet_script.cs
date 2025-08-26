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

    // Coroutine to handle delay after hit
    private System.Collections.IEnumerator CompleteEpisodeAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        if (robotControl != null)
        {
            robotControl.episodeComplete = true;
        }
        Destroy(gameObject); // Destroy projectile after the delay
    }

    void OnCollisionEnter(Collision collision)
    {
        // handle only the first collision
        var col = GetComponent<Collider>();
        if (col != null && !col.enabled) return;
        if (col != null) col.enabled = false;

        bool isTarget = target != null && (collision.transform == target || collision.transform.IsChildOf(target));
        
        // Check for bad episode conditions - first object hit is not the target or target is hit but not standing
        if (!isTarget || (isTarget && !IsTargetStanding()))
        {
            if (robotControl != null && !robotControl.inferenceMode) robotControl.HandleBadEpisode();
            return;
        }

        // If we reach here, it's a good episode - target hit and standing
        StartCoroutine(CompleteEpisodeAfterDelay());
    }

    private bool IsTargetStanding()
    {
        if (target == null) return false;

        // Check if target is roughly upright (Y axis pointing up)
        float uprightThreshold = 0.866f; // cos(30°) ≈ 0.866
        return Vector3.Dot(target.up, Vector3.up) > uprightThreshold;
    }
}

