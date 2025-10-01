using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;

/// <summary>
/// Curriculum learning - starts easy and gets harder as drone improves
/// Changes difficulty automatically based on how well it's doing
/// </summary>
public class DroneCurriculumLearning : MonoBehaviour
{
    [Header("Curriculum Stages")]
    public List<CurriculumStage> stages = new List<CurriculumStage>();
    public int currentStageIndex = 2; // Start at Stage 3 (Obstacle Avoidance) - moderate difficulty
    
    [Header("Performance Tracking")]
    public int evaluationWindow = 100; // Larger window for stability
    public float advanceThreshold = 0.65f; // 65% success rate - much higher confidence required
    public float regressionThreshold = 0.02f; // Only regress on complete failure (2%)
    public bool adaptiveDifficulty = false; // Temporarily disable to stop thrashing
    [Tooltip("Minimum episodes to spend in each stage before allowing regression")]
    public int minEpisodesPerStage = 150; // Much longer minimum time
    [Tooltip("Disable regression entirely after this many total episodes")]
    public int disableRegressionAfter = 5000;
    
    [Header("Metrics")]
    public float currentSuccessRate = 0f;
    public float averageReward = 0f;
    public float averageEpisodeLength = 0f;
    public int totalEpisodes = 0;
    public int episodesInCurrentStage = 0; // Track time in current stage
    
    private Queue<EpisodeResult> recentEpisodes = new Queue<EpisodeResult>();
    private DroneTrainingEnv environment;
    private AdvancedTrainingEnvironments advancedEnv;
    
    [System.Serializable]
    public class CurriculumStage
    {
        public string stageName;
        public float goalDistance = 10f;
        public int obstacleCount = 5;
        public float windStrength = 0f;
        public bool enableSensorNoise = false;
        public AdvancedTrainingEnvironments.ScenarioType scenario;
        public AdvancedTrainingEnvironments.WeatherCondition weather;
        public int requiredSuccesses = 50;
        public string description;
    }
    
    [System.Serializable]
    public class EpisodeResult
    {
        public bool success;
        public float reward;
        public float duration;
        public float minDistanceToGoal;
        public int collisions;
    }
    
    void Start()
    {
        environment = FindObjectOfType<DroneTrainingEnv>();
        advancedEnv = FindObjectOfType<AdvancedTrainingEnvironments>();
        InitializeDefaultStages();
        ApplyCurrentStage();
    }
    
    void InitializeDefaultStages()
    {
        if (stages.Count == 0)
        {
            stages.Add(new CurriculumStage
            {
                stageName = "Basic Hover",
                goalDistance = 5f,
                obstacleCount = 0,
                windStrength = 0f,
                enableSensorNoise = false,
                scenario = AdvancedTrainingEnvironments.ScenarioType.Basic,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Clear,
                requiredSuccesses = 30,
                description = "Learn basic stability and hovering"
            });
            
            stages.Add(new CurriculumStage
            {
                stageName = "Simple Navigation",
                goalDistance = 15f,
                obstacleCount = 3,
                windStrength = 2f,
                enableSensorNoise = false,
                scenario = AdvancedTrainingEnvironments.ScenarioType.Basic,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Clear,
                requiredSuccesses = 50,
                description = "Navigate to nearby goals with few obstacles"
            });
            
            stages.Add(new CurriculumStage
            {
                stageName = "Obstacle Avoidance",
                goalDistance = 20f,   // Reduced from 25f
                obstacleCount = 5,    // Reduced from 8
                windStrength = 2.5f,  // Reduced from 3f
                enableSensorNoise = false, // Start without noise
                scenario = AdvancedTrainingEnvironments.ScenarioType.Basic,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Clear, // Keep clear weather
                requiredSuccesses = 40, // Reduced from 60
                description = "Navigate through moderate obstacle fields"
            });
            
            stages.Add(new CurriculumStage
            {
                stageName = "Urban Environment",
                goalDistance = 40f,
                obstacleCount = 15,
                windStrength = 5f,
                enableSensorNoise = true,
                scenario = AdvancedTrainingEnvironments.ScenarioType.Urban,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Cloudy,
                requiredSuccesses = 70,
                description = "Navigate in complex urban environments"
            });
            
            stages.Add(new CurriculumStage
            {
                stageName = "Adverse Weather",
                goalDistance = 50f,
                obstacleCount = 20,
                windStrength = 8f,
                enableSensorNoise = true,
                scenario = AdvancedTrainingEnvironments.ScenarioType.Urban,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Rainy,
                requiredSuccesses = 80,
                description = "Handle challenging weather conditions"
            });
            
            stages.Add(new CurriculumStage
            {
                stageName = "Emergency Scenarios",
                goalDistance = 60f,
                obstacleCount = 25,
                windStrength = 10f,
                enableSensorNoise = true,
                scenario = AdvancedTrainingEnvironments.ScenarioType.Emergency,
                weather = AdvancedTrainingEnvironments.WeatherCondition.Stormy,
                requiredSuccesses = 90,
                description = "Handle system failures and emergency situations"
            });
        }
    }
    
