# TODO — Adventure

Việc còn nợ, gom theo mảng. Cập nhật dần; đánh dấu `[x]` khi xong.

---

## 🎮 Core loop / gameplay

- [ ] **Dash gây sát thương khi lướt qua.** Cơ chế thì rõ, **chỉ chưa chốt được sát thương lấy từ đâu** — và
  đó mới là lý do chưa làm, không phải vì khó.

  - **Câu hỏi mở: nhân theo cái gì?** `AttackPower` (nó là một đòn đánh), **mass** (nó là một cú húc — mà dash
    đang ×5 mass sẵn, nên con nặng húc đau là tự nhiên), **máu tối đa** (build trâu bò), hay một số phẳng trên
    component. **Nhiều khả năng tuỳ nhân vật**, nên đừng hard-code: nó muốn là một lựa chọn author trên chính
    skill đó, kiểu `IUpgradeEffect` chọn loại bằng `[SerializeReference]`. Chốt khi có nhân vật thứ hai có
    dash, chứ một con thì không nhìn ra hình.
  - **⚠️ Phải quét theo ĐƯỜNG ĐI, không phải overlap một lần.** `ShapeAttack` overlap tại một khoảnh khắc vì
    cú vung đứng yên; dash thì di chuyển nhanh nhất game, nên một con quái đứng giữa hai frame sẽ bị bỏ sót
    hoàn toàn. Cùng đúng cái vấn đề mà bước lướt đang phải kẹp `Radius * 0.9` để khỏi xuyên tường — ở đây là
    xuyên **qua người**. Quét từng bước một trong `StepDash` là đủ, không cần capsule cast.
  - **Mỗi mục tiêu chỉ trúng một lần cho cả cú lướt.** `ShapeAttack` đã có mẫu: một `List<IDamageable>` giữ
    những đứa đã trúng. Của dash thì list sống suốt cú lướt chứ không phải một frame — không thì lướt qua một
    con là nó ăn một đòn mỗi frame.
  - **Tâm phát lực là vị trí người lướt tại thời điểm chạm**, không phải điểm bắt đầu. `DESIGN.md` nói loot
    văng ra theo `targetPos − forceOrigin`, nên làm đúng chỗ này thì đồ rơi tự rải dọc theo đường lướt — đúng
    cái ý "cách đánh quyết định đồ rơi ở đâu" mà doc đang bán.
  - Knockback thì gần như chắc chắn muốn: đẩy theo **hướng lướt**, không phải hướng toả tròn từ người.

- [x] **CHIA LẠI PHE (team).** ✅ Thay hẳn quy ước cũ `0 trung lập / 1 player /
  2 địch / 3 prop`. Ý chính: **địch không còn là MỘT phe**. Mỗi loài quái là một phe riêng, nên quái đánh nhau
  được, và "kẻ thù của tôi" không còn suy ra được từ một con số duy nhất.

  | Team | Là ai | Ghi chú |
  |---|---|---|
  | **0** | **không ai cả** — là *nguồn dmg chung* | dmg gắn team 0 thì **mọi thứ đều phải chịu**. Bẫy, lửa, môi trường. |
  | **1** | người chơi | |
  | **2** | **động vật hiền lành** (mewfrog) | **không bao giờ chủ động đánh nhau** |
  | **10000** | **tài nguyên** (cây, đá, rương) | thay cho team 3 cũ |
  | 3, 4, 5… | mỗi loài quái hung dữ một phe | PP1 (cây ăn thịt) một phe riêng; nó săn **các phe khác**, gồm cả mewfrog |

  - **Không đụng `CombatWorld`** — luật lọc `if (attackerTeam != 0 && target.Team == attackerTeam) continue;`
    vốn đã đúng: attacker team 0 không lọc gì cả, trúng tất. Đúng y nghĩa "dmg chung".
  - **Mọi số phe gom vào `Teams`** (`Script/Combat/Teams.cs`) — `Universal/Player/Critter/Resource/FirstMonster`.
    Hết magic int trong `_Project/Script`. Chính vì trước đây rải 8 file mà "prop là team 3" và "quái là team 2"
    âm thầm hết khớp với một nửa số comment nói về chúng.
  - **Quái mỗi loài một phe:** `EnemyConfig.team` (field mới, mặc định `FirstMonster`), `EnemyController.Team`
    đọc từ đó thay vì hằng `2`. `mewfrog: 2` (Critter), `pp1: 3`. Đây mới là thứ cho quái đánh nhau — một phe
    "địch" dùng chung vô tình biến mọi con quái thành đồng minh của nhau.
  - **⚠️ Điểm cốt lõi, không phải chuyện đổi số: "khác phe" ≠ "là mục tiêu".** `CombatWorld` chỉ biết "khác phe
    nên đòn trúng" — đúng với cái cây, và đó chính là lý do cái rìu bổ được nó. Nó **không** nói con vật có nên
    *muốn* đánh cái cây hay không. Tách bằng `Teams.IsPrey(team)` (= `team < Resource`):
    - `AIContext.FindHostile` lọc `IsPrey` → thú không bao giờ chọn tài nguyên làm mồi. Không có nó thì PP1
      nhè gốc cây gần nhất mà gặm trong lúc player đi ngang.
    - `SoulFire` bỏ `_priorityTeam` (đóng đinh thế 1↔2, vỡ khi có N phe) → ưu tiên `IsPrey`: vẫn đốt được cây,
      nhưng không bao giờ ưu tiên cây hơn thứ đang đánh mình. Giải luôn mục "SoulFire ưu tiên target" bên dưới.
    - `Resource = 10000` để xa hẳn mọi phe sinh vật nên `IsPrey` là **một ngưỡng**, không phải danh sách phải
      nhớ mở rộng — loại cảnh vật mới tự rơi đúng phía.
  - **Fallback đã sửa cho "sai to hơn là sai ngầm":** `Damageable.Team` khi không có `Unit` giờ là `Universal`
    (ai cũng đánh được) chứ không phải `2` — với nghĩa mới, `2` sẽ khiến nó **âm thầm nhập bọn động vật hiền
    lành và bị thú săn**. `ShapeAttack`/`SoulFireAttack` không chủ cũng `Universal`. Xoá key chết `team: 2`
    trong `basic_tree.asset` (`PropConfig.Team` là property tính sẵn, field đó không còn tồn tại).
  - **Còn hở, chưa chốt:**
    - "Không bao giờ đánh nhau" của phe Critter mới chỉ đúng **một nửa**. Mewfrog không chủ động gây sự
      (`PassiveAggro` trả `null`) nhưng **vẫn đánh trả** khi bị đánh — đường `EnemyAI.OnDamaged` là của FSM
      chung, không đi qua behaviour nào nên không tắt được bằng asset. Nếu "hiền lành" nghĩa là *không bao giờ
      đánh, kể cả bị đánh*, thì cần một cờ trên brain (kiểu `fightsBack`) hoặc `OnDamaged` phải hỏi behaviour.
      **Chưa làm** — chờ chốt.
    - Quái **cùng loài** không bắn trúng nhau (cùng phe → `CombatWorld` lọc). Khác loài thì có. Đúng ý chứ?
    - PP1 giờ săn được mewfrog rồi, nhưng mewfrog **chưa biết chạy** — xem `SkittishRoam` ngay dưới.

- [x] **Mewfrog roaming biết né.** ✅ Đang đi dạo mà có sinh vật **khác phe** lọt vào bán kính nhỏ thì bỏ chạy,
  thay vì lững thững đi tiếp. Hành vi lúc **chưa aggro** — khác `leashRadius` (bỏ đuổi) và khác `PassiveAggro`
  (chỉ đánh trả). `FindHostile` trả đúng "khác phe, không phải cảnh vật" nên không cần thêm gì.
  - **Thuộc `IIdleBehavior`** — không phải `IAggro` (nó trả *target*, mà có target là FSM sang `Chase`, con ếch
    sẽ **lao tới** chứ không chạy đi), cũng không phải state thứ 5 (FSM cố ý một hub quyết định duy nhất là Idle;
    bỏ chạy là thứ làm *thay cho* combat, không phải một nhánh của nó).
  - **KHÔNG tách `SkittishRoam` như kế hoạch cũ ghi ở đây — nằm luôn trong `WanderRoam`.** Bỏ chạy phải huỷ
    **đích đang đi** và **timer nghỉ**, mà cả hai là state riêng của `WanderRoam`. Tách wrapper thì hết hoảng con
    vật quay lại đi tiếp tới điểm nó chọn *trước khi* sợ — với bán kính nhỏ thì thường là chính chỗ mối nguy đang
    đứng. Muốn vá thì `IIdleBehavior` phải mọc thêm `Interrupt()`, tức ba behaviour kia gánh một khái niệm chúng
    không dùng. `fleeRadius = 0` là tắt, con nào không cần không trả giá gì.
  - **KHÔNG phải hoảng chạy — là nhường chỗ.** Đi đúng tốc độ `amble` như lúc dạo; thứ duy nhất đổi là **chọn đi
    đâu**. Chạy nhanh hơn thì đọc ra "con mồi bỏ chạy khỏi thú săn", còn cái muốn ở đây là "không thích bị đứng
    sát". Vì thế **không có** field tốc độ riêng: giá trị đúng duy nhất luôn là `amble`.
  - **Không cần hysteresis** như ghi chú cũ lo. Chống strobe bằng đúng cái mẹo `WanderRoam` vốn đã dùng để đi dạo:
    **chốt một điểm đến rồi đi tới đó**. Không có đường nào "đẩy ra mỗi frame khi còn trong bán kính".
  - Ba số, mỗi số một việc: `personalSpace` (mewfrog 2.5) là thứ giữ cho việc này **hiếm**; `settleTime` 0.5s là
    **nhanh có chủ đích** — cứ đi tới là nó cứ dạt ra, dài hơn thì nó nhường một lần rồi đứng chịu trận, đọc ra
    như hỏng chứ không như điềm tĩnh; `ScanInterval` 0.25s (const, không phải knob) chỉ để con vật đứng giữa đồng
    trống khỏi quét `CombatWorld` (rebuild cả spatial hash) **mỗi frame**, vì `settleTime` chỉ bắt đầu tính sau
    khi đã thực sự có ai lại gần.
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

