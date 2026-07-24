# AI quái — FSM khung chung + chiến thuật cắm

Một bộ máy trạng thái **dùng chung cho mọi quái**, và các **chiến thuật cắm rời** để mỗi loài hành xử khác nhau. Theo quy ước "runtime là code thuần" — không behavior-tree SO, không data-driven.

## Triết lý: tách 2 lớp

- **Khung FSM** (`EnemyAI`) — states + luật chuyển, **cố định**, chỉ đọc *số* từ `EnemyConfig`.
- **Chiến thuật** (4 strategy plain-C#) — lấp vào các chỗ khác nhau giữa các loài. Một quái = subclass `EnemyAI` chỉ **chọn** strategy.

Đổi hành vi = đổi/viết strategy. Khung và các quái khác không đụng.

## Các mảnh

### `AIContext` — blackboard
Mọi state/strategy đọc chung: `controller` (EnemyController — Move/Attack/Team), `config` (EnemyConfig), `attack` (IAttack — skill trên thân), `home` (điểm spawn), `target` (IDamageable hiện tại). Helpers: `DistanceToTarget()`, `HasLiveTarget`, `AttackRange` (= `attack.Range`), `FindHostile(r)` (query CombatWorld team-khác).

### `EnemyAI` — máy FSM (MonoBehaviour trên prefab quái)
Bốn state, transition dùng số config:

| State | Hành vi (uỷ strategy) | Chuyển đi |
|---|---|---|
| **Idle** | `Idle.Tick()` | `Aggro.Detect()` ra target → Chase · *(bị đánh → Chase)* |
| **Chase** | `controller.Move(Pursuit.DirTo(target))` | ≤ `AttackRange` → Attack · > `leashRadius` → Forget · target chết → Forget |
| **Attack** | `Attack.Tick()` (gọi `controller.Attack()`) | > `AttackRange`+lề (hysteresis) → Chase · > `leashRadius` → Forget |
| **Forget** | đứng yên, đếm `forgetTime` (vẫn NHỚ target) | target về trong `reEngageRadius` → Chase · hết giờ → quên → Idle |

### 4 strategy (plug-point)

| Interface | Việc | Biến thể |
|---|---|---|
| `IIdleBehavior` | di chuyển lúc Idle | `WanderRoam` (tung tăng quanh home) · GuardStill · Patrol |
| `IAggro` | phát hiện **chủ động** | `PassiveAggro` (không bao giờ) · SightAggro (thấy trong `aggroRadius`) |
| `IPursuit` | hướng bám theo | `StraightPursuit` (thẳng, dump) · sau: Pathfind |
| `IAttackPlan` | canh + tung đòn | `SimpleAttack` (hết busy + trong tầm → đánh) · Kiter (chạy vòng, chọn khe) |

*(Tương lai: `ISteering` — flocking/separation, lớp phủ lên Move; quái nào cần mới cắm.)*

### Reactive: bị đánh → combat (chung, không phải strategy)
`EnemyAI` nghe `Damageable.Damaged(source)` → nhắm kẻ đánh, nhảy vào Chase từ Idle/Forget. Đây là cách **PassiveAggro** (MewFrog) vào trận: không chủ động, đụng vào là quánh.

### Skill nối vào AI
```
controller.Attack()  →  event Attacked  →  View phát anim  →  UnitAnimator.Hit  →  skill (Swing/SoulFire) ra đòn
```
AI chỉ **bấm cò** (`controller.Attack()`); skill nào bắn là do prefab gắn gì → **melee hay ranged do skill quyết**, không phải AI plan.

- **Tầm đánh thuộc brain** (`EnemyConfig.attackRange`) — chỗ unit dừng chân để bắn. Projectile **không có tầm riêng**: bắn ra là homing tới target mãi (target chết/biến mất thì flame quái (locked, tự theo team) tắt lịm, không rise/burst). Skill không giữ tầm.
- **Damage lấy off chủ nhân**, không phải ICharacterStats (để quái tái dùng chung skill với MC):
  ```csharp
  DynamicUnit.AttackPower   // MC => stats.AttackPower.Value;  Enemy => config.attackDamage
  ```
  Skill đọc `_owner.AttackPower` (owner là `DynamicUnit`), **bỏ `[Inject] ICharacterStats`**. → cùng một `SwingAttack`/`SoulFireAttack` chạy cho cả MC lẫn quái.

## Tạo 1 con quái
```csharp
public class MewFrogAI : EnemyAI
{
    protected override AIStrategies Build() => new()
    {
        Idle    = new WanderRoam(),      // tung tăng quanh spawn
        Aggro   = new PassiveAggro(),     // không chủ động; bị đánh mới quánh
        Pursuit = new StraightPursuit(),  // bám thẳng, dump
        Attack  = new SimpleAttack(),     // hết cd + trong tầm → đánh (tầm = skill SoulFire trên prefab)
    };
}
```
Prefab MewFrog: `EnemyController` + `MewFrogAI` + `EnemyView`/`UnitAnimator` (có Hit frame) + `SoulFireAttack` (skill ranged). Con hung hăng canh gác đổi 2 dòng: `Idle = GuardStill`, `Aggro = SightAggro`.

## Thêm quái / behavior mới
- **Trùng khuôn, khác cắm** → subclass mới, chọn strategy có sẵn. 0 code mới.
- **Hành vi mới** (vd kiểu đánh phức tạp) → viết 1 class strategy (`class Kiter : IAttackPlan`), cắm vào. Khung + quái khác không đụng.

## Số trong `EnemyConfig`
`attackRange` (dừng-chân-để-bắn), `aggroRadius`, `leashRadius`, `reEngageRadius`, `forgetTime`, `wanderRadius`, `attackDuration` (+ `moveSpeed`/`attackSpeed`/`attackDamage`/`hp`/`hitRadius`). Tầm đánh **không** ở skill nữa — brain giữ.

## Phụ thuộc & lưu ý
- **Player phải hittable để combat thật.** Enemy nhắm qua CombatWorld (IDamageable). MC hiện *chưa* là IDamageable (TODO "Máu Player") → MewFrog chase/attack được nhưng đòn **whiff** cho tới khi làm MC hittable. Test tạm bằng một Damageable team-1 (dummy).
- **Bán kính query CombatWorld ≤ ô hash (8).** `FindHostile`/`SightAggro` dùng `Overlap` nên `aggroRadius` nên ≤ 8 (không thì miss + warning). `leash`/`reEngage` dùng khoảng-cách-tới-target-đã-biết, **không** bị ràng buộc này.
- Đòn thật do skill component đánh ở frame `Hit`; AI không tự gây damage.
