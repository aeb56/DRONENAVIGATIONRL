using UnityEngine;

/// <summary>
/// Interface so RL or other systems can override higher level setpoints cleanly.
/// </summary>
public interface IFlightController
{
    /// <summary> Desired horizontal velocity (world XZ) in m/s. </summary>
    Vector2 DesiredHorizontalVelocity { get; set; }
    /// <summary> Desired vertical speed (positive up) in m/s. </summary>
    float DesiredVerticalSpeed { get; set; }
    /// <summary> Desired yaw rate in deg/s. </summary>
    float DesiredYawRateDeg { get; set; }
    /// <summary> If true the controller will also hold position when sticks centered (Position Hold mode). </summary>
    bool PositionHoldEnabled { get; set; }
}
