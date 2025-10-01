using UnityEngine;
using Unity.MLAgents.Sensors;

/// <summary>
/// Debug tool to check if drone observations are working properly
/// </summary>
public class ObservationDiagnoser : MonoBehaviour
{
    [ContextMenu("Diagnose Observation Issues")]
    public void DiagnoseObservations()
    {
        var agents = FindObjectsOfType<DroneAgent>();
        Debug.Log($"Diagnosing {agents.Length} DroneAgent(s)...");
        
        foreach (var agent in agents)
        {
            DiagnoseSingleAgent(agent);
        }
    }
    
    private void DiagnoseSingleAgent(DroneAgent agent)
    {
        Debug.Log($"\nDiagnosing agent: {agent.name}");
        
        // Check basic components
        var rb = agent.GetComponent<Rigidbody>();
        var adapter = agent.GetComponent<RLFlightAdapter>();
        
        Debug.Log($"Rigidbody: {(rb != null ? "OK" : "Missing")}");
        Debug.Log($"RLFlightAdapter: {(adapter != null ? "OK" : "Missing")}");
        Debug.Log($"Goal: {(agent.goal != null ? "OK " + agent.goal.name : "Not assigned")}");
        Debug.Log($"Environment: {(agent.env != null ? "OK " + agent.env.name : "Not assigned")}");
        
        // Check advanced sensors
        var advancedSensors = agent.GetComponent<DroneAdvancedSensors>();
        Debug.Log($"DroneAdvancedSensors: {(advancedSensors != null ? "OK" : "Missing")}");
        
        // Check if agent is active and enabled
        Debug.Log($"Agent Active: {agent.gameObject.activeInHierarchy}");
        Debug.Log($"Agent Enabled: {agent.enabled}");
        
        // Check ML-Agents components
        var behaviorParams = agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        var decisionRequester = agent.GetComponent<Unity.MLAgents.DecisionRequester>();
        
        Debug.Log($"BehaviorParameters: {(behaviorParams != null ? "OK" : "Missing")}");
        Debug.Log($"DecisionRequester: {(decisionRequester != null ? "OK" : "Missing")}");
        
        if (behaviorParams != null)
        {
            Debug.Log($"Behavior Type: {behaviorParams.BehaviorType}");
            Debug.Log($"Vector Observation Space: {behaviorParams.BrainParameters.VectorObservationSize}");
        }
        
        // Test observation collection manually
        TestObservationCollection(agent);
    }
    
    private void TestObservationCollection(DroneAgent agent)
    {
        Debug.Log($"\n🧪 Testing observation collection for {agent.name}...");
        
        try
        {
            // Create a test vector sensor
            var vectorSensorComponent = agent.GetComponent<Unity.MLAgents.Sensors.VectorSensorComponent>();
            if (vectorSensorComponent == null)
            {
                Debug.LogWarning($"No VectorSensorComponent found on {agent.name}");
                return;
            }
            
            var sensors = vectorSensorComponent.CreateSensors();
            if (sensors != null && sensors.Length > 0)
            {
                var sensor = sensors[0] as VectorSensor;
                if (sensor != null)
                {
                    Debug.Log($"Vector sensor found with observation size: {sensor.GetObservationSpec().Shape[0]}");
                    
                    // Try to manually collect observations
                    agent.CollectObservations(sensor);
                    Debug.Log($"Observation collection completed");
                }
            }
            else
            {
                Debug.LogWarning($"No sensors found on {agent.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error testing observations: {e.Message}");
        }
    }
    
    [ContextMenu("Fix Missing Components")]
    public void FixMissingComponents()
    {
        var agents = FindObjectsOfType<DroneAgent>();
        
        foreach (var agent in agents)
        {
            Debug.Log($"Fixing components for {agent.name}...");
            
            // Ensure Rigidbody
            if (agent.GetComponent<Rigidbody>() == null)
            {
                var rb = agent.gameObject.AddComponent<Rigidbody>();
                rb.mass = 1.2f;
                rb.drag = 0.05f;
                rb.angularDrag = 0.05f;
                Debug.Log($"➕ Added Rigidbody to {agent.name}");
            }
            
            // Ensure RLFlightAdapter
            if (agent.GetComponent<RLFlightAdapter>() == null)
            {
                agent.gameObject.AddComponent<RLFlightAdapter>();
                Debug.Log($"➕ Added RLFlightAdapter to {agent.name}");
            }
            
            // Ensure DroneAdvancedSensors
            if (agent.GetComponent<DroneAdvancedSensors>() == null)
            {
                agent.gameObject.AddComponent<DroneAdvancedSensors>();
                Debug.Log($"➕ Added DroneAdvancedSensors to {agent.name}");
            }
            
            // Create a goal if missing
            if (agent.goal == null)
            {
                CreateGoalForAgent(agent);
            }
            
            // Find or create environment
            if (agent.env == null)
            {
                agent.env = FindObjectOfType<DroneTrainingEnv>();
                if (agent.env != null)
                {
                    Debug.Log($"🔗 Connected {agent.name} to environment: {agent.env.name}");
                }
            }
        }
        
        Debug.Log("Component fixing complete!");
    }
    
    private void CreateGoalForAgent(DroneAgent agent)
    {
        // Create a simple goal sphere
        var goal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        goal.name = "Goal_" + agent.name;
        goal.transform.position = agent.transform.position + new Vector3(10, 0, 10);
        goal.transform.localScale = Vector3.one * 2;
        
        // Make it a trigger
        var collider = goal.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        
        // Visual
        var renderer = goal.GetComponent<Renderer>();
        renderer.material.color = Color.green;
        
        // Add goal zone
        var goalZone = goal.AddComponent<GoalZone>();
        goalZone.Assign(agent);
        
        // Assign to agent
        agent.goal = goal.transform;
        
        Debug.Log($"Created goal for {agent.name} at {goal.transform.position}");
    }
}