#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DroneCreator
{
    [MenuItem("Tools/Create Test Drone")]
    public static void CreateTestDrone()
    {
        GameObject drone = GameObject.Find("DroneAgentPlaceholder");

        // Find a drone prefab under Assets/Drone (first match)
        GameObject prefab = null;
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Drone" });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.name.ToLower().Contains("drone"))
            {
                prefab = go;
                break;
            }
            if (prefab == null && go != null) prefab = go; // fallback to first prefab found
        }
        if (prefab == null)
        {
            Debug.LogError("DroneCreator: No prefab found in Assets/Drone. Please add your simple drone prefab to that folder.");
            return;
        }

        if (drone == null)
        {
            // Create an empty root to hold the model and runtime components
            drone = new GameObject("DroneAgentPlaceholder");
            drone.transform.position = Vector3.up * 1.5f;

            GameObject inner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inner.transform.SetParent(drone.transform, false);
        }
        else
        {
            // Replace existing children with the simple drone prefab contents
            for (int i = drone.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(drone.transform.GetChild(i).gameObject);
            }
            GameObject inner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inner.transform.SetParent(drone.transform, false);
            drone.name = "DroneAgentPlaceholder";
        }

        // Disable trajectory tracking scripts (Level1) if present and unconfigured to avoid null references.
        var anyComponents = drone.GetComponentsInChildren<Component>(true);
        foreach (var c in anyComponents)
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t != null && t.Name == "WaypointProgressTracker")
            {
                var beh = c as Behaviour;
                if (beh != null) beh.enabled = false; // user can manually enable after assigning a waypoint asset
            }
        }

    // Remove any of our custom experimental control scripts if present to avoid conflicts (by type name to avoid hard deps).
    RemoveComponentsByName(drone, new string[]
    {
        "FlightModeManager",
        "QuadControllerCascaded",
        "QuadMotorModel",
        "SensorsSim",
        "WindAndGusts",
        "ManualPilot",
        "DroneController"
    });
    // DroneTuning is a ScriptableObject asset, not a component—do not call GetComponent on it.

        // Ensure physics, colliders, and movement + input components
        EnsureRuntimeComponents(drone);

        // Ensure a follow camera targets the drone
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<DroneFollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<DroneFollowCamera>();
            follow.target = drone.transform;
            follow.mode = DroneFollowCamera.Mode.Stadium;

            // HUD
            var hud = cam.GetComponent<DroneHUD>();
            if (hud == null) hud = cam.gameObject.AddComponent<DroneHUD>();
            hud.drone = drone.transform;
            hud.controller = drone.GetComponent<QuadController>();
            hud.autoFindGoals = true;
        }

        Selection.activeGameObject = drone;
        Debug.Log("Simple drone instantiated with realistic physics controller, manual input, stadium camera, and HUD. Use Tools/Create Test Drone to refresh.");
    }

        // Helpers

        private static void EnsureRuntimeComponents(GameObject drone)
        {
            // Rigidbody
            var rb = drone.GetComponent<Rigidbody>();
            if (rb == null) rb = drone.AddComponent<Rigidbody>();
            rb.mass = 1.2f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Collider
            if (!HasAnyColliderInChildren(drone))
            {
                TryAddBoxColliderFromRenderers(drone);
            }

            // Movement and Input
            if (drone.GetComponent<QuadController>() == null) drone.AddComponent<QuadController>();
            if (drone.GetComponent<ManualQuadInput>() == null) drone.AddComponent<ManualQuadInput>();
        }

        private static bool HasAnyColliderInChildren(GameObject go)
        {
            return go.GetComponentInChildren<Collider>(true) != null;
        }

        private static void TryAddBoxColliderFromRenderers(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center);

            Vector3 sizeWorld = b.size;
            Vector3 lossy = root.transform.lossyScale;
            lossy.x = Mathf.Approximately(lossy.x, 0f) ? 1f : lossy.x;
            lossy.y = Mathf.Approximately(lossy.y, 0f) ? 1f : lossy.y;
            lossy.z = Mathf.Approximately(lossy.z, 0f) ? 1f : lossy.z;
            box.size = new Vector3(sizeWorld.x / lossy.x, sizeWorld.y / lossy.y, sizeWorld.z / lossy.z);
        }


    // Helper utilities
    private static void RemoveComponentsByName(GameObject go, string[] typeNames)
    {
        if (go == null || typeNames == null || typeNames.Length == 0) return;
        var comps = go.GetComponentsInChildren<Component>(true);
        // Try multiple passes to delete dependents first
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t == null) continue;
                for (int i = 0; i < typeNames.Length; i++)
                {
                    if (t.Name == typeNames[i])
                    {
                        Object.DestroyImmediate(c, true);
                        break;
                    }
                }
            }
        }
    }

    private static bool HasComponentInChildrenByName(Transform root, string typeName)
    {
        if (root == null || string.IsNullOrEmpty(typeName)) return false;
        var comps = root.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t != null && t.Name == typeName) return true;
        }
        return false;
    }

    // (Procedural arm creation removed in favor of prefab usage.)
}
#endif
