using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class SummonedMinion : HealthBase
{
    // Định nghĩa các trạng thái có thể của minion
    private enum MinionState
    {
        Following,      // Đi theo Mage
        Attacking,      // Đang tấn công mục tiêu
        Returning,      // Đang quay về vị trí của Mage
        Supporting      // Hỗ trợ đồng đội
    }    private MinionState currentState = MinionState.Following;
    private Transform currentTarget;

    #region Settings
    [Header("Combat Settings")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private LayerMask enemyLayers;

    [Header("AI Settings")]
    [SerializeField] private float surroundRadius = 2.5f;
    [SerializeField] private float maxDistanceFromMage = 35f;
    [SerializeField] private float supportHealThreshold = 0.4f;

    [Header("Movement")]
    [SerializeField] private float avoidanceDistance = 0.5f;    [SerializeField] private float groupAvoidanceRadius = 1f;
    [SerializeField] private float movementSmoothing = 0.05f;
    #endregion

    #region References
    private Transform player;
    private Animator animator;
    private Rigidbody2D rb;
    private Coroutine attackCoroutine;
    private Vector2 currentVelocity;
    private static readonly Collider2D[] nearbyMinions = new Collider2D[10];
    private Seeker seeker;
    private AIPath aiPath;
    private float lastPathUpdate;
    private const float PATH_UPDATE_INTERVAL = 0.5f;
    private List<Vector2> currentPath;
    private int currentPathIndex;
    private float pathUpdateTimer;
    private Vector2 patrolPoint;
    #endregion

    #region State Management
    public enum MinionRole { Bait, Attacker, Support }
    public MinionRole Role { get; private set; }
    private float lastAttackTime;
    private bool facingRight = true;
    private bool isDead = false;
    #endregion

    #region Static Management
    private static readonly HashSet<GameObject> wolfCache = new HashSet<GameObject>();
    private static float lastWolfUpdate;
    private const float WOLF_UPDATE_INTERVAL = 0.5f;
    public static List<SummonedMinion> AllMinions { get; } = new List<SummonedMinion>();
    public static SummonedMinion BaitMinion { get; private set; }
    #endregion

    #region Unity Lifecycle
    protected override void Start()
    {
        base.Start();
        InitializeReferences();
        RegisterMinion();
        currentState = MinionState.Following;  // Start in following state
        StartCoroutine(AIUpdateLoop());
    }

    private void OnEnable()
    {
        Mage.OnMageAttacked += OnMageAttacked;
    }

    private void OnDisable()
    {
        Mage.OnMageAttacked -= OnMageAttacked;
    }

    private void OnDestroy()
    {
        // Đảm bảo luôn thông báo khi bị hủy
        var mage = FindObjectOfType<Mage>();
        if (mage != null)
            mage.OnMinionDeath(gameObject);
        UnregisterMinion();
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
    }

    private void FixedUpdate()
    {
        HandleAnimation();
    }
    #endregion

    #region Initialization
    private void InitializeReferences()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();

        // Cấu hình AIPath
        aiPath.maxSpeed = speed;
        aiPath.enableRotation = false;
        aiPath.orientation = OrientationMode.YAxisForward;
    }

    private void RegisterMinion()
    {
        AllMinions.Add(this);
        if (AllMinions.Count == 1) BaitMinion = this;
    }

    private void UnregisterMinion()
    {
        AllMinions.Remove(this);
        if (BaitMinion == this) BaitMinion = AllMinions.FirstOrDefault();
        ReassignRoles();
    }
    #endregion

    #region Movement
    private Vector2 GetAdjustedTargetPosition(Vector2 targetPos)
    {
        return targetPos; // A* pathfinding sẽ tự tìm đường đi phù hợp
    }

    private void MoveTowardsTarget(Vector2 targetPos)
    {
        if (Time.time - lastPathUpdate > PATH_UPDATE_INTERVAL)
        {
            seeker.StartPath(transform.position, targetPos);
            lastPathUpdate = Time.time;
        }

        // Group avoidance
        int numFound = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            groupAvoidanceRadius,
            nearbyMinions,
            LayerMask.GetMask("Minion")
        );

        if (numFound > 1)
        {
            Vector2 separation = Vector2.zero;
            for (int i = 0; i < numFound; i++)
            {
                if (nearbyMinions[i].gameObject != gameObject)
                {
                    Vector2 diff = (Vector2)transform.position - (Vector2)nearbyMinions[i].transform.position;
                    if (diff.magnitude < groupAvoidanceRadius)
                    {
                        separation += diff.normalized / diff.magnitude;
                    }
                }
            }
            
            if (separation != Vector2.zero)
            {
                separation = separation.normalized * speed;
                aiPath.destination = (Vector2)transform.position + separation;
                return;
            }
        }

        aiPath.destination = targetPos;
    }    private Vector2 CalculateTargetPosition()
    {
        if (Role == MinionRole.Bait)
            return currentTarget.position;

        int positionIndex = AllMinions.IndexOf(this);
        float angle = positionIndex * (2 * Mathf.PI / AllMinions.Count);
        return (Vector2)currentTarget.position + new Vector2(
            Mathf.Cos(angle) * surroundRadius,
            Mathf.Sin(angle) * surroundRadius
        );
    }
    
    private void GenerateNewPatrolPoint()
    {
        Vector2 randomOffset = Random.insideUnitCircle.normalized * Random.Range(5f, 10f);
        Vector2 basePoint = player != null ? (Vector2)player.position : transform.position;
        patrolPoint = basePoint + randomOffset;

        GraphNode startNode = AstarPath.active.GetNearest(transform.position).node;
        GraphNode endNode = AstarPath.active.GetNearest(patrolPoint).node;
        
        if (startNode == null || endNode == null || !PathUtilities.IsPathPossible(startNode, endNode))
        {
            GraphNode node = AstarPath.active.GetNearest(patrolPoint).node;
            if (node != null)
            {
                patrolPoint = (Vector3)node.position;
            }
        }
    }
    #endregion

    #region AI Core
    private IEnumerator AIUpdateLoop()
    {
        while (!isDead)
        {
            UpdateWolfCache();
            
            // Check for nearby threats first
            GameObject nearestWolf = FindNearestWolf();
            float distanceToWolf = nearestWolf != null ? 
                Vector2.Distance(transform.position, nearestWolf.transform.position) : float.MaxValue;            if (nearestWolf != null && distanceToWolf < 10f)
            {
                TransitionToState(MinionState.Attacking);
                currentTarget = nearestWolf.transform;
            }
            else if (Vector2.Distance(transform.position, player.position) > maxDistanceFromMage)
            {
                TransitionToState(MinionState.Following);
            }

            // Handle current state
            switch (currentState)
            {
                case MinionState.Following:
                    if (player != null && aiPath != null)
                    {
                        aiPath.canMove = true;
                        Vector3 targetPos = player.position + (Vector3)CalculateFollowOffset();
                        // Smoothly update destination only if significantly different
                        if (Vector3.Distance(aiPath.destination, targetPos) > 0.5f)
                        {
                            aiPath.destination = targetPos;
                        }
                    }
                    break;

                case MinionState.Attacking:
                    if (currentTarget != null)
                        HandleCombatState();
                    else
                        TransitionToState(MinionState.Following);
                    break;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private Vector2 CalculateFollowOffset()
    {
        // Create a formation around the mage
        float angle = (AllMinions.IndexOf(this) * 360f / AllMinions.Count) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2f;
    }

    private void HandleCombatState()
    {
        if (currentTarget == null) return;

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);        if (currentTarget.gameObject.CompareTag("Wolf"))
        {
            if (distanceToTarget <= attackRange)
            {
                aiPath.canMove = false;
                TryAttack();
            }
            else
            {
                aiPath.canMove = true;
                MoveTowardsTarget(currentTarget.position);
            }
        }
        else
        {
            aiPath.canMove = true;            Vector2 targetPos = (Vector2)currentTarget.position + (Random.insideUnitCircle.normalized * surroundRadius);
            MoveTowardsTarget(targetPos);
        }
    }

    private void HandlePatrolState()
    {
        if (Vector2.Distance(transform.position, patrolPoint) < 0.5f)
            GenerateNewPatrolPoint();

        MoveTowardsTarget(patrolPoint);
    }
    #endregion

    #region Combat
    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        lastAttackTime = Time.time;
        animator.SetTrigger("Attack");
        attackCoroutine = StartCoroutine(ExecuteAttack());
    }

    private IEnumerator ExecuteAttack()
    {
        yield return new WaitForSeconds(0.2f);
        
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
            transform.position, 
            attackRange, 
            enemyLayers
        );

        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.TryGetComponent<HealthBase>(out var health))
                health.TakeDamage(attackDamage);
        }    }
    #endregion

    #region Target Selection
    private Transform GetOptimalTarget()
    {
        if (currentTarget != null && currentTarget.gameObject.CompareTag("Wolf"))
            return currentTarget;
            
        GameObject nearestWolf = FindNearestWolf();
        if (nearestWolf != null && Vector2.Distance(transform.position, nearestWolf.transform.position) < 10f)
            return nearestWolf.transform;

        if (Role == MinionRole.Support && FindInjuredAlly(out SummonedMinion injuredAlly))
            return injuredAlly.transform;

        return player;
    }

    private GameObject FindNearestWolf()
    {
        GameObject nearest = null;
        float minDistance = float.MaxValue;

        foreach (GameObject wolf in wolfCache.Where(w => w != null))
        {            float distance = Vector2.Distance(transform.position, wolf.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = wolf;
            }
        }
        return nearest;
    }

    private bool FindInjuredAlly(out SummonedMinion injuredAlly)
    {
        injuredAlly = AllMinions
            .Where(m => m != this && m.GetHealthRatio() < supportHealThreshold)
            .OrderBy(m => m.GetCurrentHealth())
            .FirstOrDefault();

        return injuredAlly != null;
    }
    #endregion

    #region Helper Methods
    private static void UpdateWolfCache()
    {
        if (Time.time - lastWolfUpdate < WOLF_UPDATE_INTERVAL) return;
        
        wolfCache.Clear();
        wolfCache.UnionWith(GameObject.FindGameObjectsWithTag("Wolf"));
        wolfCache.RemoveWhere(w => w == null);
        lastWolfUpdate = Time.time;
    }

    private void UpdateFacing(bool faceRight)
    {
        if (faceRight == facingRight) return;
        
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private bool IsTooFarFromMage(Vector2 targetPosition)
    {
        return Vector2.Distance(targetPosition, player.position) > maxDistanceFromMage;
    }

    private void HandleAnimation()
    {
        animator.SetBool("IsRun", aiPath.velocity.magnitude > 0.1f);
        
        if (aiPath.velocity.magnitude > 0.1f)
        {
            bool faceRight = aiPath.velocity.x > 0;
            UpdateFacing(faceRight);
        }
    }

    private static void ReassignRoles()
    {
        if (AllMinions.Count == 0) return;

        var currentBait = AllMinions.FirstOrDefault(m => m.Role == MinionRole.Bait);
        BaitMinion = currentBait != null ? currentBait : AllMinions[0];
        BaitMinion.Role = MinionRole.Bait;

        var supports = AllMinions.Where(m => m.Role == MinionRole.Support).ToList();
        int requiredSupports = Mathf.Clamp(AllMinions.Count / 3, 1, 2);
        
        foreach (var m in AllMinions)
        {
            if (m == BaitMinion) continue;
            
            if (requiredSupports > 0 && m.Role != MinionRole.Support)
            {
                m.Role = MinionRole.Support;
                requiredSupports--;
            }
            else
            {
                m.Role = MinionRole.Attacker;
            }
        }
    }

    private void OnMageAttacked(GameObject attacker)
    {        if (attacker != null && attacker.CompareTag("Wolf"))
        {
            currentTarget = attacker.transform;
            Role = MinionRole.Attacker;
            lastAttackTime = 0;
        }
    }
    #endregion

    #region Overrides
    public override void Die()
    {
        if (isDead) return;
        isDead = true;
        animator.SetTrigger("IsDie");
        aiPath.canMove = false;
        rb.linearVelocity = Vector2.zero;
        // Thông báo cho Mage xóa minion khỏi danh sách
        var mage = FindObjectOfType<Mage>();
        if (mage != null)
            mage.OnMinionDeath(gameObject);
        Destroy(gameObject, 1f);
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, patrolPoint);
    }
    #endregion

    #region Health Info
    public float GetCurrentHealth()
    {
        return typeof(HealthBase).GetField("hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null
            ? (float)this.GetType().BaseType.GetField("hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(this)
            : 0f;
    }

    public float GetMaxHealth()
    {
        return typeof(HealthBase).GetField("maxHp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null
            ? (float)this.GetType().BaseType.GetField("maxHp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(this)
            : 1f;
    }

    public float GetHealthRatio()
    {
        float max = GetMaxHealth();
        return max > 0f ? GetCurrentHealth() / max : 0f;
    }
    #endregion    

    public void ResetBehavior()
    {
        if (TryGetComponent<AIPath>(out var aiPath))
        {
            aiPath.enabled = true;
            aiPath.canMove = true;
            // Reset path
            aiPath.destination = transform.position;
        }

        // Re-enable path updates
        if (TryGetComponent<Seeker>(out var seeker))
        {
            seeker.enabled = true;
        }

        // Use state transition to properly reset everything
        TransitionToState(MinionState.Following);
    }

    private void TransitionToState(MinionState newState)
    {
        if (currentState == newState) return;

        // Exit current state
        switch (currentState)
        {
            case MinionState.Attacking:
                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                    attackCoroutine = null;
                }
                break;
        }

        // Enter new state
        switch (newState)
        {
            case MinionState.Following:
                if (aiPath != null)
                {
                    aiPath.canMove = true;
                }
                currentTarget = null;
                break;

            case MinionState.Attacking:
                lastAttackTime = -attackCooldown; // Allow immediate attack
                break;
        }

        currentState = newState;
    }
}
