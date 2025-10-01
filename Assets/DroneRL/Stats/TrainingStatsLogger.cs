using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.MLAgents;

/// <summary>
/// Periodically writes aggregate training stats to a JSON lines file for human-readable inspection alongside ONNX.
/// Each line is a JSON object with cumulative counts & averages.
/// Place on a GameObject in the scene (e.g., the environment root).
/// </summary>
public class TrainingStatsLogger : MonoBehaviour
{
    public DroneTrainingEnv env; // auto found
    [Tooltip("Write interval in seconds of real time.")] public float writeInterval = 10f;
    [Tooltip("File name (relative to Application.persistentDataPath or absolute if rooted). ")] public string fileName = "training_stats.jsonl";
    [Tooltip("Append if file exists; otherwise a new header line is created.")] public bool append = true;

    private float nextWrite;
    private string fullPath;

    private void Awake()
    {
        if (env == null) env = FindObjectOfType<DroneTrainingEnv>();
        if (string.IsNullOrEmpty(fileName)) fileName = "training_stats.jsonl";
        fullPath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(Application.persistentDataPath, fileName);
        if (!append && File.Exists(fullPath)) File.Delete(fullPath);
        nextWrite = Time.time + writeInterval;
    }

    private void Update()
    {
        if (Time.time < nextWrite) return;
        nextWrite = Time.time + writeInterval;
        if (env == null) return;

        var stageCtl = FindObjectOfType<StageCurriculumController>();
        int stage = stageCtl != null ? stageCtl.currentStage : -1;
        var rec = new Dictionary<string, object>
        {
            {"time", System.DateTime.UtcNow.ToString("o")},
            {"episodes_completed", env.episodesCompleted},
            {"successes", env.successes},
            {"success_rate", env.GetSuccessRate()},
            {"avg_episode_reward", env.GetAverageEpisodeReward()},
            {"avg_episode_time", env.GetAverageEpisodeTime()},
            {"difficulty", env.difficulty},
            {"level", env.level},
            {"arena_size", new float[]{ env.arenaSize.x, env.arenaSize.y }},
            {"ceiling", env.ceilingHeight},
            {"stage", stage},
            {"stage_episodes", stageCtl != null ? stageCtl.EpisodesInStage : 0},
            {"stage_success_rate", stageCtl != null ? stageCtl.SuccessRateInStage : 0f},
            {"stage_collision_rate", stageCtl != null ? stageCtl.CollisionRateInStage : 0f},
            {"stage_agent_crashes", stageCtl != null ? stageCtl.AgentCrashesInStage : 0},
            {"stage_obstacle_crashes", stageCtl != null ? stageCtl.ObstacleCrashesInStage : 0},
            {"stage_timeouts", stageCtl != null ? stageCtl.TimeoutsInStage : 0}
        };

        string json = SimpleJson.Serialize(rec); // internal lightweight serializer
        try
        {
            File.AppendAllText(fullPath, json + "\n");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TrainingStatsLogger] Failed to write stats: {ex.Message}");
        }
    }
}

/// <summary>
/// Minimal JSON writer (supports numbers, bool, string, float[]/double[]/int[], and nested Dictionary<string,object>). Not a full parser.
/// </summary>
internal static class SimpleJson
{
    public static string Serialize(Dictionary<string, object> dict)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        WriteDict(sb, dict);
        return sb.ToString();
    }

    private static void WriteDict(System.Text.StringBuilder sb, Dictionary<string, object> d)
    {
        sb.Append('{');
        bool first = true;
        foreach (var kv in d)
        {
            if (!first) sb.Append(','); first = false;
            WriteString(sb, kv.Key); sb.Append(':'); WriteValue(sb, kv.Value);
        }
        sb.Append('}');
    }

    private static void WriteArray(System.Text.StringBuilder sb, System.Collections.IEnumerable arr)
    {
        sb.Append('[');
        bool first = true;
        foreach (var v in arr)
        {
            if (!first) sb.Append(','); first = false;
            WriteValue(sb, v);
        }
        sb.Append(']');
    }

    private static void WriteString(System.Text.StringBuilder sb, string s)
    {
        sb.Append('"');
        if (s != null)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
        }
        sb.Append('"');
    }

    private static void WriteValue(System.Text.StringBuilder sb, object v)
    {
        if (v == null) { sb.Append("null"); return; }
        switch (v)
        {
            case string str: WriteString(sb, str); break;
            case float f: sb.Append(f.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case int i: sb.Append(i); break;
            case long l: sb.Append(l); break;
            case bool b: sb.Append(b ? "true" : "false"); break;
            case System.Collections.IDictionary dict:
                var nd = new Dictionary<string, object>();
                foreach (System.Collections.DictionaryEntry de in dict) nd[de.Key.ToString()] = de.Value;
                WriteDict(sb, nd);
                break;
            case System.Collections.IEnumerable enumerable when !(v is string):
                WriteArray(sb, enumerable);
                break;
            default:
                // Fallback to string
                WriteString(sb, v.ToString());
                break;
        }
    }
}
