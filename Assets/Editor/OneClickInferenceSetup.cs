using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using Unity.Barracuda;
using System.IO;

public class OneClickInferenceSetup : EditorWindow
{
    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(80f,80f);
    public float ceilingHeight = 25f;
    public bool enableWind = false;
    [Header("Obstacles")]
    public int staticObstacleCount = 12;
    public int movingObstacleCount = 4;

    [Header("Drone")]
    public GameObject dronePrefab;
    public NNModel nnModel;
    public bool useLatestOnnxFromResults = true;

    [Header("Recording")]
    public bool enableRecording = true;
    public string outputFolder = "Recordings";
    public int frameRate = 60;
    
    [Header("Smooth Recording")]
    public bool enableSmoothRecording = true;
    public bool applyOptimalSettings = true;

    [MenuItem("Tools/DroneRL/ One-Click Inference Setup", priority = 1)]
    public static void ShowWindow()
    {
        var w = GetWindow<OneClickInferenceSetup>("Inference Setup");
        w.minSize = new Vector2(400, 320);
        w.Show();
    }

    void OnGUI()
    {
        GUILayout.Label(" Inference Environment Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        arenaSize = EditorGUILayout.Vector2Field("Arena Size", arenaSize);
        ceilingHeight = EditorGUILayout.FloatField("Ceiling Height", ceilingHeight);
        enableWind = EditorGUILayout.Toggle("Enable Wind", enableWind);
        EditorGUILayout.Space();
        dronePrefab = (GameObject)EditorGUILayout.ObjectField("Drone Prefab", dronePrefab, typeof(GameObject), false);
        nnModel = (NNModel)EditorGUILayout.ObjectField("ONNX Model", nnModel, typeof(NNModel), false);
        useLatestOnnxFromResults = EditorGUILayout.Toggle("Use Latest ONNX from results/", useLatestOnnxFromResults);
        EditorGUILayout.Space();
        enableRecording = EditorGUILayout.Toggle("Enable Recording", enableRecording);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        frameRate = EditorGUILayout.IntField("Frame Rate", frameRate);
        EditorGUILayout.Space();
        enableSmoothRecording = EditorGUILayout.Toggle("Enable Smooth Recording", enableSmoothRecording);
        applyOptimalSettings = EditorGUILayout.Toggle("Apply Optimal Unity Settings", applyOptimalSettings);
        EditorGUILayout.Space();
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("Build Inference Scene", GUILayout.Height(36)))
        {
            BuildInferenceScene();
        }
        GUI.backgroundColor = Color.white;
    }

    void BuildInferenceScene()
    {
        try
        {
            EditorUtility.DisplayProgressBar("Inference Setup", "Creating arena", 0.2f);
            var root = new GameObject("InferenceArena");
            var env = root.AddComponent<DroneTrainingEnv>();
            env.arenaSize = arenaSize;
            env.ceilingHeight = ceilingHeight;
            env.agentCount = 0; env.proceduralArenaGeometry = false; env.proceduralObstacles = false;
            env.enableWind = enableWind; env.maxWindStrength = 2f;

            // Create the same arena geometry as training setup (no cross-section)
            CreateArenaGeometry(root);

            EditorUtility.DisplayProgressBar("Inference Setup", "Spawning drone", 0.5f);
            if (dronePrefab == null) throw new System.Exception("Please assign a Drone Prefab");
            var drone = (GameObject)PrefabUtility.InstantiatePrefab(dronePrefab);
            drone.name = "InferenceDrone";
            drone.transform.SetParent(root.transform, false);
            drone.transform.position = new Vector3(0f, 2f, 0f);
            // Resolve latest model if requested
            NNModel modelToUse = nnModel;
            if (useLatestOnnxFromResults)
            {
                var latest = ImportLatestOnnxAsNNModel();
                if (latest != null) modelToUse = latest;
            }
            EnsureInferenceBindings(drone, modelToUse);

            // Add attractive goal with professional appearance
            var goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goal.name = "Goal"; goal.transform.SetParent(root.transform, false);
            goal.transform.localScale = Vector3.one * 3f; // Slightly larger for better visibility
            
            // Professional goal appearance - bright green with glow effect
            var goalRenderer = goal.GetComponent<Renderer>();
            Material goalMat = new Material(Shader.Find("Standard"));
            goalMat.color = new Color(0.2f, 1.0f, 0.3f); // Bright success green
            goalMat.SetFloat("_Metallic", 0.1f);
            goalMat.SetFloat("_Smoothness", 0.9f);
            goalMat.EnableKeyword("_EMISSION");
            goalMat.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.1f)); // Subtle green glow
            goalRenderer.material = goalMat;
            
