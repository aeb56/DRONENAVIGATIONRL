using UnityEngine;

/// <summary>
/// Sets up the main camera to follow drones properly when scene loads.
/// Runs automatically and fixes camera conflicts.
/// </summary>
public static class CameraRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("CameraBootstrap: No Camera found in scene.");
            return;
        }
        var go = cam.gameObject;

        // Disable known conflicting controllers if they exist
        DisableIfPresent<StadiumCamera>(go);
        DisableIfPresent<UltimateCameraLock>(go);
        DisableIfPresent<StadiumTVCamera>(go); // StadiumTVCamera will auto-disable if others present

        // Ensure follow + multi-drone controllers exist and are enabled
        EnsureEnabled<DroneFollowCamera>(go);
        var multi = EnsureEnabled<MultiDroneCameraController>(go);

        // Prefer starting in Follow mode and avoid auto-switching for recording
        if (multi != null)
        {
            multi.overviewMode = false;
            multi.startInFollowMode = true;
            multi.recordingMode = true;
            multi.disableConflictingControllers = true;
        }

        Debug.Log("CameraBootstrap: Main camera configured for MultiDrone follow.");
    }

    private static void DisableIfPresent<T>(GameObject go) where T : MonoBehaviour
    {
        var c = go.GetComponent<T>();
        if (c != null)
        {
            c.enabled = false;
            Debug.Log($"CameraBootstrap: Disabled {typeof(T).Name}");
        }
    }

    private static T EnsureEnabled<T>(GameObject go) where T : MonoBehaviour
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        c.enabled = true;
        return c;
    }
}
