using UnityEngine;

public class ArenaManager : MonoBehaviour
{
    public RandomObstacleSpawner randomObstacleSpawner;
    public GoalManager goalManager;
    public Transform spawnPointsParent;

    [Header("Difficulty [0..1]")]
    [Range(0f, 1f)]
    public float difficulty = 0.5f;

    public Transform GetRandomSpawnPoint()
    {
        if (spawnPointsParent == null || spawnPointsParent.childCount == 0) return null;
        int idx = Random.Range(0, spawnPointsParent.childCount);
        return spawnPointsParent.GetChild(idx);
    }

    public void ResetEpisode()
    {
        if (randomObstacleSpawner != null)
        {
            randomObstacleSpawner.SetDifficulty(difficulty);
            randomObstacleSpawner.ResetSpawn();
        }

        if (goalManager != null)
        {
            goalManager.RandomizeGoals();
        }

        // Reposition placeholder/spawn if desired (future hook for agent reset)
        var agent = GameObject.Find("DroneAgentPlaceholder");
        if (agent != null)
        {
            var sp = GetRandomSpawnPoint();
            if (sp != null)
            {
                agent.transform.SetPositionAndRotation(sp.position, Quaternion.identity);
                var rb = agent.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
