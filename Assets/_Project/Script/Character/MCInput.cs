using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    IUISystem _ui;   // so a click that lands on a button is the button's, not a swing at the shopkeeper

    Camera _cam;

    // ONE SLOT, NOT A QUEUE, and it holds the LATEST refused press. A queue would replay a flurry of mashing
    // as a scripted combo the player never timed — press three times during a swing and watch three actions
    // come out on their own. One slot says "the thing you last asked for, if it is still nearly now".
    ICharacterCommand _buffered;
    InputKind _bufferedKind;   // re-checked at fire time: a cutscene that starts mid-window must still refuse it
    bool _bufferedAtCursor;    // re-AIMED at fire time, for the same reason — see AimAtCursor
    float _bufferedAt;

    [Inject]
    public void Construct(IInputGate gate, IUISystem ui)
    {
        _gate = gate;
        _ui = ui;
    }

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

    // THE TWO MOUSE BUTTONS. Kept apart from the dictionaries because a mouse button is not a Key and they
    // cannot hold it — but each is the SAME command object its keys carry, so one press is one action however
    // it was made: one cooldown, one buffered press, one place in a combo string.
    ICharacterCommand _attack;   // left
    ICharacterCommand _skill1;   // right

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
    // THE MOUSE FIGHTS AND THE THUMB DASHES. Left button attacks where the cursor is pointing, right button
    // casts skill 1, Space lunges: the hand that aims is the hand that acts, and the one thing the other hand
    // needs under it is the way out. That is the control scheme; everything below is an alternative to it.
    //
    // SKILL 1 AND NOT SKILL 2 on the right button, because there are two buttons and four things, so one of
    // them has to be the one that gets the hand. Skill 1 is the one every character has and the one pressed
    // most often — skill 2 is the late unlock, and reaching for a key is the right price for it.
    //
    // TWO KEYBOARD SETS behind that, both running attack, dash, skill 1, skill 2 in the order the HUD draws
    // them: Z-X-C-V under the fingers WASD leaves them next to, and J-K-L-; for a hand nowhere near those.
    // Every alternative for one action is the SAME command object: one cooldown, one buffered press, however
    // it was asked for.
    //
    // NO KEY AIMS. The cursor turns the character on a mouse CLICK and nowhere else — see AimAtCursor — so the
    // same skill leaves along the pointer off the right button and along the current facing off its key. A key
    // that re-aimed would mean the pointer, left wherever it happens to be, quietly overriding the direction
    // the player is walking.
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

            var keys = KeysOf(ability.Which);
            if (keys.Length > 0 && _skills.ContainsKey(keys[0]))
            {
                Debug.LogError($"[{nameof(MCInput)}] two abilities claim slot {ability.Which} on " +
                               $"'{character.name}' — the second one can never be pressed.", ability);
                continue;
            }

            var command = new SkillCommand(ability);
            foreach (var key in keys) _skills[key] = command;   // the SAME command, so one skill either way
            if (ability.Which == AbilitySlot.Skill1) _skill1 = command;
        }

        // A character with nothing in the Attack slot cannot attack at all, which is a wiring mistake and not a
        // design: every body needs either a blow in that slot or a combo in front of one.
        if (attack == null)
            Debug.LogError($"[{nameof(MCInput)}] nothing in the Attack slot on '{character.name}' — the attack " +
                           "button does nothing. Put an AttackAbility or a ComboAttack there.", character);

        // One command object behind the button and both keys, so an attack is one attack however it was asked
        // for — one cooldown, one buffered press, one place in a combo string.
        var swing = new AttackCommand(attack);
        _attack = swing;
        _pressed[Key.J] = swing;
        _pressed[Key.Z] = swing;
    }

    // EVERY KEY A SLOT ANSWERS TO, in one list rather than one function per set: which keys reach a slot is a
    // single fact, and two lists to keep level is how a key ends up meaning two things.
    //
    // SPACE IS THE DASH, on its own, under the thumb of the hand that is not on the mouse. It is the one thing
    // that has to be reachable without looking, so it gets the biggest key on the board rather than a place in
    // a row. The rest are the two rows: X-C-V beside the fingers WASD already holds, and K-L-; for a hand
    // nowhere near them — semicolon last because slot 2 is the one a character is least likely to have, so the
    // reach is spent on the rarest thing rather than on the dash everybody carries.
    static Key[] KeysOf(AbilitySlot slot) => slot switch
    {
        AbilitySlot.Dash => new[] { Key.Space, Key.X, Key.K },
        AbilitySlot.Skill1 => new[] { Key.C, Key.L },
        AbilitySlot.Skill2 => new[] { Key.V, Key.Semicolon },
        _ => System.Array.Empty<Key>(),
    };

    public void AccumulateMove(Vector2 direction) => _localMove += direction;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        Steer(kb);

        // BEFORE this frame's presses, so a press held over from last frame goes out the instant it becomes
        // possible rather than a frame later. A fresh press that also fails simply replaces it below.
        RetryBuffered();

        if (_gate == null || _gate.Allows(InputKind.Attack))
        {
            // THE LEFT BUTTON SWINGS WHERE IT POINTS. Always aimed, like every click — see AimAtCursor.
            if (_attack != null && Clicked(Mouse.current?.leftButton))
                Press(_attack, InputKind.Attack, atCursor: true);

            foreach (var b in _pressed)
                if (kb[b.Key].wasPressedThisFrame) Press(b.Value, InputKind.Attack);
        }

        if (_gate == null || _gate.Allows(InputKind.Skill))
        {
            // AIMED, the same as the left button: a CLICK is an aim, whichever button it was made with. See
            // AimAtCursor — what does not aim is a KEY.
            if (_skill1 != null && Clicked(Mouse.current?.rightButton))
                Press(_skill1, InputKind.Skill, atCursor: true);

            foreach (var b in _skills)
                if (kb[b.Key].wasPressedThisFrame) Press(b.Value, InputKind.Skill);
        }
    }

    // THE KEYS ARE THE ONLY THING THAT WALKS THE CHARACTER. The mouse points and hits; it does not drive.
    // One hand steers and the other aims, and the two are never saying the same thing — which is what lets a
    // blow be thrown one way while the feet carry the body another.
    void Steer(Keyboard kb)
    {
        if (_gate != null && !_gate.Allows(InputKind.Move)) return;

        _localMove = Vector2.zero;
        foreach (var b in _held)
            if (kb[b.Key].isPressed) b.Value.Execute();

        if (_localMove == Vector2.zero) return;

        var cam = CameraViewDir.Transform;
        if (cam == null) return;

        // Turned against the camera, so W is up the SCREEN however the camera has been orbited.
        float camYaw = cam.eulerAngles.y;
        var world = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(_localMove.x, 0f, _localMove.y);
        character.Move(new Vector2(world.x, world.z));
    }

    // WHAT THE CURSOR IS FOR. A CLICK AIMS, A KEY DOES NOT, and that is the whole rule: clicking is the player
    // stating a direction at the moment they ask for the action — the cursor is what they were pointing at and
    // the only thing they were saying. A key is not; the pointer may have been sitting untouched for a minute,
    // and turning the character onto it would be the game acting on something nobody said. So a press made
    // with a key leaves along the facing as it stands, which is the one the player has been steering.
    //
    // TURNED IN THE SAME FRAME IT FIRES, immediately before the press: Face writes FacingDir and the ability
    // reads it — the same order an ambush predator's bite depends on (EnemyAI.TickAttack).
    void AimAtCursor()
    {
        if (!GroundUnderCursor(out Vector3 point)) return;
        character.Face(point - character.transform.position);
    }

    // Where the cursor is pointing, on the ground. Read off the plane the character is STANDING ON rather than
    // out of a raycast, because there is nothing to raycast — the ground is a TerrainGrid and the walls are
    // generated per body inside CollisionWorld, and neither one is a collider. The plane is the right answer
    // anyway: it is the surface the character can actually walk on and aim across.
    bool GroundUnderCursor(out Vector3 point)
    {
        point = default;

        var mouse = Mouse.current;
        var cam = ResolveCamera();
        if (mouse == null || cam == null) return false;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        var ground = new Plane(Vector3.up, character.transform.position);
        if (!ground.Raycast(ray, out float distance)) return false;   // pointing at the sky, or past the horizon

        point = ray.GetPoint(distance);
        return true;
    }

    // A press of a mouse button that was MEANT FOR THE WORLD. A click landing on the HUD belongs to whatever
    // is under it — swinging at a shop button, or walking off toward what happens to be behind it, is the
    // classic bug of buying something and then lunging at the shopkeeper.
    bool Clicked(ButtonControl button)
    {
        if (button == null || !button.wasPressedThisFrame) return false;
        return _ui == null || !_ui.IsPointerOverUI();
    }

    Camera ResolveCamera()
    {
        if (_cam != null) return _cam;
        var t = CameraViewDir.Transform;   // the camera the world is actually drawn from
        _cam = t != null ? t.GetComponent<Camera>() : Camera.main;
        return _cam;
    }

    // Try it now; keep it if it would not go. The press is remembered from when it was MADE, not from when it
    // was last retried — otherwise every failed attempt would renew the window and a button held down against
    // a long cooldown would fire the moment it ended, however long ago the player asked.
    void Press(ICharacterCommand command, InputKind kind, bool atCursor = false)
    {
        if (Throw(command, atCursor)) { _buffered = null; return; }   // it went; anything older is stale

        _buffered = command;
        _bufferedKind = kind;
        _bufferedAtCursor = atCursor;
        _bufferedAt = Time.time;
    }

    // Throw it — aimed, if it was asked for with the cursor — and TAKE THE TURN BACK if it would not go. The
    // aim has to be set before Execute, because the ability leaves along FacingDir in the same frame, so
    // putting the facing back afterwards is the only way a refused press can leave nothing behind.
    //
    // IT HAS TO LEAVE NOTHING BEHIND, because a button that is not ready yet is a button that gets pressed a
    // lot. Without this, mashing a cooling skill would spin the character to face the cursor over and over
    // while doing nothing else at all — the game answering a press it just refused.
    bool Throw(ICharacterCommand command, bool atCursor)
    {
        if (!atCursor) return command.Execute();

        Vector3 was = character.FacingDir;
        AimAtCursor();
        if (command.Execute()) return true;

        character.Face(was);
        return false;
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

        // AIMED WHERE THE CURSOR IS NOW, not where it was when the press was made. A buffered press is one the
        // player made a moment early, and what they asked for was "throw it the instant you can, at what I am
        // pointing at" — a swing that came out aimed at the spot the cursor has since left would be the
        // player's own early press working against them. A retry that is refused again turns nothing, the
        // same as the press that made it.
        if (Throw(_buffered, _bufferedAtCursor)) _buffered = null;
    }
}
