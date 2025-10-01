#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Quick setup tool for multi-drone path navigation training
/// Run this after "Full Setup" to configure multi-drone settings
/// </summary>
public static class MultiDroneQuickSetup
{
    [MenuItem("Tools/DroneRL/Setup Multi-Drone Path Training", priority = 11)]
    public static void SetupMultiDronePathTraining()
    {
        var arena = GameObject.Find("DroneTrainingArena");
        if (arena == null)
        {
            Debug.LogError("Please run 'Tools/DroneRL/Full Setup' first!");
            return;
        }
        
        var multiEnv = arena.GetComponent<MultiDroneTrainingEnv>();
        if (multiEnv == null)
        {
            Debug.LogError("MultiDroneTrainingEnv component not found! Run Full Setup first.");
            return;
        }
        
        // Configure for path training
        multiEnv.droneCount = 3;
        multiEnv.usePathFollowing = true;
        multiEnv.individualPaths = false; // Shared waypoints for now
        multiEnv.currentScenario = MultiDroneTrainingEnv.TrainingScenario.PathFollowing;
        multiEnv.spawnInFormation = true;
        multiEnv.formationSpacing = 8f;
        
        // Create a simple goal area if not exists
        var goalArea = GameObject.Find("GoalArea");
        if (goalArea == null)
        {
            goalArea = new GameObject("GoalArea");
            goalArea.transform.position = new Vector3(0, 5, 30);
            
            // Add a visual indicator
            var goalIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goalIndicator.name = "GoalIndicator";
            goalIndicator.transform.SetParent(goalArea.transform);
            goalIndicator.transform.localScale = Vector3.one * 3f;
            
            // Make it green and glowing
            var renderer = goalIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = Color.green;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.green);
                renderer.material = material;
            }
            
            // Remove collider so drones can pass through
            var collider = goalIndicator.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
        
        multiEnv.goalArea = goalArea.transform;
        
        // Configure curriculum learning for gradual difficulty
        var curriculum = arena.GetComponent<DroneCurriculumLearning>();
        if (curriculum != null)
        {
            curriculum.adaptiveDifficulty = true;
            curriculum.evaluationWindow = 50; // Smaller window for faster progression
        }
        
        // Set up advanced environment for realistic conditions
        var advancedEnv = arena.GetComponent<AdvancedTrainingEnvironments>();
        if (advancedEnv != null)
        {
            advancedEnv.currentScenario = AdvancedTrainingEnvironments.ScenarioType.Basic;
            advancedEnv.weatherCondition = AdvancedTrainingEnvironments.WeatherCondition.Clear;
            advancedEnv.enableFailureTraining = false; // Start simple
        }
        
        // Force setup of the multi-drone environment
        if (Application.isPlaying)
        {
            var setupMethod = multiEnv.GetType().GetMethod("SetupMultiDroneEnvironment", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            setupMethod?.Invoke(multiEnv, null);
        }
        
        Debug.Log(" Multi-Drone Path Training Setup Complete!");
        Debug.Log("📝 Configuration:");
        Debug.Log($"   • {multiEnv.droneCount} drones");
        Debug.Log($"   • Scenario: {multiEnv.currentScenario}");
        Debug.Log($"   • Formation spacing: {multiEnv.formationSpacing}m");
        Debug.Log("🎮 Press Play and run the training command!");
        
        // Select the arena for easy inspection
        Selection.activeGameObject = arena;
    }
    
    [MenuItem("Tools/DroneRL/Reset Multi-Drone Environment", priority = 12)]
    public static void ResetMultiDroneEnvironment()
    {
        var arena = GameObject.Find("DroneTrainingArena");
        if (arena == null) return;
        
        var multiEnv = arena.GetComponent<MultiDroneTrainingEnv>();
        if (multiEnv != null && Application.isPlaying)
        {
            multiEnv.ResetEnvironment();
            Debug.Log("Multi-drone environment reset!");
        }
    }
}
#endif
