using UnityEngine;

/// <summary>Landscape mobile camera framing for the stealth room.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class IsometricCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 positionOffset = new Vector3(22f, 30f, -22f);
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField, Min(0f)] private float smoothTime = 0.12f;

    private Vector3 followVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 focus = target.position + focusOffset;
        Vector3 destination = focus + positionOffset;
        transform.position = smoothTime <= 0f
            ? destination
            : Vector3.SmoothDamp(transform.position, destination, ref followVelocity, smoothTime);
        transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
    }
}
