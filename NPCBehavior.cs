using UnityEngine;

public class NPCBehavior : MonoBehaviour, IDamageable
{
    [Header("References")]
    public NPCAgent agent;
    public NPCRagdoll ragdoll;
    public Animator animator;
    public CharacterController controller;
    public Transform hips;

    [Header("Health Settings")]
    public int maxHealth;

    [Header("Behavior Settings")]
    public float cowerThreshold;

    [Header("Time Settings")]
    public Vector2 waitTimeRange;
    public Vector2 panicTimeRange;
    public Vector2 cowerTimeRange;
    public float laughTime;

    [Header("Speed Settings")]
    public float speedTransitionSmooth;
    public float walkSpeed;
    public float runSpeed;

    [Header("Dieing Settings")]
    public float npcAlertRadiusWhenDead;

    [Header("Other")]
    public LayerMask npcLayer;
    public AIState currentState;    

    public enum AIState
    {
        Waiting,
        Wandering,
        Panicking,
        Cowering,
        Laughing
    }    

    private int GroundedID = Animator.StringToHash("Grounded");
    private int WalkingID = Animator.StringToHash("Walking");
    private int RunningID = Animator.StringToHash("Running");
    private int CoweringID = Animator.StringToHash("Cowering");
    private int LaughingTriggerName = Animator.StringToHash("Laughing");

    private float stateTimer;

    private float targetSpeed;

    private int health;

    private bool isDead = false;

    private Vector3 moveDirection;

    void Start()
    {
        health = maxHealth;
    }

    void Update()
    {
        if (!isDead)
        {
            SwitchStates();
            HandleAnimator();

            agent.SetMoveSpeed(Mathf.Lerp(agent.moveSpeed, targetSpeed, speedTransitionSmooth * Time.deltaTime));            
        }
        else
        {
            AlertNearbyNPCs(npcAlertRadiusWhenDead, true);
        }
    }

    void SwitchStates()
    {
        switch (currentState)
        {
            case AIState.Waiting:
                HandleWaiting();
                break;

            case AIState.Panicking:
                HandlePanicking();
                break;

            case AIState.Wandering:
                HandleWandering();
                break;

            case AIState.Cowering:
                HandleCowering();
                break;

            case AIState.Laughing:
                HandleLaughing();
                break;
        }
    }

    public void AlertNearbyNPCs(float alertRadius, bool checkParent)
    {
        /// Uses the hips bone position of the NPC that way if the NPC's ragdoll gets pushed around the 
        /// check still passes
        Collider[] hits = Physics.OverlapSphere(hips.position, alertRadius, npcLayer);

        for (int i = 0; i < hits.Length; i++)
        {   
            Collider collider = hits[i];

            if (collider.gameObject == gameObject) continue;

            NPCBehavior npc = checkParent ? collider.GetComponentInParent<NPCBehavior>() : collider.GetComponent<NPCBehavior>();

            if (npc != null && !npc.isDead)
                npc.DecideCowerOrPanic();
        }
    }

    void HandleWaiting()
    {
        agent.MoveNPC(Vector3.zero);

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
            SetAIState(AIState.Wandering);
    }

    void HandleWandering()
    {
        moveDirection = agent.CalculatePath();

        agent.MoveNPC(moveDirection);
        targetSpeed = walkSpeed;

        if (agent.hasReachedDestination)
        {
            SetAIState(AIState.Waiting);
            return;
        }
    }

    void HandlePanicking()
    {
        stateTimer -= Time.deltaTime;

        // End panic when timer expires
        if (stateTimer <= 0)
        {
            SetAIState(AIState.Waiting);
            return;
        }

        // Get a new destination when reaching current one
        if (agent.hasReachedDestination)
        {
            agent.GetNewPath();
            agent.ResetPath();
        }

        moveDirection = agent.CalculatePath();
        agent.MoveNPC(moveDirection);
        targetSpeed = runSpeed;
    }

    void HandleCowering()
    {
        agent.MoveNPC(Vector3.zero);
        targetSpeed = 0;

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
            SetAIState(AIState.Panicking);
    }

    void HandleLaughing()
    {
        agent.MoveNPC(Vector3.zero);
        targetSpeed = 0;

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0)
            SetAIState(AIState.Waiting);
    }

    public void DecideCowerOrPanic()
    {
        if (health > cowerThreshold)
            SetAIState(AIState.Panicking);
        else if (health < cowerThreshold)
            SetAIState(AIState.Cowering);
    }

    public void DecideLaughing()
    {
        if (currentState != AIState.Panicking && currentState != AIState.Cowering)
            SetAIState(AIState.Laughing);
    }

    void HandleAnimator()
    {
        animator.SetBool(GroundedID, agent.grounded);

        bool shouldWalk = currentState == AIState.Wandering;
        bool shouldRun = currentState == AIState.Panicking;
        bool shouldCower = currentState == AIState.Cowering;

        animator.SetBool(WalkingID, shouldWalk);
        animator.SetBool(RunningID, shouldRun);
        animator.SetBool(CoweringID, shouldCower);

        if (currentState == AIState.Laughing)
        {
            animator.SetTrigger(LaughingTriggerName);

            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Laughing"))
                animator.ResetTrigger(LaughingTriggerName);
        }
    }

    public void SetAIState(AIState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        if (newState == AIState.Waiting)
        {
            stateTimer = Random.Range(waitTimeRange.x, waitTimeRange.y);
        }
        else if (newState == AIState.Wandering)
        {
            agent.GetNewPath();
            agent.ResetPath();
        }
        else if (newState == AIState.Panicking)
        {
            stateTimer = Random.Range(panicTimeRange.x, panicTimeRange.y);

            agent.GetNewPath();
            agent.ResetPath();
        }
        else if (newState == AIState.Cowering)
        {
            stateTimer = Random.Range(cowerTimeRange.x, cowerTimeRange.y);

            agent.GetNewPath();
            agent.ResetPath();
        }
        else if (newState == AIState.Laughing)
        {
            stateTimer = laughTime;

            agent.GetNewPath();
            agent.ResetPath();
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;

        DecideCowerOrPanic();

        if (health <= 0)
            Die();
    }

    void Die()
    {
        animator.enabled = false;
        agent.enabled = false;
        controller.enabled = false;

        ragdoll.SetRagdoll(true);

        isDead = true;
    }
}
