using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Multi-drone camera controller that can switch between drones or show overview
/// </summary>
public class MultiDroneCameraController : MonoBehaviour
{
    [Header("Camera Control")]
    public int currentTargetIndex = 0;
    public bool overviewMode = false;
    public float overviewHeight = 50f;
    public float switchDelay = 2f; // Auto-switch every N seconds
    [Tooltip("Start in Follow mode regardless of inspector state.")]
    public bool startInFollowMode = true;
    [Tooltip("Automatically disable conflicting camera controllers attached to the same object (recommended).")]
    public bool disableConflictingControllers = true;
    [Tooltip("Disable auto-switching during recording (set switchDelay to a large value when true).")]
    public bool recordingMode = true;
    
    [Header("Auto-detected")]
    public DroneAgent[] drones;
    
    private Camera cam;
    private DroneFollowCamera followCamera;
    private float lastSwitchTime;
    private Vector3 overviewPosition;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        followCamera = GetComponent<DroneFollowCamera>();
        // Proactively disable conflicting controllers on this camera
        if (disableConflictingControllers)
        {
            DisableConflicts();
        }
        
        // Find all drones in scene
        RefreshDroneList();
        
        // Set initial overview position
        overviewPosition = transform.position;
        overviewPosition.y = overviewHeight;

        // Prefer follow mode at start when configured
        if (startInFollowMode)
        {
            overviewMode = false;
            SetFollowMode();
        }
        else if (overviewMode)
        {
            SetOverviewMode();
        }
        else
        {
            SetFollowMode();
        }

