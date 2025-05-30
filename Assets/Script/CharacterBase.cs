using UnityEngine;

public abstract class CharacterBase : HealthBase, ISpawnable
{
    protected Rigidbody2D rb;
    protected Animator animator;
    protected bool isDead;
    protected bool facingRight = true;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    public override void Die()
    {
        if (isDead) return;
        isDead = true;
        
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        
        animator.SetTrigger("IsDie");
        StartCoroutine(HideAfterDeath());
    }

    protected virtual System.Collections.IEnumerator HideAfterDeath()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
        Object.FindFirstObjectByType<PlayerRespawnManager>()?.RespawnPlayer(gameObject);
    }

    public virtual void OnRespawn(Vector3 respawnPos)
    {
        isDead = false;
        transform.position = respawnPos;
        currentHP = maxHP;
        UpdateHealthUI();
        if (rb != null) rb.simulated = true;
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.SetBool("IsRun", false);
            animator.ResetTrigger("IsDie");
        }
        gameObject.SetActive(true);
    }

    protected virtual void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
