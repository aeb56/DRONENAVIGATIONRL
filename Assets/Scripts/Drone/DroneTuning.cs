using UnityEngine;

[CreateAssetMenu(fileName = "DroneTuning", menuName = "Drone/Drone Tuning Asset", order = 10)]
public class DroneTuning : ScriptableObject
{
    [Header("Physical")]
    public float mass = 0.23f; // slightly lighter for easier lift
    public float motorThrustCoefficient = 2.2f; // increased thrust headroom
    public float motorTimeConstant = 0.09f; // seconds
    public float armLength = 0.145f; // meters (approx 290mm diag)
    public float yawTorqueCoefficient = 0.02f; // reaction torque per N of thrust (tuned)
    public Vector3 bodyDragCd = new Vector3(0.12f, 0.18f, 0.10f);
    public Vector3 angularDamping = new Vector3(0.02f, 0.02f, 0.02f);
    public float groundEffectHeight = 0.3f;
    public float groundEffectMaxBoost = 0.12f; // 12%
    [Tooltip("Multiplier converting desired horizontal accel into tilt before clamping.")] public float horizontalAccelToTiltGain = 1.4f;

    [Header("Wind / Gusts")] 
    public Vector3 baseWind = Vector3.zero;
    public float gustStrength = 1.5f;
    public float gustFrequency = 0.1f; // Hz
    public bool windEnabled = false;

    [Header("Position PID")] public float Kp_pos = 1.6f; public float Ki_pos = 0.05f; public float Kd_pos = 0.1f; public float posILimit = 1f; public float maxPosVelCmdCine = 5f; public float maxPosVelCmdNormal = 12f; public float maxPosVelCmdSport = 18f;
    [Header("Velocity PID")] public float Kp_vel = 2.6f; public float Ki_vel = 0.2f; public float Kd_vel = 0.4f; public float velILimit = 2f;
    [Header("Attitude PID")] public float Kp_att = 8f; public float Kd_att = 0.2f;
    [Header("Rate PID")] public float Kp_rate = 0.25f; public float Ki_rate = 0.08f; public float Kd_rate = 0.005f; public float rateILimit = 0.4f;

    [Header("Flight Mode Limits (deg / speeds)")]
    public float maxTiltCine = 10f;
    public float maxTiltNormal = 25f;
    public float maxTiltSport = 35f;
    public float maxYawRateCine = 60f;      // deg/s
    public float maxYawRateNormal = 120f;
    public float maxYawRateSport = 200f;
    public float maxClimbCine = 2f;         // m/s
    public float maxClimbNormal = 4f;
    public float maxClimbSport = 4f;

    [Header("Input Shaping")]
    public float inputExpo = 0.3f; // simple expo for sticks
    public float cineInputSlew = 2f;
    public float normalInputSlew = 4f;
    public float sportInputSlew = 6f;

    [Header("Misc")]
    public float hoverThrottleGuess = 0.55f; // stored after first compute
    public bool sensorNoiseEnabled = false;
    public bool sensorLatencyEnabled = false;

    public float AbsoluteTiltHardLimitDeg = 45f;
}