    public void RecordEpisodeResult(bool success, float reward, float duration, float minDistance, int collisions)
    {
        var result = new EpisodeResult
        {
            success = success,
            reward = reward,
            duration = duration,
            minDistanceToGoal = minDistance,
            collisions = collisions
        };
        
        recentEpisodes.Enqueue(result);
        totalEpisodes++;
        episodesInCurrentStage++; // Track episodes in current stage
        
        // Maintain sliding window
        while (recentEpisodes.Count > evaluationWindow)
        {
            recentEpisodes.Dequeue();
        }
        
        UpdateMetrics();
        
        // Periodic status logging for long training runs (every 50 episodes for more frequent monitoring)
        if (totalEpisodes % 50 == 0)
        {
            var stage = stages[currentStageIndex];
            Debug.Log($"[CURRICULUM] Episode {totalEpisodes} | Stage {currentStageIndex + 1}/{stages.Count}: {stage.stageName} | Success Rate: {currentSuccessRate:P1} | Avg Reward: {averageReward:F2} | Episodes in Stage: {episodesInCurrentStage} | Window: {recentEpisodes.Count}/{evaluationWindow}");
        }
        
        // Log curriculum metrics to TensorBoard for graphs
        var recorder = Academy.Instance.StatsRecorder;
        recorder.Add("Curriculum/StageIndex", currentStageIndex + 1, StatAggregationMethod.Average);
        recorder.Add("Curriculum/SuccessRate", currentSuccessRate, StatAggregationMethod.Average);
        recorder.Add("Curriculum/AverageReward", averageReward, StatAggregationMethod.Average);
        recorder.Add("Curriculum/EpisodesInStage", episodesInCurrentStage, StatAggregationMethod.Average);
        
        if (adaptiveDifficulty)
        {
            EvaluateProgressAndAdjust();
        }
    }
    
    void UpdateMetrics()
    {
        if (recentEpisodes.Count == 0) return;
        
        int successCount = 0;
        float totalReward = 0f;
        float totalDuration = 0f;
        
        foreach (var episode in recentEpisodes)
        {
            if (episode.success) successCount++;
            totalReward += episode.reward;
            totalDuration += episode.duration;
        }
        
        currentSuccessRate = (float)successCount / recentEpisodes.Count;
        averageReward = totalReward / recentEpisodes.Count;
        averageEpisodeLength = totalDuration / recentEpisodes.Count;
    }
    
    void EvaluateProgressAndAdjust()
    {
        if (recentEpisodes.Count < evaluationWindow) return;
        
        var currentStage = stages[currentStageIndex];
        
        // Check for advancement
        if (currentSuccessRate >= advanceThreshold)
        {
            int recentSuccesses = 0;
            var episodeArray = recentEpisodes.ToArray();
            for (int i = episodeArray.Length - currentStage.requiredSuccesses; i < episodeArray.Length && i >= 0; i++)
            {
                if (episodeArray[i].success) recentSuccesses++;
            }
            
            if (recentSuccesses >= currentStage.requiredSuccesses * 0.8f)
            {
                AdvanceToNextStage();
            }
        }
        // Check for regression - but only after spending minimum time in current stage AND not too late in training
        else if (currentSuccessRate < regressionThreshold && currentStageIndex > 0 && episodesInCurrentStage >= minEpisodesPerStage && totalEpisodes < disableRegressionAfter)
        {
            RegressToPreviousStage();
        }
    }
    
