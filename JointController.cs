using UnityEngine;

public class JointController : MonoBehaviour
{
    public Vector3 rotationDirection = Vector3.up;
    public float rotationSpeed = 0f; // degrees per second
    
    void Update()
    {
        if (rotationSpeed != 0f)
        {
            float rotationThisFrame = rotationSpeed * Time.deltaTime;
            transform.Rotate(rotationDirection, rotationThisFrame, Space.Self);
        }
    }
    
    public float GetNormalizedAngle()
    {
        float angle = Vector3.Dot(transform.localEulerAngles, rotationDirection);
        if (angle > 180) angle -= 360;
        return angle / 180f;
    }
}