
using UnityEngine;
using UnityEngine.InputSystem;

public class ThrowController : MonoBehaviour
{
    [Header("References")]
    public GameObject projectilePrefab;
    public Transform throwOrigin;
    public Camera mainCamera;

    [Header("Throw Settings")]
    public float throwForce = 12f;
    public float upwardForce = 3f;

    private Vector2 startPosition;
    private bool isAiming;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (Mouse.current == null)
            return;

        // Start aiming with right mouse button
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            startPosition = Mouse.current.position.ReadValue();
            isAiming = true;
        }

        // Release to throw
        if (Mouse.current.rightButton.wasReleasedThisFrame && isAiming)
        {
            Vector2 endPosition =
                Mouse.current.position.ReadValue();

            Vector2 swipe = endPosition - startPosition;

            if (swipe.magnitude > 20f)
                ThrowObject(swipe);

            isAiming = false;
        }
    }

    void ThrowObject(Vector2 swipe)
    {
        GameObject orb = Instantiate(
            projectilePrefab,
            throwOrigin.position,
            Quaternion.identity
        );

        Rigidbody rb = orb.GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Projectile needs a Rigidbody!");
            Destroy(orb);
            return;
        }

        // Convert swipe direction into world direction
        Vector3 direction =
            mainCamera.transform.right * swipe.x +
            mainCamera.transform.up * swipe.y;

        // Remove vertical camera component
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        Vector3 launchVelocity =
            direction * throwForce +
            Vector3.up * upwardForce;

        rb.linearVelocity = launchVelocity;

        Debug.Log("Projectile thrown!");
    }
}