- [ ] **Exp — trả cho "lần đầu", không trả cho "lặp lại".** `CharacterLevels.AddExp` đã có curve, đã có save,
  HUD đã bind — **thiếu đúng phần ai trả exp**. Call site duy nhất hiện tại là nút cheat.

  Luật chọn nguồn đến từ pillar: level = điểm nâng cấp = sức mạnh, mà `DESIGN.md` nói *"ngày một map an toàn
  farm ngon hơn frontier là drop table hỏng"*. Nên exp phải chịu đúng luật đó.

  | Hành động | Kiểu | Số nằm ở đâu |
  |---|---|---|
  | Vào một map **lần đầu** | one-shot, lớn | field mới trên `Map` — giá trị khám phá là của chính map đó |
  | Giết **lần đầu** một *loài* | one-shot, vừa | `EnemyConfig.firstKillExp` |
  | Mở xong một `PayGate` | one-shot, vừa | field trên `PayGate` (đã có id + save sẵn) |
  | **Mỗi con quái** giết được | lặp lại, nhỏ | `EnemyConfig.exp` — con frontier đáng gấp nhiều lần con sân nhà |
  | *(sau)* ghi nhớ một bệ đá | one-shot, lớn | cùng đường `AwardOnce`, dựng khi có bệ đá |

  - **Prop (cây, đá) KHÔNG cho exp.** Nguồn vô hạn, không rủi ro, đứng một chỗ chặt được cả buổi — cho exp là
    biến cái rìu thành build tối ưu và biến thanh level thành cái đếm gỗ. Nó đã trả bằng tài nguyên rồi. Cưỡng
    chế bằng **interface**: `PropConfig` không implement `IDeathExpConfig`, nên "cây không cho exp" là một sự
    thật của config chứ không phải một câu `if` nằm đâu đó.
  - **⚠️ Chỗ khó nhất: level là PER CHARACTER** (xem `CharacterLevels`). Nếu "firsts" cũng khoá theo nhân vật
    thì mở nhân vật 2 → level 1 → phải đi lại toàn bộ map để lên level, đúng thứ backtracking pillar từ chối.
    Nếu firsts là toàn cục và chỉ trả cho người đang chơi thì nhân vật 2 vĩnh viễn không đuổi kịp.
    **Cách giải:** discovery exp là **một tổng của THẾ GIỚI**; mỗi nhân vật lưu "đã hấp thụ tới đâu" và được kéo
    lên bằng tổng đó lúc được chơi. Nhân vật mới mở ra là đã đủ level để chơi ở chỗ đang chơi. Riêng exp giết
    quái thì thật sự của cái thân đang chơi → chênh lệch nhỏ giữa các nhân vật, và đó là chênh lệch đúng.
  - **Tổng phải được LƯU, không tính lại từ danh sách key.** Tính lại nghĩa là tra từng first ngược về config,
    nên ngày ông chỉnh giá trị khám phá của một map là mọi save đang tồn tại âm thầm nhảy level. Cùng lý do
    `CharacterLevels` lưu tiến độ-trong-level chứ không lưu tổng: chỉnh số chỉ được đổi thứ **tiếp theo** đáng
    bao nhiêu, không được đổi chỗ người chơi đang đứng.

  **Implement:**
  - `Script/Progress/ExperienceSystem.cs` — entry point trong `GameScope`, `ISavable`, ngồi trên
    `CharacterLevels`. State: `HashSet<string> _firsts` + `int _discovery` (tổng toàn cục) +
    `Dictionary<string,int> _absorbed`. API: `Award(amount)` (lặp lại, vào nhân vật đang chơi),
    `AwardOnce(key, amount)` (firsts, gộp vào tổng), `CatchUp(characterId)`. Key kiểu `map:Map_2`,
    `kind:mewfrog`, `gate:bridge_1`. Lấy id nhân vật qua `IPlayer.Current.Id` + nghe `Spawned`, **không**
    depend `PlayerSystem` (vòng tròn).
  - `ExpOnDeath` (`Script/World/`) — **cùng pattern `DropOnDeath`**, nghe `Damageable.Died`. Chính comment của
    `Damageable` đã khai chỗ này: *"a DropOnDeath (or anything else — sound, XP, break FX) listens"*. Khác duy
    nhất: exp đi vào một service nên component cần `[Inject]` (enemy đã được inject qua scope của
    `EnemySpawner`). **Không** hook trong `EnemySpawner` — ngày mai thêm âm thanh chết, đếm kill cho quest,
    bestiary là spawner thành cái túi đựng subscription.
  - **Lọc kẻ giết**: `Died` mang `source` là component đánh trúng (`ShapeAttack` truyền `this`, `SoulFire`
    truyền caster) → `GetComponentInParent<MCController>()`. Sói cắn chết thỏ thì không ai được exp.
  - `IDeathExpConfig { int Exp; int FirstKillExp; }` trên `EnemyConfig`, đọc bằng
    `_unit.DamageableConfig as IDeathExpConfig` y như `Dropable` đọc drops.
  - Thứ tự: **làm cho quái trước**, map/gate sau (cùng `AwardOnce`, chỉ là thêm call site).
  - **⚠️ Cẩn thận khi nối vào:** `CharacterLevels.Write` gọi `_save.Save` mỗi lần `AddExp`, tức là **ghi file
    mỗi con quái chết**. Vừa là mùi hiệu năng trên mobile, vừa trái luật save của `DESIGN.md` (chỉ ghi khi về
    nhà và khi chết). Chốt cái này trước khi exp có nguồn thật.

- [x] **`Map_3` — đảo, sông, cỏ, zone quái, sinh bằng script.** ✅ Một hòn đảo, **bốn bề là nước**, bờ cách rìa
  grid **8 ô** (yêu cầu ≥5) nên còn dư đất cho lối sang map khác, và dải sương viền map nằm **hoàn toàn trên mặt
  nước** — đúng cảm giác "thế giới tan vào sương mù ngoài khơi".
  - **Nó là `Map_3`, KHÔNG phải `Map_1`.** Bản này là mẫu/đối chiếu; `Map_1` đã được **trả về nguyên trạng**
    (`baked: 1`, `Map_1_Bake/`, 1 GrassField, 2 zone, 2 gate) và vẫn là map spawn vì
    `GameController.StartMapId` hard-code `"Map_1"`. **`Map_3` hiện chưa có đường tới** — chưa portal nào trỏ vào
    nó; muốn vào thì đổi `StartMapId` hoặc trỏ `Portal` của `Map_1` sang.
  - **Sông 3 khúc CẮT ĐÔI đảo thật, không phải trang trí.** Kiểm bằng cách bỏ ford ra rồi đếm mảnh đất:
    **833 + 219 ô** (hai mảnh). Cắm lại 2 **ford bằng mud** thì về **1080 ô liền mạch**. Nên muốn sang bờ bắc
    là phải đi tìm chỗ cạn — đó là lý do con sông tồn tại. Chỗ đứng của spawn nằm ở **mảnh lớn**, cố ý.
  - 3 hồ + 4 vũng nhỏ, **chọn chỗ bằng trường khoảng-cách-tới-nước** chứ không gõ toạ độ tay: chỗ sâu trong đất
    nhất mới là chỗ một cái hồ đọc ra là hồ thay vì một khúc bờ biển lởm chởm, và nó tự chọn lại khi bờ hoặc
    sông đổi. Viền hồ có harmonic giống bờ biển: bán kính trơn thì raster ra **hình thoi**, mà 7 hình thoi
    giống nhau thì trông như sân golf.
  - Mud là **bờ 1 ô** ở mọi chỗ đất giáp nước (4 hướng, không tính chéo — chéo không phải bờ, tính vào thì mud
    gấp đôi). Không dùng **Brick**: gạch là dấu vết con người, tức là *nội dung*, để bạn tự đặt.
  - 5 `Meadow_*` (`GrassField` 24×20, density 55) thay cho 1 field cũ; mask **tắt sạch ở ô không phải Grass**
    nên không có cọng cỏ nào mọc trên bờ mud hay dưới nước, và có noise nên nó là đồng cỏ chứ không phải bãi
    cỏ sân vườn. 4 zone quái: **mewfrog ×2 ở đồng trống**, **pp1 ×2 ở đất ẩm cạnh hồ/sông** — cũng đúng chỗ
    người chơi phải đi qua khi tìm ford. Zone cách spawn ≥10 ô: zone `warm` đầy ngay lúc load, đặt trúng spawn
    thì người chơi hiện ra *giữa ổ* thay vì đi tìm được nó.
  - 9 cây + 4 đá cũ được **rải lại** (2 lùm + 1 bãi đá) — không phải việc được nhờ, nhưng để nguyên thì cả 13
    cái nằm ngoài biển.
  - **⚠️ Prefab được viết từ NGOÀI Unity, nên mọi thứ Unity thường bake đã bake bằng tay:** `cells`,
    `walls` (port `WalkBake` 1:1 — kiểm chứng bằng cách so wall đầu tiên với output cũ của Unity, **trùng khít**),
    mask của 5 field cỏ, và `cells` baked của 4 zone (mirror `SpawnZone.Bake` + `Spawnable`). Mesh terrain là
    thứ **duy nhất** không bake — xem mục "Bake mesh terrain" ở phần Flat tile map.
  - **Gate/Portal chỉ là tạm**: `Gate_0` (spawn) giữa đảo, `Gate_1` + `Portal_map2_0` ở bờ đông nam. Đặt sao cho
    game chạy được ngay, không phải thiết kế lối đi — chỗ nào gate nằm trong sương, chỗ nào không, là việc sau.
  - Script sinh map là **đồ dùng một lần**, cố ý **không** commit vào project. Từ giờ **editor là source of
    truth**: vẽ lại bằng `TerrainGridEditor` + `GrassFieldEditor` + nút Bake của zone.

