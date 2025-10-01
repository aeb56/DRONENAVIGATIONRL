using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class EvaluationRunner : EditorWindow
{
    private int seeds = 10;
    private int scenarios = 3;
    private float episodeLength = 60f;
    private string csvPath = "Assets/DroneRL/Experiments/eval_results.csv";

    [MenuItem("Tools/DroneRL/📈 Evaluation Runner")]
    public static void ShowWindow()
    {
        var w = GetWindow<EvaluationRunner>("Evaluation Runner");
        w.minSize = new Vector2(380, 220);
        w.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Evaluation Settings", EditorStyles.boldLabel);
        seeds = EditorGUILayout.IntField("Seeds", seeds);
        scenarios = EditorGUILayout.IntField("Scenarios", scenarios);
        episodeLength = EditorGUILayout.FloatField("Episode Length (s)", episodeLength);
        csvPath = EditorGUILayout.TextField("CSV Output", csvPath);

        if (GUILayout.Button("Run Evaluation (Editor Play)"))
        {
            RunEvaluation();
        }
    }

    private void RunEvaluation()
    {
        var env = Object.FindObjectOfType<DroneTrainingEnv>();
        if (env == null)
        {
            EditorUtility.DisplayDialog("Missing Env", "No DroneTrainingEnv found in scene.", "OK");
            return;
        }

        List<string> lines = new List<string>();
        lines.Add("seed,scenario,episodes_completed,successes,success_rate,avg_reward,avg_time");

        for (int s = 0; s < seeds; s++)
        {
            int seed = Random.Range(0, int.MaxValue);
            Random.InitState(seed);

            for (int sc = 0; sc < scenarios; sc++)
            {
                // Configure scenario via AdvancedTrainingEnvironments if present
                var adv = Object.FindObjectOfType<AdvancedTrainingEnvironments>();
                if (adv != null)
                {
                    adv.currentScenario = (AdvancedTrainingEnvironments.ScenarioType)Mathf.Clamp(sc, 0, System.Enum.GetValues(typeof(AdvancedTrainingEnvironments.ScenarioType)).Length - 1);
                }

                // Reset env counters
                env.episodesCompleted = 0;
                env.successes = 0;
                env.cumulativeReward = 0f;
                env.cumulativeEpisodeTime = 0f;
                env.episodeLength = episodeLength;

                // Simulate a single episode-length window per evaluation (Editor-only approximation)
                double start = EditorApplication.timeSinceStartup;
                while (EditorApplication.timeSinceStartup - start < episodeLength / Mathf.Max(Time.timeScale, 0.01f))
                {
                    // Pump the editor for a bit
                    System.Threading.Thread.Sleep(10);
                }

                lines.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4:F4},{5:F4},{6:F4}",
                    seed,
                    sc,
                    env.episodesCompleted,
                    env.successes,
                    env.GetSuccessRate(),
                    env.GetAverageEpisodeReward(),
                    env.GetAverageEpisodeTime()
                ));
            }
        }

        string dir = Path.GetDirectoryName(csvPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(csvPath, lines);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Evaluation Complete", $"Wrote {lines.Count-1} rows to {csvPath}", "OK");
    }
}


