using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LineRenderer))]
public class SoundRipple : MonoBehaviour
{
    [Header("Ripple Settings")]
    [SerializeField, Min(0.1f)] private float maxRadius = 6f;
    [SerializeField, Min(0.1f)] private float duration = 1.2f;
    [SerializeField, Min(12)] private int segments = 64;
    [SerializeField, Min(0.005f)] private float lineWidth = 0.07f;
    [SerializeField, Min(1)] private int ringCount = 3;
    [SerializeField, Min(0f)] private float ringDelay = 0.18f;

    [Header("Appearance")]
    [SerializeField] private Color rippleColor = new Color(0.18f, 1f, 0.36f, 0.78f);

    private readonly List<LineRenderer> rings = new List<LineRenderer>();
    private Material runtimeRippleMaterial;
    private float timer;

    private void Awake()
    {
        ringCount = Mathf.Max(1, ringCount);
        segments = Mathf.Max(12, segments);
        duration = Mathf.Max(0.1f, duration);

        LineRenderer first = GetComponent<LineRenderer>();
        runtimeRippleMaterial = CreateTransparentRippleMaterial(first.sharedMaterial);
        ConfigureLine(first);
        rings.Add(first);

        for (int i = 1; i < ringCount; i++)
        {
            GameObject ringObject = new GameObject("EchoRing_" + i);
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ConfigureLine(ring);
            rings.Add(ring);
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        bool finished = true;

        for (int i = 0; i < rings.Count; i++)
        {
            float localTime = timer - i * ringDelay;
            LineRenderer ring = rings[i];
            if (localTime < 0f)
            {
                ring.enabled = false;
                finished = false;
                continue;
            }

            ring.enabled = true;
            float progress = Mathf.Clamp01(localTime / duration);
            DrawRing(ring, maxRadius * progress);

            Color color = rippleColor;
            color.a *= Mathf.Pow(1f - progress, 1.25f);
            ring.startColor = color;
            ring.endColor = color;
            if (progress < 1f)
                finished = false;
        }

        if (finished)
            Destroy(gameObject);
    }

    private void ConfigureLine(LineRenderer line)
    {
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.alignment = LineAlignment.View;
        line.sharedMaterial = runtimeRippleMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static Material CreateTransparentRippleMaterial(Material fallback)
    {
        Material material;
        if (fallback != null)
        {
            material = new Material(fallback);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;
            material = new Material(shader);
        }

        material.name = "Runtime Sonar Ripple";
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private void DrawRing(LineRenderer line, float radius)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius));
        }
    }

    private void OnDestroy()
    {
        if (runtimeRippleMaterial != null)
            Destroy(runtimeRippleMaterial);
    }
}
