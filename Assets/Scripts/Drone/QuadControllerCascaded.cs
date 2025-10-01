using UnityEngine;

/// <summary>
/// Cascaded position->velocity->attitude->rate->motor controller.
/// Simplified initial implementation covering core logic & constraints.
/// </summary>
[RequireComponent(typeof(QuadMotorModel))]
[RequireComponent(typeof(SensorsSim))]
public class QuadControllerCascaded : MonoBehaviour
{
    public DroneTuning tuning;
    public FlightModeManager flightModeManager; // optional at runtime added

    // PID states
    private PIDState posXI, posYI, posZI;
    private PIDState velXI, velYI, velZI;
    private PIDState rateXI, rateYI, rateZI;

    private QuadMotorModel motors;
    private SensorsSim sensors;
    private Rigidbody rb;

    // Internal setpoints
    private Vector3 desiredWorldVel;
    private float desiredYawDeg;
    private float hoverThrustPerMotor; // approximate needed thrust per motor to hover
    private bool armed = false;
    private float armRamp = 0f;
    private float lastAlt;

    // Output (debug)
    public Vector3 desiredEulerDeg;
    public Vector3 desiredRatesRad;
    public float totalThrustCmd;
    public float[] motorCmds = new float[4];
    [Header("Debug")] public bool debugLogs = false;

    private void Awake()
    {
        motors = GetComponent<QuadMotorModel>();
        sensors = GetComponent<SensorsSim>();
        rb = GetComponent<Rigidbody>();
    }

    public void Init(DroneTuning t)
    {
    tuning = t;
    // Ensure component refs (Awake might not have run yet when called from editor menu)
    if (motors == null) motors = GetComponent<QuadMotorModel>();
    if (sensors == null) sensors = GetComponent<SensorsSim>();
    if (rb == null) rb = GetComponent<Rigidbody>();
        // Auto-upgrade tuning if legacy asset has weak motors
        if (t.motorThrustCoefficient < 2.0f)
            t.motorThrustCoefficient = 2.2f;
        if (t.mass > 0.24f)
            t.mass = 0.23f;
        if (motors != null) motors.Init(t);
    if (sensors != null) sensors.Init(t);
        hoverThrustPerMotor = (t.mass * Physics.gravity.magnitude) / 4f / t.motorThrustCoefficient; // Solve kT * cmd^2 = weight/4
        hoverThrustPerMotor = Mathf.Sqrt(Mathf.Clamp(hoverThrustPerMotor, 0f, 1f));
        t.hoverThrottleGuess = hoverThrustPerMotor;
    if (motors != null) motors.PrimeMotors(hoverThrustPerMotor); // start at hover
    armed = true;
    armRamp = 0f;
    }

