using UnityEngine;

[CreateAssetMenu(fileName = "RLRewardConfig", menuName = "DroneRL/RL Reward Config", order = 0)]
public class RLRewardConfig : ScriptableObject
{
    [Header("Positive")]
    public float aliveBonusPerStep = 0.001f; // small constant reward for staying alive
    public float distanceRewardScale = 0.1f; // stronger signal for progress (was 0.07)
    public float goalReachedBonus = 20.0f; // moderate terminal reward for smoother curves
    [Tooltip("Extra shaping for early learning: scaled reward for staying level & near target altitude.")]
    public float stabilityRewardScale = 0.01f; // increased for better early learning
    [Tooltip("Target altitude for hover shaping.")]
    public float altitudeTarget = 3f;
    [Tooltip("Altitude tolerance (meters) giving full stability reward inside; decays after.")]
    public float altitudeTolerance = 2.0f; // more forgiving (was 1.0)
    [Tooltip("Number of early episodes (per agent) to apply full stability shaping; fades out afterwards.")]
    public int stabilityShapingEpisodes = 1500; // longer curriculum (was 900)

    [Header("Negative")]
    public float crashPenalty = 10.0f; // strong discouragement (was 1.5)
    public float outOfBoundsPenalty = 5.0f; // moderate penalty (was 1.0)
    public float timeoutPenalty = 1.0f; // mild timeout penalty (was 0.2)
    public float energyPenaltyScale = 0.001f; // reduced to allow more aggressive maneuvers
    public float tiltPenaltyScale = 0.0005f; // reduced, let physics handle stability
    public float idlePenalty = 0.01f; // increased idle penalty (was 0.0075)

    [Header("Episode")]
    public float idleTimeout = 3f;
    public float goalRadius = 1.0f;
}
