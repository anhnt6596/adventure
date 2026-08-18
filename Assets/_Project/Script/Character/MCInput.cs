using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class MCInput : MonoBehaviour
{
    [SerializeField] MCController character;

    // How long a press that could not run is remembered and offered again — the amount a player may press
    // EARLY and still be heard. Long enough that the last moments of a swing are not a dead zone, short enough
    // that one press cannot fire an action later than the player would still call "now".
    //
    // A CONSTANT, NOT A FIELD. It is one number for the whole game: it describes how forgiving the game is,
    // which is not something one character should be able to disagree with the next about. Per-prefab it would
    // be four copies to keep level and three of them wrong the day somebody tunes the fourth.
    const float BufferWindow = 0.2f;

    IInputGate _gate;

    // ONE SLOT, NOT A QUEUE, and it holds the LATEST refused press. A queue would replay a flurry of mashing
    // as a scripted combo the player never timed — press three times during a swing and watch three actions
    // come out on their own. One slot says "the thing you last asked for, if it is still nearly now".
    ICharacterCommand _buffered;
    InputKind _bufferedKind;   // re-checked at fire time: a cutscene that starts mid-window must still refuse it
    float _bufferedAt;

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

        BindAbilities();
    }

    // Bound by SLOT, not by which component happens to be there: every character carries its own abilities, so
    // a key has to mean "your dash" rather than "the DashSkill". Found on the body rather than dragged in,
    // because a character with no second skill yet must simply have nothing on that key — a serialized slot
    // would be an empty reference to explain instead.
    //
    // J and Space are both the attack, and J is the one that matters: it puts attack beside K, L and ; so the
    // four abilities sit under four fingers in the order the HUD draws them. Space stays because it is what
    // hands already reach for, and one command object answers both — the same press either way.
    void BindAbilities()
    {
        CharacterSkill attack = null;

        foreach (var ability in character.GetComponentsInChildren<CharacterSkill>(true))
        {
            // A piece of something bigger — a combo step — reached only by the ability that drives it. Not an
            // error and not a warning: this is what most attack components on a body are.
            if (ability.Which == AbilitySlot.None) continue;

            if (ability.Which == AbilitySlot.Attack)
            {
                if (attack != null)
                {
                    Debug.LogError($"[{nameof(MCInput)}] two abilities claim the attack button on " +
                                   $"'{character.name}' — the second one can never be pressed.", ability);
                    continue;
                }
                attack = ability;
                continue;
            }

            var key = KeyOf(ability.Which);
            if (_skills.ContainsKey(key))
            {
                Debug.LogError($"[{nameof(MCInput)}] two abilities claim slot {ability.Which} on " +
                               $"'{character.name}' — the second one can never be pressed.", ability);
                continue;
            }
            _skills[key] = new SkillCommand(ability);
        }

        // A character with nothing in the Attack slot cannot attack at all, which is a wiring mistake and not a
        // design: every body needs either a blow in that slot or a combo in front of one.
        if (attack == null)
            Debug.LogError($"[{nameof(MCInput)}] nothing in the Attack slot on '{character.name}' — the attack " +
                           "button does nothing. Put an AttackAbility or a ComboAttack there.", character);

        var command = new AttackCommand(attack);
        _pressed[Key.Space] = command;
        _pressed[Key.J] = command;
    }

    // Semicolon for the second skill because hardly any character has one: the three abilities every character
    // does have keep the three keys the hand is already resting on, and the rare fourth is the reach.
    static Key KeyOf(AbilitySlot slot) => slot switch
    {
        AbilitySlot.Dash => Key.K,
        AbilitySlot.Skill1 => Key.L,
        _ => Key.Semicolon,
    };

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

        // BEFORE this frame's presses, so a press held over from last frame goes out the instant it becomes
        // possible rather than a frame later. A fresh press that also fails simply replaces it below.
        RetryBuffered();

        if (_gate == null || _gate.Allows(InputKind.Attack))
        {
            foreach (var b in _pressed)
                if (kb[b.Key].wasPressedThisFrame) Press(b.Value, InputKind.Attack);
        }

        if (_gate == null || _gate.Allows(InputKind.Skill))
        {
            foreach (var b in _skills)
                if (kb[b.Key].wasPressedThisFrame) Press(b.Value, InputKind.Skill);
        }
    }

    // Try it now; keep it if it would not go. The press is remembered from when it was MADE, not from when it
    // was last retried — otherwise every failed attempt would renew the window and a button held down against
    // a long cooldown would fire the moment it ended, however long ago the player asked.
    void Press(ICharacterCommand command, InputKind kind)
    {
        if (command.Execute())
        {
            _buffered = null;   // it went; anything older is stale by definition
            return;
        }

        _buffered = command;
        _bufferedKind = kind;
        _bufferedAt = Time.time;
    }

    void RetryBuffered()
    {
        if (_buffered == null) return;

        // Dropped on expiry whether or not it could have run: a press the player has stopped thinking of as
        // "now" must not go off, and holding it any longer is the game acting on an intention that has passed.
        if (Time.time - _bufferedAt > BufferWindow)
        {
            _buffered = null;
            return;
        }

        if (_gate != null && !_gate.Allows(_bufferedKind)) return;   // still remembered, just not allowed yet
        if (_buffered.Execute()) _buffered = null;
    }
}
