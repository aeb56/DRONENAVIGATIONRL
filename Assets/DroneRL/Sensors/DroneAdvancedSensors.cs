using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;


/// Extra sensors for the drone like cameras, GPS, IMU etc.
/// Makes the simulation more realistic

public class DroneAdvancedSensors : MonoBehaviour
{
    [Header("Camera Sensors")]
    public Camera frontCamera;
    public Camera downwardCamera;
    [Range(64, 512)] public int cameraResolution = 128;
    public bool useRGBCamera = true;
    public bool useDepthCamera = true;
    [Tooltip("If false, sensor cameras won't render to the Game view (recommended for training/recording).")]
    public bool renderSensorCameras = false;
    [Tooltip("Hide auto-created sensor cameras from the Hierarchy to avoid clutter.")]
    public bool hideSensorCamerasInHierarchy = true;
    [Tooltip("Create ML-Agents visual sensors for cameras (RGB/Depth).")]
    public bool registerVisualSensors = false;
    [Range(1, 4)] public int visualObservationStacks = 1;
    
    [Header("LiDAR Simulation")]
    public bool useLiDAR = true;
    [Range(4, 64)] public int lidarRays = 16;
    public float lidarRange = 20f;
    public float lidarNoiseStdDev = 0.05f;
    
    [Header("IMU & Navigation")]
    public bool useIMU = true;
    public bool useGPS = true;
    public bool useMagnetometer = true;
    public bool useBarometer = true;
    
    [Header("Environmental")]
    public bool useWindSensor = true;
    public bool useProximityWarning = true;
    public float proximityRange = 2f;
    
    [Header("Detection & Layers")]
    [Tooltip("Physics layers considered by LiDAR and proximity raycasts (defaults to Everything).")]
    public LayerMask detectionMask = ~0; // Everything by default
    [Tooltip("Skip hits from this drone's own colliders when raycasting.")]
    public bool ignoreSelfColliders = true;
    [Tooltip("If true, other DroneAgent colliders are treated as obstacles and thus detected by sensors.")]
    public bool includeAgentsInSensors = true;
    [Tooltip("Small offset to move raycast origins outside the drone's own collider to avoid immediate self-hits.")]
    public float rayOriginUpOffset = 0.05f;
    
    [Header("Sensor Fusion")]
    public bool useExtendedKalmanFilter = false;
    public float sensorFusionRate = 50f; // Hz
    
    private DroneAgent agent;
    private Rigidbody rb;
    
    // Sensor data storage
    private Vector3 lastGPSPosition;
    private Vector3 estimatedVelocity;
    private Quaternion lastOrientation;
    private float[] lidarDistances;
    
