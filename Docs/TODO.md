# TODO — Adventure

Việc còn nợ, gom theo mảng. Cập nhật dần; đánh dấu `[x]` khi xong.

---

## 🎮 Core loop / gameplay

- [ ] **Mewfrog roaming biết né.** Đang đi dạo (`WanderRoam`) mà có sinh vật **khác loài** lọt vào bán kính nhỏ
  thì bỏ chạy ra xa, thay vì cứ lững thững đi tiếp. Đây là hành vi lúc **chưa aggro** — khác với `leashRadius`
  (bỏ đuổi) và khác `PassiveAggro` (chỉ đánh trả khi bị đánh).
  - Seam đã có và giờ rẻ hẳn: viết `SkittishRoam : IIdleBehavior` (`[Serializable]`, `fleeRadius` + hệ số
    hysteresis là field của **chính nó**) — quét `ctx.FindHostile(radius)` mỗi tick, có thì đi ngược hướng nó,
    không thì roam như cũ. Xong thì nó **tự hiện trong dropdown** slot Idle của `mewfrog Brain`; không sửa
    `EnemyConfig`, không sửa `EnemyAI`, không đăng ký ở đâu.
  - **Cần chốt trước:** "khác loài" định nghĩa thế nào? Team không đủ (cây/đá cùng team 2 với quái, mà player
    là team 1 — nên `FindHostile` sẽ trả về **cả player lẫn không gì khác**). Có thể cần id/kind trên
    `UnitController`, gộp với mục "Đồng bộ mọi thứ nhận dmg là `UnitController`" ở Tech debt.
  - Bán kính né nên **nhỏ hơn** `aggroRadius`, và cần hysteresis (chạy tới khi ra ngoài `radius × k`) —
    không thì lại strobe đúng như vụ Chase/Attack vừa rồi.
- [x] **PP1 (Predator plant): cây phục kích có giờ hoạt động.** ✅ Làm hoàn toàn bằng behaviour mới + sửa asset,
  **không đụng FSM khung** — đúng lời hứa của nấc 2. Đặc trưng con này: **thức 6h–10h**.
  - **Idle** = `WanderRoam` với `activeHoursOnly` (field mới) + `rest 20–45s`, `radius 1`: ngoài giờ đứng chết,
    trong giờ thi thoảng nhích một đoạn ngắn. Không đẻ class mới vì khác biệt duy nhất là *một cái cổng* và
    *mấy con số* — đúng thứ nên nằm trong asset.
  - **Aggro** = `PredatorAggro`: **mọi giờ** cắn bất cứ thứ gì lọt vào `attackRange` (đớp cơ hội, **không**
    dấn thân → vẫn đứng yên); **trong giờ** săn trong `huntRadius` (4) và **dấn thân luôn**, không cần bị đánh.
  - **Pursuit** = `StraightPursuit` — chỉ chạy khi đã dấn thân, vì "không đuổi" **không** cài ở pursuit mà ở
    chỗ giữ target (xem dưới). Đã thử `AmbushPursuit` (trả `Vector2.zero` khi chưa committed) rồi **bỏ**: nó
    khiến cây đứng im nhưng vẫn ở state Chase và vẫn `FaceTarget` → quay đầu bám theo con mồi, sai vibe.
  - **`AIContext.committed` + `EnemyAI.Reflex` — luật FSM mới:** `committed` = "trận này thật rồi", bật khi
    **bị đánh** (`OnDamaged`) hoặc khi **aggro behaviour CHỌN** một mục tiêu nó định đuổi (`SightAggro`,
    `PredatorAggro` lúc trong giờ). Chưa committed = `Reflex`: **không giữ target quá cái khoảnh khắc ra đòn** —
    không đuổi, không bám. Quay **đúng một frame** — frame ra đòn — rồi `Release()` về Idle như chưa có gì.
    - Quay ở đúng frame đó là **bắt buộc**, không phải trang trí: `ShapeAttack` của PP1 là Rect ném **về phía
      trước** (`forwardOffset 0.4`), không quay thì cú đớp bay theo hướng roam lần cuối và con mồi đứng ngay
      sát sườn vẫn thoát. Quay ở đây mà **không** quay ở các frame trước chính là ranh giới giữa *ngắm* và *bám*.
    - Thứ tự đúng: `Face` ghi `FacingDir` → `Attack` bắn → `UnitView.PlayAttack` đẩy hướng đó vào animation,
      tất cả trong cùng một frame. Buông target ngay sau đó không ảnh hưởng đòn đang vung: `IsBusy` khoá di
      chuyển suốt swing nên `FacingDir` đứng yên, và `ShapeAttack` trúng bằng overlap chứ không đọc target.
    - `Release()` là chỗ **duy nhất** xoá `committed` — giữ lâu hơn thì một phát đánh biến ambusher thành con
      chuyên đuổi vĩnh viễn.
    - Hệ quả: `SightAggro` **phải** set `committed` (nhìn thấy và chọn = quyết định đi tới), không thì con nào
      dùng nó cũng tụt thành reflex-snapper.
  - **`activeFrom`/`activeTo` đặt ở `EnemyBrainConfig`**, không phải trong từng behaviour: giờ thức là nết của
    **con vật**, mà cả roam lẫn săn đều phải đồng ý với nhau về "sáng sớm là mấy giờ"; hai bản số sẽ lệch nhau.
    Đọc qua `ctx.IsActiveHours`. Mặc định `0..24` = luôn thức → con nào không quan tâm thì không thấy nó tồn tại.
    Có wrap qua nửa đêm (`20 → 4`) cho con ăn đêm sau này.
  - `EnemyAI` inject `DayNightClock` (đã có sẵn trong `GameScope`), và **cảnh báo** nếu brain có khung giờ thật
    mà không inject được clock — hỏng kiểu này trông y hệt lỗi chỉnh số, phải nói ra.
  - Luật `Reflex` bịt luôn một cái hở mà bản `AmbushPursuit` từng có: hồi đó cây đớp xong **giữ** target nên
    `Detect` không chạy lại, con mồi đứng sẵn trong `huntRadius` lúc 6h gõ thì cây không bao giờ dấn thân. Giờ
    reflex luôn trả về Idle không target → `Detect` chạy mỗi tick → 6h là vồ ngay.
