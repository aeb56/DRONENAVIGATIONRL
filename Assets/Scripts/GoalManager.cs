using System.Collections.Generic;
using UnityEngine;

public class GoalManager : MonoBehaviour
{
    [Header("Arena")]
    public Vector2 arenaSize = new Vector2(200f, 200f);

    [Header("Goals")]
    public int count = 5;
    public float[] goalHeights = new float[] { 1f, 3f, 5f, 7f, 10f };
    public GameObject goalPrefab;
    public bool randomizeOnStart = true;

    public System.Action<GameObject, Collider> OnAnyGoalReached;

    private readonly List<GameObject> goals = new List<GameObject>();
    private System.Random rng = new System.Random();

    private void Start()
    {
        EnsureGoals();
        if (randomizeOnStart)
        {
            RandomizeGoals();
        }
    }

    public void EnsureGoals()
    {
        if (goalPrefab == null) return;

        // Clear existing children
        for (int i = goals.Count - 1; i >= 0; i--)
        {
            var g = goals[i];
            if (g != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    GameObject.DestroyImmediate(g);
                else
#endif
                    GameObject.Destroy(g);
            }
        }
        goals.Clear();

        int n = Mathf.Min(count, goalHeights.Length);
        for (int i = 0; i < n; i++)
        {
            var g = Instantiate(goalPrefab, transform);
            g.name = $"Goal_{i}";
            goals.Add(g);
        }
    }

    public void RandomizeGoals()
    {
        if (goals.Count == 0) EnsureGoals();

        var used = new HashSet<Vector2Int>();
        for (int i = 0; i < goals.Count; i++)
        {
            float h = goalHeights[Mathf.Clamp(i, 0, goalHeights.Length - 1)];
            Vector3 pos = RandomNonOverlappingPosition(used, h);
            goals[i].transform.position = pos;

            var zone = goals[i].GetComponentInChildren<GoalZone>();
            if (zone != null)
            {
                zone.OnGoalTriggered -= OnGoalTriggered;
                zone.OnGoalTriggered += OnGoalTriggered;
            }
        }
    }

    private Vector3 RandomNonOverlappingPosition(HashSet<Vector2Int> used, float height)
    {
        // Snap to coarse grid to avoid overlaps
        int grid = 6;
        int attempts = 0;
        while (attempts < 100)
        {
            attempts++;
            int gx = RandomRangeInt(-Mathf.FloorToInt(arenaSize.x * 0.5f / grid) + 1, Mathf.FloorToInt(arenaSize.x * 0.5f / grid) - 1);
            int gz = RandomRangeInt(-Mathf.FloorToInt(arenaSize.y * 0.5f / grid) + 1, Mathf.FloorToInt(arenaSize.y * 0.5f / grid) - 1);
            var key = new Vector2Int(gx, gz);
            if (used.Contains(key)) continue;
            used.Add(key);
            float x = gx * grid;
            float z = gz * grid;
            return new Vector3(x, height, z);
        }
        return new Vector3(0, height, 0);
    }

    private int RandomRangeInt(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }

    private void OnGoalTriggered(GameObject goal, Collider other)
    {
        // Notify listeners (e.g., EpisodeController/Agent) that a goal was reached
        OnAnyGoalReached?.Invoke(goal, other);
    }
}
