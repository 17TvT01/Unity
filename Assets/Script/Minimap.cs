using UnityEngine;

public class Minimap : MonoBehaviour
{
    public Camera minimapCamera;
    public float height = 30f;  // Độ cao của camera minimap
    private Transform target;    // Nhân vật để follow

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target == null || minimapCamera == null) return;

        // Cập nhật vị trí camera minimap
        Vector3 newPos = target.position;
        newPos.y = height;  // Giữ camera ở độ cao cố định
        newPos.z = target.position.z - 10f;  // Offset cho camera
        transform.position = newPos;
    }
}
