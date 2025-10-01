using UnityEngine;

public struct PIDState
{
    public float integral;
    public float lastError;
}

public static class PIDUtility
{
    public static float Step(ref PIDState state, float error, float Kp, float Ki, float Kd, float dt, float integralLimit)
    {
        state.integral += error * dt * Ki;
        if (integralLimit > 0f) state.integral = Mathf.Clamp(state.integral, -integralLimit, integralLimit);
        float deriv = (error - state.lastError) / Mathf.Max(dt, 1e-5f);
        state.lastError = error;
        return Kp * error + state.integral + Kd * deriv;
    }
}
