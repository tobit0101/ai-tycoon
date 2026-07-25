using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCWander : MonoBehaviour
{
    public enum WanderState
    {
        Idle,
        Walking
    }

    [Header("Wander Settings")]
    [Tooltip("The center point of the wander area. Defaults to starting position.")]
    public Vector3 wanderCenter;
    
    [Tooltip("The maximum distance from the center point the NPC can wander.")]
    public float wanderRadius = 15.0f;

    [Tooltip("Minimum time to wait in seconds at each destination before picking a new one.")]
    public float minIdleTime = 2.0f;

    [Tooltip("Maximum time to wait in seconds at each destination before picking a new one.")]
    public float maxIdleTime = 5.0f;

    [Header("Animation Settings")]
    [Tooltip("The animator parameter name for speed.")]
    public string speedParameterName = "Speed";

    [Header("Current Status")]
    [Tooltip("The current state of this NPC, visible in the Inspector.")]
    [SerializeField]
    private WanderState currentState = WanderState.Idle;

    private NavMeshAgent agent;
    private Animator animator;
    private bool isWaiting = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Disable Root Motion so the animation doesn't fight with the NavMeshAgent.
        // This resolves any "wobbling" or jittering behavior during idle or walk.
        animator.applyRootMotion = false;

        // Set starting position as the wander center
        wanderCenter = transform.position;

        // Start wandering
        ChooseNextDestination();
    }

    private void Update()
    {
        // Smoothly feed the agent's current speed into the animator
        // We use agent.velocity.magnitude so the animation matches actual movement speed
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat(speedParameterName, currentSpeed);

        // Update state display for the Inspector
        if (isWaiting || (agent.velocity.magnitude < 0.1f && agent.remainingDistance <= agent.stoppingDistance))
        {
            currentState = WanderState.Idle;
        }
        else
        {
            currentState = WanderState.Walking;
        }

        // Check if the agent has reached its destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!isWaiting)
            {
                StartCoroutine(WaitAndWander());
            }
        }
    }

    private IEnumerator WaitAndWander()
    {
        isWaiting = true;
        currentState = WanderState.Idle;

        // Explicitly stop the NavMeshAgent from trying to move or rotate
        agent.isStopped = true;

        // Choose a random wait time
        float idleTime = Random.Range(minIdleTime, maxIdleTime);
        yield return new WaitForSeconds(idleTime);

        // Resume the NavMeshAgent before moving to the next spot
        agent.isStopped = false;

        // Find and set next random destination on NavMesh
        ChooseNextDestination();

        isWaiting = false;
    }

    private void ChooseNextDestination()
    {
        Vector3 randomPoint = GetRandomPointOnNavMesh(wanderCenter, wanderRadius);
        agent.SetDestination(randomPoint);
        currentState = WanderState.Walking;
    }

    private Vector3 GetRandomPointOnNavMesh(Vector3 center, float radius)
    {
        // Try up to 30 times to find a valid point on the NavMesh
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += center;

            // Project onto NavMesh flat plane (since insideUnitSphere is 3D, y offset is fine as SamplePosition resolves it)
            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        // Fallback: return the center point if no valid point was found
        return center;
    }

    // Draw the wander radius in the editor for visual reference
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? wanderCenter : transform.position, wanderRadius);
    }
}

