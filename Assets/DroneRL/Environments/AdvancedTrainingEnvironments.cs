using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Advanced training scenarios for comprehensive drone navigation
/// Includes weather, lighting, traffic, emergency situations
/// </summary>
public class AdvancedTrainingEnvironments : MonoBehaviour
{
    [Header("Environment Scenarios")]
    public ScenarioType currentScenario = ScenarioType.Basic;
    public bool randomizeScenarios = true;
    
    [Header("Weather Simulation")]
    public bool enableWeather = true;
    public WeatherCondition weatherCondition = WeatherCondition.Clear;
    public float windGustProbability = 0.1f;
    public float rainIntensity = 0f;
    public float fogDensity = 0f;
    
    [Header("Lighting Conditions")]
    public bool varyLighting = true;
    public float timeOfDay = 12f; // 0-24 hours
    public bool enableNightVision = false;
    public float visibility = 100f; // meters
    
    [Header("Traffic & Obstacles")]
    public bool enableAirTraffic = false;
    public int airTrafficDensity = 3;
    public bool enableBirds = true;
    public bool enableDynamicObstacles = true;
    
    [Header("Emergency Scenarios")]
    public bool enableFailureTraining = false;
    public float failureProbability = 0.01f;
    public List<FailureType> possibleFailures;
    
    [Header("Urban Environment")]
    public bool enableUrbanFeatures = false;
    public bool enablePowerLines = true;
    public bool enableBuildings = true;
    public bool enableRFInterference = false;
    
    public enum ScenarioType
    {
        Basic,
        Urban,
        Forest,
        Mountain,
        Desert,
        Coastal,
        Industrial,
        Emergency,
        Combat,
        Delivery
    }
    
    public enum WeatherCondition
    {
        Clear,
        Cloudy,
        Rainy,
        Stormy,
        Foggy,
        Windy,
        Snow
    }
    
    public enum FailureType
    {
        MotorFailure,
        SensorFailure,
        GPSLoss,
        CommsLoss,
        BatteryDrain,
        WeatherDamage
    }
    
    private Light mainLight;
    private WindField windField;
    private List<GameObject> trafficDrones = new List<GameObject>();
    private List<GameObject> dynamicObstacles = new List<GameObject>();
    
    void Start()
    {
        mainLight = FindObjectOfType<Light>();
        windField = FindObjectOfType<WindField>();
        SetupScenario();
    }
    
    void Update()
    {
        UpdateTimeOfDay();
        UpdateWeather();
        UpdateTraffic();
        
        if (enableFailureTraining && Random.value < failureProbability * Time.deltaTime)
        {
            TriggerRandomFailure();
        }
    }
    
    void SetupScenario()
    {
        switch (currentScenario)
        {
            case ScenarioType.Urban:
                SetupUrbanEnvironment();
                break;
            case ScenarioType.Forest:
                SetupForestEnvironment();
                break;
            case ScenarioType.Mountain:
                SetupMountainEnvironment();
                break;
            case ScenarioType.Emergency:
                SetupEmergencyScenario();
                break;
        }
    }
    
    void SetupUrbanEnvironment()
    {
        enableAirTraffic = true;
        enableRFInterference = true;
        airTrafficDensity = 5;
        SpawnBuildings();
        SpawnPowerLines();
    }
    
    void SetupForestEnvironment()
    {
        enableBirds = true;
        visibility = 50f; // Reduced visibility through trees
        SpawnTrees();
    }
    
    void SetupMountainEnvironment()
    {
        windGustProbability = 0.3f;
        SpawnMountainTerrain();
        // Altitude effects on performance
        var drones = FindObjectsOfType<DroneAgent>();
        foreach (var drone in drones)
        {
            var controller = drone.GetComponent<QuadController>();
            if (controller != null)
            {
                // Reduced thrust at altitude
                controller.thrustToWeight *= Mathf.Lerp(1f, 0.7f, transform.position.y / 100f);
            }
        }
    }
    
    void SetupEmergencyScenario()
    {
        enableFailureTraining = true;
        failureProbability = 0.05f; // Higher failure rate
        weatherCondition = WeatherCondition.Stormy;
        enableAirTraffic = true;
    }
    
    void UpdateTimeOfDay()
    {
        if (!varyLighting) return;
        
        timeOfDay += Time.deltaTime / 60f; // 1 minute = 1 hour
        if (timeOfDay >= 24f) timeOfDay = 0f;
        
        if (mainLight != null)
        {
            // Simulate sun angle and intensity
            float sunAngle = (timeOfDay - 6f) * 15f; // 6 AM = 0 degrees
            mainLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
            
            // Intensity based on time of day
            if (timeOfDay >= 6f && timeOfDay <= 18f) // Daytime
            {
                mainLight.intensity = Mathf.Lerp(0.1f, 1f, 1f - Mathf.Abs(timeOfDay - 12f) / 6f);
            }
            else // Nighttime
            {
                mainLight.intensity = 0.1f;
            }
            
            // Color temperature
            mainLight.color = Color.Lerp(Color.blue, Color.yellow, mainLight.intensity);
        }
    }
    
    void UpdateWeather()
    {
        if (!enableWeather) return;
        
        switch (weatherCondition)
        {
            case WeatherCondition.Rainy:
                UpdateRain();
                break;
            case WeatherCondition.Windy:
                UpdateWind();
                break;
            case WeatherCondition.Foggy:
                UpdateFog();
                break;
            case WeatherCondition.Stormy:
                UpdateStorm();
                break;
        }
    }
    
