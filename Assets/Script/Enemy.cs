using UnityEngine;
using VContainer;

public class Enemy : MonoBehaviour
{
    // Dependency được VContainer tiêm vào khi enemy được spawn.
    IInventory _inventory;
    string _enemyName;

    [Inject]
    public void Construct(IInventory inventory) => _inventory = inventory;

    // Đặt tên cho enemy (dữ liệu riêng của mỗi instance, không phải dependency).
    public void Init(string enemyName) => _enemyName = enemyName;

    void Start()
    {
        // Chạy SAU khi [Inject] đã tiêm _inventory, nên dùng được ngay.
        Debug.Log($"[Enemy] {_enemyName} xuất hiện!");
    }

    // Khi enemy chết thì rơi đồ vào inventory (chứng minh dependency hoạt động).
    public void Die()
    {
        Debug.Log($"[Enemy] {_enemyName} bị hạ!");
        _inventory.Add($"Chiến lợi phẩm từ {_enemyName}");
        Destroy(gameObject);
    }
}