- [x] **AI enemy chuyển sang asset-driven.** ✅ Không dùng enum→factory (không chở được tham số), mà
  **`[SerializeReference]`**: `EnemyBrainConfig` (SO) giữ 4 slot `IIdleBehavior/IAggro/IPursuit/IAttackPlan`,
  mỗi slot serialize **object C# thật** nên vừa *chọn* thuật toán vừa mang *tham số riêng* của nó. Thuật toán
  vẫn là plain C# — asset chỉ là bảng lắp ráp, **không method, không logic trong SO**.
  - `EnemyAI` hết `abstract`, xoá `MewFrogAI` (PP1 đang mượn não con ếch chỉ vì đẻ class mới thì phiền — đúng
    triệu chứng). Prefab Mewfrog/PP1 trỏ thẳng `EnemyAI`; mỗi con một brain asset riêng.
  - **Copy per-unit là bắt buộc:** brain asset dùng chung cả loài, mà behaviour có state (`WanderRoam._dest/_rest`)
    → `EnemyAI.BuildBrain` gọi `Instantiate(brain)` (deep-copy cả managed reference), `OnDestroy` dọn. Không copy
    thì cả bầy đi cùng một đích cùng một nhịp **và** asset bị dirty trên đĩa.
  - **Luật số nằm ở đâu — chia theo CHỦ SỞ HỮU:** `EnemyConfig` là **thân** (hp/speed/damage/hitRadius + đúng
    một con trỏ `brain`); `EnemyBrainConfig` là **trí** — cả số của FSM khung (`attackRange`, `leashRadius`,
    `reEngageRadius`, `forgetTime`, `recognizeTime`, `retaliateRadius`) lẫn 4 behaviour. Số chỉ **một**
    behaviour đọc thì nằm trong chính behaviour đó (`WanderRoam.radius` ← `wanderRadius` cũ,
    `SightAggro.radius` ← `aggroRadius` cũ). Nhờ vậy hai loài stats y hệt vẫn nghĩ khác nhau, và một bộ não
    dùng lại được cho nhiều loài.
  - `aggroRadius` → **`retaliateRadius`**, đổi cả nghĩa: nó là bán kính FSM tự quét "đứa nào vừa đánh tôi",
    **không phải** tầm nhìn. Con `PassiveAggro` không có tầm nhìn nhưng vẫn cần đánh trả.
  - Thêm `SightAggro` (chủ động gây sự) — chưa con nào dùng; muốn PP1 phục kích thì đổi **một dropdown** trong
    `pp1 Brain`, không đụng code. Đó là toàn bộ điểm của việc này.
  - `EnemyBrainConfigEditor` vẽ 4 dropdown + field của behaviour ngay dưới, list dựng bằng `TypeCache` → thêm
    behaviour mới = **1 class `[Serializable]`**, tự hiện trong dropdown, không phải đăng ký ở đâu cả.
  - **Bẫy còn lại:** `[SerializeReference]` lưu tên class → **đổi tên/namespace là đứt ref, mất data**. Rename
    thì gắn `[MovedFrom]`.

- [x] **Combat State chỉnh sửa.** ✅ `DynamicUnit` giờ giữ **hai** timer, cả hai chạy từ lúc đòn bắt đầu:
  `_busyTimer` = `AttackDuration / AttackRate` → `IsBusy` khoá cả di chuyển lẫn tấn công như cũ;
  `_cooldownTimer` = `AttackCooldown / AttackRate`, **clamp `>= duration`**, chỉ gate đòn kế tiếp qua
  `CanAttack` chứ **không khoá hành động nào khác** — trong cooldown vẫn chạy được. Thêm `attackCooldown` vào
  `MainCharStatsConfig` + `EnemyConfig` (mặc định `0` = đánh liên tục, clamp tự kéo lên bằng duration → asset
  cũ giữ nguyên nhịp). `SimpleAttack` gate bằng `CanAttack`; `EnemyAI.TickAttack` vẫn trả về Chase sau mỗi lần
  thử để lúc chờ cooldown nó còn biết đuổi theo.
  - **Attack speed scale CẢ HAI cửa sổ** (theo cách các action game vẫn làm), không chỉ cooldown: tỉ lệ
    khoá:trống giữ nguyên ở mọi rate, và attack speed không bao giờ thành dead stat khi chạm clamp. Clamp
    giờ chỉ còn là guard cho config sai (chia cả hai cho cùng số thì thứ tự không đổi).
  - **Animation scale theo**: `UnitView.PlayAttack` set `UnitAnimator.PlaybackSpeed = AttackRate`, trả về `1`
    khi hết busy. Nhờ vậy `Hit` AnimationEvent tự rơi đúng cùng một mốc % của swing ở mọi attack speed — không
    có timing thứ hai phải canh. Dùng `animator.speed` (global, giới hạn trong cửa sổ swing) thay vì per-state
    speed multiplier: 3 controller hiện tại đều để `m_SpeedParameterActive: 0`, và cách này đúng cho cả
    controller thêm sau.
  - ✅ **Hết nợ `attackDuration` lệch clip — field đã XOÁ hẳn.** Độ dài khoá giờ chỉ đến từ clip:
    `UnitView` đo `UnitAnimator.LengthOf(Attack)` = `frames.Length / fps` rồi đẩy vào
    `DynamicUnit.SetSwingClipLength`. Cả nó lẫn animation cùng chia/nhân `AttackRate` nên **khoá và swing kết
    thúc đúng cùng một thời điểm ở mọi attack speed** (đại số bằng nhau, không phải xấp xỉ). Số cũ của 4 asset
    đã dời sang `attackCooldown` (`MC 1` 0.5 · `MC 2` 0.6 · `mewfrog` 2 · `pp1` 3) nên **nhịp đánh giữ
    nguyên**, chỉ khác là giờ chỉ bị root đúng đoạn vung. Muốn giãn nhịp thì dùng `attackCooldown` — nó không
    khoá di chuyển; **không** thêm lại một số "duration" thứ hai.
- [x] **Bot AI chỉnh sửa.** ✅ Nguyên nhân **không nằm ở AI** — `EnemyAI.TickAttack` update `Facing` đúng. Lỗi ở
  `UnitView`: hướng chỉ được đẩy xuống animator trong `LateUpdate`, mà `LateUpdate` lại `return` sớm khi
  `IsBusy`. Hai hệ quả cộng dồn:
  - **Frame bắt đầu đòn:** Animator tiêu thụ trigger ở animation phase — chạy **sau `Update`** (nơi đòn bắn ra)
    và **trước `LateUpdate`** — nên transition `Attack && Dir == n` đọc `Dir` của frame **trước**. AI xoay mặt
    rồi đánh trong cùng một `Update` thì đòn vung theo hướng cũ.
  - **Suốt swing:** busy khoá luôn `UpdateDir`, nên cả 2–3s đòn đánh hướng bị đóng băng. Với unit mirror
    (`Mewfrog`/`MC 1`: `dirType 0` + `isMirror 1` → `Dir` luôn = 1) thì hướng **chính là** flip `scaleNode`,
    tức là frozen = sai hẳn. Với `PP1` (`dirType 1`, `isMirror 0`) thì hướng nằm ở `Dir` int → dính cả hai.
  - **Sửa:** tách `PushDir()`, gọi ở **đầu `LateUpdate`** (trước guard `IsBusy`) **và** trong `PlayAttack()`
    ngay trước `TriggerAttack()`. Chỉ `UpdateState` còn nằm sau guard. Đẩy `Dir` giữa swing an toàn: vào state
    attack cần trigger `Attack`, đổi hướng suông không restart được đòn.
