using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

/// <summary>
/// Main drone agent for ML training. Works with the training environment and flight controller.
/// Observations: basic drone state + raycast sensors if enabled.
/// Actions: pitch, roll, yaw, throttle (4 continuous values).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(RLFlightAdapter))]
public class DroneAgent : Agent
{
    [Header("Bindings (set by training environment)")]
    public Transform goal;
    public DroneTrainingEnv env;
    public RLRewardConfig rewardConfig;

    [Header("Raycast Sensors")]
    public bool useRaycasts = true;
    [Range(1, 64)] public int rayCount = 8;
    public float rayDistance = 15f;
    public LayerMask obstacleMask = ~0;
    [Tooltip("Don't raycast against this drone's own colliders")]
    public bool ignoreSelfColliders = true;
    [Tooltip("Include other drones as obstacles in raycasts")]
    public bool includeAgentsInRaycasts = true;
    [Tooltip("Offset ray start point up a bit to avoid self-collision")]
    public float rayOriginUpOffset = 0.05f;

    [Header("Sensor Noise (for realism)")]
    [Tooltip("Add noise to velocity readings")]
    [Range(0f, 0.5f)] public float velocityNoise = 0.02f;
    [Tooltip("Add noise to gyro readings")]
    [Range(0f, 0.2f)] public float gyroNoise = 0.01f;
    [Tooltip("Add noise to position (like GPS error)")]
    [Range(0f, 2f)] public float positionNoise = 0.05f;
    [Tooltip("Add noise to attitude readings")]
    [Range(0f, 5f)] public float attitudeNoise = 0.2f;
    public bool logObservationSizeMismatch = true;

    private Rigidbody rb;
    private RLFlightAdapter adapter;
    private float prevGoalDistance;
    private float idleTimer;
    
    // Extra sensor systems
    private DroneAdvancedSensors advancedSensors;
    private DroneNavigationSystem navigationSystem;

    // When to call it a success
    [Header("Success Settings")]
    [Tooltip("How close to goal counts as success")]
    public float successRadius = 1.5f;
    [Tooltip("How many steps to stay near goal before success")]
    public int successHoldSteps = 10;
    private int successHoldCounter = 0;

    // Stop episodes that get stuck
    [Header("Stall Detection")]
    [Tooltip("End episode if no progress for this many steps")]
    public int maxStallSteps = 500;  // 50 seconds at 10Hz
    private int noProgressSteps = 0;
    private float lastDistanceCheck;

    // Stats for display
    public int EpisodeIndex { get; private set; }
    public int StepCount { get; private set; }
    public float LastStepReward { get; private set; }
    public float CurrentDistanceToGoal => DistanceToGoal();
    [Tooltip("How many times we succeeded")]
    public int SuccessCount { get; internal set; }
    [Tooltip("How many times we failed")]
    public int FailureCount { get; internal set; }
    [Tooltip("Crashes this episode")]
    public int CollisionCount { get; private set; }
    [Tooltip("Closest we got to goal this episode")]
    public float MinDistanceThisEpisode { get; private set; } = float.MaxValue;

    // Track unsafe behavior
    [Header("Safety Tracking")]
    [Tooltip("Max tilt before we consider it unsafe")]
    public float maxTiltSafeDeg = 60f;
    [Tooltip("Max speed before we consider it unsafe")]
    public float maxSpeedSafe = 12f;
    [Tooltip("How many times we went unsafe this episode")]
    public int SafetyExceedanceCount { get; private set; }
    [Tooltip("Fastest speed this episode")]
    public float MaxSpeedThisEpisode { get; private set; }

    // Don't count tiny bumps as crashes
    [Header("Collision Settings")]
    [Tooltip("How fast impact needs to be to count as crash")]
    public float minCrashSpeed = 1.5f;
    [Tooltip("Grace period after spawn before crashes count")]
    public float minCrashTimeSinceStart = 0.5f;

    private float episodeStartTime;

    // Track different reward components
    public float EpDistanceRewardSum { get; private set; }
    public float EpAliveRewardSum { get; private set; }
    public float EpStabilityRewardSum { get; private set; }
    public float EpEnergyPenaltySum { get; private set; }
    public float EpTiltPenaltySum { get; private set; }
    public float EpGoalReward { get; private set; }
    public float EpCollisionPenalty { get; private set; }

    public event System.Action<DroneAgent> OnStepInfoUpdated; // Called when step info updates

