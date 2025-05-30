using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Mage : PlayerBase
{
    // Event that fires when Mage is attacked
    public delegate void MageAttackedHandler(GameObject attacker);
    public static event MageAttackedHandler OnMageAttacked;
    private GameObject lastAttacker;

    [Header("Skills")]
    // Fireball Skill
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private float fireballSpeed = 15f;
    [SerializeField] private float fireballManaCost = 25f;
    [SerializeField] private float fireballCooldown = 2f;
    private float lastFireballTime = -10f;

    // Enhanced Summoning
    [SerializeField] private float summonManaCost = 40f;
    [SerializeField] private float summonCooldown = 8f;
    [SerializeField] private int maxMinions = 3;
    [SerializeField] private GameObject summonEffectPrefab;
    [SerializeField] private GameObject recallEffectPrefab;
    private float lastSummonTime = -10f;
    public GameObject[] summonPrefabs;
    public Transform summonPoint;

    // Minion Management
    private List<MinionInfo> activeMinions = new List<MinionInfo>();
    private List<MinionInfo> storedMinions = new List<MinionInfo>();
    private bool hasSummonedMinions => activeMinions.Count > 0 || storedMinions.Count > 0;
    private Vector3[] summonPositions;

    [Header("Skill Settings")]
    [SerializeField] private float recallCooldown = 8f;
    [SerializeField] private float recallDuration = 0.5f;
    private float lastRecallTime = -10f;

    private class MinionInfo
    {
        public GameObject gameObject;
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public bool isRecalling;
        public float recallStartTime;
        public Vector3 recallStartPosition;

        public MinionInfo(GameObject go)
        {
            gameObject = go;
            originalPosition = go.transform.position;
            originalRotation = go.transform.rotation;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        CalculateSummonPositions();
    }

    protected override void Update()
    {
        base.Update();
        UpdateMinionRecall();
    }

    private void CalculateSummonPositions()
    {
        summonPositions = new Vector3[maxMinions];
        float angleStep = 360f / maxMinions;
        float radius = 2f;

        for (int i = 0; i < maxMinions; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            summonPositions[i] = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
        }
    }

    private void UpdateMinionRecall()
    {
        // Chỉ xử lý thu hồi minion mà không có hiệu ứng animation
        for (int i = activeMinions.Count - 1; i >= 0; i--)
        {
            var info = activeMinions[i];
            if (info.isRecalling && info.gameObject != null)
            {
                float progress = (Time.time - info.recallStartTime) / recallDuration;
                if (progress >= 1f)
                {
                    CompleteRecall(info);
                    activeMinions.RemoveAt(i);
                }
                else
                {
                    // Không còn hiệu ứng scale hay lerp, chỉ giữ nguyên vị trí
                }
            }
        }
    }

    private void Summon()
    {
        if (activeMinions.Count >= maxMinions) return;

        UseMana(summonManaCost);
        lastSummonTime = Time.time;
        animator.SetTrigger("Summon");

        if (summonPrefabs != null && summonPrefabs.Length > 0 && summonPoint != null)
        {
            // Clean up destroyed minions
            activeMinions.RemoveAll(info => info.gameObject == null);

            // Calculate position for new minion
            int currentIndex = activeMinions.Count;
            Vector3 summonPos = summonPoint.position + summonPositions[currentIndex];

            // Spawn summon effect
            if (summonEffectPrefab != null)
            {
                var effect = Instantiate(summonEffectPrefab, summonPos, Quaternion.identity);
                Destroy(effect, 1f);
            }

            // Summon new minion
            int idx = Random.Range(0, summonPrefabs.Length);
            GameObject minion = Instantiate(summonPrefabs[idx], summonPos, Quaternion.identity);
              // Add to active minions with position info
            var minionInfo = new MinionInfo(minion);
            minionInfo.originalPosition = summonPos;  // Set the original position to summon position
            activeMinions.Add(minionInfo);

            // Initialize minion behavior
            if (minion.TryGetComponent<SummonedMinion>(out var summonedMinion))
            {
                summonedMinion.ResetBehavior();
            }
        }
    }

    protected override void HandleAbilityInput()
    {
        // Chuột trái: Bắn cầu lửa
        if (Input.GetMouseButtonDown(0) && Time.time >= lastFireballTime + fireballCooldown && HasEnoughMana(fireballManaCost))
        {
            CastFireball();
        }

        // E - Summon/Release
        int totalMinions = activeMinions.Count + storedMinions.Count;
        if (Input.GetKeyDown(KeyCode.E) && Time.time - lastSummonTime > summonCooldown)
        {
            if (totalMinions < maxMinions && HasEnoughMana(summonManaCost))
            {
                Summon();
            }
            else if (storedMinions.Count > 0)
            {
                ReleaseMinions();
            }
        }

        // Q - Recall Minions
        if (Input.GetKeyDown(KeyCode.Q) && Time.time - lastRecallTime > recallCooldown)
        {
            RecallMinions();
        }
    }

    private void CastFireball()
    {
        lastFireballTime = Time.time;
        UseMana(fireballManaCost);
        animator.SetTrigger("Attack");

        // Tính hướng bắn
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (mousePos - transform.position).normalized;

        // Tạo cầu lửa
        if (fireballPrefab != null)
        {
            GameObject fireball = Instantiate(fireballPrefab, transform.position, Quaternion.identity);
            var fireballProjectile = fireball.GetComponent<FireballProjectile>();
            if (fireballProjectile != null)
            {
                fireballProjectile.Initialize(attackDamage, direction * fireballSpeed);
            }
            else
            {
                // Fallback nếu không có script FireballProjectile
                Rigidbody2D rb = fireball.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = direction * fireballSpeed;
                }
            }
        }
    }

    private void ReleaseMinions()
    {
        lastSummonTime = Time.time;
        animator.SetTrigger("Release");        int releasedCount = 0;
        foreach (var minionInfo in storedMinions.ToList())
        {
            if (minionInfo.gameObject != null && activeMinions.Count + releasedCount < maxMinions)
            {
                // Calculate new position using releasedCount
                Vector3 releasePos = summonPoint.position + summonPositions[activeMinions.Count + releasedCount];
                releasedCount++;

                // Release effect
                if (summonEffectPrefab != null)
                {
                    var effect = Instantiate(summonEffectPrefab, releasePos, Quaternion.identity);
                    Destroy(effect, 1f);
                }                // Reset minion
                minionInfo.gameObject.transform.position = releasePos;
                minionInfo.gameObject.transform.localScale = Vector3.one;
                minionInfo.gameObject.SetActive(true);
                
                // Reset original position to current position
                minionInfo.originalPosition = releasePos;
                
                // Re-enable AI component and reset its state
                if (minionInfo.gameObject.TryGetComponent<SummonedMinion>(out var minion))
                {
                    minion.enabled = true;
                    minion.ResetBehavior();  // Thêm phương thức này vào SummonedMinion
                }
                
                // Move to active list
                activeMinions.Add(minionInfo);
            }
        }
        storedMinions.Clear();
    }

    private void RecallMinions()
    {
        if (!hasSummonedMinions) return;
        
        lastRecallTime = Time.time;
        animator.SetTrigger("Recall");

        // Start recall process for each active minion
        foreach (var minionInfo in activeMinions)
        {
            if (minionInfo.gameObject != null)
            {
                minionInfo.isRecalling = true;
                minionInfo.recallStartTime = Time.time;
                minionInfo.recallStartPosition = minionInfo.gameObject.transform.position;

                // Spawn recall effect
                if (recallEffectPrefab != null)
                {
                    var effect = Instantiate(recallEffectPrefab, minionInfo.gameObject.transform.position, Quaternion.identity);
                    Destroy(effect, recallDuration);
                }

                // Disable minion AI during recall
                if (minionInfo.gameObject.TryGetComponent<SummonedMinion>(out var minion))
                {
                    minion.enabled = false;
                }
            }
        }
    }    private void CompleteRecall(MinionInfo minionInfo)
    {
        if (minionInfo.gameObject != null)
        {
            // Reset scale before storing
            minionInfo.gameObject.transform.localScale = Vector3.one;
            minionInfo.gameObject.SetActive(false);
            minionInfo.isRecalling = false;
            storedMinions.Add(minionInfo);
        }
    }

    public override void TakeDamage(float amount, GameObject attacker = null)
    {
        // Mage không có hệ thống invincibility như Knight
        base.TakeDamage(amount, attacker);
        if (amount > 0)
        {
            lastAttacker = attacker;
            OnMageAttacked?.Invoke(attacker);
        }
    }    // Clean up references when minions die
    public void OnMinionDeath(GameObject minion)
    {
        activeMinions.RemoveAll(info => info.gameObject == minion);
        storedMinions.RemoveAll(info => info.gameObject == minion);
    }

    // Đảm bảo Mage sử dụng logic Die() của PlayerBase để biến mất và respawn
    public override void Die()
    {
        base.Die();
        // Nếu muốn thêm hiệu ứng riêng cho Mage khi chết, thêm ở đây
    }    public override void OnRespawn(Vector3 position)
    {
        // Call base class OnRespawn to reset basic states
        base.OnRespawn(position);

        // Reset combat timers
        nextAttackTime = 0f;

        // Reset all skill cooldowns
        lastFireballTime = -10f;
        lastSummonTime = -10f;
        lastRecallTime = -10f;

        // Reset any ongoing effects or states
        lastAttacker = null;

        // Clear and clean up minions
        foreach (var minionInfo in activeMinions.ToList())
        {
            if (minionInfo.gameObject != null)
            {
                Destroy(minionInfo.gameObject);
            }
        }
        activeMinions.Clear();

        foreach (var minionInfo in storedMinions.ToList())
        {
            if (minionInfo.gameObject != null)
            {
                Destroy(minionInfo.gameObject);
            }
        }
        storedMinions.Clear();

        // Reset animation flags and triggers
        if (animator != null)
        {
            animator.SetBool("IsCasting", false);
            animator.SetBool("IsAttacking", false);
            animator.ResetTrigger("Cast");
            animator.ResetTrigger("Summon");
            animator.ResetTrigger("Release");
            animator.ResetTrigger("Recall");
            animator.ResetTrigger("Attack");
        }

        // Reset rigidbody state
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        Debug.Log($"[Mage] OnRespawn: Combat states and minions reset");
    }

}
