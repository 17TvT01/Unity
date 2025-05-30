using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHealthUI : MonoBehaviour
{
    public Image healthBarFill;
    public TextMeshProUGUI healthText;
    private HealthBase healthBase;

    private void Start()
    {
        healthBase = GetComponent<HealthBase>();
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (healthBase != null)
        {
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = healthBase.currentHP / healthBase.maxHP;
            }
            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(healthBase.currentHP)}";
            }
        }
    }
}
