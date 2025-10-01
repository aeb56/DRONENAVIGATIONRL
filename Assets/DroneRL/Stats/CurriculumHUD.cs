using UnityEngine;
using UnityEngine.UI;

public class CurriculumHUD : MonoBehaviour
{
    public StageCurriculumController controller;
    public Vector2 panelSize = new Vector2(320, 150);
    public Vector2 panelMargin = new Vector2(16, 16);
    public Color panelColor = new Color(0f,0f,0f,0.45f);
    public int fontSize = 14; public Color fontColor = Color.white;

    private Text text;

    private void Awake()
    {
        if (controller == null) controller = FindObjectOfType<StageCurriculumController>();
        CreateUI();
    }

    private void Update()
    {
        if (text == null || controller == null) return;
        var thresholds = controller.GetCurrentThresholds();
        text.text =
            $"Curriculum\n" +
            $"Stage: {controller.currentStage}/9\n" +
            $"Episodes in Stage: {controller.EpisodesInStage}\n" +
            $"Success: {controller.SuccessRateInStage*100f:F1}%  Collisions: {controller.CollisionRateInStage*100f:F1}%\n" +
            $"Agent Collisions: {controller.AgentCrashesInStage}  Obstacle Collisions: {controller.ObstacleCrashesInStage}\n" +
            $"Timeouts: {controller.TimeoutsInStage}\n" +
            $"Promote when: Succ>={thresholds.succ*100f:F0}%  Coll<={thresholds.coll*100f:F0}%";
    }

    private void CreateUI()
    {
        var goCanvas = new GameObject("CurriculumHUD");
        var canvas = goCanvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; goCanvas.AddComponent<CanvasScaler>(); goCanvas.AddComponent<GraphicRaycaster>();
        var panel = new GameObject("Panel"); panel.transform.SetParent(goCanvas.transform,false); var r = panel.AddComponent<RectTransform>(); r.sizeDelta = panelSize; r.anchorMin = new Vector2(1f,0f); r.anchorMax = new Vector2(1f,0f); r.pivot = new Vector2(1f,0f); r.anchoredPosition = new Vector2(-panelMargin.x,panelMargin.y); var img = panel.AddComponent<Image>(); img.color = panelColor;
        var tgo = new GameObject("Text"); tgo.transform.SetParent(panel.transform,false); var tr = tgo.AddComponent<RectTransform>(); tr.anchorMin = new Vector2(0f,0f); tr.anchorMax = new Vector2(1f,1f); tr.offsetMin = new Vector2(10,10); tr.offsetMax = new Vector2(-10,-10); text = tgo.AddComponent<Text>();
        Font fontAsset = null; try { fontAsset = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch {} if (fontAsset==null) { try { fontAsset = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch {} }
        text.font = fontAsset; text.fontSize = fontSize; text.color = fontColor; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
    }
}


