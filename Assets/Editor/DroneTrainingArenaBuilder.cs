#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DroneTrainingArenaBuilder
{
    private const string ScenePath = "Assets/Scenes/DroneTrainingArena.unity";
    private const string RootName = "DroneTrainingArena";
    private const float ArenaSize = 200f;

    [MenuItem("Tools/Build Drone Training Arena")]
    public static void BuildArena()
    {
        // Create folder structure
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Scripts");
        EnsureFolder("Assets/Editor");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/ObstaclePrefabs");
        EnsureFolder("Assets/Prefabs/GoalPrefabs");
        EnsureFolder("Assets/Materials");

        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "DroneTrainingArena";

        // Root hierarchy
        var root = new GameObject(RootName);
        var env = new GameObject("Environment"); env.transform.SetParent(root.transform);
        var terrainParent = new GameObject("Terrain"); terrainParent.transform.SetParent(env.transform);
        var staticObs = new GameObject("StaticObstacles"); staticObs.transform.SetParent(env.transform);
        var dynamicObs = new GameObject("DynamicObstacles"); dynamicObs.transform.SetParent(env.transform);
        var goalZones = new GameObject("GoalZones"); goalZones.transform.SetParent(env.transform);
        var checkpoints = new GameObject("Checkpoints"); checkpoints.transform.SetParent(env.transform);

        var scriptsParent = new GameObject("Scripts"); scriptsParent.transform.SetParent(root.transform);
        var prefabsParent = new GameObject("Prefabs"); prefabsParent.transform.SetParent(root.transform);

        // Lighting and skybox
        CreateLighting();
        CreateSkyboxMaterial();

        // Camera
        CreateCamera();

        // Materials (robust shader selection)
        Shader litShader = GetSupportedLitShader();
        var grassMat = CreateMaterial("GrassMat", litShader, new Color(0.22f, 0.45f, 0.24f));
        var concreteMat = CreateMaterial("ConcreteMat", litShader, new Color(0.6f, 0.6f, 0.62f));
        var sandMat = CreateMaterial("SandMat", litShader, new Color(0.84f, 0.76f, 0.52f));
        var neutralGreyMat = CreateMaterial("NeutralGreyMat", litShader, new Color(0.5f, 0.5f, 0.5f));
        var emissiveGoalMat = CreateEmissiveMaterial("GoalEmissiveMat", new Color(1f, 0.6f, 0.1f), 5f);

        grassMat.enableInstancing = true;
        concreteMat.enableInstancing = true;
        sandMat.enableInstancing = true;
        neutralGreyMat.enableInstancing = true;
        emissiveGoalMat.enableInstancing = true;

        // Terrain (flat ground + zones)
        BuildGround(terrainParent.transform, grassMat, concreteMat, sandMat);

        // Prefabs
        var cubePrefab = CreatePrimitivePrefab("Assets/Prefabs/ObstaclePrefabs/CubeBlock.prefab", PrimitiveType.Cube, neutralGreyMat);
        var cylinderPrefab = CreatePrimitivePrefab("Assets/Prefabs/ObstaclePrefabs/CylinderTower.prefab", PrimitiveType.Cylinder, neutralGreyMat);
        var movingPlatformPrefab = CreatePrimitivePrefab("Assets/Prefabs/ObstaclePrefabs/MovingPlatform.prefab", PrimitiveType.Cube, neutralGreyMat);
        var propellerRootPrefab = CreatePropellerPrefab("Assets/Prefabs/ObstaclePrefabs/RotatingPropeller.prefab", neutralGreyMat);
        var goalPrefab = CreateGoalPrefab("Assets/Prefabs/GoalPrefabs/GoalPlatform.prefab", emissiveGoalMat);

        // Static obstacles: towers and cubes
        var staticCount = 60;
        var rng = new System.Random(12345);
        for (int i = 0; i < staticCount; i++)
        {
            bool isTower = (i % 2 == 0);
            var prefab = isTower ? cylinderPrefab : cubePrefab;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = (isTower ? "Tower_" : "Cube_") + i;
            go.transform.SetParent(staticObs.transform);

            var pos = RandomXZ(rng, ArenaSize * 0.48f);
            float height = isTower ? Mathf.Lerp(5f, 30f, (float)rng.NextDouble()) : Mathf.Lerp(1f, 4f, (float)rng.NextDouble());
            float width = Mathf.Lerp(1f, 3f, (float)rng.NextDouble());

            if (isTower)
            {
                go.transform.localScale = new Vector3(width, height * 0.5f, width); // Cylinder default height 2
                go.transform.position = new Vector3(pos.x, height * 0.5f, pos.y);
            }
            else
            {
                go.transform.localScale = new Vector3(width, height, width);
                go.transform.position = new Vector3(pos.x, height * 0.5f, pos.y);
            }

            Color bright = RandomBrightColor(rng);
            ApplyColor(go, bright);

            MarkStaticForOcclusion(go);
        }

        // Create some narrow passages/corridors
        for (int i = 0; i < 10; i++)
        {
            var a = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab, staticObs.scene);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(cubePrefab, staticObs.scene);
            a.name = $"PassageA_{i}";
            b.name = $"PassageB_{i}";
            a.transform.SetParent(staticObs.transform);
            b.transform.SetParent(staticObs.transform);

            Vector2 basePos = RandomXZ(rng, ArenaSize * 0.45f);
            float wallHeight = 4f;
            float wallThickness = 0.5f;
            float passageWidth = 2.0f + (float)rng.NextDouble() * 2.0f; // 2-4m
            float length = 6f + (float)rng.NextDouble() * 6f;

            float angle = (float)rng.NextDouble() * 360f;
            Quaternion rot = Quaternion.Euler(0, angle, 0);

            a.transform.position = new Vector3(basePos.x, wallHeight * 0.5f, basePos.y);
            b.transform.position = new Vector3(basePos.x, wallHeight * 0.5f, basePos.y);
            a.transform.rotation = rot;
            b.transform.rotation = rot;

            a.transform.localScale = new Vector3(wallThickness, wallHeight, length);
            b.transform.localScale = new Vector3(wallThickness, wallHeight, length);

            // Offset sideways by passageWidth/2
            var side = a.transform.right;
            a.transform.position -= side * (passageWidth * 0.5f);
            b.transform.position += side * (passageWidth * 0.5f);

            ApplyColor(a, RandomBrightColor(rng));
            ApplyColor(b, RandomBrightColor(rng));

            MarkStaticForOcclusion(a);
            MarkStaticForOcclusion(b);
        }

        // Navigation features
        BuildNavigationFeatures(staticObs.transform, cubePrefab, rng);

        // Dynamic obstacles: propellers and moving platforms
        int dynamicCount = 20;
        for (int i = 0; i < dynamicCount; i++)
        {
            bool isProp = (i % 2 == 0);
            if (isProp)
            {
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(propellerRootPrefab);
                prop.name = $"Propeller_{i}";
                prop.transform.SetParent(dynamicObs.transform);
                var pos = RandomXZ(rng, ArenaSize * 0.45f);
                float y = 2f + (float)rng.NextDouble() * 10f;
                prop.transform.position = new Vector3(pos.x, y, pos.y);

                var mover = prop.GetComponent<MovingObstacleController>();
                mover.mode = MovingObstacleController.Mode.Rotate;
                mover.angularSpeed = Mathf.Lerp(-180f, 180f, (float)rng.NextDouble());
            }
            else
            {
                var plat = (GameObject)PrefabUtility.InstantiatePrefab(movingPlatformPrefab);
                plat.name = $"MovingPlatform_{i}";
                plat.transform.SetParent(dynamicObs.transform);
                var pos = RandomXZ(rng, ArenaSize * 0.4f);
                float y = 1f + (float)rng.NextDouble() * 5f;
                plat.transform.position = new Vector3(pos.x, y, pos.y);
                plat.transform.localScale = new Vector3(3f + (float)rng.NextDouble() * 4f, 0.5f, 3f + (float)rng.NextDouble() * 4f);

                var mover = plat.AddComponent<MovingObstacleController>();
                mover.mode = MovingObstacleController.Mode.MoveBetweenPoints;
                mover.speed = 2f + (float)rng.NextDouble() * 3f;
                mover.waypoints = new List<Transform> { CreateWaypoint(plat.transform, new Vector3(0, 0, 0)), CreateWaypoint(plat.transform, new Vector3(rng.Next(-8, 9), 0, rng.Next(-8, 9))) };
                mover.loop = true;
            }
        }

        // Goal manager and goals (5 platforms at fixed heights but randomized positions per episode)
        var goalManagerGO = new GameObject("GoalManager");
        goalManagerGO.transform.SetParent(goalZones.transform);
        var goalManager = goalManagerGO.AddComponent<GoalManager>();
        goalManager.arenaSize = new Vector2(ArenaSize, ArenaSize);
        goalManager.goalPrefab = goalPrefab;
        goalManager.goalHeights = new float[] { 1f, 3f, 5f, 7f, 10f };
        goalManager.count = 5;
        goalManager.randomizeOnStart = true;

        // Checkpoints: invisible triggers at strategic spots (ring around center)
        BuildCheckpoints(checkpoints.transform, rng);

        // Random Obstacle Spawner
        var spawnerGO = new GameObject("RandomObstacleSpawner");
        spawnerGO.transform.SetParent(scriptsParent.transform);
        var spawner = spawnerGO.AddComponent<RandomObstacleSpawner>();
        spawner.arenaSize = new Vector2(ArenaSize, ArenaSize);
        spawner.minObjects = 10;
        spawner.maxObjects = 30;

        // DroneAgentPlaceholder + SpawnPoints
        var agentPlaceholder = new GameObject("DroneAgentPlaceholder");
        agentPlaceholder.transform.SetParent(root.transform);
        agentPlaceholder.transform.position = new Vector3(0, 1f, 0);

        var spawnParent = new GameObject("SpawnPoints");
        spawnParent.transform.SetParent(root.transform);
        CreateSpawnPoints(spawnParent.transform, 16, ArenaSize * 0.45f);

        // ArenaManager to manage resets
        var arenaManagerGO = new GameObject("ArenaManager");
        arenaManagerGO.transform.SetParent(root.transform);
        var arenaManager = arenaManagerGO.AddComponent<ArenaManager>();
        arenaManager.randomObstacleSpawner = spawner;
        arenaManager.goalManager = goalManager;
        arenaManager.spawnPointsParent = spawnParent.transform;

        // Save scene
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("DroneTrainingArena scene created at: " + ScenePath);
    }

    private static void CreateLighting()
    {
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1.0f;
        light.shadows = LightShadows.Soft;
        light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void CreateSkyboxMaterial()
    {
        var shader = Shader.Find("Skybox/Procedural");
        if (shader == null) shader = Shader.Find("Skybox/Cubemap");
        if (shader == null) shader = Shader.Find("Skybox/Blended");
        if (shader == null)
        {
            Debug.LogWarning("No skybox shader found. Using current RenderSettings.skybox.");
            return;
        }

        var skyMat = new Material(shader) { name = "ProceduralSkyboxMat" };
        EnsureFolder("Assets/Materials");
        var path = "Assets/Materials/ProceduralSkyboxMat.mat";
        AssetDatabase.CreateAsset(skyMat, path);
        RenderSettings.skybox = skyMat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
    }

    private static void CreateCamera()
    {
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.transform.position = new Vector3(0, 60f, -90f);
        cam.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        cam.allowHDR = true;
        cam.useOcclusionCulling = true;
    }

    private static void BuildGround(Transform parent, Material grass, Material concrete, Material sand)
    {
        GameObject makePlane(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = name;
            plane.transform.SetParent(parent);
            plane.transform.position = pos;
            plane.transform.localScale = scale; // Unity Plane is 10x10 units
            var mr = plane.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            var col = plane.GetComponent<Collider>();
            if (col) col.sharedMaterial = null;
            MarkStaticForOcclusion(plane);
            return plane;
        }

        // Base
        makePlane("Ground_Grass", new Vector3(0, 0, 0), new Vector3(ArenaSize / 10f, 1f, ArenaSize / 10f), grass);

        // Zones (rectangles)
        makePlane("Zone_Concrete", new Vector3(-ArenaSize * 0.25f, 0.01f, ArenaSize * 0.2f), new Vector3(6f, 1f, 10f), concrete);
        makePlane("Zone_Sand", new Vector3(ArenaSize * 0.3f, 0.01f, -ArenaSize * 0.25f), new Vector3(8f, 1f, 6f), sand);
    }

    private static GameObject CreatePrimitivePrefab(string path, PrimitiveType type, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = Path.GetFileNameWithoutExtension(path);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sharedMaterial = mat;
        // Ensure collider exists and is convex-appropriate for moving obstacles
        var col = go.GetComponent<Collider>();
        if (col) col.isTrigger = false;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreatePropellerPrefab(string path, Material armMat)
    {
        var root = new GameObject("RotatingPropeller");
        var controller = root.AddComponent<MovingObstacleController>();
        controller.mode = MovingObstacleController.Mode.Rotate;
        controller.angularSpeed = 90f;

        // central pole
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(root.transform);
        pole.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
        pole.transform.localPosition = new Vector3(0, 1.5f, 0);
        ApplyMaterial(pole, armMat);

        // arms
        for (int i = 0; i < 2; i++)
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm_" + i;
            arm.transform.SetParent(root.transform);
            arm.transform.localScale = new Vector3(8f, 0.2f, 0.3f);
            arm.transform.localPosition = Vector3.zero;
            arm.transform.localRotation = Quaternion.Euler(0, i * 90f, 0);
            ApplyMaterial(arm, armMat);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateGoalPrefab(string path, Material glowMat)
    {
        var root = new GameObject("GoalPlatform");
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePlate.name = "Plate";
        basePlate.transform.SetParent(root.transform);
        basePlate.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
        basePlate.transform.localPosition = Vector3.zero;
        ApplyMaterial(basePlate, glowMat);

        var col = basePlate.GetComponent<Collider>();
        if (col is CapsuleCollider) { /* default for cylinder */ }
        // Trigger for goal detection
        var trigger = basePlate.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 0.6f, 0);
        trigger.size = new Vector3(3f, 1.2f, 3f);

        basePlate.AddComponent<GoalZone>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        GameObject.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildNavigationFeatures(Transform parent, GameObject cubePrefab, System.Random rng)
    {
        // Narrow tunnels (2m wide, 5m long)
        for (int i = 0; i < 6; i++)
        {
            var tunnel = new GameObject($"Tunnel_{i}");
            tunnel.transform.SetParent(parent);
            float length = 5f;
            float width = 2f;
            float height = 2.2f;

            var pos = RandomXZ(rng, ArenaSize * 0.4f);
            tunnel.transform.position = new Vector3(pos.x, height * 0.5f, pos.y);
            tunnel.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            // floor
            CreateScaledChild(cubePrefab, tunnel.transform, new Vector3(length, 0.2f, width), new Vector3(0, -height * 0.5f, 0));
            // roof
            CreateScaledChild(cubePrefab, tunnel.transform, new Vector3(length, 0.2f, width), new Vector3(0, height * 0.5f, 0));
            // sides
            CreateScaledChild(cubePrefab, tunnel.transform, new Vector3(length, height, 0.2f), new Vector3(0, 0, -width * 0.5f));
            CreateScaledChild(cubePrefab, tunnel.transform, new Vector3(length, height, 0.2f), new Vector3(0, 0, width * 0.5f));
            MarkStaticForOcclusion(tunnel);
        }

        // Bridges at multiple heights
        for (int i = 0; i < 5; i++)
        {
            var bridge = new GameObject($"Bridge_{i}");
            bridge.transform.SetParent(parent);
            float length = 12f + (float)rng.NextDouble() * 8f;
            float height = 3f + (float)rng.NextDouble() * 10f;
            float width = 2f;

            var pos = RandomXZ(rng, ArenaSize * 0.45f);
            bridge.transform.position = new Vector3(pos.x, height, pos.y);
            bridge.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            CreateScaledChild(cubePrefab, bridge.transform, new Vector3(length, 0.3f, width), Vector3.zero);
            // simple pillars
            CreateScaledChild(cubePrefab, bridge.transform, new Vector3(0.3f, height, 0.3f), new Vector3(-length * 0.4f, -height * 0.5f, -width * 0.4f));
            CreateScaledChild(cubePrefab, bridge.transform, new Vector3(0.3f, height, 0.3f), new Vector3(length * 0.4f, -height * 0.5f, width * 0.4f));
            MarkStaticForOcclusion(bridge);
        }

        // Archways
        for (int i = 0; i < 6; i++)
        {
            var arch = new GameObject($"Arch_{i}");
            arch.transform.SetParent(parent);
            float width = 3f + (float)rng.NextDouble() * 4f;
            float height = 3f + (float)rng.NextDouble() * 6f;
            float thickness = 0.4f;

            var pos = RandomXZ(rng, ArenaSize * 0.45f);
            arch.transform.position = new Vector3(pos.x, height * 0.5f, pos.y);
            arch.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);

            // pillars
            CreateScaledChild(cubePrefab, arch.transform, new Vector3(thickness, height, thickness), new Vector3(-width * 0.5f, 0, 0));
            CreateScaledChild(cubePrefab, arch.transform, new Vector3(thickness, height, thickness), new Vector3(width * 0.5f, 0, 0));
            // top
            CreateScaledChild(cubePrefab, arch.transform, new Vector3(width + thickness, thickness, thickness), new Vector3(0, height * 0.5f, 0));
            MarkStaticForOcclusion(arch);
        }

        // Elevated platforms with ramps
        for (int i = 0; i < 5; i++)
        {
            var platform = new GameObject($"ElevatedPlatform_{i}");
            platform.transform.SetParent(parent);
            float height = 4f + (float)rng.NextDouble() * 8f;
            float size = 5f + (float)rng.NextDouble() * 5f;
            var pos = RandomXZ(rng, ArenaSize * 0.45f);
            platform.transform.position = new Vector3(pos.x, height, pos.y);

            CreateScaledChild(cubePrefab, platform.transform, new Vector3(size, 0.5f, size), Vector3.zero);

            // ramp
            var ramp = CreateScaledChild(cubePrefab, platform.transform, new Vector3(2f, 0.3f, height * 1.4f), new Vector3(size * 0.4f, -height * 0.5f, 0));
            ramp.transform.rotation = Quaternion.Euler(20f, (float)rng.NextDouble() * 360f, 0);
            MarkStaticForOcclusion(platform);
        }

        // Small caves/enclosed spaces
        for (int i = 0; i < 4; i++)
        {
            var cave = new GameObject($"Cave_{i}");
            cave.transform.SetParent(parent);
            float width = 6f, depth = 6f, height = 3f;
            var pos = RandomXZ(rng, ArenaSize * 0.45f);
            cave.transform.position = new Vector3(pos.x, height * 0.5f, pos.y);

            // floor, roof, 4 walls
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(width, 0.3f, depth), new Vector3(0, -height * 0.5f, 0));
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(width, 0.3f, depth), new Vector3(0, height * 0.5f, 0));
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(width, height, 0.3f), new Vector3(0, 0, -depth * 0.5f));
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(width, height, 0.3f), new Vector3(0, 0, depth * 0.5f));
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(0.3f, height, depth), new Vector3(-width * 0.5f, 0, 0));
            CreateScaledChild(cubePrefab, cave.transform, new Vector3(0.3f, height, depth), new Vector3(width * 0.5f, 0, 0));
            MarkStaticForOcclusion(cave);
        }
    }

    private static Transform CreateWaypoint(Transform parent, Vector3 localPos)
    {
        var wp = new GameObject("WP").transform;
        wp.SetParent(parent);
        wp.localPosition = localPos;
        return wp;
    }

    private static GameObject CreateScaledChild(GameObject prefab, Transform parent, Vector3 scale, Vector3 localPos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent);
        go.transform.localScale = scale;
        go.transform.localPosition = localPos;
        return go;
    }

    private static void BuildCheckpoints(Transform parent, System.Random rng)
    {
        int ringCount = 12;
        float radius = ArenaSize * 0.35f;
        for (int i = 0; i < ringCount; i++)
        {
            float ang = i * Mathf.PI * 2f / ringCount;
            var cp = new GameObject($"Checkpoint_{i}");
            cp.transform.SetParent(parent);
            cp.transform.position = new Vector3(Mathf.Cos(ang) * radius, 1f, Mathf.Sin(ang) * radius);

            var bc = cp.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(4f, 3f, 4f);

            var mr = cp.AddComponent<MeshRenderer>();
            var mf = cp.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateInvisibleCubeMesh();
            mr.enabled = false; // invisible
        }
    }

    private static Mesh CreateInvisibleCubeMesh()
    {
        // Use Unity built-in cube if present; otherwise create a tiny mesh
        return Resources.GetBuiltinResource<Mesh>("Cube.fbx");
    }

    private static void CreateSpawnPoints(Transform parent, int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            var sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(parent);
            float ang = i * Mathf.PI * 2f / count;
            sp.transform.position = new Vector3(Mathf.Cos(ang) * radius, 1f, Mathf.Sin(ang) * radius);
        }
    }

    private static Vector2 RandomXZ(System.Random rng, float halfRange)
    {
        float x = (float)(rng.NextDouble() * 2 - 1) * halfRange;
        float z = (float)(rng.NextDouble() * 2 - 1) * halfRange;
        return new Vector2(x, z);
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(mr.sharedMaterial);
            mat.color = color;
            mat.enableInstancing = true;
            mr.sharedMaterial = mat;
        }
    }

    private static void ApplyMaterial(GameObject go, Material mat)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = mat;
        }
    }

    private static Shader GetSupportedLitShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("HDRP/Lit");
        if (s == null) s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        return s;
    }

    private static Color RandomBrightColor(System.Random rng)
    {
        // HSV: high saturation and value
        float h = (float)rng.NextDouble();
        float s = 0.8f + 0.2f * (float)rng.NextDouble();
        float v = 0.8f + 0.2f * (float)rng.NextDouble();
        return Color.HSVToRGB(h, s, v);
    }

    private static Material CreateMaterial(string name, Shader shader, Color color)
    {
        if (shader == null) shader = GetSupportedLitShader();
        if (shader == null)
        {
            Debug.LogWarning($"No supported lit shader found. Falling back to Unlit/Color for {name}.");
            shader = Shader.Find("Unlit/Color");
        }

        var mat = new Material(shader) { name = name, color = color };
        var path = $"Assets/Materials/{name}.mat";
        EnsureFolder("Assets/Materials");
        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material CreateEmissiveMaterial(string name, Color color, float intensity)
    {
        var shader = GetSupportedLitShader();
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        var mat = new Material(shader) { name = name, color = color };

        // Try to set emission if the shader supports it
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", color * intensity);
            mat.EnableKeyword("_EMISSION");
        }

        var path = $"Assets/Materials/{name}.mat";
        EnsureFolder("Assets/Materials");
        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static void MarkStaticForOcclusion(GameObject go)
    {
        var flags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;
        GameObjectUtility.SetStaticEditorFlags(go, flags);
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || parent == "Assets")
            {
                AssetDatabase.CreateFolder("Assets", name);
            }
            else
            {
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
