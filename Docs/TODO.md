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

## ⛰️ Terrain elevation (độ cao) — hướng đã chốt, triển dần

**Ý tưởng:** ô tile có **độ cao** (thấp/cao), cạnh giữa 2 ô lệch cao thì sinh **quad dựng đứng** nối
xuống làm vách. Art vách để **màu trơn hoặc hơi nhờ** trước, đẹp sau. Chấp nhận chưa tối ưu, tối ưu sau.

### 🏛️ Kiến trúc dữ liệu map — CHỐT

**CHỐT: 2 chiều + độ cao (heightmap), KHÔNG voxel 3D.** Lý do, cái đầu là quyết định:
1. **Toàn bộ logic đang là 2D trên XZ** — `WorldToCell` trả `(x,y)`, collision, spatial hash của
   `CombatWorld`, di chuyển, AI. Lên voxel là **viết lại cả tầng logic**, không phải thêm feature.
2. Camera gần ortho nhìn từ trên → hang/mái đua/cầu vượt (thứ duy nhất voxel cho thêm) **gần như không
   nhìn thấy**. Trả giá cực đắt cho thứ khuất tầm nhìn.
3. Nhiều tầng thật (hang, nhà nhiều tầng) → **map/scene riêng qua `MapService`** (đã có), không phải voxel.
   Stardew và Don't Starve đều làm vậy.

**CHỐT: height và vật liệu là 2 TRỤC ĐỘC LẬP.** Không ép vật liệu = f(height) — sẽ mất "đường đất vắt qua
bãi cỏ" (cùng cao khác chất) và "đường mòn leo đồi" (đổi cao không đổi chất). Map là open world **dựng tay**
nên đúng lúc cần kiểm soát thủ công nhất. Với lại `TerrainSet` + auto-tiling 3 chế độ đã đầu tư sẵn.

**Nhưng suy ra làm MẶC ĐỊNH, cho phép ghi đè** — sculpt xong là map tự đẹp, chỉ paint chỗ muốn khác:
- `height < waterLevel` → **nước** (tuyệt đối, không ghi đè được)
- sát ô ngập → **cát** · còn lại → **cỏ**

**CHỐT: 3 tầng dữ liệu, tách theo VÒNG ĐỜI** (đây là chỗ dễ làm sai nhất):
```
height[]   dense byte,  MÌNH dựng      → hình dáng           → trong prefab map
cells[]    dense byte,  MÌNH dựng      → ngoại lệ vật liệu   → trong prefab map   (0 = Auto)
soil       SPARSE dict, NGƯỜI CHƠI ghi → cày/ướt/cháy + data → trong FILE SAVE
```
- **`cells` đã tồn tại rồi** — chỉ cần định nghĩa **id 0 = "Auto"** (suy ra từ height). Không thêm mảng,
  không migrate: map hiện tại `cells` toàn 0 → tự thành "auto hết". Painter thêm ô "Auto" đầu palette để
  **xóa ngoại lệ về mặc định**. Giữ **dense**, đừng sparse — 256×256 = 64KB, không đáng tối ưu, mà mesh
  builder vốn duyệt hết mọi ô.
- **ĐỪNG nhét "đất cày" chung vào `cells`.** Nhìn thì cùng là "ô này vẽ khác đi", nhưng: ai ghi (mình vs
  người chơi), sống ở đâu (prefab vs save), cần bao nhiêu data (1 byte vs cây trồng gì/lớn tới đâu/độ ẩm/
  timer), mật độ (rải khắp vs **thưa thật**). Nhét chung → designer tô nhầm "đất cày" trong palette, và save
  phải diff nguyên mảng 64KB để biết người chơi đổi gì. Trạng thái runtime **sparse mới đúng**.
- **Đá/gạch/tường = OBJECT, không phải terrain** (đang làm đúng rồi: prefab + `Damageable` + `CollisionBody`).
  Ranh giới: **terrain là thứ đi lên trên, object là thứ đứng trên nó.** Tường xây sau cũng vào nhóm object,
  đặt theo cao độ của ô.
