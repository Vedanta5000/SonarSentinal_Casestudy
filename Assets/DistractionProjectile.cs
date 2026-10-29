using System;
using UnityEngine;

/// <summary>
/// Shared sound event for Sonar Sentinel.
/// Drone AI can subscribe to this event.
/// </summary>
public static class SonarNoiseBus
{
    public static event Action<Vector3, float> NoiseCreated;

    public static void RaiseNoise(
        Vector3 position,
        float radius
    )
    {
        NoiseCreated?.Invoke(position, radius);
    }
}
// Add this variable at the top with your other settings
    public bool isThrown = false; 

    private void OnCollisionEnter(Collision collision)
    {
        // Add this guard clause: if it hasn't been thrown, ignore all collisions
        if (!isThrown) return; 

        if (hasEmittedNoise)
            return;

        Debug.Log("PROJECTILE HIT! Sending coordinates to the Drone.");    
        hasEmittedNoise = true;

        SonarNoiseBus.RaiseNoise(transform.position, noiseRadius);
    }
/// <summary>
/// Attach this component to the thrown distraction-object prefab.
/// Emits one sound event on its first collision.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DistractionProjectile : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField, Min(0f)]
    private float noiseRadius = 8f;

    [Header("Lifetime")]
    [SerializeField, Min(0f)]
    private float lifetimeSeconds = 12f;

    private bool hasEmittedNoise;

    private void Start()
    {
        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasEmittedNoise)
            return;
        Debug.Log("PROJECTILE HIT! Sending coordinates to the Drone.");    

        hasEmittedNoise = true;

        SonarNoiseBus.RaiseNoise(
            transform.position,
            noiseRadius
        );
    }
}
