using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEPRECATED: Use StadiumTVCamera instead for proper broadcast-style camera behavior
/// This class provided rigid locking but didn't behave like a real stadium camera
/// </summary>
[System.Obsolete("Use StadiumTVCamera for professional broadcast camera behavior")]
public class StadiumCamera : MonoBehaviour
{
    [Header("Stadium Camera Settings")]
    [Tooltip("Height above the training area")]
    public float cameraHeight = 40f;
    
    [Tooltip("Distance back from center of action")]
    public float cameraDistance = 50f;
    
    [Tooltip("Angle to look down at the drones (0-90 degrees)")]
    [Range(0f, 90f)] public float lookDownAngle = 30f;
    
    [Tooltip("Field of view for the camera")]
    [Range(30f, 120f)] public float fieldOfView = 75f;
    
    [Tooltip("Center point to look at (will auto-find if not set)")]
    public Transform lookAtTarget;
    
    [Header("Auto-Setup")]
    [Tooltip("Automatically find the best position based on training area")]
    public bool autoPosition = true;
    
    [Tooltip("Update position when drones spawn/despawn")]
    public bool dynamicPositioning = false;
    
    private Camera cam;
    private Vector3 fixedPosition;
    private Vector3 fixedLookAt;
    private bool isLocked = false;
    private float lastMoveWarningTime = 0f;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = gameObject.AddComponent<Camera>();
        }
        
        // Force stop any other movement immediately
        ForceStopAllMovement();
        
        SetupStadiumCamera();
        
        // Start the position lock immediately
        StartCoroutine(LockCameraPosition());
        
        Debug.Log($"Stadium Camera Started: Fixed position {fixedPosition}");
    }
    
    void ForceStopAllMovement()
    {
        // Remove or disable rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            DestroyImmediate(rb);
        }
        
        // Disable ALL other camera-related components
        var allComponents = GetComponents<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp != this && comp.GetType().Name.ToLower().Contains("camera"))
            {
                comp.enabled = false;
                Debug.Log($"Disabled interfering component: {comp.GetType().Name}");
            }
        }
    }
    
    void SetupStadiumCamera()
    {
        // Disable any other camera controllers
        DisableOtherCameraScripts();
        
        // Disable physics on camera (if any rigidbody exists)
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        // Configure camera settings
        cam.fieldOfView = fieldOfView;
        cam.clearFlags = CameraClearFlags.Skybox;
        
        if (autoPosition)
        {
            CalculateOptimalPosition();
        }
        else
        {
            SetManualPosition();
        }
        
        // Set the position and rotation - force it
        transform.position = fixedPosition;
        transform.LookAt(fixedLookAt);
        
        // Mark as locked
        isLocked = true;
        
        // NUCLEAR OPTION: Try to freeze the transform
        if (Application.isPlaying)
        {
            // Disable the transform component if possible (Unity doesn't allow this, but we'll try other methods)
            try
            {
                // Set transform constraints to prevent movement (reuse the rb variable from above)
                if (rb != null)
                {
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not freeze rigidbody: {e.Message}");
            }
        }
        
        Debug.Log($"🔒 Stadium Camera LOCKED at position {fixedPosition}, looking at {fixedLookAt}");
        
        // Lock transform if possible
        if (Application.isPlaying)
        {
            // Ensure transform doesn't change
            StartCoroutine(LockCameraPosition());
        }
        
        Debug.Log($"Stadium Camera Setup: Position {fixedPosition}, Looking at {fixedLookAt}");
    }
    
    void CalculateOptimalPosition()
    {
        // Find the training arena or drone spawn area
        Vector3 arenaCenter = Vector3.zero;
        float arenaSize = 50f; // Default arena size
        
        // Try to find the arena bounds
        var arena = GameObject.Find("DroneTrainingArena");
        if (arena != null)
        {
            arenaCenter = arena.transform.position;
            
            // Look for environment or terrain to estimate size
            var env = arena.GetComponent<DroneTrainingEnv>();
            if (env != null)
            {
                // Use environment hints if available
                arenaSize = 60f; // Typical training area size
            }
        }
        
        // Find all drones to get a better center point
        var drones = FindObjectsOfType<DroneAgent>();
        if (drones.Length > 0)
        {
            Vector3 droneCenter = Vector3.zero;
            Vector3 minBounds = Vector3.one * float.MaxValue;
            Vector3 maxBounds = Vector3.one * float.MinValue;
            
            foreach (var drone in drones)
            {
                if (drone != null)
                {
                    Vector3 pos = drone.transform.position;
                    droneCenter += pos;
                    
                    minBounds = Vector3.Min(minBounds, pos);
                    maxBounds = Vector3.Max(maxBounds, pos);
                }
            }
            
            droneCenter /= drones.Length;
            arenaCenter = droneCenter;
            
            // Calculate arena size from drone spread
            Vector3 spread = maxBounds - minBounds;
            arenaSize = Mathf.Max(spread.x, spread.z, 30f) + 20f; // Add padding
        }
        
        // Look for goals/waypoints to include in view
        GameObject[] goals = null;
        try 
        {
            goals = GameObject.FindGameObjectsWithTag("Goal");
        }
        catch 
        {
            goals = new GameObject[0]; // No Goal tag exists
        }
        
        var allTransforms = FindObjectsOfType<Transform>();
        var waypoints = new List<Transform>();
        
        foreach (var t in allTransforms)
        {
            if (t.name.ToLower().Contains("waypoint") || t.name.ToLower().Contains("goal"))
            {
                waypoints.Add(t);
            }
        }
        
        if (goals.Length > 0 || waypoints.Count > 0)
        {
            Vector3 targetCenter = Vector3.zero;
            int targetCount = 0;
            
            foreach (var goal in goals)
            {
                targetCenter += goal.transform.position;
                targetCount++;
            }
            
            foreach (var wp in waypoints)
            {
                if (wp.gameObject.activeInHierarchy)
                {
                    targetCenter += wp.position;
                    targetCount++;
                }
            }
            
            if (targetCount > 0)
            {
                targetCenter /= targetCount;
                // Blend arena center with target center
                arenaCenter = Vector3.Lerp(arenaCenter, targetCenter, 0.3f);
            }
        }
        
        // Calculate optimal camera position
        fixedLookAt = arenaCenter;
        
        // Position camera at an angle to see everything
        float radians = lookDownAngle * Mathf.Deg2Rad;
        float horizontalDistance = cameraHeight / Mathf.Tan(radians);
        
        // Place camera behind and above the action
        Vector3 cameraOffset = new Vector3(0, cameraHeight, -horizontalDistance);
        fixedPosition = arenaCenter + cameraOffset;
        
        // Adjust field of view to encompass the area
        float requiredFOV = Mathf.Atan(arenaSize / (2f * horizontalDistance)) * Mathf.Rad2Deg * 2f;
        cam.fieldOfView = Mathf.Clamp(requiredFOV + 10f, 40f, 100f); // Add padding and clamp
        
        Debug.Log($"Stadium Camera: Arena center {arenaCenter}, size {arenaSize}, FOV {cam.fieldOfView}");
    }
    
    void SetManualPosition()
    {
        // Use manual settings
        fixedLookAt = lookAtTarget != null ? lookAtTarget.position : Vector3.zero;
        
        float radians = lookDownAngle * Mathf.Deg2Rad;
        float horizontalDistance = cameraHeight / Mathf.Tan(radians);
        
        Vector3 cameraOffset = new Vector3(0, cameraHeight, -horizontalDistance);
        fixedPosition = fixedLookAt + cameraOffset;
    }
    
    void DisableOtherCameraScripts()
    {
        // Disable ALL other camera-related components - MORE AGGRESSIVE VERSION
        var followCam = GetComponent<DroneFollowCamera>();
        if (followCam != null) 
        {
            followCam.enabled = false;
            // Also destroy the component entirely to prevent re-enabling
            if (Application.isPlaying) DestroyImmediate(followCam);
        }
        
        var multiCam = GetComponent<MultiDroneCameraController>();
        if (multiCam != null) 
        {
            multiCam.enabled = false;
            if (Application.isPlaying) DestroyImmediate(multiCam);
        }
        
        var simpleCam = GetComponent<SimpleCameraFix>();
        if (simpleCam != null) 
        {
            simpleCam.enabled = false;
            if (Application.isPlaying) DestroyImmediate(simpleCam);
        }
        
        // FORCE DISABLE DroneHUD - this is often the culprit
        var hud = GetComponent<DroneHUD>();
        if (hud != null) 
        {
            hud.enabled = false;
            hud.drone = null; // Remove drone reference
            if (Application.isPlaying) DestroyImmediate(hud);
            Debug.Log("FORCE DISABLED AND DESTROYED DroneHUD component");
        }
        
        // Clean up diagnostic scripts (they're no longer needed)
        var diagnostic = GetComponent<CameraMovementDiagnostic>();
        if (diagnostic != null && Application.isPlaying) DestroyImmediate(diagnostic);
        
        var executionFix = GetComponent<CameraExecutionOrderFix>();
        if (executionFix != null && Application.isPlaying) DestroyImmediate(executionFix);
        
        var debugger = GetComponent<CameraDebugger>();
        if (debugger != null && Application.isPlaying) DestroyImmediate(debugger);
        
        Debug.Log($"🔒 StadiumCamera: Camera successfully locked in fixed position");
    }
    
    void Update()
    {
        // NUCLEAR OPTION: Completely lock the transform
        if (fixedPosition != Vector3.zero && fixedLookAt != Vector3.zero)
        {
            // Force position and rotation every single frame
            transform.position = fixedPosition;
            transform.rotation = Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized);
            
            // Also lock the transform component itself
            transform.hasChanged = false;
        }
        
        // Debug any unexpected movement with detailed logging
        float distanceFromFixed = Vector3.Distance(transform.position, fixedPosition);
        if (distanceFromFixed > 0.001f)
        {
            if (Time.time - lastMoveWarningTime > 0.5f) // Log every 0.5 seconds
            {
                Debug.LogError($"🚨 CAMERA MOVED! Distance: {distanceFromFixed:F6}m");
                Debug.LogError($"   Expected: {fixedPosition}");
                Debug.LogError($"   Actual: {transform.position}");
                Debug.LogError($"   Forcing back immediately!");
                
                // Force back immediately
                transform.position = fixedPosition;
                transform.rotation = Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized);
                
                lastMoveWarningTime = Time.time;
            }
        }
    }
    
    void OnGUI()
    {
        // Stadium Camera Status Display (NO MANUAL CONTROLS)
        GUI.Box(new Rect(10, 10, 250, 100), "Stadium Camera");
        GUI.Label(new Rect(20, 35, 230, 20), $"Height: {cameraHeight:F1}m");
        GUI.Label(new Rect(20, 55, 230, 20), $"Look Down Angle: {lookDownAngle:F1}°");
        GUI.Label(new Rect(20, 75, 230, 20), $"Field of View: {cam.fieldOfView:F1}°");
        GUI.Label(new Rect(20, 95, 230, 20), $"Position: {transform.position}");
        // Removed manual controls to prevent accidental movement
        
        // Add a lock indicator
        if (fixedPosition != Vector3.zero)
        {
            GUI.Label(new Rect(20, 115, 230, 20), "🔒 CAMERA LOCKED");
        }
    }
    
    // Public methods for runtime adjustment
    public void SetHeight(float height)
    {
        cameraHeight = height;
        SetupStadiumCamera();
    }
    
    public void SetLookDownAngle(float angle)
    {
        lookDownAngle = Mathf.Clamp(angle, 0f, 90f);
        SetupStadiumCamera();
    }
    
    public void RecenterCamera()
    {
        CalculateOptimalPosition();
        transform.position = fixedPosition;
        transform.LookAt(fixedLookAt);
    }
    
    System.Collections.IEnumerator LockCameraPosition()
    {
        while (true)
        {
            // Force the camera to stay exactly where we want it
            if (fixedPosition != Vector3.zero)
            {
                transform.position = fixedPosition;
                transform.LookAt(fixedLookAt);
            }
            yield return null; // Every frame
        }
    }
    
    void LateUpdate()
    {
        // FINAL OVERRIDE - this runs after all other Updates
        // This should override any other script that tries to move the camera
        if (fixedPosition != Vector3.zero && fixedLookAt != Vector3.zero)
        {
            // FORCE position and rotation
            transform.position = fixedPosition;
            transform.rotation = Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized);
            
            // Double-check and log if something moved us
            float distance = Vector3.Distance(transform.position, fixedPosition);
            if (distance > 0.0001f)
            {
                Debug.LogError($"🚨 LateUpdate: Camera was moved by {distance:F6}m! Forcing back!");
                transform.position = fixedPosition;
                transform.rotation = Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized);
            }
        }
    }
    
    void FixedUpdate()
    {
        // Physics-based lock - runs at fixed intervals
        // ABSOLUTE FINAL LOCK
        if (fixedPosition != Vector3.zero && fixedLookAt != Vector3.zero)
        {
            transform.position = fixedPosition;
            transform.rotation = Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized);
            
            // Ensure the rigidbody (if any) doesn't interfere
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.MovePosition(fixedPosition);
                rb.MoveRotation(Quaternion.LookRotation((fixedLookAt - fixedPosition).normalized));
            }
        }
    }
}
