using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class Knight : PlayerBase
{
    [Header("Knight Specific Stats")]
    [SerializeField] private float manaRegenRate = 5f;

    // Dash skill properties
    [Header("Skills")]
    [SerializeField] private float dashForce = 20f;
    [SerializeField] private float dashManaCost = 20f;
    [SerializeField] private float dashCooldown = 3f;
    private float lastDashTime = -10f;

    // Shield skill properties
    [SerializeField] private float shieldDuration = 3f;
    [SerializeField] private float shieldManaCost = 30f;
    [SerializeField] private float shieldCooldown = 5f;
    private float lastShieldTime = -10f;
    private bool isShieldActive = false;

    // New skill properties
    [SerializeField] private float provokeRange = 5f;
    [SerializeField] private float provokeCost = 20f;
    [SerializeField] private float provokeCooldown = 8f;
    private float lastProvokeTime = -10f;

    [SerializeField] private float defensiveStanceCost = 30f;
    [SerializeField] private float defensiveStanceDuration = 5f;
    [SerializeField] private float defensiveStanceCooldown = 12f;
    private float lastDefensiveStanceTime = -10f;
    private bool isDefensiveStanceActive = false;


    protected override void HandleAbilityInput()
    {
        // Check attack input with mana cost
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime && HasEnoughMana(attackManaCost))
        {
            Attack();
        }

        // Dash skill (Q key)
        if (Input.GetKeyDown(KeyCode.Q) && Time.time - lastDashTime > dashCooldown && HasEnoughMana(dashManaCost))
        {
            UseSkill(SkillType.Dash);
        }

        // Shield skill (E key)
        if (Input.GetKeyDown(KeyCode.E) && Time.time - lastShieldTime > shieldCooldown && HasEnoughMana(shieldManaCost))
        {
            UseSkill(SkillType.Shield);
        }

        // Provoke skill (R key)
        if (Input.GetKeyDown(KeyCode.R) && Time.time - lastProvokeTime > provokeCooldown && HasEnoughMana(provokeCost))
        {
            UseProvoke();
        }

        // Defensive Stance skill (F key)
        if (Input.GetKeyDown(KeyCode.F) && Time.time - lastDefensiveStanceTime > defensiveStanceCooldown && HasEnoughMana(defensiveStanceCost))
        {
            UseDefensiveStance();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        resourceManager.mana.regenRate = manaRegenRate;
    }

    protected override void Update()
    {
        base.Update();

        // Update invincibility timer
        if (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }

    private void UseProvoke()
    {
        UseMana(provokeCost);
        lastProvokeTime = Time.time;
        animator?.SetTrigger("Provoke");

        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, provokeRange, enemyLayer);
        foreach (Collider2D enemy in enemies)
        {            
            if (enemy.TryGetComponent<AIController>(out var ai))
            {
                ai.ForceTarget(gameObject, 5f);
            }
        }
    }

    private void UseDefensiveStance()
    {
        UseMana(defensiveStanceCost);
        lastDefensiveStanceTime = Time.time;
        isDefensiveStanceActive = true;
        animator?.SetTrigger("DefensiveStance");
        StartCoroutine(HandleDefensiveStanceDuration());
    }

    private IEnumerator HandleDefensiveStanceDuration()
    {
        yield return new WaitForSeconds(defensiveStanceDuration);
        isDefensiveStanceActive = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        // Draw provoke range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, provokeRange);
    }
#endif


    private void UseSkill(SkillType skillType)
    {
        switch (skillType)
        {
            case SkillType.Dash:
                Dash();
                break;
            case SkillType.Shield:
                ActivateShield();
                break;
        }
    }

    private void Dash()
    {
        if (!HasEnoughMana(dashManaCost)) return;

        UseMana(dashManaCost);
        lastDashTime = Time.time;
        
        // Get dash direction from movement or mouse position
        Vector2 dashDirection = movement.sqrMagnitude > 0 
            ? movement.normalized 
            : (Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position).normalized;

        // Apply dash force
        rb.AddForce(dashDirection * dashForce, ForceMode2D.Impulse);
        animator?.SetTrigger("Dash");
    }

    private void ActivateShield()
    {
        if (!HasEnoughMana(shieldManaCost)) return;

        UseMana(shieldManaCost);
        lastShieldTime = Time.time;
        isShieldActive = true;
        animator?.SetTrigger("Shield");

        // Visual effect for shield (you'll need to implement this)
        StartCoroutine(HandleShieldDuration());
    }

    private System.Collections.IEnumerator HandleShieldDuration()
    {
        // Add shield visual effect here
        
        yield return new WaitForSeconds(shieldDuration);
        
        isShieldActive = false;
        // Remove shield visual effect here
    }

    public override void TakeDamage(float amount, GameObject attacker = null)
    {
        if (invincibilityTimer > 0) return;
        
        if (isShieldActive)
        {
            amount *= 0.5f;
        }
        if (isDefensiveStanceActive)
        {
            amount *= 0.5f;
        }
        
        base.TakeDamage(amount, attacker);
        invincibilityTimer = invincibilityTime;
    }
}
