using UnityEngine;

/// <summary>
/// Simple follow/first-person camera for the test drone. Attach to the Main Camera.
/// On play it will snap to the drone and optionally allow toggling view modes.
/// </summary>
public class DroneFollowCamera : MonoBehaviour
{
    public enum Mode { FirstPerson, Chase, Stadium }

    [Header("Target")]
    public Transform target; // Drone root

    [Header("First Person")]
    public Vector3 firstPersonLocalOffset = new Vector3(0f, 0.15f, 0.0f);

    [Header("Chase Offsets")]
    public float chaseDistance = 6.0f;       // behind the drone
    public float chaseHeight = 2.0f;         // above the drone
    public float chaseSideOffset = 0.0f;     // small lateral offset

    [Header("Chase Aim")]
    public float lookAhead = 10.0f;          // focus point in front of the drone
    public float lookUp = 1.2f;              // slight upward look to keep horizon visible

    [Header("Stadium Camera")]
    public float stadiumDistance = 12.0f;    // behind along velocity
    public float stadiumHeight = 6.0f;       // above world up
    public float stadiumSideOffset = 2.0f;   // lateral offset (broadcast vibe)
    public float stadiumLookAhead = 12.0f;   // aim point in front of drone
    public float stadiumLookUp = 1.2f;       // slight upward bias
    public float stadiumFov = 60f;           // camera FOV for stadium look

    [Header("Global constraints")]
    public float minHeightAboveTarget = 1.0f; // enforce camera is always above target

    [Header("Smoothing")]
    public float followSmooth = 8f;
    public float followSmoothVertical = 14f;

    public bool alwaysLookAtTarget = true;

    [Header("Aiming")]
    public bool focusOnDrone = true;      // if true, always aim at the drone (with slight upward bias)
    public float focusUpBias = 0.6f;      // meters above drone pivot to look at

    [Header("Mode")]
    public Mode mode = Mode.Stadium;
    public KeyCode toggleKey = KeyCode.V;

    private Rigidbody targetRb;
    private Camera cam;

    private void Start()
    {
        if (target == null)
        {
            var drone = GameObject.Find("DroneAgentPlaceholder");
            if (drone != null) target = drone.transform;
        }

        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            transform.position = DesiredPosition();
            if (alwaysLookAtTarget) transform.rotation = DesiredRotation();
        }

        cam = GetComponent<Camera>();
        ApplyFovIfNeeded();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            mode = mode == Mode.FirstPerson ? Mode.Stadium : (mode == Mode.Stadium ? Mode.Chase : Mode.FirstPerson);
            ApplyFovIfNeeded();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = DesiredPosition();

        // Exponential smoothing with stronger vertical response
        float kH = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        float kV = 1f - Mathf.Exp(-followSmoothVertical * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, desired.x, kH);
        pos.z = Mathf.Lerp(pos.z, desired.z, kH);
        pos.y = Mathf.Lerp(pos.y, desired.y, kV);

        // Enforce "always above" in non-first-person modes
        if (mode != Mode.FirstPerson)
        {
            float minY = target.position.y + minHeightAboveTarget;
            if (pos.y < minY) pos.y = minY;
        }

        transform.position = pos;

        if (alwaysLookAtTarget)
        {
            Quaternion desiredRot = DesiredRotation();
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, kH);
        }
    }

    private Vector3 DesiredPosition()
    {
        if (mode == Mode.FirstPerson)
        {
            // Cockpit-like view follows full orientation
            return target.TransformPoint(firstPersonLocalOffset);
        }
        else if (mode == Mode.Chase)
        {
            // Yaw-only chase position: behind and slightly above
            float yaw = target.eulerAngles.y;
            Quaternion yawOnly = Quaternion.Euler(0f, yaw, 0f);
            Vector3 planarOffset = new Vector3(chaseSideOffset, 0f, -chaseDistance);
            Vector3 p = target.position + yawOnly * planarOffset + Vector3.up * chaseHeight;
            // Ensure desired is never below the drone
            float minY = target.position.y + minHeightAboveTarget;
            if (p.y < minY) p.y = minY;
            return p;
        }
        else // Stadium
        {
            // Use velocity direction if available; fallback to forward
            Vector3 dir = (targetRb != null && targetRb.velocity.sqrMagnitude > 0.25f)
                ? targetRb.velocity.normalized
                : target.forward;

            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 p = target.position
                       - dir * stadiumDistance
                       + Vector3.up * stadiumHeight
                       + right * stadiumSideOffset;
            // Ensure desired is never below the drone
            float minY = target.position.y + minHeightAboveTarget;
            if (p.y < minY) p.y = minY;
            return p;
        }
    }

    private Quaternion DesiredRotation()
    {
        if (mode == Mode.FirstPerson)
        {
            return target.rotation;
        }
        else
        {
            // Choose focus point
            Vector3 lookPoint;
            if (focusOnDrone)
            {
                // Always look directly at the drone, with a slight upward bias for better framing
                lookPoint = target.position + Vector3.up * focusUpBias;
            }
            else
            {
                if (mode == Mode.Chase)
                {
                    lookPoint = target.position + target.forward * lookAhead + Vector3.up * lookUp;
                }
                else // Stadium
                {
                    Vector3 dir = (targetRb != null && targetRb.velocity.sqrMagnitude > 0.25f)
                        ? targetRb.velocity.normalized
                        : target.forward;
                    lookPoint = target.position + dir * stadiumLookAhead + Vector3.up * stadiumLookUp;
                }
            }

            Vector3 to = (lookPoint - transform.position);
            if (to.sqrMagnitude < 0.0001f)
            {
                // Fallback if extremely close: use forward or velocity
                to = (mode == Mode.Stadium && targetRb != null && targetRb.velocity.sqrMagnitude > 0.25f)
                    ? targetRb.velocity.normalized
                    : target.forward;
            }
            return Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }

    private void ApplyFovIfNeeded()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = (mode == Mode.Stadium) ? stadiumFov : cam.fieldOfView;
        }
    }
}
