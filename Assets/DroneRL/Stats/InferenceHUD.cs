using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents.Actuators;

namespace DroneRL.Stats
{
    public class InferenceHUD : MonoBehaviour
    {
        [Header("Path Display")]
        public bool showOptimizationPath = true;
        public int maxPathPoints = 100;
        public Color pathColor = Color.cyan;
        public Color decisionColor = Color.yellow;
        
        private DroneAgent agent;
        private DroneTrainingEnv env;
        private float startTime;
        private Vector3 initialPosition;
        private Vector3 initialGoalPosition;
        private float totalDistance;
        private float lastFrameDistance;
        private int frameCount;
        private float maxSpeed;
        private float maxTilt;
        private bool goalReached = false;
        private float goalReachedTime;
        private List<float> recentRewards = new List<float>();
        private List<float> recentSpeeds = new List<float>();
        
        // Track the path the drone takes
        private List<Vector3> pathPoints = new List<Vector3>();
        private List<float> pathRewards = new List<float>();
        private List<Vector4> pathActions = new List<Vector4>();
        private float lastPathUpdate = 0f;
        private float pathUpdateInterval = 0.1f;
        
        // What the AI is currently doing
        private Vector4 currentAction = Vector4.zero;
        private float currentReward = 0f;
        private string currentDecision = "";
        private float decisionConfidence = 0f;
        
        void Start()
        {
            FindComponents();
            Initialize();
        }
        
        void FindComponents()
        {
            // Find drone and environment in scene
            agent = FindObjectOfType<DroneAgent>();
            env = FindObjectOfType<DroneTrainingEnv>();
        }
        
        void Initialize()
        {
            startTime = Time.time;
            frameCount = 0;
            maxSpeed = 0f;
            maxTilt = 0f;
            goalReached = false;
            totalDistance = 0f;
            lastFrameDistance = 0f;
            
            // Reset path tracking
            pathPoints.Clear();
            pathRewards.Clear();
            pathActions.Clear();
            lastPathUpdate = 0f;
            
            if (agent != null)
            {
                initialPosition = agent.transform.position;
                if (agent.goal != null)
                {
                    initialGoalPosition = agent.goal.position;
                }
                
                // Listen for agent updates
                agent.OnStepInfoUpdated += OnAgentStepUpdated;
            }
        }
        
        void Update()
        {
            if (agent == null) return;
            
            frameCount++;
            UpdateMetrics();
            UpdatePathTracking();
        }
        
        void OnAgentStepUpdated(DroneAgent updatedAgent)
        {
            if (updatedAgent != agent) return;
            
            // Update current reward
            currentReward = agent.GetCumulativeReward();
            
            // Figure out what the AI is doing right now
            AnalyzeCurrentDecision();
        }
        
        void AnalyzeCurrentDecision()
        {
            if (agent.goal == null) return;
            
            Vector3 toGoal = agent.goal.position - agent.transform.position;
            float distanceToGoal = toGoal.magnitude;
            Vector3 velocity = agent.GetComponent<Rigidbody>().velocity;
            
            // Figure out what the drone is trying to do
            if (distanceToGoal < 3f)
            {
                currentDecision = "FINAL APPROACH - Being precise";
                decisionConfidence = Mathf.Clamp01(1f - distanceToGoal / 3f);
            }
            else if (velocity.magnitude > 8f)
            {
                currentDecision = "SPEED MODE - Going fast";
                decisionConfidence = Mathf.Clamp01(velocity.magnitude / 12f);
            }
            else if (Physics.OverlapSphere(agent.transform.position, 5f, agent.obstacleMask).Length > 0)
            {
                currentDecision = "AVOIDING OBSTACLES - Replanning path";
                decisionConfidence = 0.8f;
            }
            else if (Vector3.Dot(velocity.normalized, toGoal.normalized) > 0.8f)
            {
                currentDecision = "DIRECT PATH - Heading to goal";
                decisionConfidence = Vector3.Dot(velocity.normalized, toGoal.normalized);
            }
            else
            {
                currentDecision = "CORRECTING COURSE - Adjusting direction";
                decisionConfidence = 0.6f;
            }
        }
        
        void UpdatePathTracking()
        {
            if (!showOptimizationPath || agent == null) return;
            
            if (Time.time - lastPathUpdate > pathUpdateInterval)
            {
                // Record current position and decision
                pathPoints.Add(agent.transform.position);
                pathRewards.Add(agent.GetCumulativeReward());
                
                // Keep path manageable
                if (pathPoints.Count > maxPathPoints)
                {
                    pathPoints.RemoveAt(0);
                    pathRewards.RemoveAt(0);
                    if (pathActions.Count > 0) pathActions.RemoveAt(0);
                }
                
                lastPathUpdate = Time.time;
            }
        }
        
        void UpdateMetrics()
        {
            // Track distance traveled
            if (frameCount > 1)
            {
                float frameDistance = Vector3.Distance(agent.transform.position, 
                    agent.transform.position - agent.GetComponent<Rigidbody>().velocity * Time.deltaTime);
                totalDistance += frameDistance;
            }
            
            // Track speed
            var rb = agent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float currentSpeed = rb.velocity.magnitude;
                recentSpeeds.Add(currentSpeed);
                if (recentSpeeds.Count > 60) recentSpeeds.RemoveAt(0); // Keep last 60 frames (1 sec at 60fps)
                
                maxSpeed = Mathf.Max(maxSpeed, currentSpeed);
            }
            
