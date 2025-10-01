using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class QuadController : MonoBehaviour
{
    [Header("Thrust Settings")]
    [Tooltip("How much thrust relative to weight (2.2 = can lift 2.2x its weight)")]
    public float thrustToWeight = 2.2f;
    [Tooltip("How fast motors respond to changes")]
    public float motorTimeConstant = 0.12f;

    [Header("Flight Control")]
    public float maxTiltDegrees = 35f;     // max tilt angle
    public float yawRate = 120f;           // how fast it can turn
    public float kAttP = 8f;               // attitude control strength
    public float kAttD = 0.4f;             // damping to reduce oscillation
    public float kAttI = 0.2f;             // integral term for steady state
    public float iLimit = 0.6f;            // limit integral windup

    [Header("Physics & Drag")]
    public Vector3 bodyDrag = new Vector3(0.4f, 0.8f, 0.4f); // air resistance
    public float angularDamping = 0.3f;
    public float lateralDamping = 0.15f;
    [Tooltip("How close to ground before getting extra lift")]
    public float groundEffectHeight = 2.0f;
    public float groundEffectGain = 0.15f;

    [Header("Realism Features")]
    [Tooltip("Add sensor noise to make it more realistic")]
    [Range(0f, 0.1f)] public float sensorNoise = 0.02f;
    [Tooltip("How much wind affects the drone")]
    [Range(0f, 2f)] public float windSusceptibility = 1.0f;
    [Tooltip("Each motor has slightly different efficiency")]
    public Vector4 motorEfficiency = new Vector4(1.0f, 0.98f, 1.02f, 0.99f);
    [Tooltip("Battery level affects thrust power")]
    [Range(0.5f, 1f)] public float batteryLevel = 1.0f;
    [Range(0f, 1f)] public float throttle = 0.5f;  // thrust amount (0.5 = hover)
    [Range(-1f, 1f)] public float pitch;           // forward/back
    [Range(-1f, 1f)] public float roll;            // left/right
    [Range(-1f, 1f)] public float yaw;             // rotation

    private Rigidbody rb;
    private float currentThrust; // current thrust force
    private Vector3 attIntegral;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void SetInputs(float throttle, float pitch, float roll, float yaw)
    {
        this.throttle = Mathf.Clamp01(throttle);
        this.pitch = Mathf.Clamp(pitch, -1f, 1f);
        this.roll = Mathf.Clamp(roll, -1f, 1f);
        this.yaw = Mathf.Clamp(yaw, -1f, 1f);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        float g = Physics.gravity.magnitude;
        float hover = rb.mass * g;
        float maxTotalThrust = Mathf.Max(hover * thrustToWeight, hover); // don't go below hover

        // Make 0.5 throttle = hover
        float centered = (throttle - 0.5f) * 2f; // convert to -1 to 1
        float commanded = Mathf.Clamp(hover + centered * (maxTotalThrust - hover), 0f, maxTotalThrust);

        // Simulate motor lag and battery effects
        float k = 1f - Mathf.Exp(-Time.fixedDeltaTime / Mathf.Max(0.0001f, motorTimeConstant));
        float batteryModifier = Mathf.Pow(batteryLevel, 0.5f); // low battery = less thrust
        float thrustWithBattery = commanded * batteryModifier;
        currentThrust = Mathf.Lerp(currentThrust, thrustWithBattery, k);

        // Add some noise to make it realistic
        if (sensorNoise > 0f)
        {
            float noise = (Random.value - 0.5f) * 2f * sensorNoise * currentThrust;
            currentThrust += noise;
        }

        // Ground effect (extra lift near ground)
        float groundBoost = 0f;
        if (groundEffectHeight > 0f)
        {
            Ray ray = new Ray(transform.position, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, groundEffectHeight + 0.01f))
            {
                float h = Mathf.Max(0.02f, hit.distance);
                float factor = Mathf.Clamp01(1f - (h / groundEffectHeight));
                groundBoost = currentThrust * groundEffectGain * factor;

                // Reduce ground effect when descending so it can land
                float vLocalY = transform.InverseTransformDirection(rb.velocity).y;
                if (vLocalY < -0.5f)       groundBoost *= 0.3f;  // fast down
                else if (vLocalY < 0f)     groundBoost *= 0.6f;  // slow down
            }
        }

        // Apply thrust upward
        rb.AddForce(transform.up * (currentThrust + groundBoost), ForceMode.Force);

        // Desired tilt from inputs (keep current yaw to decouple)
        float desiredPitch = maxTiltDegrees * pitch;       // +pitch tilts nose down (forward)
        float desiredRoll = maxTiltDegrees * roll;         // +roll tilts right
        float currentYaw = transform.rotation.eulerAngles.y;

        Quaternion desired = Quaternion.Euler(desiredPitch, currentYaw, -desiredRoll);
        Quaternion current = transform.rotation;

        // Orientation error
        Quaternion qErr = desired * Quaternion.Inverse(current);
        qErr.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (!float.IsFinite(axis.x) || !float.IsFinite(axis.y) || !float.IsFinite(axis.z)) axis = Vector3.zero;

        Vector3 errVec = axis.normalized * (Mathf.Deg2Rad * angleDeg); // radians
        // Integral with simple windup clamp
        attIntegral += errVec * Time.fixedDeltaTime;
        attIntegral = Vector3.ClampMagnitude(attIntegral, iLimit);

        // Damping using angular velocity in world space (approx PD)
        Vector3 damping = -rb.angularVelocity * kAttD;

        // Torque for attitude
        Vector3 attTorque = errVec * kAttP + attIntegral + damping;

        // Yaw rate control about local Y
        float yawRateRad = yawRate * Mathf.Deg2Rad;
        float desiredYawRate = yaw * yawRateRad;
        Vector3 localAV = transform.InverseTransformDirection(rb.angularVelocity);
        float yawErr = desiredYawRate - localAV.y;
        Vector3 yawTorqueVec = transform.up * (yawErr * kAttP * 0.25f); // reuse scale

        // Add torques plus general angular damping
        rb.AddTorque(attTorque + yawTorqueVec - rb.angularVelocity * angularDamping, ForceMode.Force);

        // Motor asymmetry effects (realistic imperfections)
        if (motorEfficiency != Vector4.one)
        {
            float avgEff = (motorEfficiency.x + motorEfficiency.y + motorEfficiency.z + motorEfficiency.w) * 0.25f;
            Vector3 motorImbalance = new Vector3(
                (motorEfficiency.x - motorEfficiency.z) * 0.1f, // roll imbalance
                (motorEfficiency.y - motorEfficiency.w) * 0.1f, // pitch imbalance  
                (motorEfficiency.x + motorEfficiency.z - motorEfficiency.y - motorEfficiency.w) * 0.05f // yaw imbalance
            );
            rb.AddTorque(motorImbalance * currentThrust * 0.01f, ForceMode.Force);
        }

        // Aerodynamic body drag (approximate)
        Vector3 vLocal = transform.InverseTransformDirection(rb.velocity);
        Vector3 dragLocal = new Vector3(
            -vLocal.x * Mathf.Abs(vLocal.x) * bodyDrag.x,
            -vLocal.y * Mathf.Abs(vLocal.y) * bodyDrag.y,
            -vLocal.z * Mathf.Abs(vLocal.z) * bodyDrag.z
        );
        rb.AddForce(transform.TransformDirection(dragLocal), ForceMode.Force);

        // Additional lateral damping to reduce drift
        Vector3 lateral = rb.velocity - Vector3.Project(rb.velocity, Vector3.up);
        rb.AddForce(-lateral * lateralDamping, ForceMode.Acceleration);
    }
}
