using UnityEngine;

/// <summary>Fits a full-screen HUD root inside the active device safe area.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (lastSafeArea != Screen.safeArea || lastScreenSize != size)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        Rect safeArea = Screen.safeArea;
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        rectTransform.anchorMin = new Vector2(safeArea.xMin / Screen.width,
            safeArea.yMin / Screen.height);
        rectTransform.anchorMax = new Vector2(safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
