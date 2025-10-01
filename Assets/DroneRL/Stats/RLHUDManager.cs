using UnityEngine;
using TMPro;

/// <summary>
/// Modular RL HUD showing per-agent metrics (distance, steps, episode, rewards, velocity, position).
/// Attach to a GameObject in the scene; it will spawn a Canvas + TextMeshProUGUI overlay on the left side.
/// Designed to work with a single primary DroneAgent for display (can be extended for multi-agent aggregation).
/// </summary>
public class RLHUDManager : MonoBehaviour
{
    [Header("Bindings")] public DroneAgent agent; public Transform targetOverride;
    [Header("Appearance")] public string canvasName = "RLHUDCanvas"; public Vector2 panelSize = new Vector2(340, 240); public Vector2 margin = new Vector2(16, 240); public Color panelColor = new Color(0,0,0,0.55f); public int fontSize = 16; public Color fontColor = Color.white;
    [Header("Options")] public bool showVelocity = true; public bool showPosition = true; public bool autoFindAgent = true; public bool autoFindTarget = true;

    private Canvas canvas; private RectTransform panelRect; private TextMeshProUGUI text; private Rigidbody agentRB;
    private float cumulativeRewardThisEpisode; private int lastRecordedEpisode = -1;

    private void Awake()
    {
        if (autoFindAgent && agent == null) agent = FindObjectOfType<DroneAgent>();
        if (agent != null && agentRB == null) agentRB = agent.GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (agent != null) agent.OnStepInfoUpdated += HandleAgentStep;
    }
    private void OnDisable()
    {
        if (agent != null) agent.OnStepInfoUpdated -= HandleAgentStep;
    }

    private void Start() { EnsureUI(); }

    private void EnsureUI()
    {
        if (GameObject.Find(canvasName) != null)
        {
            var existing = GameObject.Find(canvasName);
            text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null) return;
        }
        var goCanvas = new GameObject(canvasName);
        canvas = goCanvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; goCanvas.AddComponent<UnityEngine.UI.CanvasScaler>(); goCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var panel = new GameObject("Panel"); panel.transform.SetParent(goCanvas.transform,false); panelRect = panel.AddComponent<RectTransform>(); panelRect.sizeDelta = panelSize; panelRect.anchorMin = new Vector2(0,1); panelRect.anchorMax = new Vector2(0,1); panelRect.pivot = new Vector2(0,1); panelRect.anchoredPosition = new Vector2(margin.x,-margin.y); var img = panel.AddComponent<UnityEngine.UI.Image>(); img.color = panelColor;
        var tgo = new GameObject("RLText"); tgo.transform.SetParent(panel.transform,false); var tr = tgo.AddComponent<RectTransform>(); tr.anchorMin = new Vector2(0,0); tr.anchorMax = new Vector2(1,1); tr.offsetMin = new Vector2(10,10); tr.offsetMax = new Vector2(-10,-10); text = tgo.AddComponent<TextMeshProUGUI>(); text.fontSize = fontSize; text.color = fontColor; text.alignment = TextAlignmentOptions.TopLeft; text.enableWordWrapping = false;
    }

    private void Update()
    {
        if (agent == null && autoFindAgent) { agent = FindObjectOfType<DroneAgent>(); if (agent!=null) { agentRB = agent.GetComponent<Rigidbody>(); agent.OnStepInfoUpdated += HandleAgentStep; } }
        if (agent != null && agentRB == null) agentRB = agent.GetComponent<Rigidbody>();
        if (agent == null || text == null) return;
        if (autoFindTarget && targetOverride == null && agent.goal != null) targetOverride = agent.goal;

        // Detect new episode boundary
        if (agent.EpisodeIndex != lastRecordedEpisode)
        {
            lastRecordedEpisode = agent.EpisodeIndex;
            cumulativeRewardThisEpisode = 0f; // will be rebuilt from step rewards as they come in
        }

        // Compose HUD text
        float dist = (targetOverride != null ? Vector3.Distance(agent.transform.position, targetOverride.position) : agent.CurrentDistanceToGoal);
        var sb = new System.Text.StringBuilder(256);
        sb.AppendLine("RL Training HUD");
        sb.AppendLine($"Episode: {agent.EpisodeIndex}");
        sb.AppendLine($"Step: {agent.StepCount}");
        sb.AppendLine($"Distance: {dist:F2} m");
        sb.AppendLine($"Step Reward: {agent.LastStepReward:F4}");
        sb.AppendLine($"Cumulative Ep Reward: {cumulativeRewardThisEpisode:F3}");
        sb.AppendLine($"Successes: {agent.SuccessCount}  Failures: {agent.FailureCount}");
        sb.AppendLine($"Collisions This Ep: {agent.CollisionCount}");
        sb.AppendLine($"Min Distance This Ep: {agent.MinDistanceThisEpisode:F2}m");
        if (showVelocity && agentRB != null)
        {
            var v = agentRB.velocity; float speed = v.magnitude; sb.AppendLine($"Speed: {speed:F2} m/s (vx {v.x:F2} vy {v.y:F2} vz {v.z:F2})");
        }
        if (showPosition)
        {
            var p = agent.transform.position; sb.AppendLine($"Pos: {p.x:F1}, {p.y:F1}, {p.z:F1}");
        }
        text.text = sb.ToString();
    }

    private void HandleAgentStep(DroneAgent a)
    {
        // Keep cumulative reward manually: Agent.GetCumulativeReward() resets only at episode boundaries; we want per-episode total.
        if (a == null) return;
        cumulativeRewardThisEpisode += a.LastStepReward;
    }
}
