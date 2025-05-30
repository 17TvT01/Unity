using UnityEngine;
using System.Collections;

public interface ISpawnable
{
    void OnRespawn(Vector3 position);
}

public class PlayerRespawnManager : MonoBehaviour
{
    [Header("Respawn Settings")]
    public float respawnDelay = 2f;
    public Transform defaultRespawnPoint;
    public GameObject respawnEffect;
    public bool useCheckpoints = true;

    private Transform lastCheckpoint;
    private GameObject currentPlayer;

    public static PlayerRespawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        lastCheckpoint = defaultRespawnPoint;
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        if (useCheckpoints && checkpoint != null)
            lastCheckpoint = checkpoint;
    }

    public void SetCurrentPlayer(GameObject player)
    {
        currentPlayer = player;
    }

    public GameObject GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public void RemoveCurrentPlayer()
    {
        currentPlayer = null;
    }

    public bool HasCurrentPlayer()
    {
        return currentPlayer != null;
    }

    public void RespawnCurrentPlayer()
    {
        if (currentPlayer != null)
            StartCoroutine(RespawnCoroutine(currentPlayer));
    }

    public void RespawnPlayer(GameObject player)
    {
        SetCurrentPlayer(player);
        StartCoroutine(RespawnCoroutine(player));
    }

    private IEnumerator RespawnCoroutine(GameObject player)
    {
        yield return new WaitForSeconds(respawnDelay);

        // Chọn vị trí hồi sinh
        Vector3 respawnPos = (useCheckpoints && lastCheckpoint != null)
            ? lastCheckpoint.position
            : defaultRespawnPoint.position;

        // Gọi OnRespawn nếu có (trước khi SetActive để đảm bảo trạng thái được reset)
        if (player.TryGetComponent<ISpawnable>(out var spawnable))
        {
            spawnable.OnRespawn(respawnPos);
        }

        // Đặt lại vị trí và active player
        player.transform.position = respawnPos;
        player.SetActive(true);

        // Đảm bảo Rigidbody2D được bật lại để di chuyển
        var rb2d = player.GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.simulated = true;
        }        // Reset resources and state through ResourceManager
        if (player.TryGetComponent<ResourceManager>(out var resourceManager))
        {
            resourceManager.Reset();
        }

        // Hiệu ứng hồi sinh
        if (respawnEffect != null)
        {
            Instantiate(respawnEffect, respawnPos, Quaternion.identity);
        }
    }
}