        // In recording mode, avoid auto switching
        if (recordingMode)
        {
            switchDelay = 9999f;
        }
    }
    
    void Update()
    {
        // Handle keyboard input
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SwitchToNextDrone();
        }
        
        if (Input.GetKeyDown(KeyCode.O))
        {
            ToggleOverviewMode();
        }
        
        // Auto-switch drones periodically
        if (!overviewMode && Time.time - lastSwitchTime > switchDelay && drones != null && drones.Length > 1)
        {
            SwitchToNextDrone();
        }
        
        // Refresh drone list periodically
        if (Time.frameCount % 120 == 0) // Every 2 seconds at 60fps
        {
            RefreshDroneList();
        }
    }

    private void DisableConflicts()
    {
        var conflicts = new MonoBehaviour[]
        {
            GetComponent<StadiumCamera>(),
            GetComponent<StadiumTVCamera>(),
            GetComponent<UltimateCameraLock>(),
            // Optional helpers that might interfere
            GetComponent<CameraMovementDiagnostic>(),
            GetComponent<CameraDebugger>(),
            GetComponent<CameraExecutionOrderFix>(),
            GetComponent<SimpleCameraFix>()
        };

        foreach (var c in conflicts)
        {
            if (c != null && c.enabled)
            {
                c.enabled = false;
                Debug.Log($"Camera: Disabled conflicting controller {c.GetType().Name}");
            }
        }
    }
    
    void RefreshDroneList()
    {
        // Look for both DroneAgent and any GameObjects with "Drone" in the name
        var allDroneAgents = FindObjectsOfType<DroneAgent>();
        var allDroneObjects = new List<DroneAgent>();
        
        // Add all found drone agents
        allDroneObjects.AddRange(allDroneAgents);
        
        // Also look for GameObjects with "Drone" in name that might not have DroneAgent yet
        var allGameObjects = FindObjectsOfType<GameObject>();
        foreach (var go in allGameObjects)
        {
            if (go.name.ToLower().Contains("drone") && go.GetComponent<DroneAgent>() != null)
            {
                var agent = go.GetComponent<DroneAgent>();
                if (!allDroneObjects.Contains(agent))
                {
                    allDroneObjects.Add(agent);
                }
            }
        }
        
        drones = allDroneObjects.ToArray();
        
        Debug.Log($"Camera found {drones.Length} drones: {string.Join(", ", System.Array.ConvertAll(drones, d => d?.name ?? "null"))}");
        
        // Ensure current index is valid
        if (drones == null || drones.Length == 0)
        {
            currentTargetIndex = 0;
            return;
        }
        
        if (currentTargetIndex >= drones.Length)
        {
            currentTargetIndex = 0;
        }
    }
    
    public void SwitchToNextDrone()
    {
        if (drones == null || drones.Length == 0)
        {
            RefreshDroneList();
            return;
        }
        
        currentTargetIndex = (currentTargetIndex + 1) % drones.Length;
        lastSwitchTime = Time.time;
        
        if (!overviewMode)
        {
            SetFollowMode();
        }
        
        Debug.Log($"Camera following Drone {currentTargetIndex + 1}/{drones.Length}");
    }
    
    public void ToggleOverviewMode()
    {
        overviewMode = !overviewMode;
        
        if (overviewMode)
        {
            SetOverviewMode();
        }
        else
        {
            SetFollowMode();
        }
    }
    
    void SetOverviewMode()
    {
        overviewMode = true;
        
        // Disable follow camera
        if (followCamera != null)
        {
            followCamera.enabled = false;
        }
        
        // Calculate center of all drones
        Vector3 center = Vector3.zero;
        int validDroneCount = 0;
        
        if (drones != null && drones.Length > 0)
        {
            foreach (var drone in drones)
            {
                if (drone != null && drone.gameObject != null)
                {
                    center += drone.transform.position;
                    validDroneCount++;
                }
            }
            
            if (validDroneCount > 0)
            {
                center /= validDroneCount;
                overviewPosition = center + Vector3.up * overviewHeight;
            }
            else
            {
                // Fallback to arena center if no drones found
                var arena = GameObject.Find("DroneTrainingArena");
                if (arena != null)
                {
                    overviewPosition = arena.transform.position + Vector3.up * overviewHeight;
                }
                else
                {
                    overviewPosition = Vector3.up * overviewHeight;
                }
            }
        }
        else
        {
            // Fallback position
            overviewPosition = Vector3.up * overviewHeight;
        }
        
        // Set camera position and look down
        transform.position = overviewPosition;
        transform.rotation = Quaternion.LookRotation(Vector3.down);
        
        Debug.Log($"Camera: Overview Mode at {overviewPosition} watching {validDroneCount} drones");
    }
    
    void SetFollowMode()
    {
        overviewMode = false;
        
        if (drones == null || drones.Length == 0 || currentTargetIndex >= drones.Length)
        {
            RefreshDroneList();
            return;
        }
        
        var targetDrone = drones[currentTargetIndex];
        if (targetDrone == null)
        {
            SwitchToNextDrone();
            return;
        }
        
        // Enable and configure follow camera
        if (followCamera != null)
        {
            followCamera.enabled = true;
            followCamera.target = targetDrone.transform;
            followCamera.mode = DroneFollowCamera.Mode.Chase; // Less jittery than other modes
        }
        
        Debug.Log($"Camera: Following {targetDrone.name}");
    }
    
    void OnGUI()
    {
        if (drones == null) return;
        
        // Position camera controls below MODEL INFO panel (top-right area)
        int panelWidth = 200;
        int panelHeight = 80;
        int margin = 10;
        int rightX = Screen.width - panelWidth - margin;
        int cameraY = 120; // Below the MODEL INFO panel
        
        GUI.Box(new Rect(rightX, cameraY, panelWidth, panelHeight), "Camera Controls");
        GUI.Label(new Rect(rightX + 10, cameraY + 20, 180, 20), $"Mode: {(overviewMode ? "Overview" : "Follow")}");
        
        if (!overviewMode && drones.Length > 0)
        {
            GUI.Label(new Rect(rightX + 10, cameraY + 40, 180, 20), $"Target: Drone {currentTargetIndex + 1}/{drones.Length}");
        }
        
        GUI.Label(new Rect(rightX + 10, cameraY + 60, 180, 20), "Tab: Switch | O: Overview");
    }
}
