using UnityEngine;

public class ExpDropper : MonoBehaviour
{
    [SerializeField] private int expAmount = 10;
    
    // Gọi hàm này khi quái chết, truyền vào killer nếu có
    public void DropExp(GameObject killer = null)
    {
        PlayerBase player = null;
        if (killer != null)
        {
            player = killer.GetComponent<PlayerBase>();
        }
        if (player == null)
        {
            player = FindClosestPlayer();
        }
        if (player != null)
        {
            player.AddExp(expAmount);
            Debug.Log($"[ExpDropper] Awarded {expAmount} EXP to {player.name}");
        }
        else
        {
            Debug.LogWarning("[ExpDropper] No PlayerBase found to award EXP!");
        }
    }

    private PlayerBase FindClosestPlayer()
    {
        PlayerBase[] players = FindObjectsOfType<PlayerBase>();
        PlayerBase closest = null;
        float minDist = float.MaxValue;
        foreach (var p in players)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = p;
            }
        }
        return closest;
    }
}