using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

/// <summary>
/// Manual keyboard control for drone during heuristic mode
/// </summary>
public class DroneManualInput : MonoBehaviour
{
    [Header("Manual Control Settings")]
    public float inputSensitivity = 1.0f;
    public bool showControls = true;
    
    private DroneAgent droneAgent;
    
    void Start()
    {
        droneAgent = GetComponent<DroneAgent>();
        if (droneAgent == null)
        {
            Debug.LogError("DroneManualInput requires DroneAgent component!");
            enabled = false;
        }
    }
    
    void Update()
    {
        // Only show controls if this is the active manual control
        if (showControls && droneAgent != null)
        {
            var behaviorParams = droneAgent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams != null && behaviorParams.BehaviorType == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly)
            {
                ShowControlsGUI();
            }
        }
    }
    
    /// <summary>
    /// Called by ML-Agents when in Heuristic mode
    /// </summary>
    public void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        
        // WASD controls
        float pitch = 0f;  // W/S
        float roll = 0f;   // A/D
        float yaw = 0f;    // Q/E
        float throttle = 0f; // Space/C
        
        // Pitch (forward/backward)
        if (Input.GetKey(KeyCode.W)) pitch = 1f;
        if (Input.GetKey(KeyCode.S)) pitch = -1f;
        
        // Roll (left/right)
        if (Input.GetKey(KeyCode.A)) roll = -1f;
        if (Input.GetKey(KeyCode.D)) roll = 1f;
        
        // Yaw (rotation)
        if (Input.GetKey(KeyCode.Q)) yaw = -1f;
        if (Input.GetKey(KeyCode.E)) yaw = 1f;
        
        // Throttle (up/down)
        if (Input.GetKey(KeyCode.Space)) throttle = 1f;
        if (Input.GetKey(KeyCode.C)) throttle = -1f;
        
        // Apply sensitivity
        continuousActions[0] = pitch * inputSensitivity;
        continuousActions[1] = roll * inputSensitivity;
        continuousActions[2] = yaw * inputSensitivity;
        continuousActions[3] = throttle * inputSensitivity;
    }
    
    void OnGUI()
    {
        if (showControls && droneAgent != null)
        {
            var behaviorParams = droneAgent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams != null && behaviorParams.BehaviorType == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly)
            {
                ShowControlsGUI();
            }
        }
    }
    
    private void ShowControlsGUI()
    {
        GUI.Box(new Rect(10, 10, 200, 160), "Manual Drone Controls");
        GUI.Label(new Rect(20, 35, 180, 20), "W/S: Pitch (Forward/Back)");
        GUI.Label(new Rect(20, 55, 180, 20), "A/D: Roll (Left/Right)");
        GUI.Label(new Rect(20, 75, 180, 20), "Q/E: Yaw (Rotate)");
        GUI.Label(new Rect(20, 95, 180, 20), "Space/C: Throttle (Up/Down)");
        GUI.Label(new Rect(20, 115, 180, 20), "ESC: Exit Play Mode");
        GUI.Label(new Rect(20, 135, 180, 20), $"Mode: {(droneAgent != null ? "Manual" : "Unknown")}");
    }
}