    void UpdateRain()
    {
        // Reduce visibility and sensor accuracy
        visibility *= (1f - rainIntensity);
        
        // Rain affects sensors
        var drones = FindObjectsOfType<DroneAgent>();
        foreach (var drone in drones)
        {
            var sensors = drone.GetComponent<DroneAdvancedSensors>();
            if (sensors != null)
            {
                // Increase sensor noise in rain
                drone.velocityNoise += rainIntensity * 0.1f;
                drone.positionNoise += rainIntensity * 0.2f;
            }
        }
    }
    
    void UpdateWind()
    {
        if (windField != null)
        {
            // Random gusts
            if (Random.value < windGustProbability * Time.deltaTime)
            {
                Vector3 gust = Random.insideUnitSphere * 15f;
                gust.y *= 0.3f; // Less vertical wind
                windField.SetWind(gust, 0.5f);
            }
        }
    }
    
    void UpdateFog()
    {
        visibility = Mathf.Lerp(visibility, 20f * (1f - fogDensity), Time.deltaTime);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fog = true;
        RenderSettings.fogDensity = fogDensity * 0.01f;
    }
    
    void UpdateStorm()
    {
        UpdateRain();
        UpdateWind();
        
        // Lightning effects (visual/electromagnetic interference)
        if (Random.value < 0.001f)
        {
            TriggerLightning();
        }
    }
    
    void UpdateTraffic()
    {
        if (!enableAirTraffic) return;
        
        // Spawn air traffic if needed
        while (trafficDrones.Count < airTrafficDensity)
        {
            SpawnTrafficDrone();
        }
        
        // Move traffic drones
        foreach (var traffic in trafficDrones)
        {
            if (traffic != null)
            {
                // Simple traffic movement
                traffic.transform.Translate(Vector3.forward * 5f * Time.deltaTime);
            }
        }
    }
    
    void TriggerRandomFailure()
    {
        if (possibleFailures.Count == 0) return;
        
        var failureType = possibleFailures[Random.Range(0, possibleFailures.Count)];
        var drones = FindObjectsOfType<DroneAgent>();
        if (drones.Length > 0)
        {
            var targetDrone = drones[Random.Range(0, drones.Length)];
            ApplyFailure(targetDrone, failureType);
        }
    }
    
    void ApplyFailure(DroneAgent drone, FailureType failure)
    {
        switch (failure)
        {
            case FailureType.MotorFailure:
                var controller = drone.GetComponent<QuadController>();
                if (controller != null)
                {
                    controller.thrustToWeight *= 0.7f; // 30% thrust loss
                }
                break;
                
            case FailureType.SensorFailure:
                drone.velocityNoise += 1f;
                drone.positionNoise += 2f;
                break;
                
            case FailureType.GPSLoss:
                // Disable position observations
                drone.positionNoise = 10f;
                break;
                
            case FailureType.BatteryDrain:
                var quad = drone.GetComponent<QuadController>();
                if (quad != null)
                {
                    quad.batteryLevel = Mathf.Max(0.1f, quad.batteryLevel - 0.3f);
                }
                break;
        }
        
        Debug.LogWarning($"Failure applied to {drone.name}: {failure}");
    }
    
    void TriggerLightning()
    {
        // Visual flash
        if (mainLight != null)
        {
            StartCoroutine(LightningFlash());
        }
        
        // Electromagnetic interference
        var drones = FindObjectsOfType<DroneAgent>();
        foreach (var drone in drones)
        {
            // Temporary sensor disruption
            drone.positionNoise += 5f;
            drone.gyroNoise += 1f;
        }
    }
    
    System.Collections.IEnumerator LightningFlash()
    {
        float originalIntensity = mainLight.intensity;
        mainLight.intensity = 3f;
        yield return new WaitForSeconds(0.1f);
        mainLight.intensity = originalIntensity;
    }
    
    void SpawnBuildings()
    {
        for (int i = 0; i < 20; i++)
        {
            var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building_" + i;
            building.transform.position = Random.insideUnitSphere * 50f;
            building.transform.position = new Vector3(
                building.transform.position.x,
                Random.Range(5f, 30f),
                building.transform.position.z
            );
            building.transform.localScale = new Vector3(
                Random.Range(5f, 15f),
                building.transform.position.y * 2f,
                Random.Range(5f, 15f)
            );
        }
    }
    
    void SpawnPowerLines()
    {
        // Implementation for power line obstacles
    }
    
    void SpawnTrees()
    {
        // Implementation for forest obstacles
    }
    
    void SpawnMountainTerrain()
    {
        // Implementation for mountainous terrain
    }
    
    void SpawnTrafficDrone()
    {
        var traffic = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        traffic.name = "TrafficDrone";
        traffic.transform.position = Random.insideUnitSphere * 100f + Vector3.up * 10f;
        traffic.AddComponent<Rigidbody>().useGravity = false;
        trafficDrones.Add(traffic);
    }
    
    public void SetScenario(ScenarioType scenario)
    {
        currentScenario = scenario;
        SetupScenario();
    }
    
    public void SetWeather(WeatherCondition weather)
    {
        weatherCondition = weather;
    }
}
