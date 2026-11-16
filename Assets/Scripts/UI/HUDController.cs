using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Presentation layer for the Sonar Sentinel HUD. Mission code can call the
/// public setters directly; the optional scene references bridge existing
/// detection and sound signals without owning their gameplay logic.
/// </summary>
public sealed class HUDController : MonoBehaviour
{
    [Header("Alert meter")]
    [SerializeField] private Image alertBarFill;
    [SerializeField] private Image[] alertSegments;
    [SerializeField] private TMP_Text detectionStatus;
    [SerializeField] private TMP_Text detectionPercentText;
    [SerializeField, Range(0f, 1f)] private float alertMediumThreshold = 0.34f;
    [SerializeField, Range(0f, 1f)] private float alertHighThreshold = 0.67f;

    [Header("Noise meter")]
    [SerializeField] private Image noiseBarFill;
    [SerializeField] private TMP_Text noiseStatus;
    [SerializeField] private TMP_Text noiseSignalLabel;
    [SerializeField] private float maximumSoundRadius = 12f;
    [SerializeField] private float noiseHoldSeconds = 1.5f;
    [SerializeField] private float noiseFadeSeconds = 2f;
    [SerializeField, Range(0f, 1f)] private float noiseMediumThreshold = 0.34f;
    [SerializeField, Range(0f, 1f)] private float noiseHighThreshold = 0.67f;

    [Header("Objective")]
    [SerializeField] private GameObject objectivePanel;
    [SerializeField] private TMP_Text objectiveText;

    [Header("Mission screens")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject caughtScreen;

    [Header("Live signal bridges")]
    [SerializeField] private DroneVision droneVision;
    [SerializeField] private Transform player;
    [SerializeField] private Transform drone;
    [SerializeField] private Transform extractionPoint;
    [SerializeField] private Transform playerMapMarker;
    [SerializeField] private Transform droneMapMarker;
    [SerializeField] private Transform extractionMapMarker;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private float minimapHeight = 35f;
    [SerializeField] private float markerHeight = 0.65f;
    [SerializeField] private bool listenToGameplaySignals = true;

    private float noiseLevel;
    private float noiseHoldTimer;
    private float noisePulseTimer;
    private Transform noisePulseRoot;
    private Vector3 noisePulseBaseScale = Vector3.one;
    private bool dataCoreSecured;

    private static readonly Color LowColor = new Color(0.12f, 0.92f, 0.75f, 1f);
    private static readonly Color MediumColor = new Color(1f, 0.69f, 0.16f, 1f);
    private static readonly Color HighColor = new Color(1f, 0.22f, 0.28f, 1f);

    private void Awake()
    {
        // Existing scenes gain phone notch/home-indicator handling without
        // requiring a manual inspector migration.
        if (GetComponent<SafeAreaFitter>() == null)
            gameObject.AddComponent<SafeAreaFitter>();

        SetPanelRaycastTarget("AlertPanel");
        SetPanelRaycastTarget("NoisePanel");
        SetPanelRaycastTarget("ObjectivePanel");
        SetPanelRaycastTarget("MinimapPanel");

        TMP_Text iconGlyph = transform.Find("ObjectivePanel/ObjectiveIconGlyph")?.GetComponent<TMP_Text>();
        if (iconGlyph != null)
            iconGlyph.text = "C";

        if (noiseBarFill != null && noiseBarFill.transform.parent != null)
        {
            noisePulseRoot = noiseBarFill.transform.parent;
            noisePulseBaseScale = noisePulseRoot.localScale;
        }
    }

    private void OnEnable()
    {
        if (listenToGameplaySignals)
            SoundSystem.OnSoundEmitted += HandleSoundEmitted;

        if (droneVision != null)
            droneVision.PlayerCaught += ShowCaughtScreen;
    }

    private void OnDisable()
    {
        SoundSystem.OnSoundEmitted -= HandleSoundEmitted;

        if (droneVision != null)
            droneVision.PlayerCaught -= ShowCaughtScreen;
    }

    private void Start()
    {
        SetDetectionProgress(droneVision != null ? droneVision.DetectionProgress : 0f);
        SetNoiseLevel(0f);
        SetObjectiveStatus(dataCoreSecured);
        SetExtractionMarkerVisible(extractionPoint != null);
    }

    private void Update()
    {
        if (listenToGameplaySignals && droneVision != null)
            SetDetectionProgress(droneVision.DetectionProgress);

        if (noiseHoldTimer > 0f)
        {
            noiseHoldTimer -= Time.deltaTime;
        }
        else if (noiseLevel > 0f)
        {
            float fadeDuration = Mathf.Max(0.01f, noiseFadeSeconds);
            SetNoiseLevel(Mathf.MoveTowards(noiseLevel, 0f, Time.deltaTime / fadeDuration));
        }

        if (noisePulseTimer > 0f && noisePulseRoot != null)
        {
            noisePulseTimer = Mathf.Max(0f, noisePulseTimer - Time.deltaTime);
            float pulseProgress = 1f - noisePulseTimer / 0.42f;
            float pulse = 1f + Mathf.Sin(pulseProgress * Mathf.PI) * 0.075f;
            noisePulseRoot.localScale = noisePulseBaseScale * pulse;
        }
        else if (noisePulseRoot != null)
        {
            noisePulseRoot.localScale = noisePulseBaseScale;
        }
    }

    private void LateUpdate()
    {
        if (minimapCamera != null && player != null)
        {
            Vector3 cameraPosition = player.position;
            cameraPosition.y = minimapHeight;
            minimapCamera.transform.position = cameraPosition;
        }

        FollowMarker(playerMapMarker, player, markerHeight);
        FollowMarker(droneMapMarker, drone, markerHeight);
        FollowMarker(extractionMapMarker, extractionPoint, markerHeight);
    }

    /// <summary>Sets the existing vision timer as a normalized [0, 1] value.</summary>
    public void SetDetectionProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (alertBarFill != null)
            alertBarFill.fillAmount = progress;

        SetMeterState(detectionStatus, null, progress,
            alertMediumThreshold, alertHighThreshold, "LOW");
        if (detectionStatus != null)
            detectionStatus.text = "DETECTION: " + detectionStatus.text;
        if (detectionPercentText != null)
            detectionPercentText.text = Mathf.RoundToInt(progress * 100f) + "%";

        for (int i = 0; i < alertSegments.Length; i++)
        {
            Image segment = alertSegments[i];
            if (segment == null) continue;

            bool active = progress > i / (float)alertSegments.Length;
            float segmentValue = (i + 0.5f) / alertSegments.Length;
            segment.color = active ? GetLevelColor(segmentValue,
                alertMediumThreshold, alertHighThreshold) : new Color(0.25f, 0.29f, 0.33f, 1f);
        }
    }

