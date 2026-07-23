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
