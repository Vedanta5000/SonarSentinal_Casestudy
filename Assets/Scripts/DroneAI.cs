
using UnityEngine;
using UnityEngine.AI;

public class DroneAI : MonoBehaviour
{
    public enum DroneState
    {
        Patrol,
        Alert,
        InvestigateNoise,
        Search,
        Resume
    }

    [Header("Patrol")]
    public Transform[] waypoints;

    private int currentWaypoint = 0;

    [Header("AI Settings")]
    public float alertDuration = 0.5f;
    public float arrivalDistance = 0.5f;

    [Header("Search Settings")]
    public float searchRadius = 3f;
    public float searchDuration = 6f;
    public int searchPoints = 3;

    [Header("Navigation")]
    public float sampleRadius = 5f;

    private NavMeshAgent agent;

    private DroneState currentState;

    private Vector3 soundPosition;
    private float alertTimer;
    private float searchTimer;

    private int currentSearchPoint;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogError("Drone NavMeshAgent missing or not on NavMesh!");
            enabled = false;
            return;
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError("No patrol waypoints assigned!");
            enabled = false;
            return;
        }

        currentState = DroneState.Patrol;
        MoveToNextWaypoint();
    }

    void Update()
    {
        switch (currentState)
        {
            case DroneState.Patrol:
                PatrolUpdate();
                break;

            case DroneState.Alert:
                AlertUpdate();
                break;

            case DroneState.InvestigateNoise:
                InvestigateUpdate();
                break;

            case DroneState.Search:
                SearchUpdate();
                break;

            case DroneState.Resume:
                ResumePatrol();
                break;
        }
    }

    // PATROL

    void PatrolUpdate()
    {
        if (!agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            MoveToNextWaypoint();
        }
    }

    void MoveToNextWaypoint()
    {
        agent.isStopped = false;

        agent.SetDestination(
            waypoints[currentWaypoint].position
        );

        currentWaypoint++;

        if (currentWaypoint >= waypoints.Length)
            currentWaypoint = 0;

        Debug.Log("PATROLLING");
    }

    // SOUND DETECTION

    public void HearSound(Vector3 position)
    {
        if (currentState != DroneState.Patrol)
            return;

        NavMeshHit hit;

        if (!NavMesh.SamplePosition(
            position,
            out hit,
            sampleRadius,
            agent.areaMask))
        {
            Debug.LogWarning("Invalid sound destination!");
            return;
        }

        soundPosition = hit.position;

        currentState = DroneState.Alert;
        alertTimer = alertDuration;

        agent.isStopped = true;

        Debug.Log("ALERT!");
    }

    // ALERT

    void AlertUpdate()
    {
        alertTimer -= Time.deltaTime;

        if (alertTimer <= 0f)
        {
            currentState = DroneState.InvestigateNoise;

            agent.isStopped = false;
            agent.SetDestination(soundPosition);

            Debug.Log("INVESTIGATING NOISE");
        }
    }

    // INVESTIGATE

    void InvestigateUpdate()
    {
        if (agent.pathPending)
            return;

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            StartSearch();
            return;
        }

        if (agent.hasPath &&
            agent.remainingDistance <= arrivalDistance)
        {
            StartSearch();
        }
    }

    // SEARCH

    void StartSearch()
    {
        currentState = DroneState.Search;

        searchTimer = searchDuration;
        currentSearchPoint = 0;

        agent.isStopped = false;

        Debug.Log("SEARCHING AREA");

        MoveToSearchPoint();
    }

    void SearchUpdate()
    {
        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0f)
        {
            currentState = DroneState.Resume;
            return;
        }

        if (!agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance <= arrivalDistance)
        {
            if (currentSearchPoint >= searchPoints)
            {
                currentState = DroneState.Resume;
                return;
            }

            MoveToSearchPoint();
        }
    }

    void MoveToSearchPoint()
    {
        Vector2 randomOffset = Random.insideUnitCircle * searchRadius;

        Vector3 randomPosition = soundPosition +
            new Vector3(randomOffset.x, 0f, randomOffset.y);

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            randomPosition,
            out hit,
            sampleRadius,
            agent.areaMask))
        {
            agent.SetDestination(hit.position);
            currentSearchPoint++;

            Debug.Log("Moving to search point");
        }
        else
        {
            currentSearchPoint++;

            if (currentSearchPoint >= searchPoints)
                currentState = DroneState.Resume;
        }
    }

    // RESUME

    void ResumePatrol()
    {
        currentState = DroneState.Patrol;

        Debug.Log("RESUMING PATROL");

        MoveToNextWaypoint();
    }
}