- [ ] **Hệ thống modify stats của MC.** Buff / đồ / nâng cấp sửa stats **runtime** (MoveSpeed, AttackSpeed,
  AttackPower, MaxHp, Mass...). `MainCharStats` giờ chỉ copy phẳng từ config; `Stat` (MoveSpeed/AttackSpeed/
  AttackPower đã là `Stat`) là **seam sẵn** — thêm modifier (cộng/nhân, mỗi nguồn 1 id để gỡ) lên đó. **MaxHp
  phải modify được** (đúng vibe "HP trên Unit modify được"); `Damageable` đọc MaxHp từ `Unit.DamageableConfig`
  nên cân nhắc cho nó đọc giá-trị-đã-modify chứ không phải config gốc. Nhớ recompute chỗ đang one-shot: `Mass`
  set 1 lần ở `DynamicUnit.Start` (xem `// TEMP`) → gọi lại `body.SetMass` khi mass đổi.
- [ ] **Máu Player (bespoke IDamageable).** Player *là* IDamageable nhưng đặc biệt: HP vào
  `MainCharStatsConfig`, tự implement (KHÔNG dùng `Damageable`), team **1**, đăng ký vào `CombatWorld`
  (`combat.Add`). Chết = game-over/hồi sinh, *không* rơi loot. Không i-frame, không khoá hành động.
- [ ] **Inventory / Backpack.** `Pickable.Collect()` đang chỉ `Destroy` — chỗ `// TODO(inventory)` là
  nơi cộng resource vào backpack khi có hệ inventory.
- [ ] **Damage do chạm / DoT + cooldown theo nguồn.** Quái húc / đứng trong lửa: mỗi *nguồn* có nhịp
  trừ máu riêng (vd 0.5s/lần), độc lập nhau. KHÔNG i-frame ở người nhận (mọi dmg đều tính). Làm khi
  dựng enemy.

- [x] **Sprite animation bỏ AnimatorController → `CharacterAnimSet`.** ✅ Gốc của cả hai vấn đề "animator
  cực" và "xoay cam phá animation đang chạy" là **hướng bị nướng vào identity của state**: state = hành động ×
  hướng. Nên số state nhân lên (8 hướng = 24 state + 24 transition/nhân vật), và đổi hướng = đổi state = mất
  playhead. Soi `.anim` ra thì nội dung thật của một clip chỉ là **mảng sprite + fps + 1 event `OnHit`** — toàn
  bộ máy trạng thái chỉ để chọn phát mảng nào.
  - **Đã code:** `CharacterAnimSet` (data: dirs 2/4/8 + mirror + clip theo action), `UnitAnimator` viết lại chạy
    frame bằng code (giữ nguyên GUID nên `animatorSource` trong prefab không đứt), `UnitView` đổi sang
    `Play(AnimAction)` / `SetDir(dir8)`, tool `Assets > Sprite3D > Build Character Anim Set`.
  - **Đã dọn:** gỡ `Animator` khỏi 5 prefab, xoá 3 `.controller` + 24 `.anim` + tool `AnimSetBuilder` (nó đọc
    `.anim` — đúng cái format vừa khai tử, giữ lại chỉ để lại đường mòn). Nhân vật mới: thả sprite vào array
    trong inspector của set, không đụng controller. Giữ `Sprite3D.Editor.asmdef` cho `CharacterAnimSetEditor`.
  - `hitFrame` là **index**, không phải AnimationEvent → hết cửa lệch. Kéo theo: `attackDuration` có thể đọc
    thẳng `frames.Length / fps` thay vì gõ tay (xem mục "Còn nợ" ở Combat State).

## 🌳 Content systems

- [ ] **Drops → Resources.** `DeathDrop.prefab` đang là ref trực tiếp → nằm luôn trong RAM cùng config.
  Chuyển sang **id/path load qua Resources** (load khi cần, free sau). Xem `// TODO(drops)` trong
  `DamageableConfig.cs`.
- [ ] **Logic rơi đồ phức tạp hơn.** Loot table, trọng số, điều kiện rơi. `DeathDrop` là seam.
- [ ] **Config cho từng loại cây/đá.** Tạo `OakConfig`, `PineConfig`, `RockConfig`... (mỗi loại 1 SO
  `DamageableConfig`), kéo vào `Damageable` của từng prefab.