- [x] **Cầu — geometry riêng, ghép ở tầng truy vấn.** ✅ `IWalkVolume` + `TerrainQuery` + `BridgeShape` +
  `Bridge`. **Thay hoàn toàn hướng per-ô của bản trước** (`IDeck`, bitmap `deck` trong `TerrainGrid`) — hướng đó
  đã bị xoá sạch.
  - **Luật kiến trúc: `TerrainGrid` KHÔNG biết cầu tồn tại.** Nó là tilemap thuần — ô đã paint, face của chính
    nó, region của chính nó. Không một chữ "deck" nào trong file. Cầu là geometry **thứ hai, độc lập**, do
    chính object `Bridge` cung cấp. Hai nguồn không bao giờ được ghi vào nhau.
  - **LUẬT DUY NHẤT: tường là BIÊN CỦA HỢP.** Tập đi được là hợp của tilemap và các deck, nên
    `∂(A ∪ B) = (∂A \ trong B) ∪ (∂B \ trong A)`. Viết ra:
    - `walkable(p) = anyDeckCovers(p) || tilemap.walkable(cell(p))`
    - `faces(ô) = biên tilemap \ trong(mọi deck)  +  biên deck \ (đất đi được ∪ các deck khác)`
    Hoàn toàn đối xứng, và cả hai vế đi qua **cùng một hàm trừ** (`TerrainQuery.WalkableRanges`). Không bản nào
    được lưu — dựng lại mỗi truy vấn, nên cầu trượt/nhấc/xoá có hiệu lực ngay ở câu hỏi kế tiếp.
  - **KHÔNG CÓ KHÁI NIỆM "LAN CAN".** Bản đầu tôi viết bắt shape khai cạnh nào là sườn (tường), cạnh nào là đầu
    (hở) — **sai hướng**. Shape phát **toàn bộ** outline, không phán xét gì; đầu cầu hở *tự động* vì phần biên đó
    nằm trên đất đi được nên bị trừ. Hệ quả, không phải ca đặc biệt nào cả:
    - Cầu bắc qua **đảo nhỏ giữa sông** → mở ra đảo đúng như phải thế (trước đây tôi ghi đây là "giới hạn đã biết").
    - **Hai cầu nối đuôi** → không rào lẫn nhau, nhờ số hạng `∪ các deck khác`. Cách nghĩ "lan can" không bao giờ
      gợi ra số hạng này.
  - **Deck là hình tự do, đơn vị thế giới** (`BridgeShape` → `DeckRect`). Không raster xuống ô, không đo theo
    grid — deck rộng 1.4 đơn vị thì đúng 1.4, cầu chéo là hình chữ nhật đã quay chứ không phải bậc thang. Điều
    này **lật lại** kết luận cũ ("art hẹp hơn 1 ô là không dùng được", "art phải trùm hết ô vàng"): art và vùng
    đi được giờ là **cùng một hình**.
  - **Bất biến của một shape: `Outline` phải đúng bằng biên của `Contains`.** Cạnh outline không nằm trên biên →
    tường lơ lửng; đoạn biên không có cạnh → lỗ để đi tuột khỏi deck. `DeckRect` (lồi) thoả tuyệt đối.
    - **Đã thử và BỎ `DeckPath`** (cầu bẻ góc, chuỗi slab): hợp của nhiều mảnh thì biên mỗi mảnh còn phải trừ
      ruột các mảnh khác, và chỗ bẻ góc biên thật là **cung tròn** mà `WallSeg` chỉ là đoạn thẳng. Đo được: vi
      phạm bất biến 65% số ca, hở tới 0.78 đơn vị. Muốn cầu bẻ góc thì phải làm miter đa giác để `Contains` khớp
      — chưa ai cần nên chưa có.
  - **Trừ thì trừ theo WALL, không theo ô.** Face của tilemap **thụt vào `WallSeg.Inset` = 1/8 ô**, nên cắt
    outline theo "ô nào walkable" làm cạnh deck thò quá tường bờ đúng 1/8 — mẩu lan can thừa, nhìn thấy trong
    editor. Đo được: **100%** số cạnh sai, lệch đúng 0.1250. Sửa bằng `InsetSkin`: cắt tại giao điểm với chính
    các `WallSeg` đó (một ô bị chặn vẫn có "lớp da" đi được giữa mép ô và tường của nó). Sau sửa: 0/60000.
  - **Ranh giới tilemap ↔ collision.** Duyệt ô chỉ là **tra cứu** xem lấy wall của ô nào; mọi quyết định hình học
    là với `WallSeg`. Tilemap chỉ góp **đúng 1 bit mỗi ô: trống hay đặc** — thứ wall *không thể* suy ra, vì đất
    trống phát 0 face mà ruột đá cũng phát 0 face. Biên không định nghĩa được miền nếu không có điểm mồi.
  - Cạnh outline mang terrain id `WallSeg.VolumeRail = 255` → `TerrainSet.BitOf` = 0 → **không pass mask nào mở
    được**, thợ lội cũng bị chặn. Nó là vật cản vật lý, không phải một loại mặt đất.
  - **`Bridge` KHÔNG tham chiếu grid.** `IWalkVolume` nói bằng **world space**; `TerrainQuery` tự quy đổi sang
    không gian nó so sánh (và cache theo `VolumeVersion`, nên đường collision không trả giá). Cầu chỉ biết
    transform của chính nó — chạy được trên map nào cũng vậy, hoặc không map nào cả.
  - **Collision BẮT BUỘC đi qua `TerrainQuery`.** `CollisionWorld` đọc `_query.CellFaces` chứ không phải
    `_terrain.CellFaces`. Đọc thẳng tilemap thì tường bờ sông vẫn còn nguyên dưới mặt cầu và sẽ **xô người chơi
    văng khỏi cầu** — bug này không có ở bản per-ô nên rất dễ quên.
  - **Terrain KHÔNG bị sửa** (giữ nguyên từ bản trước, và giờ đúng theo nghĩa mạnh hơn): ô dưới cầu vẫn là Water
    trong `cells`, nên `BuildWaterMesh` vẫn vẽ sông dưới cầu và `uv1.x` (khoảng cách tới bờ) không xê dịch. Vẫn
    là lý do **loại** hướng "paint một layer Bridge vào TerrainSet".
  - **Reachability là index CHỒNG LÊN region của tilemap**, không phải tập region mới: union-find trên id region
    có sẵn, mỗi cầu union các bờ nó chạm. `TerrainQuery.Reachable` O(1), cầu rút lên là false ngay.
    - **Đây là chỗ duy nhất còn làm tròn về ô**, vì region của tilemap vốn là ô. Đứng và va chạm vẫn analytic.
  - **Hiệu năng**: `CellFaces` early-out bằng 1 phép AABB/cầu — ô nào không có cầu gần thì đi thẳng qua, không
    clip gì. Chỉ ô có cầu mới trả giá lấy mẫu + bisection.
  - **Còn hở, cần bạn quyết:** body đang đứng trên deck lúc deck mất (drawbridge, cầu sập). `ResolveTerrain` tự
    ứng biến: đẩy về phía walkable → dạt về bờ, và **giữa sông rộng thì tường hai bên đẩy ngược nhau nên có thể
    kẹt**. Cần luật gameplay (rơi xuống nước / dịch về bờ / trừ máu).
  - `SpawnZone.Bake` **cố ý chỉ đọc tilemap** — không spawn quái trên mặt cầu. Khác bản trước (bản đó thấy cả
    deck vì deck nằm trong `IsWalkable`), và không còn ràng buộc "bake zone sau khi đặt cầu".
  - ~~**Bug đang có:** `WanderRoam` chọn điểm chỉ theo `IsWalkable` nên mewfrog chọn được điểm bên kia sông rồi
    húc bờ mãi.~~ ✅ **Đã sửa:** `WanderRoam.Standable` giờ đòi cả `IsWalkable` **và** `Reachable`. Vì `Reachable`
    tính cả cầu nên bờ bên kia thành điểm đến hợp lệ ngay khi cầu mở, và mất đi ngay khi cầu rút.

