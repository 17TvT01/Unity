using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceManager : MonoBehaviour
{
    [System.Serializable]
    public class ResourceSettings
    {
        public float currentValue;
        public float maxValue = 100f;
        public float regenRate = 0f;
        public float regenInterval = 5f;
        public float regenPercent = 0.01f;
        public float buffMultiplier = 1f;
        private float lastRegenTime;

        public void Initialize()
        {
            currentValue = maxValue;
            lastRegenTime = Time.time;
        }

        public void SetMaxValue(float newMax)
        {
            float ratio = currentValue / maxValue;
            maxValue = newMax;
            currentValue = maxValue * ratio;
        }

        public void Update()
        {
            if (regenRate > 0 || (regenInterval > 0 && regenPercent > 0))
            {
                if (Time.time - lastRegenTime >= regenInterval)
                {
                    float regenAmount = regenRate > 0 
                        ? regenRate * buffMultiplier 
                        : maxValue * regenPercent * buffMultiplier;
                    
                    currentValue = Mathf.Min(maxValue, currentValue + regenAmount);
                    lastRegenTime = Time.time;
                }
            }
        }

        public bool HasEnough(float amount)
        {
            return currentValue >= amount;
        }

        public void Use(float amount)
        {
            currentValue = Mathf.Max(0, currentValue - amount);
            lastRegenTime = Time.time; // Reset regen timer when resource is used
        }

        public void SetBuffMultiplier(float multiplier)
        {
            buffMultiplier = multiplier;
        }
    }

    [Header("Health Settings")]
    public ResourceSettings health = new ResourceSettings
    {
        maxValue = 100f,
        regenInterval = 5f,
        regenPercent = 0.01f
    };

    [Header("Mana Settings")]
    public ResourceSettings mana = new ResourceSettings
    {
        maxValue = 100f,
        regenRate = 5f
    };

    [Header("UI References")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image manaBarFill;
    [SerializeField] private TextMeshProUGUI manaText;

    // Delegate để PlayerBase đăng ký cập nhật EXP UI khi máu/mana thay đổi
    public System.Action OnResourceChanged;

    private void Start()
    {
        health.Initialize();
        mana.Initialize();
        UpdateUI();
    }

    private void Update()
    {
        bool changed = false;

        float oldHealth = health.currentValue;
        float oldMana = mana.currentValue;

        health.Update();
        mana.Update();

        if (oldHealth != health.currentValue || oldMana != mana.currentValue)
        {
            UpdateUI();
            OnResourceChanged?.Invoke();
        }
    }

    public void TakeDamage(float amount)
    {
        health.Use(amount);
        UpdateUI();
        OnResourceChanged?.Invoke();
    }

    public void Heal(float amount)
    {
        health.currentValue = Mathf.Min(health.maxValue, health.currentValue + amount);
        UpdateUI();
        OnResourceChanged?.Invoke();
    }

    public bool UseMana(float amount)
    {
        if (!mana.HasEnough(amount)) return false;
        
        mana.Use(amount);
        UpdateUI();
        OnResourceChanged?.Invoke();
        return true;
    }    public void UpdateUI()
    {
        // Use the GameManager's UI manager for smooth animations if possible
        if (GameManager.Instance?.uiManager != null)
        {
            GameManager.Instance.uiManager.UpdateHealthUI(health.currentValue, health.maxValue);
            GameManager.Instance.uiManager.UpdateManaUI(mana.currentValue, mana.maxValue);
        }
        else
        {
            // Fallback to direct update if no GameManager
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = health.currentValue / health.maxValue;
            }
            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(health.currentValue)} / {Mathf.CeilToInt(health.maxValue)}";
            }

            if (manaBarFill != null)
            {
                manaBarFill.fillAmount = mana.currentValue / mana.maxValue;
            }
            if (manaText != null)
            {
                manaText.text = $"{Mathf.CeilToInt(mana.currentValue)} / {Mathf.CeilToInt(mana.maxValue)}";
            }
        }
    }

    public void SetupUI(Image hpFill, TextMeshProUGUI hpText, Image mpFill, TextMeshProUGUI mpText)
    {
        healthBarFill = hpFill;
        healthText = hpText;
        manaBarFill = mpFill;
        manaText = mpText;
        UpdateUI();
    }

    public void Initialize(float maxHealth, float maxMana)
    {
        health.SetMaxValue(maxHealth);
        mana.SetMaxValue(maxMana);
        health.Initialize();
        mana.Initialize();
        UpdateUI();
        Debug.Log($"[ResourceManager] Initialized: Health={health.currentValue}/{health.maxValue}, Mana={mana.currentValue}/{mana.maxValue}");
    }

    public void Reset()
    {
        // Reset health and mana
        health.Initialize();
        mana.Initialize();

        // Reset status effects
        health.buffMultiplier = 1f;
        mana.buffMultiplier = 1f;

        UpdateUI();
        Debug.Log($"[ResourceManager] Reset: Health={health.currentValue}/{health.maxValue}, Mana={mana.currentValue}/{mana.maxValue}");
    }

    // Helper properties
    public float CurrentHealth => health.currentValue;
    public float MaxHealth => health.maxValue;
    public float CurrentMana => mana.currentValue;
    public float MaxMana => mana.maxValue;
    public bool IsDead => health.currentValue <= 0;

    public void SetHealthRegenBuff(float multiplier)
    {
        health.SetBuffMultiplier(multiplier);
    }
}