    void Start()
    {
        InitializeComponents();
        SetupCameras();
        lidarDistances = new float[lidarRays];

        // Diagnostic: Warn if other agents are on the Ignore Raycast layer (Layer 2)
        if (includeAgentsInSensors)
        {
            var agents = FindObjectsOfType<DroneAgent>();
            int ignoreCount = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] != null && agents[i].gameObject.layer == 2) // 2 == Ignore Raycast
                {
                    ignoreCount++;
                }
            }
            if (ignoreCount > 0)
            {
                Debug.LogWarning($"DroneAdvancedSensors: {ignoreCount} DroneAgent(s) are on 'Ignore Raycast' layer and won't be detected by LiDAR/proximity. Move them to a detectable layer or adjust settings.");
            }
        }
    }
    
    void InitializeComponents()
    {
        if (agent == null) agent = GetComponent<DroneAgent>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        // Initialize arrays if not already done
        if (lidarDistances == null) lidarDistances = new float[lidarRays];
        
        // Initialize data
        lastGPSPosition = transform.position;
        estimatedVelocity = Vector3.zero;
        lastOrientation = transform.rotation;
    }
    
    void SetupCameras()
    {
        // Only create cameras when the corresponding sensor type is enabled.
        if (useRGBCamera)
        {
            if (frontCamera == null)
            {
                var camGO = new GameObject("FrontCamera");
                camGO.transform.SetParent(transform);
                camGO.transform.localPosition = new Vector3(0, 0, 0.3f);
                frontCamera = camGO.AddComponent<Camera>();
                frontCamera.fieldOfView = 90f;
            }
            ConfigureSensorCamera(frontCamera);
            TryRegisterVisualSensor(frontCamera, "FrontRGB", cameraResolution, cameraResolution, false, visualObservationStacks);
        }

        if (useDepthCamera)
        {
            if (downwardCamera == null)
            {
                var camGO = new GameObject("DownCamera");
                camGO.transform.SetParent(transform);
                camGO.transform.localPosition = new Vector3(0, -0.2f, 0);
                camGO.transform.localRotation = Quaternion.Euler(90, 0, 0);
                downwardCamera = camGO.AddComponent<Camera>();
                downwardCamera.fieldOfView = 120f;
            }
            ConfigureSensorCamera(downwardCamera);
            // Use grayscale to approximate depth channel for learning
            TryRegisterVisualSensor(downwardCamera, "DownDepth", cameraResolution, cameraResolution, true, visualObservationStacks);
        }
    }

    /// <summary>
    /// Configure sensor cameras so they don't interfere with the main view or clutter the Hierarchy.
    /// </summary>
    private void ConfigureSensorCamera(Camera cam)
    {
        if (cam == null) return;

        // Hide in Hierarchy to reduce clutter (optional)
        if (hideSensorCamerasInHierarchy)
        {
            cam.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        // By default, don't render sensor cameras to the main display
        cam.enabled = renderSensorCameras;

        // Ensure they never take precedence over Main Camera
        cam.depth = -100f;

        // If not rendering, avoid clearing and extra work
        if (!renderSensorCameras)
        {
            cam.cullingMask = 0; // Render nothing
            cam.clearFlags = CameraClearFlags.Nothing;
        }
    }

    private void TryRegisterVisualSensor(Camera cam, string sensorName, int width, int height, bool grayscale, int stacks)
    {
        if (!registerVisualSensors || cam == null) return;
        var existing = cam.GetComponent<CameraSensorComponent>();
        if (existing == null)
        {
            existing = cam.gameObject.AddComponent<CameraSensorComponent>();
        }
        existing.SensorName = sensorName;
        existing.Width = Mathf.Clamp(width, 8, 1024);
        existing.Height = Mathf.Clamp(height, 8, 1024);
        existing.Grayscale = grayscale;
        existing.ObservationStacks = Mathf.Clamp(stacks, 1, 4);
        existing.CompressionType = SensorCompressionType.PNG;
    }
    
    public void AddSensorObservations(VectorSensor sensor)
    {
        // Ensure components are initialized
        InitializeComponents();
        
        if (useIMU) AddIMUData(sensor);
        if (useGPS) AddGPSData(sensor);
        if (useMagnetometer) AddMagnetometerData(sensor);
        if (useBarometer) AddBarometerData(sensor);
        if (useLiDAR) AddLiDARData(sensor);
        if (useWindSensor) AddWindData(sensor);
        if (useProximityWarning) AddProximityData(sensor);
    }
    
    void AddIMUData(VectorSensor sensor)
    {
        if (rb == null)
        {
            // Fallback values if rigidbody not available
            sensor.AddObservation(Vector3.zero); // accel
            sensor.AddObservation(Vector3.zero); // gyro
            return;
        }
        
        // Accelerometer (with noise)
        Vector3 accel = (rb.velocity - estimatedVelocity) / Time.fixedDeltaTime;
        accel += Random.insideUnitSphere * 0.1f; // noise
        sensor.AddObservation(accel);
        
        // Gyroscope (with bias and noise)
        Vector3 gyro = rb.angularVelocity + Random.insideUnitSphere * 0.05f;
        sensor.AddObservation(gyro);
        
        estimatedVelocity = rb.velocity;
    }
    
    void AddGPSData(VectorSensor sensor)
    {
        Vector3 gpsPos = transform.position;
        // GPS noise (higher at altitude, near buildings)
        float accuracy = Mathf.Clamp(2f - transform.position.y * 0.1f, 0.5f, 5f);
        gpsPos += Random.insideUnitSphere * accuracy;
        sensor.AddObservation(gpsPos - lastGPSPosition); // relative movement
        lastGPSPosition = gpsPos;
    }
    
    void AddMagnetometerData(VectorSensor sensor)
    {
        // Magnetic heading with interference
        Vector3 magnetic = transform.forward;
        magnetic += Random.insideUnitSphere * 0.1f; // interference
        sensor.AddObservation(magnetic.normalized);
    }
    
    void AddBarometerData(VectorSensor sensor)
    {
        // Pressure altitude with lag and noise
        float altitude = transform.position.y + Random.Range(-0.5f, 0.5f);
        sensor.AddObservation(altitude / 50f); // normalized
    }
    
    void AddLiDARData(VectorSensor sensor)
    {
        if (lidarDistances == null)
        {
            lidarDistances = new float[lidarRays];
        }
        
        for (int i = 0; i < lidarRays; i++)
        {
            float angle = (360f / lidarRays) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            Vector3 origin = transform.position + transform.up * rayOriginUpOffset;

            // Use RaycastAll to skip self (and optionally skip agents) deterministically
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, lidarRange, detectionMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            float normalized = 1f; // default: nothing hit
            for (int h = 0; h < hits.Length; h++)
            {
                var hit = hits[h];
                if (ignoreSelfColliders && IsSelfCollider(hit.collider))
                    continue;
                if (!includeAgentsInSensors && IsAgentCollider(hit.collider))
                    continue;

                float distance = hit.distance + Random.Range(-lidarNoiseStdDev, lidarNoiseStdDev);
                normalized = Mathf.Clamp(distance / lidarRange, 0f, 1f);
                break; // take first valid hit
            }
            lidarDistances[i] = normalized;
            sensor.AddObservation(lidarDistances[i]);
        }
    }
    
    void AddWindData(VectorSensor sensor)
    {
        // Estimate wind from unexpected accelerations
        Vector3 expectedAccel = Physics.gravity;
        Vector3 actualAccel = (rb.velocity - estimatedVelocity) / Time.fixedDeltaTime;
        Vector3 windEstimate = actualAccel - expectedAccel;
        sensor.AddObservation(windEstimate);
    }
    
    void AddProximityData(VectorSensor sensor)
    {
        // Close-range obstacle warning (ultrasonic sensors)
        float[] proximities = new float[6]; // front, back, left, right, up, down
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
        
        for (int i = 0; i < 6; i++)
        {
            Vector3 dir = transform.TransformDirection(directions[i]);
            Vector3 origin = transform.position + transform.up * rayOriginUpOffset;

            float normalized = 1f;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, proximityRange, detectionMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int h = 0; h < hits.Length; h++)
            {
                var hit = hits[h];
                if (ignoreSelfColliders && IsSelfCollider(hit.collider))
                    continue;
                if (!includeAgentsInSensors && IsAgentCollider(hit.collider))
                    continue;

                normalized = hit.distance / Mathf.Max(0.0001f, proximityRange);
                break;
            }
            proximities[i] = Mathf.Clamp01(normalized);
            sensor.AddObservation(proximities[i]);
        }
    }

    private bool IsSelfCollider(Collider c)
    {
        if (c == null) return false;
        return c.transform.IsChildOf(transform);
    }

    private bool IsAgentCollider(Collider c)
    {
        if (c == null) return false;
        return c.GetComponentInParent<DroneAgent>() != null;
    }
}
