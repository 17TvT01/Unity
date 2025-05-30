using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }    [Header("UI")]
    public GameplayUIManager uiManager;  // Manager cho gameplay UI (HP, Mana bars)
    public GameObject minimapCanvas;     // Canvas chứa minimap

    [Header("Camera")]
    public CameraFollow cameraFollow;    // Component để camera follow nhân vật
    public Minimap minimap;             // Component minimap camera

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

    public void StartGame(GameObject player)
    {
        // Setup camera follow
        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(player.transform);
        }

        // Setup minimap
        if (minimap != null)
        {
            minimap.SetTarget(player.transform);
        }        // Setup gameplay UI
        if (uiManager != null)
        {
            uiManager.SetupPlayerUI(player);
        }

        // Enable minimap
        if (minimapCanvas != null)
        {
            minimapCanvas.SetActive(true);
        }
    }
}
