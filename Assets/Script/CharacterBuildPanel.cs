using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterBuildPanel : MonoBehaviour
{
    public TextMeshProUGUI statPointsText;
    public Button hpButton;
    public Button manaButton;
    public Button attackButton;
    public TextMeshProUGUI hpValueText;
    public TextMeshProUGUI manaValueText;
    public TextMeshProUGUI attackValueText;

    private PlayerBase player;

    void Start()
    {
        player = FindObjectOfType<PlayerBase>();
        hpButton.onClick.AddListener(OnHpButton);
        manaButton.onClick.AddListener(OnManaButton);
        attackButton.onClick.AddListener(OnAttackButton);
        UpdatePanel();
    }

    public void SetPlayer(PlayerBase p)
    {
        player = p;
        UpdatePanel();
    }

    public void UpdatePanel()
    {
        if (player == null) return;
        statPointsText.text = $"Stat Points: {player.statPoints}";
        hpValueText.text = $"HP: {player.MaxHealth}";
        manaValueText.text = $"Mana: {player.MaxMana}";
        attackValueText.text = $"Attack: {player.attackDamage}";
        hpButton.interactable = player.statPoints > 0;
        manaButton.interactable = player.statPoints > 0;
        attackButton.interactable = player.statPoints > 0;
    }

    void OnHpButton()
    {
        if (player != null && player.statPoints > 0)
        {
            player.AddStatPoint(PlayerBase.StatType.HP);
            UpdatePanel();
        }
    }
    void OnManaButton()
    {
        if (player != null && player.statPoints > 0)
        {
            player.AddStatPoint(PlayerBase.StatType.Mana);
            UpdatePanel();
        }
    }
    void OnAttackButton()
    {
        if (player != null && player.statPoints > 0)
        {
            player.AddStatPoint(PlayerBase.StatType.Attack);
            UpdatePanel();
        }
    }
}
