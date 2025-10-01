using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents.Policies;

/// <summary>
/// Multi-objective navigation system supporting various mission types
/// Path planning, waypoint following, dynamic re-routing, emergency procedures
/// </summary>
public class DroneNavigationSystem : MonoBehaviour
{
    [Header("Mission Types")]
    public NavigationMode currentMode = NavigationMode.WaypointFollowing;
    
    [Header("Path Planning")]
    public bool useAStar = true;
    public bool useDynamicReplanning = true;
    public float replanningInterval = 2f;
    public int maxPathNodes = 100;
    
    [Header("Waypoint Following")]
    public List<Transform> waypoints = new List<Transform>();
    public float waypointTolerance = 2f;
    public bool loopWaypoints = false;
    
    [Header("Dynamic Obstacles")]
    public bool avoidMovingObstacles = true;
    public float predictionHorizon = 3f;
    public LayerMask dynamicObstacleMask = -1;
    
    [Header("Emergency Behaviors")]
    public bool enableFailsafes = true;
    public Transform emergencyLandingZone;
    public float lowBatteryThreshold = 0.2f;
    public float maxWindSpeed = 10f;
    
    [Header("Formation Flying")]
    public bool isFormationLeader = false;
    public List<DroneNavigationSystem> followers = new List<DroneNavigationSystem>();
    public Vector3 formationOffset = Vector3.zero;
    
    public enum NavigationMode
    {
        WaypointFollowing,
        PathPlanning,
        FormationFlying,
        Search,
        Delivery,
        Surveillance,
        EmergencyReturn
    }
    
    private DroneAgent agent;
    private int currentWaypointIndex = 0;
    private Vector3[] plannedPath;
    private float lastReplanTime;
    private bool missionActive = true;
    
    void Start()
    {
        agent = GetComponent<DroneAgent>();
        if (waypoints.Count == 0) GenerateRandomWaypoints();
    }
    
    void Update()
    {
        if (!missionActive) return;
        
        CheckEmergencyConditions();
        
        switch (currentMode)
        {
            case NavigationMode.WaypointFollowing:
                UpdateWaypointFollowing();
                break;
            case NavigationMode.PathPlanning:
                UpdatePathPlanning();
                break;
            case NavigationMode.FormationFlying:
                UpdateFormationFlying();
                break;
            case NavigationMode.Search:
                UpdateSearchPattern();
                break;
            case NavigationMode.EmergencyReturn:
                UpdateEmergencyReturn();
                break;
        }
    }
    
    void UpdateWaypointFollowing()
    {
        if (waypoints.Count == 0) return;
        
        // Don't override RL agent's goal during training
        if (agent != null && agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>() != null)
        {
            var behaviorParams = agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams.BehaviorType != Unity.MLAgents.Policies.BehaviorType.HeuristicOnly)
            {
                // Agent is in training mode, don't interfere with its goal
                return;
            }
        }
        
        Transform currentWaypoint = waypoints[currentWaypointIndex];
        float distance = Vector3.Distance(transform.position, currentWaypoint.position);
        
