using UnityEngine;
using VContainer;

public class Player : MonoBehaviour
{
    IInventory _inventory;

    [Inject]
    public void Construct(IInventory inventory) => _inventory = inventory;
}