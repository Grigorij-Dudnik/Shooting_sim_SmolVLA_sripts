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
        Debug.Log("Time to finish");
        if (robotControl != null)
        {
            robotControl.episodeComplete = true;
        }
        Destroy(gameObject); // Destroy projectile after the delay
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform == target)
        {
            // Projectile hit the target
            StartCoroutine(CompleteEpisodeAfterDelay());
        }
    }
}