    // How many observations we send to ML-Agents
    // Basic stuff: position(3) + velocity(3) + rotation(4) + angular velocity(3) + goal direction(3) + distance(1) + time(1) + throttle(1) = 19
    private const int BaseObs = 19;
    private const int AdvancedSensorObs = 38; // Extra sensors add 38 more

    public int CurrentObservationCount => BaseObs + (useRaycasts ? (rayCount + 2) : 0) + AdvancedSensorObs;

    private void Awake()
    {
        // Make sure ML-Agents behavior component is set up properly
        EnsureBehaviorParameters();
    }

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        adapter = GetComponent<RLFlightAdapter>();
        
        // Set up extra sensor systems
        advancedSensors = GetComponent<DroneAdvancedSensors>();
        if (advancedSensors == null) advancedSensors = gameObject.AddComponent<DroneAdvancedSensors>();
        
        navigationSystem = GetComponent<DroneNavigationSystem>();
        if (navigationSystem == null) navigationSystem = gameObject.AddComponent<DroneNavigationSystem>();
        
        if (env == null) env = GetComponentInParent<DroneTrainingEnv>();
        // Check behavior parameters again in case settings changed
        EnsureBehaviorParameters();
    }

    public override void OnEpisodeBegin()
    {
        if (env != null) env.ResetAgent(this); else ResetLocallyFallback();
        prevGoalDistance = DistanceToGoal();
        lastDistanceCheck = prevGoalDistance;
        idleTimer = 0f;
        adapter.ResetDynamics();
        StepCount = 0;
        LastStepReward = 0f;
        EpisodeIndex++;
        CollisionCount = 0;
        MinDistanceThisEpisode = prevGoalDistance;
        successHoldCounter = 0;
        noProgressSteps = 0;
        SafetyExceedanceCount = 0;
        MaxSpeedThisEpisode = 0f;
        episodeStartTime = Time.time;
        EpDistanceRewardSum = 0f;
        EpAliveRewardSum = 0f;
        EpStabilityRewardSum = 0f;
        EpEnergyPenaltySum = 0f;
        EpTiltPenaltySum = 0f;
        EpGoalReward = 0f;
        EpCollisionPenalty = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 pos = transform.position;
        Vector3 velLocal = transform.InverseTransformDirection(rb.velocity);
        Vector3 angVelLocal = transform.InverseTransformDirection(rb.angularVelocity);
        Quaternion rot = transform.rotation;

        // Add realistic sensor noise
        if (positionNoise > 0f)
        {
            pos += new Vector3(
                (Random.value - 0.5f) * positionNoise,
                (Random.value - 0.5f) * positionNoise * 0.5f, // less noise in altitude
                (Random.value - 0.5f) * positionNoise
            );
        }
        
        if (velocityNoise > 0f)
        {
            velLocal += new Vector3(
                (Random.value - 0.5f) * velocityNoise,
                (Random.value - 0.5f) * velocityNoise,
                (Random.value - 0.5f) * velocityNoise
            );
        }
        
        if (gyroNoise > 0f)
        {
            angVelLocal += new Vector3(
                (Random.value - 0.5f) * gyroNoise,
                (Random.value - 0.5f) * gyroNoise,
                (Random.value - 0.5f) * gyroNoise
            );
        }
        
        if (attitudeNoise > 0f)
        {
            float noiseRad = attitudeNoise * Mathf.Deg2Rad;
            Quaternion attNoise = Quaternion.Euler(
                (Random.value - 0.5f) * noiseRad,
                (Random.value - 0.5f) * noiseRad,
                (Random.value - 0.5f) * noiseRad
            );
            rot = rot * attNoise;
        }

        // Arena-relative normalized position (x,z) and height
        Vector3 rel = Vector3.zero;
        if (env != null)
        {
            float halfX = Mathf.Max(1f, env.arenaSize.x * 0.5f);
            float halfZ = Mathf.Max(1f, env.arenaSize.y * 0.5f);
            rel = new Vector3(Mathf.Clamp(pos.x / halfX, -1f, 1f), Mathf.Clamp(pos.y / Mathf.Max(1f, env.ceilingHeight), 0f, 1f), Mathf.Clamp(pos.z / halfZ, -1f, 1f));
        }
        sensor.AddObservation(rel); // 3

        sensor.AddObservation(velLocal); // 3
        sensor.AddObservation(new Vector3(rot.x, rot.y, rot.z)); // 3
        sensor.AddObservation(rot.w); // 1
        sensor.AddObservation(angVelLocal); // 3

        Vector3 toGoal = goal ? (goal.position - pos) : Vector3.zero;
        float dist = toGoal.magnitude;
        Vector3 dirToGoalLocal = dist > 1e-4f ? transform.InverseTransformDirection(toGoal.normalized) : Vector3.zero;
        sensor.AddObservation(dirToGoalLocal); // 3
        sensor.AddObservation(Mathf.Clamp(dist / (env != null ? env.maxGoalDistanceHint : 50f), 0f, 1f)); // 1
        sensor.AddObservation(env != null ? env.GetTimeRemaining01() : 1f); // 1
        sensor.AddObservation(adapter.GetNormalizedThrottle()); // 1

        if (useRaycasts)
        {
            for (int i = 0; i < rayCount; i++)
            {
                float ang = (360f / rayCount) * i;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * transform.forward;
                Vector3 origin = transform.position + transform.up * rayOriginUpOffset;
                float normalized = 1f;
                var hits = Physics.RaycastAll(origin, dir, rayDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (ignoreSelfColliders && IsSelfCollider(hit.collider))
                        continue;
                    if (!includeAgentsInRaycasts && IsAgentCollider(hit.collider))
                        continue;
                    normalized = hit.distance / Mathf.Max(0.0001f, rayDistance);
                    break;
                }
                sensor.AddObservation(Mathf.Clamp01(normalized));
            }
            // Down ray
            {
                Vector3 origin = transform.position + transform.up * rayOriginUpOffset;
                float normalized = 1f;
                var hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (ignoreSelfColliders && IsSelfCollider(hit.collider))
                        continue;
                    if (!includeAgentsInRaycasts && IsAgentCollider(hit.collider))
                        continue;
                    normalized = hit.distance / Mathf.Max(0.0001f, rayDistance);
                    break;
                }
                sensor.AddObservation(Mathf.Clamp01(normalized));
            }
            // Up ray
            {
                Vector3 origin = transform.position + transform.up * rayOriginUpOffset;
                float normalized = 1f;
                var hits = Physics.RaycastAll(origin, Vector3.up, rayDistance, obstacleMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (ignoreSelfColliders && IsSelfCollider(hit.collider))
                        continue;
                    if (!includeAgentsInRaycasts && IsAgentCollider(hit.collider))
                        continue;
                    normalized = hit.distance / Mathf.Max(0.0001f, rayDistance);
                    break;
                }
                sensor.AddObservation(Mathf.Clamp01(normalized));
            }
        }
        
        // Add advanced sensor observations
        if (advancedSensors != null)
        {
            advancedSensors.AddSensorObservations(sensor);
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var a = actionBuffers.ContinuousActions;
        if (a.Length < 4) return; // safety
        Vector4 act = new Vector4(
            Mathf.Clamp(a[0], -1f, 1f), // pitch
            Mathf.Clamp(a[1], -1f, 1f), // roll
            Mathf.Clamp(a[2], -1f, 1f), // yaw
            Mathf.Clamp(a[3], -1f, 1f)  // throttle
        );
        adapter.ApplyAction(act);

        float r = 0f;
        float dist = DistanceToGoal();
        
        // Track minimum distance for stats
        if (dist < MinDistanceThisEpisode)
            MinDistanceThisEpisode = dist;

        // Safety tracking (tilt/speed exceedances)
        float tiltNow = Vector3.Angle(transform.up, Vector3.up);
        float speedNow = rb != null ? rb.velocity.magnitude : 0f;
        if (speedNow > MaxSpeedThisEpisode) MaxSpeedThisEpisode = speedNow;
        if (tiltNow > maxTiltSafeDeg || speedNow > maxSpeedSafe)
        {
            SafetyExceedanceCount++;
        }

        // Success detection - must hold position near goal
        if (dist < successRadius)
        {
            successHoldCounter++;
            if (successHoldCounter >= successHoldSteps)
            {
                // Strong terminal reward for success
                float successReward = rewardConfig ? rewardConfig.goalReachedBonus : 50f;
                AddReward(successReward);
                EpGoalReward += successReward;
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add("Drone/MinDistance", MinDistanceThisEpisode, StatAggregationMethod.Average);
                recorder.Add("Drone/Collisions", CollisionCount, StatAggregationMethod.Average);
                EndEpisodeSafe(true);
                return;
            }
        }
        else
        {
            successHoldCounter = 0;
        }

        // Stall detection - no meaningful progress (more lenient)
        if (Mathf.Abs(dist - lastDistanceCheck) < 0.1f) // 10cm progress required (was 2cm)
        {
            noProgressSteps++;
            if (noProgressSteps >= maxStallSteps)
            {
                float stallPenalty = (rewardConfig ? rewardConfig.idlePenalty * 2f : 0.1f);
                AddReward(-stallPenalty);
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add("Drone/MinDistance", MinDistanceThisEpisode, StatAggregationMethod.Average);
                recorder.Add("Drone/Collisions", CollisionCount, StatAggregationMethod.Average);
                EndEpisodeSafe(false);
                return;
            }
        }
        else
        {
            noProgressSteps = 0;
            lastDistanceCheck = dist;
        }

        // Simplified reward shaping - focus on main objectives
        
        // 1. Primary: Distance progress (main signal)
        float delta = prevGoalDistance - dist;
        float distReward = (rewardConfig ? rewardConfig.distanceRewardScale : 0.1f) * delta;
        r += distReward;
        EpDistanceRewardSum += distReward;
        
        // 2. Small alive bonus to encourage exploration
        float aliveReward = rewardConfig ? rewardConfig.aliveBonusPerStep : 0.001f;
        r += aliveReward;
        EpAliveRewardSum += aliveReward;
        
        // 3. Proximity bonus only when very close (final approach)
        if (dist < 3f)
        {
            float proximityBonus = (rewardConfig ? rewardConfig.distanceRewardScale * 0.3f : 0.03f) * (1f - dist / 3f);
            r += proximityBonus;
            EpDistanceRewardSum += proximityBonus;
        }
        
        // 4. Gentle penalties only for extreme behaviors
        float tilt = tiltNow;
        if (tilt > 45f) // Only penalize extreme tilts
        {
            float tiltPenalty = (rewardConfig ? rewardConfig.tiltPenaltyScale * 0.5f : 0.0005f) * (tilt - 45f) / 45f;
            r -= tiltPenalty;
            EpTiltPenaltySum += tiltPenalty;
        }

        // 5. Energy/throttle penalty (encourage efficiency)
        if (rewardConfig)
        {
            float thr01 = Mathf.Clamp01(adapter != null ? adapter.GetNormalizedThrottle() : 0f);
            // Penalize proportional to throttle squared for stronger discouragement at high thrust
            float energyPenalty = rewardConfig.energyPenaltyScale * thr01 * thr01;
            r -= energyPenalty;
            EpEnergyPenaltySum += energyPenalty;
        }

        // Stability shaping (fades out after configured episodes)
        float stabilityAdded = 0f;
        if (rewardConfig && EpisodeIndex <= rewardConfig.stabilityShapingEpisodes)
        {
            float fade = 1f - (float)EpisodeIndex / Mathf.Max(1, rewardConfig.stabilityShapingEpisodes);
            float alt = transform.position.y;
            float altErr = Mathf.Abs(alt - rewardConfig.altitudeTarget);
            float altReward = Mathf.Clamp01(1f - altErr / Mathf.Max(0.001f, rewardConfig.altitudeTolerance));
            float upright = Mathf.Clamp01(1f - tilt * 0.5f);
            stabilityAdded = rewardConfig.stabilityRewardScale * fade * (0.6f * altReward + 0.4f * upright);
            r += stabilityAdded;
            EpStabilityRewardSum += stabilityAdded;
        }
        
        // Remove redundant idle timer (already handled by stall detection)
        AddReward(r);
        prevGoalDistance = dist;
        LastStepReward = r;
        StepCount++;
        
        // Periodic diagnostic logging to inspect reward composition
        if (StepCount % 1000 == 0)
        {
            Debug.Log($"[DroneAgent] Ep {EpisodeIndex} Step {StepCount} DistReward {distReward:F4} Alive {aliveReward:F4} Stability {stabilityAdded:F4} StepTotal {r:F4} Cum {GetCumulativeReward():F3}");
        }
        OnStepInfoUpdated?.Invoke(this);

        // Goal reward now handled only by GoalZone trigger to avoid double bonus.
        if (env && !env.IsInsideBounds(transform.position))
        {
            AddReward(-(rewardConfig ? rewardConfig.outOfBoundsPenalty : 1f));
            var recorder = Academy.Instance.StatsRecorder;
            recorder.Add("Drone/MinDistance", MinDistanceThisEpisode, StatAggregationMethod.Average);
            recorder.Add("Drone/Collisions", CollisionCount, StatAggregationMethod.Average);
            recorder.Add("Drone/SafetyExceed", SafetyExceedanceCount, StatAggregationMethod.Average);
            recorder.Add("Drone/MaxSpeed", MaxSpeedThisEpisode, StatAggregationMethod.Average);
            EndEpisodeSafe(false);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var c = actionsOut.ContinuousActions;
        if (c.Length < 4) return;
        c[0] = Input.GetAxis("Vertical");
        c[1] = Input.GetAxis("Horizontal");
        c[2] = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        float thr = 0.5f + (Input.GetKey(KeyCode.Space) ? 0.5f : 0f) - (Input.GetKey(KeyCode.LeftControl) ? 0.5f : 0f);
        c[3] = Mathf.Clamp(thr * 2f - 1f, -1f, 1f);
    }

    // Request decisions at a higher frequency for responsive inference
    private float decisionTimer = 0f;
    private float decisionInterval = 0.01f; // 100Hz for ultra-smooth inference response
    
    private void FixedUpdate()
    {
        decisionTimer += Time.fixedDeltaTime;
        if (decisionTimer >= decisionInterval)
        {
            RequestDecision();
            decisionTimer = 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider && goal && collision.collider.transform == goal) return; // ignore goal sphere
        if (collision.collider != null && collision.collider.isTrigger) return; // ignore trigger contacts
        // Ignore early, gentle touches to avoid false positives
        if (Time.time - episodeStartTime < minCrashTimeSinceStart) return;
        // Ignore collisions with other drones (agent-agent contact should not count)
        var otherAgent = collision.collider.GetComponentInParent<DroneAgent>();
        if (otherAgent != null && otherAgent != this)
        {
            // Count as agent-agent crash only if env allows it
            if (env != null && env.enableAgentAgentCollisions)
            {
                if (env) env.RegisterCrash(true);
                EndEpisodeSafe(false);
            }
            return;
        }
        if (collision.relativeVelocity.magnitude < minCrashSpeed) return;
        
        CollisionCount++;
        float penalty = -(rewardConfig ? rewardConfig.crashPenalty : 10f);
        AddReward(penalty);
        EpCollisionPenalty += -penalty;
        LastStepReward = penalty; // record for HUD
        OnStepInfoUpdated?.Invoke(this);
        
        var recorder = Academy.Instance.StatsRecorder;
        recorder.Add("Drone/MinDistance", MinDistanceThisEpisode, StatAggregationMethod.Average);
        recorder.Add("Drone/Collisions", CollisionCount, StatAggregationMethod.Average);
        if (env) env.RegisterCrash(false);
        EndEpisodeSafe(false);
    }

    private float DistanceToGoal() => goal ? Vector3.Distance(transform.position, goal.position) : 0f;

    private void ResetLocallyFallback()
    {
        transform.position = Vector3.up * 1.5f;
        transform.rotation = Quaternion.identity;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    internal void EndEpisodeSafe(bool success)
    {
        if (env) env.ReportEpisodeEnd(this, success);
    if (success) SuccessCount++; else FailureCount++;
        EndEpisode();
    }

    /// <summary>
    /// Optional manual reset for success/failure tallies (e.g. between training phases).
    /// Call from an environment script instead of assigning the properties directly.
    /// </summary>
    public void ResetOutcomeCounters()
    {
        SuccessCount = 0;
        FailureCount = 0;
        CollisionCount = 0;
    }

    private bool IsSelfCollider(Collider c)
    {
        if (c == null) return false;
        return c.transform.IsChildOf(transform);
    }

    private bool IsAgentCollider(Collider c)
    {
        if (c == null) return false;
        return c.GetComponentInParent<DroneAgent>() != null;
    }

    private void EnsureBehaviorParameters()
    {
        var bp = GetComponent<BehaviorParameters>();
        if (!bp)
        {
            bp = gameObject.AddComponent<BehaviorParameters>();
        }

        // Behavior name
        if (string.IsNullOrEmpty(bp.BehaviorName) || bp.BehaviorName != "DroneAgent")
            bp.BehaviorName = "DroneAgent";

        // Observation size
        int expected = CurrentObservationCount;
        if (bp.BrainParameters.VectorObservationSize != expected)
        {
            bp.BrainParameters.VectorObservationSize = expected; // serialized so inspector shows real size pre-play
        }

        // Continuous action spec (size 4)
        if (bp.BrainParameters.ActionSpec.NumContinuousActions != 4)
        {
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (rayCount < 1) rayCount = 1;
        EnsureBehaviorParameters(); // run also in edit mode so inspector values stay accurate
    }
#endif
}
