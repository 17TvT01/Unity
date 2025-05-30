using UnityEngine;
using System.Collections;

public class Dragon : HealthBase
{
    public float moveSpeed = 2f;
    public float attackRange = 3f;
    public float attackCooldown = 2f;
    public float fireBreathCooldown = 5f;
    public float fireBreathRange = 6f;
    public float attackDamage = 30f;
    public float fireBreathDamage = 15f;
    public LayerMask playerLayer;
    private float lastAttackTime = 0f;
    private float lastFireBreathTime = 0f;
    private Transform player;
    private Animator animator;
    private bool facingRight = true;
    private bool isDead = false;

    protected override void Start()
    {
        base.Start();
        animator = GetComponent<Animator>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distToPlayer = Vector2.Distance(transform.position, player.position);
        if (distToPlayer > attackRange)
        {
            // Di chuyển về phía player
            Vector3 dir = (player.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            animator.SetBool("IsRun", true);
            // Flip hướng
            if (dir.x > 0 && !facingRight) Flip();
            else if (dir.x < 0 && facingRight) Flip();
        }
        else
        {
            animator.SetBool("IsRun", false);
            // Tấn công thường
            if (Time.time - lastAttackTime > attackCooldown)
            {
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                StartCoroutine(DealAttackDamage());
            }
        }

        // Tấn công đặc biệt: phun lửa khi máu thấp hoặc cooldown xong
        if (currentHP < maxHP * 0.5f && Time.time - lastFireBreathTime > fireBreathCooldown)
        {
            animator.SetTrigger("FireBreath");
            lastFireBreathTime = Time.time;
            StartCoroutine(DealFireBreathDamage());
        }
    }

    private IEnumerator DealAttackDamage()
    {
        yield return new WaitForSeconds(0.5f); // Delay cho animation
        Collider2D hit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (hit != null)
        {
            PlayerBase playerScript = hit.GetComponent<PlayerBase>();
            if (playerScript != null)
                playerScript.TakeDamage(attackDamage);
        }
    }

    private IEnumerator DealFireBreathDamage()
    {
        yield return new WaitForSeconds(0.7f); // Delay cho animation
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, fireBreathRange, playerLayer);
        foreach (var hit in hits)
        {
            PlayerBase playerScript = hit.GetComponent<PlayerBase>();
            if (playerScript != null)
                playerScript.TakeDamage(fireBreathDamage);
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= 1;
        transform.localScale = scale;
    }

    public override void Die()
    {
        if (isDead) return;
        isDead = true;
        animator.SetTrigger("IsDie");
        StartCoroutine(HideAndDestroyAfterDeath());
    }

    private IEnumerator HideAndDestroyAfterDeath()
    {
        yield return new WaitForSeconds(2f); // Thời gian chờ animation death
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fireBreathRange);
    }
#endif
}
