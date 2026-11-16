using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// On-screen alert meter (top-left). Builds its own Canvas at runtime,
// so no scene setup is needed: DroneVisuals creates it automatically.
// Every drone reports its alert level each frame; the HUD shows the highest.
public class SonarHUD : MonoBehaviour
{
    public static SonarHUD Instance { get; private set; }

    [Header("Colours")]
    [SerializeField] private Color calmColor = Color.cyan;
    [SerializeField] private Color suspiciousColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color alertColor = Color.red;

    private struct Report
    {
        public float level;
        public string status;
        public int frame;
    }

    private readonly Dictionary<Object, Report> reports =
        new Dictionary<Object, Report>();
    private readonly List<Object> staleKeys = new List<Object>();

    private RectTransform fillRect;
    private Image fillImage;
    private Text label;

    private float shownLevel = -1f;
    private string shownStatus;

    public static SonarHUD EnsureExists()
    {
        if (Instance == null)
            new GameObject("SonarHUD").AddComponent<SonarHUD>();

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Simple API (matches the original snippet).
    public void UpdateAlertLevel(float level) =>
        ReportAlert(this, level, null);

    // Per-source API used by DroneVisuals.
    public void ReportAlert(Object source, float level, string status)
    {
        reports[source] = new Report
        {
            level = Mathf.Clamp01(level),
            status = status,
            frame = Time.frameCount
        };
    }

    private void LateUpdate()
    {
        float level = 0f;
        string status = "ALL CLEAR";
        staleKeys.Clear();

        foreach (var kv in reports)
        {
            // Drop sources that were destroyed or stopped reporting.
            if (kv.Key == null || kv.Value.frame < Time.frameCount - 1)
            {
                staleKeys.Add(kv.Key);
                continue;
            }

            if (kv.Value.level >= level)
            {
                level = kv.Value.level;
                if (!string.IsNullOrEmpty(kv.Value.status))
                    status = kv.Value.status;
            }
        }

        foreach (var key in staleKeys)
            reports.Remove(key);

        if (Mathf.Approximately(level, shownLevel) && status == shownStatus)
            return;

        shownLevel = level;
        shownStatus = status;

        Color color = LevelToColor(level);

        fillRect.anchorMax = new Vector2(Mathf.Max(level, 0.02f), 1f);
        fillImage.color = color;

        label.text = "SONAR // " + status;
        label.color = color;
    }

    private Color LevelToColor(float level)
    {
        return level < 0.5f
            ? Color.Lerp(calmColor, suspiciousColor, level / 0.5f)
            : Color.Lerp(suspiciousColor, alertColor, (level - 0.5f) / 0.5f);
    }

    // UI construction

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel
        var panel = CreateRect("Panel", transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(30f, -30f);
        panel.sizeDelta = new Vector2(380f, 80f);
        AddImage(panel, new Color(0f, 0f, 0f, 0.6f));

        // Label
        var labelRect = CreateRect("Label", panel);
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(-14f, -6f);

        label = labelRect.gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;

        // Bar background
        var barBg = CreateRect("BarBackground", panel);
        barBg.anchorMin = new Vector2(0f, 0f);
        barBg.anchorMax = new Vector2(1f, 0.5f);
        barBg.offsetMin = new Vector2(14f, 12f);
        barBg.offsetMax = new Vector2(-14f, -4f);
        AddImage(barBg, new Color(1f, 1f, 1f, 0.12f));

        // Bar fill (width driven by anchorMax.x)
        fillRect = CreateRect("BarFill", barBg);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0.02f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fillImage = AddImage(fillRect, calmColor);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
