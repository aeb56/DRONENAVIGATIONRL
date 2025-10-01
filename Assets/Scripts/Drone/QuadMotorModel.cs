using UnityEngine;

/// <summary>
/// Simulates individual motors with lag and thrust forces
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class QuadMotorModel : MonoBehaviour
{
    public struct Motor
    {
        public Vector3 localPos;
        public float cmd;       // command [0,1]
        public float state;     // lagged state [0,1]
        public int yawSign;     // +1 or -1 for reaction torque direction
    }

    public Motor[] motors = new Motor[4];

    [Header("Runtime Values (read-only)")] public float totalThrustN;

    private Rigidbody rb;
    private DroneTuning tuning;
    private float invTau;

    public void Init(DroneTuning t)
    {
        tuning = t;
        rb = GetComponent<Rigidbody>();
        rb.mass = t.mass;
        rb.drag = 0.015f;
        rb.angularDrag = 0.04f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = new Vector3(0f, -0.01f, 0f);

        // X configuration (front +Z). Motor order: FL(+x,+z), FR(-x,+z), BR(-x,-z), BL(+x,-z)
        float L = t.armLength;
        motors[0].localPos = new Vector3( L, 0f,  L); motors[0].yawSign = +1;
        motors[1].localPos = new Vector3(-L, 0f,  L); motors[1].yawSign = -1;
        motors[2].localPos = new Vector3(-L, 0f, -L); motors[2].yawSign = +1;
        motors[3].localPos = new Vector3( L, 0f, -L); motors[3].yawSign = -1;
        invTau = 1f / Mathf.Max(0.01f, t.motorTimeConstant);
    }

    /// <summary>Immediately set motor internal states & commands (e.g., to hover) to avoid initial drop.</summary>
    public void PrimeMotors(float command)
    {
        for (int i = 0; i < motors.Length; i++)
        {
            var m = motors[i];
            m.cmd = Mathf.Clamp01(command);
            m.state = m.cmd;
            motors[i] = m;
        }
    }

    public void SetMotorCommands(float m0, float m1, float m2, float m3)
    {
        motors[0].cmd = Mathf.Clamp01(m0);
        motors[1].cmd = Mathf.Clamp01(m1);
        motors[2].cmd = Mathf.Clamp01(m2);
        motors[3].cmd = Mathf.Clamp01(m3);
    }

    private void FixedUpdate()
    {
        if (tuning == null || rb == null) return;
        float dt = Time.fixedDeltaTime;
        totalThrustN = 0f;

        for (int i = 0; i < motors.Length; i++)
        {
            var m = motors[i];
            // 1st order lag
            m.state = Mathf.Lerp(m.state, m.cmd, 1f - Mathf.Exp(-dt * invTau));
            float thrust = tuning.motorThrustCoefficient * m.state * m.state; // kT * cmd^2

            // Ground effect using altitude AGL via raycast
            if (Physics.Raycast(transform.position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, tuning.groundEffectHeight + 0.1f))
            {
                float h = hit.distance;
                if (h < tuning.groundEffectHeight)
                {
                    float factor = 1f + tuning.groundEffectMaxBoost * (1f - (h / Mathf.Max(0.0001f, tuning.groundEffectHeight)));
                    thrust *= factor;
                }
            }

            Vector3 worldPos = transform.TransformPoint(m.localPos);
            Vector3 force = transform.up * thrust;
            rb.AddForceAtPosition(force, worldPos, ForceMode.Force);

            // Yaw reaction torque
            float yawTorque = m.yawSign * thrust * tuning.yawTorqueCoefficient;
            rb.AddTorque(transform.up * yawTorque, ForceMode.Force);

            totalThrustN += thrust;
            motors[i] = m;
        }
    }
}
