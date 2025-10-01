using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DroneHUD : MonoBehaviour
{
    [Header("Bindings")]
    public Transform drone;                 // assign the drone root
    public QuadController controller;       // optional; used for input readout
    public Transform explicitTarget;        // optional; overrides auto-find goals
    public bool autoFindGoals = true;

    [Header("Appearance")]
    public string panelName = "DroneHUDCanvas";
    public Vector2 panelSize = new Vector2(320, 180);
    public Vector2 panelMargin = new Vector2(16, 16); // from top-right
    public Color panelColor = new Color(0f, 0f, 0f, 0.35f);
    public int fontSize = 14;
    public Color fontColor = Color.white;

    [Header("Altitude")]
    public float maxAGLProbe = 1000f; // meters for ground raycast

    private Rigidbody rb;
    private Canvas canvas;
    private Text text;
    private Transform nearestGoalCached;
    private float nextGoalSearchTime;

    private void Awake()
    {
        if (drone == null)
        {
            var d = GameObject.Find("DroneAgentPlaceholder");
            if (d != null) drone = d.transform;
        }
        if (drone != null && controller == null) controller = drone.GetComponent<QuadController>();
        if (drone != null && rb == null) rb = drone.GetComponent<Rigidbody>();
    }

    private void Start()
    {
        EnsureUI();
    }

    private void OnValidate()
    {
        if (drone != null && rb == null) rb = drone.GetComponent<Rigidbody>();
        if (drone != null && controller == null) controller = drone.GetComponent<QuadController>();
    }

    private void EnsureUI()
    {
        // Reuse an existing HUD canvas if present
        var existing = GameObject.Find(panelName);
        if (existing != null)
        {
            canvas = existing.GetComponent<Canvas>();
            if (canvas != null)
            {
                text = existing.GetComponentInChildren<Text>(true);
                if (text != null) return;
            }
        }

        // Create Canvas
        var goCanvas = new GameObject(panelName);
        canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        goCanvas.AddComponent<CanvasScaler>();
        goCanvas.AddComponent<GraphicRaycaster>();

        // Panel
        var panel = new GameObject("Panel");
        panel.transform.SetParent(goCanvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.sizeDelta = panelSize;
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-panelMargin.x, -panelMargin.y);
        var image = panel.AddComponent<Image>();
        image.color = panelColor;

        // Text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(10f, 10f);
        textRect.offsetMax = new Vector2(-10f, -10f);
        text = textGO.AddComponent<Text>();

        // Robust font resolution for modern Unity versions
        Font fontAsset = null;
        try { fontAsset = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {}
        if (fontAsset == null)
        {
            // Fallbacks: try legacy Arial builtin (older Unity) or OS fonts
            try { fontAsset = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {}
            if (fontAsset == null)
            {
                string[] candidates = { "Arial", "Segoe UI", "Tahoma", "Helvetica", "Verdana" };
                for (int i = 0; i < candidates.Length && fontAsset == null; i++)
                {
                    try { fontAsset = Font.CreateDynamicFontFromOSFont(candidates[i], fontSize); } catch {}
                }
            }
        }
        text.font = fontAsset;

        text.fontSize = fontSize;
        text.color = fontColor;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void Update()
    {
        if (drone == null) return;
        if (rb == null) rb = drone.GetComponent<Rigidbody>();

        // Resolve target
        Transform tgt = explicitTarget != null ? explicitTarget : (autoFindGoals ? FindNearestGoal() : null);

        // Metrics
        Vector3 pos = drone.position;
        Vector3 vel = rb != null ? rb.velocity : Vector3.zero;
        float speed = vel.magnitude;
        float horSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
        float vertSpeed = vel.y;

        float agl = ComputeAGL(pos);
        string aglStr = float.IsNaN(agl) ? "n/a" : agl.ToString("F1") + " m";

        float dist = (tgt != null) ? Vector3.Distance(pos, tgt.position) : -1f;
        string distStr = (tgt != null) ? dist.ToString("F1") + " m" : "n/a";

        float throttle = controller != null ? controller.throttle : 0f;
        float pitchIn = controller != null ? controller.pitch : 0f;
        float rollIn = controller != null ? controller.roll : 0f;
        float yawIn = controller != null ? controller.yaw : 0f;

        var sb = new StringBuilder(256);
        sb.AppendLine("Drone HUD");
        sb.AppendLine($"Target Dist: {distStr}");
        sb.AppendLine($"Altitude AGL: {aglStr}");
        sb.AppendLine($"Speed: {speed:F1} m/s   Hor: {horSpeed:F1}   Vert: {vertSpeed:F1}");
        sb.AppendLine($"Throttle: {throttle:F2}   Pitch: {pitchIn:F2}   Roll: {rollIn:F2}   Yaw: {yawIn:F2}");
        sb.AppendLine($"Pos: X {pos.x:F1}  Y {pos.y:F1}  Z {pos.z:F1}");

        if (text != null) text.text = sb.ToString();
    }

    private float ComputeAGL(Vector3 origin)
    {
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxAGLProbe))
        {
            return hit.distance;
        }
        return float.NaN;
    }

    private Transform FindNearestGoal()
    {
        float now = Time.time;
        if (nearestGoalCached != null && now < nextGoalSearchTime)
            return nearestGoalCached;

        nextGoalSearchTime = now + 0.5f;

        // Local helper: decide if a transform looks like a goal without relying on tags
        bool LooksLikeGoal(Transform tr)
        {
            if (tr == null) return false;
            string nm = tr.name ?? string.Empty;
            string nml = nm.ToLowerInvariant();
            if (nml.StartsWith("goal_") || nml.Contains("goal")) return true;

            // Fallback: check for any component whose type name suggests a goal zone
            var comps = tr.GetComponents<Component>();
            for (int j = 0; j < comps.Length; j++)
            {
                var c = comps[j];
                if (c == null) continue;
                var tn = c.GetType().Name;
                if (tn == "GoalZone" || tn == "Goal" || tn == "GoalTag") return true;
            }
            return false;
        }

        Transform best = null;
        float bestD2 = float.PositiveInfinity;

        var all = GameObject.FindObjectsOfType<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var tr = all[i];
            if (tr == null || tr == drone) continue;
            if (!LooksLikeGoal(tr)) continue;

            float d2 = (tr.position - drone.position).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = tr;
            }
        }

        nearestGoalCached = best;
        return best;
    }
}
