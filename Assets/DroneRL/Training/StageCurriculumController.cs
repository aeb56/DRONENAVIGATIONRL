using UnityEngine;
using Unity.MLAgents;

public class StageCurriculumController : MonoBehaviour
{
    public DroneTrainingEnv env;
    public int currentStage = -1; // current difficulty stage
    private float lastPoll;
    
    [Header("Stage Progression")]
    public int evalWindowEpisodes = 200;
    public int minEpisodesPerStage = 150;
    [Tooltip("Get starting stage from ML-Agents parameters")] public bool useExternalStageOnStart = true;
    
    private int stageStartEpisodes;
    private int stageStartSuccesses;
    private int stageStartCrashes;
    private int stageStartAgentCrashes;
    private int stageStartObstacleCrashes;
    private int stageStartTimeouts;

    // Stats for this stage (for UI display)
    public int EpisodesInStage => env != null ? (env.episodesCompleted - stageStartEpisodes) : 0;
    public int SuccessesInStage => env != null ? (env.successes - stageStartSuccesses) : 0;
    public int CrashesInStage => env != null ? (env.crashes - stageStartCrashes) : 0;
    public int AgentCrashesInStage => env != null ? (env.agentCrashes - stageStartAgentCrashes) : 0;
    public int ObstacleCrashesInStage => env != null ? (env.obstacleCrashes - stageStartObstacleCrashes) : 0;
    public int TimeoutsInStage => env != null ? (env.timeouts - stageStartTimeouts) : 0;
    public float SuccessRateInStage => EpisodesInStage > 0 ? (float)SuccessesInStage / EpisodesInStage : 0f;
    public float CollisionRateInStage => EpisodesInStage > 0 ? (float)CrashesInStage / EpisodesInStage : 0f;

    private void Start()
    {
        if (env == null) env = FindObjectOfType<DroneTrainingEnv>();
        // Load saved stage if we have one
        if (PlayerPrefs.HasKey("DroneRL_LastStage"))
        {
            currentStage = PlayerPrefs.GetInt("DroneRL_LastStage", 0);
        }
        ApplyFromEnvParams(true);
    }

    private void Update()
    {
        if (Time.time - lastPoll > 1.0f)
        {
            ApplyFromEnvParams(false);
            lastPoll = Time.time;
        }
    }

