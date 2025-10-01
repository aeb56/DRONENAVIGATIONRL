using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;

/// <summary>
/// Simple, reliable environment setup for multi-drone training
/// Focuses only on core environment creation
/// </summary>
public class SimpleEnvironmentSetup
{
    [MenuItem("Tools/DroneRL/ Simple Environment Setup", priority = 1)]
    public static void CreateSimpleEnvironment()
    {
        if (EditorUtility.DisplayDialog("Create Training Environment", 
            "This will create a basic multi-drone training environment.\n\nProceed?", 
            "Yes", "Cancel"))
        {
            DoSimpleSetup();
        }
    }
    
    [MenuItem("Tools/DroneRL/🔧 Fix All Drone Settings", priority = 2)]
    public static void FixAllDroneSettings()
    {
        FixExistingDrones();
    }
    
    static void DoSimpleSetup()
    {
        try
        {
            // 1. Create main arena
            GameObject arena = new GameObject("TrainingArena");
            
            // 2. Add basic environment
            var env = arena.AddComponent<DroneTrainingEnv>();
            env.arenaSize = new Vector2(60f, 60f);
            env.ceilingHeight = 20f;
            env.agentCount = 6;
            env.episodeLength = 90f;
            env.baseObstacleCount = 12;
            env.enableWind = true;
            env.proceduralArenaGeometry = true;
            env.proceduralObstacles = true;
            
            // 3. Try to add multi-drone component
            try 
            {
                var multiEnv = arena.AddComponent<MultiDroneTrainingEnv>();
                multiEnv.droneCount = 6;
                multiEnv.currentScenario = MultiDroneTrainingEnv.TrainingScenario.PathFollowing;
                Debug.Log(" Added MultiDroneTrainingEnv component");
            }
            catch 
            {
                Debug.LogWarning("⚠️ MultiDroneTrainingEnv not found, using basic DroneTrainingEnv only");
            }
            
            // 4. Create reward config
            CreateBasicRewardConfig(env);
            
            // 5. Position camera
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(0, 15, -30);
                Camera.main.transform.LookAt(Vector3.zero);
            }
            
            Selection.activeGameObject = arena;
            EditorUtility.DisplayDialog("Success!", 
                "Basic training environment created!\n\n" +
                "• Arena: 60x60m\n" +
                "• 6 drone slots\n" +
                "• Procedural obstacles\n" +
                "• Wind enabled\n\n" +
                "Now run 'Fix All Drone Settings' to optimize existing drones.", 
                "Great!");
                
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Setup failed: {e.Message}", "OK");
            Debug.LogError($"Simple setup error: {e}");
        }
    }
    
    static void CreateBasicRewardConfig(DroneTrainingEnv env)
    {
        // Create folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        
        try
        {
            string path = "Assets/Resources/BasicRewardConfig.asset";
            RLRewardConfig config = ScriptableObject.CreateInstance<RLRewardConfig>();
            
            // Optimized values
            config.aliveBonusPerStep = 0.001f;
            config.distanceRewardScale = 0.1f;
            config.goalReachedBonus = 20.0f;
            config.crashPenalty = 5.0f;
            config.outOfBoundsPenalty = 3.0f;
            config.energyPenaltyScale = 0.0005f;
            config.tiltPenaltyScale = 0.0002f;
            config.idlePenalty = 0.005f;
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            env.rewardConfig = config;
            Debug.Log($" Created reward config: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not create reward config: {e.Message}");
        }
    }
    
    static void FixExistingDrones()
    {
        DroneAgent[] agents = Object.FindObjectsOfType<DroneAgent>();
        int fixedCount = 0;
        
        foreach (var agent in agents)
        {
            // Fix RLFlightAdapter
            var adapter = agent.GetComponent<RLFlightAdapter>();
            if (adapter != null)
            {
                adapter.maxTiltCmd = 0.5f;
                adapter.maxYawCmd = 0.5f;
                EditorUtility.SetDirty(adapter);
                Debug.Log($" Fixed RLFlightAdapter on {agent.name}");
            }
            
            // Fix DroneAgent sensor noise
            agent.velocityNoise = 0.02f;
            agent.gyroNoise = 0.01f;
            agent.positionNoise = 0.05f;
            agent.attitudeNoise = 0.2f;
            agent.maxStallSteps = 500;
            EditorUtility.SetDirty(agent);
            
            // Fix QuadController
            var controller = agent.GetComponent<QuadController>();
            if (controller != null)
            {
                controller.sensorNoise = 0.02f;
                EditorUtility.SetDirty(controller);
            }
            
            // Fix BehaviorParameters
            var behaviorParams = agent.GetComponent<BehaviorParameters>();
            if (behaviorParams != null)
            {
                behaviorParams.BrainParameters.VectorObservationSize = agent.CurrentObservationCount;
                EditorUtility.SetDirty(behaviorParams);
            }
            
            Debug.Log($" Fixed all settings on {agent.name}");
            fixedCount++;
        }
        
        if (fixedCount > 0)
        {
            EditorUtility.DisplayDialog("Drones Fixed! 🎉", 
                $"Applied optimized settings to {fixedCount} drones:\n\n" +
                "• Reduced action scaling (0.5)\n" +
                "• Minimized sensor noise\n" +
                "• Extended stall detection (500 steps)\n" +
                "• Updated observation sizes\n\n" +
                "Ready for stable training!", 
                "Excellent!");
        }
        else
        {
            EditorUtility.DisplayDialog("No Drones Found", 
                "No DroneAgent components found in the scene.\n\n" +
                "Create some drones first, then run this tool.", 
                "OK");
        }
        
        Debug.Log($" Fixed {fixedCount} drones with optimized settings");
    }
}
