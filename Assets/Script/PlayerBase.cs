using UnityEngine;
using System.Collections;

public abstract class PlayerBase : MonoBehaviour, ISpawnable
{
    [Header("Character Data")]
    [SerializeField] protected CharacterData characterData;

    [Header("Combat Stats")]
    public float attackDamage { get; protected set; }
    public float attackRange = 1.5f;
    protected float attackManaCost;
    protected float attackCooldown = 0.5f;
    protected float nextAttackTime = 0f;

    [Header("Level and EXP")]
    public int level = 1;
    public int currentExp = 0;
    public int totalExpRequired = 100;  // Tổng exp cần để lên level tiếp theo
    private int baseExpRequired = 100;  // Exp cơ bản cần cho level 1
    public int previousLevelExp = 0;   // Exp cần cho level trước đó

    [Header("Movement")]
    public float baseMoveSpeed { get; protected set; }
    protected Vector2 movement;
    protected bool isRunning = false;
    protected bool facingRight = true;

    [Header("References")]
    public Transform attackPoint;
    public LayerMask enemyLayer;
    protected ResourceManager resourceManager;
    protected Rigidbody2D rb;
    protected Animator animator;
    protected bool isDead = false;

    [Header("Invincibility")]
    [SerializeField] protected float invincibilityTime = 1f;
    protected float invincibilityTimer = 0f;

    protected GameObject characterBuildPanel;

    protected virtual void Awake()
    {
        // Get components
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        resourceManager = GetComponent<ResourceManager>();
        if (resourceManager == null)
        {
            resourceManager = gameObject.AddComponent<ResourceManager>();
        }

        // Khởi tạo EXP system
        InitializeExpSystem();

        // Initialize stats from character data if available
        if (characterData != null)
        {
            baseMoveSpeed = characterData.baseMoveSpeed;
            attackDamage = characterData.baseAttackDamage;
            attackManaCost = characterData.attackManaCost;
            resourceManager.Initialize(characterData.baseHealth, characterData.baseMana);
        }

        // Character build panel logic (shared by all player types)
        characterBuildPanel = GameObject.Find("CharacterBuildPanel");
        if (characterBuildPanel != null)
            characterBuildPanel.SetActive(false);
    }

    private void InitializeExpSystem()
    {
        level = 1;
        currentExp = 0;
        previousLevelExp = 0;
        totalExpRequired = baseExpRequired; // 100 exp cho level đầu tiên
        UpdateExpUI(); // Cập nhật UI để hiển thị 0/100
    }

    protected virtual void Update()
    {
        if (isDead || !gameObject.activeInHierarchy) return;

        HandleMovementInput();
        HandleFacing();
        HandleAbilityInput();

        // Toggle character build panel with 'C' for all player types
        if (Input.GetKeyDown(KeyCode.C) && characterBuildPanel != null)
        {
            characterBuildPanel.SetActive(!characterBuildPanel.activeSelf);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;
        MovePlayer();
    }

    protected virtual void HandleMovementInput()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        isRunning = movement.sqrMagnitude > 0;
        animator?.SetBool("IsRun", isRunning);
    }

