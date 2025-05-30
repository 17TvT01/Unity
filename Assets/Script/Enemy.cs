using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Enemy : MonoBehaviour
{
    [SerializeField] private ExpDropper expDropper;
    [Header("Combat Settings")]
    [SerializeField] private float maxHP = 100f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float retreatThreshold = 0.2f; // HP dưới 20% sẽ rút lui
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float patrolRadius = 5f;

    private float currentHP;
    private float lastAttackTime;
    private Transform player;
    private Vector2 patrolPoint;
    private bool isDead = false;
    private bool isRetreating = false;
    private enum State { Idle, Patrol, Chase, Attack, Retreat, Dead }
    private State currentState = State.Idle;
    private enum EnemyRole { Leader, Follower }
    private EnemyRole role = EnemyRole.Follower;
    private static List<Enemy> allEnemies = new List<Enemy>();
    private static float groupRadius = 3f;
    private float lastAggressorTime;
    private GameObject lastAggressor;

    void Start()
    {
        expDropper = GetComponent<ExpDropper>();
        currentHP = maxHP;
        ChooseNewPatrolPoint();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        allEnemies.Add(this);
        UpdateEnemyRoles();
    }

    void Update()
    {
        if (isDead) return;
        switch (currentState)
        {
            case State.Patrol:
                Patrol();
                DetectPlayer();
                break;
            case State.Chase:
                ChasePlayer();
                break;
            case State.Attack:
                AttackPlayer();
                break;
            case State.Retreat:
                Retreat();
                break;
            case State.Idle:
                Patrol();
                DetectPlayer();
                break;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        currentHP -= amount;
        lastAggressor = player != null ? player.gameObject : null;
        lastAggressorTime = Time.time;
        if (currentHP <= 0)
        {
            Die();
        }
        else if (ShouldRetreat())
        {
            isRetreating = true;
            currentState = State.Retreat;
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    private void Die()
    {
        isDead = true;
        currentState = State.Dead;
        // Thả EXP khi chết
        expDropper?.DropExp();
        // Thêm hiệu ứng chết hoặc hủy object ở đây
        Destroy(gameObject, 2f);
    }

    private void Patrol()
    {
        if (role == EnemyRole.Follower)
        {
            var leader = allEnemies.FirstOrDefault(e => e.role == EnemyRole.Leader && !e.isDead);
            if (leader != null && leader != this)
            {
                MoveTo(leader.transform.position + (Vector3)(Random.insideUnitCircle * 1.5f));
                return;
            }
        }
        if (Vector2.Distance(transform.position, patrolPoint) < 0.2f)
        {
            ChooseNewPatrolPoint();
        }
        MoveTo(patrolPoint);
    }

    private void ChooseNewPatrolPoint()
    {
        Vector2 center = transform.position;
        patrolPoint = center + Random.insideUnitCircle * patrolRadius;
    }

    private void DetectPlayer()
    {
        if (player != null && Vector2.Distance(transform.position, player.position) < detectionRadius)
        {
            currentState = State.Chase;
        }
    }

    private void ChasePlayer()
    {
        if (player == null) { currentState = State.Patrol; return; }
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > detectionRadius * 1.5f)
        {
            currentState = State.Patrol;
            return;
        }
        if (dist <= attackRange)
        {
            currentState = State.Attack;
            return;
        }
        MoveTo(player.position);
    }

    private void AttackPlayer()
    {
        if (player == null) { currentState = State.Patrol; return; }
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist > attackRange)
        {
            currentState = State.Chase;
            return;
        }
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            // Gây sát thương cho player ở đây
            // player.GetComponent<Player>()?.TakeDamage(attackDamage);
            lastAttackTime = Time.time;
        }
    }

    private void Retreat()
    {
        if (player == null) { currentState = State.Patrol; return; }
        Vector2 dir = (transform.position - player.position).normalized;
        MoveTo((Vector2)transform.position + dir * moveSpeed * Time.deltaTime);
        if (currentHP / maxHP > retreatThreshold)
        {
            isRetreating = false;
            currentState = State.Patrol;
        }
    }

    private void MoveTo(Vector2 target)
    {
        Vector2 pos = Vector2.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    private bool ShouldRetreat()
    {
        int nearbyEnemies = 0;
        foreach (var enemy in allEnemies)
        {
            if (enemy != this && !enemy.isDead && Vector2.Distance(transform.position, enemy.transform.position) < groupRadius)
                nearbyEnemies++;
        }
        return currentHP / maxHP < retreatThreshold || nearbyEnemies >= 3;
    }

    private static void UpdateEnemyRoles()
    {
        var aliveEnemies = allEnemies.Where(e => !e.isDead).OrderBy(e => e.GetInstanceID()).ToList();
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            aliveEnemies[i].role = i == 0 ? EnemyRole.Leader : EnemyRole.Follower;
        }
    }

    private void OnDestroy()
    {
        allEnemies.Remove(this);
        UpdateEnemyRoles();
    }
}