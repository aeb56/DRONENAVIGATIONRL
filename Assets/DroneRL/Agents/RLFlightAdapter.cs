using UnityEngine;

/// <summary>
/// Connects ML-Agents actions to the drone flight controller.
/// Converts AI outputs to flight commands and handles resets.
/// </summary>
[RequireComponent(typeof(QuadController))]
[RequireComponent(typeof(Rigidbody))]
public class RLFlightAdapter : MonoBehaviour
{
    [Header("Command Limits")]
    [Tooltip("Max tilt command from AI")]
    public float maxTiltCmd = 0.5f;  // keep it stable
    [Tooltip("Max yaw rate from AI")]
    public float maxYawCmd = 0.5f;   // keep it stable
    [Tooltip("Throttle range after conversion")]
    public Vector2 throttleRange01 = new Vector2(0f, 1f);

    private QuadController controller;
    private Rigidbody rb;

    private void Awake()
    {
        controller = GetComponent<QuadController>();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Take AI action and send to flight controller
    /// Actions: [0]=pitch, [1]=roll, [2]=yaw, [3]=throttle (all -1 to 1)
    /// </summary>
    public void ApplyAction(Vector4 action)
    {
        float aPitch = Mathf.Clamp(action.x, -1f, 1f) * maxTiltCmd;
        float aRoll  = Mathf.Clamp(action.y, -1f, 1f) * maxTiltCmd;
        float aYaw   = Mathf.Clamp(action.z, -1f, 1f) * maxYawCmd;
        // Convert throttle from -1..1 to 0..1
        float aThr   = Mathf.InverseLerp(-1f, 1f, Mathf.Clamp(action.w, -1f, 1f));
        // Apply throttle range
        aThr = Mathf.Clamp01(Mathf.Lerp(throttleRange01.x, throttleRange01.y, aThr));

        controller.SetInputs(aThr, aPitch, aRoll, aYaw);
    }

    /// <summary>
    /// Reset drone physics at start of new episode
    /// </summary>
    public void ResetDynamics(bool keepHeight = false)
    {
        if (rb == null) return;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Gently neutralize tilt, keep yaw and optionally keep height orientation-wise
        Vector3 e = transform.rotation.eulerAngles;
        float yaw = e.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    /// <summary>
    /// Normalized thrust output for observation/reward (0..1).
    /// </summary>
    public float GetNormalizedThrottle() => controller != null ? controller.throttle : 0f;
}