- **Hệ quả phải lường: rebuild theo CHUNK.** `TerrainRenderer.Build()` hiện phá sạch rồi dựng lại **toàn bộ
  layer × toàn map**, kèm tạo mới GameObject + mesh + material từng layer. Cày/phá một ô mà chạy cái đó là
  khựng thấy rõ. Chia mesh theo chunk (16×16 ô), chỉ dựng lại chunk bị đụng. Map lớn rồi cũng cần.
  - **⚠️ Phải bẩn cả chunk HÀNG XÓM.** Auto-tiling nhìn ô kề, và vách đá phụ thuộc height ô bên cạnh — nên
    ô nằm sát mép chunk thì chunk kế bên cũng phải dựng lại. Quên là ra **đường nối hở / sai tile ngay biên
    chunk**, loại bug rất khó lần ra vì chỗ sai không phải chỗ vừa sửa.
  - **Gom lại, dựng một lần.** Đánh dấu chunk bẩn vào một `HashSet`, `LateUpdate` mới rebuild. Người chơi
    phá 5 ô trong một nhát → **một** lần dựng, không phải năm.
  - Nước / cỏ / bóng đều **suy ra** nên tự đúng theo, chỉ cần mỗi bên cũng rebuild theo chunk của nó.
    `GrassField` **đã chia chunk sẵn** (`chunkSize`, `_chunks`) → nhẹ đô hơn terrain nhiều.
- **Terraforming (người chơi đào/nâng đất):** hiện coi như KHÔNG có, `height` là authored-only. Nếu sau muốn
  thì lưu **sparse diff** so với map gốc vào save, đừng copy cả mảng.

**Logic di chuyển đổi hướng:** thay vì `IsWalkable` bool như hiện tại, chặn theo **chênh lệch độ cao**
giữa 2 ô + một setting **"khả năng leo"** trên từng mover → con **cao lớn vượt được bậc cao hơn**, con
nhỏ thì không. Vừa là luật đi lại, vừa là công cụ design (gating khu vực bằng địa hình).

- [ ] **① SPIKE trước — đừng làm gì khác cho tới khi cái này xanh đèn.** Chỉ **2 mức cao** (0 và 1), một
  loại vách, hard-code, chưa paint tool, chưa ramp. Câu hỏi duy nhất cần trả lời: *bật depth rồi
  sprite/billboard/shadow/cỏ có sống sót không?* Nửa ngày.
- [ ] **② Mesh.** `TerrainRenderer.AddQuadUV` đang hard-code `y = 0f` — đổi thành `heights[cell] * stepHeight`.
  Thêm `byte[] heights` song song `cells` trong `TerrainGrid`. Vách: mỗi ô so 4 hàng xóm, ô nào thấp hơn
  thì emit quad đứng dọc cạnh chung từ `myY` xuống `neighbourY`.
  - **Gotcha:** giữ height **per-cell**. Đừng bắt `DualGrid` (chạy theo góc, mỗi góc chạm 4 ô) xử height
    khác nhau — auto-tiling chỉ chạy trên **mặt trên**, vách dựng riêng.
- [ ] **③ Sorting/che khuất — cái nặng nhất, đụng nền móng.** Terrain đang xếp bằng `sortingOrder`, material
  **không ghi depth** (xem comment trong `TerrainRenderer.Build`). Có độ cao thì vách phải che vật đứng
  sau-dưới, mà vật đứng **trên** vách lại phải vẽ đè — một con số sorting order không tả nổi.
  - Mẹo "sorting order theo mức cao" kiểu Stardew **vỡ khi xoay camera** (`RotateYawCommand`) — cái đang
    sau thành trước. Nên với game này gần như **bắt buộc dùng depth thật**: terrain **bật ZWrite**, sprite
    **depth-test nhưng không depth-write**.
