using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Actuators;
using System.IO;

/// <summary>
/// One-click setup tool for drone training environment
/// Creates everything needed for training with good default settings
/// </summary>
public class OneClickDroneSetup : EditorWindow
{
    [Header("Environment Settings")]
    public int droneCount = 6;
    public Vector2 arenaSize = new Vector2(80f, 80f);
    public float ceilingHeight = 25f;
    public int obstacleCount = 15;
    public int movingObstacleCount = 3;
    public int aerialObstacleCount = 4;
    public bool enableWind = true;
    
    [Header("Drone Prefab Selection")]
    public GameObject selectedDronePrefab;
    public bool autoSelectDronePrefab = true;
    
    [Header("Training Optimization")]
    public bool useOptimizedSettings = true;
    public bool createRewardConfig = true;
    public bool setupCameras = true;
    
    [Header("Ablations & Manifest")]
    public bool createAblationVariants = false;
    public bool enableVisionPolicy = false;
    public bool enableDomainRandomizationByDefault = true;
    public bool useSharedGroupRewards = false;
    public string manifestFileName = "experiment_manifest.json";
    
    private static readonly string[] REQUIRED_FOLDERS = {
        "Assets/DroneRL",
        "Assets/DroneRL/Agents", 
        "Assets/DroneRL/Environment",
        "Assets/DroneRL/Rewards",
        "Assets/Prefabs",
        "Assets/Resources"
    };

    [MenuItem("Tools/DroneRL/One-Click Complete Setup", priority = 0)]
    public static void ShowWindow()
    {
        var window = GetWindow<OneClickDroneSetup>("One-Click Drone Setup");
        window.minSize = new Vector2(400, 600);
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("One-Click Multi-Drone RL Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("This will create a complete multi-drone training environment with optimized settings.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Environment Settings
        GUILayout.Label("Environment Configuration", EditorStyles.boldLabel);
        droneCount = EditorGUILayout.IntSlider("Drone Count", droneCount, 1, 12);
        arenaSize = EditorGUILayout.Vector2Field("Arena Size", arenaSize);
        ceilingHeight = EditorGUILayout.FloatField("Ceiling Height", ceilingHeight);
        obstacleCount = EditorGUILayout.IntSlider("Static Obstacles", obstacleCount, 0, 50);
        movingObstacleCount = EditorGUILayout.IntSlider("Moving Obstacles", movingObstacleCount, 0, 10);
        aerialObstacleCount = EditorGUILayout.IntSlider("Aerial Obstacles", aerialObstacleCount, 0, 15);
        enableWind = EditorGUILayout.Toggle("Enable Wind", enableWind);
        
        EditorGUILayout.Space();
        
        // Drone Prefab Selection
        GUILayout.Label("Drone Prefab", EditorStyles.boldLabel);
        autoSelectDronePrefab = EditorGUILayout.Toggle("Auto-Select Best Prefab", autoSelectDronePrefab);
        
        if (!autoSelectDronePrefab)
        {
            selectedDronePrefab = (GameObject)EditorGUILayout.ObjectField("Drone Prefab", selectedDronePrefab, typeof(GameObject), false);
        }
        else
        {
            // Show which prefab will be auto-selected
            GameObject autoPrefab = FindBestDronePrefab();
            string prefabName = autoPrefab ? autoPrefab.name : "None found";
            EditorGUILayout.LabelField("Will use:", prefabName);
        }
        
        EditorGUILayout.Space();
        
        // Optimization Settings
        GUILayout.Label("Training Optimization", EditorStyles.boldLabel);
        useOptimizedSettings = EditorGUILayout.Toggle("Use Optimized Settings", useOptimizedSettings);
        createRewardConfig = EditorGUILayout.Toggle("Create Reward Config", createRewardConfig);
        setupCameras = EditorGUILayout.Toggle("Setup Training Cameras", setupCameras);
        
        EditorGUILayout.Space();
        
        if (useOptimizedSettings)
        {
            EditorGUILayout.HelpBox("Optimized settings:\n" +
                "• Reduced action scaling (0.5)\n" +
                "• Lower decision frequency (10Hz)\n" +
                "• Minimal sensor noise\n" +
                "• Simplified rewards\n" +
                "• Extended stall detection", MessageType.Info);
        }
        
        EditorGUILayout.Space();
        
        // Setup Button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("CREATE COMPLETE SETUP", GUILayout.Height(40)))
        {
            CreateCompleteSetup();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space();
        
        // Quick Actions
        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Create Folder Structure"))
        {
            CreateFolderStructure();
        }
        
        if (GUILayout.Button("Create Reward Config Only"))
        {
            CreateOptimizedRewardConfig();
        }
        
        if (GUILayout.Button("Fix Existing Drones"))
        {
            FixExistingDrones();
        }
        
        if (GUILayout.Button("🧪 Generate Ablation Variants"))
        {
            GenerateAblationVariantsPreview();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("After setup, use: mlagents-learn config_improved_drone.yaml --run-id=optimized_training", MessageType.Info);
    }

    void CreateCompleteSetup()
    {
        // Validate setup before starting
        if (!ValidateSetup()) return;
        
        try
        {
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Cleaning existing setup...", 0.05f);
            CleanupExistingSetup();
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Creating folder structure...", 0.1f);
            CreateFolderStructure();
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Creating reward configuration...", 0.2f);
            if (createRewardConfig) CreateOptimizedRewardConfig();
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Preparing drone prefab...", 0.3f);
            GameObject dronePrefab = GetOrCreateDronePrefab();
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Setting up training arena...", 0.5f);
            GameObject arena = CreateTrainingArena(dronePrefab);
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Configuring multi-drone environment...", 0.7f);
            ConfigureMultiDroneEnvironment(arena, dronePrefab);
            
            if (setupCameras)
            {
                EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Setting up cameras...", 0.8f);
                SetupTrainingCameras(arena);
            }
            
            EditorUtility.DisplayProgressBar("Setting up Multi-Drone Environment", "Finalizing setup...", 0.9f);
            FinalizeSetup(arena);
            
            EditorUtility.ClearProgressBar();
            
            GameObject usedPrefab = GetOrCreateDronePrefab();
            string prefabName = usedPrefab ? usedPrefab.name : "Custom";
            
            EditorUtility.DisplayDialog("Setup Complete! 🎉", 
                $"Multi-drone training environment created successfully!\n\n" +
                $"{droneCount} drones spawned in scene\n" +
                $"Using prefab: {prefabName}\n" +
                $"Optimized settings applied\n" +
                $"Arena size: {arenaSize.x}x{arenaSize.y}m\n" +
                $"{obstacleCount} static + {movingObstacleCount} moving + {aerialObstacleCount} aerial obstacles\n" +
                $"Visible in Scene View!\n\n" +
                $"Ready to train with:\n" +
                $"mlagents-learn config_improved_drone.yaml --run-id=optimized_training", 
                "Awesome!");
                
            Selection.activeGameObject = arena;
            SceneView.FrameLastActiveSceneView();
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Setup Error", $"An error occurred during setup:\n{e.Message}", "OK");
            Debug.LogError($"OneClickDroneSetup Error: {e}");
        }
    }

    void CreateFolderStructure()
    {
        foreach (string folder in REQUIRED_FOLDERS)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parentFolder = Path.GetDirectoryName(folder).Replace('\\', '/');
                string folderName = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parentFolder, folderName);
                Debug.Log($"Created folder: {folder}");
            }
        }
        AssetDatabase.Refresh();
    }