- [ ] **Enemy** = `Damageable` (máu + loot) **+** não (AI) **+** chân (di chuyển) **+** đòn. Nền đã dựng
  sẵn — tối nay chỉ ráp 3 cái sau, đừng dựng lại nền.
  - **Nền tái dùng (đã có):**
    - `Damageable` lo máu + `Died`→loot. Enemy: `EnemyConfig.hp` **bind vào `Damageable` lúc spawn**
      (`EnemySpawner`), team lấy từ `UnitController`. Đích cuối: `Damageable` **trỏ `UnitController`** đọc máu —
      xem mục "Đồng bộ: mọi thứ nhận dmg là `UnitController`" ở phần Tech debt.
    - `CombatWorld.Overlap(centre, radius, attackerTeam, results)` — dò mục tiêu quanh quái (query **team 1**
      = player trong bán kính aggro).
    - `ShapeAttack` là **khuôn đòn**: ở frame `Hit` của `CharacterAnimator` → `Overlap` quanh origin →
      `TakeDamage`. Đòn quái mirror y hệt nhưng **team 2**, đánh player.
    - Di chuyển: khuôn `Character.Move` + `CollisionBody` (không xuyên đá). Quái thay input tay bằng input
      do AI sinh (hướng tới target).
    - Team: 0 trung lập / 1 player / 2 địch — không friendly-fire cùng team. Spawn **qua DI container** (hoặc
      Auto Inject của `GameScope`) để `CombatWorld` được inject vào `Damageable`/đòn.
  - **Phải làm tối nay:**
    - [ ] **Player hittable TRƯỚC** (mục "Máu Player" ở đầu file) — hiện `Character` chưa implement
      `IDamageable`, chưa `combat.Add` → **quái không có gì để đánh**. Đây là chặn đầu tiên.
    - [ ] **AI brain** — FSM **code thuần** (idle → phát hiện theo aggro radius → đuổi → đánh khi trong tầm +
      cooldown → mất dấu/về). ~~KHÔNG behavior-tree SO, KHÔNG data-driven~~ — **đã lật**, xem mục "AI enemy
      chuyển sang asset-driven" ở đầu file. FSM khung vẫn là code thuần; phần được asset-hoá là việc *chọn*
      strategy nào cho con nào.
    - [ ] **EnemyMelee** — mirror `ShapeAttack`, team 2, dmg/tầm/nhịp từ `EnemyConfig`.
    - [ ] **EnemyMotor** — steer tới target qua `CollisionBody`, tốc độ từ `EnemyConfig.moveSpeed`.
    - [ ] **Spawn** — tạm đặt tay 1-2 con để test (nhớ cho vào Auto Inject); spawner thật xem **`Docs/SPAWN.md`**
      (thiết kế zone đã chốt; bước 1 `SpawnArea`+bake làm được ngay, phần đẻ chờ enemy runtime này).
    - [ ] Art: hero/quái cận cảnh → **AnimatorController** (blend/attach); crowd đông → cân nhắc
      AnimationInstancing. `CharacterAnimator.Hit` là seam frame-đánh.
  - **Gotcha:** bán kính `Overlap` phải ≤ cell hash (`4`) không thì miss (có warning); đòn tự `Rebuild()`
    trước query — xem `ShapeAttack.OnHit`.
  - **SoulFire ưu tiên target.** `SoulFire.FindNearest` giờ lấy `Damageable` gần nhất không cùng team (nên
    hiện gồm cả cây/đá). Khi có Enemy thật → sửa để **ưu tiên quái trước, cây cối sau** (vd lọc theo loại/
    tag/team, hoặc 2 pass: quét quái trước, không có mới tới cây). Xem `// TODO` trong `SoulFire.FindNearest`.
- [ ] **Rương (chest).** *Breakable, KHÔNG phải pickup.* Rơi ra nằm trên map, có `CollisionBody` (chiếm
  chỗ), là `Damageable` — chém vỡ (`Died`) → `Dropable` rơi đồ khác. Không nhặt trực tiếp. → tái dùng
  nguyên pattern cây (`Damageable` + `Dropable` + `DropOnDeath` + `CollisionBody`). Thêm: **save các rương
  trên map** (vị trí + trạng thái) — cần cơ chế persist object trên map (chưa có). Làm sau.

## 🏞️ Flat tile map + height mask — hướng đã chốt, chưa implement

**Sửa lại quyết định cũ:** không làm terrain nhiều tầng, không cho nhân vật đi trên các mặt
cao khác nhau. Cách renderer hiện tại luôn đặt ground ở sorting layer thấp hơn billboard; nó không
thể diễn tả đúng việc một mặt đất cao che nhân vật ở phía sau. Giữ tham vọng đó sẽ kéo theo
depth thật, vách, ramp, sorting và shadow nhiều tầng — không còn hợp với game 2D perspective này.

### Mô hình map đích

- **Tile map vẫn là một ground grid, không có mặt cao để đi.** Không có platform chồng tầng, ramp hay
  cầu vượt. Mesh có thể hạ cell xuống và sinh cliff ở biên hố, nhưng cliff chỉ là visual của vùng bị
  chặn. Hang/nhà nhiều tầng nếu cần sẽ là map/scene riêng qua `MapService`.
- Mỗi cell có một giá trị **height authored theo map**, nhưng height là **mask gameplay/visual**, không
  biến tile map thành terrain 3D có thể leo. Mốc ground bình thường là `height == 0`.
- **Chỉ cell `height == 0` là walkable.** Mọi height khác 0 đều bị chặn; phần lớn cell khác
  0 dự kiến là chỗ trũng thấp. Không có `climbHeight`, `wadeDepth`, ramp hay luật so chênh cao
  giữa hai cell.
- **Nước là một shader overlay**, không phải terrain id được paint. Nơi cell thấp hơn mặt
  ground/nước quy ước sẽ được water shader phủ lên. Cell đó vẫn không walkable; chưa thiết
  kế bơi hay lội.
- Đá, tường, bục cao và vật che tầm nhìn là **object/billboard**, không phải tile cao.
  Tile không bao giờ block sight; object dùng `CollisionBody` và `blocksSight` như thiết kế hiện có.
- `height` là authored-only. Chưa có terraforming; nếu sau này cho người chơi sửa thì lưu sparse
  diff so với map gốc, không copy cả grid vào save.

### Ranh giới với code hiện tại

Code hiện tại vẫn là tile map phẳng có nhiều terrain id trong `cells` (`TerrainSet`, auto-tiling,
`walkable` theo layer). **Không đổi ngầm nghĩa code cũ trong lúc sửa note.** Khi implement hướng mới
cần migrate rõ ràng: cell trở thành ground, height quyết định walkability, water tách khỏi terrain
palette. `Assets/_Project/Art/Terrain/TERRAIN.md` tiếp tục mô tả **renderer hiện tại** cho tới khi
migration này được code thật.

### Việc cần làm

- [ ] **Depth data.** Grid mới mặc định toàn `0`. Dùng kiểu có dấu (`sbyte` hoặc `short`): `0` là mặt
  đất chuẩn, số âm là từng depth step xuống hố. Thêm dữ liệu per-cell vào `TerrainGrid`; không tự
  sinh giá trị dương/raised walkable ground.
- [ ] Sửa `TerrainGrid.IsWalkable`/`CanPass`: chỉ cho phép ô trong map có `height == 0`; bỏ việc
  quyết định walkable bằng terrain layer sau khi migration.
- [ ] **Depth painter trong `TerrainGridEditor`.** Có ba tool riêng, không giấu Flatten trong modifier:
  - `Lower`: giảm depth theo step.
  - `Raise`: tăng depth theo step nhưng clamp tối đa về `0`.
  - `Flatten / Reset`: đưa tất cả cell trong brush về **đúng `0`**. Đây là công cụ cào bằng và phải là
    mode riêng, dễ chọn, vì nó đồng thời khôi phục ground walkable.
  - Có brush size, drag liên tục, preview màu theo depth, Undo/Redo và lưu đúng vào prefab/scene.
