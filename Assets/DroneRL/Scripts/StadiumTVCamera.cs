using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Professional Stadium TV Camera - mimics real broadcast camera behavior
/// Fixed position, but smoothly tracks the action like a real stadium camera operator
/// </summary>
public class StadiumTVCamera : MonoBehaviour
{
    [Header("Controller Mode")]
    [Tooltip("If true, this script takes exclusive control and removes other camera controllers on Start.")]
    public bool exclusiveControl = false;

    [Tooltip("If true and other camera controllers are detected (e.g., MultiDroneCameraController, DroneFollowCamera), this script disables itself to avoid conflicts.")]
    public bool disableIfOtherControllersDetected = true;
    [Header("Stadium Camera Position")]
    [Tooltip("Fixed height above the field")]
    public float stadiumHeight = 40f;
    
    [Tooltip("Fixed distance from field center")]
    public float stadiumDistance = 67f;
    
    [Tooltip("Side offset for better viewing angle")]
    public float sideOffset = 0f;
    
    [Header("Camera Behavior")]
    [Tooltip("How smoothly the camera pans to follow action")]
    [Range(0.5f, 10f)] public float panSpeed = 3f;
    
    [Tooltip("How smoothly the camera tilts up/down")]
    [Range(0.5f, 10f)] public float tiltSpeed = 2.5f;
    
    [Tooltip("How smoothly the camera zooms in/out")]
    [Range(0.5f, 10f)] public float zoomSpeed = 2f;
    
    [Header("Tracking Behavior")]
    [Tooltip("Focus on the most active drone")]
    public bool followMostActiveDrone = true;
    
    [Tooltip("Show overview when drones are spread out")]
    public bool autoOverview = true;
    
    [Tooltip("Maximum spread before switching to overview")]
    public float overviewThreshold = 30f;
    
    [Header("Field of View")]
    [Tooltip("Minimum FOV (zoomed in)")]
    [Range(20f, 60f)] public float minFOV = 35f;
    
    [Tooltip("Maximum FOV (zoomed out)")]
    [Range(40f, 120f)] public float maxFOV = 70f;
    
    [Header("Auto-Detection")]
    public bool autoFindDrones = true;
    public bool autoFindGoals = true;
    
    private Camera cam;
    private Vector3 fixedPosition;
    private Transform[] drones;
    private Transform[] goals;
    private Vector3 currentLookTarget;
    private float currentTargetFOV;
    private float lastDroneUpdate;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();

        // If other controllers are present and we should not fight them, disable self.
        if (disableIfOtherControllersDetected &&
            (GetComponent<MultiDroneCameraController>() != null || GetComponent<DroneFollowCamera>() != null))
        {
            Debug.Log("📺 StadiumTVCamera: Other camera controllers detected; disabling StadiumTVCamera to avoid conflicts.");
            enabled = false;
            return;
        }

        // Only remove other controllers when explicitly requested
        if (exclusiveControl)
        {
            // Destroy any other camera controllers immediately
            DestroyOtherCameraControllers();
        }
        
        SetupFixedPosition();
        FindDronesAndGoals();
        
        currentTargetFOV = (minFOV + maxFOV) / 2f;
        
