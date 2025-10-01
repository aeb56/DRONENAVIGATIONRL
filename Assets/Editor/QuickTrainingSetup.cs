using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

/// <summary>
/// Ultra-quick training setup that creates everything needed in one click
/// </summary>
public class QuickTrainingSetup
{
    [MenuItem("Tools/DroneRL/ INSTANT TRAINING SETUP", priority = 0)]
    public static void InstantSetup()
    {
        if (EditorUtility.DisplayDialog("Instant Training Setup", 
            "This will create a complete training scene with optimized settings.\n\n" +
            "• 6 optimized drones\n" +
            "• Multi-drone environment\n" +
            "• Proper reward configuration\n" +
            "• All settings optimized\n\n" +
            "Continue?", 
            " GO!", "Cancel"))
        {
            CreateInstantSetup();
        }
    }
    
    static void CreateInstantSetup()
    {
        EditorUtility.DisplayProgressBar("Instant Setup", "Creating training environment...", 0.2f);
        
        try
        {
            // Clear scene
            GameObject[] allObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name != "Main Camera" && obj.name != "Directional Light")
                {
                    Object.DestroyImmediate(obj);
                }
            }
            
            EditorUtility.DisplayProgressBar("Instant Setup", "Setting up environment...", 0.4f);
            
            // Create environment
            GameObject arena = CreateEnvironment();
            
            EditorUtility.DisplayProgressBar("Instant Setup", "Creating optimized drones...", 0.6f);
            
            // Create drones
            CreateOptimizedDrones(arena, 6);
            
            EditorUtility.DisplayProgressBar("Instant Setup", "Finalizing...", 0.8f);
            
            // Setup camera
            SetupCamera(arena);
            
            // Save scene
            string scenePath = "Assets/Scenes/OptimizedTraining.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);
            
            EditorUtility.ClearProgressBar();
            
            Selection.activeGameObject = arena;
            SceneView.FrameLastActiveSceneView();
            
