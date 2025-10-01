using UnityEngine;

/// <summary>
/// Makes sure StadiumCamera runs after other camera scripts
/// So it can override their movements and work properly
/// </summary>
[System.Serializable]
public class CameraExecutionOrderFix : MonoBehaviour
{
    [Header("Execution Order Fix")]
    [Tooltip("This component should have the highest execution order")]
    public int executionOrder = 1000;
    
    [Header("Debug")]
    public bool logExecutionOrder = true;
    
    private StadiumCamera stadiumCamera;
    
    void Awake()
    {
        stadiumCamera = GetComponent<StadiumCamera>();
        
        if (logExecutionOrder)
        {
            Debug.Log($"🔄 CameraExecutionOrderFix: Ensuring StadiumCamera runs last (order: {executionOrder})");
        }
        
        // Force disable other camera scripts immediately in Awake
        // This runs before Start() of other components
        ForceDisableOtherCameraScripts();
    }
    
    void ForceDisableOtherCameraScripts()
    {
        var followCam = GetComponent<DroneFollowCamera>();
        if (followCam != null) 
        {
            followCam.enabled = false;
            Debug.Log("🚫 Disabled DroneFollowCamera in Awake");
        }
        
        var multiCam = GetComponent<MultiDroneCameraController>();
        if (multiCam != null) 
        {
            multiCam.enabled = false;
            Debug.Log("🚫 Disabled MultiDroneCameraController in Awake");
        }
        
        var simpleCam = GetComponent<SimpleCameraFix>();
        if (simpleCam != null) 
        {
            simpleCam.enabled = false;
            Debug.Log("🚫 Disabled SimpleCameraFix in Awake");
        }
        
        var hud = GetComponent<DroneHUD>();
        if (hud != null) 
        {
            hud.enabled = false;
            Debug.Log("🚫 Disabled DroneHUD in Awake");
        }
    }
    
    // This runs VERY late in the frame, after LateUpdate
    void OnPostRender()
    {
        if (stadiumCamera != null && stadiumCamera.enabled)
        {
            // Final position lock at the very end of the frame
            // This should override ANY movement that happened during the frame
            var fixedPos = GetFixedPosition();
            var fixedLookAt = GetFixedLookAt();
            
            if (fixedPos != Vector3.zero)
            {
                transform.position = fixedPos;
                transform.LookAt(fixedLookAt);
            }
        }
    }
    
    private Vector3 GetFixedPosition()
    {
        // Use reflection to get the private fixedPosition field from StadiumCamera
        var field = typeof(StadiumCamera).GetField("fixedPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (Vector3)field.GetValue(stadiumCamera);
        }
        return Vector3.zero;
    }
    
    private Vector3 GetFixedLookAt()
    {
        // Use reflection to get the private fixedLookAt field from StadiumCamera
        var field = typeof(StadiumCamera).GetField("fixedLookAt", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (Vector3)field.GetValue(stadiumCamera);
        }
        return Vector3.zero;
    }
}
