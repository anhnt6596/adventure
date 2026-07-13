using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

public class EnemySpawner : IStartable, ITickable
{
    // IObjectResolver luôn có sẵn trong container, KHÔNG cần đăng ký.
    readonly IObjectResolver _resolver;
    int _count;
    Enemy _lastSpawned;

    public EnemySpawner(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    public void Start()
    {
        Spawn(); // spawn 1 con lúc bắt đầu
    }

    public void Tick()
    {
        if (Keyboard.current == null) return;

        // Nhấn E: spawn thêm enemy
        if (Keyboard.current.eKey.wasPressedThisFrame)
            Spawn();

        // Nhấn K: giết con vừa spawn (nó rơi đồ vào inventory)
        if (Keyboard.current.kKey.wasPressedThisFrame && _lastSpawned != null)
            _lastSpawned.Die();
    }

    void Spawn()
    {
        _count++;

        // Tạo enemy bằng code (demo không cần prefab).
        var go = new GameObject($"Enemy_{_count}");
        var enemy = go.AddComponent<Enemy>();
        enemy.Init($"Goblin #{_count}");

        // Điểm mấu chốt: tiêm dependencies vào enemy vừa tạo.
        // InjectGameObject sẽ chạy [Inject] cho mọi MonoBehaviour trong object.
        _resolver.InjectGameObject(go);

        _lastSpawned = enemy;

        // --- Trong game thật, dùng prefab như sau (cần kéo prefab vào Inspector): ---
        // var enemy = _resolver.Instantiate(_enemyPrefab);
        // _resolver.Instantiate đã tự động tiêm, không cần gọi InjectGameObject.
    }
}