- [ ] **Cầu phải XÂY — slot trả góp, 8 gỗ.** Cầu không có sẵn trên map: chỗ đó là một **điểm xây**. Người chơi bỏ
  gỗ vào **dần dần** (không cần đủ 8 một lúc); trả đủ **8 gỗ** thì visual của cầu hiện ra và `lowered = true`.
  - **Seam có sẵn hết, gần như không phải đụng gì bên dưới.** `Bridge.Lowered` là một bool public, set là bump
    version, có hiệu lực ở **truy vấn kế tiếp** — không rebuild, không bake, không rewire. Còn visual thì
    `lowered` **cố ý không đụng tới** (xem comment ở đầu `Bridge.cs`), nên "hiện art" là bật `GameObject` của art,
    một việc tách hẳn. Hai thứ độc lập là **đúng ý muốn**: xây xong bật cả hai, nhưng lúc debug bật riêng được.
  - **Một chiều nên an toàn hơn drawbridge.** Mục "còn hở" của cầu ở trên (body đang đứng trên deck lúc deck biến
    mất → giữa sông rộng có thể kẹt) **không áp dụng**: xây chỉ *thêm* chỗ đi được, không bao giờ lấy đi. Chỉ khi
    nào làm cầu **sập / tháo** thì mới phải chốt luật kia trước.
  - **Reachability tự đúng, không phải nối dây.** Chưa xây → `VolumeActive` false → không union region →
    `TerrainQuery.Reachable` trả false qua sông ngay từ đầu. AI và pathfinding tự biết chưa sang được, và đúng cái
    tick người chơi trả nốt đồng gỗ thứ 8 là chúng biết sang được. Không có bước "báo cho ai đó".
  - **⚠️ Phải PERSIST số gỗ đã trả.** `paid` là state runtime của một object nằm trong map prefab, mà map được
    `MapService` instantiate lại mỗi lần vào — để trong component thì đi map khác rồi quay lại là mất sạch,
    người chơi trả 7 gỗ xong về số 0. **Chốt: một `slotId` string author trong inspector + một `ISavable` giữ
    đúng `slotId → paid`.** Khuôn chép: `InventorySystem` (`SaveKey`, `Save/Load(SaveBag)`, một file).
    - **Id duy nhất TOÀN GAME, không phải duy nhất trong map** — nên bảng là **phẳng**, không có tầng `mapId`:
      ```
      world.json   "bridge_map1_north": 3   "bridge_map3_ford": 8
      ```
      Đây không chỉ là gọn hơn. Nó **xoá luôn ba cái bẫy thời điểm của `MapService`**, vì không chỗ nào còn phải
      hỏi `CurrentMapId`: (1) `CurrentMapId` được set **sau** `Instantiate` (`MapService.cs:60-61`) nên `Awake`
      đọc ra id map **cũ**; (2) map cũ bị destroy **sau** khi map mới đã vào (`:68-71`) nên `OnDisable` chạy lúc
      `CurrentMapId` đã là map mới → ghi nhầm bucket; (3) cả hai đều vô hình khi đọc code lưu. Slot chỉ biết id
      của chính nó là hết cửa.
    - **Lưu `paid` thôi; "đã xây" là SUY RA** (`paid >= required`). Lưu cả hai thì chúng mâu thuẫn được, và mâu
      thuẫn đó vô hình cho tới lúc không còn vô hình. Cái giá phải chịu: nâng giá một cầu **đã xây** từ 8 lên 12
      thì save cũ đọc ra "chưa xây" — nhưng đó **đúng là** content migration, giấu sau một cờ `built` chỉ là
      giấu việc vừa đổi giá dưới chân một save đang sống.
    - **Ghi ngay lúc trả, KHÔNG ghi lúc disable/destroy.** Alt-F4 sau khi bỏ 7 gỗ vào thì 7 gỗ đó phải còn.
      `InventorySystem` đã đúng vậy (`Changed` → `_save.Save`).
    - **Vắng mặt = 0 = chưa trả**, không bao giờ là "không biết". Cẩn thận `SaveBag.Child()` là *create-on-ask*
      — đường đọc mà dùng nó thì slot chưa ai đụng vẫn ghi entry rỗng. Cần thêm `TryChild` (đúng cặp với `Child`
      như `TryGet` đã đúng cặp với `Get`). Và **đừng dọn key mồ côi**: xoá một cầu rồi cắm lại thì tiến trình cũ
      nên quay về.
    - **Editor phải bắt `slotId` rỗng và trùng.** Id trùng = hai cầu chung một ví và một cái mở miễn phí — đúng
      hạng bug mà `RegionsJoined()` đang canh: nhìn thì hoàn toàn bình thường cho tới lúc đi tới.
    - **Prefab là mặc định lúc author, không phải state:** `Bridge.lowered` mặc định `true` trong class, nên cầu
      cần xây thì trong prefab phải để **`false`**. Save chỉ đè lên lúc spawn, không bao giờ ghi ngược vào
      prefab (`Bridge` có `[ExecuteAlways]` + `OnValidate` — nghịch trong play mode rất dễ dirty asset).
    - Set `Bridge.Lowered` ở `Awake` hay `Start` đều kịp: nó chỉ bump version, `TerrainQuery` đọc ở truy vấn kế,
      mà bước collision đầu tiên nằm ở `LateUpdate`.
    - **Làm cái này là giải luôn mục "Rương" ở dưới** — nó đang chờ đúng cơ chế "persist object trên map (chưa
      có)" này. Nên đừng đẻ ra `BridgePaymentState`; làm một kho state per-object dùng chung, mỗi object một
      `SaveBag` con (cầu ghi `paid`, rương ghi `opened`) để rương tới không phải đổi format file.
  - **Trả góp không cần code riêng.** `Inventory.Remove(def, amount)` trả về **số thật sự lấy được**, nên
    `paid += inv.Remove(wood, required - paid)` là toàn bộ logic: không phải kiểm tra "có đủ 8 không" trước, có
    bao nhiêu ăn bấy nhiêu, và không bao giờ trừ quá phần còn thiếu.
  - Giá và loại tài nguyên nằm trong **inspector của chính điểm xây** (`ResourceDef` + `int`), không hard-code —
    mỗi cây cầu một giá, và cầu ở map sau đắt hơn thì không phải sửa code.
  - **Chưa chốt, cần bạn quyết:**
    - **Tương tác kiểu gì?** Đứng gần + bấm phím, tự trút khi đi ngang, hay mở một popup UI?
    - **Trả rồi có rút lại được không?** Nếu không thì UI phải nói trước khi trừ, không phải sau.
    - **Hiển thị `3/8` ở đâu?** World-space trên đầu điểm xây, hay chỉ trong popup lúc tương tác.
    - **Art lúc chưa xong:** khung cầu dở dang theo % đã trả (tốn art, nhưng tiến độ tự nhìn thấy), hay một cái
      cọc/biển rồi cầu bụp hiện ra lúc đủ (rẻ)?

- [ ] **Mặt cầu ra khối: dựng cạnh thành QUAD THẬT thay vì bake vào texture. → THỬ MAI.** Bản đang chạy là
  `ViewDir8` (`Sprite3D/Runtime/Scripts`): quad nằm phẳng trên đất, 3 sprite `bridgeN`/`bridgeE`/`bridgeNE` + lật,
  bề dày vẽ sẵn trong ảnh. Chạy được, nhưng lúc đổi sprite giữa cú lượn camera thì **giật một cái**.
  - **Ý tưởng: hai cái dải tối đó CHÍNH LÀ mặt bên của khối.** Thay vì vẽ vào texture, dựng chúng thành quad
    đứng thật ở mép deck.
  - **Điểm ăn tiền không phải "mượt hơn", mà là KHÔNG CÒN CÚ CẮT NÀO ĐỂ MÀ LÀM MƯỢT.** Khi camera quay, quad
    cạnh tự co theo phối cảnh, và thời điểm cần tắt nó đi đúng là lúc nó nhìn nghiêng — **bề rộng chiếu bằng 0**.
    Tắt một mặt lúc nó đang vô hình thì không có gì để thấy. Đây là lý do nên thử, chứ không phải vì nó "3D hơn".
  - **Giải luôn cái sọc:** 4 tile hiện tại mỗi cái tự vẽ dải dày riêng nên thân cầu sẽ ra 4 vệt lặp. Cách này là
    **một** mặt trên cho cả deck + 4 cạnh quanh chu vi = 5 quad, một viền liền.
  - **Giá phải trả:** cần một texture dải gỗ cho mặt bên (crop từ chính dải dưới của `bridgeN`), và deck phải
    thành một mảnh thay vì 4 tile xếp chồng.
  - **Ba cái bẫy đã thấy trước:**
    - **Material sprite (`Sprites/Default`) KHÔNG cull mặt sau** — nó hai mặt. Nên các cạnh xa sẽ **không tự
      biến mất**, phải tắt bằng góc cam (chính là cái toggle miễn phí ở trên) hoặc đổi sang material có
      `Cull Back`. Đừng trông vào backface culling.
    - **Chiều cao khối phải canh lại bằng mắt.** Dải dưới của `bridgeN` cao 29px; quad đứng nhìn ở pitch 45° chỉ
      chiếu ra khoảng 0.7 chiều cao thật, nên h thật phải lớn hơn cái nhìn thấy. Cỡ **0.2 unit** là chỗ bắt đầu,
      không phải con số đúng.
    - **Thứ tự vẽ phải chủ động.** Cạnh gần đứng lên nhưng vẫn thuộc dải `WorldOrder` nằm-trên-đất (−99..−1) và
      phải vẽ **đè** mặt trên của deck. Cho hai thứ cùng order rồi trông chờ depth là hoà — mà hoà thì Unity tự
      quyết, và nó sẽ đổi ý giữa các góc cam.
  - **`ViewDir8` không bỏ đi** — nó vẫn đúng cho thứ thật sự phẳng: đường, decal, vệt bùn. Chỉ mặt cầu mới đổi.
  - **Nếu quad nhìn tệ, đường lui rẻ hơn:** tách mặt ván ra khỏi dải cạnh thành 2 renderer. Mặt ván nằm **đúng
    cùng một ô pixel ở cả 3 sprite** (đo rồi: `x[62..186] y[66..189]`), nên nó có thể đứng yên tuyệt đối và chỉ
    còn dải tối mỏng là đổi. Cùng một cú chuyển, nhưng thứ đang nhảy nhỏ hơn nhiều.