    void CleanupExistingSetup()
    {
        // Find and remove existing training arenas
        GameObject[] existingArenas = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in existingArenas)
        {
            if (obj.name.Contains("TrainingArena") || obj.name.Contains("OptimizedTraining"))
            {
                Debug.Log($"🧹 Removing existing arena: {obj.name}");
                Object.DestroyImmediate(obj);
            }
        }
        
        // Also check for any DroneTrainingEnv components
        DroneTrainingEnv[] existingEnvs = Object.FindObjectsOfType<DroneTrainingEnv>();
        foreach (var env in existingEnvs)
        {
            if (env.gameObject.name.Contains("TrainingArena") || env.gameObject.name.Contains("OptimizedTraining"))
            {
                Debug.Log($"🧹 Removing existing environment: {env.gameObject.name}");
                Object.DestroyImmediate(env.gameObject);
            }
        }
        
        // Clean up any orphaned drones
        DroneAgent[] orphanedDrones = Object.FindObjectsOfType<DroneAgent>();
        foreach (var drone in orphanedDrones)
        {
            if (drone.transform.parent == null || drone.name.Contains("Drone_"))
            {
                Debug.Log($"🧹 Removing orphaned drone: {drone.name}");
                Object.DestroyImmediate(drone.gameObject);
            }
        }
        
