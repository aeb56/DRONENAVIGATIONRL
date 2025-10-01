using UnityEngine;

public class GoalZone : MonoBehaviour
{
    public delegate void GoalTriggered(GameObject goal, Collider other);
    public event GoalTriggered OnGoalTriggered;

    // ML-Agents support
    private DroneAgent assignedAgent;

    private void OnTriggerEnter(Collider other)
    {
        // Legacy event system (for GoalManager)
        OnGoalTriggered?.Invoke(gameObject, other);

        // ML-Agents goal handling
        if (assignedAgent != null && other != null && other.transform == assignedAgent.transform)
        {
            float reward = assignedAgent.rewardConfig != null ? assignedAgent.rewardConfig.goalReachedBonus : 1f;
            assignedAgent.AddReward(reward);
            assignedAgent.EndEpisodeSafe(true);
        }
    }

    /// <summary>
    /// Assign this goal to a specific DroneAgent for ML-Agents training.
    /// </summary>
    public void Assign(DroneAgent agent)
    {
        assignedAgent = agent;
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = renderer.sharedMaterial;
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                renderer.sharedMaterial = mat;
            }
            mat.color = Color.yellow;
        }
    }
}
