using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using Unity.Barracuda;

/// <summary>
/// Creates a clean inference test environment with a single drone using the trained model
/// </summary>
public class InferenceTestSetup : EditorWindow
{
    [MenuItem("Tools/Drone RL/Create Inference Test Scene")]
    public static void ShowWindow()
    {
        GetWindow<InferenceTestSetup>("Inference Test Setup");
    }
    
    private NNModel trainedModel;
    private GameObject dronePrefab;
    private bool createNewScene = true;
    
    private void OnGUI()
    {
        GUILayout.Label("Inference Test Environment Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("This will create a clean test environment with a single drone using your trained model for inference demonstration.", MessageType.Info);
        EditorGUILayout.Space();
        
        // Model assignment
        EditorGUILayout.LabelField("Trained Model:", EditorStyles.boldLabel);
        trainedModel = (NNModel)EditorGUILayout.ObjectField("DroneAgent Model", trainedModel, typeof(NNModel), false);
        
        if (trainedModel == null)
        {
            EditorGUILayout.HelpBox("Please assign the DroneAgent.onnx model from Assets/Resources", MessageType.Warning);
            if (GUILayout.Button("Auto-Find DroneAgent.onnx"))
            {
                trainedModel = Resources.Load<NNModel>("DroneAgent");
                if (trainedModel == null)
                {
                    var guids = AssetDatabase.FindAssets("DroneAgent t:NNModel");
                    if (guids.Length > 0)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        trainedModel = AssetDatabase.LoadAssetAtPath<NNModel>(path);
                    }
                }
            }
        }
        
        EditorGUILayout.Space();
        
        // Drone prefab
        EditorGUILayout.LabelField("Drone Prefab:", EditorStyles.boldLabel);
        dronePrefab = (GameObject)EditorGUILayout.ObjectField("Drone Prefab", dronePrefab, typeof(GameObject), false);
        
        if (dronePrefab == null)
        {
            EditorGUILayout.HelpBox("Please assign a drone prefab with DroneAgent component", MessageType.Warning);
            if (GUILayout.Button("Auto-Find Drone Prefab"))
            {
                var guids = AssetDatabase.FindAssets("DroneAgentPrefab t:Prefab");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
        }
        
        EditorGUILayout.Space();
        
        // Options
        createNewScene = EditorGUILayout.Toggle("Create New Scene", createNewScene);
        
        EditorGUILayout.Space();
        
        // Create button
        GUI.enabled = trainedModel != null;
        if (GUILayout.Button("Create Inference Test Environment", GUILayout.Height(40)))
        {
            CreateInferenceTestEnvironment();
        }
        GUI.enabled = true;
        
        if (trainedModel == null)
        {
            EditorGUILayout.HelpBox("Cannot create environment without a trained model", MessageType.Error);
        }
    }
    
    private void CreateInferenceTestEnvironment()
    {
        if (createNewScene)
        {
            // Create new scene
            var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            
            Debug.Log(" Created new scene for inference testing");
        }
        
        // Create the test environment
        CreateTestArena();
        CreateInferenceDrone();
        CreateGoal();
        CreateCamera();
        CreateLighting();
        
        Debug.Log(" Inference test environment created successfully!");
        Debug.Log(" Press Play to see the trained drone in action!");
        
        Close();
    }
    
    private void CreateTestArena()
    {
        // Create simple arena
        var arena = new GameObject("Inference Test Arena");
        
        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(arena.transform);
        floor.transform.localScale = new Vector3(5, 1, 5); // 50x50 floor
        floor.transform.position = Vector3.zero;
        
        // Add some obstacles for testing
        CreateObstacle(arena.transform, new Vector3(10, 1, 0), new Vector3(2, 2, 2), "Obstacle1");
        CreateObstacle(arena.transform, new Vector3(-8, 1, 8), new Vector3(1.5f, 3, 1.5f), "Obstacle2");
        CreateObstacle(arena.transform, new Vector3(5, 1, -10), new Vector3(3, 1, 3), "Obstacle3");
        
        Debug.Log(" Created test arena with obstacles");
    }
    
    private void CreateObstacle(Transform parent, Vector3 position, Vector3 scale, string name)
    {
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = name;
        obstacle.transform.SetParent(parent);
        obstacle.transform.position = position;
        obstacle.transform.localScale = scale;
        
        // Make it kinematic
        var rb = obstacle.GetComponent<Rigidbody>();
        if (rb == null) rb = obstacle.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        
        // Random color for visibility
        var renderer = obstacle.GetComponent<Renderer>();
        var colors = new Color[] { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta };
        renderer.material.color = colors[Random.Range(0, colors.Length)];
    }
    
    private void CreateInferenceDrone()
    {
        GameObject drone;
        
        if (dronePrefab != null)
        {
            // Use provided prefab
            drone = PrefabUtility.InstantiatePrefab(dronePrefab) as GameObject;
            drone.name = "Inference Test Drone";
        }
        else
        {
            // Create basic drone if no prefab
            drone = new GameObject("Inference Test Drone");
            drone.AddComponent<Rigidbody>();
            
            // Add visual representation
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(drone.transform);
            body.transform.localPosition = Vector3.zero;
            body.GetComponent<Renderer>().material.color = Color.cyan;
            
            // Add drone components (you may need to adjust these based on your setup)
            drone.AddComponent<QuadController>();
            drone.AddComponent<RLFlightAdapter>();
            drone.AddComponent<DroneAgent>();
        }
        
        // Position drone
        drone.transform.position = new Vector3(0, 3, 0);
        
        // Configure for inference
        ConfigureDroneForInference(drone);
        
        Debug.Log(" Created inference test drone");
    }
    
    private void ConfigureDroneForInference(GameObject drone)
    {
        // Get or add BehaviorParameters
        var behaviorParams = drone.GetComponent<BehaviorParameters>();
        if (behaviorParams == null)
        {
            behaviorParams = drone.AddComponent<BehaviorParameters>();
        }
        
        // Configure for inference
        behaviorParams.BehaviorName = "DroneAgent";
        behaviorParams.Model = trainedModel;
        behaviorParams.BehaviorType = BehaviorType.InferenceOnly;
        behaviorParams.InferenceDevice = InferenceDevice.CPU;
        
        // Get or add DecisionRequester
        var decisionRequester = drone.GetComponent<Unity.MLAgents.DecisionRequester>();
        if (decisionRequester == null)
        {
            decisionRequester = drone.AddComponent<Unity.MLAgents.DecisionRequester>();
        }
        
        decisionRequester.DecisionPeriod = 1;
        decisionRequester.TakeActionsBetweenDecisions = true;
        
        Debug.Log($" Configured drone for inference with model: {trainedModel.name}");
    }
    
    private void CreateGoal()
    {
        var goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goal.name = "Goal";
        goal.transform.position = new Vector3(15, 3, 10);
        goal.transform.localScale = Vector3.one * 2;
        
        // Make it a trigger
        var collider = goal.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        
        // Visual
        var renderer = goal.GetComponent<Renderer>();
        renderer.material.color = Color.green;
        
        // Add goal zone component
        var goalZone = goal.AddComponent<GoalZone>();
        
        // Find and assign to drone
        var drone = FindObjectOfType<DroneAgent>();
        if (drone != null)
        {
            drone.goal = goal.transform;
            goalZone.Assign(drone);
        }
        
        Debug.Log(" Created goal at position " + goal.transform.position);
    }
    
    private void CreateCamera()
    {
        var cameraObj = new GameObject("Inference Test Camera");
        var camera = cameraObj.AddComponent<Camera>();
        
        // Position camera to view the action
        cameraObj.transform.position = new Vector3(-5, 8, -15);
        cameraObj.transform.LookAt(Vector3.zero);
        
        // Add camera controller for following drone
        var cameraController = cameraObj.AddComponent<SimpleCameraFollow>();
        
        Debug.Log("📷 Created camera with follow script");
    }
    
    private void CreateLighting()
    {
        // Create directional light
        var lightObj = new GameObject("Directional Light");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        lightObj.transform.rotation = Quaternion.Euler(30, 30, 0);
        
        // Set sky color
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.cyan;
        RenderSettings.ambientEquatorColor = Color.gray;
        RenderSettings.ambientGroundColor = Color.black;
        
        Debug.Log(" Created lighting setup");
    }
}