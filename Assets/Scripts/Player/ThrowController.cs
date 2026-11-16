using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ThrowController : MonoBehaviour
{
    [Header("References")]
    public GameObject projectilePrefab;
    public Transform throwOrigin;
    public Camera mainCamera;
    [SerializeField] private RectTransform mobileThrowButton;

    [Header("Throw Settings")]
    public float throwForce = 12f;
    public float upwardForce = 3f;
    [SerializeField] private float minimumSwipeDistance = 24f;

    [Header("Ball path feedback")]
    [SerializeField, Min(8)] private int trajectorySegments = 28;
    [SerializeField, Min(0.25f)] private float trajectoryDuration = 1.4f;
    [SerializeField] private Color trajectoryColor = new Color(0.14f, 0.9f, 1f, 0.9f);
    [SerializeField, Min(0.01f)] private float flightTrailDuration = 1.2f;

    private Vector2 startPosition;
    private bool isAiming;
    private LineRenderer trajectoryLine;
    private Material trajectoryMaterial;
    private readonly RaycastHit[] trajectoryHits = new RaycastHit[16];

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        CreateTrajectoryRenderer();
    }

    private void Update()
    {
        HandleTouchInput();
        HandleMouseInput();
    }

    private void HandleTouchInput()
    {
        if (Touchscreen.current == null || mobileThrowButton == null)
            return;

        var touch = Touchscreen.current.primaryTouch;
        Vector2 touchPosition = touch.position.ReadValue();

        if (touch.press.wasPressedThisFrame &&
            RectTransformUtility.RectangleContainsScreenPoint(mobileThrowButton, touchPosition, null))
        {
            startPosition = touchPosition;
            isAiming = true;
        }

        if (isAiming && touch.press.isPressed)
            UpdateTrajectory(touchPosition - startPosition);

        if (touch.press.wasReleasedThisFrame && isAiming)
            FinishThrow(touchPosition);
    }

    private void HandleMouseInput()
    {
        if (Mouse.current == null)
            return;

        // Desktop testing keeps the right-drag control.
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            startPosition = Mouse.current.position.ReadValue();
            isAiming = true;
        }

        if (isAiming && Mouse.current.rightButton.isPressed)
            UpdateTrajectory(Mouse.current.position.ReadValue() - startPosition);

        if (Mouse.current.rightButton.wasReleasedThisFrame && isAiming)
            FinishThrow(Mouse.current.position.ReadValue());
    }

    private void FinishThrow(Vector2 endPosition)
    {
        Vector2 swipe = endPosition - startPosition;
        isAiming = false;
        if (trajectoryLine != null)
            trajectoryLine.enabled = false;

        if (swipe.magnitude >= minimumSwipeDistance)
            ThrowObject(swipe);
    }

    private Vector3 CalculateLaunchVelocity(Vector2 swipe)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return Vector3.zero;

        Vector3 direction = mainCamera.transform.right * swipe.x + mainCamera.transform.up * swipe.y;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return Vector3.zero;

        direction.Normalize();
        return direction * throwForce + Vector3.up * upwardForce;
    }

    private void UpdateTrajectory(Vector2 swipe)
    {
        if (trajectoryLine == null || projectilePrefab == null || throwOrigin == null ||
            swipe.magnitude < minimumSwipeDistance)
        {
            if (trajectoryLine != null)
                trajectoryLine.enabled = false;
            return;
        }

        Vector3 velocity = CalculateLaunchVelocity(swipe);
        if (velocity.sqrMagnitude < 0.001f)
        {
            trajectoryLine.enabled = false;
            return;
        }

        Rigidbody projectileBody = projectilePrefab.GetComponent<Rigidbody>();
        Vector3 acceleration = projectileBody != null && projectileBody.useGravity
            ? Physics.gravity
            : Vector3.zero;
        float projectileRadius = GetProjectileRadius();
        Vector3 previous = throwOrigin.position;
        int usedPoints = 1;
        trajectoryLine.positionCount = trajectorySegments;
        trajectoryLine.SetPosition(0, previous);

        for (int i = 1; i < trajectorySegments; i++)
        {
            float time = trajectoryDuration * i / (trajectorySegments - 1f);
            Vector3 next = throwOrigin.position + velocity * time + 0.5f * acceleration * time * time;
            Vector3 segment = next - previous;
            float distance = segment.magnitude;

            if (distance > 0.0001f && TryFindTrajectoryHit(previous, segment / distance,
                    distance, projectileRadius, out RaycastHit hit))
            {
                trajectoryLine.SetPosition(usedPoints++, hit.point);
                break;
            }

            trajectoryLine.SetPosition(usedPoints++, next);
            previous = next;
        }

        trajectoryLine.positionCount = usedPoints;
        trajectoryLine.enabled = usedPoints > 1;
    }

    private bool TryFindTrajectoryHit(Vector3 origin, Vector3 direction, float distance,
        float radius, out RaycastHit closestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(origin, radius, direction, trajectoryHits,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        closestHit = default;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = trajectoryHits[i].collider;
            if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
                continue;

            if (trajectoryHits[i].distance < closestDistance)
            {
                closestDistance = trajectoryHits[i].distance;
                closestHit = trajectoryHits[i];
                found = true;
            }
        }

        return found;
    }

    private float GetProjectileRadius()
    {
        if (projectilePrefab == null)
            return 0.08f;

        SphereCollider sphere = projectilePrefab.GetComponent<SphereCollider>();
        if (sphere == null)
            return 0.08f;

        Vector3 scale = projectilePrefab.transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(0.04f, sphere.radius * largestScale);
    }

    private void CreateTrajectoryRenderer()
    {
        GameObject guide = new GameObject("ThrowTrajectoryPreview");
        guide.transform.SetParent(transform, false);
        trajectoryLine = guide.AddComponent<LineRenderer>();
        trajectoryLine.useWorldSpace = true;
        trajectoryLine.loop = false;
        trajectoryLine.positionCount = 0;
        trajectoryLine.startWidth = 0.055f;
        trajectoryLine.endWidth = 0.025f;
        trajectoryLine.numCornerVertices = 3;
        trajectoryLine.numCapVertices = 4;
        trajectoryLine.shadowCastingMode = ShadowCastingMode.Off;
        trajectoryLine.receiveShadows = false;
        trajectoryLine.enabled = false;

        trajectoryMaterial = CreateTransparentLineMaterial();
        trajectoryLine.sharedMaterial = trajectoryMaterial;
        trajectoryLine.startColor = trajectoryColor;
        Color endColor = trajectoryColor;
        endColor.a = 0.25f;
        trajectoryLine.endColor = endColor;
    }

    private void ThrowObject(Vector2 swipe)
    {
        if (projectilePrefab == null || throwOrigin == null)
            return;

        Vector3 launchVelocity = CalculateLaunchVelocity(swipe);
        if (launchVelocity.sqrMagnitude < 0.001f)
            return;

        GameObject orb = Instantiate(projectilePrefab, throwOrigin.position, Quaternion.identity);
        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Projectile needs a Rigidbody!");
            Destroy(orb);
            return;
        }

        rb.linearVelocity = launchVelocity;
        AddFlightTrail(orb);
        Debug.Log("Projectile thrown!");
    }

    private void AddFlightTrail(GameObject orb)
    {
        TrailRenderer trail = orb.GetComponent<TrailRenderer>();
        if (trail == null)
            trail = orb.AddComponent<TrailRenderer>();

        trail.time = flightTrailDuration;
        trail.startWidth = 0.12f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = trajectoryMaterial;
        trail.startColor = trajectoryColor;
        Color faded = trajectoryColor;
        faded.a = 0f;
        trail.endColor = faded;
        trail.emitting = true;
        trail.autodestruct = true;
    }

    private static Material CreateTransparentLineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader) { name = "Runtime Throw Path" };
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private void OnDestroy()
    {
        if (trajectoryMaterial != null)
            Destroy(trajectoryMaterial);
    }
}
