using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public int maxEnemies = 5;
    public float spawnInterval = 2f;
    public float cooldownDuration = 5f;
    
    [Header("Territory Settings")]
    public float territoryRadius = 25f;  // Bán kính lãnh thổ cho mỗi con sói
    public float spawnRadius = 5f;       // Bán kính vùng sinh quái
    public LayerMask groundLayer;

    private float timer;
    private bool playerInZone = false;
    private List<GameObject> enemies = new List<GameObject>();
    private float cooldownTimer = 0f;
    private bool firstWaveSpawned = false;

    void Update()
    {
        enemies.RemoveAll(enemy => enemy == null);

        // Spawn đàn đầu tiên khi player vào vùng
        if (playerInZone && !firstWaveSpawned)
        {
            SpawnFirstWave();
            return;
        }

        // Xử lý cooldown và respawn
        if (firstWaveSpawned && enemies.Count == 0)
        {
            cooldownTimer += Time.deltaTime;
            
            if (cooldownTimer >= cooldownDuration && playerInZone)
            {
                SpawnNewWave();
            }
            return;
        }

        // Spawn từng con nếu chưa đủ số lượng
        if (playerInZone && enemies.Count < maxEnemies)
        {
            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnEnemy();
                timer = 0;
            }
        }
    }

    void SpawnFirstWave()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            SpawnEnemy();
        }
        firstWaveSpawned = true;
        cooldownTimer = 0f;
    }

    void SpawnNewWave()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            SpawnEnemy();
        }
        cooldownTimer = 0f;
    }    void SpawnEnemy()
    {
        Vector3 spawnPoint = GetValidSpawnPoint();

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint, Quaternion.identity);
        enemies.Add(enemy);

        // Khởi tạo lãnh thổ cho Wolf nếu có component
        if (enemy.TryGetComponent<Wolf>(out var wolf))
        {
            wolf.InitializeTerritory(transform.position, territoryRadius);
        }
        
        // Cấu hình A* Pathfinding
        var aiPath = enemy.GetComponent<AIPath>() ?? enemy.AddComponent<AIPath>();
        var seeker = enemy.GetComponent<Seeker>() ?? enemy.AddComponent<Seeker>();
        
        if (wolf != null)
        {
            aiPath.maxSpeed = wolf.maxSpeed;
            aiPath.enableRotation = false;
            aiPath.orientation = OrientationMode.YAxisForward;
        }
    }

    private Vector3 GetValidSpawnPoint()
    {
        Vector3 spawnPoint = Vector3.zero;
        bool isValid = false;
        int attempts = 0;
        float maxAttempts = 20;
        
        while (!isValid && attempts < maxAttempts)
        {
            spawnPoint = transform.position + new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                Random.Range(-spawnRadius, spawnRadius),
                0
            );

            // Kiểm tra vị trí spawn hợp lệ trên ground layer
            if (Physics2D.OverlapCircle(spawnPoint, 0.5f, groundLayer))
            {
                isValid = true;
            }
            attempts++;
        }

        return spawnPoint;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            if (!firstWaveSpawned)
            {
                cooldownTimer = 0f;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
        }
    }    void OnDrawGizmosSelected()
    {
        // Vẽ vùng spawn
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        
        // Vẽ phạm vi lãnh thổ
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, territoryRadius);
    }
}