using System.Text;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays high-level training / episode information on the left side of the screen.
/// Non-intrusive: does not replace the existing DroneHUD (which sits top-right).
/// Allows manual environment reset with the 'R' key (in Editor / when not running inference build automation).
/// </summary>
public class TrainingHUD : MonoBehaviour
{
    [Header("Bindings")] public DroneTrainingEnv env; // auto-found if null

    [Header("Appearance")] public string panelName = "TrainingHUDPanel"; public Vector2 panelSize = new Vector2(340, 210); public Vector2 panelMargin = new Vector2(16, 16); public Color panelColor = new Color(0f,0f,0f,0.45f); public int fontSize = 14; public Color fontColor = Color.white;

    [Header("Controls")] public KeyCode resetKey = KeyCode.R; public bool allowKeyboardReset = true; public bool showActionHints = true;

    private Canvas canvas; private Text text; private float lastStatPollTime; private float avgEpisodeLen; private float avgReward; private float successRate; private float collisionRate; private float timeoutRate; private int episodes; private int successes; private int timeouts; private int crashes;

    private void Awake() { if (env == null) env = FindObjectOfType<DroneTrainingEnv>(); }
    private void Start() { EnsureUI(); }

    private void EnsureUI()
    {
        if (GameObject.Find(panelName) != null) { var existing = GameObject.Find(panelName); text = existing.GetComponentInChildren<Text>(true); if (text!=null) return; }
        var goCanvas = new GameObject(panelName);
        canvas = goCanvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; goCanvas.AddComponent<CanvasScaler>(); goCanvas.AddComponent<GraphicRaycaster>();
        var panel = new GameObject("Panel"); panel.transform.SetParent(goCanvas.transform,false); var r = panel.AddComponent<RectTransform>(); r.sizeDelta = panelSize; r.anchorMin = new Vector2(0f,0f); r.anchorMax = new Vector2(0f,0f); r.pivot = new Vector2(0f,0f); r.anchoredPosition = new Vector2(panelMargin.x,panelMargin.y); var img = panel.AddComponent<Image>(); img.color = panelColor;
        var tgo = new GameObject("Text"); tgo.transform.SetParent(panel.transform,false); var tr = tgo.AddComponent<RectTransform>(); tr.anchorMin = new Vector2(0f,0f); tr.anchorMax = new Vector2(1f,1f); tr.offsetMin = new Vector2(10,10); tr.offsetMax = new Vector2(-10,-10); text = tgo.AddComponent<Text>();
        Font fontAsset = null; try { fontAsset = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {} if (fontAsset==null) { try { fontAsset = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {} }
        text.font = fontAsset; text.fontSize = fontSize; text.color = fontColor; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void Update()
    {
        if (env == null) env = FindObjectOfType<DroneTrainingEnv>();
    if (allowKeyboardReset && Input.GetKeyDown(resetKey) && env != null)
        {
            // Forcibly reset: emulate episode timeout path
            foreach (var agent in FindObjectsOfType<DroneAgent>()) agent.EndEpisode();
            typeof(DroneTrainingEnv).GetMethod("ResetEnvironment", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.Invoke(env,null);
        }

        // Poll stats recorder less often to avoid overhead
        if (Time.time - lastStatPollTime > 1.0f)
        {
            lastStatPollTime = Time.time;
            if (env != null)
            {
                avgEpisodeLen = env.GetAverageEpisodeTime();
                avgReward = env.GetAverageEpisodeReward();
                successRate = env.GetSuccessRate();
                episodes = env.episodesCompleted;
                successes = env.successes;
                timeouts = env.timeouts;
                crashes = env.crashes;
                collisionRate = episodes > 0 ? (float)crashes / episodes : 0f;
                timeoutRate = episodes > 0 ? (float)timeouts / episodes : 0f;
            }
        }

        if (text != null)
        {
            float tRemain = env != null ? Mathf.Max(0f, env.episodeLength - env.timer) : 0f; float tPct = env != null ? env.GetTimeRemaining01()*100f : 0f;
            var agents = FindObjectsOfType<DroneAgent>();
            int agentCount = agents.Length;
            float avgDist = 0f; int distSamples = 0;
            for (int i=0;i<agents.Length;i++)
            {
                var ag = agents[i];
                if (ag != null && ag.goal != null)
                {
                    avgDist += Vector3.Distance(ag.transform.position, ag.goal.position);
                    distSamples++;
                }
            }
            if (distSamples>0) avgDist /= distSamples;
            float representativeReward = agents.Length>0 ? agents[0].GetCumulativeReward() : 0f;
            var sb = new StringBuilder(256);
            sb.AppendLine("Training HUD");
            sb.AppendLine($"Agents: {agentCount}");
            if (env != null)
            {
                sb.AppendLine($"Episode Time: {env.timer:F1}s / {env.episodeLength:F0}s ({tPct:F0}% left)");
                sb.AppendLine($"Difficulty: {env.difficulty:F2}  Level: {env.level}");
                sb.AppendLine($"Arena: {env.arenaSize.x:F0}x{env.arenaSize.y:F0}  Ceiling: {env.ceilingHeight:F0}m");
                sb.AppendLine($"Avg Goal Dist: {avgDist:F1} m");
                sb.AppendLine($"Episodes: {episodes}  Success: {successRate*100f:F1}%  Collisions: {collisionRate*100f:F1}%  Timeouts: {timeoutRate*100f:F1}%");
            }
            sb.AppendLine($"Avg Ep Len: {avgEpisodeLen:F1}s  Avg Reward: {avgReward:F2}  Success Rate: {successRate*100f:F1}%");
            sb.AppendLine($"Current Agent Reward: {representativeReward:F2}");
            if (showActionHints && allowKeyboardReset) sb.AppendLine($"Press {resetKey} to force reset");
            text.text = sb.ToString();
        }
    }
}