- [ ] **Dựng Plant1 + Plant2 thành quái, y khuôn PP1.** Art nằm sẵn ở `Assets/DraftArt/Predator plant/Plant1`
  và `Plant2`, cấu trúc thư mục **giống hệt** `Plant3` (đã thành PP1): `Attack/Death/Hurt/Idle/Run/Walk`.
  Làm được **hoàn toàn ngoài Unity** — đã kiểm, chốt bên dưới. Xong thì sửa AI để thành hai loài khác nhau.
  - **Chỗ tưởng là chặn nhưng không phải — CẮT SPRITE SHEET.** Meta trong DraftArt đang `spriteMode: 0`,
    `nameFileIdTable: {}` (chưa cắt), còn PP1 đã có 16 sprite con. Sinh tay được vì lưới suy ra bằng số học:
    - Cell **64×64**, cắt **trái→phải, hàng TRÊN trước** (frame 0 ở `y = height-64`).
    - Kích thước sheet Plant1/Plant2 **trùng khít** Plant3: `Idle 256×256` (4×4 = 16 frame),
      `Attack 448×256` (7×4 = 28). Cùng bộ art nên cùng cell.
    - `spriteID` (32 hex) và `internalID` (int32) **không cần Unity sinh** — chỉ cần **duy nhất trong file** và
      khớp giữa mục sprite ↔ `nameFileIdTable` ↔ `fileID` mà AnimSet trỏ tới. Tự phát thoải mái.
  - **Các bước** (mẫu để soi: `Plant3_*.png.meta`, `Predator plant AnimSet.asset`, `PP1.prefab`, `pp1.asset`,
    `pp1 Brain.asset`):
    1. Chuyển art sang `_Project/Art/Enemies/...` kèm `.meta` (giữ GUID, đừng để đứt ref).
    2. Sinh meta đã cắt cho các `_full.png`.
    3. `CharacterAnimSet` — `dirs: 4`, `mirror: 0`, `fps: 6`, frame theo action, `hitFrame` cho Attack.
    4. Prefab — nhân bản `PP1.prefab`, trỏ lại AnimSet + sprite.
    5. `EnemyConfig` + `EnemyBrainConfig`, **team riêng cho mỗi loài** (4, 5 — xem "CHIA LẠI PHE").
    6. Đăng ký vào `Config Registry.asset` + `Prefab Registry.asset`.
  - **⚠️ BA THỨ PHẢI CANH BẰNG MẮT, tôi không suy ra được — chốt trước thì mai chạy một mạch:**
    - **`hitFrame`.** PP1 để `4`. Đòn khác thì frame chạm khác; đây là thứ chỉ nhìn animation mới biết.
    - **Số frame THẬT mỗi action.** Sheet 4×4 = 16 ô nhưng vài ô cuối có thể trống — không đọc được pixel, nên
      sẽ lấy tạm đúng số frame PP1 dùng. Lệch thì animation hụt hoặc chớp ô trống, liếc là thấy.
    - **`hitRadius` + `size`/`forwardOffset` của `ShapeAttack`.** Lấy PP1 làm mặc định rồi canh lại theo art.
  - **Chưa chốt — hai con này là loài gì?** Quyết định `brain`: hung hơn PP1? Bỏ khung giờ 6–10h? Săn chủ động
    (`SightAggro`) thay vì `PredatorAggro`? Đây là phần **rẻ nhất**, chỉ đổi dropdown trong brain asset.
  - Không cần `FadeWhenBlocking` — đây là quái, không phải vật cản đường.
  - **Cùng công thức dùng lại được ngay:** `DraftArt/slime` có **Slime1/2/3** cấu trúc y hệt, và `DraftArt/Trees`,
    `Crystals`, `Objects_separately` còn nguyên.

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
    - Team: ~~0 trung lập / 1 player / 2 địch~~ — **quy ước này đã bị thay**, xem "CHIA LẠI PHE" ở đầu file.
      Luật không đổi phần friendly-fire: không đánh trúng cùng team. Spawn **qua DI container** (hoặc
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
  - **Chỗ sửa là đúng một hàm:** `TerrainGrid.Standable(x,y)` = `set.IsWalkable(cells[ô])`. Migration là thêm
    một số hạng vào chính dòng đó: `&& height == 0`. Không có mảng nào để dựng lại, không có bake nào để mở lại
    — `WalkBake` đã bị xoá. Cầu **không liên quan**: nó không đi qua hàm này, nó được ghép ở `TerrainQuery`.
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
  - **`Map_3` đang đúng trạng thái đó**: `baked: 0`, không có `Layer_*`/`Water_*` trong prefab, không có thư mục
    bake (nó là **cache dẫn xuất** — `PrepareBakeFolder` xoá sạch rồi tạo lại mỗi lần bake). Muốn bake thì mở
    prefab rồi `TerrainRenderer > Rebuild Terrain Mesh`. `Map_1` và `Map_2` vẫn `baked: 1`, chưa đụng.
  - Chỉ bake khi **profiling load-time thật sự đáng kể** (map to / swap map liên tục qua jump-point). Đo
    `Build()` trước; mesh phẳng vài ms/lần thường là bỏ qua được.
  - Nếu cần: **`IPreprocessBuildWithReport`** bake mọi map thành Mesh asset + serialize `Layer_*` vào prefab
    **chỉ trong bản ship**; `TerrainRenderer` skip `Build()` khi đã có baked children. **Editor workflow
    không đổi** → tránh ma sát re-bake mỗi lần vẽ (khác với nút bake tay ở tầng asset).
  - `walls` (collision) đã bake & lưu sẵn — chỉ mesh *hình ảnh* mới cần lo.
- [ ] **Runtime mutation — hiếm, tối ưu vừa đủ.** Game sau này có thể phá/hạ một vài tile hoặc lấp hố,
  nhưng thay đổi map xảy ra rất hiếm; chưa xây hệ terraforming liên tục.
  - **Phần collision của mục này đã KHÔNG CÒN VIỆC** (từ lúc làm cầu): boundary sinh từ ô khi được hỏi, nên phá
    một tile là sửa `cells` — collision đúng ngay tick sau, không rebake, không dirty chunk. Mọi thứ dưới đây giờ
    chỉ còn nói về **mesh / water mask / grass**.
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

- [x] **Sương mù / vùng tối viền map.** ✅ `Unlit/BorderFog` (shader) + `BorderFog.mat` + `MapBorderFog` (script)
  + một quad `BorderFog` trong `Main Camera.prefab`. Vì camera **luôn giữ player ở giữa**, người chơi không bao
  giờ "thấy" rìa map từ xa — họ **đi vào** nó, và không có cái này thì khoảng trống ngoài map cứ thế lòi vào khung.
  - **Hình học là quad màn hình, nhưng MASK tính trong WORLD-SPACE.** Quad cưỡi camera (`MaskFollowCamera`, y
    khuôn 2 veil ngày/đêm), còn mỗi pixel thì **bắn tia của chính nó xuống mặt phẳng đất** rồi tính sương tại
    toạ độ world đó. Vignette không phải bản rẻ hơn của cái này, nó là **hiệu ứng khác và sai** — vì cột mốc là
    rìa map (đứng yên trong thế giới, y như cái cổng player phải tìm), và vì camera **xoay được** (Q/E).
    Không đọc depth và **không cần**: sprite trong project đều `ZWrite Off` nên chẳng có depth mà đọc; giao
    tia/mặt phẳng là công thức đóng. Tia nào không chúc xuống đất = đang nhìn ra ngoài thế giới → sương tối đa.
  - **KHỐI GIẢ, nhưng đúng toán:** ba **tầng sương** xếp từ mặt đất lên, mỗi tầng lấy mẫu tại chỗ tia của pixel
    **cắt đúng tầng đó**: `p_h = p_0 + rd.xz * (h / rd.y)` — chính xác, không xấp xỉ, tốn ~2 lệnh. Nhờ vậy các
    tầng **thật sự ở độ cao khác nhau**: chúng trượt lên nhau khi camera đi/xoay (parallax là thứ bán được cảm
    giác sâu), và mỗi tầng cuộn noise riêng nên bờ sương **sôi** chứ không đứng chết. Composite từ tầng thấp
    lên: với một pixel thì tầng cao hơn được lấy mẫu **gần camera hơn**, tức nó ở phía trước → thấp→cao =
    xa→gần.
  - **Vì sao KHÔNG cần pass che đen phần ngoài map:** camera clear màu **đen**, mà ngoài rìa map thì **không vẽ
    gì cả** → khoảng trống đã đen sẵn. Việc của sương ở ngoài đó là **làm cái bóng tường sương trên nền đen**,
    không phải phủ kín. Nên `_FogTopColor` **sáng hơn** `_FogColor`: tầng trên vừa nhạt vừa tơi (`_TopWisp`),
    và đó chính là thứ khiến nó đọc ra "một tường sương đứng đó" thay vì "thế giới bị cắt".
  - **Nằm DƯỚI veil ngày/đêm — `Queue = Overlay-10`**: sau mọi sprite, **trước** `DarknessMask` (multiply) và
    `Unlit/Fog` (additive glare). Sương là **một phần của THẾ GIỚI, không phải của cái ảnh**, nên tint ambient
    phải rơi lên nó y như rơi lên mọi thứ khác. Nằm trên veil thì nó là thứ duy nhất trên màn hình **không đổi
    theo giờ**: đêm cả cảnh ngả về ambient mà riêng viền map thì không → đọc ra như **một lỗ khoét trong ảnh**,
    không phải sương đứng trong thế giới.
    - **Giá phải trả, đã chấp nhận:** glare trên `Unlit/Fog` (additive, toàn màn hình, **hiện đang đen = tắt**,
      xem mục SunnyWeather) khi bật lại sẽ rửa nhạt dải sương lúc trưa. Đó là tint trên một màu vốn đã tối, không
      đổi hình dạng, nên viền vẫn còn đọc được là viền.
    - Nói theo đúng chữ thì **sương KHÔNG còn "không ảnh hưởng ngày đêm"** nữa — không thể vừa nằm dưới veil vừa
      miễn nhiễm với nó. Cái được giữ là: sương **không tự đọc** `EnvironmentState`/`DayNightConfig`, không có
      nhánh code nào theo giờ; nó chỉ chịu đúng cái tint mà cả cảnh đang chịu.
    - Ngưỡng cần nhớ: thứ vẽ sau sương là queue > 3990. Muốn thêm veil nào **trên** sương thì đặt ≥ 4000.
    - **Đã thử để sương vào chung queue `Transparent` với sprite rồi bỏ** — không phải vì trông sai, mà vì nó
      **không đổi gì cả**: project chỉ có một sorting layer, mọi sprite `sortingOrder 0`, transparency sort để
      Default (cự ly tới camera) → quad cưỡi camera ở 4.9 đơn vị luôn thắng cự ly trước cả thế giới ở ~50, vẫn
      vẽ cuối queue. Cùng một khung hình, chỉ khác là thứ tự **giành bằng hình học** thay vì bằng con số. Và nó
      **không mua được** thứ đáng lẽ biện minh cho nó: sprite đứng **trước** tường sương vẫn không vẽ đè lên
      được, vì quad trên camera không có vị trí trong thế giới để đem ra xếp hạng. Muốn thế thì sương phải thành
      geometry thật ở rìa map — và lúc đó lại vấp chuyện sprite `ZWrite Off`, không có depth để xếp.
  - **⚠️ Hệ quả đã biết của một veil không có depth:** pixel phần trên của sprite cao lấy mẫu mặt đất **phía sau
    nó**, nên nhân vật đi vào vùng sương bị nuốt **từ đầu xuống**, không phải từ chân lên. Không có cách sửa khi
    không có depth buffer để biết bề mặt thật nằm đâu; `_WallHeight` là núm quyết định sương với lên màn hình
    bao xa, tức quyết định cái này lộ bao nhiêu.
  - **Rect map = origin + 2 trục đơn vị + size**, không phải min/max → **map quay quanh Y vẫn đúng**, giá đúng
    2 phép dot, bằng đúng một phép so AABB. `MapBorderFog` đo bằng `TransformVector` nên map bị scale cũng đúng.
  - **Bề rộng dải tính bằng Ô (`_BandCells`), `_CellSize` thì ĐO từ grid** — không có field cellSize thứ hai để
    lệch. Giữ đúng đơn vị mà map được author và gate được đặt.
  - **Chia việc: material giữ toàn bộ phần NHÌN, script chỉ đẩy thứ nó đo được** (rect + trục + groundY +
    cellSize, qua `MaterialPropertyBlock`). Nhờ vậy **tinh chỉnh trong play mode là giữ được số** — đó là cả lý
    do của chiều chia này, chứ không phải mirror số của material thành field trong script.
  - **Renderer được author TẮT**, chỉ script bật: chưa có map thì shader không có gì để đo và sẽ tối đen cả màn
    — kể cả trong edit mode, nơi không ai đẩy rect vào.
  - **Seam đã dùng:** `MapService.WireMapToScene` — đúng chỗ đang trỏ `CollisionSystem` vào terrain của map, chỉ
    thêm một dòng. Mọi map (kể cả map đầu, `GameController.Start`) đều đi qua đó nên không có đường nào lọt.
  - **Còn phải canh bằng mắt** (số trên material, đổi dropdown không đủ): `_BandCells 2` = **4 đơn vị** với
    `cellSize 2`, mà tầm nhìn thật chỉ khoảng **9 đơn vị** (`fov 6°`, `distance 50`, `pitch 35°` →
    `2·tan3°·50 / sin35° ≈ 9.1`) — tức dải rộng gần **nửa màn hình**. `_WallHeight 1.5` thì tường sương lấn vào
    trong map `1.5·cos35°/sin35° ≈ 2.1` đơn vị. Cả `_Opacity`, `_WobbleCells`, `_NoiseScale` đều là
    thứ chỉ nhìn mới chốt được.
  - **Chỗ test trong `Map_3`:** đất vẽ tới sát grid border ở **+X** và **−Z**; phía **−X còn 8 đơn vị nước**
    (cell 0–3 là Water id 0) nên đứng ở bờ tây chỉ thấy nửa ngoài của dải. Gate_0 ở `(18.7, 29.11)` → đi
    xuống **−Z** khoảng 29 đơn vị là tới biên trên đất liền. Muốn thấy ngay thì kéo tạm `_BandCells` lên ~20.
  - Chưa dùng chung `CloudShade.hlsl` (mục Mây bay) vì file đó **chưa tồn tại** — noise nằm inline trong shader,
    cùng `Hash/Noise` với `Grass`/`Water`. Khi làm mây thật thì rút cả ba về một include, đừng dựng trước.

- [ ] **Vùng tối viền map thành LỐI SANG MAP KHÁC.** Phần thị giác đã xong ở mục trên; còn lại là luồng đi.
  Player đi vào cổng nằm sát vùng tối thì nhảy map; sang map mới thì hiện ra **như vừa bước từ trong vùng tối
  đi ra**.
  - **Seam đã có, ráp vào chứ đừng dựng lại:** `Portal` (kích hoạt warp) · `Gate` (điểm đặt chân khi tới) ·
    `Map.GetGate(index)` · `MapService.WarpAsync` (đã chặn input suốt lúc swap qua `IInputGate`, và
    `PlaceAtGate` đã đặt player + `SnapToTarget` camera).
    - Muốn cảnh "bước ra từ vùng tối" thì **gate đến phải nằm TRONG vùng tối**, rồi player tự đi ra. Cân nhắc
      giữ input thêm một nhịp sau khi swap để cú bước ra đó đọc được, thay vì thả ngay.
    - Gate hiện tại đều nằm giữa map (`Map_1` Gate_0 `(18.7, 29.11)`, Gate_1 `(23.6, 21.45)`; `Map_3` cũng
      vậy) → phải **dời gate
      vào trong dải sương** mới có cảnh đó, và dời thì kiểm luôn cell đó có walkable không (bờ tây là nước).

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
- [x] **Vật cản mờ đi khi che player.** ✅ `FadeWhenBlocking` (gắn lên vật muốn mờ) + `BlockerFadeManager`
  (static, tự tạo, một `LateUpdate` cho tất cả — khuôn `BillboardManager`). Subject do `CameraFollowsPlayer`
  đẩy sang: thứ đáng lộ ra chính là thứ camera đang bám, policy đó vốn đã nằm ở đó.
  - **Thuật toán: test rect trong screen-space, 3 tầng rẻ→đắt.** (1) cổng độ sâu — chỉ thứ ở TRƯỚC player mới
    che được, 1 dot; (2) khoảng vuông góc tới tia camera→player so với bán kính bao — trong screen-space player
    **là một điểm**, nên vật chỉ che được nếu tia xuyên qua nó; (3) rect chính xác từ **4 góc của quad**.
    Tầng 3 cần thiết vì bao hình **cầu** rất sai với sprite cây (cao, hẹp) — dừng ở tầng 2 thì player đứng
    *cạnh* gốc cũng làm cây mờ.
  - **⚠️ Không dùng `renderer.bounds` cho rect.** Nó là AABB thế giới, mà billboard **ngả theo camera nhìn
    xuống**, nên AABB phải bao cả góc trên-xa và dưới-gần của quad — hai điểm **không nằm trên sprite**. Chiều
    ngang không sao (trục rộng của quad vẫn nằm ngang), nhưng **chiều dọc phồng lên**: mép trên của rect nhận
    vơ một dải trời phía trên tán, nơi chẳng vẽ gì. Ngả càng nhiều càng sai, và sai đúng chỗ cây cần đúng nhất
    là cái ngọn. Chiếu thẳng 4 góc quad (`sprite.bounds` + `transform.right/up`) vừa đúng vừa rẻ hơn một nửa.
    `bounds` chỉ còn dùng ở tầng 2, nơi ước lượng thừa là vô hại.
  - **Hai trục hỏi HAI câu khác nhau** — vì hình chiếu của player lên màn hình không đối xứng quanh thứ ta gọi
    tên được rẻ tiền:
    - **Ngang = một điểm** (đường tâm player). Player trên màn hình là một cột hẹp, nên tâm là đại diện công
      bằng; coi họ là một hộp rộng thì cây mờ chỉ vì quẹt trúng vai.
    - **Dọc = cả chiều cao người.** Player là một dải cao **dựng lên từ** điểm mà camera ghim ở tâm màn hình,
      nên hỏi ở *một* độ cao thì chỉ bắt được cây che đúng độ cao đó — che đầu hay che chân đều báo "không che".
  - **⚠️ Bán kính ở tầng 2 phải BẤT BIẾN THEO XOAY.** Dùng `bounds.extents.magnitude` (AABB của quad nghiêng)
    thì bán kính **phình/teo theo yaw camera** — mỏng khi nhìn dọc trục, béo ở góc chéo. Hệ quả: hiệu ứng mạnh
    yếu khác nhau tuỳ hướng nhìn, mà lại rất khó đoán ra. Dùng nửa đường chéo của chính quad
    (`sprite.bounds.extents` × `lossyScale`) — nó không xoay được.
  - **Chỉ update quanh player, qua `Core.SpatialHash`.** Thứ che được player **bắt buộc** nằm giữa camera và
    player, nên bán kính query **chính là khoảng cách camera** (+ margin) — kích thước map không tham gia. Hash
    rebuild theo **dirty flag** lúc register/unregister chứ không mỗi frame, vì blocker đứng yên (vật **di
    chuyển** thì phải gọi `Rebuild` lại — hiện chưa có cái nào).
    - Bẫy đi kèm: vật **rời** vùng query lúc đang mờ dở sẽ không được tick nữa → kẹt mờ vĩnh viễn. Manager giữ
      danh sách `_wasNear`, cái nào rớt ra mà chưa `Settled` thì vẫn kéo theo cho tới khi về đục hẳn.
  - **⚠️ CẢ HAI phía đều phải đo từ TÂM SPRITE, không phải `transform.position`.** Đây là cái bẫy dai nhất, và
    nó cắn ở **hai đầu** với hai triệu chứng khác nhau:
    - **Phía vật cản:** transform nằm dưới đất ở gốc cây, còn art thì cao hẳn lên và ngả về phía camera. Đo từ
      gốc thì cây đang **lấy tán nuốt player** vẫn bị chấm là "xa tia" → loại ngay ở tầng 2, chưa kịp tới bước
      test hình. Cây càng cao càng chắc trượt — đúng lý do **cái ngọn** là phần lì nhất không chịu mờ.
      Dùng tâm sprite còn được thêm một cái: nó **chính là** thứ Unity dùng để sắp thứ tự vẽ giữa các renderer
      trong suốt, nên tầng 1 khớp đúng với draw order mà nó đang cố đoán, thay vì xấp xỉ qua vị trí mặt đất.
    - **Phía player:** `CameraRig.Pivot` lấy vị trí **mặt đất** → chân player luôn ở tâm màn hình, cả thân vẽ
      **phía trên**. Test ở chân thì mọi cây che thân/đầu đều báo "không che". Lấy `bounds.center` của art
      (không phải offset chiều cao gõ tay) nên tự đúng với mọi nhân vật, đổi art khỏi chỉnh lại. `CameraRig.Pivot` lấy vị trí
    **mặt đất** của player → chân luôn nằm ngay tâm màn hình, còn cả thân được vẽ **phía trên** điểm đó. Test ở
    chân thì mọi cây che thân/đầu mà chưa chạm tới chân đều báo "không che" — sai lệch **dồn hết vào chiều dọc**,
    đúng phần ngọn, tức đúng phần thật sự che người. Lấy `bounds.center` của art player (không phải một offset
    chiều cao gõ tay) nên tự đúng với mọi nhân vật to nhỏ khác nhau, đổi art không phải chỉnh lại.
  - **Không chỗ nào đọc/giả định pitch.** Tất cả lấy từ camera sống: `rayDir` từ vị trí camera, `WorldToScreenPoint`
    dùng ma trận thật, trục quad lấy từ billboard (do `BillboardManager` xoay theo `CameraViewDir.CamForward`).
    Đổi góc rig hay xoay lúc chạy đều không phải sửa gì ở đây.
  - **Vì sao screen-space đúng ở đây chứ không phải xấp xỉ:** sprite là billboard, luôn quay mặt vào camera →
    dấu chân trên màn hình của nó **chính là** cái quad.
  - **Đã loại:** raycast (project không dùng Unity physics, cây **không có Collider** — `CollisionSystem` là hệ
    tự viết); depth/stencil trong shader (sprite `ZWrite Off`, không có depth để test).
  - **Fade có GRADIENT, không phẳng** — shader riêng `Sprite/Flash Fade` (`SpriteFlashFade.shader/.mat`,
    `Sprite/Flash` cũ **giữ nguyên**, chưa đụng). Dưới `fadeFrom` (% chiều cao) alpha = 1 → **gốc cây đặc
    nguyên**, từ đó tới `fadeTo` mờ dần về `fadedAlpha`, trên nữa thì giữ. Nhờ vậy cây tránh đường mà thế giới
    vẫn đọc ra "có một cái cây ở đó", không thành bóng ma.
    - **⚠️ Ramp chạy theo OBJECT-SPACE Y, KHÔNG phải `texcoord.y`.** Sprite cắt từ sheet mang **sub-rect của
      atlas** làm UV nên `texcoord.y` không hề chạy 0→1 dọc sprite; gradient dựng trên nó sẽ nằm sai độ cao, và
      sai khác nhau ở từng sprite. Mesh thì Unity dựng từ chính `sprite.bounds`, nên object space có sẵn trục
      đó — CPU quy đổi % của người dùng sang toạ độ ấy (`PushFadeRange`). Billboard xoay **transform** chứ không
      xoay mesh, nên trục này luôn là "hướng lên" của sprite bất kể camera.
  - **⚠️ `HitFlash` KHÔNG được `SetPropertyBlock(null)` nữa.** Fade dùng chung MPB với nó; null cả block thì
    **mỗi lần cây ăn đòn xong là độ mờ bị xoá**, cây bật lại đặc trong lúc player vẫn đứng sau. Kết thúc flash =
    ghi `_FlashAmount = 0`, không phải xoá phần của người khác. (Comment cũ nói null để sprite batch lại — đúng
    hồi nó là thứ duy nhất ghi block.)
  - **`reach` tách theo cạnh, cho phép ÂM.** Rect là **AABB** mà tán cây thì **thon lại phía trên** → mép trên
    chủ yếu là trời trống, cần bóp vào (`reachTop` âm); hai bên sát pixel thật nên cần nới (`reach` dương). Một
    số chung ép phải thoả hiệp và sai ở cả hai đầu. **Không có knob đáy**: đáy box vốn đã ôm sát thân, và cổng
    độ sâu (phải nằm giữa camera và player) đã giữ mép dưới trung thực rồi. `stickyEdge` thì cộng đều mọi cạnh —
    nó là chống nhấp nháy, không phải để tạo hình.
  - Bóng **không** mờ: `SpriteShadow` tạo renderer bóng lúc runtime treo vào **root**, cố ý nằm ngoài
    `Billboard` — nên luật auto-fill "chỉ lấy renderer dưới `Billboard`" loại nó mà không phụ thuộc thứ tự Awake.
- [ ] **Hit flash mạnh hơn (tùy chọn).** `HitFlash` đang dùng `SpriteRenderer.color` (nhân → chỉ tối
  lại thành đỏ, không sáng rực). Muốn "pop" đỏ/trắng chói thì thêm `_Flash` vào shader sprite (lerp về
  màu flash). Giờ để tạm màu-nhân.
- [ ] **Flinch / khựng.** Là *thuộc tính của đòn nặng* (set busy có chủ đích), KHÔNG phải mặc định khi
  trúng. Đòn thường không khoá hành động.
- [ ] **FlyingPickup nảy/lăn (tùy chọn).** Giờ là velocity + friction cơ bản. Muốn nảy khi chạm / lăn
  thì thêm sau.

## 🐛 Debug / tooling

- [ ] **BUG: đổi map thì quái map cũ không chết, số quái nhân đôi.** Đi sang map khác → quái của map cũ vẫn
  đứng đó, mà zone của map mới **vẫn chạy `warm`** nên đẻ thêm một lứa đầy → gấp đôi. Quay đi quay lại vài lần
  là ngập.
  - **Nặng hơn ở `Map_3`:** map đó có **4 zone** (mewfrog ×2 cap 4, pp1 ×2 cap 3) = **14 con mỗi
    lượt load, không phải 10**. Đi qua đi lại 3 lần là 42 con quái mồ côi vẫn nằm trong `CombatWorld`.
  - **Nguyên nhân — quái không thuộc về map.** `EnemySpawner.cs:44` gọi
    `Object.Instantiate(ident.gameObject, position, rotation)` — **không truyền parent**, nên con quái nằm ở
    **gốc scene** chứ không phải dưới map. `MapService.WarpAsync` huỷ `old` (GameObject của map) thì zone chết
    theo vì nó là con, còn **quái thì không** — chẳng có gì trỏ tới chúng để dọn.
  - Không chỉ là chuyện số lượng: quái mồ côi **vẫn nằm trong `CombatWorld`**, tức player có thể ăn đòn từ
    một con thuộc cái map đã rời khỏi.
  - **Hướng sửa gọn nhất:** cho quái làm **con của map** (`Instantiate(prefab, pos, rot, parent)` — bản overload
    giữ world position). Lúc đó `MapService` `SetActive(false)` rồi `Destroy` sẽ dọn sạch, và đi đúng đường đã có:
    `Damageable.OnDisable` tự rời `CombatWorld`, `CollisionBody.OnDisable` tự rời `CollisionSystem`. Không phải
    viết thêm sổ sách theo dõi.
    - Parent là **map** hay là **zone**? Zone thì gọn hơn về ngữ nghĩa (zone sở hữu lứa của mình, sau này làm
      respawn debt / wipe-lock trong `Docs/SPAWN.md` là cần đúng cái đó). Map thì đủ để bịt bug này.
  - Kiểm luôn khi sửa: `EnemySpawner._scopes` cache **một scope cho mỗi `EnemyConfig`**, không phải mỗi map —
    cái đó đúng và không cần đụng, nhưng đừng vô tình dọn nhầm nó theo map.

- [ ] **Số máu trên đầu (editor/test).** Thanh máu ẩn hẳn; chỉ hiện *số* HP trên đầu ở chế độ
  editor/test (gate bằng `#if UNITY_EDITOR` hoặc cờ debug). Chưa làm.

## 🧹 Tech debt / cleanup

- [ ] **`PlayerSystem.SwitchTo` áp cây upgrade của nhân vật CŨ lên thân MỚI.** `SwitchTo` gọi `Spawn(id)` rồi
  mới gán `_currentId`, mà bên trong `Spawn` đã gọi `ApplyUpgrades()` — hàm này đọc `_currentId`. Nên đổi nhân
  vật một lần là thân mới mang buff của cây cũ; tới lần dựng lại sau (mua node, respec, respawn) mới đúng.
  Sửa: gán `_currentId = id` **bên trong** `Spawn`, ngay sau khi mọi kiểm tra đã qua, `SwitchTo` chỉ còn việc
  lưu. Phát hiện lúc đọc code, **chưa test** — với một nhân vật thì đường này chưa chạy bao giờ.

- [ ] **`ViewDir8` / `ViewDir2` cần manager + culling khi map to.** Chưa làm, **cố ý**: hiện mỗi map có đúng một
  cây cầu. Nhưng đây là loại component sẽ nhân lên rất nhanh — mặt cầu, cầu tàu, đường, bục, decal, mọi thứ nằm
  phẳng trên đất mà muốn ra khối đều dùng nó — nên ghi lại trước khi quên hình dạng của việc.
  - **Tiền không nằm ở lúc đứng yên.** Cả hai đã early-out sẵn (`CameraViewDir.TransformChanged` + so rotation),
    nên frame camera đứng im gần như miễn phí. **Toàn bộ chi phí dồn vào đúng frame người chơi bấm Q/E** — mà
    đó lại là frame tệ nhất để tốn, vì cú lượn 45° đang làm cả màn hình động. Đo thì phải đo **frame xoay**, đo
    frame nhàn sẽ ra kết luận sai là "không có gì".
  - **Gộp vào một manager, y như `BillboardManager`.** Nó tồn tại trong chính module này vì đúng lý do đó ("One
    LateUpdate for ALL billboards instead of N MonoBehaviour.LateUpdate calls") — `ViewDir*` chỉ là chưa được
    hưởng. Cái đắt là N lần callback của Unity, không phải thân hàm.
  - **Hai native call mỗi instance, cả hai đều hoisted được:**
    - `CameraViewDir.Transform.eulerAngles.y` — manager lấy **một lần**, phát cho tất cả.
    - `transform.up` (để ra `slabYaw`) — chỉ đổi khi **chính object xoay**, mà mặt cầu/con đường thì gần như
      không bao giờ xoay. Cache lúc enable + tại đúng chỗ so rotation đã có sẵn. Sau đó việc mỗi instance làm
      mỗi frame chỉ còn vài phép float và may ra một lần ghi sprite.
  - **Cull theo TRÊN MÀN HÌNH, không theo khoảng cách.** `SpriteRenderer.isVisible` là miễn phí (renderer đã tự
    tính culling rồi) và đúng là tín hiệu cần: sprite của mặt cầu ngoài khung hình thì sai cũng không ai thấy.
    Sửa lười lại bằng `OnBecameVisible`, nhờ vậy bất biến vẫn giữ nguyên — **đúng khi đang nhìn thấy**.
    - ⚠️ `isVisible` tính cả camera của Scene view, nên nó **nói dối trong editor**. Đừng debug bằng nó.
  - **Nếu vẫn gợn thì rải ra nhiều frame** (mỗi frame một lát). An toàn **riêng ở đây** vì cú lượn camera mất
    khoảng chục frame (`snapSpeed 8`), nên một mặt cầu trễ một hai frame giữa lúc đang xoay là không nhìn ra.
    Đừng bê mẹo này sang thứ phải đúng ngay trong một frame.
  - **Ngưỡng để bắt tay vào:** khi một map có đủ sprite mặt đất để frame Q/E hiện lên trong profiler. Không phải
    bây giờ.

- [x] **Chia lại Order in Layer của thế giới.** ✅ `Core.WorldOrder` — **một bảng, một chỗ**, vì project chỉ có
  **MỘT** sorting layer (`Default`) nên order-in-layer là toàn bộ ngân sách thứ tự.

  | order | gì |
  |---|---|
  | **−300** | phản chiếu nước (`WaterReflection`) |
  | **−200** | mặt nước (layer kind Water) |
  | **−100 trở xuống** | tile terrain — layer trên cùng ở −100, mỗi layer dưới nó thấp hơn 1 |
  | **−99 … −1** | **NẰM TRÊN mặt đất**: mặt cầu, tàu thuyền, đường, decal, bóng đổ |
  | **0** | đứng trong thế giới: nhân vật, cây, prop — mọi thứ billboard |

  - **Cái dải trống giữa tile và 0 mới là điểm của việc này.** Trước đó cả thế giới bị nhồi vào **−5..0**
    (`-5` water, `-4/-3/-2` tile, `-2/-1` bóng, `0` sprite, `-10` reflection) — **không còn chỗ chèn**: thứ gì
    nằm phẳng trên đất mà không phải terrain, đi *qua* chứ không đi *quanh*, đều không có ô nào để nhét vào.
    100 bậc mỗi band không phải hào phóng, nó là thứ chặn lần sau phải đánh số lại cả dãy.
  - `TerrainRenderer`: bỏ `sortingOrder`, thay bằng `tileOrder` (−100) + `waterOrder` (−200). Tile **treo xuống**
    từ `tileOrder` nên layer trên cùng là cao nhất và dải trên nó luôn rảnh. Với `Terrain Set 1`:
    Brick **−100** · Grass **−101** · Mud **−102** · Water **−200**.
  - **`WaterReflection.order` thành TUYỆT ĐỐI (−300), không còn là offset theo sprite chủ.** Chỗ của phản chiếu
    trong stack là **sự thật về thế giới** (nó ở dưới mặt nước), không phải quan hệ với vật được phản chiếu —
    offset sẽ đi theo một sprite đổi band rồi nổi lên trên mặt nước mà nó lẽ ra phải nằm trong.
  - **`SpriteShadow.orderOffset` GIỮ tương đối (−1)** — trái với reflection, và có lý: bóng thuộc về vật đổ ra nó
    nên phải đi theo. −1 rơi vào dải trên-mặt-đất, tức bóng đổ **lên** mặt cầu và **dưới** mọi thứ đang đứng.
  - **Map đã bake không cần bake lại để đổi order.** `TerrainRenderer.OnEnable` giờ gọi `ApplyOrders()` cả khi
    `baked` — order là **chính sách, không phải hình học**. Kèm theo sửa một bug: `OnValidate` cũ lặp
    `_layerObjects`, mà domain reload xoá list nhưng giữ object, nên đổi order trong inspector **âm thầm không có
    tác dụng**; giờ match theo tên child (`Layer_{i}_` / `Water_{i}_`) nên luôn tới được.
  - **Render queue KHÔNG phải núm ở đây.** Order-in-layer thắng nó — đó là lý do các layer terrain (queue
    3000..3003) vẫn vẽ **dưới** sprite ở queue 3000. Queue chỉ phá thế hoà: 3 veil camera đều order 0 và chỉ tách
    nhau bằng queue (`BorderFog` 3990 · `DarknessMask`/`Fog` 4000).
  - **Hai thứ nằm ngoài thang này, đừng cố xếp vào:** **cỏ** (`Grass/Billboard` queue `AlphaTest` 2450 → pass
    **opaque**, **ghi depth**, vẽ bằng `RenderMeshInstanced` nên **không có sortingOrder**) và **layer 7 `Light`**
    (`spotlight`/`light` → `LightCamera` → RT `LightMap`, một vũ trụ thứ tự riêng).
  - `World.asmdef` và `Sprite3D.asmdef` giờ ref `Core` để cùng đọc được bảng. Không tạo vòng: `Core` chỉ ref
    UniTask + Newtonsoft.

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
- [x] **Mass của quái bị nuốt mất.** ✅ Knockback vốn **đã** chia theo mass đúng như mong muốn
  (`CollisionBody.AddImpulse` → `impulse * InvMass`, `InvMass = 1/mass`: mass 1 = 100%, 2 = 50%, 0.5 = 200%,
  0 = bất động) — nhưng với quái nó **không có tác dụng**: `DynamicUnit.Start` gọi `body.SetMass(Mass)` ghi đè
  giá trị author trên prefab, mà `EnemyController.Mass` là **hằng `1f` cứng**. PP1 author mass 10 chạy thành 1,
  Mewfrog author 0.5 cũng thành 1.
  - Sửa: thêm `mass` vào `EnemyConfig`, `EnemyController.Mass => config.mass`. Chép đúng số đã author trên
    prefab sang config (pp1 `10`, mewfrog `0.5`) nên **ý đồ giữ nguyên**, chỉ là giờ nó thật sự có hiệu lực.
  - Ảnh hưởng **cả hai** thứ vì `CollisionBody.mass` dùng chung: knockback **và** độ bị đẩy dạt khi hai thân
    chồng nhau. PP1 trước đây vừa bị bắn văng vừa bị xô dạt như con ếch.
  - Cây/đá/`Log` không dính vì chúng là `Unit` chứ không phải `DynamicUnit` → `Start` không ghi đè, mass trên
    prefab (`0` = bất động, `0.2`) vẫn đúng. Đó cũng là lý do bug này sống lâu mà không ai thấy.
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

Config-hoá stats MC · ~~quy ước team (0 trung lập / 1 player / 2 địch)~~ **đã thay, xem "CHIA LẠI PHE" đầu
file** · `Damageable` (máu + drop) ·
hệ ngày/đêm (`DayNightClock/Config/Lighting`) + `Docs/LIGHTING.md` · hit-flash đỏ · **gỗ văng**
(velocity, height-trên-art, collision khi bay) + pickup (`Picker`/`Pickable`/`FlyingPickup`).