            var col = goal.GetComponent<SphereCollider>(); col.isTrigger = true;
            var gz = goal.AddComponent<GoalZone>();
            var agent = drone.GetComponent<DroneAgent>(); agent.goal = goal.transform; agent.env = env; gz.Assign(agent);
            Vector3 gpos = new Vector3(
                Random.Range(-arenaSize.x*0.35f, arenaSize.x*0.35f),
                Mathf.Clamp(Random.Range(3f, ceilingHeight*0.6f), 3f, ceilingHeight-3f),
                Random.Range(-arenaSize.y*0.35f, arenaSize.y*0.35f)
            );
            goal.transform.position = gpos;

            EditorUtility.DisplayProgressBar("Inference Setup", "Adding cameras & HUD", 0.7f);
            AddCameras(root);
            AddHUD(root);
            AddProfessionalLighting(root);

            // Place obstacles for a showcase navigation course
            CreateObstacles(root, staticObstacleCount, movingObstacleCount);

            if (enableRecording)
            {
                SetupRecording(root);
            }
            
            if (enableSmoothRecording && applyOptimalSettings)
            {
                ApplySmoothRecordingSettings();
            }

            Selection.activeGameObject = root;
            Debug.Log(" Inference scene ready. Press Play and record smooth professional footage!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Inference setup failed: {ex.Message}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void EnsureInferenceBindings(GameObject drone, NNModel model)
    {
        var bp = drone.GetComponent<BehaviorParameters>();
        if (bp == null) bp = drone.AddComponent<BehaviorParameters>();
        bp.BehaviorName = "DroneAgent";
        bp.BehaviorType = BehaviorType.InferenceOnly;
        if (model != null)
        {
            bp.Model = model;
        }
        else
        {
            Debug.LogWarning("No NNModel assigned; the drone will run without an inference model.");
        }

        if (drone.GetComponent<Rigidbody>() == null) drone.AddComponent<Rigidbody>();
        
        var quadController = drone.GetComponent<QuadController>();
        if (quadController == null) quadController = drone.AddComponent<QuadController>();
        
        // Configure for fast, responsive inference flight
        quadController.thrustToWeight = 2.8f;        // Higher thrust for faster acceleration
        quadController.motorTimeConstant = 0.08f;    // Faster motor response
        quadController.maxTiltDegrees = 45f;         // Allow more aggressive tilting
        quadController.yawRate = 180f;               // Faster yaw response
        
        if (drone.GetComponent<RLFlightAdapter>() == null) drone.AddComponent<RLFlightAdapter>();
        if (drone.GetComponent<DroneAgent>() == null) drone.AddComponent<DroneAgent>();
        
        // Add DecisionRequester for ultra-smooth inference
        var decisionRequester = drone.GetComponent<Unity.MLAgents.DecisionRequester>();
        if (decisionRequester == null) 
        {
            decisionRequester = drone.AddComponent<Unity.MLAgents.DecisionRequester>();
        }
        decisionRequester.DecisionPeriod = 1; // Every physics step for maximum responsiveness (100Hz)
        decisionRequester.TakeActionsBetweenDecisions = true;
        
        // Ensure DroneAgent uses the new ultra-smooth decision frequency
        var droneAgent = drone.GetComponent<DroneAgent>();
        if (droneAgent != null)
        {
            // The DroneAgent now uses 100Hz decisions (0.01s interval) for silky smooth movement
            Debug.Log(" DroneAgent configured for 100Hz ultra-smooth decisions");
        }
    }

    void AddCameras(GameObject root)
    {
        var main = new GameObject("InferenceCamera");
        main.transform.SetParent(root.transform, false);
        var cam = main.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 60f; cam.farClipPlane = 200f;
        main.transform.position = new Vector3(0, ceilingHeight * 0.7f, -arenaSize.y * 0.5f);
        main.transform.LookAt(Vector3.zero);

        try { main.AddComponent<MultiDroneCameraController>(); } catch { }
    }

    void AddHUD(GameObject root)
    {
        var hud = new GameObject("InferenceHUD");
        hud.transform.SetParent(root.transform, false);
        var hudComp = hud.AddComponent<DroneRL.Stats.InferenceHUD>();
        // Use dedicated InferenceHUD for evaluation metrics and model information
    }
    
    void AddProfessionalLighting(GameObject root)
    {
        // Main directional light (sun/skylight through glass ceiling)
        var mainLight = new GameObject("Main Directional Light");
        mainLight.transform.SetParent(root.transform, false);
        var light = mainLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f, 0.95f, 0.8f); // Warm white like natural light
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        mainLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        
        // Ambient gym lighting - multiple point lights for even coverage
        Vector3[] lightPositions = {
            new Vector3(-arenaSize.x*0.3f, ceilingHeight*0.8f, -arenaSize.y*0.3f),
            new Vector3(arenaSize.x*0.3f, ceilingHeight*0.8f, -arenaSize.y*0.3f),
            new Vector3(-arenaSize.x*0.3f, ceilingHeight*0.8f, arenaSize.y*0.3f),
            new Vector3(arenaSize.x*0.3f, ceilingHeight*0.8f, arenaSize.y*0.3f)
        };
        
        for (int i = 0; i < lightPositions.Length; i++)
        {
            var gymLight = new GameObject($"Gym Light {i+1}");
            gymLight.transform.SetParent(root.transform, false);
            gymLight.transform.position = lightPositions[i];
            
            var pointLight = gymLight.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(0.9f, 0.95f, 1.0f); // Cool white like LED gym lights
            pointLight.intensity = 0.8f;
            pointLight.range = arenaSize.x * 0.7f;
            pointLight.shadows = LightShadows.Soft;
        }
        
        // Set nice ambient lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1.0f); // Blue sky
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f); // Neutral
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f); // Dark ground
        RenderSettings.ambientIntensity = 0.3f;
    }

    void CreateObstacles(GameObject root, int statics, int movers)
    {
        // Create modern gym equipment-style obstacles with distinct colors
        Color[] obstacleColors = {
            new Color(0.8f, 0.2f, 0.2f), // Red - like gym equipment
            new Color(0.2f, 0.6f, 0.8f), // Blue - professional
            new Color(0.3f, 0.7f, 0.3f), // Green - nature accent
            new Color(0.6f, 0.3f, 0.8f), // Purple - modern accent
            new Color(0.9f, 0.5f, 0.1f)  // Orange - warning/visibility
        };
        
        for (int i = 0; i < statics; i++)
        {
            var shape = (i % 3 == 0) ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            var o = GameObject.CreatePrimitive(shape);
            o.name = $"Obstacle_{i:D2}"; o.transform.SetParent(root.transform, false);
            Vector3 p = new Vector3(
                Random.Range(-arenaSize.x*0.45f, arenaSize.x*0.45f),
                Random.Range(0.6f, ceilingHeight-1.5f),
                Random.Range(-arenaSize.y*0.45f, arenaSize.y*0.45f)
            );
            o.transform.position = p;
            float s = Random.Range(0.8f, 3.0f);
            o.transform.localScale = Vector3.one * s;
            
            // Modern gym equipment material
            var renderer = o.GetComponent<Renderer>();
            Material obstacleMat = new Material(Shader.Find("Standard"));
            obstacleMat.color = obstacleColors[i % obstacleColors.Length];
            obstacleMat.SetFloat("_Metallic", 0.3f);
            obstacleMat.SetFloat("_Smoothness", 0.7f);
            renderer.material = obstacleMat;
            
            var rb = o.AddComponent<Rigidbody>(); rb.isKinematic = true;
            var col = o.GetComponent<Collider>(); if (col != null) col.isTrigger = false;
        }
        
        for (int i = 0; i < movers; i++)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            o.name = $"MovingObstacle_{i:D2}"; o.transform.SetParent(root.transform, false);
            o.transform.position = new Vector3(
                Random.Range(-arenaSize.x*0.35f, arenaSize.x*0.35f),
                Random.Range(2f, ceilingHeight*0.7f),
                Random.Range(-arenaSize.y*0.35f, arenaSize.y*0.35f)
            );
            o.transform.localScale = Vector3.one * Random.Range(1.0f, 2.0f);
            
            // Moving obstacles - bright warning colors
            var renderer = o.GetComponent<Renderer>();
            Material movingMat = new Material(Shader.Find("Standard"));
            movingMat.color = new Color(1.0f, 0.4f, 0.0f); // Bright orange for moving hazards
            movingMat.SetFloat("_Metallic", 0.1f);
            movingMat.SetFloat("_Smoothness", 0.8f);
            renderer.material = movingMat;
            
            var rb = o.AddComponent<Rigidbody>(); rb.isKinematic = false; rb.useGravity = false; rb.drag = 2f; rb.angularDrag = 5f;
            var col = o.GetComponent<Collider>(); if (col != null) col.isTrigger = false;
            var mover = o.AddComponent<SimpleObstacleMover>();
            mover.moveSpeed = Random.Range(1.5f, 3.0f);
            mover.moveRange = Random.Range(6f, 12f);
        }
    }

    void CreateArenaGeometry(GameObject arena)
    {
        // Create modern gym floor with nice materials
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Arena_Floor";
        floor.transform.SetParent(arena.transform, false);
        floor.transform.localScale = new Vector3(arenaSize.x / 10f, 1f, arenaSize.y / 10f);
        floor.transform.position = new Vector3(0f, 0f, 0f);
        
        // Modern gym floor material - clean white with subtle grid pattern
        var floorRenderer = floor.GetComponent<Renderer>();
        Material floorMat = new Material(Shader.Find("Standard"));
        floorMat.color = new Color(0.95f, 0.95f, 0.98f, 1f); // Clean white-blue
        floorMat.SetFloat("_Metallic", 0.1f);
        floorMat.SetFloat("_Smoothness", 0.8f); // Polished gym floor
        floorRenderer.material = floorMat;
        
        // Ensure floor has solid collider and proper layer
        var floorCollider = floor.GetComponent<Collider>();
        if (floorCollider != null)
        {
            floorCollider.isTrigger = false;
        }
        
        // Set to Default layer (0) to ensure raycast detection
        floor.layer = 0;
        
        var floorRb = floor.AddComponent<Rigidbody>();
        floorRb.isKinematic = true;
        floorRb.useGravity = false;
        
        // Create walls (visible in scene) - same as training setup
        CreateWall(arena, "Wall_North", new Vector3(0, ceilingHeight * 0.5f, arenaSize.y * 0.5f + 0.5f), new Vector3(arenaSize.x, ceilingHeight, 1f));
        CreateWall(arena, "Wall_South", new Vector3(0, ceilingHeight * 0.5f, -arenaSize.y * 0.5f - 0.5f), new Vector3(arenaSize.x, ceilingHeight, 1f));
        CreateWall(arena, "Wall_East", new Vector3(arenaSize.x * 0.5f + 0.5f, ceilingHeight * 0.5f, 0), new Vector3(1f, ceilingHeight, arenaSize.y));
        CreateWall(arena, "Wall_West", new Vector3(-arenaSize.x * 0.5f - 0.5f, ceilingHeight * 0.5f, 0), new Vector3(1f, ceilingHeight, arenaSize.y));
        
        // Create modern gym ceiling - glass-like skylight effect
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ceiling.name = "Arena_Ceiling";
        ceiling.transform.SetParent(arena.transform, false);
        ceiling.transform.localScale = new Vector3(arenaSize.x / 10f, 1f, arenaSize.y / 10f);
        ceiling.transform.position = new Vector3(0f, ceilingHeight, 0f);
        ceiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        
        // Glass skylight ceiling material
        var ceilingRenderer = ceiling.GetComponent<Renderer>();
        Material ceilingMat = new Material(Shader.Find("Standard"));
        ceilingMat.color = new Color(0.8f, 0.9f, 1.0f, 0.2f); // Light blue tint like glass
        ceilingMat.SetFloat("_Metallic", 0.1f);
        ceilingMat.SetFloat("_Smoothness", 0.9f); // Very smooth like glass
        ceilingMat.SetFloat("_Mode", 3); // Transparent mode
        ceilingMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ceilingMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ceilingMat.SetInt("_ZWrite", 0);
        ceilingMat.DisableKeyword("_ALPHATEST_ON");
        ceilingMat.EnableKeyword("_ALPHABLEND_ON");
        ceilingMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ceilingMat.renderQueue = 3000;
        ceilingRenderer.material = ceilingMat;
        
        // Ensure ceiling has solid collider and proper layer
        var ceilingCollider = ceiling.GetComponent<Collider>();
        if (ceilingCollider != null)
        {
            ceilingCollider.isTrigger = false;
        }
        
        ceiling.layer = 0;
        
        var ceilingRb = ceiling.AddComponent<Rigidbody>();
        ceilingRb.isKinematic = true;
        ceilingRb.useGravity = false;
    }
    
    void CreateWall(GameObject parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        
        // Modern gym walls - clean blue-gray with professional look
        var renderer = wall.GetComponent<Renderer>();
        Material wallMat = new Material(Shader.Find("Standard"));
        wallMat.color = new Color(0.2f, 0.3f, 0.5f, 0.8f); // Professional blue-gray
        wallMat.SetFloat("_Metallic", 0.2f);
        wallMat.SetFloat("_Smoothness", 0.6f);
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

    NNModel ImportLatestOnnxAsNNModel()
    {
        try
        {
            string projectRoot = Path.GetFullPath(".");
            string resultsDir = Path.Combine(projectRoot, "results");
            if (!Directory.Exists(resultsDir)) return null;
            var files = Directory.GetFiles(resultsDir, "*.onnx", SearchOption.AllDirectories);
            if (files.Length == 0) return null;
            string latest = null; System.DateTime latestTime = System.DateTime.MinValue;
            foreach (var f in files)
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > latestTime) { latestTime = t; latest = f; }
            }
            if (latest == null) return null;
            string targetDir = "Assets/TrainedModels";
            if (!AssetDatabase.IsValidFolder(targetDir))
            {
                AssetDatabase.CreateFolder("Assets", "TrainedModels");
            }
            string targetPath = Path.Combine(targetDir, "LatestDroneAgent.onnx");
            File.Copy(latest, targetPath, true);
            AssetDatabase.ImportAsset(targetPath);
            var model = AssetDatabase.LoadAssetAtPath<NNModel>(targetPath);
            if (model != null) Debug.Log($" Imported latest ONNX: {latest} -> {targetPath}");
            return model;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Could not import latest ONNX: {ex.Message}");
            return null;
        }
    }

    void SetupRecording(GameObject root)
    {
        // Simple PNG sequence recorder using Application.CaptureScreenshot equivalent
        var rec = root.GetComponent<SimpleFrameRecorder>();
        if (rec == null) rec = root.AddComponent<SimpleFrameRecorder>();
        rec.outputFolder = outputFolder;
        rec.frameRate = Mathf.Max(1, frameRate);
    }
    
    void ApplySmoothRecordingSettings()
    {
        Debug.Log(" Applying smooth recording settings...");
        
        // Physics settings for ultra-smooth movement
        Time.fixedDeltaTime = 0.01f;        // 100Hz physics (ultra-smooth)
        Time.maximumDeltaTime = 0.01f;      // Prevent physics lag spikes
        
        // Rendering settings for smooth visuals
        Application.targetFrameRate = 60;    // 60 FPS target
        QualitySettings.vSyncCount = 1;      // V-Sync enabled for smooth display
        QualitySettings.antiAliasing = 8;    // 8x MSAA for professional quality
        
        // Physics solver settings for stability
        Physics.defaultSolverIterations = 8;
        Physics.defaultSolverVelocityIterations = 2;
        Physics.bounceThreshold = 0.2f;
        
        // Graphics settings for professional appearance
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowDistance = 150f;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        
        Debug.Log(" Smooth recording settings applied!");
        Debug.Log(" Physics: 100Hz | Rendering: 60 FPS | V-Sync: ON | AA: 8x | Drone decisions: 100Hz");
        
        EditorUtility.DisplayDialog(
            " Smooth Recording Ready!",
            "Unity is now optimized for ultra-smooth drone recording:\n\n" +
            "• Physics: 100Hz (silky smooth movement)\n" +
            "• Rendering: 60 FPS with V-Sync\n" +
            "• Anti-aliasing: 8x MSAA (professional quality)\n" +
            "• Drone decisions: 100Hz (ultra-responsive)\n" +
            "• Enhanced shadows and filtering\n\n" +
            "Perfect for creating professional demonstration videos!",
            "Ready to Record!"
        );
    }
}

public class SimpleFrameRecorder : MonoBehaviour
{
    public string outputFolder = "Recordings";
    public int frameRate = 60;
    private int frameIndex;
    private string fullPath;

    void Start()
    {
        Time.captureFramerate = frameRate;
        fullPath = Path.IsPathRooted(outputFolder) ? outputFolder : Path.Combine(Application.persistentDataPath, outputFolder);
        if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        frameIndex = 0;
        Debug.Log($"🎥 Recording to {fullPath} at {frameRate} FPS (PNG sequence)");
    }

    void LateUpdate()
    {
        string file = Path.Combine(fullPath, $"frame_{frameIndex:D06}.png");
        ScreenCapture.CaptureScreenshot(file);
        frameIndex++;
    }
}