    /// <summary>Sets relative game noise intensity. This is not a dB reading.</summary>
    public void SetNoiseLevel(float level)
    {
        noiseLevel = Mathf.Clamp01(level);
        if (noiseBarFill != null)
            noiseBarFill.fillAmount = noiseLevel;

        SetMeterState(noiseStatus, noiseBarFill, noiseLevel,
            noiseMediumThreshold, noiseHighThreshold, "LOW");
        if (noiseSignalLabel != null)
            noiseSignalLabel.text = "RELATIVE REACH  /  " + Mathf.RoundToInt(noiseLevel * 100f) + "%";
    }

    /// <summary>Updates the Data Core objective from mission code.</summary>
    public void SetObjectiveStatus(bool secured)
    {
        dataCoreSecured = secured;
        RefreshObjectiveText();
    }

    public void ToggleObjectivePanel()
    {
        if (objectivePanel == null)
        {
            Transform panel = transform.Find("ObjectivePanel");
            if (panel != null) objectivePanel = panel.gameObject;
        }

        if (objectivePanel != null)
            objectivePanel.SetActive(!objectivePanel.activeSelf);
    }

    private void RefreshObjectiveText()
    {
        if (objectiveText == null)
            return;

        objectiveText.text = dataCoreSecured
            ? extractionPoint != null
                ? "DATA CORE   //   SECURED\nPROCEED TO EXTRACTION"
                : "DATA CORE   //   SECURED\nEXTRACTION POINT AWAITING COORDINATES"
            : "DATA CORE   //   NOT SECURED\nEXTRACTION COORDINATES PENDING";
    }

    public void ShowMissionComplete()
    {
        if (winScreen != null) winScreen.SetActive(true);
        if (caughtScreen != null) caughtScreen.SetActive(false);
    }

    public void ShowCaughtScreen()
    {
        if (caughtScreen != null) caughtScreen.SetActive(true);
        if (winScreen != null) winScreen.SetActive(false);
    }

    /// <summary>Restart button hook; reloads the currently active scene.</summary>
    public void RestartMission()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Allows Part 1 to provide or replace its extraction target.</summary>
    public void SetExtractionPoint(Transform target)
    {
        extractionPoint = target;
        SetExtractionMarkerVisible(target != null);
        RefreshObjectiveText();
    }

    /// <summary>Allows Part 1 to provide its mission-specific DroneVision.</summary>
    public void SetDroneVision(DroneVision vision)
    {
        if (droneVision != null)
            droneVision.PlayerCaught -= ShowCaughtScreen;

        droneVision = vision;

        if (isActiveAndEnabled && droneVision != null)
            droneVision.PlayerCaught += ShowCaughtScreen;
    }

    private void HandleSoundEmitted(Vector3 position, float radius)
    {
        if (maximumSoundRadius <= 0f)
            return;

        // SoundSystem publishes game-space reach, so the HUD displays a
        // normalized relative signal rather than claiming physical dB.
        SetNoiseLevel(radius / maximumSoundRadius);
        noiseHoldTimer = Mathf.Max(0f, noiseHoldSeconds);
        noisePulseTimer = 0.42f;
    }

    private void SetExtractionMarkerVisible(bool visible)
    {
        if (extractionMapMarker != null)
            extractionMapMarker.gameObject.SetActive(visible);
    }

    private static void FollowMarker(Transform marker, Transform target, float height)
    {
        if (marker == null)
            return;

        if (target == null)
        {
            marker.gameObject.SetActive(false);
            return;
        }

        marker.gameObject.SetActive(true);
        marker.position = target.position + Vector3.up * height;
    }

    private void SetPanelRaycastTarget(string panelName)
    {
        Transform panel = transform.Find(panelName);
        Image panelImage = panel != null ? panel.GetComponent<Image>() : null;
        if (panelImage != null)
            panelImage.raycastTarget = true;
    }

    private static void SetMeterState(TMP_Text label, Image fill, float value,
        float mediumThreshold, float highThreshold, string emptyLabel)
    {
        string state = value >= highThreshold ? "HIGH"
            : value >= mediumThreshold ? "MEDIUM"
            : emptyLabel;
        Color color = GetLevelColor(value, mediumThreshold, highThreshold);

        if (label != null)
        {
            label.text = state;
            label.color = color;
        }

        if (fill != null)
            fill.color = color;
    }

    private static Color GetLevelColor(float value, float mediumThreshold, float highThreshold)
    {
        return value >= highThreshold ? HighColor
            : value >= mediumThreshold ? MediumColor
            : LowColor;
    }
}