    private void ApplyFromEnvParams(bool force)
    {
        if (env == null) return;
        int stage = currentStage;
        // Only get stage from ML-Agents on startup, not during training
        if (currentStage < 0 && useExternalStageOnStart)
        {
            try
            {
                var ep = Academy.Instance?.EnvironmentParameters;
                if (ep != null)
                {
                    stage = Mathf.RoundToInt(ep.GetWithDefault("stage", 0));
                }
            }
            catch { }
        }

        if (force || stage != currentStage)
        {
            currentStage = Mathf.Clamp(stage, 0, 9);
            ApplyStage(currentStage);
            Debug.Log($"[Curriculum] Applied stage {currentStage}");
        }

        // Check if we should move to next stage
        TryAutoPromote();

        // Log stats for TensorBoard
        try
        {
            var rec = Academy.Instance.StatsRecorder;
            rec.Add("Stage/Current", currentStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/Episodes", EpisodesInStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/SuccessRate", SuccessRateInStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/CollisionRate", CollisionRateInStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/AgentCrashes", AgentCrashesInStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/ObstacleCrashes", ObstacleCrashesInStage, StatAggregationMethod.MostRecent);
            rec.Add($"Stage/{currentStage}/Timeouts", TimeoutsInStage, StatAggregationMethod.MostRecent);
        }
        catch { }
    }

    private void ApplyStage(int s)
    {
        // Defaults common to all stages
        env.enableWind = false;
        env.maxWindStrength = 0f;
        env.enableAgentAgentCollisions = false;
        env.minInterDroneSpacing = 6f;
        env.enableDomainRandomization = false;
        env.goalStartRadiusFraction = 0.2f; // will be adjusted below
        env.goalFullRadiusEpisodes = 1;     // use current radius factor immediately

        // Map stage to environment settings
        switch (s)
        {
            case 0: // Hover & Stabilize
                env.baseObstacleCount = 0;
                env.episodeLength = 45f;
                env.goalStartRadiusFraction = 0.0f; // goal near spawn
                env.minInterDroneSpacing = 6f;
                SetAgentParams(successRadius: 1.5f);
                break;

            case 1: // Short open goals
                env.baseObstacleCount = 0;
                env.episodeLength = 45f;
                env.goalStartRadiusFraction = 0.15f; // ~short distances
                env.minInterDroneSpacing = 8f;
                SetAgentParams(successRadius: 3.0f);
                break;

            case 2: // Long open goals
                env.baseObstacleCount = 0;
                env.episodeLength = 60f;
                env.goalStartRadiusFraction = 0.35f; // longer distances
                env.minInterDroneSpacing = 10f;
                SetAgentParams(successRadius: 2.5f);
                break;

            case 3: // Sparse static obstacles
                env.baseObstacleCount = 6;
                env.episodeLength = 70f;
                env.goalStartRadiusFraction = 0.4f;
                SetAgentParams(successRadius: 2.5f);
                break;

            case 4: // Moderate static clutter
                env.baseObstacleCount = 12;
                env.episodeLength = 75f;
                env.goalStartRadiusFraction = 0.45f;
                SetAgentParams(successRadius: 2.0f);
                break;

            case 5: // Narrow passages
                env.baseObstacleCount = 18;
                env.episodeLength = 80f;
                env.goalStartRadiusFraction = 0.5f;
                SetAgentParams(successRadius: 1.8f);
                break;

            case 6: // Moving obstacles + enable drone collisions
                env.baseObstacleCount = 14; // mix; moving via level>=2 in RebuildObstacles
                env.level = 2;
                env.episodeLength = 85f;
                env.goalStartRadiusFraction = 0.5f;
                env.enableAgentAgentCollisions = true;
                SetAgentParams(successRadius: 1.8f);
                break;

            case 7: // DR-Lite
                env.baseObstacleCount = 14; env.level = 2;
                env.episodeLength = 90f;
                env.goalStartRadiusFraction = 0.55f;
                env.enableDomainRandomization = true;
                env.enableWind = true; env.maxWindStrength = 1.5f;
                ApplyNoiseToAgents(0.06f);
                SetAgentParams(successRadius: 1.6f);
                break;

            case 8: // DR-Hard
                env.baseObstacleCount = 16; env.level = 2;
                env.episodeLength = 95f;
                env.goalStartRadiusFraction = 0.6f;
                env.enableDomainRandomization = true;
                env.enableWind = true; env.maxWindStrength = 2.0f;
                ApplyNoiseToAgents(0.07f);
                SetAgentParams(successRadius: 1.2f);
                break;

            case 9: // Final evaluation
                env.baseObstacleCount = 20; env.level = 2;
                env.episodeLength = 95f;
                env.goalStartRadiusFraction = 0.6f;
                env.enableAgentAgentCollisions = true;
                env.enableDomainRandomization = true;
                env.enableWind = true; env.maxWindStrength = 2.0f;
                ApplyNoiseToAgents(0.07f);
                SetAgentParams(successRadius: 1.2f);
                break;
        }

        // Ensure collision matrix and reward config are applied
        ApplyCollisionMatrix();
        env.ApplyRewardConfig();

        // Reset stage counters
        if (env != null)
        {
            stageStartEpisodes = env.episodesCompleted;
            stageStartSuccesses = env.successes;
            stageStartCrashes = env.crashes;
            stageStartAgentCrashes = env.agentCrashes;
            stageStartObstacleCrashes = env.obstacleCrashes;
            stageStartTimeouts = env.timeouts;
        }
    }

    private void ApplyNoiseToAgents(float sigma)
    {
        var agents = FindObjectsOfType<DroneAgent>();
        foreach (var a in agents)
        {
            a.positionNoise = sigma;
            a.velocityNoise = sigma * 0.5f;
            a.gyroNoise = sigma * 0.25f;
            a.attitudeNoise = Mathf.Rad2Deg * (sigma * 0.02f);
        }
    }

    private void SetAgentParams(float successRadius)
    {
        var agents = FindObjectsOfType<DroneAgent>();
        foreach (var a in agents)
        {
            a.successRadius = successRadius;
        }
    }

    private void ApplyCollisionMatrix()
    {
        if (env == null) return;
        // Put all agents on the same DRONE layer and set ignore based on toggle
        var agents = FindObjectsOfType<DroneAgent>();
        foreach (var a in agents) if (a != null) a.gameObject.layer = 8;
        Physics.IgnoreLayerCollision(8, 8, !env.enableAgentAgentCollisions);
    }

    // Threshold tables for promotion (by stage index)
    private static readonly float[] SuccThresh = {0.80f, 0.70f, 0.70f, 0.70f, 0.70f, 0.65f, 0.60f, 0.60f, 0.60f};
    private static readonly float[] CollMax   = {1.10f, 0.20f, 1.10f, 0.20f, 0.15f, 0.15f, 0.20f, 0.20f, 1.10f};

    public (float succ, float coll) GetCurrentThresholds()
    {
        int idx = Mathf.Clamp(currentStage, 0, SuccThresh.Length - 1);
        return (SuccThresh[idx], CollMax[idx]);
    }

    private void TryAutoPromote()
    {
        if (env == null || currentStage >= 9) return; // final stage has no promotion
        int ep = env.episodesCompleted - stageStartEpisodes;
        if (ep < Mathf.Max(evalWindowEpisodes, minEpisodesPerStage)) return;
        int succ = env.successes - stageStartSuccesses;
        int crashes = env.crashes - stageStartCrashes;
        float successRate = ep > 0 ? (float)succ / ep : 0f;
        float collisionRate = ep > 0 ? (float)crashes / ep : 0f;

        int idx = Mathf.Clamp(currentStage, 0, SuccThresh.Length-1);
        if (successRate >= SuccThresh[idx] && collisionRate <= CollMax[idx])
        {
            currentStage++;
            ApplyStage(currentStage);
            // Persist stage so a domain reload or scene reload can resume at the promoted stage
            PlayerPrefs.SetInt("DroneRL_LastStage", currentStage);
            PlayerPrefs.Save();
        }
    }
}


