using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class MCInput : MonoBehaviour
{
    [SerializeField] MCController character;

    IInputGate _gate;

    [Inject]
    public void Construct(IInputGate gate) => _gate = gate;

    void Start()
    {
        if (_gate == null)
            Debug.LogError($"[{nameof(MCInput)}] IInputGate not injected — add this GameObject to GameScope's Auto Inject Game Objects; input gating is disabled.", this);
    }

    readonly Dictionary<Key, ICharacterCommand> _held = new Dictionary<Key, ICharacterCommand>();
    readonly Dictionary<Key, ICharacterCommand> _pressed = new Dictionary<Key, ICharacterCommand>();

    // Kept apart from _pressed so the gate can allow one and refuse the other. They are two different
    // permissions — a scripted moment may well want you able to swing where you stand but not to dash out of
    // it — and one dictionary could only ever be allowed or refused as a whole.
    readonly Dictionary<Key, ICharacterCommand> _skills = new Dictionary<Key, ICharacterCommand>();
    Vector2 _localMove;

    void Awake()
    {
        if (character == null) character = GetComponent<MCController>();

        var up = new MoveCommand(this, Vector2.up);
        var down = new MoveCommand(this, Vector2.down);
        var left = new MoveCommand(this, Vector2.left);
        var right = new MoveCommand(this, Vector2.right);

        _held[Key.W] = up;
        _held[Key.S] = down;
        _held[Key.A] = left;
        _held[Key.D] = right;
        _held[Key.UpArrow] = up;
        _held[Key.DownArrow] = down;
        _held[Key.LeftArrow] = left;
        _held[Key.RightArrow] = right;

        // J as well as Space, and J is the one that matters: it puts attack beside K and L so the three
        // abilities sit under three fingers in the order the HUD draws them. Space stays because it is what
        // hands already reach for, and one command object answers both — the same press either way.
        var attack = new AttackCommand(character);
        _pressed[Key.Space] = attack;
        _pressed[Key.J] = attack;

        BindSkills();
    }

    // Bound by SLOT, not by which component happens to be there: every character carries its own skills, so
    // the key has to mean "your first skill" rather than "the dash". Found on the body rather than dragged in,
    // because a character with no second skill yet must simply have nothing on that key — a serialized slot
    // would be an empty reference to explain instead.
    void BindSkills()
    {
        foreach (var skill in character.GetComponentsInChildren<CharacterSkill>(true))
        {
            var key = skill.Which == CharacterSkill.Slot.One ? Key.K : Key.L;
            if (_skills.ContainsKey(key))
            {
                Debug.LogError($"[{nameof(MCInput)}] two skills claim slot {skill.Which} on '{character.name}' — " +
                               "the second one can never be pressed.", skill);
                continue;
            }
            _skills[key] = new SkillCommand(skill);
        }
    }

    public void AccumulateMove(Vector2 direction) => _localMove += direction;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (_gate == null || _gate.Allows(InputKind.Move))
        {
            _localMove = Vector2.zero;
            foreach (var b in _held)
                if (kb[b.Key].isPressed) b.Value.Execute();

            var cam = CameraViewDir.Transform;
            if (_localMove != Vector2.zero && cam != null)
            {
                float camYaw = cam.eulerAngles.y;
                var world = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(_localMove.x, 0f, _localMove.y);
                character.Move(new Vector2(world.x, world.z));
            }
        }

        if (_gate == null || _gate.Allows(InputKind.Attack))
        {
            foreach (var b in _pressed)
                if (kb[b.Key].wasPressedThisFrame) b.Value.Execute();
        }

        if (_gate == null || _gate.Allows(InputKind.Skill))
        {
            foreach (var b in _skills)
                if (kb[b.Key].wasPressedThisFrame) b.Value.Execute();
        }
    }
}
