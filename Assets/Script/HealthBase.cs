using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBase : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;
    protected bool isPlayer = false;

    protected ResourceManager resourceManager;

    public void SetupHealthUI(Image healthFill, TextMeshProUGUI healthText)
    {
        // Force initial UI update only if this is the player
        if (isPlayer)
        {
            GameManager.Instance?.uiManager?.UpdateHealthUI(currentHP, maxHP);
        }
    }

    protected virtual void Awake()
    {
        // Get ResourceManager if it exists
        resourceManager = GetComponent<ResourceManager>();

        // Set initial HP, preferring ResourceManager's values if available
        if (resourceManager != null)
        {
            maxHP = resourceManager.MaxHealth;
            currentHP = resourceManager.CurrentHealth;
        }
        else
        {
            currentHP = maxHP;
        }
        Debug.Log($"[HealthBase] Initialized HP: {currentHP}/{maxHP}");
    }

    protected virtual void Start()
    {
        if (!resourceManager) // Only update UI directly if we don't have a ResourceManager
        {
            UpdateHealthUI();
        }
    }

    public virtual void TakeDamage(float amount, GameObject attacker = null)
    {
        if (resourceManager != null)
        {
            resourceManager.TakeDamage(amount);
            currentHP = resourceManager.CurrentHealth; // Keep our value in sync
        }
        else
        {
            currentHP -= amount;
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            UpdateHealthUI();
        }
        
        if (currentHP <= 0)
        {
            Die();
        }
    }

    public virtual void Heal(float amount)
    {
        if (resourceManager != null)
        {
            resourceManager.Heal(amount);
            currentHP = resourceManager.CurrentHealth; // Keep our value in sync
        }
        else
        {
            currentHP += amount;
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            UpdateHealthUI();
        }
    }

    public virtual void Die()
    {
        // Override in class con nếu cần
    }

    protected virtual void UpdateHealthUI()
    {
        if (resourceManager != null)
        {
            return; // Let ResourceManager handle UI updates
        }

        if (isPlayer)
        {
            // Update player UI through GameManager
            GameManager.Instance?.uiManager?.UpdateHealthUI(currentHP, maxHP);
        }
        else
        {
            // Update enemy UI if it exists
            var enemyUI = GetComponent<EnemyHealthUI>();
            enemyUI?.UpdateUI();
        }
    }

    public virtual void OnRespawn(Vector3 position)
    {
        if (resourceManager != null)
        {
            resourceManager.Reset();
            currentHP = resourceManager.CurrentHealth;
        }
        else
        {
            currentHP = maxHP;
            UpdateHealthUI();
        }
    }
}