        Debug.Log($"📺 Stadium TV Camera initialized at {fixedPosition}");
    }
    
    void DestroyOtherCameraControllers()
    {
        var components = GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            if (comp != this && (comp.GetType().Name.Contains("Camera") || 
                                comp.GetType().Name.Contains("HUD") ||
                                comp.GetType().Name.Contains("Lock")))
            {
                if (comp.GetType().Name != "Camera") // Don't destroy the Camera component itself
                {
                    Debug.Log($"🗑️ Removing {comp.GetType().Name}");
                    DestroyImmediate(comp);
                }
            }
        }
    }
    
    void SetupFixedPosition()
    {
        // Calculate fixed stadium camera position
        Vector3 fieldCenter = Vector3.zero;
        
        // Try to find the arena center
        var arena = GameObject.Find("DroneTrainingArena");
        if (arena != null)
        {
            fieldCenter = arena.transform.position;
        }
        
        // Position camera like a real stadium camera
        fixedPosition = fieldCenter + new Vector3(sideOffset, stadiumHeight, -stadiumDistance);
        
        // LOCK the position - this never changes
        transform.position = fixedPosition;
        
        Debug.Log($"📺 Stadium camera positioned at {fixedPosition}");
    }
    
    void FindDronesAndGoals()
    {
        if (autoFindDrones)
        {
            var droneAgents = FindObjectsOfType<DroneAgent>();
            drones = droneAgents.Select(d => d.transform).ToArray();
            Debug.Log($"📺 Found {drones.Length} drones to track");
        }
        
        if (autoFindGoals)
        {
            var goalObjects = GameObject.FindGameObjectsWithTag("Goal");
            if (goalObjects.Length == 0)
            {
                // Fallback: find objects with "goal" in name
                goalObjects = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.ToLower().Contains("goal"))
                    .ToArray();
            }
            goals = goalObjects.Select(g => g.transform).ToArray();
            Debug.Log($"📺 Found {goals.Length} goals");
        }
    }
    
    void Update()
    {
        // ALWAYS maintain fixed position (camera operator never moves the camera base)
        transform.position = fixedPosition;
        
        // Update drone list periodically
        if (Time.time - lastDroneUpdate > 2f)
        {
            FindDronesAndGoals();
            lastDroneUpdate = Time.time;
        }
        
        // Calculate where the camera should look
        Vector3 targetLookAt = CalculateOptimalLookTarget();
        
        // Smooth camera rotation (like a camera operator)
        RotateCameraToTarget(targetLookAt);
        
        // Adjust zoom based on action spread
        AdjustZoomForAction();
    }
    
    Vector3 CalculateOptimalLookTarget()
    {
        if (drones == null || drones.Length == 0)
        {
            return Vector3.zero; // Look at field center
        }
        
        // Filter out null/inactive drones
        var activeDrones = drones.Where(d => d != null && d.gameObject.activeInHierarchy).ToArray();
        if (activeDrones.Length == 0) return Vector3.zero;
        
        if (followMostActiveDrone && activeDrones.Length > 0)
        {
            // Find the most active drone (fastest moving or closest to goal)
            Transform mostActiveDrone = FindMostActiveDrone(activeDrones);
            if (mostActiveDrone != null)
            {
                return mostActiveDrone.position;
            }
        }
        
        // If auto overview is enabled, check if drones are spread out
        if (autoOverview && activeDrones.Length > 1)
        {
            float spread = CalculateDroneSpread(activeDrones);
            if (spread > overviewThreshold)
            {
                // Show overview of all drones
                return CalculateCenterOfAction(activeDrones);
            }
        }
        
        // Default: center of active drones
        return CalculateCenterOfAction(activeDrones);
    }
    
    Transform FindMostActiveDrone(Transform[] activeDrones)
    {
        Transform mostActive = null;
        float highestScore = 0f;
        
        foreach (var drone in activeDrones)
        {
            if (drone == null) continue;
            
            float score = 0f;
            
            // Speed factor
            var rb = drone.GetComponent<Rigidbody>();
            if (rb != null)
            {
                score += rb.velocity.magnitude * 2f;
            }
            
            // Proximity to goals factor
            if (goals != null && goals.Length > 0)
            {
                float minDistToGoal = goals.Where(g => g != null)
                                           .Min(g => Vector3.Distance(drone.position, g.position));
                score += (100f - minDistToGoal) * 0.1f; // Closer to goal = higher score
            }
            
            // Height factor (more interesting if flying higher)
            score += drone.position.y * 0.5f;
            
            if (score > highestScore)
            {
                highestScore = score;
                mostActive = drone;
            }
        }
        
        return mostActive;
    }
    
    float CalculateDroneSpread(Transform[] activeDrones)
    {
        if (activeDrones.Length < 2) return 0f;
        
        Vector3 center = CalculateCenterOfAction(activeDrones);
        float maxDistance = 0f;
        
        foreach (var drone in activeDrones)
        {
            if (drone != null)
            {
                float distance = Vector3.Distance(drone.position, center);
                if (distance > maxDistance) maxDistance = distance;
            }
        }
        
        return maxDistance;
    }
    
    Vector3 CalculateCenterOfAction(Transform[] activeDrones)
    {
        if (activeDrones.Length == 0) return Vector3.zero;
        
        Vector3 center = Vector3.zero;
        int count = 0;
        
        foreach (var drone in activeDrones)
        {
            if (drone != null)
            {
                center += drone.position;
                count++;
            }
        }
        
        return count > 0 ? center / count : Vector3.zero;
    }
    
    void RotateCameraToTarget(Vector3 targetLookAt)
    {
        if (targetLookAt == Vector3.zero) return;
        
        // Calculate desired rotation
        Vector3 direction = (targetLookAt - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        
        // Smooth rotation like a human camera operator
        float panLerpRate = 1f - Mathf.Exp(-panSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, panLerpRate);
        
        currentLookTarget = Vector3.Lerp(currentLookTarget, targetLookAt, panLerpRate);
    }
    
    void AdjustZoomForAction()
    {
        if (drones == null || drones.Length == 0) return;
        
        var activeDrones = drones.Where(d => d != null && d.gameObject.activeInHierarchy).ToArray();
        if (activeDrones.Length == 0) return;
        
        // Calculate required FOV based on action spread
        float spread = CalculateDroneSpread(activeDrones);
        
        // Map spread to FOV
        float normalizedSpread = Mathf.Clamp01(spread / overviewThreshold);
        currentTargetFOV = Mathf.Lerp(minFOV, maxFOV, normalizedSpread);
        
        // Smooth zoom adjustment
        float zoomLerpRate = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, currentTargetFOV, zoomLerpRate);
    }
    
    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 140), "📺 Stadium TV Camera");
        GUI.Label(new Rect(20, 35, 280, 20), $"Position: {transform.position} (FIXED)");
        GUI.Label(new Rect(20, 55, 280, 20), $"Looking at: {currentLookTarget}");
        GUI.Label(new Rect(20, 75, 280, 20), $"FOV: {cam.fieldOfView:F1}° (zoom)");
        GUI.Label(new Rect(20, 95, 280, 20), $"Tracking: {(drones?.Length ?? 0)} drones");
        GUI.Label(new Rect(20, 115, 280, 20), $"Mode: {(followMostActiveDrone ? "Active Drone" : "Overview")}");
        GUI.Label(new Rect(20, 135, 280, 20), "📺 Professional Stadium Camera");
    }
    
    [ContextMenu("Recenter Camera")]
    public void RecenterCamera()
    {
        SetupFixedPosition();
        FindDronesAndGoals();
    }
    
    [ContextMenu("Switch to Overview Mode")]
    public void SwitchToOverview()
    {
        followMostActiveDrone = false;
        autoOverview = true;
    }
    
    [ContextMenu("Switch to Active Tracking")]
    public void SwitchToActiveTracking()
    {
        followMostActiveDrone = true;
    }
}
