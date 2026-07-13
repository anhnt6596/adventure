using UnityEngine.InputSystem;
using VContainer.Unity;

public class GameController : IStartable, ITickable
{
    readonly IInventory _inventory;

    public GameController(IInventory inventory)
    {
        _inventory = inventory;
    }

    public void Start()
    {
        _inventory.Add("Thanh kiếm gỉ");
    }

    public void Tick()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            _inventory.Add("Vàng");
    }
}