    protected virtual void HandleFacing()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (mousePos.x > transform.position.x && !facingRight)
            Flip();
        else if (mousePos.x < transform.position.x && facingRight)
            Flip();
    }

    protected virtual void MovePlayer()
    {
        rb.MovePosition(rb.position + movement.normalized * baseMoveSpeed * Time.fixedDeltaTime);
    }

    protected virtual void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    protected virtual void Attack()
    {
        if (Time.time < nextAttackTime || !resourceManager.UseMana(attackManaCost))
            return;

        nextAttackTime = Time.time + attackCooldown;
        animator.SetTrigger("Attack");
        PerformAttack();
    }

    protected virtual void PerformAttack()
    {
        Vector3 center = attackPoint != null ? attackPoint.position : transform.position;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(center, attackRange, enemyLayer);
        
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.TryGetComponent<HealthBase>(out var enemyHealth))
            {
                enemyHealth.TakeDamage(attackDamage);
            }
        }
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
#endif

    protected abstract void HandleAbilityInput();

    public virtual void TakeDamage(float amount, GameObject attacker = null)
    {
        resourceManager.TakeDamage(amount);
        if (resourceManager.IsDead)
        {
            Die();
        }
    }

    public virtual void Heal(float amount)
    {
        resourceManager.Heal(amount);
    }

    public virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        
        rb.simulated = false;
        animator?.SetTrigger("IsDie");

        // Ẩn nhân vật sau khi animation death chạy xong và gọi respawn
        float deathAnimLength = 1.0f; // Thay bằng độ dài animation death thực tế nếu biết
        // Đảm bảo luôn lấy được MonoBehaviour để chạy coroutine
        MonoBehaviour mono = GetComponent<MonoBehaviour>() ?? this as MonoBehaviour;
        if (mono != null)
            mono.StartCoroutine(HideAndRespawnAfterDelay(deathAnimLength));
    }

    private IEnumerator HideAndRespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
        // Gọi respawn từ hệ thống respawn manager
        var respawnManager = Object.FindFirstObjectByType<PlayerRespawnManager>();
        if (respawnManager != null)
        {
            respawnManager.RespawnPlayer(gameObject);
        }
    }
    public virtual void OnRespawn(Vector3 position)
    {
        // Reset vital state flags
        isDead = false;
        isRunning = false;
        
        // Reset transform and physics
        transform.position = position;
        if (rb != null)
        {
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        // Reset input state
        movement = Vector2.zero;
        
        // Reset movement state
        facingRight = true;
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

        // Reset animation state completely
        if (animator != null)
        {
            // Reset to default state
            animator.Rebind();
            animator.Update(0f);
            
            // Clear all triggers
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.ResetTrigger(parameter.name);
                }
            }
            
            // Reset common state flags
            animator.SetBool("IsRun", false);
            animator.SetBool("IsAttacking", false);
            animator.SetFloat("Speed", 0);
            animator.Play("Idle", 0, 0f);
        }

        // KHÔNG reset EXP khi respawn
        // Chỉ reset health và mana
        if (resourceManager != null)
        {
            resourceManager.Reset();
        }

        // Enable GameObject and ensure it's visible
        gameObject.SetActive(true);
        if (TryGetComponent<SpriteRenderer>(out var spriteRenderer))
        {
            spriteRenderer.enabled = true;
        }

        Debug.Log($"[PlayerBase] OnRespawn: Position={position}, isDead={isDead}, simulated={rb?.simulated}, facingRight={facingRight}");
    }

    // Helper properties
    public float CurrentHealth => resourceManager.CurrentHealth;
    public float MaxHealth => resourceManager.MaxHealth;
    public float CurrentMana => resourceManager.CurrentMana;
    public float MaxMana => resourceManager.MaxMana;

    public virtual void SetStats(CharacterData data)
    {
        if (data != null)
        {
            attackDamage = data.baseAttackDamage;
            baseMoveSpeed = data.baseMoveSpeed;
            attackManaCost = data.attackManaCost;
            resourceManager.Initialize(data.baseHealth, data.baseMana);
            
            Debug.Log($"[PlayerBase] SetStats: HP={data.baseHealth}, Mana={data.baseMana}, " +
                     $"Damage={data.baseAttackDamage}, Speed={data.baseMoveSpeed}");
        }
    }

    protected bool HasEnoughMana(float cost)
    {
        return resourceManager.mana.HasEnough(cost);
    }

    protected void UseMana(float amount)
    {
        resourceManager.UseMana(amount);
    }

    protected void UpdateHealthAndManaUI()
    {
        resourceManager.UpdateUI();
    }

    public void AddExp(int amount)
    {
        currentExp += amount;
        CheckLevelUp();
        UpdateExpUI();
        Debug.Log($"Added {amount} EXP. Current: {currentExp}, Need for next level: {totalExpRequired}");
    }

    protected void CheckLevelUp()
    {
        while (currentExp >= totalExpRequired)
        {
            LevelUp();
        }
    }

    protected virtual void LevelUp()
    {
        level++;
        previousLevelExp = totalExpRequired;
        totalExpRequired = CalculateExpRequired(level + 1);

        // Tăng các chỉ số cơ bản khi lên cấp
        resourceManager.health.maxValue += 20f;
        resourceManager.health.currentValue = resourceManager.health.maxValue;
        resourceManager.mana.maxValue += 10f;
        resourceManager.mana.currentValue = resourceManager.mana.maxValue;
        baseMoveSpeed += 0.5f;

        Debug.Log($"Player leveled up to {level}! Next level requires {totalExpRequired} total EXP");
        UpdateExpUI(); // Đảm bảo UI cập nhật cấp mới
    }

    public int CalculateExpRequired(int targetLevel)
    {
        // Công thức tính exp cần thiết: baseExp * (1.5^(level-1))
        return Mathf.RoundToInt(baseExpRequired * Mathf.Pow(1.5f, targetLevel - 1));
    }

    protected void UpdateExpUI()
    {
        if (resourceManager != null)
        {
            var gameplayUI = FindObjectOfType<GameplayUIManager>();
            if (gameplayUI != null)
            {
                // Hiển thị exp dựa trên khoảng cách giữa level hiện tại và level tiếp theo
                float currentLevelExp = currentExp - previousLevelExp;
                float expNeededForLevel = totalExpRequired - previousLevelExp;
                gameplayUI.UpdateExpUI(currentLevelExp, expNeededForLevel);
                gameplayUI.UpdateLevelUI(level); // Đảm bảo cập nhật luôn cấp độ
                Debug.Log($"[EXP UI] Current Level: {level}, EXP: {currentLevelExp}/{expNeededForLevel} " +
                         $"(Total: {currentExp}/{totalExpRequired})");
            }
        }
    }
}
