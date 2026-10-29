using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic sound-reactive drone AI for Sonar Sentinel.
///
/// State order from the GDD:
/// Patrol -> Alert -> InvestigateNoise -> Search -> Resume -> Patrol.
///
/// This script now actively listens to the global SonarNoiseBus 
/// triggered by the player's DistractionProjectile.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SonarSentinelDroneAI : MonoBehaviour
{
    // These are the five drone behaviours listed in the GDD.
    public enum DroneState
    {
        Patrol,
        Alert,
        InvestigateNoise,
        Search,
        Resume
    }

    [Header("Patrol Area")]
    [Tooltip("Optional collider marking the area the drone should stay inside. A trigger BoxCollider is a simple choice.")]
    [SerializeField] private Collider patrolArea;

    [Tooltip("Optional patrol route. Add empty GameObjects in route order. Leave empty for random patrol points.")]
    [SerializeField] private Transform[] patrolWaypoints;

    [Tooltip("Used only when Patrol Area is empty: patrol radius around the drone's starting position.")]
    [SerializeField, Min(0.5f)] private float fallbackPatrolRadius = 5f;

    [Tooltip("Pause at each patrol point for this many seconds.")]
    [SerializeField, Min(0f)] private float patrolPauseDuration = 1f;

    [Tooltip("Distance Unity can search to find a nearby walkable NavMesh position.")]
    [SerializeField, Min(0.1f)] private float navMeshSampleDistance = 2f;

    [Header("Sound Response")]
    [Tooltip("How long the drone pauses when it first hears a sound.")]
    [SerializeField, Min(0f)] private float alertDuration = 0.6f;

    [Tooltip("Maximum time the drone spends searching around a sound.")]
    [SerializeField, Min(0.1f)] private float searchDuration = 4f;

    [Tooltip("Radius around the sound location used for search points.")]
    [SerializeField, Min(0f)] private float searchRadius = 2f;

    [Tooltip("Number of nearby points the drone tries during its search.")]
    [SerializeField, Min(0)] private int searchPointCount = 3;

    [Header("Testing - Optional")]
    [Tooltip("Optional Empty GameObject for testing a sound at a chosen location.")]
    [SerializeField] private Transform testNoisePoint;

    [Tooltip("Hearing radius used by the TEST: Emit Noise command.")]
    [SerializeField, Min(0.1f)] private float testNoiseRadius = 8f;

    [Header("Debug")]
    // Displayed in the Inspector during Play mode so you can see the current state.
    [SerializeField] private DroneState currentState = DroneState.Patrol;

    private NavMeshAgent agent;

    // Used as the centre of the fallback patrol circle when no collider is assigned.
    private Vector3 patrolCenter;

    // The most recent sound this drone heard.
    private Vector3 noiseTarget;
    private float lastNoiseRadius;
    private bool hasHeardNoise;

    // Save the current patrol target so the drone can return to it after investigating.
    private Vector3 patrolTarget;
    private bool hasPatrolTarget;

    // Basic state and movement tracking.
    private float stateTimer;
    private float patrolPauseTimer;
    private int nextWaypointIndex;
    private int searchPointsVisited;
    private bool hasDestination;
    private bool waitingAtPatrolPoint;

    private void OnEnable()
    {
        // Connect Anuraag's AI directly to YOUR global projectile bus
        SonarNoiseBus.NoiseCreated += OnNoiseEmitted;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks when the drone is destroyed
        SonarNoiseBus.NoiseCreated -= OnNoiseEmitted;
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolCenter = transform.position;

        // A NavMeshAgent cannot move until the level has a baked NavMesh under it.
        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Drone AI: The drone is not on a NavMesh. Bake a NavMesh and place the drone on it.", this);
            enabled = false;
            return;
        }

        currentState = DroneState.Patrol;

        // Find the first destination. If setup is not ready yet, Update will try again.
        if (!ChoosePatrolDestination())
        {
            Debug.LogWarning("Drone AI: No patrol point found. Check the patrol area and NavMesh.", this);
        }
    }

    private void Update()
    {
        // Skip movement calls if the agent is missing or temporarily off the NavMesh.
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        stateTimer += Time.deltaTime;

        // Keep each state simple: it checks one condition and then moves to the next state.
        switch (currentState)
        {
            case DroneState.Patrol:
                UpdatePatrol();
                break;

            case DroneState.Alert:
                if (stateTimer >= alertDuration)
                {
                    ChangeState(DroneState.InvestigateNoise);
                }
                break;

            case DroneState.InvestigateNoise:
                if (!hasDestination)
                {
                    TryMoveTo(noiseTarget, out _); // Retry if a valid path was not found earlier.
                }
                else if (HasReachedDestination())
                {
                    ChangeState(DroneState.Search);
                }
                break;

            case DroneState.Search:
                UpdateSearch();
                break;

            case DroneState.Resume:
                UpdateResume();
                break;
        }
    }

    // ------------------------------------------------------------------
    // PATROL: follow waypoints, or wander randomly inside the patrol area.
    // ------------------------------------------------------------------
    private void UpdatePatrol()
    {
        if (!hasDestination)
        {
            ChoosePatrolDestination();
            return;
        }

        // Pause briefly at a destination before moving to the next point.
        if (waitingAtPatrolPoint)
        {
            patrolPauseTimer += Time.deltaTime;

            if (patrolPauseTimer >= patrolPauseDuration)
            {
                waitingAtPatrolPoint = false;
                patrolPauseTimer = 0f;
                ChoosePatrolDestination();
            }

            return;
        }

        if (HasReachedDestination())
        {
            agent.isStopped = true;
            waitingAtPatrolPoint = true;
            patrolPauseTimer = 0f;
        }
    }

    // ------------------------------------------------------------------
    // SEARCH: visit a few random points near the last sound, then resume.
    // ------------------------------------------------------------------
    private void UpdateSearch()
    {
        if (stateTimer >= searchDuration)
        {
            ChangeState(DroneState.Resume);
            return;
        }

        if (!hasDestination || HasReachedDestination())
        {
            if (searchPointsVisited < searchPointCount)
            {
                ChooseSearchPoint();
            }
            else
            {
                // Stay still for the rest of the search time if there are no points left.
                agent.isStopped = true;
                hasDestination = false;
            }
        }
    }

    // ------------------------------------------------------------------
    // RESUME: return to the previous patrol target, or choose another one.
    // ------------------------------------------------------------------
    private void UpdateResume()
    {
        if (!hasDestination)
        {
            ChoosePatrolDestination();
            return;
        }

        if (HasReachedDestination())
        {
            waitingAtPatrolPoint = false;
            patrolPauseTimer = 0f;
            ChangeState(DroneState.Patrol);
        }
    }

    // Set up the destination and timer for the new state.
    private void ChangeState(DroneState newState)
    {
        currentState = newState;
        stateTimer = 0f;

        switch (newState)
        {
            case DroneState.Patrol:
                agent.isStopped = false;
                break;

            case DroneState.Alert:
                // Stop briefly, but keep patrolTarget so it can resume later.
                agent.isStopped = true;
                hasDestination = false;
                waitingAtPatrolPoint = false;
                patrolPauseTimer = 0f;
                break;

            case DroneState.InvestigateNoise:
                TryMoveTo(noiseTarget, out _);
                break;

            case DroneState.Search:
                searchPointsVisited = 0;
                ChooseSearchPoint();
                break;

            case DroneState.Resume:
                // Try to go back to the patrol target saved before the sound was heard.
                if (hasPatrolTarget && TryMoveTo(patrolTarget, out Vector3 returnPoint))
                {
                    patrolTarget = returnPoint;
                }
                else
                {
                    ChoosePatrolDestination();
                }
                break;
        }
    }

    // ------------------------------------------------------------------
    // SOUND: Core logic updated to use SonarNoiseBus
    // ------------------------------------------------------------------

    /// <summary>
    /// Broadcast a sound to every active drone via the central bus.
    /// </summary>
    public static void EmitNoise(Vector3 soundPosition, float hearingRadius)
    {
        if (hearingRadius > 0f)
        {
            // Route inspector tests through your event bus to ensure consistency
            SonarNoiseBus.RaiseNoise(soundPosition, hearingRadius);
        }
    }

    private void OnNoiseEmitted(Vector3 soundPosition, float hearingRadius)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        // Only drones inside the sound's radius react to it.
        if (Vector3.Distance(transform.position, soundPosition) > hearingRadius)
        {
            return;
        }

        // Keep the investigation target inside this drone's patrol space.
        noiseTarget = ClampToPatrolSpace(soundPosition);
        lastNoiseRadius = hearingRadius;
        hasHeardNoise = true;

        // New sounds interrupt the current behaviour and restart the response sequence.
        ChangeState(DroneState.Alert);
    }

    // ------------------------------------------------------------------
    // DESTINATIONS: choose patrol/search points and put them on the NavMesh.
    // ------------------------------------------------------------------

    private bool ChoosePatrolDestination()
    {
        // If waypoints are assigned, visit them in the order shown in the Inspector.
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                Transform waypoint = patrolWaypoints[nextWaypointIndex];
                nextWaypointIndex = (nextWaypointIndex + 1) % patrolWaypoints.Length;

                if (waypoint != null && TryMoveTo(waypoint.position, out Vector3 waypointPosition))
                {
                    patrolTarget = waypointPosition;
                    hasPatrolTarget = true;
                    waitingAtPatrolPoint = false;
                    return true;
                }
            }
        }

        // If no usable waypoints exist, choose a random point within the patrol space.
        for (int i = 0; i < 20; i++)
        {
            if (TryGetRandomPatrolPoint(out Vector3 randomPoint) && TryMoveTo(randomPoint, out Vector3 destination))
            {
                patrolTarget = destination;
                hasPatrolTarget = true;
                waitingAtPatrolPoint = false;
                return true;
            }
        }

        hasDestination = false;
        return false;
    }

    private bool TryGetRandomPatrolPoint(out Vector3 point)
    {
        point = patrolCenter;

        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate;

            if (patrolArea != null)
            {
                // Pick a random horizontal point inside the collider's bounds.
                Bounds bounds = patrolArea.bounds;
                candidate = new Vector3(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    patrolCenter.y,
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z));

                // The bounds may include corners outside a non-box collider, so skip those.
                if (!IsInsidePatrolSpace(candidate))
                {
                    continue;
                }
            }
            else
            {
                // Fallback for a quick test when no patrol-area collider is assigned.
                Vector2 offset = UnityEngine.Random.insideUnitCircle * fallbackPatrolRadius;
                candidate = new Vector3(patrolCenter.x + offset.x, patrolCenter.y, patrolCenter.z + offset.y);
            }

            if (TryFindNavMeshPoint(candidate, out point))
            {
                return true;
            }
        }

        return false;
    }

    private void ChooseSearchPoint()
    {
        if (searchPointsVisited >= searchPointCount)
        {
            agent.isStopped = true;
            hasDestination = false;
            return;
        }

        // Count attempts so invalid level geometry cannot make the drone loop forever.
        searchPointsVisited++;

        // Try several nearby points; failed attempts are retried on the next Update.
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * searchRadius;
            Vector3 candidate = noiseTarget + new Vector3(offset.x, 0f, offset.y);
            candidate = ClampToPatrolSpace(candidate);

            if (TryMoveTo(candidate, out _))
            {
                return;
            }
        }

        hasDestination = false;
    }

    private bool TryMoveTo(Vector3 requestedPoint, out Vector3 destination)
    {
        destination = transform.position;

        if (!TryFindNavMeshPoint(requestedPoint, out destination))
        {
            hasDestination = false;
            return false;
        }

        agent.isStopped = false;
        hasDestination = agent.SetDestination(destination);
        return hasDestination;
    }

    private bool TryFindNavMeshPoint(Vector3 requestedPoint, out Vector3 navMeshPoint)
    {
        navMeshPoint = transform.position;
        Vector3 constrainedPoint = ClampToPatrolSpace(requestedPoint);

        // SamplePosition moves the target onto a nearby walkable NavMesh location.
        if (NavMesh.SamplePosition(constrainedPoint, out NavMeshHit hit, navMeshSampleDistance, agent.areaMask)
            && IsInsidePatrolSpace(hit.position))
        {
            navMeshPoint = hit.position;
            return true;
        }

        return false;
    }

    private bool HasReachedDestination()
    {
        if (!hasDestination || agent.pathPending)
        {
            return false;
        }

        // Treat an invalid path as finished so the drone can choose another destination.
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return true;
        }

        return agent.remainingDistance <= agent.stoppingDistance + 0.15f;
    }

    // ------------------------------------------------------------------
    // BOUNDARY: keep random points and sound targets inside the patrol space.
    // ------------------------------------------------------------------

    private Vector3 ClampToPatrolSpace(Vector3 point)
    {
        if (patrolArea != null)
        {
            // ClosestPoint leaves points inside the collider where they are and clamps outside points.
            return patrolArea.ClosestPoint(point);
        }

        // Without a collider, keep the drone within a circle around its starting position.
        Vector2 offset = new Vector2(point.x - patrolCenter.x, point.z - patrolCenter.z);
        float radius = Mathf.Max(0.5f, fallbackPatrolRadius);

        if (offset.magnitude > radius)
        {
            offset = offset.normalized * radius;
        }

        return new Vector3(patrolCenter.x + offset.x, patrolCenter.y, patrolCenter.z + offset.y);
    }

    private bool IsInsidePatrolSpace(Vector3 point)
    {
        if (patrolArea != null)
        {
            // A small tolerance accepts points that lie right on the collider surface.
            Vector3 closest = patrolArea.ClosestPoint(point);
            return (closest - point).sqrMagnitude <= 0.01f;
        }

        Vector2 offset = new Vector2(point.x - patrolCenter.x, point.z - patrolCenter.z);
        float radius = Mathf.Max(0.5f, fallbackPatrolRadius);
        return offset.sqrMagnitude <= (radius * radius) + 0.1f;
    }

    // ------------------------------------------------------------------
    // TESTING: run this from the component's Inspector context menu.
    // ------------------------------------------------------------------

    [ContextMenu("TEST: Emit Noise")]
    private void TestEmitNoise()
    {
        // If no test point is assigned, emit a test sound a short distance in front of the drone.
        Vector3 position = testNoisePoint != null
            ? testNoisePoint.position
            : transform.position + transform.forward * 3f;

        EmitNoise(position, testNoiseRadius);
        Debug.Log($"Test noise emitted at {position} with radius {testNoiseRadius}.", this);
    }

    private void OnDrawGizmosSelected()
    {
        // Show the patrol boundary and the most recent sound radius in the Scene view.
        if (patrolArea != null)
        {
            Gizmos.DrawWireCube(patrolArea.bounds.center, patrolArea.bounds.size);
        }

        if (Application.isPlaying && hasHeardNoise)
        {
            Gizmos.DrawWireSphere(noiseTarget, lastNoiseRadius);
        }
    }
}