- [ ] **④ Shadow phải bỏ giả định "đất phẳng".**
  - `_ShadowGroundY` đang là **float global** → cây trên đồi đổ bóng rơi tuột xuống y=0. Phải thành
    per-object (sample height tại ô của nó → đẩy qua `MaterialPropertyBlock`).
  - `ShadowComposite` tệ hơn: quad fill là **một mặt phẳng ở `height = 0.03`** → chỉ phủ đúng một tầng.
    **Fix gọn hơn cả code hiện tại:** đổi fill thành **fullscreen quad stencil-test** — tô tối theo pixel
    đã đánh dấu, bất kể pixel đó ở độ cao nào. Bỏ luôn được đống auto-size theo viewport corner.
- [ ] **⑤ Logic di chuyển.** `TerrainGrid.IsWalkable`/`CanPass` thêm luật chênh cao + `climbHeight` trên
  mover. Kèm tile **dốc/ramp** để lên xuống có chủ đích. Làm SAU khi ③ ổn.
- [ ] **⑥ Paint tool.** `TerrainGridEditor` thêm brush cho height (cùng khuôn brush terrain sẵn có).
- [ ] **⑦ Cỏ.** `GrassField` đang scatter phẳng theo `transform.position` (`-sink`) → phải sample height.

### 🌊 Nước — mọc thẳng ra từ height, không phải hệ riêng

**CHỐT: KHÔNG paint nước, không có cờ "ô này có nước".** Một `waterLevel` (float), ô nào `height < waterLevel` là ngập. Tự nhất quán: đào hố
là nước tràn vào, nâng đất là nước rút. (Muốn hồ trên cao thì sau thêm `waterLevel` theo vùng — bắt đầu global.)

- [ ] **Mesh nước.** Thêm một layer trong `BuildLayerMesh`: quad phẳng ở `y = waterLevel`, chỉ emit cho ô
  ngập. Material `Queue = Transparent`, **ZWrite Off**, vẽ sau đất để đáy hiện xuyên qua.
  → **Phụ thuộc thẳng vào ③.** Với sorting-order-không-depth hiện tại thì cực khổ; ③ xong là gần như tự chạy.
- [ ] **Độ sâu bake vào vertex — đừng sample `_CameraDepthTexture`.** Lúc build mesh đã **biết sẵn cao độ đáy
  từng ô**, nên nhét thẳng `waterLevel - groundY` vào **vertex color / UV2**. Không tốn depth prepass (đắt
  cho mobile), mà lại chính xác hơn.
  - Tint theo độ sâu: `lerp(_Shallow, _Deep, depth/_DepthFade)`, alpha cũng theo depth (nông thì trong).
  - **Foam ven bờ free luôn** — bờ chính là chỗ `depth` nhỏ: `foam = 1 - smoothstep(0, _FoamWidth, depth)`,
    nhân thêm noise scroll. Đây là thứ bán cảm giác "nước" mạnh nhất, đừng bỏ.
  - Sóng: scroll noise trên tint + lấp lánh là đủ cho tông stylized. **KHÔNG refraction** (cần grab-pass,
    đắt trên mobile), **KHÔNG reflection**.
- [ ] **Chặn di chuyển theo ĐỘ SÂU — cùng một luật với chênh cao, chỉ đổi dấu.**
  Mặt nước coi như mốc; `waterDepth = waterLevel - cellHeight`. Chặn khi `waterDepth > mover.wadeDepth`.
  - Đúng cùng hình dạng với `heightDiff > mover.climbHeight` ở ⑤ → **một luật, hai công dụng**, không cần
    hệ thống riêng cho nước.
  - Con **cao lớn lội được nước sâu hơn** (và trèo bậc cao hơn) — vừa là luật đi lại, vừa là công cụ design
    để gate khu vực bằng địa hình.
  - Sau có thể thêm cờ `canSwim` cho nước sâu, và trạng thái "đang lội" (chậm lại, hiệu ứng nước).
- [ ] **Cỏ KHÔNG spawn trên ô ngập** — `GrassField` phải đọc mask nước (chung với ⑦).
- [ ] **Bóng đổ trên mặt nước** — với fullscreen stencil quad ở ④ thì tự đúng, không phải làm gì thêm.

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
