using UnityEngine;

public class InventoryToggle : MonoBehaviour
{
    public GameObject inventoryUI;  // Kéo GameObject Canvas vào đây

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryUI != null)
            {
                bool isActive = inventoryUI.activeSelf;
                inventoryUI.SetActive(!isActive);
            }
        }
    }
}
