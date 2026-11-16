
using UnityEngine;
using UnityEngine.AI;

public class DroneVision : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private DroneAI droneAI;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private LayerMask obstructionMask;

    [Header("Vision")]
    [SerializeField] private float viewDistance = 8f;
    [SerializeField] private float viewAngle = 180f;

    [Header("Detection")]
    [SerializeField] private float detectionTime = 0.5f;

    private float detectionTimer;
    private bool playerCaught;

    // Read-only access for visuals / HUD.
    public bool PlayerCaught => playerCaught;
    public float DetectionProgress =>
        detectionTime > 0f ? Mathf.Clamp01(detectionTimer / detectionTime) : 0f;

    void Awake()
    {
        if (droneAI == null)
            droneAI = GetComponent<DroneAI>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (playerCaught || player == null)
            return;

        if (CanSeePlayer())
        {
            detectionTimer += Time.deltaTime;

            Debug.DrawLine(
                transform.position + Vector3.up * 0.5f,
                player.position + Vector3.up * 0.8f,
                Color.green
            );

            Debug.Log("PLAYER VISIBLE! Detection: " +
                detectionTimer.ToString("F2"));

            if (detectionTimer >= detectionTime)
                CatchPlayer();
        }
        else
        {
            detectionTimer = 0f;
        }
    }

    bool CanSeePlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 target = player.position + Vector3.up * 0.8f;

        Vector3 direction = target - origin;
        float distance = direction.magnitude;

        if (distance > viewDistance)
        {
            Debug.DrawLine(origin, target, Color.gray);
            return false;
        }

        float angle = Vector3.Angle(
            transform.forward,
            direction
        );

        if (angle > viewAngle / 2f)
        {
            Debug.DrawLine(origin, target, Color.yellow);
            return false;
        }

        if (Physics.Raycast(
            origin,
            direction.normalized,
            out RaycastHit hit,
            distance,
            obstructionMask))
        {
            Debug.DrawLine(origin, hit.point, Color.red);

            Debug.Log("VISION BLOCKED BY: " +
                hit.collider.name);

            return false;
        }

        Debug.DrawLine(origin, target, Color.green);
        return true;
    }

    void CatchPlayer()
    {
        playerCaught = true;

        Debug.Log("PLAYER CAUGHT! GAME OVER!");

        if (droneAI != null)
            droneAI.enabled = false;

        if (agent != null)
            agent.isStopped = true;

        PlayerController controller =
            player.GetComponent<PlayerController>();

        if (controller != null)
            controller.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 left = Quaternion.Euler(
            0, -viewAngle / 2f, 0
        ) * transform.forward;

        Vector3 right = Quaternion.Euler(
            0, viewAngle / 2f, 0
        ) * transform.forward;

        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);
    }
}
