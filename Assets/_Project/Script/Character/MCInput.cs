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
    IUISystem _ui;   // so a right-click that lands on a button is the button's, not the ground's

    // Where the player last right-clicked, if it is still standing. Owned HERE and not by the unit: it is
    // something the player asked for, the same kind of thing as a key being held, and the unit has no more
    // business remembering it than it has remembering which key is down. It also means a body that dies
    // takes its orders with it.
    readonly MoveOrder _order = new MoveOrder();
    bool _dragging;   // the right button went down on the world and has not come up — see TakeClick
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
    // TWO HANDS, TWO SETS, both running attack, dash, skill 1, skill 2 in the order the HUD draws them. The
    // left hand plays with the right one on the mouse: Z-X-C-V under the fingers already resting there from
    // WASD, with Space as a second attack under the thumb. J-K-L-; is the keyboard-only set, for playing with
    // no hand on the mouse at all. Every alternative for one action is the SAME command object: one cooldown,
    // one buffered press, however it was asked for.
    //
    // The two sets are not identical in one respect: the left hand's keys AIM — see AimedKeys.
    //
    // THE MOUSE THROWS NOTHING. Its right button walks the character and that is all it does: an attack on the
    // left button would be a second thing to do with the hand that is already aiming.
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

            var command = new SkillCommand(ability);
            _skills[key] = command;

            var alt = AltKeyOf(ability.Which);
            if (alt != Key.None) _skills[alt] = command;   // the SAME command, so one skill either way
        }

        // A character with nothing in the Attack slot cannot attack at all, which is a wiring mistake and not a
        // design: every body needs either a blow in that slot or a combo in front of one.
        if (attack == null)
            Debug.LogError($"[{nameof(MCInput)}] nothing in the Attack slot on '{character.name}' — the attack " +
                           "button does nothing. Put an AttackAbility or a ComboAttack there.", character);

        // One command object behind all three keys, so an attack is one attack however it was asked for — one
        // cooldown, one buffered press, one place in a combo string.
        var swing = new AttackCommand(attack);
        _pressed[Key.J] = swing;
        _pressed[Key.Space] = swing;
        _pressed[Key.Z] = swing;
    }

    // The keyboard-only set, running rightwards from the attack key on J: dash, then the two skills. Semicolon
    // for the last of them because it is the one a character is least likely to have — the reach is spent on
    // the rarest thing rather than on the dash every character carries.
    static Key KeyOf(AbilitySlot slot) => slot switch
    {
        AbilitySlot.Dash => Key.K,
        AbilitySlot.Skill1 => Key.L,
        _ => Key.Semicolon,
    };

    // The left hand's copy of the same three, under the fingers WASD leaves them next to — the set that
    // matters while the other hand is on the mouse, and the set that AIMS (see AimedKeys).
    static Key AltKeyOf(AbilitySlot slot) => slot switch
    {
        AbilitySlot.Dash => Key.X,
        AbilitySlot.Skill1 => Key.C,
        AbilitySlot.Skill2 => Key.V,
        _ => Key.None,
    };

    // THE KEYS PRESSED WITH A HAND ON THE MOUSE. Space and Z-X-C-V sit under the left hand while the right one
    // holds the cursor, so a press there is made while already pointing at something: it turns the character at
    // the cursor first and throws from there. J-K-L is the keyboard-only set and stays that way — nobody
    // pressing L is looking at where the pointer happens to have been left, and swinging at it would be the
    // game acting on something the player never said.
    static readonly HashSet<Key> AimedKeys = new HashSet<Key> { Key.Space, Key.Z, Key.X, Key.C, Key.V };

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
            foreach (var b in _pressed)
                if (kb[b.Key].wasPressedThisFrame) PressKey(b.Key, b.Value, InputKind.Attack);
        }

        if (_gate == null || _gate.Allows(InputKind.Skill))
        {
            foreach (var b in _skills)
                if (kb[b.Key].wasPressedThisFrame) PressKey(b.Key, b.Value, InputKind.Skill);
        }
    }

    // The two ways the player asks the character to move, and which of them wins. A HAND ON THE KEYS BEATS A
    // CLICK, always: steering now is a plainer statement of intent than a destination chosen a moment ago, and
    // a player who grabs the keys to dodge something must not be dragged back on course when they let go.
    void Steer(Keyboard kb)
    {
        if (_gate != null && !_gate.Allows(InputKind.Move))
        {
            // Dropped, not held: the controls come back at the end of a cutscene or a shop, and a character
            // that then strolls off toward somewhere asked for minutes ago is acting on an intention that has
            // passed — the same reason a buffered press expires.
            _order.Clear();
            return;
        }

        TakeClick();

        _localMove = Vector2.zero;
        foreach (var b in _held)
            if (kb[b.Key].isPressed) b.Value.Execute();

        if (_localMove != Vector2.zero)
        {
            // The keys end the DRAG as well as the destination. Without that, a held button would re-issue the
            // point every frame the player was steering past it, and let go of the keys to find the character
            // strolling back to wherever the cursor had drifted.
            _order.Clear();
            _dragging = false;

            var cam = CameraViewDir.Transform;
            if (cam == null) return;

            float camYaw = cam.eulerAngles.y;
            var world = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(_localMove.x, 0f, _localMove.y);
            character.Move(new Vector2(world.x, world.z));
            return;
        }

        if (!_order.Active) return;

        // THE ORDER WAITS OUT ANYTHING THAT HAS THE BODY. Mid-swing the feet are pinned already (DynamicUnit
        // zeroes velocity while busy), so steering could only turn the character — off whatever it is hitting
        // and back toward the destination, mid-blow; and a knockback throws input away outright. Held rather
        // than dropped, so the walk picks up afterwards, and held rather than stepped, so neither one counts
        // against the stall that ends an order going nowhere.
        if (character.IsBusy || character.IsKnocked) { _order.Hold(character.transform.position); return; }

        var step = _order.Step(character.transform.position, character.Speed, Time.deltaTime);
        if (step != Vector2.zero) character.Move(step);
    }

    // Right-click: walk to that spot.
    //
    // The click is not checked against the map. Somewhere unwalkable is a fine thing to ask for — the walk
    // toward it stops at the bank, and MoveOrder gives up once it stops getting anywhere.
    void TakeClick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // HELD, NOT MERELY PRESSED. A button held down is a live instruction, the same as a key held: the
        // destination follows the cursor for as long as it is down, so leading the character around is one
        // drag rather than a rattle of clicks. Release leaves the last point standing and the walk finishes it.
        //
        // The DRAG is what is tracked, not the button: a press that was refused (it landed on the HUD) must not
        // become a walk the moment the cursor slides off the button, so nothing happens until the next press
        // that was actually meant for the world.
        if (Clicked(mouse.rightButton)) _dragging = true;
        else if (!mouse.rightButton.isPressed) { _dragging = false; return; }
        else if (!_dragging) return;

        if (!GroundUnderCursor(out Vector3 point)) return;

        _order.Set(point);
    }

    // WHAT THE CURSOR IS FOR. A blow thrown blind leaves along whatever way the character already happened to
    // point; aimed, it leaves where the player is looking. Every press made with a hand on the mouse goes
    // through here — see AimedKeys.
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
        if (Throw(command, atCursor)) { Fired(); return; }

        _buffered = command;
        _bufferedKind = kind;
        _bufferedAtCursor = atCursor;
        _bufferedAt = Time.time;
    }

    // A key press, aimed at the cursor if it is one of the aiming row.
    void PressKey(Key key, ICharacterCommand command, InputKind kind)
        => Press(command, kind, AimedKeys.Contains(key));

    // Throw it — aimed, if it was asked for with a hand on the mouse — and TAKE THE TURN BACK if it would not
    // go. The aim has to be set before Execute, because the ability leaves along FacingDir in the same frame,
    // so putting the facing back afterwards is the only way a refused press can leave nothing behind.
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
        if (Throw(_buffered, _bufferedAtCursor)) Fired();
    }

    // One place, so a press that went straight out and one that waited in the buffer settle the same way.
    void Fired()
    {
        _buffered = null;   // it went; anything older is stale by definition

        // ACTING ENDS THE WALK. Throwing a blow is the player taking hold of the character — and where they
        // are standing when it lands is most of what they were deciding. Walking on afterwards would carry
        // them out of the fight they just chose to be in, toward a spot picked before it started.
        _order.Clear();
    }
}
