using System.Text;
using TMPro;
using UnityEngine;

// The number floating over a pay gate: what has gone in, out of what it wants. Put it on a child of the
// gate and drop a TMP_Text on it.
//
// Named for the number and not for the gate, because that is all it is. The slot, its rim and its sign are
// plain sprites with no script at all, so a "PayGateView" would be claiming to render something it does not
// touch — and View already means the whole look of a thing here (CharacterView, EnemyView, UnitView).
//
// It reads the gate rather than being told by it, and only redraws when the gate says something changed. A
// slot standing in an empty field costs one disabled GameObject and nothing per frame.
[DisallowMultipleComponent]
public class PayGateCounter : MonoBehaviour
{
    [SerializeField] PayGate gate;
    [SerializeField] TMP_Text label;

    [Tooltip("Hide the whole thing once the gate is paid for. Off if the slot has finished art of its own " +
             "that should keep its number.")]
    [SerializeField] bool hideWhenPaid = true;

    readonly StringBuilder _sb = new StringBuilder(32);

    void Reset()
    {
        gate = GetComponentInParent<PayGate>();
        label = GetComponentInChildren<TMP_Text>();
    }

    void Awake()
    {
        if (gate == null) gate = GetComponentInParent<PayGate>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        if (gate != null) gate.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (gate != null) gate.Changed -= Refresh;
    }

    // Start as well as OnEnable: the gate reads its saved deposits in its own Start, and which of the two
    // runs first is not something to rely on.
    void Start() => Refresh();

    void Refresh()
    {
        if (gate == null || label == null) return;

        if (gate.Paid && hideWhenPaid)
        {
            label.gameObject.SetActive(false);
            return;
        }
        label.gameObject.SetActive(true);

        // One line per resource the price asks for. With the usual single-resource gate that is just "12/20".
        _sb.Clear();
        var cost = gate.Cost;
        if (cost != null)
            foreach (var c in cost)
            {
                if (c.resource == null) continue;
                if (_sb.Length > 0) _sb.Append('\n');
                _sb.Append(Mathf.Min(gate.Deposited(c), c.amount)).Append('/').Append(c.amount);
            }

        label.text = _sb.ToString();
    }
}