            // Track tilt
            float currentTilt = Vector3.Angle(Vector3.up, agent.transform.up);
            maxTilt = Mathf.Max(maxTilt, currentTilt);
            
            // Track rewards
            if (agent.GetCumulativeReward() != 0)
            {
                recentRewards.Add(agent.GetCumulativeReward());
                if (recentRewards.Count > 300) recentRewards.RemoveAt(0); // Keep last 5 seconds worth
            }
            
            // Check if goal was reached
            if (!goalReached && agent.goal != null)
            {
                float distanceToGoal = Vector3.Distance(agent.transform.position, agent.goal.position);
                if (distanceToGoal < 2.5f) // Assuming goal radius is around 2.5
                {
                    goalReached = true;
                    goalReachedTime = Time.time - startTime;
                }
            }
        }
        
        void OnGUI()
        {
            if (agent == null) return;
            
            float elapsed = Time.time - startTime;
            string status = goalReached ? $"GOAL REACHED ({goalReachedTime:F1}s)" : "NAVIGATING TO GOAL";
            
            float distanceToGoal = agent.goal != null ? 
                Vector3.Distance(agent.transform.position, agent.goal.position) : 0f;
            
            float avgSpeed = recentSpeeds.Count > 0 ? recentSpeeds.Average() : 0f;
            float currentReward = agent.GetCumulativeReward();
            float avgReward = recentRewards.Count > 0 ? recentRewards.Average() : 0f;
            
            // Calculate responsive panel dimensions based on screen size
            int panelWidth = Mathf.Min(300, Screen.width / 4 - 20);
            int panelHeight = 120;
            int margin = 10;
            
            // Bottom row panels - ensure they fit within screen width
            int bottomY = Screen.height - panelHeight - margin;
            int totalBottomWidth = (panelWidth * 3) + (margin * 4);
            int startX = (Screen.width - totalBottomWidth) / 2; // Center the bottom panels
            
            // Main panel (bottom-left)
            GUI.Box(new Rect(startX, bottomY, panelWidth, panelHeight), "EVALUATION MODE");
            GUI.Label(new Rect(startX + 10, bottomY + 20, panelWidth - 20, 20), status);
            GUI.Label(new Rect(startX + 10, bottomY + 40, panelWidth - 20, 20), $"Time: {elapsed:F1}s");
            GUI.Label(new Rect(startX + 10, bottomY + 60, panelWidth - 20, 20), $"Distance: {distanceToGoal:F1}m");
            GUI.Label(new Rect(startX + 10, bottomY + 80, panelWidth - 20, 20), $"Reward: {currentReward:F2}");
            GUI.Label(new Rect(startX + 10, bottomY + 100, panelWidth - 20, 20), $"Total Dist: {totalDistance:F1}m");
            
            // Performance panel (bottom-center)
            int centerX = startX + panelWidth + margin;
            GUI.Box(new Rect(centerX, bottomY, panelWidth, panelHeight), "PERFORMANCE METRICS");
            GUI.Label(new Rect(centerX + 10, bottomY + 20, panelWidth - 20, 20), $"Speed: {avgSpeed:F1} m/s");
            GUI.Label(new Rect(centerX + 10, bottomY + 40, panelWidth - 20, 20), $"Max Speed: {maxSpeed:F1} m/s");
            GUI.Label(new Rect(centerX + 10, bottomY + 60, panelWidth - 20, 20), $"Max Tilt: {maxTilt:F1}°");
            GUI.Label(new Rect(centerX + 10, bottomY + 80, panelWidth - 20, 20), $"Avg Reward: {avgReward:F2}");
            
            // AI Decision panel (bottom-right)
            int rightX = centerX + panelWidth + margin;
            GUI.Box(new Rect(rightX, bottomY, panelWidth, panelHeight), "AI OPTIMIZATION");
            GUI.Label(new Rect(rightX + 10, bottomY + 20, panelWidth - 20, 20), $"Decision: {GetShortDecision()}");
            GUI.Label(new Rect(rightX + 10, bottomY + 40, panelWidth - 20, 20), $"Confidence: {decisionConfidence:P0}");
            GUI.Label(new Rect(rightX + 10, bottomY + 60, panelWidth - 20, 20), $"Strategy: {GetOptimizationStrategy()}");
            GUI.Label(new Rect(rightX + 10, bottomY + 80, panelWidth - 20, 20), $"Neural: {GetNeuralOutputSummary()}");
            GUI.Label(new Rect(rightX + 10, bottomY + 100, panelWidth - 20, 20), $"Path Points: {pathPoints.Count}");
            
            // Top row panels
            int topPanelHeight = 100;
            int topMargin = 10;
            
            // Model info panel (top-right)
            var behaviorParams = agent.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behaviorParams != null)
            {
                string modelName = behaviorParams.Model != null ? behaviorParams.Model.name : "No Model";
                string behaviorType = behaviorParams.BehaviorType.ToString();
                
                GUI.Box(new Rect(Screen.width - panelWidth - margin, topMargin, panelWidth, topPanelHeight), "MODEL INFO");
                GUI.Label(new Rect(Screen.width - panelWidth, topMargin + 20, panelWidth - 20, 20), $"MODEL: {modelName}");
                GUI.Label(new Rect(Screen.width - panelWidth, topMargin + 40, panelWidth - 20, 20), $"MODE: {behaviorType}");
                GUI.Label(new Rect(Screen.width - panelWidth, topMargin + 60, panelWidth - 20, 20), $"LEARNING: {(behaviorType == "InferenceOnly" ? "OFF" : "ON")}");
                GUI.Label(new Rect(Screen.width - panelWidth, topMargin + 80, panelWidth - 20, 20), $"BRAIN: DroneAgent");
            }
            
