using UnityEngine;

// Thin adapter: when the Damageable dies, tell the Dropable to drop. Keeps Dropable free of any "death"
// knowledge, so other causes (an interact, a timer, a depleted node) can drive Drop the same way.
[RequireComponent(typeof(Damageable), typeof(Dropable))]
[DisallowMultipleComponent]
public class DropOnDeath : MonoBehaviour
{
    Damageable _damageable;
    Dropable _dropable;

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _dropable = GetComponent<Dropable>();
    }

    void OnEnable()  { if (_damageable != null) _damageable.Died += OnDied; }
    void OnDisable() { if (_damageable != null) _damageable.Died -= OnDied; }

    void OnDied(object source) => _dropable.Drop(source);
}
