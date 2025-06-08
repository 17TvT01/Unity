using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(AIPath))]
public class Wolf : HealthBase
{
    [SerializeField] private ExpDropper expDropper;
    #region Settings
    [Header("Movement Settings")]
    [SerializeField] private float speed = 2f;
    [SerializeField] public float maxSpeed = 12f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask enemyLayers; // Layer của player và minions
    [SerializeField] private float pathUpdateInterval = 0.1f;
    [Header("Prediction Settings")]
    [SerializeField] private float predictionFactor = 0.5f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float acceleration = 10f;

    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float baseDamage = 15f; // Sát thương cơ bản
    [SerializeField] private float nextAttackTime; // Thời điểm có thể tấn công tiếp theo
    [SerializeField] private Transform attackPoint;  
    [SerializeField] private float attackRadius = 0.5f; 
    [SerializeField] private GameObject hitEffectPrefab; // Prefab hiệu ứng đánh trúng

    [Header("AI Settings")]
    [SerializeField] private float viewRadius = 25f;
    [SerializeField] private float minionDetectRange = 2f;
    [SerializeField] private float supportRange = 6f;
    [SerializeField] private float groupRadius = 3f;
    [SerializeField] private float retreatThreshold = 0.3f;
    [SerializeField] private float aggressorPriorityDuration = 3f;

    [Header("Territory Settings")]
    [SerializeField] private Vector2 territoryCenter;
    [SerializeField] private float territoryRadius = 25f;
    [SerializeField] private float returnToTerritoryThreshold = 40f;

    // Thêm các biến cho tốc độ tuần tra
    [SerializeField] private float minPatrolSpeed = 1f;
    [SerializeField] private float maxPatrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 3f;
    #endregion

    #region References
    private Seeker seeker;
    private AIPath aiPath;
    private Rigidbody2D rb;
    private Animator animator;
    private Transform player;
    private float lastPathUpdate;
    private Collider2D[] minionCache = new Collider2D[10];
    private bool hasValidNavMesh;
    private bool isReturningToTerritory;
    #endregion

    #region State Management
    public enum WolfRole { Leader, Follower }
    public enum WolfState { Patrolling, Chasing, Retreating, Investigating, ReturnToTerritory }
    public WolfRole Role { get; private set; }
    private WolfState currentState = WolfState.Patrolling;
    
    // Pack behavior
    [Header("Pack Behavior")]
    [SerializeField] private float packCallRadius = 15f;    // Bán kính gọi đồng đội
    [SerializeField] private float leaderFollowDistance = 5f; // Khoảng cách theo sau leader
    [SerializeField] private float packSpacing = 3f;        // Khoảng cách giữa các thành viên
    
    // Intelligence settings
    [Header("Intelligence Settings")]
    [SerializeField] private float investigationTime = 3f;   // Thời gian điều tra điểm khả nghi
    [SerializeField] private float memoryDuration = 5f;      // Thời gian nhớ vị trí cuối của mục tiêu
    [SerializeField] private float hearingRadius = 10f;      // Bán kính nghe thấy tiếng động
    [SerializeField] private float alertnessRadius = 8f;     // Bán kính cảnh giác xung quanh
    
    private Vector2 lastKnownTargetPosition;
    private float lastTargetSpottedTime;
    private float investigationStartTime;
    private bool isInvestigating;
    private bool isAlerted;
    
    // Original state variables
    private GameObject currentTarget;
    private GameObject lastAggressor;
    private float lastAttackTime;
    private float lastAggressorTime;
    private bool isRetreating;
    private bool isDead;
    
    // Patrol and territory variables
    private bool isPatrolling;
    private Vector2 currentPatrolPoint;    private float patrolWaitTime = 2f;
    private float lastPatrolPointReachedTime;
    private float minPatrolRadius = 3f;
    private float maxPatrolRadius = 8f;
    #endregion

    #region Static Management
    private static readonly List<Wolf> allWolves = new List<Wolf>();
    private static readonly HashSet<GameObject> validTargets = new HashSet<GameObject>();
    private static float lastTargetUpdate;
    private const float TARGET_UPDATE_INTERVAL = 0.5f;
    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        // Tự động thêm ExpDropper nếu chưa có
        if (expDropper == null)
            expDropper = GetComponent<ExpDropper>();
        if (expDropper == null)
            expDropper = gameObject.AddComponent<ExpDropper>();
        