        Debug.Log("Cleanup completed");
    }

    bool ValidateSetup()
    {
        // Check for reasonable values
        if (droneCount <= 0 || droneCount > 20)
        {
            EditorUtility.DisplayDialog("Invalid Settings", 
                $"Drone count ({droneCount}) must be between 1 and 20", "OK");
            return false;
        }
        
        if (arenaSize.x <= 0 || arenaSize.y <= 0 || ceilingHeight <= 0)
        {
            EditorUtility.DisplayDialog("Invalid Settings", 
                "Arena size and ceiling height must be positive values", "OK");
            return false;
        }
        
        // Check if there are existing setups that might interfere
        DroneTrainingEnv[] existingEnvs = Object.FindObjectsOfType<DroneTrainingEnv>();
        if (existingEnvs.Length > 0)
        {
            bool proceed = EditorUtility.DisplayDialog("Existing Setup Detected", 
                $"Found {existingEnvs.Length} existing training environment(s).\n\n" +
                "This setup will clean them up and create a new one.\n\n" +
                "Continue?", 
                "Yes, Clean & Setup", "Cancel");
            if (!proceed) return false;
        }
        
        return true;
    }

    void CreateOptimizedRewardConfig()
    {
        string path = "Assets/DroneRL/Rewards/OptimizedRewardConfig.asset";
        
        // Create the ScriptableObject
        RLRewardConfig config = ScriptableObject.CreateInstance<RLRewardConfig>();
        
        // Apply optimized settings
        config.aliveBonusPerStep = 0.001f;
        config.distanceRewardScale = 0.1f;
        config.goalReachedBonus = 20.0f; // Reduced for smoother curves
        config.stabilityRewardScale = 0.01f;
        config.altitudeTarget = 3f;
        config.altitudeTolerance = 2.0f;
        config.stabilityShapingEpisodes = 1000;
        
        config.crashPenalty = 5.0f;
        config.outOfBoundsPenalty = 3.0f;
        config.timeoutPenalty = 0.5f;
        config.energyPenaltyScale = 0.0005f; // Reduced
        config.tiltPenaltyScale = 0.0002f;   // Reduced
        config.idlePenalty = 0.005f;
        
        config.idleTimeout = 5f;
        config.goalRadius = 1.5f;
        
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created optimized reward config: {path}");
    }

    GameObject CreateOptimizedDronePrefab()
    {
        // Try to find an existing visual drone prefab first
        GameObject existingPrefab = FindBestDronePrefab();
        if (existingPrefab != null)
        {
            Debug.Log($"Found existing drone prefab: {existingPrefab.name}");
            return existingPrefab; // Return the existing prefab directly - no cylinder needed!
        }
        
        Debug.LogWarning("No drone prefab found, creating basic fallback");
        
        // Create drone body as fallback ONLY if no prefab found
        GameObject drone = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        drone.name = "FallbackDronePrefab";
        drone.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
        
        // Add Rigidbody with optimized settings - ensure it exists
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
        
        // Add QuadController with optimized settings
        var controller = drone.AddComponent<QuadController>();
        if (controller != null)
        {
            controller.thrustToWeight = 2.2f;
            controller.motorTimeConstant = 0.16f; // slower response for smoother motion
            controller.maxTiltDegrees = 28f;       // reduce aggressive tilting
            controller.yawRate = 90f;              // gentler yaw
            controller.kAttP = 7f;                 // slightly softer P
            controller.kAttD = 0.6f;               // more damping
            controller.angularDamping = 0.6f;      // global angular damping
            controller.lateralDamping = 0.2f;      // reduce slide a bit
            controller.sensorNoise = 0.02f;        // Reduced
            controller.batteryLevel = 1.0f;
            controller.throttle = 0.5f;
        }
        
        // Add RLFlightAdapter with optimized settings
        var adapter = drone.AddComponent<RLFlightAdapter>();
        if (adapter != null)
        {
            adapter.maxTiltCmd = 0.5f;  // Optimized
            adapter.maxYawCmd = 0.5f;   // Optimized
            adapter.throttleRange01 = new Vector2(0f, 1f);
        }
        
        // Add DroneAgent with optimized settings
        var agent = drone.AddComponent<DroneAgent>();
        if (agent != null)
        {
            agent.useRaycasts = true;
            agent.rayCount = 8;
            agent.rayDistance = 15f;
            agent.obstacleMask = ~0; // Detect all layers (including Default layer 0)
            agent.ignoreSelfColliders = true;
            agent.includeAgentsInRaycasts = true;
            agent.rayOriginUpOffset = 0.05f;
            
            Debug.Log($"DroneAgent raycast settings: rayCount={agent.rayCount}, rayDistance={agent.rayDistance}, obstacleMask={agent.obstacleMask.value}");
            
            // Optimized sensor noise
            agent.velocityNoise = 0.02f;
            agent.gyroNoise = 0.01f;
            agent.positionNoise = 0.05f;
            agent.attitudeNoise = 0.2f;
            
            // Optimized success/stall detection
            agent.successRadius = 1.5f;
            agent.successHoldSteps = 10;
            agent.maxStallSteps = 500; // Extended
        }
        
        // Add BehaviorParameters
        var behaviorParams = drone.AddComponent<BehaviorParameters>();
        if (behaviorParams != null && agent != null)
        {
            behaviorParams.BehaviorName = "DroneAgent";
            behaviorParams.BrainParameters.VectorObservationSize = agent.CurrentObservationCount;
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
            behaviorParams.BehaviorType = BehaviorType.Default;
        }
        
        // Add advanced sensors
        var advancedSensors = drone.AddComponent<DroneAdvancedSensors>();
        // Configure sensors with reasonable defaults
        
        // Create visual elements (propellers)
        for (int i = 0; i < 4; i++)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            prop.name = $"Propeller_{i}";
            prop.transform.SetParent(drone.transform);
            prop.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
            
            float angle = i * 90f;
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * 0.6f;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.6f;
            prop.transform.localPosition = new Vector3(x, 0.2f, z);
            
            Object.DestroyImmediate(prop.GetComponent<Collider>());
            
            var renderer = prop.GetComponent<Renderer>();
            Material propMat = new Material(Shader.Find("Standard"));
            propMat.color = Color.gray;
            renderer.material = propMat;
        }
        
        // Save as prefab
        string prefabPath = "Assets/Prefabs/OptimizedDronePrefab.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(drone, prefabPath);
        Object.DestroyImmediate(drone);
        
        Debug.Log($"Created optimized drone prefab: {prefabPath}");
        return prefab;
    }

    GameObject CreateTrainingArena(GameObject dronePrefab)
    {
        // Create main arena GameObject
        GameObject arena = new GameObject("OptimizedTrainingArena");
        
        // Add DroneTrainingEnv
        var env = arena.AddComponent<DroneTrainingEnv>();
        env.arenaSize = arenaSize;
        env.ceilingHeight = ceilingHeight;
        env.agentCount = 0; // Set to 0 to prevent auto-spawning by DroneTrainingEnv
        env.episodeLength = 120f; // 2 minutes
        env.difficulty = 0.3f;
        env.baseObstacleCount = 0; // Set to 0 since we create obstacles manually
        env.enableWind = enableWind;
        env.maxWindStrength = 5f;
        env.proceduralArenaGeometry = false; // Disable since we create geometry manually
        env.proceduralObstacles = false; // Disable since we create obstacles manually
        env.enableDomainRandomization = enableDomainRandomizationByDefault;
        env.minInterDroneSpacing = 6f;
        env.enableAgentAgentCollisions = false; // Stage 0-5 default
        
        // Assign reward config if created
        string rewardConfigPath = "Assets/DroneRL/Rewards/OptimizedRewardConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<RLRewardConfig>(rewardConfigPath) != null)
        {
            env.rewardConfig = AssetDatabase.LoadAssetAtPath<RLRewardConfig>(rewardConfigPath);
        }
        
        // Create arena geometry immediately for scene visibility
        CreateArenaGeometry(arena);
        
        return arena;
    }
    
    void CreateArenaGeometry(GameObject arena)
    {
        // Create floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Arena_Floor";
        floor.transform.SetParent(arena.transform, false);
        floor.transform.localScale = new Vector3(arenaSize.x / 10f, 1f, arenaSize.y / 10f);
        floor.transform.position = new Vector3(0f, 0f, 0f);
        
        // Ensure floor has solid collider and proper layer
        var floorCollider = floor.GetComponent<Collider>();
        if (floorCollider != null)
        {
            floorCollider.isTrigger = false;
        }
        floor.layer = 0; // Default layer for raycast detection
        
        var floorRb = floor.AddComponent<Rigidbody>();
        floorRb.isKinematic = true;
        floorRb.useGravity = false;
        
        // Create walls (visible in scene)
        CreateWall(arena, "Wall_North", new Vector3(0, ceilingHeight * 0.5f, arenaSize.y * 0.5f + 0.5f), new Vector3(arenaSize.x, ceilingHeight, 1f));
        CreateWall(arena, "Wall_South", new Vector3(0, ceilingHeight * 0.5f, -arenaSize.y * 0.5f - 0.5f), new Vector3(arenaSize.x, ceilingHeight, 1f));
        CreateWall(arena, "Wall_East", new Vector3(arenaSize.x * 0.5f + 0.5f, ceilingHeight * 0.5f, 0), new Vector3(1f, ceilingHeight, arenaSize.y));
        CreateWall(arena, "Wall_West", new Vector3(-arenaSize.x * 0.5f - 0.5f, ceilingHeight * 0.5f, 0), new Vector3(1f, ceilingHeight, arenaSize.y));
        
        // Create ceiling (semi-transparent)
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceiling.name = "Arena_Ceiling";
        ceiling.transform.SetParent(arena.transform, false);
        ceiling.transform.localScale = new Vector3(arenaSize.x / 10f, 1f, arenaSize.y / 10f);
        ceiling.transform.position = new Vector3(0f, ceilingHeight, 0f);
        ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        
        // Ensure ceiling has solid collider and proper layer
        var ceilingCollider = ceiling.GetComponent<Collider>();
        if (ceilingCollider != null)
        {
            ceilingCollider.isTrigger = false;
        }
        ceiling.layer = 0; // Default layer for raycast detection
        
        var ceilingRb = ceiling.AddComponent<Rigidbody>();
        ceilingRb.isKinematic = true;
        ceilingRb.useGravity = false;
        
        // Make ceiling semi-transparent
        var ceilingRenderer = ceiling.GetComponent<Renderer>();
        Material ceilingMat = new Material(Shader.Find("Standard"));
        ceilingMat.color = new Color(0.8f, 0.8f, 1f, 0.3f);
        ceilingMat.SetFloat("_Mode", 3); // Transparent mode
        ceilingMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ceilingMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ceilingMat.SetInt("_ZWrite", 0);
        ceilingMat.DisableKeyword("_ALPHATEST_ON");
        ceilingMat.EnableKeyword("_ALPHABLEND_ON");
        ceilingMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ceilingMat.renderQueue = 3000;
        ceilingRenderer.material = ceilingMat;
        
        // Create obstacles for training challenge
        CreateStaticObstacles(arena);
        CreateMovingObstacles(arena);
        CreateAerialObstacles(arena);
    }
    
    void CreateWall(GameObject parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        
        // Make walls semi-transparent
        var renderer = wall.GetComponent<Renderer>();
        Material wallMat = new Material(Shader.Find("Standard"));
        wallMat.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        wallMat.SetFloat("_Mode", 3); // Transparent mode
        wallMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        wallMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        wallMat.SetInt("_ZWrite", 0);
        wallMat.DisableKeyword("_ALPHATEST_ON");
        wallMat.EnableKeyword("_ALPHABLEND_ON");
        wallMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        wallMat.renderQueue = 3000;
        renderer.material = wallMat;
        
        // Ensure wall has solid collider and proper layer
        var collider = wall.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
        
        // Set to Default layer (0) to ensure raycast detection
        wall.layer = 0;
        
        var rb = wall.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
    
    void CreateStaticObstacles(GameObject arena)
    {
        for (int i = 0; i < obstacleCount; i++)
        {
            var shape = (i % 3 == 0) ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject obstacle = GameObject.CreatePrimitive(shape);
            obstacle.name = $"Obstacle_{i:D2}";
            obstacle.transform.SetParent(arena.transform, false);
            
            Vector3 pos = new Vector3(
                Random.Range(-arenaSize.x * 0.4f, arenaSize.x * 0.4f),
                Random.Range(1f, 4f), // Keep obstacles at ground level, not floating
                Random.Range(-arenaSize.y * 0.4f, arenaSize.y * 0.4f)
            );
            obstacle.transform.position = pos;
            
            float scale = Random.Range(0.6f, 3.2f);
            obstacle.transform.localScale = Vector3.one * scale;
            
            // Ensure proper colliders for obstacles and set layer
            var collider = obstacle.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false; // Make sure it's a solid collider
            }
            
            // Set to Default layer (0) to ensure raycast detection
            obstacle.layer = 0;
            
            var rb = obstacle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            
            // Color obstacles
            var renderer = obstacle.GetComponent<Renderer>();
            Material obstacleMat = new Material(Shader.Find("Standard"));
            obstacleMat.color = new Color(Random.Range(0.3f, 0.8f), Random.Range(0.3f, 0.8f), Random.Range(0.3f, 0.8f));
            obstacleMat.SetFloat("_Metallic", 0.2f);
            obstacleMat.SetFloat("_Smoothness", 0.6f);
            renderer.material = obstacleMat;
        }
    }

    void ConfigureMultiDroneEnvironment(GameObject arena, GameObject dronePrefab)
    {
        // Add MultiDroneTrainingEnv component but configure it to NOT auto-spawn
        var multiEnv = arena.AddComponent<MultiDroneTrainingEnv>();
        multiEnv.droneCount = 0; // Set to 0 to prevent auto-spawning
        multiEnv.droneAgentPrefab = dronePrefab;
        multiEnv.currentScenario = MultiDroneTrainingEnv.TrainingScenario.PathFollowing;
        multiEnv.individualPaths = true;
        multiEnv.formationSpacing = 5f;
        multiEnv.pathDifficulty = 0.8f;
        multiEnv.sharedRewards = useSharedGroupRewards;
        multiEnv.useAgentGroup = useSharedGroupRewards;
        
        // Spawn drones in scene for visualization (we control this manually)
        SpawnDronesInScene(arena, dronePrefab);
        
        Debug.Log($"Configured multi-drone environment with {droneCount} manually spawned drones");
    }
    
    void SpawnDronesInScene(GameObject arena, GameObject dronePrefab)
    {
        float radius = Mathf.Min(arenaSize.x, arenaSize.y) * 0.3f;
        
        for (int i = 0; i < droneCount; i++)
        {
            // Create drone instance in scene
            GameObject drone = PrefabUtility.InstantiatePrefab(dronePrefab) as GameObject;
            if (drone == null) 
            {
                Debug.LogError($"Failed to instantiate drone prefab: {dronePrefab.name}");
                continue;
            }
            
            drone.name = $"Drone_{i:D2}";
            drone.transform.SetParent(arena.transform, false);
            
            // Remove any extra colliders that might be causing overlapping visuals
            Collider[] colliders = drone.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.gameObject.name.Contains("Capsule") || col.gameObject.name.Contains("Cylinder"))
                {
                    Debug.Log($"🧹 Removing extra collider: {col.gameObject.name}");
                    Object.DestroyImmediate(col.gameObject);
                }
            }
            
            Debug.Log($"Instantiated drone prefab: {dronePrefab.name} as {drone.name}");
            
            // Position drones in a circle formation
            float angle = (360f / droneCount) * i;
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
            drone.transform.position = new Vector3(x, 3f, z);
            drone.transform.rotation = Quaternion.Euler(0f, angle + 90f, 0f);
            
            // Create and assign goal (independent of drone)
            GameObject goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goal.name = $"Goal_{i:D2}";
            goal.transform.SetParent(arena.transform, false); // Use worldPositionStays = false
            goal.transform.localScale = Vector3.one * 2f;
            
            // Position goal randomly in arena
            Vector3 goalPos = new Vector3(
                Random.Range(-arenaSize.x * 0.4f, arenaSize.x * 0.4f),
                Random.Range(2f, ceilingHeight * 0.7f),
                Random.Range(-arenaSize.y * 0.4f, arenaSize.y * 0.4f)
            );
            goal.transform.position = goalPos;
            
            // Setup goal appearance and trigger
            var goalCollider = goal.GetComponent<SphereCollider>();
            goalCollider.isTrigger = true;
            
            var goalRenderer = goal.GetComponent<Renderer>();
            Material goalMat = new Material(Shader.Find("Standard"));
            goalMat.color = Color.yellow;
            goalMat.SetFloat("_Metallic", 0.2f);
            goalMat.SetFloat("_Smoothness", 0.8f);
            goalRenderer.material = goalMat;
            
            // Add goal zone component
            var goalZone = goal.AddComponent<GoalZone>();
            
            // Ensure required components exist and apply optimized settings
            EnsureRequiredComponents(drone);
            ApplyOptimizedSettings(drone);
            
            // Assign references
            var agent = drone.GetComponent<DroneAgent>();
            if (agent != null)
            {
                agent.goal = goal.transform;
                agent.env = arena.GetComponent<DroneTrainingEnv>();
                
                // Assign reward config if available
                string rewardConfigPath = "Assets/DroneRL/Rewards/OptimizedRewardConfig.asset";
                var rewardConfig = AssetDatabase.LoadAssetAtPath<RLRewardConfig>(rewardConfigPath);
                if (rewardConfig != null)
                {
                    agent.rewardConfig = rewardConfig;
                }
                
                goalZone.Assign(agent);
            }
            
            Debug.Log($"Spawned drone {i+1}/{droneCount} in scene: {drone.name}");
        }
        
        // Write experiment manifest
        try { WriteExperimentManifest(arena); } catch (System.Exception ex) { Debug.LogWarning($"Manifest write failed: {ex.Message}"); }
    }
    
    void EnsureRequiredComponents(GameObject drone)
    {
        // Ensure Rigidbody exists
        if (drone.GetComponent<Rigidbody>() == null)
        {
            var rb = drone.AddComponent<Rigidbody>();
            rb.mass = 1.2f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Debug.Log($" Added Rigidbody to {drone.name}");
        }
        
        // Ensure Collider exists (needed for collision detection)
        if (drone.GetComponent<Collider>() == null)
        {
            var capsule = drone.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.direction = 1; // Y axis
            capsule.radius = 0.35f;
            capsule.height = 0.5f;
            capsule.center = new Vector3(0f, 0.25f, 0f);
            Debug.Log($" Added CapsuleCollider to {drone.name}");
        }
        
        // Ensure QuadController exists
        if (drone.GetComponent<QuadController>() == null)
        {
            drone.AddComponent<QuadController>();
            Debug.Log($" Added QuadController to {drone.name}");
        }
        
        // Ensure RLFlightAdapter exists
        if (drone.GetComponent<RLFlightAdapter>() == null)
        {
            drone.AddComponent<RLFlightAdapter>();
            Debug.Log($" Added RLFlightAdapter to {drone.name}");
        }
        
        // Ensure DroneAgent exists
        if (drone.GetComponent<DroneAgent>() == null)
        {
            drone.AddComponent<DroneAgent>();
            Debug.Log($" Added DroneAgent to {drone.name}");
        }
        
        // Ensure BehaviorParameters exists
        if (drone.GetComponent<BehaviorParameters>() == null)
        {
            var behaviorParams = drone.AddComponent<BehaviorParameters>();
            behaviorParams.BehaviorName = "DroneAgent";
            behaviorParams.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
            behaviorParams.BehaviorType = BehaviorType.Default;
            Debug.Log($" Added BehaviorParameters to {drone.name}");
        }
    }

    void ApplyOptimizedSettings(GameObject drone)
    {
        // Apply all our optimized settings to the drone instance
        var adapter = drone.GetComponent<RLFlightAdapter>();
        if (adapter != null)
        {
            adapter.maxTiltCmd = 0.5f;
            adapter.maxYawCmd = 0.5f;
        }
        
        var agent = drone.GetComponent<DroneAgent>();
        if (agent != null)
        {
            agent.velocityNoise = 0.02f;
            agent.gyroNoise = 0.01f;
            agent.positionNoise = 0.05f;
            agent.attitudeNoise = 0.2f;
            agent.maxStallSteps = 500;
            agent.minCrashSpeed = 1.5f;
            agent.minCrashTimeSinceStart = 0.5f;

            // Ensure visual sensors are only registered when explicitly enabled
            var adv = drone.GetComponent<DroneAdvancedSensors>();
            if (adv != null)
            {
                adv.registerVisualSensors = enableVisionPolicy;
            }
        }
        
        var controller = drone.GetComponent<QuadController>();
        if (controller != null)
        {
            controller.sensorNoise = 0.02f;
            controller.motorTimeConstant = 0.16f;
            controller.maxTiltDegrees = 28f;
            controller.yawRate = 90f;
            controller.kAttP = 7f;
            controller.kAttD = 0.6f;
            controller.angularDamping = 0.6f;
            controller.lateralDamping = 0.2f;
        }
        
        var behaviorParams = drone.GetComponent<BehaviorParameters>();
        if (behaviorParams != null && agent != null)
        {
            behaviorParams.BrainParameters.VectorObservationSize = agent.CurrentObservationCount;
            // If vision policy is requested, ensure DecisionRequester is present for consistent cadence
            if (enableVisionPolicy)
            {
                var dr = drone.GetComponent<DecisionRequester>();
                if (dr == null) dr = drone.AddComponent<DecisionRequester>();
                dr.DecisionPeriod = 5; // ~10Hz at 50Hz physics
                dr.TakeActionsBetweenDecisions = true;
            }
        }
    }

    void SetupTrainingCameras(GameObject arena)
    {
        // Main overview camera
        GameObject mainCam = new GameObject("TrainingOverviewCamera");
        mainCam.transform.SetParent(arena.transform);
        
        Camera cam = mainCam.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 200f;
        
        // Position camera for good overview
        mainCam.transform.position = new Vector3(0, ceilingHeight * 0.8f, -arenaSize.y * 0.6f);
        mainCam.transform.LookAt(Vector3.zero);
        
        // Add camera controller if available
        try 
        {
            var cameraController = mainCam.AddComponent<MultiDroneCameraController>();
        }
        catch 
        {
            Debug.LogWarning("MultiDroneCameraController not found, skipping camera controller setup");
        }
        
        Debug.Log(" Setup training cameras");
    }

    void FinalizeSetup(GameObject arena)
    {
        // Mark arena as dirty for saving
        EditorUtility.SetDirty(arena);
        
        // Save scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
        // Create a simple training stats HUD if available
        GameObject statsHUD = new GameObject("TrainingStatsHUD");
        statsHUD.transform.SetParent(arena.transform);
        try 
        {
            var statsLogger = statsHUD.AddComponent<TrainingStatsLogger>();
        }
        catch 
        {
            Debug.LogWarning("TrainingStatsLogger not found, skipping stats logger setup");
        }
        
        // Add on-screen Enhanced Training HUD
        GameObject hud = new GameObject("EnhancedTrainingHUD");
        hud.transform.SetParent(arena.transform);
        try
        {
            hud.AddComponent<TrainingHUD>();
        }
        catch
        {
            Debug.LogWarning("TrainingHUD not found, skipping HUD setup");
        }
        
        // Add curriculum stage controller
        GameObject stageCtl = new GameObject("StageCurriculumController");
        stageCtl.transform.SetParent(arena.transform);
        try
        {
            var ctl = stageCtl.AddComponent<StageCurriculumController>();
            ctl.env = arena.GetComponent<DroneTrainingEnv>();
        }
        catch
        {
            Debug.LogWarning("StageCurriculumController not found, skipping controller setup");
        }

        // Add curriculum HUD (top-right)
        GameObject chud = new GameObject("CurriculumHUD");
        chud.transform.SetParent(arena.transform);
        try
        {
            chud.AddComponent<CurriculumHUD>();
        }
        catch
        {
            Debug.LogWarning("CurriculumHUD not found, skipping curriculum HUD setup");
        }
        
        Debug.Log(" Setup finalized - Ready for training!");
    }

    void GenerateAblationVariantsPreview()
    {
        Debug.Log("Ablation variants planned: \n" +
            "1) Baseline (vector only)\n" +
            "2) +Vision sensors\n" +
            "3) +Domain randomization\n" +
            "4) +Shared group rewards\n" +
            "5) All combined\n");
    }

    [System.Serializable]
    class ExperimentManifest
    {
        public int droneCount;
        public Vector2 arenaSize;
        public float ceilingHeight;
        public int obstacleCount;
        public int movingObstacleCount;
        public int aerialObstacleCount;
        public bool enableWind;
        public bool enableVisionPolicy;
        public bool enableDomainRandomization;
        public bool sharedGroupRewards;
        public float decisionHz;
        public string rewardConfigPath;
        public string createdUtc;
    }

    void WriteExperimentManifest(GameObject arena)
    {
        var env = arena.GetComponent<DroneTrainingEnv>();
        var manifest = new ExperimentManifest
        {
            droneCount = droneCount,
            arenaSize = arenaSize,
            ceilingHeight = ceilingHeight,
            obstacleCount = obstacleCount,
            movingObstacleCount = movingObstacleCount,
            aerialObstacleCount = aerialObstacleCount,
            enableWind = enableWind,
            enableVisionPolicy = enableVisionPolicy,
            enableDomainRandomization = env != null && env.enableDomainRandomization,
            sharedGroupRewards = useSharedGroupRewards,
            decisionHz = 10f,
            rewardConfigPath = "Assets/DroneRL/Rewards/OptimizedRewardConfig.asset",
            createdUtc = System.DateTime.UtcNow.ToString("o")
        };

        string json = UnityEngine.JsonUtility.ToJson(manifest, true);
        string folder = "Assets/DroneRL/Experiments";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/DroneRL", "Experiments");
        }
        string path = System.IO.Path.Combine(folder, manifestFileName);
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();
        Debug.Log($"📝 Wrote experiment manifest: {path}");
    }

    void FixExistingDrones()
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
            }
            
            // Fix DroneAgent sensor noise
            agent.velocityNoise = 0.02f;
            agent.gyroNoise = 0.01f;
            agent.positionNoise = 0.05f;
            agent.attitudeNoise = 0.2f;
            agent.maxStallSteps = 500;
            EditorUtility.SetDirty(agent);
            
            // Fix QuadController sensor noise
            var controller = agent.GetComponent<QuadController>();
            if (controller != null)
            {
                controller.sensorNoise = 0.02f;
                EditorUtility.SetDirty(controller);
            }
            
            fixedCount++;
        }
        
        Debug.Log($" Fixed {fixedCount} existing drones with optimized settings");
        EditorUtility.DisplayDialog("Drones Fixed", $"Applied optimized settings to {fixedCount} existing drones", "OK");
    }

    GameObject FindBestDronePrefab()
    {
        // Look for existing drone prefabs in common locations
        string[] searchPaths = {
            "Assets/Drone/prefab",
            "Assets/Prefabs", 
            "Assets",
            "Assets/DroneRL"
        };
        
        string[] prefabNames = {
            "drone",
            "drone Black",
            "drone black and white Variant",
            "drone blue", 
            "drone green",
            "drone orange",
            "DroneAgent", 
            "OptimizedDronePrefab",
            "DroneAgentPrefab"
        };
        
        foreach (string path in searchPaths)
        {
            foreach (string name in prefabNames)
            {
                string fullPath = $"{path}/{name}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
                if (prefab != null)
                {
                    // Check if it has the required components OR if it's a visual drone model
                    if (prefab.GetComponent<DroneAgent>() != null || 
                        prefab.GetComponent<QuadController>() != null ||
                        prefab.name.ToLower().Contains("drone"))
                    {
                        Debug.Log($"🎯 Found drone prefab: {prefab.name} at {fullPath}");
                        return prefab;
                    }
                }
            }
        }
        
        // Look for any prefab with DroneAgent component
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in allPrefabs)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null && prefab.GetComponent<DroneAgent>() != null)
            {
                return prefab;
            }
        }
        
        return null;
    }
    
    GameObject GetOrCreateDronePrefab()
    {
        GameObject dronePrefab = null;
        
        if (autoSelectDronePrefab)
        {
            dronePrefab = FindBestDronePrefab();
        }
        else
        {
            dronePrefab = selectedDronePrefab;
        }
        
        if (dronePrefab != null)
        {
            // Use existing prefab directly - NO optimization needed, just return it
            Debug.Log($" Using existing drone prefab: {dronePrefab.name}");
            return dronePrefab; // Return as-is, don't modify the original prefab
        }
        else
        {
            // Create new optimized prefab as fallback
            Debug.Log(" No suitable drone prefab found, creating new one");
            return CreateOptimizedDronePrefab();
        }
    }
    
    void OptimizeExistingPrefab(GameObject prefab)
    {
        // Create a temporary instance to modify
        GameObject temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (temp == null) return;
        
        try
        {
            // Apply optimizations to the temporary instance
            var adapter = temp.GetComponent<RLFlightAdapter>();
            if (adapter != null)
            {
                adapter.maxTiltCmd = 0.5f;
                adapter.maxYawCmd = 0.5f;
            }
            
            var agent = temp.GetComponent<DroneAgent>();
            if (agent != null)
            {
                agent.velocityNoise = 0.02f;
                agent.gyroNoise = 0.01f;
                agent.positionNoise = 0.05f;
                agent.attitudeNoise = 0.2f;
                agent.maxStallSteps = 500;
            }
            
            var controller = temp.GetComponent<QuadController>();
            if (controller != null)
            {
                controller.sensorNoise = 0.02f;
            }
            
            // Apply changes back to prefab
            PrefabUtility.ApplyPrefabInstance(temp, InteractionMode.AutomatedAction);
            Debug.Log($" Applied optimizations to prefab: {prefab.name}");
        }
        finally
        {
            // Clean up temporary instance
            Object.DestroyImmediate(temp);
        }
    }
    
    void CreateMovingObstacles(GameObject arena)
    {
        for (int i = 0; i < movingObstacleCount; i++)
        {
            // Create moving obstacle (sphere or cube)
            var shape = (i % 2 == 0) ? PrimitiveType.Sphere : PrimitiveType.Cube;
            GameObject obstacle = GameObject.CreatePrimitive(shape);
            obstacle.name = $"MovingObstacle_{i:D2}";
            obstacle.transform.SetParent(arena.transform, false);
            
            // Position at ground level initially
            Vector3 pos = new Vector3(
                Random.Range(-arenaSize.x * 0.3f, arenaSize.x * 0.3f),
                Random.Range(2f, 6f),
                Random.Range(-arenaSize.y * 0.3f, arenaSize.y * 0.3f)
            );
            obstacle.transform.position = pos;
            
            float scale = Random.Range(1f, 2.5f);
            obstacle.transform.localScale = Vector3.one * scale;
            
            // Setup physics for moving obstacle
            var collider = obstacle.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
            obstacle.layer = 0;
            
            var rb = obstacle.AddComponent<Rigidbody>();
            rb.isKinematic = false; // Allow movement
            rb.useGravity = false; // Don't fall
            rb.drag = 2f; // Add some drag for smoother movement
            rb.angularDrag = 5f;
            
            // Add moving behavior script
            var mover = obstacle.AddComponent<SimpleObstacleMover>();
            mover.moveSpeed = Random.Range(2f, 8f);
            mover.moveRange = Random.Range(5f, 15f);
            mover.movePattern = (SimpleObstacleMover.MovementPattern)(i % 3); // Cycle through patterns
            
            // Color moving obstacles (bright colors)
            var renderer = obstacle.GetComponent<Renderer>();
            Material movingMat = new Material(Shader.Find("Standard"));
            movingMat.color = new Color(Random.Range(0.7f, 1f), Random.Range(0.3f, 0.8f), Random.Range(0.3f, 0.8f));
            movingMat.SetFloat("_Metallic", 0.4f);
            movingMat.SetFloat("_Smoothness", 0.8f);
            movingMat.EnableKeyword("_EMISSION");
            movingMat.SetColor("_EmissionColor", movingMat.color * 0.3f); // Slight glow
            renderer.material = movingMat;
            
            Debug.Log($"🚀 Created moving obstacle: {obstacle.name} with {mover.movePattern} pattern");
        }
    }
    
    void CreateAerialObstacles(GameObject arena)
    {
        for (int i = 0; i < aerialObstacleCount; i++)
        {
            // Create aerial obstacle (cylinder or capsule for variety)
            var shape = (i % 2 == 0) ? PrimitiveType.Cylinder : PrimitiveType.Capsule;
            GameObject obstacle = GameObject.CreatePrimitive(shape);
            obstacle.name = $"AerialObstacle_{i:D2}";
            obstacle.transform.SetParent(arena.transform, false);
            
            // Position in the air (various heights)
            Vector3 pos = new Vector3(
                Random.Range(-arenaSize.x * 0.4f, arenaSize.x * 0.4f),
                Random.Range(8f, ceilingHeight - 3f), // High in the air
                Random.Range(-arenaSize.y * 0.4f, arenaSize.y * 0.4f)
            );
            obstacle.transform.position = pos;
            
            // Random rotation for more interesting shapes
            obstacle.transform.rotation = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );
            
            // Varied sizes
            float scale = Random.Range(1.5f, 4f);
            Vector3 scaleVec = new Vector3(scale, Random.Range(scale * 0.5f, scale * 1.5f), scale);
            obstacle.transform.localScale = scaleVec;
            
            // Setup physics
            var collider = obstacle.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
            }
            obstacle.layer = 0;
            
            var rb = obstacle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            
            // Color aerial obstacles (sky-like colors)
            var renderer = obstacle.GetComponent<Renderer>();
            Material aerialMat = new Material(Shader.Find("Standard"));
            aerialMat.color = new Color(
                Random.Range(0.4f, 0.8f), 
                Random.Range(0.6f, 0.9f), 
                Random.Range(0.8f, 1f)
            ); // Bluish tones
            aerialMat.SetFloat("_Metallic", 0.6f);
            aerialMat.SetFloat("_Smoothness", 0.9f);
            renderer.material = aerialMat;
            
            Debug.Log($"☁️ Created aerial obstacle: {obstacle.name} at height {pos.y:F1}m");
        }
    }
}

