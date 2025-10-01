using UnityEngine;

/// <summary>
/// Handles pilot input -> high level setpoints, enforcing per-mode limits and smoothing.
/// </summary>
[RequireComponent(typeof(QuadControllerCascaded))]
public class FlightModeManager : MonoBehaviour, IFlightController
{
    public enum Mode { Cine, Normal, Sport, AltitudeHold }
    public Mode mode = Mode.Normal;

    public Vector2 DesiredHorizontalVelocity { get; set; }
    public float DesiredVerticalSpeed { get; set; }
    public float DesiredYawRateDeg { get; set; }
    public bool PositionHoldEnabled { get; set; } = true;

    private QuadControllerCascaded controller;
    private DroneTuning tuning;

    private Vector2 stickVelTarget;
    private float verticalTarget;
    private float yawRateTarget;

    private void Awake()
    {
        controller = GetComponent<QuadControllerCascaded>();
        tuning = controller.tuning;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) mode = Mode.Cine;
        if (Input.GetKeyDown(KeyCode.Alpha2)) mode = Mode.Normal;
        if (Input.GetKeyDown(KeyCode.Alpha3)) mode = Mode.Sport;
        if (Input.GetKeyDown(KeyCode.Alpha4)) mode = Mode.AltitudeHold;

        // Read raw sticks (reuse Unity default axes)
        float sx = Input.GetAxis("Horizontal");   // left/right
        float sy = Input.GetAxis("Vertical");     // forward/back
        float vz = 0f;
        if (Input.GetKey(KeyCode.Space)) vz += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) vz -= 1f;
        float yawStick = 0f; if (Input.GetKey(KeyCode.Q)) yawStick -= 1f; if (Input.GetKey(KeyCode.E)) yawStick += 1f;

        // Expo
        float expo = tuning.inputExpo;
        sx = Mathf.Sign(sx) * Mathf.Pow(Mathf.Abs(sx), 1f + expo);
        sy = Mathf.Sign(sy) * Mathf.Pow(Mathf.Abs(sy), 1f + expo);
        yawStick = Mathf.Sign(yawStick) * Mathf.Pow(Mathf.Abs(yawStick), 1f + expo);

        float maxXY = mode switch { Mode.Cine => tuning.maxPosVelCmdCine, Mode.Sport => tuning.maxPosVelCmdSport, _ => tuning.maxPosVelCmdNormal };
        float maxClimb = mode switch { Mode.Cine => tuning.maxClimbCine, Mode.Sport => tuning.maxClimbSport, _ => tuning.maxClimbNormal };
        float maxYaw = mode switch { Mode.Cine => tuning.maxYawRateCine, Mode.Sport => tuning.maxYawRateSport, _ => tuning.maxYawRateNormal };

        Vector2 desiredVel = new Vector2(sx, sy) * maxXY; // in local forward/right axes (will convert later)
        float desiredVz = vz * maxClimb;
        float desiredYawRate = yawStick * maxYaw;

        // Slew limiting
        float slew = mode switch { Mode.Cine => tuning.cineInputSlew, Mode.Sport => tuning.sportInputSlew, _ => tuning.normalInputSlew };
        float k = 1f - Mathf.Exp(-slew * Time.deltaTime);
        stickVelTarget = Vector2.Lerp(stickVelTarget, desiredVel, k);
        verticalTarget = Mathf.Lerp(verticalTarget, desiredVz, k);
        yawRateTarget = Mathf.Lerp(yawRateTarget, desiredYawRate, k);

        DesiredHorizontalVelocity = stickVelTarget;
        DesiredVerticalSpeed = verticalTarget;
        DesiredYawRateDeg = yawRateTarget;
        PositionHoldEnabled = (mode != Mode.AltitudeHold); // altitude hold disables full position hold horizontally (still velocity control)
    }
}
