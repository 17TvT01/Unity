using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FireballProjectile : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage;
    public bool destroyOnHit = true;

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float destroyDelay = 0.1f;

    private Rigidbody2D rb;
    private bool hasHit = false;
    private float maxTravelDistance = 20f; // Khoảng cách tối đa cầu lửa bay được
    private Vector3 spawnPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
    }

    private void Update()
    {
        // Nếu cầu lửa bay quá xa thì tự hủy
        if (Vector3.Distance(transform.position, spawnPosition) > maxTravelDistance)
        {
            DestroyFireball();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // Check if we hit an enemy
        if (other.TryGetComponent<HealthBase>(out var healthComponent))
        {
            // Don't damage the caster
            if (healthComponent.gameObject.CompareTag("Player"))
                return;

            // Apply damage
            healthComponent.TakeDamage(damage, gameObject);
            hasHit = true;

            // Spawn hit effect if we have one
            SpawnHitEffect();
            DestroyFireball();
        }
        // Check if we hit a wall or ground
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            hasHit = true;
            SpawnHitEffect();
            DestroyFireball();
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab != null)
        {
            GameObject effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            // Tự động hủy hiệu ứng sau 1 giây
            Destroy(effect, 1f);
        }
    }

    private void DestroyFireball()
    {
        // Tắt renderer để fireball biến mất ngay lập tức
        if (TryGetComponent<SpriteRenderer>(out var renderer))
            renderer.enabled = false;

        // Disable collider and rigidbody
        if (TryGetComponent<Collider2D>(out var col))
            col.enabled = false;
        if (rb != null)
            rb.simulated = false;

        // Destroy ngay lập tức vì hiệu ứng nổ đã được tạo riêng
        Destroy(gameObject);
    }

    public void Initialize(float damage, Vector2 velocity)
    {
        this.damage = damage;
        if (rb != null)
        {
            rb.linearVelocity = velocity;
            // Rotate fireball to face direction of travel
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
}