        base.Awake();
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        nextAttackTime = 0f; // Khởi tạo để có thể tấn công ngay lập tức
        
        // Cấu hình AIPath
        aiPath.maxSpeed = minPatrolSpeed;
    aiPath.rotationSpeed = rotationSpeed;
    aiPath.maxAcceleration = acceleration;
    aiPath.slowdownDistance = 0.1f;
    aiPath.endReachedDistance = 0.1f;

        aiPath.enableRotation = false;
        aiPath.orientation = OrientationMode.YAxisForward;
        
        // Thêm event cho animation
        animator.speed = 1.2f; // Điều chỉnh tốc độ animation nếu cần
    }    protected virtual void OnStart()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        StartCoroutine(AIUpdateLoop());
    }    
    
    protected override void Start()
    {
        base.Start();
        // Nếu chưa được khởi tạo bởi spawner, sử dụng vị trí hiện tại
        if (territoryCenter == Vector2.zero)
        {
            InitializeTerritory(transform.position, territoryRadius);
        }
        OnStart();
    }

    private void OnEnable()
    {
        allWolves.Add(this);
        UpdateWolfRoles();
    }

    private void OnDisable()
    {
        allWolves.Remove(this);
        UpdateWolfRoles();
    }

    private void FixedUpdate()
    {
        UpdateAnimationState();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        seeker = GetComponent<Seeker>();    }
    #endregion

    #region AI Core    
    private IEnumerator AIUpdateLoop()
    {
        while (!isDead)
        {
            UpdateTargetCache();
            CheckNearbySound();

            // Ưu tiên xử lý trạng thái đặc biệt trước
            if (isReturningToTerritory)
            {
                if (Vector2.Distance(transform.position, territoryCenter) <= territoryRadius)
                {
                    isReturningToTerritory = false;
                }
                else
                {
                    HandleReturnToTerritoryState();
                }
            }
            else if (isRetreating || ShouldRetreat())
            {
                isRetreating = true;
                HandleRetreatState();
            }
            else if (isInvestigating)
            {
                HandleInvestigationState();
            }
            else
            {
                // Lấy mục tiêu tối ưu một lần
                var optimalTarget = GetOptimalTarget();
                currentTarget = optimalTarget;

                if (currentTarget != null)
                {
                    isPatrolling = false;
                    if (CheckLineOfSight(currentTarget))
                    {
                        HandleCombatState();
                    }
                    else if (ShouldInvestigateLastPosition())
                    {
                        isInvestigating = true;
                        investigationStartTime = Time.time;
                    }
                }
                else if (ShouldInvestigateLastPosition())
                {
                    isInvestigating = true;
                    investigationStartTime = Time.time;
                }
                else
                {
                    HandlePatrolState();
                }
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    // Tách riêng trạng thái quay về lãnh thổ
    private void HandleReturnToTerritoryState()
    {
        MoveToTarget(territoryCenter);
        currentState = WolfState.ReturnToTerritory;
    }

    private void HandleCombatState()
    {
        if (currentTarget == null || !isTargetValid(currentTarget))
        {
            currentTarget = GetOptimalTarget();
            if (currentTarget == null)
            {
                currentState = WolfState.Patrolling;
                return;
            }
        }

        // Get direction to target with prediction
        Vector3 targetPosition = PredictTargetPosition();
        Vector2 directionToTarget = (targetPosition - transform.position).normalized;
        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);

        // Update facing direction
        transform.localScale = new Vector3(
            Mathf.Abs(transform.localScale.x) * (directionToTarget.x < 0 ? 1 : -1),
            transform.localScale.y,
            transform.localScale.z
        );

        // Kiểm tra mục tiêu trong vùng AttackPoint
        bool inAttackRange = false;
        if (attackPoint != null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayers);
            foreach (var hit in hits)
            {
                if (hit.gameObject == currentTarget)
                {
                    inAttackRange = true;
                    break;
                }
            }
        }
        else
        {
            inAttackRange = (distanceToTarget <= attackRange);
        }

        // Tính lợi thế quân số
        int wolfCount = GetNearbyWolfCount(currentTarget.transform.position, groupRadius);
        int advantage = Mathf.Max(0, wolfCount - 1); // Số sói vượt trội (trừ bản thân)
        float attackBonus = 1f + 0.3f * advantage; // Mỗi sói thêm 30% damage
        float cooldownBonus = Mathf.Max(0.5f, attackCooldown - 0.1f * advantage); // Giảm cooldown tối đa 50%

        if (inAttackRange)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsRun", false);

            // Tấn công liên tục khi mục tiêu còn trong vùng
            if (Time.time >= nextAttackTime)
            {
                AttackWithAdvantage(attackBonus);
                nextAttackTime = Time.time + cooldownBonus;
            }
        }
        else
        {
            // Ngoài tầm đánh - di chuyển đến mục tiêu
            float speedMultiplier = Mathf.Lerp(0.5f, 1.0f, distanceToTarget / viewRadius);
            rb.linearVelocity = directionToTarget * moveSpeed * speedMultiplier;
            animator.SetBool("IsRun", true);
        }

        currentState = WolfState.Chasing;
    }

    // Hàm tấn công với lợi thế quân số
    private void AttackWithAdvantage(float attackBonus)
    {
        if (currentTarget == null) return;
        // Play attack animation
        animator.SetTrigger("Attack");
        lastAttackTime = Time.time;

        float finalDamage = baseDamage * Random.Range(0.9f, 1.1f) * attackBonus;
        float minionDamage = attackDamage * Random.Range(0.9f, 1.1f) * attackBonus;

        if (currentTarget.CompareTag("Player"))
        {
            var playerHealth = currentTarget.GetComponent<PlayerBase>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(finalDamage, gameObject);
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, currentTarget.transform.position, Quaternion.identity);
                }
            }
        }
        else
        {
            var targetHealth = currentTarget.GetComponent<HealthBase>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(minionDamage, gameObject);
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, currentTarget.transform.position, Quaternion.identity);
                }
            }
        }
    }

    private void HandleRetreatState()
    {
        if (currentTarget == null) return;

        // Tính toán hướng rút lui - ngược với hướng của mục tiêu
        Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.transform.position).normalized;
        
        // Tìm điểm rút lui an toàn trong lãnh thổ
        Vector2 safePoint = territoryCenter;
        float distanceToCenter = Vector2.Distance(transform.position, territoryCenter);
        
        if (distanceToCenter < territoryRadius)
        {
            // Nếu vẫn trong lãnh thổ, rút lui theo hướng ngược với mục tiêu
            safePoint = (Vector2)transform.position + (retreatDirection * supportRange);
            
            // Đảm bảo điểm rút lui vẫn nằm trong lãnh thổ
            if (Vector2.Distance(safePoint, territoryCenter) > territoryRadius)
            {
                // Nếu điểm rút lui nằm ngoài lãnh thổ, sử dụng điểm gần nhất trên đường biên
                Vector2 directionToCenter = (territoryCenter - safePoint).normalized;
                safePoint = territoryCenter - directionToCenter * territoryRadius;
            }
        }

        MoveToTarget(safePoint);

        // Kiểm tra xem đã đủ xa chưa
        if (Vector2.Distance(transform.position, currentTarget.transform.position) > supportRange * 1.5f)
        {
            isRetreating = false;
            aiPath.canMove = true;
        }
    }    private void HandlePatrolState()
    {
        // Điều chỉnh tốc độ về tốc độ tuần tra
        aiPath.maxSpeed = Random.Range(minPatrolSpeed, maxPatrolSpeed);

        // Nếu chưa có điểm tuần tra hoặc đã đến điểm tuần tra và đã đợi đủ thời gian
        if (!isPatrolling || 
            (Vector2.Distance(transform.position, currentPatrolPoint) < 0.5f && 
             Time.time - lastPatrolPointReachedTime > patrolWaitTime))
        {
            GenerateNewPatrolPoint();
        }

        // Nếu đến gần điểm tuần tra
        if (Vector2.Distance(transform.position, currentPatrolPoint) < 0.5f)
        {
            if (!isPatrolling)
            {
                lastPatrolPointReachedTime = Time.time;
                isPatrolling = true;
                // Dừng lại một chút tại điểm tuần tra
                aiPath.canMove = false;
                StartCoroutine(ResumePatrolAfterWait());
            }
        }
        else if (aiPath.canMove)
        {
            // Di chuyển đến điểm tuần tra nếu được phép di chuyển
            MoveToTarget(currentPatrolPoint);
        }
    }    private void GenerateNewPatrolPoint()
    {
        // Sử dụng Random.insideUnitCircle để tạo điểm ngẫu nhiên theo phân phối đều
        Vector2 randomPoint = Random.insideUnitCircle;
        float randomRadius = Random.Range(minPatrolRadius, maxPatrolRadius);
        
        // Tạo điểm ngẫu nhiên trong lãnh thổ
        Vector2 newPoint = territoryCenter + (randomPoint.normalized * randomRadius);

        // Đảm bảo điểm mới nằm trong lãnh thổ
        if (Vector2.Distance(newPoint, territoryCenter) > territoryRadius)
        {
            Vector2 directionToCenter = (territoryCenter - newPoint).normalized;
            newPoint = territoryCenter + directionToCenter * territoryRadius * 0.8f;
        }

        currentPatrolPoint = newPoint;
        isPatrolling = true;
        lastPatrolPointReachedTime = Time.time;
    }

    private IEnumerator ResumePatrolAfterWait()
    {
        yield return new WaitForSeconds(patrolWaitTime);
        if (isPatrolling) // Chỉ tiếp tục nếu vẫn đang trong trạng thái tuần tra
        {
            aiPath.canMove = true;
            GenerateNewPatrolPoint();
        }
    }
    #endregion

    #region Targeting
    private GameObject GetOptimalTarget()
    {
        if (!isTargetValid(currentTarget))
        {
            currentTarget = null;
        }

        // Priority 1: Kiểm tra aggressor gần đây
        if (lastAggressor != null && Time.time - lastAggressorTime < aggressorPriorityDuration)
        {
            if (isTargetValid(lastAggressor))
            {
                return lastAggressor;
            }
            lastAggressor = null;
        }

        // Priority 2: Tìm minion yếu nhất trong tầm
        GameObject weakestMinion = GetWeakestMinion();
        if (weakestMinion != null && isTargetValid(weakestMinion))
        {
            return weakestMinion;
        }

        // Priority 3: Kiểm tra player nếu trong tầm
        if (player != null && isTargetValid(player.gameObject))
        {
            return player.gameObject;
        }

        return null;
    }

    private bool isTargetValid(GameObject target)
    {
        if (target == null) return false;
        if (!target.activeInHierarchy) return false;

        // Kiểm tra khoảng cách
        float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
        if (distanceToTarget > viewRadius) return false;

        // Kiểm tra target có nằm trong territory
        float distanceToTerritory = Vector2.Distance(target.transform.position, territoryCenter);
        if (distanceToTerritory > territoryRadius * 1.2f) return false;

        // Kiểm tra line of sight
        Vector2 directionToTarget = (target.transform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayer);
        
        if (hit.collider != null && hit.collider.gameObject != target)
        {
            return false;
        }

        // Kiểm tra target còn sống
        var targetHealth = target.GetComponent<HealthBase>();
        if (targetHealth != null && targetHealth.currentHP <= 0)
        {
            return false;
        }

        return true;
    }

    private GameObject GetWeakestMinion()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, minionDetectRange, enemyLayers);
        var validTargets = hits
            .Select(h => h.gameObject)
            .Where(isTargetValid)
            .Where(g => g.TryGetComponent<HealthBase>(out var health) && health.currentHP > 0)
            .OrderBy(g => g.GetComponent<HealthBase>().currentHP);
        
        return validTargets.FirstOrDefault();
    }

    private static void UpdateTargetCache()
    {
        if (Time.time - lastTargetUpdate < TARGET_UPDATE_INTERVAL) return;
        
        validTargets.Clear();
        var targets = GameObject.FindGameObjectsWithTag("Minion");
        foreach (var target in targets)
        {
            if (target != null) validTargets.Add(target);
        }
        lastTargetUpdate = Time.time;
    }
    #endregion

    #region Combat
    private void AttackAllTargets()
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
                if (health.CompareTag("Minion")) 
                {
                    SetAggressor(hit.gameObject);
                }
            }
        }
    }

    private GameObject lastAttackerRef; // Store reference to last attacker
    
    public override void TakeDamage(float amount, GameObject attacker = null)
    {
        base.TakeDamage(amount, attacker);
        if (attacker != null)
        {
            lastAttackerRef = attacker; // Store the attacker reference
        }
        if ((currentHP / maxHP) < retreatThreshold) isRetreating = true;
    }
    #endregion

    #region Role Management
    private static void UpdateWolfRoles()
    {
        if (allWolves.Count == 0) return;

        var aliveWolves = allWolves.Where(w => !w.isDead).OrderBy(w => w.GetInstanceID()).ToList();
        
        for (int i = 0; i < aliveWolves.Count; i++)
        {
            aliveWolves[i].Role = i == 0 ? WolfRole.Leader : WolfRole.Follower;
        }
    }
    #endregion

    #region Helper Methods
    private void SetAggressor(GameObject aggressor)
    {
        lastAggressor = aggressor;
        lastAggressorTime = Time.time;
    }

    private bool ShouldRetreat()
    {
        return currentHP / maxHP < retreatThreshold || 
               Physics2D.OverlapCircleAll(transform.position, groupRadius, enemyLayers).Length >= 3;
    }

    private void UpdateAnimationState()
    {
        float currentSpeed = aiPath.velocity.magnitude;
        animator.SetBool("IsRun", currentSpeed > 0.1f);

        // Chỉ lật sprite khi tốc độ đủ lớn và hướng thay đổi đáng kể
        if (currentSpeed > 0.5f)
        {
            float currentDirection = transform.localScale.x;
            float targetDirection = aiPath.velocity.x < 0 ? 1 : -1;
            
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

    private void MoveToTarget(Vector2 targetPosition)
    {
        if (currentState == WolfState.Chasing || Time.time - lastPathUpdate > pathUpdateInterval)
        {
            seeker.StartPath(transform.position, targetPosition);
            lastPathUpdate = Time.time;
        }

        aiPath.destination = targetPosition;
        UpdateAnimationState();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
    if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
    {
        Vector2 pushDir = (transform.position - collision.transform.position).normalized;
        rb.AddForce(pushDir * 10f, ForceMode2D.Impulse);
    }
    }

    private bool IsOutsideTerritory()
    {
        return Vector2.Distance(transform.position, territoryCenter) > returnToTerritoryThreshold;
    }

    private void ReturnToTerritory()
    {
        isReturningToTerritory = true;
        MoveToTarget(territoryCenter);    }
    #endregion

    #region Overrides    
    public override void Die()
    {
        if (isDead) return;
        isDead = true;

        // Disable movement components
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.enabled = false;
        }
        if (seeker != null) seeker.enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Play death animation
        if (animator != null)
        {
            animator.SetBool("IsRun", false);
            animator.SetTrigger("IsDie");
        }

        // Drop EXP when dying
        expDropper?.DropExp(lastAttackerRef);

        // Optional: Don't destroy immediately to show death animation
        Destroy(gameObject, 1f);
    }

    public override void OnRespawn(Vector3 position)
    {
        base.OnRespawn(position);
        
        // Reset HP và vị trí
        currentHP = maxHP;
        transform.position = position;
        
        // Reset các trạng thái di chuyển
        isDead = false;
        isRetreating = false;
        isPatrolling = false;
        isInvestigating = false;
        isReturningToTerritory = false;
        
        // Reset movement components
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (aiPath != null)
        {
            aiPath.canMove = true;
            aiPath.maxSpeed = minPatrolSpeed;
            aiPath.enabled = true;
        }
        if (seeker != null) seeker.enabled = true;
        
        // Khởi tạo lại territory và AI
        InitializeTerritory(position, territoryRadius);
        if (!isDead) StartCoroutine(AIUpdateLoop());
        
        // Reset animation
        if (animator != null)
        {
            animator.SetBool("IsRun", false);
            animator.ResetTrigger("Attack");
            animator.ResetTrigger("IsDie");
        }

        UpdateHealthUI();
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        // Chỉ hiện attack range khi cần thiết
        if (UnityEditor.Selection.activeGameObject == gameObject)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
    #endregion

    public void InitializeTerritory(Vector2 center, float radius)
    {
        territoryCenter = center;
        territoryRadius = radius;
        returnToTerritoryThreshold = radius * 1.2f;
        minPatrolRadius = radius * 0.3f;
        maxPatrolRadius = radius * 0.8f;
        
        // Bắt đầu tuần tra ngay lập tức
        isPatrolling = false;
        GenerateNewPatrolPoint();
    }
    
    #region Intelligence Methods
    private void HandleInvestigationState()
    {
        // Nếu đã điều tra đủ lâu, quay về trạng thái tuần tra
        if (Time.time - investigationStartTime > investigationTime)
        {
            isInvestigating = false;
            return;
        }

        // Di chuyển đến điểm điều tra
        MoveToTarget(lastKnownTargetPosition);

        // Kiểm tra xem có phát hiện mục tiêu không
        if (currentTarget != null)
        {
            isInvestigating = false;
            lastKnownTargetPosition = currentTarget.transform.position;
            lastTargetSpottedTime = Time.time;
        }
    }

    private void CheckNearbySound()
    {
        // Kiểm tra âm thanh trong phạm vi nghe
        Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, hearingRadius, enemyLayers);
        foreach (var obj in nearbyObjects)
        {
            if (obj.TryGetComponent<INoisemaker>(out var noisemaker))
            {
                if (noisemaker.IsGeneratingSound())
                {
                    // Nếu nghe thấy tiếng động, điều tra vị trí đó
                    lastKnownTargetPosition = obj.transform.position;
                    isInvestigating = true;
                    investigationStartTime = Time.time;
                    AlertNearbyWolves(obj.transform.position);
                }
            }
        }
    }

    private void AlertNearbyWolves(Vector2 position)
    {
        // Thông báo cho các con sói khác trong phạm vi
        foreach (var wolf in allWolves)
        {
            if (wolf != this && Vector2.Distance(transform.position, wolf.transform.position) <= alertnessRadius)
            {
                wolf.OnAlerted(position);
            }
        }
    }

    public void OnAlerted(Vector2 position)
    {
        if (!isAlerted)
        {
            isAlerted = true;
            lastKnownTargetPosition = position;
            isInvestigating = true;
            investigationStartTime = Time.time;
        }
    }

    private bool CheckLineOfSight(GameObject target)
    {
        if (target == null) return false;

        Vector2 directionToTarget = (target.transform.position - transform.position).normalized;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, viewRadius, obstacleLayer);

        // Nếu không có vật cản hoặc vật cản là mục tiêu
        if (hit.collider == null || hit.collider.gameObject == target)
        {
            lastKnownTargetPosition = target.transform.position;
            lastTargetSpottedTime = Time.time;
            return true;
        }
        return false;
    }

    private bool ShouldInvestigateLastPosition()
    {
        // Kiểm tra xem có nên điều tra vị trí cuối của mục tiêu không
        return Time.time - lastTargetSpottedTime < memoryDuration && 
               Vector2.Distance(transform.position, lastKnownTargetPosition) > 0.5f;
    }
    #endregion

    // Dự đoán vị trí mục tiêu dựa trên vận tốc hiện tại
    private Vector3 PredictTargetPosition()
    {
        if (currentTarget == null) return transform.position;
        Rigidbody2D targetRb = currentTarget.GetComponent<Rigidbody2D>();
        if (targetRb == null) return currentTarget.transform.position;
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.transform.position);
        float timeToReach = distanceToTarget / moveSpeed;
        return currentTarget.transform.position + (Vector3)(targetRb.linearVelocity * timeToReach * 0.5f);
    }

    // Đếm số lượng sói xung quanh một vị trí trong bán kính nhất định
    private int GetNearbyWolfCount(Vector2 targetPos, float radius = 3f)
    {
        int count = 0;
        foreach (var wolf in allWolves)
        {
            if (wolf != null && !wolf.isDead && Vector2.Distance(wolf.transform.position, targetPos) <= radius)
                count++;
        }
        return count;
    }
}
