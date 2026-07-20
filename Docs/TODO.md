# TODO — Adventure

Việc còn nợ, gom theo mảng. Cập nhật dần; đánh dấu `[x]` khi xong.

---

## 🎮 Core loop / gameplay

- [ ] **Máu Player (bespoke IDamageable).** Player *là* IDamageable nhưng đặc biệt: HP vào
  `MainCharStatsConfig`, tự implement (KHÔNG dùng `Damageable`), team **1**, đăng ký vào `CombatWorld`
  (`combat.Add`). Chết = game-over/hồi sinh, *không* rơi loot. Không i-frame, không khoá hành động.
- [ ] **Inventory / Backpack.** `Pickable.Collect()` đang chỉ `Destroy` — chỗ `// TODO(inventory)` là
  nơi cộng resource vào backpack khi có hệ inventory.
- [ ] **Damage do chạm / DoT + cooldown theo nguồn.** Quái húc / đứng trong lửa: mỗi *nguồn* có nhịp
  trừ máu riêng (vd 0.5s/lần), độc lập nhau. KHÔNG i-frame ở người nhận (mọi dmg đều tính). Làm khi
  dựng enemy.

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
    - `Damageable` lo máu + `Died`→loot. **HP đang ở `DamageableConfig.MaxHp`**, mà `EnemyConfig` cũng có
      `hp` → chọn **1 chủ máu**, tránh 2 nguồn (đề xuất: bỏ `hp` khỏi `EnemyConfig`, để `Damageable` giữ
      máu+loot; `EnemyConfig` chỉ còn move/attack).
    - `CombatWorld.Overlap(centre, radius, attackerTeam, results)` — dò mục tiêu quanh quái (query **team 1**
      = player trong bán kính aggro).
    - `SwingAttack` là **khuôn đòn**: ở frame `Hit` của `CharacterAnimator` → `Overlap` quanh origin →
      `TakeDamage`. Đòn quái mirror y hệt nhưng **team 2**, đánh player.
    - Di chuyển: khuôn `Character.Move` + `CollisionBody` (không xuyên đá). Quái thay input tay bằng input
      do AI sinh (hướng tới target).
    - Team: 0 trung lập / 1 player / 2 địch — không friendly-fire cùng team. Spawn **qua DI container** (hoặc
      Auto Inject của `GameScope`) để `CombatWorld` được inject vào `Damageable`/đòn.
  - **Phải làm tối nay:**
    - [ ] **Player hittable TRƯỚC** (mục "Máu Player" ở đầu file) — hiện `Character` chưa implement
      `IDamageable`, chưa `combat.Add` → **quái không có gì để đánh**. Đây là chặn đầu tiên.
    - [ ] **AI brain** — FSM **code thuần** (idle → phát hiện theo aggro radius → đuổi → đánh khi trong tầm +
      cooldown → mất dấu/về). KHÔNG behavior-tree SO, KHÔNG data-driven (xem quy ước "runtime là plain code").
    - [ ] **EnemyMelee** — mirror `SwingAttack`, team 2, dmg/tầm/nhịp từ `EnemyConfig`.
    - [ ] **EnemyMotor** — steer tới target qua `CollisionBody`, tốc độ từ `EnemyConfig.moveSpeed`.
    - [ ] **Spawn** — tạm đặt tay 1-2 con để test (nhớ cho vào Auto Inject); spawner thật sau.
    - [ ] Art: hero/quái cận cảnh → **AnimatorController** (blend/attach); crowd đông → cân nhắc
      AnimationInstancing. `CharacterAnimator.Hit` là seam frame-đánh.
  - **Gotcha:** bán kính `Overlap` phải ≤ cell hash (`4`) không thì miss (có warning); đòn tự `Rebuild()`
    trước query — xem `SwingAttack.OnSwingHit`.
- [ ] **Rương (chest).** *Breakable, KHÔNG phải pickup.* Rơi ra nằm trên map, có `CollisionBody` (chiếm
  chỗ), là `Damageable` — chém vỡ (`Died`) → `Dropable` rơi đồ khác. Không nhặt trực tiếp. → tái dùng
  nguyên pattern cây (`Damageable` + `Dropable` + `DropOnDeath` + `CollisionBody`). Thêm: **save các rương
  trên map** (vị trí + trạng thái) — cần cơ chế persist object trên map (chưa có). Làm sau.

## 🌤️ Environment (day/night + weather)

- [ ] **Weather system.** Cắm vào seam `--- Weather seam ---` trong `DayNightLighting.LateUpdate`:
  weather biến đổi `EnvironmentState` (ambient/fog/intensity) *sau* day/night rồi mới đẩy vào LightManager.
  - [ ] **SunnyWeather**: bóc cái glare vàng trưa (`#ACAE72`) từ base day/night ra đây
    (xem `// TODO(weather)` trong `DayNightConfig`). Trời âm u thì trưa không chói.
  - [ ] Mưa / sương mù / tuyết: fog (cộng sáng/haze) + giảm intensity + tông ambient.
- [ ] **Day/night timing → config + save.** `DayNightClock` đang hard-code `DayLengthSeconds` +
  `StartTime` (`// TODO: load from save`). Đưa ra config, và giờ khởi động lấy từ save.

## ✨ Polish / feedback

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

- [ ] **Esc bị 2 chủ.** `UISystem.Update` và `GameController.Tick` cùng xử Esc (đang né bằng
  `CloseOnEscape=false`). Gộp về một chỗ sở hữu.
- [ ] **Picker → interface config.** `Picker` đọc thẳng `ICharacterStats.PickupRadius` (giờ chỉ MC nhặt).
  Khi có picker khác MC → tách interface cung cấp config. Giờ overkill.
- [ ] **Pickable registry.** Đang là `static List<Pickable> Active`. Nâng thành DI service (như
  `CombatWorld`) nếu cần query không gian ở quy mô lớn.
- [ ] **Config gắn bằng code, phụ thuộc interface.** `Damageable`/`Dropable` đang `[SerializeField]` SO cụ
  thể (`DamageableConfig`) vì Unity không serialize interface — nên phụ thuộc nguyên SO. Sau gắn config bằng
  code (provider theo id) để chỉ phụ thuộc `IDamageableConfig`/`IDeathDropableConfig`. (Xem `// TEMP` ở 2 field.)
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
