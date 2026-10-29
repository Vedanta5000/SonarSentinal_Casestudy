using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Swipe-to-throw controller for Sonar Sentinel.
/// Attach to the player or another active scene object.
/// </summary>
public class ThrowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Rigidbody projectilePrefab;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Swipe")]
    [SerializeField, Min(0f)]
    private float minimumSwipePixels = 35f;

    [SerializeField, Min(1f)]
    private float maximumSwipePixels = 350f;

    [Header("Throw")]
    [SerializeField, Min(0f)]
    private float minimumThrowSpeed = 5f;

    [SerializeField, Min(0f)]
    private float maximumThrowSpeed = 16f;

    [SerializeField]
    private float upwardSpeed = 1.5f;

    [SerializeField, Min(1f)]
    private float groundRayDistance = 250f;

    private bool isTrackingSwipe;
    private Vector2 swipeStartScreen;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (throwOrigin == null)
            throwOrigin = transform;

        if (aimCamera == null)
        {
            Debug.LogError(
                "ThrowController: Assign an aim camera or tag the camera MainCamera.",
                this
            );
        }

        if (projectilePrefab == null)
        {
            Debug.LogError(
                "ThrowController: Assign a Rigidbody projectile prefab.",
                this
            );
        }
    }

    private void Update()
    {
        if (aimCamera == null || projectilePrefab == null)
            return;

        if (TryGetPointerDown(out Vector2 pointerDown))
        {
            isTrackingSwipe = true;
            swipeStartScreen = pointerDown;
        }

        if (!isTrackingSwipe)
            return;

        if (TryGetPointerUp(out Vector2 pointerUp))
        {
            isTrackingSwipe = false;
            TryThrow(swipeStartScreen, pointerUp);
        }
    }

    private void TryThrow(Vector2 startScreen, Vector2 endScreen)
    {
        float swipePixels = Vector2.Distance(
            startScreen,
            endScreen
        );

        if (swipePixels < minimumSwipePixels)
            return;

        if (!TryGetGroundPoint(startScreen, out Vector3 startWorld))
            return;

        if (!TryGetGroundPoint(endScreen, out Vector3 endWorld))
            return;

        Vector3 direction = endWorld - startWorld;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        float maxSwipe = Mathf.Max(
            maximumSwipePixels,
            minimumSwipePixels + 1f
        );

        float strength = Mathf.InverseLerp(
            minimumSwipePixels,
            maxSwipe,
            swipePixels
        );

        float throwSpeed = Mathf.Lerp(
            minimumThrowSpeed,
            maximumThrowSpeed,
            strength
        );

        Rigidbody projectile = Instantiate(
            projectilePrefab,
            throwOrigin.position,
            Quaternion.identity
        );

        Vector3 launchVelocity =
            direction * throwSpeed +
            Vector3.up * upwardSpeed;

        projectile.AddForce(
            launchVelocity,
            ForceMode.VelocityChange
        );
    }
    void ExecuteThrow()
    {
        if (projectile == null) return;

        Vector2 swipeVector = endTouchPos - startTouchPos;

        Vector3 throwForce = new Vector3(
            swipeVector.x * throwPower, 
            swipeVector.magnitude * throwPower * upwardArcMultiplier, 
            swipeVector.y * throwPower
        );

        projectile.isKinematic = false;
        
        // ADD THIS LINE: Unlock the sound right before applying the physics force
        projectile.GetComponent<DistractionProjectile>().isThrown = true; 

        projectile.AddForce(throwForce, ForceMode.Impulse);
        
        projectile = null; // Prevents throwing the same object twice
    }
    
    private bool TryGetGroundPoint(
        Vector2 screenPosition,
        out Vector3 worldPoint
    )
    {
        Ray ray = aimCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            groundRayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            worldPoint = hit.point;
            return true;
        }

        worldPoint = default;
        return false;
    }

    private bool TryGetPointerDown(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position =
                Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif

        position = default;
        return false;
    }

    private bool TryGetPointerUp(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            position =
                Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasReleasedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonUp(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif

        position = default;
        return false;
    }

    private void OnDisable()
    {
        isTrackingSwipe = false;
    }
}
