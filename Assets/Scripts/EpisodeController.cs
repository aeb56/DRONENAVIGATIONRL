using UnityEngine;

public class EpisodeController : MonoBehaviour
{
    [Header("References")]
    public ArenaManager arenaManager;
    public GoalManager goalManager;

    [Header("Episode Settings")]
    public float maxEpisodeSeconds = 60f;
    public bool autoResetOnSuccess = true;
    public bool autoResetOnTimeout = true;

    [Header("HUD")]
    public bool showHud = true;

    private float timeLeft;
    private int episodeIndex = 0;
    private int successCount = 0;
    private int failCount = 0;

    private void Start()
    {
        if (arenaManager == null) arenaManager = FindObjectOfType<ArenaManager>();
        if (goalManager == null) goalManager = FindObjectOfType<GoalManager>();

        if (goalManager != null)
        {
            goalManager.OnAnyGoalReached += HandleGoalReached;
        }

        BeginEpisode();
    }

    private void OnDestroy()
    {
        if (goalManager != null)
        {
            goalManager.OnAnyGoalReached -= HandleGoalReached;
        }
    }

    private void Update()
    {
        timeLeft -= Time.deltaTime;

        if (autoResetOnTimeout && timeLeft <= 0f)
        {
            failCount++;
            ResetEpisode("timeout");
        }

        // Keyboard controls
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetEpisode("manual");
        }

        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus))
        {
            AdjustDifficulty(0.1f);
        }

        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Underscore))
        {
            AdjustDifficulty(-0.1f);
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            // New random seed for random obstacles
            if (arenaManager != null && arenaManager.randomObstacleSpawner != null)
            {
                int newSeed = Random.Range(1, int.MaxValue);
                arenaManager.randomObstacleSpawner.SetSeed(newSeed, respawn: true);
            }
        }
    }

    private void HandleGoalReached(GameObject goal, Collider _)
    {
        successCount++;
        if (autoResetOnSuccess)
        {
            ResetEpisode("goal");
        }
    }

    private void BeginEpisode()
    {
        timeLeft = Mathf.Max(1f, maxEpisodeSeconds);
    }

    private void ResetEpisode(string reason)
    {
        episodeIndex++;
        if (arenaManager != null)
        {
            arenaManager.ResetEpisode();
        }
        BeginEpisode();
    }

    private void AdjustDifficulty(float delta)
    {
        if (arenaManager == null) return;
        arenaManager.difficulty = Mathf.Clamp01(arenaManager.difficulty + delta);
        // Apply immediately
        if (arenaManager.randomObstacleSpawner != null)
        {
            arenaManager.randomObstacleSpawner.SetDifficulty(arenaManager.difficulty);
            arenaManager.randomObstacleSpawner.ResetSpawn();
        }
        if (goalManager != null)
        {
            goalManager.RandomizeGoals();
        }
    }

    private void OnGUI()
    {
        if (!showHud) return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft };
        GUILayout.BeginArea(new Rect(10, 10, 350, 160), GUIContent.none, style);
        GUILayout.Label($"Episode: {episodeIndex}");
        GUILayout.Label($"Successes: {successCount}   Failures: {failCount}");
        GUILayout.Label($"Time Left: {timeLeft:0.0}s / {maxEpisodeSeconds:0.0}s");
        if (arenaManager != null)
        {
            GUILayout.Label($"Difficulty: {arenaManager.difficulty:0.00}");
        }
        GUILayout.Space(6);
        GUILayout.Label("Controls:");
        GUILayout.Label("R = Reset   N = New Seed   +/- = Adjust Difficulty");
        GUILayout.EndArea();
    }
}