    void AdvanceToNextStage()
    {
        if (currentStageIndex < stages.Count - 1)
        {
            currentStageIndex++;
            ApplyCurrentStage();
            var stage = stages[currentStageIndex];
            Debug.Log($"[CURRICULUM] Advanced to Stage {currentStageIndex + 1}/{stages.Count}: {stage.stageName} | Obstacles: {stage.obstacleCount} | Wind: {stage.windStrength} | Goal Distance: {stage.goalDistance}");
            
            // Reset metrics for new stage
            recentEpisodes.Clear();
            episodesInCurrentStage = 0; // Reset stage timer;
        }
        else
        {
            Debug.Log("[CURRICULUM] Training completed! Agent has mastered all stages.");
        }
    }
    
    void RegressToPreviousStage()
    {
        currentStageIndex--;
        ApplyCurrentStage();
        var stage = stages[currentStageIndex];
        Debug.Log($"[CURRICULUM] Regressed to Stage {currentStageIndex + 1}/{stages.Count}: {stage.stageName} | Performance declined, reducing difficulty");
        
        episodesInCurrentStage = 0; // Reset stage timer
        // Clear some recent episodes to avoid immediate re-regression
        for (int i = 0; i < evaluationWindow / 4 && recentEpisodes.Count > 0; i++)
        {
            recentEpisodes.Dequeue();
        }
    }
    
    void ApplyCurrentStage()
    {
        if (currentStageIndex >= stages.Count) return;
        
        var stage = stages[currentStageIndex];
        
        // Apply to training environment
        if (environment != null)
        {
            environment.goalStartRadiusFraction = Mathf.Clamp01(stage.goalDistance / 100f);
            environment.baseObstacleCount = stage.obstacleCount;
            environment.maxWindStrength = stage.windStrength;
        }
        
        // Apply to advanced environment
        if (advancedEnv != null)
        {
            advancedEnv.SetScenario(stage.scenario);
            advancedEnv.SetWeather(stage.weather);
        }
        
        // Apply sensor noise to agents
        var agents = FindObjectsOfType<DroneAgent>();
        foreach (var agent in agents)
        {
            if (stage.enableSensorNoise)
            {
                agent.velocityNoise = 0.1f;
                agent.positionNoise = 0.3f;
                agent.gyroNoise = 0.05f;
                agent.attitudeNoise = 1f;
            }
            else
            {
                agent.velocityNoise = 0f;
                agent.positionNoise = 0f;
                agent.gyroNoise = 0f;
                agent.attitudeNoise = 0f;
            }
        }
        
        // Log curriculum application details for console monitoring during long training runs
        Debug.Log($"[CURRICULUM] Applied Stage {currentStageIndex + 1}/{stages.Count}: {stage.stageName} | Obstacles: {stage.obstacleCount} | Wind: {stage.windStrength:F1} | Goal Distance: {stage.goalDistance} | Sensor Noise: {stage.enableSensorNoise} | Scenario: {stage.scenario}");
    }
    
    public void SetStage(int stageIndex)
    {
        if (stageIndex >= 0 && stageIndex < stages.Count)
        {
            currentStageIndex = stageIndex;
            ApplyCurrentStage();
        }
    }
    
    public string GetCurrentStageInfo()
    {
        if (currentStageIndex < stages.Count)
        {
            var stage = stages[currentStageIndex];
            return $"Stage {currentStageIndex + 1}/{stages.Count}: {stage.stageName}\n" +
                   $"Success Rate: {currentSuccessRate:F2} (Target: {advanceThreshold:F2})\n" +
                   $"Average Reward: {averageReward:F2}\n" +
                   $"Description: {stage.description}";
        }
        return "Curriculum completed!";
    }
    
    // Called by DroneAgent when episode ends
    public void OnEpisodeEnd(DroneAgent agent, bool success)
    {
        float reward = agent.GetCumulativeReward();
        float duration = Time.time; // Simplified - you'd track actual episode time
        float minDistance = agent.MinDistanceThisEpisode;
        int collisions = agent.CollisionCount;
        
        RecordEpisodeResult(success, reward, duration, minDistance, collisions);
    }
    
    void OnGUI()
    {
        // Display curriculum info in game
        GUI.BeginGroup(new Rect(Screen.width - 300, 10, 280, 200));
        GUI.Box(new Rect(0, 0, 280, 200), "Curriculum Progress");
        GUI.Label(new Rect(10, 25, 260, 150), GetCurrentStageInfo());
        GUI.EndGroup();
    }
}
