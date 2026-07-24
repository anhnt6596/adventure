using System.Collections.Generic;
using UnityEngine;
using VContainer;

// Lives on the actor. Each frame, asks the field which zones it's standing in and fires
// enter/exit. Approaching a zone triggers it — no picking, no priority.
[RequireComponent(typeof(MCController))]
public class Interactor : MonoBehaviour
{
    InteractField _field;
    MCController _actor;

    readonly HashSet<InteractZone> _inside = new HashSet<InteractZone>();
    readonly List<InteractZone> _now = new List<InteractZone>();
    readonly List<InteractZone> _left = new List<InteractZone>();

    [Inject]
    public void Construct(InteractField field) => _field = field;

    void Awake() => _actor = GetComponent<MCController>();

    void Start()
    {
        if (_field == null)
            Debug.LogError($"[{nameof(Interactor)}] InteractField not injected — add this GameObject to GameScope's Auto Inject Game Objects.", this);
    }

    void Update()
    {
        if (_field == null) return;

        _field.QueryAt(transform.position, _now);

        for (int i = 0; i < _now.Count; i++)
            if (_inside.Add(_now[i]))
                _now[i].OnActorEnter(_actor);

        _left.Clear();
        foreach (var z in _inside)
            if (z == null || !_now.Contains(z)) _left.Add(z);

        for (int i = 0; i < _left.Count; i++)
        {
            _inside.Remove(_left[i]);
            _left[i]?.OnActorExit(_actor);
        }
    }
}
