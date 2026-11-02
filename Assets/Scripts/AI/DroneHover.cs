
using UnityEngine;

public class DroneHover : MonoBehaviour
{
    [Header("Hover Settings")]
    [SerializeField] private float hoverHeight = 0.15f;
    [SerializeField] private float hoverSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] private float tiltAmount = 3f;
    [SerializeField] private float tiltSpeed = 1.5f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.localPosition;
    }

    void Update()
    {
        float hoverOffset =
            Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;

        transform.localPosition =
            startPosition + Vector3.up * hoverOffset;

        float tilt =
            Mathf.Sin(Time.time * tiltSpeed) * tiltAmount;

        transform.localRotation =
            Quaternion.Euler(0f, 0f, tilt);
    }
}