- [ ] **Ground/cliff mesh từ depth map.** Mặt mỗi cell nằm ở depth của nó. Hai cell cùng depth nối phẳng;
  hai cell lệch depth sinh profile ở cạnh chung. Rebuild theo chunk và dirty cả chunk hàng xóm khi sửa
  cell sát biên.
  - Profile đã chốt: top bevel chiếm **20% chiều cao của một depth step**, rồi tới vách thẳng. Khi phía
    dưới cạnh đó thực sự có mặt low ground, sinh thêm bottom bevel cao **20% một step**: nó tiếp tục
    **trồi ra phía ô thấp cùng hướng với top bevel**, không co ngược vào chân vách. Điểm cuối bevel dùng
    chung đường biên/vertex với mặt low ground, nên mặt thấp bắt đầu ở đó, không chạy dưới bevel.
    Mặt cắt đúng (cả hai dấu `\` cùng nghiêng/trồi về phía low ground):
    ```text
    high ground ──────╲
                       ╲  top bevel 20% step
                        │
                        │  wall
                        ╲  bottom bevel 20% step — CHỈ khi có low surface
    low ground ──────────╲────────
                          ↑ shared boundary; low surface bắt đầu tại đây
    ```
    **Không làm profile chữ S** (`top \`, `bottom /`); bottom bevel tuyệt đối không co về phía high ground.
  - Bottom bevel là topology có điều kiện, không phải trang trí luôn có: không có mặt low ground để
    tiếp nhận thì không sinh. Chênh 1 step có profile 20% top bevel + 60% wall + 20% bottom bevel;
    chênh nhiều step vẫn giữ hai bevel theo đúng một step và chỉ kéo dài wall ở giữa. Không scale bevel
    theo tổng độ sâu.
  - Cạnh có đủ hai bevel cần 3 quad = 6 tris; con số 20% không tự đẻ thêm tris. Chỉ thêm segment nếu sau
    này thật sự muốn bevel cong.
  - **Corner geometry riêng là bắt buộc** cho góc lồi, góc lõm và nơi nhiều depth gặp nhau; không chồng
    các edge strip độc lập vì sẽ hở hoặc overlap. Đặc biệt phải giải quyết chỗ hai bottom bevel cùng
    lấn vào một ô thấp. Bevel là visual, không ảnh hưởng walkability.
- [ ] **Bake theo chunk trong Editor.** Depth grid là source of truth; mesh chỉ là cache hiển thị. Painter
  chỉ preview/đánh dấu dirty, còn `Bake Dirty Chunks` hoặc `Bake All` sinh ground, cliff, bevel, corner,
  water mask và bounds theo chunk rồi lưu cùng map. Không rebuild toàn map lúc load runtime.
- [ ] **Bake mesh terrain lúc BUILD game (product), không phải tầng asset.** Hiện `TerrainRenderer.Build()`
  procedural ở `OnEnable`, các `Layer_*` để `HideFlags.DontSave` nên **không lưu vào prefab** (chỉ `cells`
  + `walls` lưu). Dev cứ để procedural: asset nhỏ, sửa map khỏi re-bake, mesh dựng lại từ `cells` khi load.
  - Chỉ bake khi **profiling load-time thật sự đáng kể** (map to / swap map liên tục qua jump-point). Đo
    `Build()` trước; mesh phẳng vài ms/lần thường là bỏ qua được.
  - Nếu cần: **`IPreprocessBuildWithReport`** bake mọi map thành Mesh asset + serialize `Layer_*` vào prefab
    **chỉ trong bản ship**; `TerrainRenderer` skip `Build()` khi đã có baked children. **Editor workflow
    không đổi** → tránh ma sát re-bake mỗi lần vẽ (khác với nút bake tay ở tầng asset).
  - `walls` (collision) đã bake & lưu sẵn — chỉ mesh *hình ảnh* mới cần lo.
- [ ] **Runtime mutation — hiếm, tối ưu vừa đủ.** Game sau này có thể phá/hạ một vài tile hoặc lấp hố,
  nhưng thay đổi map xảy ra rất hiếm; chưa xây hệ terraforming liên tục.
  - Khi cell đổi: cập nhật depth grid ngay, lấy bounds các cell vừa sửa rồi `Expand(1)` theo bốn hướng;
    mọi chunk giao vùng đó vào một `HashSet` dirty. Cách này tự kéo cả chunk cạnh/góc cần thiết vì cliff
    và corner phụ thuộc hàng xóm.
  - Gom toàn bộ thay đổi của một action/vụ nổ, rồi mỗi dirty chunk chỉ rebuild **một lần** ở cuối action
    hoặc `LateUpdate`; không rebuild sau từng hit/từng cell.
  - Chunk chưa từng đổi dùng mesh asset đã bake. Lần đầu bị đổi thì tạo runtime mesh cho chunk đó; các
    lần sau `Clear`/tái dùng cùng mesh và buffer, không tạo lại GameObject/material.
  - Cập nhật walkability/collision ngay theo depth data; mesh, water mask và grass chỉ rebuild cho dirty
    chunks. Nếu sau này một action làm bẩn quá nhiều chunk mới cân nhắc budget rebuild theo frame.
  - Theo luật hiện tại, phá/hạ tạo depth âm; tăng depth chỉ là lấp dần và clamp về `0`, không tạo platform
    dương. Cách save runtime diff sẽ chốt riêng khi feature phá map thật sự được làm.
- [ ] **Water.** Tách water khỏi `TerrainSet`/palette; build world-space mask cho cell thấp. Có thể dùng
  một water render plane/proxy lớn đi theo camera (nên snap theo cell/chunk), nhưng mask, UV/noise và
  đường bờ phải bám **world-space** để nước không trượt khi camera di chuyển hoặc xoay. Giữ material
  trong suốt, stylized (tint/noise/foam); chưa làm refraction, reflection, bơi hay lội.
- [ ] **Water theo thời tiết.** Thông số shader `World/StylizedWater` (deep/shallow/edge color, foam,
  caustic, scroll/wobble) cho weather lái được: bão → tối + nhiều foam + gợn mạnh; lặng → trong, phẳng.
  - **Phần thị giác thì rẻ, làm lúc nào cũng được**: chỉ set per-material float hoặc `Shader.SetGlobal`,
    **KHÔNG rebuild mesh**. Cắm vào cùng seam weather (`EnvironmentState`) như day/night.
  - **Chỉ đổi MỰC/PHẠM VI nước** (lụt/cạn → cell nào là nước đổi) mới cần **regen water mesh runtime** —
    cái này đi kèm việc build-mesh-runtime + chunk (xem phần elevation), để sau.
- [ ] `GrassField` không scatter trên cell `height != 0`/cell có water overlay.
- [ ] Kiểm tra billboard, shadow và camera xoay trên map có hố trũng. Không đổi terrain sang
  ZWrite/depth pipeline trừ khi spike thật sự chứng minh là cần.

## 🌤️ Environment (day/night + weather)

- [ ] **Weather system.** Cắm vào seam `--- Weather seam ---` trong `DayNightLighting.LateUpdate`:
  weather biến đổi `EnvironmentState` (ambient/fog/intensity) *sau* day/night rồi mới đẩy vào LightManager.
  - [ ] **SunnyWeather**: bóc cái glare vàng trưa (`#ACAE72`) từ base day/night ra đây
    (xem `// TODO(weather)` trong `DayNightConfig`). Trời âm u thì trưa không chói.
  - [ ] Mưa / sương mù / tuyết: fog (cộng sáng/haze) + giảm intensity + tông ambient.
- [ ] **Day/night timing → config + save.** `DayNightClock` đang hard-code `DayLengthSeconds` +
  `StartTime` (`// TODO: load from save`). Đưa ra config, và giờ khởi động lấy từ save.

## 💡 Sprite lighting (đèn điểm: lửa trại, đuốc…)

Mục tiêu: đêm quanh đống lửa, **mỗi cây sáng một góc riêng** theo hướng lửa (đã soi ảnh tham khảo:
vệt sáng lật đúng theo phía có lửa → là **directional per-pixel thật**, không phải chỉ tint theo khoảng cách).

- [ ] **⚠️ CHỐT ART CONTRACT TRƯỚC KHI THUÊ HỌA SĨ — rẻ khi sớm, cực đắt khi muộn.**
  Sprite phải vẽ **phẳng/trung tính** (ambient, KHÔNG bake key light cố định), kèm **normal map tả khối
  tổng**. Nếu art đã bake highlight góc trên-trái sẵn **+** dynamic light từ lửa bên phải = **hai nguồn
  sáng đá nhau**, cây trông bẩn, **không fix được bằng code**, phải vẽ lại cả bộ.
  → Brief giao 2 file/sprite: `base` (shading phẳng) + `normal` (palette lượng tử ~12–16 màu).
- [ ] **Hạ tầng — làm sớm được, không dính art.** `_PointLights[N]` (xyz + radius) + `_LightColors[N]`,
  manager giữ N đèn gần camera nhất → y khuôn `GrassInteractorManager` (`_GrassInteractors[16]`).
  - **Lớp 1 (rẻ, chạy với mọi sprite kể cả asset mua sẵn):** per-object falloff + tint, sample **tại gốc
    cây** (`scaleNote`), tô đều cả sprite. Lo phần "cây xa chìm vào đêm, cây gần trại thì ấm". Cố ý
    **đừng làm mượt** — lượng tử theo object mới ra cảm giác từng cây là một bậc sáng.
  - **Lớp 2:** shader hỗ trợ `_NormalMap` **optional** (không gán → fallback phẳng). Cắm sẵn ống, thả
    normal vào từng sprite lúc nào cũng được.
- [ ] **Normal map hàng loạt — ĐỢI có art thật.** Là art asset per-sprite, đổi art = vẽ lại sạch. Giờ chỉ
  vẽ **đúng 1 cây** làm prototype + mẫu brief (~30 phút).
  - Normal chỉ tả **KHỐI TỔNG** (cả vòm cây = một quả trứng: giữa tím, trái lam, phải hồng, đỉnh xanh lá).
    Chi tiết vảy lá để nguyên ở texture. Auto-bevel từ alpha sai chính vì nó bám viền lá thay vì tả khối.
  - Không cần vẽ normal tay từ đầu: vẽ **height map xám** (trắng = nhô, đen = lõm) rồi convert. Hoặc thử
    **Laigter** (free, batch cả folder) sinh height từ luminance — tán lá vốn đã có cụm sáng tối nên tỉ lệ
    trúng khá cao. Nhờn thì mới vẽ tay cho vài loại cây chính; bụi/cỏ/đá để auto.
  - **3 bẫy chắc chắn dính:** (a) texture normal phải **TẮT sRGB** (là dữ liệu, không phải màu), Point
    filter, compression None; (b) MC lật bằng `scaleNote.scale.x < 0` → phải **đảo `n.x`**, không thì quay
    trái mà sáng vẫn bên phải; (c) lửa sát đất còn tán cây ở y≈2 → `L` chúc xuống, cây sáng từ dưới lên
    trông kỳ → nâng `lightPos.y` giả hoặc ép `L.y = 0` + bias lên.
  - Billboard: `N = right*n.x + up*n.y + (-camFwd)*n.z`, basis dựng y hệt `Grass.shader`. Lửa ở **sau** cây
    thì `NdotL` âm → tự có **viền sáng ngược sáng**, không phải code thêm.
- [ ] **Bóng đổ từ lửa (tùy chọn, làm sau).** Ảnh tham khảo **bỏ hẳn** cái này — làm phần sáng thôi đã đủ.
  Nếu làm: chọn **dominant light** (đèn mạnh nhất thắng), chấp nhận snap khi đi qua đường phân giác giữa 2
  đống lửa. Muốn mượt thì blend hướng — nhưng nhớ **`length = max(length_i)`, KHÔNG phải `length(sum)`**,
  không thì 2 nguồn đối xứng triệt tiêu nhau làm bóng **biến mất**.
  - Bonus: hệ stencil merge sẵn có khiến **N bóng chồng nhau không bị đen gấp đôi** — nên nếu sau muốn
    nhiều bóng thật thì đã đỡ được cái khó nhất.

## 🌥️ Mây bay (noise) — ý tưởng, chưa làm

- [ ] Noise scroll theo world XZ làm **bóng mây** trôi qua map. Hook vào `Grass.shader` thì rẻ (đã có sẵn
  `Noise()`), **nhưng phải là hiệu ứng world-space DÙNG CHUNG** cho đất + cỏ + sprite — chỉ cỏ tối mà đất
  vẫn sáng thì trông như cỏ đổi màu chứ không ra bóng mây.
  → 1 file `CloudShade.hlsl` với `float CloudShade(float2 worldXZ)` + globals (`_CloudScale/_CloudSpeed/`
  `_CloudDir/_CloudStrength/_CloudCoverage`) do một script driver push; mọi shader cùng đọc. Sprite sample
  tại **điểm chân** → cả cây tối đều một cục, rẻ và đúng cảm giác.
  → Buộc vào `ShadowSun`: mây chỉ có bóng khi mặt trời lên, và **offset vệt mây theo `_SunGroundDir`** (mây
  trên cao nên bóng lệch) — gần như free, nhìn rất "thật".
  → Làm 1 lần thì **giải luôn bài "cỏ nhận bóng cây"** (lá cỏ đứng lên, vẽ đè lên fill đất nên hiện không
  bị bóng cây làm tối → cỏ sáng trưng giữa vệt bóng). Cùng một cơ chế: một hàm world-space trả độ tối.

## ✨ Polish / feedback

- [ ] **Mũi tên hướng di chuyển.** Hiện mũi tên chỉ hướng MC đang hướng/di chuyển — dùng `DynamicUnit.FacingDir`
  (world XZ, đã có). Cũng là **chỉ báo hướng bắn SoulFire** (tia MC bay theo `FacingDir`). World-space dưới chân
  MC hoặc UI; cân nhắc snap 8 hướng cho khớp sprite.
- [ ] **Hit flash mạnh hơn (tùy chọn).** `HitFlash` đang dùng `SpriteRenderer.color` (nhân → chỉ tối
  lại thành đỏ, không sáng rực). Muốn "pop" đỏ/trắng chói thì thêm `_Flash` vào shader sprite (lerp về
  màu flash). Giờ để tạm màu-nhân.
- [ ] **Flinch / khựng.** Là *thuộc tính của đòn nặng* (set busy có chủ đích), KHÔNG phải mặc định khi
  trúng. Đòn thường không khoá hành động.
- [ ] **FlyingPickup nảy/lăn (tùy chọn).** Giờ là velocity + friction cơ bản. Muốn nảy khi chạm / lăn
  thì thêm sau.

## 🐛 Debug / tooling

- [ ] **Số máu trên đầu (editor/test).** Thanh máu ẩn hẳn; chỉ hiện *số* HP trên đầu ở chế độ
  editor/test (gate bằng `#if UNITY_EDITOR` hoặc cờ debug). Chưa làm.

## 🧹 Tech debt / cleanup

- [x] **`hitRadius` rời config → `[SerializeField]` trên `Damageable`.** ✅ Bán kính bị đánh là thuộc tính của
  **thân này**, không phải của loài: hai thứ dùng chung config vẫn có thể vẽ to nhỏ khác nhau, và số này phải
  khớp với **art** — thứ chỉ nhìn được trong prefab, cạnh gizmo. Gỡ khỏi `IDamageableConfig` + `EnemyConfig` +
  `PropConfig` + `MainCharStatsConfig`; `basic_tree` giữ `0.25`, còn lại `0.5` (mặc định, y như cũ).
  - Gizmo đổi sang **vòng tròn đỏ**, và giờ đọc **giá trị authored ngay trong edit mode** — trước kia `Cfg`
    luôn null ngoài play mode nên nó vẽ cứng `0.5`, tức là vô dụng đúng ở lúc cần nó nhất.
  - Quy ước rút ra: **số SPATIAL author cạnh art (prefab + gizmo), số của loài ở config.** `ShapeAttack.radius`
    vốn đã theo luật này rồi; giờ `hitRadius` cùng một chỗ. `attackRange` là ngoại lệ có lý do — nó là *quyết
    định* ("đứng cách bao xa thì dừng"), không phải kích thước, nên ở brain.
- [ ] **Một unit = một đòn.** `DynamicUnit.Attack()` **không có tham số**, `event Action Attacked` không chở
  thông tin đòn nào, `AnimAction` có đúng **một** slot `Attack`, và `ShapeAttack` là component gắn cứng trên
  prefab → một con quái không thể có hai đòn. Chưa cần vội (mới 1 tuần tuổi), nhưng **con quái thứ ba** kiểu
  vừa húc vừa phun sẽ ép trả nợ này, và nó chặn mọi thứ dính tới boss.
  - Hình dạng: `Attack(Move m)` với `Move` = { clip nào + hitbox nào + hitFrame + cooldown riêng }. Kéo theo
    `AnimAction.Attack` thành nhiều slot, `Attacked` chở `Move`, `ShapeAttack` thành nhiều khuôn chọn được.
  - Làm xong cái đó thì **chọn đòn** cắm đúng vào slot `IAttackPlan` sẵn có: `[SerializeReference] List<Move>`
    mỗi Move tự chấm điểm theo cự ly/HP/cooldown, cao nhất thắng (mô hình Monster Hunter). Cộng thêm lên hạ
    tầng brain hiện tại, không phải làm lại. Quái 1 đòn thì selector trivial → không tốn gì.
  - Boss thì **không** phải hệ AI khác: thêm đúng một state `Scripted` vào FSM để một component kịch bản giành
    quyền rồi trả lại (Souls/Hollow Knight đều là mỗi boss một kịch bản riêng, dùng chung nguyên liệu). Đừng
    fork cả cây AI, và đừng tự viết graph tool — cần thì lấy `com.unity.behavior`.
- [ ] **Mass động theo đồ/nâng cấp.** `UnitController.Start` đang `body.SetMass(Mass)` **một lần** từ stats gốc.
  Sau này mass đổi theo trang bị / nâng cấp / buff → cần **tính lại mass khi thay đổi** (event stats-changed →
  `SetMass`), không phải set cứng ở Start. Xem `// TEMP` trong `UnitController.Start`.
- [ ] **Input lên system, đừng ở trên body.** `MCInput` đang là component trên prefab MC → mỗi respawn/đổi
  nhân vật lại đẻ lại + re-wire vào body mới. Nên tách thành **system đọc ý định player** (WASD/attack) rồi
  lái vào `IPlayer.Current` — input sống độc lập với thân, đổi MC không đụng gì. Command/gate giữ nguyên,
  chỉ đổi chủ sở hữu.
- [ ] **Esc bị 2 chủ.** `UISystem.Update` và `GameController.Tick` cùng xử Esc (đang né bằng
  `CloseOnEscape=false`). Gộp về một chỗ sở hữu.
- [ ] **Picker → interface config.** `Picker` đọc thẳng `ICharacterStats.PickupRadius` (giờ chỉ MC nhặt).
  Khi có picker khác MC → tách interface cung cấp config. Giờ overkill.
- [ ] **Đổi nhân vật chưa rebind inventory/UI (config backpack đang per-char).** `PlayerSystem.SwitchTo(id)`
  respawn body mới → **stats cập nhật** (child-scope per-spawn) **nhưng inventory/capacity thì chưa**.
  `backpackCapacity` nằm trong `MainCharStatsConfig` nên capacity thuộc *từng character*.
  - **Phần đúng đã dựng:** inventory `"main_char"` do **Picker của player tạo** (child-scope → đúng config char
    hiện tại); `GameUI` **inject `IPlayer`**, lấy inventory từ `player.Current` (Picker) lúc StartGame. Đã bỏ
    `IInventoryConfig` ở GameScope → hết vụ phải canh thứ tự register, và GameUI sẵn sàng bám player.
  - **Còn chặn khi switch:**
    - `"main_char"` là id cố định → `GetOrCreate` trả inventory cũ, **capacity char mới bị bỏ**. Cần: id
      inventory theo char, hoặc recreate với capacity mới.
    - `GameUI` lấy inventory **một lần** ở StartGame → switch không rebind. Cần: nghe `IPlayer.Spawned` rồi
      `SetInventory` lại (giống `CameraFollowsPlayer`).
  - **Design chưa chốt — quyết trước khi làm:**
    - **Backpack của PLAYER** (đổi char = đổi skin/chỉ số, túi giữ nguyên): tách `backpackCapacity` khỏi
      `MainCharStatsConfig` sang config player-level → switch không đụng inventory. Đơn giản nhất, hợp 1 MC.
    - **Backpack của CHARACTER** (mỗi con 1 túi/capacity riêng — hướng hiện tại vì config ở MC): reactive như
      trên + inventory per-char.
  - Chỗ đụng: `GameUI` (nghe `Spawned` để rebind), `PlayerSystem.SwitchTo`, `InventorySystem` (id per-char / recreate).
- [ ] **PrefabRegistry giữ hard-ref → mọi prefab thường trú RAM. Chuyển sang `Resources` + naming convention.**
  `PrefabRegistry` giữ `List<Identifiable>`, mà nó là DI singleton → load lúc khởi động là kéo theo **toàn bộ
  prefab có `Identifiable` + cả cây phụ thuộc** (mesh/texture/anim/material), thường trú suốt session.
  `UnloadUnusedAssets` cũng không giải phóng được vì registry vẫn giữ ref.
  - **Hướng đã chốt** (không dùng Addressables — rào cản kĩ thuật):
    - Prefab nằm dưới `Resources/<Category>/{id}.prefab`, phẳng theo category; **id = tên file** (khớp
      `Identifiable.Id` vốn đã fallback về `name`).
    - Load theo yêu cầu: `Resources.LoadAsync<GameObject>($"Units/{id}")` — **cùng cơ chế `MapService` đang
      dùng cho map**, gom project về một cách load duy nhất.
    - `PrefabRegistry` **hạ xuống editor-only validator**, bỏ list runtime: cảnh báo **trùng id**, check
      `Identifiable.Id` == tên file, check mỗi prefab có config khớp id. Đó là phần `Resources` không tự làm.
    - Thu hồi RAM ở **chỗ đổi map** (đã có load boundary + input gate): `Resources.UnloadUnusedAssets()`.
      Không gọi giữa combat — sweep toàn cục, gây khựng.
  - **Đánh đổi chấp nhận:** không giải phóng lẻ từng asset được (muốn vậy phải ref-count). RAM đi theo bậc
    thang — phình theo loại đã thực sự spawn, tụt sau mỗi sweep. Vẫn hơn hiện tại (thường trú vĩnh viễn).
  - **Lưu ý:** Addressables **không gỡ được** — dependency bắc cầu của `com.unity.localization`, mà UI
    framework đang dùng thật (`LocalizationWrapper`, `DynamicLabel`). Cứ để nằm im, **không dùng trong game code**.
- [ ] **Pickable registry.** Đang là `static List<Pickable> Active`. Nâng thành DI service (như
  `CombatWorld`) nếu cần query không gian ở quy mô lớn.
- [ ] **Đồng bộ: mọi thứ nhận dmg là `UnitController`, config từ Registry.** Đích: enemy, **cây, đá, rương**
  đều là `UnitController`; `Damageable` **trỏ `UnitController`** để đọc máu + team, không giữ config riêng.
  Max HP đọc từ đó nên **modify được** (đồ / nâng cấp / buff). Config lấy **từ `ConfigRegistry` theo id** (như
  `EnemyConfig`), **bỏ kéo `DamageableConfig` SO tay** vào từng prefab — **một đường config duy nhất** cho mọi
  loại. Hiện `Damageable` drag (cây) **hoặc** bind lúc spawn (enemy) chỉ là **bridge tạm** — xem `// TEMP` ở
  `Damageable.Team`/`Bind` + `EnemyConfig`. Kéo theo: cây cũng cần controller + config-kind (gộp với "Config
  cho từng loại cây/đá" ở trên), và đặt/spawn qua đường inject config như `EnemySpawner`.
- [ ] **Config gắn bằng code, phụ thuộc interface.** `Damageable`/`Dropable` đang `[SerializeField]` SO cụ
  thể (`DamageableConfig`) vì Unity không serialize interface — nên phụ thuộc nguyên SO. Sau gắn config bằng
  code (provider theo id) để chỉ phụ thuộc `IDamageableConfig`/`IDeathDropableConfig`. (Xem `// TEMP` ở 2 field.)
  Việc này gộp vào mục "Đồng bộ mọi thứ nhận dmg là `UnitController`" ngay trên.
- [x] **Pool đồ spawn/destroy nhiều (LeanPool).** ✅ Plugin copy vào `Assets/Plugins/CW` (LeanPool +
  LeanCommon + CW.Common, chỉ `Required`, bỏ Examples/Extras; trim ref HDRP khỏi `CW.Common.asmdef` vì
  project URP). Gọi **thẳng** `LeanPool.Spawn/Despawn` (đã là API static mỏng, không bọc `ISpawner` — rule
  of two). Sites: `Dropable.Drop` + `Pickable.SpawnFlyVisual` → `Spawn`; `Pickable` (nhặt hết) +
  `PickupFlyVisual` (tới nơi) → `Despawn`. Reset khi tái dùng: `Pickable.OnEnable` (CanPick) +
  `FlyingPickup.Launch` (vel/height/body) sẵn đủ; **`PickupFlyVisual.Launch` phải khôi phục scale** (bay
  xong bị co lại → cache `_restScale` ở Awake). Cây KHÔNG pool (đặt tay, churn thấp). Pool tự tạo theo
  prefab lần Spawn đầu — muốn **prewarm** thì gắn `LeanGameObjectPool` + set Preload, tuỳ sau.
- [ ] **Dọn serialized-ref / wiring.** Đang làm từ đầu nên còn bừa; gọn dần khi ổn định.

---

## ✅ Đã xong gần đây (tham chiếu)

Config-hoá stats MC · quy ước team (0 trung lập / 1 player / 2 địch) · `Damageable` (máu + drop) ·
hệ ngày/đêm (`DayNightClock/Config/Lighting`) + `Docs/LIGHTING.md` · hit-flash đỏ · **gỗ văng**
(velocity, height-trên-art, collision khi bay) + pickup (`Picker`/`Pickable`/`FlyingPickup`).
