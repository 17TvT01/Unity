using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pathfinding;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class MonsterBase : HealthBase
{
    #region Settings
    [Header("Movement Settings")]
    [SerializeField] protected float speed = 2f;
    [SerializeField] protected float maxSpeed = 12f;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected LayerMask obstacleLayer;
    [SerializeField] protected LayerMask enemyLayers;
    [SerializeField] protected float pathUpdateInterval = 0.1f;

    [Header("Combat Settings")]
    [SerializeField] protected float attackRange = 1.2f;
    [SerializeField] protected float attackCooldown = 1f;
    [SerializeField] protected float attackDamage = 10f;
    [SerializeField] protected Transform attackPoint;
    [SerializeField] protected float attackRadius = 0.5f;
    [SerializeField] protected GameObject hitEffectPrefab;

    [Header("AI Settings")]
    [SerializeField] protected float viewRadius = 25f;
    [SerializeField] protected float retreatThreshold = 0.3f;
    [SerializeField] protected float chaseSpeed = 8f;
    [SerializeField] protected float detectionRange = 15f;

    [Header("Experience Settings")]
    [SerializeField] protected int expReward = 50;
    #endregion

    #region References
    protected Seeker seeker;
    protected AIPath aiPath;
    protected Rigidbody2D rb;
    protected Animator animator;
    protected Transform player;
    protected float lastPathUpdate;
    protected bool isDead;
    protected bool isRetreating;
    protected float nextAttackTime;
    protected GameObject currentTarget;
    #endregion

    #region State Management
    public enum MonsterState { Idle, Patrolling, Chasing, Retreating, Investigating }
    protected MonsterState currentState = MonsterState.Idle;
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        nextAttackTime = 0f;

        aiPath.maxSpeed = moveSpeed;
        aiPath.rotationSpeed = 360f;
        aiPath.maxAcceleration = 10f;
        aiPath.slowdownDistance = 0.1f;
        aiPath.endReachedDistance = 0.1f;
        aiPath.enableRotation = false;
        aiPath.orientation = OrientationMode.YAxisForward;
    }

    protected virtual void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        StartCoroutine(AIUpdateLoop());
    }

    protected virtual void FixedUpdate()
    {
        UpdateAnimationState();
    }
    #endregion

    #region AI Core
    protected virtual IEnumerator AIUpdateLoop()
    {
        while (!isDead)
        {
            UpdateTargetCache();

            if (isRetreating)
            {
                HandleRetreatState();
            }
            else
            {
                HandleBehavior();
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    protected virtual void HandleBehavior()
    {
        // Kiểm tra xem có phát hiện player không
        if (CanDetectPlayer())
        {
            currentTarget = player.gameObject;
            currentState = MonsterState.Chasing;
            HandleChaseState();
        }
        else
        {
            currentState = MonsterState.Idle;
            HandleIdleState();
        }
    }

    protected virtual void HandleChaseState()
    {
        if (currentTarget == null || !IsTargetValid(currentTarget))
        {
            currentTarget = null;
            currentState = MonsterState.Idle;
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);

        // Nếu trong tầm tấn công
        if (distanceToTarget <= attackRange)
        {
            // Dừng di chuyển và tấn công
            aiPath.canMove = false;
            rb.linearVelocity = Vector2.zero;
            
            if (Time.time >= nextAttackTime)
            {
                AttackPlayer();
            }
        }
        else
        {
            // Di chuyển đến mục tiêu
            aiPath.canMove = true;
            aiPath.maxSpeed = chaseSpeed;
            MoveToTarget(currentTarget.transform.position);
            
            // Cập nhật hướng nhìn
            UpdateFacingDirection(currentTarget.transform.position);
        }
    }

    protected virtual void HandleIdleState()
    {
        // Hành vi mặc định khi không có mục tiêu
        aiPath.canMove = false;
        rb.linearVelocity = Vector2.zero;
    }

    protected virtual void HandleRetreatState()
    {
        // Default retreat behavior: stop moving
        aiPath.canMove = false;
    }
    #endregion

    #region Combat
    protected virtual void Attack()
    {
        if (Time.time < nextAttackTime) return;

        animator.SetTrigger("Attack");
        nextAttackTime = Time.time + attackCooldown;
    }

    protected virtual void AttackPlayer()
    {
        if (currentTarget == null) return;
        if (Time.time < nextAttackTime) return;

        // Phát animation tấn công
        animator.SetTrigger("Attack");
        nextAttackTime = Time.time + attackCooldown;

        // Gây sát thương cho player
        if (currentTarget.CompareTag("Player"))
        {
            var playerHealth = currentTarget.GetComponent<HealthBase>();
            if (playerHealth != null)
            {
                float damage = attackDamage * Random.Range(0.9f, 1.1f);
                playerHealth.TakeDamage(damage, gameObject);
                
                // Hiệu ứng đánh trúng
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, currentTarget.transform.position, Quaternion.identity);
                }
                
                Debug.Log($"Monster attacked player for {damage} damage");
            }
        }
    }

    protected virtual void AttackAllTargets()
    {
        if (attackPoint == null)
        {
            Debug.LogError("Attack Point is not assigned!");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayers);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<HealthBase>(out var health))
            {
                health.TakeDamage(attackDamage);
            }
        }
    }

    public override void TakeDamage(float amount, GameObject attacker = null)
    {
        base.TakeDamage(amount, attacker);
        if (currentHP / maxHP < retreatThreshold) isRetreating = true;
    }
    #endregion

    #region Helper Methods
    protected virtual bool CanDetectPlayer()
    {
        if (player == null) return false;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        
        // Kiểm tra khoảng cách
        if (distanceToPlayer > detectionRange) return false;

        // Kiểm tra line of sight
        Vector2 directionToPlayer = (player.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToPlayer, distanceToPlayer, obstacleLayer);
        
        // Nếu không có vật cản hoặc vật cản là player
        return hit.collider == null || hit.collider.gameObject == player.gameObject;
    }

    protected virtual bool IsTargetValid(GameObject target)
    {
        if (target == null) return false;
        if (!target.activeInHierarchy) return false;

        // Kiểm tra khoảng cách
        float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
        if (distanceToTarget > viewRadius) return false;

        // Kiểm tra target còn sống
        var targetHealth = target.GetComponent<HealthBase>();
        if (targetHealth != null && targetHealth.currentHP <= 0)
        {
            return false;
        }

        return true;
    }    protected virtual void UpdateFacingDirection(Vector3 targetPosition)
    {
        Vector2 direction = (targetPosition - transform.position).normalized;
        
        if (Mathf.Abs(direction.x) > 0.1f)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(transform.localScale.x) * (direction.x > 0 ? -1 : 1),
                transform.localScale.y,
                transform.localScale.z
            );
        }
    }

    protected virtual void UpdateTargetCache()
    {
        // Placeholder for derived classes to implement target caching
    }    protected virtual void UpdateAnimationState()
    {
        float currentSpeed = aiPath.velocity.magnitude;
        animator.SetBool("IsRun", currentSpeed > 0.1f);

        // Cập nhật hướng nhìn dựa trên velocity
        if (currentSpeed > 0.5f)
        {
            float currentDirection = transform.localScale.x;
            float targetDirection = aiPath.velocity.x > 0 ? -1 : 1;
            
            // Chỉ lật khi hướng thực sự khác biệt
            if (Mathf.Sign(currentDirection) != Mathf.Sign(targetDirection))
            {
                transform.localScale = new Vector3(
                    Mathf.Abs(transform.localScale.x) * targetDirection,
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
        }
    }

    protected virtual void MoveToTarget(Vector2 targetPosition)
    {
        if (Time.time - lastPathUpdate > pathUpdateInterval)
        {
            seeker.StartPath(transform.position, targetPosition);
            lastPathUpdate = Time.time;
        }

        aiPath.destination = targetPosition;
        UpdateAnimationState();
    }
    #endregion

    #region Overrides
    public override void Die()
    {
        if (isDead) return;
        isDead = true;

        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.enabled = false;
        }
        if (seeker != null) seeker.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (animator != null)
        {
            animator.SetBool("IsRun", false);
            animator.SetTrigger("IsDie");
        }

        // Thưởng EXP cho player khi quái chết
        AwardExpToPlayer();

        Destroy(gameObject, 1f);
    }

    public override void OnRespawn(Vector3 position)
    {
        base.OnRespawn(position);

        currentHP = maxHP;
        transform.position = position;

        isDead = false;
        isRetreating = false;

        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (aiPath != null)
        {
            aiPath.canMove = true;
            aiPath.maxSpeed = moveSpeed;
            aiPath.enabled = true;
        }
        if (seeker != null) seeker.enabled = true;

        if (animator != null)
        {
            animator.SetBool("IsRun", false);
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("IsDie");
        }
    }
    #endregion

    #region Experience
    protected void AwardExpToPlayer()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) return;
        }

        var playerBase = player.GetComponent<PlayerBase>();
        if (playerBase != null)
        {
            playerBase.AddExp(expReward);
            Debug.Log($"Player received {expReward} EXP from monster.");
        }
    }
    #endregion
}
