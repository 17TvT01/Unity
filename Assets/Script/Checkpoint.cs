using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Effects")]
    public GameObject activationEffect; // Hiệu ứng khi kích hoạt checkpoint
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerRespawnManager respawnSystem = FindObjectOfType<PlayerRespawnManager>();
            if (respawnSystem != null)
            {
                respawnSystem.SetCheckpoint(this.transform);
                
                // Hiệu ứng kích hoạt
                if (activationEffect != null)
                {
                    Instantiate(activationEffect, transform.position, Quaternion.identity);
                }
            }
        }
    }
}