    private void FixedUpdate()
    {
    if (tuning == null || flightModeManager == null) return;
        float dt = Time.fixedDeltaTime;

        // 1) Position (if hold enabled) -> velocity setpoint
        Vector3 currentPos = transform.position;
        Vector3 currentVel = rb.velocity;

        Vector3 horizontalVelSp;
        if (flightModeManager.PositionHoldEnabled && flightModeManager.DesiredHorizontalVelocity.sqrMagnitude < 0.01f)
        {
            // hold position: track error to zero (store target on first frame)
            if (!_haveHoldTarget) _holdTarget = currentPos; // lock
            Vector3 posErr = _holdTarget - currentPos; posErr.y = 0f;
            float vxCmd = PIDUtility.Step(ref posXI, posErr.x, tuning.Kp_pos, tuning.Ki_pos, tuning.Kd_pos, dt, tuning.posILimit);
            float vzCmd = PIDUtility.Step(ref posZI, posErr.z, tuning.Kp_pos, tuning.Ki_pos, tuning.Kd_pos, dt, tuning.posILimit);
            horizontalVelSp = new Vector3(vxCmd, 0f, vzCmd);
        }
        else
        {
            _haveHoldTarget = false;
            horizontalVelSp = new Vector3(flightModeManager.DesiredHorizontalVelocity.x, 0f, flightModeManager.DesiredHorizontalVelocity.y); // local forward mapped later
            // Convert from local input frame (x=right, y=forward) to world
            horizontalVelSp = transform.TransformDirection(new Vector3(horizontalVelSp.x, 0f, horizontalVelSp.z));
        }

        // Vertical velocity setpoint direct
        float verticalVelSp = flightModeManager.DesiredVerticalSpeed;
        // Altitude hold smoothing: if AltHold mode and near ground, bias upward a bit to lift
        if (flightModeManager.mode == FlightModeManager.Mode.AltitudeHold && currentPos.y < 0.2f)
        {
            verticalVelSp = Mathf.Max(verticalVelSp, 0.6f); // push up to take off
        }

        desiredWorldVel = new Vector3(horizontalVelSp.x, verticalVelSp, horizontalVelSp.z);

        // 2) Velocity -> Attitude (desired roll/pitch) + collective thrust adjustment
        Vector3 velErr = desiredWorldVel - currentVel;
    float axCmd = PIDUtility.Step(ref velXI, velErr.x, tuning.Kp_vel, tuning.Ki_vel, tuning.Kd_vel, dt, tuning.velILimit);
    float ayCmd = PIDUtility.Step(ref velZI, velErr.z, tuning.Kp_vel, tuning.Ki_vel, tuning.Kd_vel, dt, tuning.velILimit); // world Z
        float azCmd = PIDUtility.Step(ref velYI, velErr.y, tuning.Kp_vel, tuning.Ki_vel, tuning.Kd_vel, dt, tuning.velILimit);

        // Map desired world accelerations axCmd, ayCmd into roll/pitch assuming small angles:
        // ax_world ≈ g * tan(theta) -> theta ≈ ax/g,  ay_world ≈ -g * tan(phi) -> phi ≈ -ay/g (phi roll, theta pitch)
        float g = Physics.gravity.magnitude;
    float desiredPitch = Mathf.Clamp((axCmd / g) * tuning.horizontalAccelToTiltGain, -Mathf.Deg2Rad * tuning.AbsoluteTiltHardLimitDeg, Mathf.Deg2Rad * tuning.AbsoluteTiltHardLimitDeg); // around X
    float desiredRoll = Mathf.Clamp((-ayCmd / g) * tuning.horizontalAccelToTiltGain, -Mathf.Deg2Rad * tuning.AbsoluteTiltHardLimitDeg, Mathf.Deg2Rad * tuning.AbsoluteTiltHardLimitDeg); // around Z

        // Flight mode tilt limits
        float maxTiltDeg = flightModeManager.mode switch { FlightModeManager.Mode.Cine => tuning.maxTiltCine, FlightModeManager.Mode.Sport => tuning.maxTiltSport, _ => tuning.maxTiltNormal };
        float maxTiltRad = Mathf.Deg2Rad * maxTiltDeg;
        desiredPitch = Mathf.Clamp(desiredPitch, -maxTiltRad, maxTiltRad);
        desiredRoll = Mathf.Clamp(desiredRoll, -maxTiltRad, maxTiltRad);

        // Vertical acceleration command -> collective thrust offset
        float desiredUpAccel = azCmd + g; // need to counter gravity
        // Add takeoff assist if not moving vertically and close to ground
        if (currentPos.y < 0.15f && Mathf.Abs(currentVel.y) < 0.05f)
        {
            desiredUpAccel += 0.5f * g * (0.15f - currentPos.y); // proportional boost
        }
    float thrustTotal = Mathf.Clamp(desiredUpAccel * tuning.mass, 0f, tuning.mass * g * 3.0f);

        // Guarantee climb authority: if user commands positive vertical velocity ensure thrust margin above hover.
        float maxClimb = flightModeManager.mode switch { FlightModeManager.Mode.Cine => tuning.maxClimbCine, FlightModeManager.Mode.Sport => tuning.maxClimbSport, _ => tuning.maxClimbNormal };
        if (verticalVelSp > 0.05f)
        {
            float climbFrac = Mathf.Clamp01(verticalVelSp / Mathf.Max(0.01f, maxClimb));
            float minRequired = tuning.mass * g * (1f + 0.8f * climbFrac); // 80% extra at full climb command
            if (thrustTotal < minRequired) thrustTotal = minRequired;
        }
        else
        {
            // Small hover bias (5%) so it lifts off ground friction / contact
            float minHover = tuning.mass * g * 1.05f;
            if (thrustTotal < minHover) thrustTotal = minHover;
        }

        // Ground contact assist: if very low altitude and downward velocity small, add boost
        if (currentPos.y < 0.25f && currentVel.y < 0.2f)
        {
            thrustTotal = Mathf.Max(thrustTotal, tuning.mass * g * 1.15f);
        }

        // Yaw handling: integrate desired yaw rate
        desiredYawDeg += flightModeManager.DesiredYawRateDeg * dt;

        // 3) Attitude -> desired rates using PD on quaternion error
        Quaternion qCurrent = transform.rotation;
        Quaternion qDesired = Quaternion.Euler(Mathf.Rad2Deg * desiredPitch, desiredYawDeg, Mathf.Rad2Deg * desiredRoll);
        Quaternion qErr = qDesired * Quaternion.Inverse(qCurrent);
        qErr.ToAngleAxis(out float angleDeg, out Vector3 axis); if (angleDeg > 180f) { angleDeg -= 360f; }
        Vector3 attError = axis * Mathf.Deg2Rad * angleDeg; // in world frame
        // Project to local (body) frame for rate command
        Vector3 attErrorBody = transform.InverseTransformVector(attError);
        Vector3 desiredRates = attErrorBody * tuning.Kp_att - rb.angularVelocity * tuning.Kd_att; // rad/s

        // 4) Rate control (PID per axis)
        Vector3 rateErrBody = desiredRates - transform.InverseTransformVector(rb.angularVelocity);
        float tx = PIDUtility.Step(ref rateXI, rateErrBody.x, tuning.Kp_rate, tuning.Ki_rate, tuning.Kd_rate, dt, tuning.rateILimit);
        float ty = PIDUtility.Step(ref rateYI, rateErrBody.y, tuning.Kp_rate, tuning.Ki_rate, tuning.Kd_rate, dt, tuning.rateILimit);
        float tz = PIDUtility.Step(ref rateZI, rateErrBody.z, tuning.Kp_rate, tuning.Ki_rate, tuning.Kd_rate, dt, tuning.rateILimit);

        // 5) Proper mixer using orthogonal coefficient sets for X configuration
        float collectivePerMotor = thrustTotal / 4f; // base thrust (N)
        float L = Mathf.Max(0.01f, tuning.armLength);
        // Compute thrust adjustments a,b,c per derivation
        float a = tx / (4f * L); // pitch component
        float b = tz / (4f * L); // roll component
        float c = ty / (4f * Mathf.Max(1e-4f, tuning.yawTorqueCoefficient)); // yaw component

        // Coeff arrays (index 0..3): FL, FR, BR, BL
        int[] pitchCoeff = { +1, +1, -1, -1 }; // uses z sign
        int[] rollCoeff  = { +1, -1, -1, +1 }; // uses x sign
        int[] yawCoeff   = { +1, -1, +1, -1 }; // given yawSign pattern

        float kT = tuning.motorThrustCoefficient;
        for (int i = 0; i < 4; i++)
        {
            float thrust = collectivePerMotor + a * pitchCoeff[i] + b * rollCoeff[i] + c * yawCoeff[i];
            thrust = Mathf.Max(0.02f * collectivePerMotor, thrust); // keep small positive to preserve control authority
            motorCmds[i] = Mathf.Sqrt(Mathf.Clamp01(thrust / kT));
        }

        // Hard attitude safety clamp
        Vector3 euler = qCurrent.eulerAngles;
        float rollAbs = Mathf.Abs(Normalize180(euler.z));
        float pitchAbs = Mathf.Abs(Normalize180(euler.x));
        if (rollAbs > tuning.AbsoluteTiltHardLimitDeg || pitchAbs > tuning.AbsoluteTiltHardLimitDeg)
        {
            for (int i = 0; i < 4; i++) motorCmds[i] = Mathf.Min(motorCmds[i], tuning.hoverThrottleGuess); // reduce to hover to recover
        }

        // Arm ramp (only allow increase above hover, never below) first 0.3s
        if (armed && armRamp < 1f)
        {
            armRamp += dt * 3.3f; // ~0.3s
            for (int i = 0; i < 4; i++)
            {
                float hover = hoverThrustPerMotor;
                if (motorCmds[i] < hover) motorCmds[i] = hover; // enforce hover minimum during ramp
            }
        }

        motors.SetMotorCommands(motorCmds[0], motorCmds[1], motorCmds[2], motorCmds[3]);

        desiredEulerDeg = new Vector3(Mathf.Rad2Deg * desiredPitch, desiredYawDeg, Mathf.Rad2Deg * desiredRoll);
        desiredRatesRad = desiredRates;
        totalThrustCmd = thrustTotal;

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[QuadCtrl] vSpY={verticalVelSp:F2} thrust={thrustTotal:F2} hover={(tuning.mass * g):F2} cmd0={motorCmds[0]:F2}");
        }
    }

    private bool _haveHoldTarget = false;
    private Vector3 _holdTarget;

    private float Normalize180(float deg)
    {
        deg %= 360f; if (deg > 180f) deg -= 360f; if (deg < -180f) deg += 360f; return deg;
    }
}
