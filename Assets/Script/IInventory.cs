using System.Collections.Generic;
using UnityEngine;

public interface IInventory
{
    void Add(string item);
}

public class Inventory : IInventory
{
    readonly List<string> _items = new();
    public void Add(string item)
    {
        _items.Add(item);
        Debug.Log($"Nhặt được: {item} (tổng {_items.Count} món)");
    }
}