/// <summary>
/// Simple script to make obstacles move in predictable patterns
/// </summary>
public class SimpleObstacleMover : MonoBehaviour
{
    public enum MovementPattern
    {
        Linear,     // Back and forth
        Circular,   // Circular motion
        Figure8     // Figure-8 pattern
    }
    
    public MovementPattern movePattern = MovementPattern.Linear;
    public float moveSpeed = 5f;
    public float moveRange = 10f;
    
    private Vector3 startPosition;
    private float time = 0f;
    
    void Start()
    {
        startPosition = transform.position;
        time = Random.Range(0f, 2f * Mathf.PI); // Random starting phase
    }
    
    void Update()
    {
        time += Time.deltaTime * moveSpeed;
        
        switch (movePattern)
        {
            case MovementPattern.Linear:
                // Move back and forth along X axis
                float offsetX = Mathf.Sin(time) * moveRange;
                transform.position = startPosition + new Vector3(offsetX, 0f, 0f);
                break;
                
            case MovementPattern.Circular:
                // Circular motion in XZ plane
                float circleX = Mathf.Cos(time) * moveRange * 0.5f;
                float circleZ = Mathf.Sin(time) * moveRange * 0.5f;
                transform.position = startPosition + new Vector3(circleX, 0f, circleZ);
                break;
                
            case MovementPattern.Figure8:
                // Figure-8 pattern
                float fig8X = Mathf.Sin(time) * moveRange * 0.7f;
                float fig8Z = Mathf.Sin(2f * time) * moveRange * 0.4f;
                transform.position = startPosition + new Vector3(fig8X, 0f, fig8Z);
                break;
        }
        
        // Slow rotation for visual interest
        transform.Rotate(Vector3.up, Time.deltaTime * 30f);
    }
}
