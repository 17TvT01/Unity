using UnityEngine;
using System.Collections;
using Pathfinding;

[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class ChaseEnemy : HealthBase
{    [Header("Target Settings")]
    private Transform target;
    private Transform player;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float nextWaypointDistance = 0.5f;
    [SerializeField] private float pathUpdateInterval = 0.2f;
    
    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.5f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float retreatThreshold = 0.3f; // Retreat when HP below 30%

    // Component references
    private Seeker seeker;
    private AIPath aiPath;
    private Rigidbody2D rb;
    private Animator animator;
    
    // Pathfinding variables
    private Path currentPath;
    private int currentWaypoint = 0;
    private bool reachedEndOfPath = false;
    private float lastPathUpdateTime;
    private float nextAttackTime;
    private bool isDead = false;
    private bool isRetreating = false;

    protected override void Awake()
    {
        base.Awake();
        // Get component references
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }    private void Start()
    {
        // Find initial target
        FindAndSetTarget();

        // Configure pathfinding settings
        aiPath.maxSpeed = moveSpeed;
        aiPath.endReachedDistance = attackRange * 0.8f; // Slightly less than attack range
        aiPath.slowdownDistance = attackRange * 1.5f;
        aiPath.pickNextWaypointDist = nextWaypointDistance;
        aiPath.enableRotation = false;
        aiPath.orientation = OrientationMode.YAxisForward;
        aiPath.maxAcceleration = 40f;
        aiPath.canMove = true;
        aiPath.canSearch = true;
        
        // Set seeker settings
        seeker.graphMask = -1; // All graphs
        seeker.traversableTags = -1; // All tags

        // Start updating path
        StartCoroutine(UpdatePathRoutine());
    }    private void FindAndSetTarget()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("Player not found!");
            return;
        }
        target = player;
    }

    private IEnumerator UpdatePathRoutine()
    {
        WaitForSeconds waitTime = new WaitForSeconds(pathUpdateInterval);

        while (!isDead)
        {
            // Always try to find target if we don't have one
            if (target == null)
            {
                FindAndSetTarget();
                yield return waitTime;
                continue;
            }

            if (!isRetreating)
            {
                // Always update path to chase target
                if (Time.time >= lastPathUpdateTime + pathUpdateInterval)
                {
                    lastPathUpdateTime = Time.time;
                    seeker.StartPath(transform.position, target.position, OnPathComplete);
                }
            }
            else if (isRetreating && target != null)
            {
                // Calculate retreat position - move away from target
                Vector2 directionFromTarget = ((Vector2)transform.position - (Vector2)target.position).normalized;
                Vector2 retreatPosition = (Vector2)transform.position + directionFromTarget * 10f;
                
                // Make sure the retreat position is valid
                seeker.StartPath(transform.position, retreatPosition, OnPathComplete);
            }

            yield return waitTime;
        }
    }

    private void UpdatePath()
    {
        if (Time.time >= lastPathUpdateTime + pathUpdateInterval && player != null)
        {
            lastPathUpdateTime = Time.time;
            seeker.StartPath(transform.position, player.position, OnPathComplete);
        }
    }    private void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            currentPath = p;
            currentWaypoint = 0;
            aiPath.destination = p.vectorPath[p.vectorPath.Count - 1];
        }
    }    private void Update()
    {
        if (isDead || target == null) return;

        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        
        // Update animation based on velocity
        animator.SetBool("IsRun", aiPath.velocity.magnitude > 0.1f);
        
        // Update facing direction based on velocity or direction to target
        Vector2 direction;
        if (aiPath.velocity.magnitude > 0.1f)
        {
            direction = aiPath.velocity.normalized;
        }
        else
        {
            direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
        }
        UpdateFacingDirection(direction);

        if (isRetreating)
        {
            HandleRetreat();
            return;
        }
        
        // Check if within attack range
        if (distanceToTarget <= attackRange)
        {
            // Stop movement
            aiPath.canMove = false;
            rb.linearVelocity = Vector2.zero;
            
            // Attack if cooldown is ready
            if (Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            // Always chase target
            aiPath.canMove = true;
            aiPath.destination = target.position;
            animator.SetBool("IsRun", aiPath.velocity.magnitude > 0.1f);
        }
    }

    private void Attack()
    {
        if (isDead) return;

        // Play attack animation
        animator.SetTrigger("Attack");

        // Check for player in attack range
        if (attackPoint != null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, playerLayer);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var playerHealth = hit.GetComponent<PlayerBase>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(attackDamage, gameObject);
                    }
                }
            }
        }
    }

    private void HandleRetreat()
    {
        if (currentHP / maxHP > retreatThreshold)
        {
            isRetreating = false;
            return;
        }        // Move away from player
        aiPath.canMove = true;
        animator.SetBool("IsRun", aiPath.velocity.magnitude > 0.1f);
    }

    private void UpdateFacingDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > 0.1f)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x) * (direction.x < 0 ? 1 : -1),
                transform.localScale.y,
                transform.localScale.z
            );
        }
    }

    public override void TakeDamage(float amount, GameObject attacker = null)
    {
        base.TakeDamage(amount, attacker);

        // Check if should retreat
        if (currentHP / maxHP <= retreatThreshold)
        {
            isRetreating = true;
        }
    }

    public override void Die()
    {
        if (isDead) return;
        isDead = true;

        // Disable AI and movement
        aiPath.canMove = false;
        seeker.enabled = false;
        rb.linearVelocity = Vector2.zero;

        // Play death animation
        animator.SetBool("IsRun", false);
        animator.SetTrigger("Die");

        // Drop exp if any exp dropper component exists
        var expDropper = GetComponent<ExpDropper>();
        expDropper?.DropExp();

        // Destroy after animation
        Destroy(gameObject, 2f);
    }    private void OnDrawGizmosSelected()
    {
        // Draw attack range
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
    }
}