            // Path optimization explanation (top-left) - clean positioning
            int explanationWidth = Mathf.Min(400, Screen.width / 2 - margin);
            GUI.Box(new Rect(margin, topMargin, explanationWidth, topPanelHeight), "OPTIMIZATION PATH EXPLANATION");
            GUI.Label(new Rect(margin + 10, topMargin + 20, explanationWidth - 20, 20), "Deep RL optimizes multiple objectives:");
            GUI.Label(new Rect(margin + 10, topMargin + 40, explanationWidth - 20, 20), "• Distance • Energy • Collision avoidance");
            GUI.Label(new Rect(margin + 10, topMargin + 60, explanationWidth - 20, 20), $"• Current: {GetCurrentStrategy()}");
            GUI.Label(new Rect(margin + 10, topMargin + 80, explanationWidth - 20, 20), "• Path shows AI decision trajectory");
        }
        
        string GetOptimizationStrategy()
        {
            if (agent.goal == null) return "No Goal";
            
            float distanceToGoal = Vector3.Distance(agent.transform.position, agent.goal.position);
            Vector3 velocity = agent.GetComponent<Rigidbody>().velocity;
            
            if (distanceToGoal < 5f)
                return "Precision Landing";
            else if (velocity.magnitude > 6f)
                return "Speed Max";
            else
                return "Path Efficiency";
        }
        
        string GetShortDecision()
        {
            // Return a shortened version of currentDecision for small panels
            if (string.IsNullOrEmpty(currentDecision))
                return "Initializing";
            
            if (currentDecision.Contains("FINAL APPROACH"))
                return "Final Approach";
            else if (currentDecision.Contains("OBSTACLE AVOIDANCE"))
                return "Avoiding Obstacles";
            else if (currentDecision.Contains("SPEED OPTIMIZATION"))
                return "Speed Optimize";
            else if (currentDecision.Contains("DIRECT NAVIGATION"))
                return "Direct Nav";
            else if (currentDecision.Contains("COURSE CORRECTION"))
                return "Course Correct";
            else
                return "Navigating";
        }
        
        string GetNeuralOutputSummary()
        {
            var rb = agent.GetComponent<Rigidbody>();
            if (rb == null) return "No Data";
            
            Vector3 vel = rb.velocity;
            return $"V:{vel.magnitude:F1} T:{Vector3.Angle(transform.up, Vector3.up):F0}°";
        }
        
        string GetCurrentStrategy()
        {
            if (pathPoints.Count < 2) return "Initializing";
            
            Vector3 recent = pathPoints[pathPoints.Count - 1] - pathPoints[pathPoints.Count - 2];
            if (recent.magnitude > 0.5f)
                return "Aggressive navigation";
            else
                return "Careful approach";
        }
        
        // Visual path rendering in Scene view
        void OnDrawGizmos()
        {
            if (!showOptimizationPath || pathPoints.Count < 2) return;
            
            // Draw optimization path
            Gizmos.color = pathColor;
            for (int i = 1; i < pathPoints.Count; i++)
            {
                Gizmos.DrawLine(pathPoints[i-1], pathPoints[i]);
                
                // Color-code path by reward
                if (i < pathRewards.Count)
                {
                    float normalizedReward = Mathf.Clamp01(pathRewards[i] / 10f + 0.5f); // Assume rewards range -10 to 10
                    Gizmos.color = Color.Lerp(Color.red, Color.green, normalizedReward);
                    Gizmos.DrawSphere(pathPoints[i], 0.2f);
                }
            }
            
            // Draw current decision indicator
            if (agent != null)
            {
                Gizmos.color = decisionColor;
                Gizmos.DrawWireSphere(agent.transform.position, 1f);
                
                // Draw direction to goal
                if (agent.goal != null)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(agent.transform.position, agent.goal.position);
                }
            }
        }
        
        void OnDestroy()
        {
            if (agent != null)
            {
                agent.OnStepInfoUpdated -= OnAgentStepUpdated;
            }
        }
        
        // Public method to reset metrics (useful for multiple evaluation runs)
        public void ResetMetrics()
        {
            Initialize();
            recentRewards.Clear();
            recentSpeeds.Clear();
        }
    }
}
