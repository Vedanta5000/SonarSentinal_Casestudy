
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask groundLayer;

    [Header("Movement")]
    [SerializeField] private float sampleRadius = 2f;

    private NavMeshAgent agent;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (TryGetPointerPosition(out Vector2 screenPosition) && !PointerIsOverHud(screenPosition))
        {
            MoveToPosition(screenPosition);
        }
    }

    private bool PointerIsOverHud(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointer, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
        {
            if (result.gameObject.GetComponentInParent<HUDController>() != null)
                return true;
        }

        return false;
    }

    bool TryGetPointerPosition(out Vector2 position)
    {
        // Android touch
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        // Editor mouse click
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }

    void MoveToPosition(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundLayer))
        {
            NavMeshHit navHit;

            if (NavMesh.SamplePosition(
                hit.point,
                out navHit,
                sampleRadius,
                agent.areaMask))
            {
                agent.SetDestination(navHit.position);

                Debug.Log("Player moving to: " + navHit.position);
            }
        }
    }
}