        if (distance < waypointTolerance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                if (loopWaypoints)
                    currentWaypointIndex = 0;
                else
                    missionActive = false;
            }
        }
        
        // Set current waypoint as goal for agent (only in heuristic mode)
        if (currentWaypointIndex < waypoints.Count)
        {
            agent.goal = waypoints[currentWaypointIndex];
        }
    }
    
    void UpdatePathPlanning()
    {
        if (useDynamicReplanning && Time.time - lastReplanTime > replanningInterval)
        {
            ReplanPath();
            lastReplanTime = Time.time;
        }
    }
    
    void UpdateFormationFlying()
    {
        if (isFormationLeader)
        {
            // Leader follows waypoints, followers maintain formation
            UpdateWaypointFollowing();
        }
        else
        {
            // Follow formation leader
            var leader = FindFormationLeader();
            if (leader != null)
            {
                Vector3 targetPos = leader.transform.position + formationOffset;
                // In training mode, do not interfere with the RL agent's goal
                if (!IsTrainingControlled())
                {
                    // Create temporary goal for formation following
                    if (agent.goal != null)
                        agent.goal.position = targetPos;
                }
            }
        }
    }
    
    void UpdateSearchPattern()
    {
        // Implement lawn-mower or spiral search pattern
        // This is a simplified version
        if (waypoints.Count == 0)
            GenerateSearchPattern();
        UpdateWaypointFollowing();
    }
    
    void UpdateEmergencyReturn()
    {
        if (emergencyLandingZone != null)
        {
            if (!IsTrainingControlled()) agent.goal = emergencyLandingZone;
        }
        else
        {
            // Emergency land at current position
            Vector3 landingSpot = transform.position;
            landingSpot.y = 0.5f; // Land 0.5m above ground
            if (!IsTrainingControlled() && agent.goal != null)
                agent.goal.position = landingSpot;
        }
    }
    
    void CheckEmergencyConditions()
    {
        // Battery check
        var quadController = GetComponent<QuadController>();
        if (quadController != null && quadController.batteryLevel < lowBatteryThreshold)
        {
            TriggerEmergency("Low Battery");
        }
        
        // Wind speed check
        var windField = FindObjectOfType<WindField>();
        if (windField != null && windField.wind.magnitude > maxWindSpeed)
        {
            TriggerEmergency("High Wind");
        }
        
        // Collision avoidance
        if (DetectImmediateCollisionRisk())
        {
            TriggerEmergencyAvoidance();
        }
    }
    
    void TriggerEmergency(string reason)
    {
        Debug.LogWarning($"Emergency triggered: {reason}");
        currentMode = NavigationMode.EmergencyReturn;
    }
    
    bool DetectImmediateCollisionRisk()
    {
        // Fast collision detection for emergency avoidance, ignoring self
        var hits = Physics.OverlapSphere(transform.position, 1f, dynamicObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            var hitAgent = h.GetComponentInParent<DroneAgent>();
            if (hitAgent != null && hitAgent == agent) continue; // ignore self
            return true;
        }
        return false;
    }
    
    void TriggerEmergencyAvoidance()
    {
        // Quick evasive maneuver
        Vector3 avoidanceVector = Vector3.up * 2f; // Quick climb
        if (!IsTrainingControlled() && agent.goal != null)
            agent.goal.position = transform.position + avoidanceVector;
    }
    
    void ReplanPath()
    {
        // A* pathfinding implementation would go here
        // For now, simplified direct path
        if (agent.goal != null)
        {
            plannedPath = new Vector3[] { transform.position, agent.goal.position };
        }
    }
    
    void GenerateRandomWaypoints()
    {
        for (int i = 0; i < 5; i++)
        {
            var wp = new GameObject($"Waypoint_{i}").transform;
            wp.position = Random.insideUnitSphere * 20f + Vector3.up * 5f;
            waypoints.Add(wp);
        }
    }
    
    void GenerateSearchPattern()
    {
        // Generate lawn-mower search pattern
        float spacing = 10f;
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                var wp = new GameObject($"SearchWP_{x}_{z}").transform;
                wp.position = new Vector3(x * spacing, 5f, z * spacing);
                waypoints.Add(wp);
            }
        }
    }
    
    DroneNavigationSystem FindFormationLeader()
    {
        return FindObjectOfType<DroneNavigationSystem>(); // Simplified
    }
    
    public void SetMission(NavigationMode mode, List<Vector3> targets = null)
    {
        currentMode = mode;
        if (targets != null)
        {
            waypoints.Clear();
            foreach (var target in targets)
            {
                var wp = new GameObject("MissionWP").transform;
                wp.position = target;
                waypoints.Add(wp);
            }
        }
        currentWaypointIndex = 0;
        missionActive = true;
    }

    private bool IsTrainingControlled()
    {
        if (agent == null) return false;
        var bp = agent.GetComponent<BehaviorParameters>();
        if (bp == null) return false;
        return bp.BehaviorType != BehaviorType.HeuristicOnly;
    }
}
