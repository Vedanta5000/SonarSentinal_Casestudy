
using System;
using UnityEngine;

public static class SoundSystem
{
    // Other scripts can subscribe to this event.
    public static event Action<Vector3, float> OnSoundEmitted;

    // Called whenever an object generates a sound.
    public static void EmitSound(Vector3 position, float radius)
    {
        if (radius <= 0f)
            return;

        Debug.Log(
            $"Sound emitted at {position}, Radius: {radius}"
        );

        OnSoundEmitted?.Invoke(position, radius);
    }
}
