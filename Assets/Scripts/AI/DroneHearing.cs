
using UnityEngine;

public class DroneHearing : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DroneAI droneAI;

    [Header("Hearing Settings")]
    [SerializeField] private float hearingMultiplier = 1f;

    private void Awake()
    {
        if (droneAI == null)
            droneAI = GetComponent<DroneAI>();
    }

    private void OnEnable()
    {
        SoundSystem.OnSoundEmitted += OnSoundReceived;
    }

    private void OnDisable()
    {
        SoundSystem.OnSoundEmitted -= OnSoundReceived;
    }

    private void OnSoundReceived(Vector3 soundPosition, float soundRadius)
    {
        if (droneAI == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            soundPosition
        );

        float effectiveRadius = soundRadius * hearingMultiplier;

        if (distance <= effectiveRadius)
        {
            Debug.Log(
                "Drone heard sound! Distance: " + distance
            );

            droneAI.HearSound(soundPosition);
        }
    }
}