            EditorUtility.DisplayDialog("🎉 READY TO TRAIN!", 
                "Training environment created successfully!\n\n" +
                " Scene: OptimizedTraining.unity\n" +
                " 6 optimized drones\n" +
                " Multi-drone environment\n" +
                " All settings optimized\n\n" +
                "Start training with:\n" +
                "mlagents-learn config_improved_drone.yaml --run-id=instant_training", 
                "Let's Train! 🚁");
                
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Setup Error", $"Error: {e.Message}", "OK");
            Debug.LogError($"Instant setup error: {e}");
        }
    }
    
    static GameObject CreateEnvironment()
    {
        GameObject arena = new GameObject("TrainingArena");
        
        // Add environment components
        var env = arena.AddComponent<DroneTrainingEnv>();
        env.arenaSize = new Vector2(60f, 60f);
        env.ceilingHeight = 20f;
        env.agentCount = 6;
        env.episodeLength = 120f;
        env.baseObstacleCount = 15;
        env.enableWind = true;
        env.maxWindStrength = 4f;
        env.proceduralArenaGeometry = true;
        env.proceduralObstacles = true;
        
        // Create reward config
        CreateRewardConfig(env);
        
        return arena;
    }
    
    static void CreateOptimizedDrones(GameObject arena, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Create drone
            GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            drone.name = $"Drone_{i:D2}";
            drone.transform.SetParent(arena.transform);
            drone.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
            
            // Position in circle
            float angle = (360f / count) * i;
            float radius = 8f;
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            drone.transform.position = new Vector3(x, 2f, z);
            
            // Add physics - ensure Rigidbody exists
            Rigidbody rb = drone.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = drone.AddComponent<Rigidbody>();
            }
            rb.mass = 1.2f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // Add controller
            var controller = drone.AddComponent<QuadController>();
            if (controller != null)
            {
                controller.sensorNoise = 0.02f;
                controller.thrustToWeight = 2.2f;
                controller.motorTimeConstant = 0.12f;
                controller.maxTiltDegrees = 35f;
                controller.yawRate = 120f;
            }
            
            // Add adapter with optimized settings
            var adapter = drone.AddComponent<RLFlightAdapter>();
            if (adapter != null)
            {
                adapter.maxTiltCmd = 0.5f;  // KEY OPTIMIZATION
                adapter.maxYawCmd = 0.5f;   // KEY OPTIMIZATION
                adapter.throttleRange01 = new Vector2(0f, 1f);
            }
            
            // Add agent with optimized settings
            var agent = drone.AddComponent<DroneAgent>();
            if (agent != null)
            {
                agent.velocityNoise = 0.02f;    // REDUCED
                agent.gyroNoise = 0.01f;        // REDUCED
                agent.positionNoise = 0.05f;    // REDUCED
                agent.attitudeNoise = 0.2f;     // REDUCED
                agent.maxStallSteps = 500;      // EXTENDED
                agent.useRaycasts = true;
                agent.rayCount = 8;
                agent.rayDistance = 15f;
                agent.successRadius = 1.5f;
                agent.successHoldSteps = 10;
            }
            
            // Add behavior parameters
            var behaviorParams = drone.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams != null)
            {
                behaviorParams.BehaviorName = "DroneAgent";
                behaviorParams.BrainParameters.VectorObservationSize = 67; // Will auto-update
                behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
                behaviorParams.BehaviorType = BehaviorType.Default;
            }
            
            // Create goal for this drone
            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goal.name = $"Goal_{i:D2}";
            goal.transform.SetParent(arena.transform);
            goal.transform.localScale = Vector3.one * 2f;
            
            // Position goal randomly
            Vector3 goalPos = new Vector3(
                Random.Range(-25f, 25f),
                Random.Range(3f, 15f),
                Random.Range(-25f, 25f)
            );
            goal.transform.position = goalPos;
            
            // Setup goal
            var goalCollider = goal.GetComponent<SphereCollider>();
            goalCollider.isTrigger = true;
            
            var goalZone = goal.AddComponent<GoalZone>();
            goalZone.Assign(agent);
            
            var goalRenderer = goal.GetComponent<Renderer>();
            Material goalMat = new Material(Shader.Find("Standard"));
            goalMat.color = Color.yellow;
            goalMat.SetFloat("_Metallic", 0.2f);
            goalRenderer.material = goalMat;
            
            // Assign references
            if (agent != null)
            {
                agent.goal = goal.transform;
                agent.env = arena.GetComponent<DroneTrainingEnv>();
                
                // Assign reward config if available
                var rewardConfig = AssetDatabase.LoadAssetAtPath<RLRewardConfig>("Assets/Resources/InstantRewardConfig.asset");
                if (rewardConfig != null)
                {
                    agent.rewardConfig = rewardConfig;
                }
            }
            
            Debug.Log($" Created optimized drone {i+1}/{count} with all components");
        }
    }
    
    static void CreateRewardConfig(DroneTrainingEnv env)
    {
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            RLRewardConfig config = ScriptableObject.CreateInstance<RLRewardConfig>();
            
            // Optimized reward settings
            config.aliveBonusPerStep = 0.001f;
            config.distanceRewardScale = 0.1f;
            config.goalReachedBonus = 20.0f;    // Moderate for stable learning
            config.crashPenalty = 5.0f;
            config.outOfBoundsPenalty = 3.0f;
            config.energyPenaltyScale = 0.0005f; // Reduced
            config.tiltPenaltyScale = 0.0002f;   // Reduced
            config.idlePenalty = 0.005f;
            
            string path = "Assets/Resources/InstantRewardConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            env.rewardConfig = config;
            Debug.Log(" Created optimized reward configuration");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not create reward config: {e.Message}");
        }
    }
    
    static void SetupCamera(GameObject arena)
    {
        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, 25, -40);
            Camera.main.transform.LookAt(Vector3.zero);
            Camera.main.farClipPlane = 200f;
        }
    }
}
