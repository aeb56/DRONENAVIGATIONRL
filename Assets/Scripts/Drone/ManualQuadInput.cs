using UnityEngine;

[RequireComponent(typeof(QuadController))]
public class ManualQuadInput : MonoBehaviour
{
    public float inputSmoothing = 8f;
    public float throttleAdjustSpeed = 0.6f;     // base speed
    public float throttleAdjustSpeedFast = 2.0f; // when holding Shift (descend) or ArrowDown
    public float scrollSensitivity = 0.15f;      // mouse wheel throttle delta
    public bool clampTilt = true;

    private QuadController controller;
    private float targetThrottle = 0.5f;
    private float roll;
    private float pitch;
    private float yaw;

    private void Awake()
    {
        controller = GetComponent<QuadController>();
    }

    private void Update()
    {
        // Throttle controls:
        // - Space: ascend
        // - LeftCtrl / C / LeftShift / ArrowDown: descend
        // - ArrowUp: ascend
        // - Mouse wheel: fine adjust
        // - K: emergency cut (kill throttle)
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow))
            targetThrottle += throttleAdjustSpeed * Time.deltaTime;

        float descendSpeed = Input.GetKey(KeyCode.LeftShift) ? throttleAdjustSpeedFast : throttleAdjustSpeed;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.DownArrow))
            targetThrottle -= descendSpeed * Time.deltaTime;

        // Mouse wheel fine adjustment
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
            targetThrottle += scroll * scrollSensitivity;

        // Emergency cut
        if (Input.GetKeyDown(KeyCode.K))
            targetThrottle = 0f;

        targetThrottle = Mathf.Clamp01(targetThrottle);

        // Roll/Pitch from horizontal/vertical axes
        float targetRoll = Input.GetAxis("Horizontal"); // A/D
        float targetPitch = Input.GetAxis("Vertical");  // W/S

        // Yaw from Q/E
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.Q)) yawInput -= 1f;
        if (Input.GetKey(KeyCode.E)) yawInput += 1f;

        // Smooth inputs
        float k = 1f - Mathf.Exp(-inputSmoothing * Time.deltaTime);
        roll = Mathf.Lerp(roll, targetRoll, k);
        pitch = Mathf.Lerp(pitch, targetPitch, k);
        yaw = Mathf.Lerp(yaw, yawInput, k);

        if (clampTilt)
        {
            roll = Mathf.Clamp(roll, -1f, 1f);
            pitch = Mathf.Clamp(pitch, -1f, 1f);
        }

        controller.SetInputs(targetThrottle, pitch, roll, yaw);
    }

    private void OnDisable()
    {
        if (controller != null) controller.SetInputs(0.5f, 0f, 0f, 0f);
    }
}
