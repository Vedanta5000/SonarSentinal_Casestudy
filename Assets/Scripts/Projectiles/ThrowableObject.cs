
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableObject : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private float soundRadius = 12f;

    [Header("Visual Effect")]
    [SerializeField] private GameObject soundRipplePrefab;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 15f;

    private bool soundAlreadyEmitted = false;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (soundAlreadyEmitted)
            return;

        soundAlreadyEmitted = true;

        Vector3 impactPosition = transform.position;

        if (collision.contactCount > 0)
        {
            impactPosition = collision.GetContact(0).point;
        }

        Debug.Log("Projectile impact: " + collision.gameObject.name);

        // Generate visual ripple
        if (soundRipplePrefab != null)
        {
            Vector3 ripplePosition = impactPosition +
                                     Vector3.up * 0.05f;

            Instantiate(
                soundRipplePrefab,
                ripplePosition,
                Quaternion.identity
            );
        }

        // Notify the sound system
        SoundSystem.EmitSound(
            impactPosition,
            soundRadius
        );
    }
}
