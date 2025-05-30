using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    // Event khi nhân vật được spawn
    public System.Action<GameObject> OnCharacterSpawned;

    [Header("Character Data")]
    public List<CharacterData> availableCharacters = new List<CharacterData>();
    private int currentCharacterIndex = 0;
    
    [Header("UI References")]
    public Image characterPreviewImage;
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI characterStatsText;
    public GameObject characterSelectionCanvas;  // Canvas chọn nhân vật
    
    [Header("Spawn Settings")]
    public Transform spawnPoint;
    private GameObject currentCharacterInstance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (availableCharacters.Count > 0)
        {
            UpdateCharacterPreview();
        }
    }

    public void NextCharacter()
    {
        currentCharacterIndex = (currentCharacterIndex + 1) % availableCharacters.Count;
        UpdateCharacterPreview();
    }

    public void PreviousCharacter()
    {
        currentCharacterIndex--;
        if (currentCharacterIndex < 0)
            currentCharacterIndex = availableCharacters.Count - 1;
        UpdateCharacterPreview();
    }

    private void UpdateCharacterPreview()
    {
        CharacterData currentChar = availableCharacters[currentCharacterIndex];
        
        if (characterPreviewImage != null)
            characterPreviewImage.sprite = currentChar.characterSprite;
            
        if (characterNameText != null)
            characterNameText.text = currentChar.characterName;
            
        if (characterStatsText != null)
        {
            characterStatsText.text = $"HP: {currentChar.baseHealth}\n" +
                                    $"Mana: {currentChar.baseMana}\n" +
                                    $"Attack: {currentChar.baseAttackDamage}\n" +
                                    $"Speed: {currentChar.baseMoveSpeed}";
        }
    }

    public void SelectCharacter()
    {
        if (currentCharacterInstance != null)
        {
            Destroy(currentCharacterInstance);
        }

        CharacterData selectedChar = availableCharacters[currentCharacterIndex];
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
          currentCharacterInstance = Instantiate(selectedChar.characterPrefab, spawnPosition, Quaternion.identity);
        Debug.Log($"Spawned character with stats: HP={selectedChar.baseHealth}, Mana={selectedChar.baseMana}");
        
        // Initialize ResourceManager first
        if (currentCharacterInstance.TryGetComponent<ResourceManager>(out var resourceManager))
        {
            resourceManager.Initialize(selectedChar.baseHealth, selectedChar.baseMana);
            Debug.Log($"Character ResourceManager initialized: HP={resourceManager.CurrentHealth}/{resourceManager.MaxHealth}, Mana={resourceManager.CurrentMana}/{resourceManager.MaxMana}");
        }
        
        // HealthBase will sync with ResourceManager in its Awake method

        if (currentCharacterInstance.TryGetComponent<PlayerBase>(out PlayerBase player))
        {
            player.SetStats(selectedChar);
        }

        // Ẩn UI chọn nhân vật
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(false);
        }

        // Thông báo nhân vật mới được spawn
        OnCharacterSpawned?.Invoke(currentCharacterInstance);

        // Bắt đầu game với nhân vật đã chọn
        GameManager.Instance.StartGame(currentCharacterInstance);
    }

    public void ShowCharacterSelection()
    {
        if (characterSelectionCanvas != null)
        {
            characterSelectionCanvas.SetActive(true);
        }
        UpdateCharacterPreview();
    }
}
