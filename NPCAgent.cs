using UnityEngine;
using UnityEngine.AI;

public class NPCAgent : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 5f;
    public float stoppingDistance = 0.5f;
    public float arrivalSlowdownTime = 0.5f;

    [Header("Wander Settings")]
    public float wanderRadius = 20f;

    [Header("Destination Finding Settings")]
    public float maxTimesToSamplePosition = 30f;
    public float maxNavMeshSearchDistance = 3f;

    [Header("Avoidance Settings")]
    public LayerMask obstacleLayer;
    public float detectionDistance;
    public float detectionRadius;
    public float avoidanceTurnSpeed;
    public bool hasReachedDestination;

    [Header("Path Regen Settings")]
    public float stuckThreshold = 2f;
    public float velocityThreshold = 0.01f;
    public float distanceToCornerThreshold = 2f;
    public float directionToCornerThreshold = -0.5f;

    public bool moving => path != null && !hasReachedDestination && cornerIndex < path.corners.Length;
    public bool grounded => Physics.CheckSphere(transform.position, 0.4f, ~LayerMask.GetMask("NPC"));

    private CharacterController controller;
    private NavMeshPath path;
    private NavMeshHit hit;

    private int cornerIndex = 1;

    private Transform lastObstacle;
    private float obstacleCommitTimer = 0f;
    private float obstacleCommitDuration = 1.5f;
    private float cachedSide;

    private Vector3 previousPosition;

    private float stuckTimer = 0f;

    private bool isAvoidingObstacle;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        previousPosition = transform.position;

        path = new NavMeshPath();

        GetNewPath();
        ResetPath();
    }

    #region Gizmos Stuff
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(hit.position, 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Vector3.zero, wanderRadius);

        DrawPathCorners();
    }

    void DrawPathCorners()
    {
        if (path == null || path.corners == null)
            return;

        Gizmos.color = Color.cyan;

        // Draw all the path corners
        for (int i = 0; i < path.corners.Length; i++)
        {
            Gizmos.DrawSphere(path.corners[i], 0.3f);

            // Draw lines connecting them
            if (i < path.corners.Length - 1)
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
        }

        // Draw the current target corner in a different color
        if (cornerIndex < path.corners.Length)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path.corners[cornerIndex], 0.4f);
        }
    }
    #endregion
    
    #region Path Stuff
    public void MoveNPC(Vector3 direction)
    {
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        Vector3 velocity = transform.forward * moveSpeed;

        controller.Move(velocity * Time.deltaTime);
    }    
    
    public void SetMoveSpeed(float value)
    {
        moveSpeed = value;
    }

    public void GetNewPath()
    {
        Vector3 centerPoint = Vector3.zero;

        for (int i = 0; i < maxTimesToSamplePosition; i++)
        {
            Vector3 randomPoint = centerPoint + Random.insideUnitSphere * wanderRadius;

            if (NavMesh.SamplePosition(randomPoint, out hit, maxNavMeshSearchDistance, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path);

                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    cornerIndex = 1;
                    return;
                }
            }
        }
    }

    public void ResetPath()
    {
        cornerIndex = 1;
        hasReachedDestination = false;
    }
    #endregion

    #region Path Calculation Stuff
    public Vector3 CalculatePath()
    {
        // Cycle has finished
        if (PathHasEnded())
            return Vector3.zero;

        Vector3 directionToCorner = GetDirectionToCorner();

        if (IsStuckAndNeedNewPath(directionToCorner))
            return RequestNewPath();

        Vector3 finalDirection = directionToCorner.normalized;
        finalDirection = ApplyArrivalSlowdown(finalDirection, directionToCorner.magnitude);
        finalDirection = CheckForObstacleAndMove(finalDirection);

        previousPosition = transform.position;
        return finalDirection;
    }

    Vector3 ApplyArrivalSlowdown(Vector3 normalizedDirection, float distToCorner)
    {
        float slowdownRadius = moveSpeed * arrivalSlowdownTime;

        if (distToCorner >= slowdownRadius || slowdownRadius <= 0f)
            return normalizedDirection;

        float speedScale = Mathf.Clamp01(distToCorner / slowdownRadius);

        return normalizedDirection * speedScale;
    }

    Vector3 GetDirectionToCorner()
    {
        Vector3 target = path.corners[cornerIndex];
        Vector3 pathDir = target - transform.position;
        pathDir.y = 0;

        if (pathDir.magnitude < stoppingDistance)
        {
            cornerIndex++;
            previousPosition = transform.position;
            return Vector3.zero;
        }

        return pathDir;
    }

    bool PathHasEnded()
    {
        if (cornerIndex >= path.corners.Length)
        {
            hasReachedDestination = true;
            return true;
        }

        hasReachedDestination = false;
        return false;
    }

    bool IsStuckAndNeedNewPath(Vector3 directionToCorner)
    {
        return !isAvoidingObstacle && ShouldRegeneratePath(directionToCorner, directionToCorner.magnitude);
    }

    Vector3 RequestNewPath()
    {
        GetNewPath();
        ResetPath();
        return Vector3.zero;
    }
    #endregion

    #region Path Regen Stuff
    bool ShouldRegeneratePath(Vector3 pathDir, float distToCorner)
    {
        if (path == null || path.corners.Length == 0)
            return false;

        Vector3 currentVelocity = (transform.position - previousPosition).normalized;

        if (currentVelocity.magnitude < velocityThreshold)
        {
            if (RegenAfterTimer())
                return true;
        }
        else
        {
            stuckTimer = 0f;
        }

        if (RegenWhenFar(currentVelocity, pathDir, distToCorner))
            return true;

        return false;
    }

    bool RegenAfterTimer()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckThreshold)
        {
            Debug.Log($"NPC Regenerated Path! : {transform.name} : Reason: Stuck too long!");

            stuckTimer = 0f;
            return true;
        }

        return false;
    }

    bool RegenWhenFar(Vector3 currentVelocity, Vector3 pathDir, float distToCorner)
    {
        float dotProduct = Vector3.Dot(currentVelocity, pathDir.normalized);

        /// We are close to the corner but going the wrong way
        /// Dot product explanation:
        ///    1.0 = going the right direction to corner
        ///    0.0 = perpendicular to the corner
        ///   -1.0 = going the complete opposite direction
        if (distToCorner < distanceToCornerThreshold && dotProduct < directionToCornerThreshold)
        {
            Debug.Log($"NPC Regenerated Path! : {transform.name} : Reason: Going the wrong way!");
            return true;
        }

        return false;
    }
    #endregion

    #region Obstacle Avoidance Stuff
    Vector3 CheckForObstacleAndMove(Vector3 direction)
    {
        Vector3 pathRight = new Vector3(direction.z, 0f, -direction.x);
        Vector3 boxSize = new Vector3(controller.radius * 2f, controller.height, controller.radius);

        RaycastHit hitInfo;

        if (CheckForObstacle(direction, out hitInfo))
        {
            direction = CalculateDirection(hitInfo, direction, pathRight, boxSize);
        }
        else
        {
            ClearAllObstacles();
        }

        return direction;
    }

    Vector3 CalculateDirection(RaycastHit hitInfo, Vector3 direction, Vector3 pathRight, Vector3 boxSize)
    {
        isAvoidingObstacle = true;

        bool rightHit = SideBlocked(transform.position + pathRight * controller.radius, boxSize * 0.5f, obstacleLayer);
        bool leftHit = SideBlocked(transform.position - pathRight * controller.radius, boxSize * 0.5f, obstacleLayer);

        bool leftBlocked = leftHit && !rightHit;
        bool rightBlocked = rightHit && !leftHit;

        float side;

        if (leftBlocked)
            side = -1f;
        else if (rightBlocked)
            side = 1f;
        else
        {
            side = CalculateSide(hitInfo, pathRight);
        }

        SetDirectionAndObstacleTimer(hitInfo, side);

        return direction += pathRight * avoidanceTurnSpeed * cachedSide;
    }

    float CalculateSide(RaycastHit hitInfo, Vector3 pathRight)
    {
        Vector3 toObstacle = hitInfo.transform.position - transform.position;
        float sideCheck = Vector3.Dot(toObstacle, pathRight);
        float side = (sideCheck >= 0f) ? -1f : 1f;

        // If head-on, use instance IDs to pick opposite sides
        if (Mathf.Abs(sideCheck) < 0.5f)
        {
            if (hitInfo.transform.TryGetComponent<NPCAgent>(out NPCAgent otherNPC))
            {
                side = (this.GetInstanceID() > otherNPC.GetInstanceID()) ? 1f : -1f;
            }
        }

        return side;
    }

    void SetDirectionAndObstacleTimer(RaycastHit hitInfo, float side)
    {
        obstacleCommitTimer = obstacleCommitDuration;
        lastObstacle = hitInfo.transform;
        cachedSide = side;

        obstacleCommitTimer -= Time.deltaTime;
    }

    void ClearAllObstacles()
    {
        lastObstacle = null;
        obstacleCommitTimer = 0f;

        isAvoidingObstacle = false;
    }

    bool CheckForObstacle(Vector3 direction, out RaycastHit hitInfo)
    {
        bool capsuleHit = Physics.CapsuleCast(
            transform.position,
            transform.position + Vector3.up * controller.height,
            detectionRadius,
            direction,
            out hitInfo,
            detectionDistance,
            obstacleLayer);

        if (capsuleHit) return true;

        RaycastHit rayHit;
        bool raycastHit = Physics.Raycast(transform.position + Vector3.up * controller.height / 2f, direction, out rayHit, detectionDistance, obstacleLayer);

        if (raycastHit)
            hitInfo = rayHit;

        return raycastHit;
    }

    bool SideBlocked(Vector3 center, Vector3 halfExtents, LayerMask layer)
    {
        return Physics.CheckBox(center, halfExtents, Quaternion.identity, layer);
    }
    #endregion
}