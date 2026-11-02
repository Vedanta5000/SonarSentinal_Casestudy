
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SoundRipple : MonoBehaviour
{
    [Header("Ripple Settings")]
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private int segments = 64;
    [SerializeField] private float lineWidth = 0.06f;

    [Header("Appearance")]
    [SerializeField] private Color rippleColor =
        new Color(0.1f, 1f, 0.3f, 1f);

    private LineRenderer lineRenderer;
    private float timer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = segments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    void Start()
    {
        DrawRing(0f);
    }

    void Update()
    {
        timer += Time.deltaTime;

        float progress = Mathf.Clamp01(timer / duration);

        float currentRadius = Mathf.Lerp(
            0f,
            maxRadius,
            progress
        );

        DrawRing(currentRadius);

        // Fade the ring as it expands.
        Color fadedColor = rippleColor;
        fadedColor.a = 1f - progress;

        lineRenderer.startColor = fadedColor;
        lineRenderer.endColor = fadedColor;

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    void DrawRing(float radius)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = (2f * Mathf.PI * i) / segments;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            lineRenderer.SetPosition(
                i,
                new Vector3(x, 0f, z)
            );
        }
    }
}
