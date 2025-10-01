#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Actuators; // for ActionSpec
using System.IO;

/// <summary>
/// One-click scene bootstrap for the Drone RL setup.
/// Menu: Tools/DroneRL/Full Setup
/// Creates (if missing):
///   - Root GameObject with DroneTrainingEnv
///   - Reward config asset (RLRewardConfig)
///   - Drone agent prefab (Rigidbody + QuadController + RLFlightAdapter + DroneAgent + BehaviorParameters)
///   - RL HUD Manager object
/// Assigns prefab & reward config to the environment.
/// Safe to run multiple times (idempotent-ish); will reuse existing assets / objects if found.
/// </summary>
public static class DroneRLFullSetup
{
    private const string RootName = "DroneTrainingArena";
    private const string RewardAssetName = "AutoRewardConfig.asset";
    private const string RewardFolder = "Assets/DroneRL/Rewards";
    private const string PrefabFolder = "Assets/DroneRL/Prefabs";
    private const string PrefabName = "DroneAgentPrefab.prefab";

    [MenuItem("Tools/DroneRL/Full Setup", priority = 10)]
    public static void DoFullSetup()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        try
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create DroneTrainingArena root");
            }

            // Ensure environment component
            var env = root.GetComponent<DroneTrainingEnv>();
            if (env == null)
            {
                env = Undo.AddComponent<DroneTrainingEnv>(root);
            }
            
            // Add multi-drone environment
            var multiEnv = root.GetComponent<MultiDroneTrainingEnv>();
            if (multiEnv == null)
            {
                multiEnv = Undo.AddComponent<MultiDroneTrainingEnv>(root);
            }
            
            // Add curriculum learning
            var curriculum = root.GetComponent<DroneCurriculumLearning>();
            if (curriculum == null)
            {
                curriculum = Undo.AddComponent<DroneCurriculumLearning>(root);
            }
            
            // Add advanced training environments
            var advancedEnv = root.GetComponent<AdvancedTrainingEnvironments>();
            if (advancedEnv == null)
            {
                advancedEnv = Undo.AddComponent<AdvancedTrainingEnvironments>(root);
            }
            
            // Configure to work inside a pre-built arena (similar to Build Drone Training Arena script output)
            env.proceduralArenaGeometry = false; // assume arena geometry is already in scene or user will add it
            env.proceduralObstacles = false;     // designer-driven obstacles; user can enable later

            // Find or create reward config asset
            RLRewardConfig reward = FindExistingRewardConfig();
            if (reward == null)
            {
                Directory.CreateDirectory(RewardFolder);
                reward = ScriptableObject.CreateInstance<RLRewardConfig>();
                AssetDatabase.CreateAsset(reward, Path.Combine(RewardFolder, RewardAssetName));
                AssetDatabase.SaveAssets();
                Debug.Log("[DroneRL Full Setup] Created reward config asset at " + RewardFolder + "/" + RewardAssetName);
            }

            // Find or create agent prefab
            GameObject prefab = EnsureAgentPrefab();
            env.agentPrefab = prefab;
            
            // Assign prefab to multi-drone environment
            multiEnv.droneAgentPrefab = prefab;
            multiEnv.rewardConfig = reward;
            env.rewardConfig = reward;

            // Create or locate agent prefab
            GameObject agentPrefab = EnsureAgentPrefab();
            env.agentPrefab = agentPrefab;

            // HUD Manager
            var hud = GameObject.Find("RL HUD Manager");
            if (hud == null)
            {
                hud = new GameObject("RL HUD Manager");
                Undo.RegisterCreatedObjectUndo(hud, "Create RL HUD Manager");
                var hudComp = Undo.AddComponent<RLHUDManager>(hud);
                hudComp.autoFindAgent = true;
            }

            // Directional Light (basic lighting if missing)
            if (GameObject.FindObjectOfType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Undo.RegisterCreatedObjectUndo(lightGO, "Create Directional Light");
            }

            // Main Camera (ensure presence)
            if (Camera.main == null)
            {
                var camGO = new GameObject("Main Camera");
                var cam = camGO.AddComponent<Camera>();
                cam.tag = "MainCamera";
                camGO.transform.position = new Vector3(-env.arenaSize.x * 0.4f, env.ceilingHeight * 0.6f, -env.arenaSize.y * 0.4f);
                camGO.transform.LookAt(root.transform);
                Undo.RegisterCreatedObjectUndo(camGO, "Create Main Camera");
            }

            EditorUtility.SetDirty(env);
            AssetDatabase.SaveAssets();

            Debug.Log("[DroneRL Full Setup] Complete. Press Play to auto-spawn agents & goals.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[DroneRL Full Setup] Error: " + ex.Message + "\n" + ex.StackTrace);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static RLRewardConfig FindExistingRewardConfig()
    {
        var guids = AssetDatabase.FindAssets("t:RLRewardConfig");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var asset = AssetDatabase.LoadAssetAtPath<RLRewardConfig>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    private static GameObject LoadAgentPrefab()
    {
        string prefabPath = Path.Combine(PrefabFolder, PrefabName);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return prefab;
    }

    private static GameObject EnsureAgentPrefab()
    {
        Directory.CreateDirectory(PrefabFolder);
        string prefabPath = Path.Combine(PrefabFolder, PrefabName);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing == null)
        {
            // Create fresh prefab
            var temp = new GameObject("DroneAgentPrefabTEMP");
            AddOrGetRequiredComponents(temp);
            var saved = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            Debug.Log("[DroneRL Full Setup] Created agent prefab at " + prefabPath);
            return saved;
        }
        else
        {
            // Open for editing and ensure components
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            AddOrGetRequiredComponents(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.Log("[DroneRL Full Setup] Verified agent prefab components at " + prefabPath);
            return existing;
        }
    }

    private static void AddOrGetRequiredComponents(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.mass = 1.2f; rb.drag = 0.05f; rb.angularDrag = 0.05f;
        if (go.GetComponent<QuadController>() == null) go.AddComponent<QuadController>();
        if (go.GetComponent<RLFlightAdapter>() == null) go.AddComponent<RLFlightAdapter>();
        
        // Add advanced systems
        if (go.GetComponent<DroneAdvancedSensors>() == null) go.AddComponent<DroneAdvancedSensors>();
        if (go.GetComponent<DroneNavigationSystem>() == null) go.AddComponent<DroneNavigationSystem>();
        
        var agent = go.GetComponent<DroneAgent>();
        if (agent == null) agent = go.AddComponent<DroneAgent>();
        var bp = go.GetComponent<BehaviorParameters>();
        if (bp == null) bp = go.AddComponent<BehaviorParameters>();
        if (string.IsNullOrEmpty(bp.BehaviorName)) bp.BehaviorName = "DroneAgent";
        // Provide a default continuous action spec (4) and placeholder observation size (updated at runtime by agent script)
        try
        {
            // Force continuous 4, no discrete
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
            // Derive expected observation size identical to DroneAgent logic:
            // BaseObs (19) + rays (agent.rayCount) + 2 (up/down) when useRaycasts + advanced sensors (38)
            int baseObs = 19;
            int rayObs = agent.useRaycasts ? (agent.rayCount + 2) : 0;
            int advancedObs = 38; // From DroneAdvancedSensors
            int expected = baseObs + rayObs + advancedObs;
            bp.BrainParameters.VectorObservationSize = expected;
        }
        catch { /* API differences across ML-Agents versions - ignore */ }
    }
}
#endif
