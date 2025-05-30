using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUIManager : MonoBehaviour
{
    [Header("Health Bar")]
    public Image healthBarBackground;
    public Image healthBarFill;
    public TextMeshProUGUI healthText;

    [Header("Mana Bar")]
    public Image manaBarBackground;
    public Image manaBarFill;
    public TextMeshProUGUI manaText;

    [Header("EXP Bar")]
    public Image expBarBackground;
    public Image expBarFill;
    public TextMeshProUGUI expText;

    [Header("Level")]
    public TextMeshProUGUI levelText;

    [Header("UI Animation")]
    [SerializeField] private float updateSpeed = 5f;

    private float targetHealthFill;
    private float targetManaFill;

    private void Awake()
    {
        ValidateUIReferences();
    }

    private void Update()
    {
        // Smooth lerp for health and mana bars
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, targetHealthFill, Time.deltaTime * updateSpeed);
        }
        if (manaBarFill != null)
        {
            manaBarFill.fillAmount = Mathf.Lerp(manaBarFill.fillAmount, targetManaFill, Time.deltaTime * updateSpeed);
        }
    }

    private void ValidateUIReferences()
    {
        if (healthBarFill == null)
            Debug.LogError("Health Bar Fill Image is not set in GameplayUIManager!");
        if (healthText == null)
            Debug.LogError("Health Text is not set in GameplayUIManager!");
        if (manaBarFill == null)
            Debug.LogError("Mana Bar Fill Image is not set in GameplayUIManager!");
        if (manaText == null)
            Debug.LogError("Mana Text is not set in GameplayUIManager!");
        if (expBarFill == null)
            Debug.LogError("EXP Bar Fill Image is not set in GameplayUIManager!");
        if (expText == null)
            Debug.LogError("EXP Text is not set in GameplayUIManager!");
        if (levelText == null)
            Debug.LogError("Level Text is not set in GameplayUIManager!");
    }

    public void SetupPlayerUI(GameObject player)
    {
        if (player.TryGetComponent<ResourceManager>(out var resourceManager))
        {
            resourceManager.SetupUI(healthBarFill, healthText, manaBarFill, manaText);
        }
    }

    public void UpdateHealthUI(float current, float max)
    {
        if (healthBarFill != null)
        {
            targetHealthFill = Mathf.Clamp01(current / max);
        }
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    public void UpdateManaUI(float current, float max)
    {
        if (manaBarFill != null)
        {
            targetManaFill = Mathf.Clamp01(current / max);
        }
        if (manaText != null)
        {
            manaText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    public void UpdateExpUI(float current, float max)
    {
        if (expBarFill != null)
        {
            expBarFill.fillAmount = Mathf.Clamp01(current / max);
        }
        if (expText != null)
        {
            expText.text = $"{current} / {max}";
        }
    }

    public void UpdateLevelUI(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"{level}";
        }
    }

    private void OnDestroy()
    {

    }
}
