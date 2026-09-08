# Cầu bay quanh nhân vật — thiết kế (chưa triển khai)

Một loại thẻ nâng cấp trong arena: nhận thẻ **cầu lửa** thì hai quả cầu lửa xuất hiện, bay vòng
quanh nhân vật ở hai vị trí đối xứng, chạm địch là gây sát thương. Nhận thêm thẻ cùng loại thì thêm
cầu, và **cả vòng chia lại đều**: 2 → 4 → 6 → 8.

---

## Cái gì thuộc về cái gì

| | Nắm gì |
|---|---|
| **`OrbitKind`** (asset) | một LOẠI cầu: prefab, bán kính vòng, tốc độ quay, % sát thương, bán kính chạm, nhịp chạm lại |
| **`OrbitRing`** (trên thân MC) | mọi cầu đang có, **nhóm theo loại**; sinh/xoá cầu và rải góc đều |
| **`OrbitSphere`** (prefab) | một quả cầu: hình ảnh, và phát hiện chạm |
| **`GrantOrbitEffect`** (`IUpgradeEffect`) | thẻ cộng thêm N cầu loại X |

**Mỗi loại một vòng riêng**, bán kính và tốc độ riêng. Không gộp chung: bốn loại cùng một quỹ đạo
là một đám mờ không đọc được, còn bốn vòng khác bán kính khác chiều quay thì nhìn phát biết đang có
gì. Đó cũng là lý do bán kính/tốc độ nằm trên `OrbitKind` chứ không nằm trên thẻ.

---

## Chia đều — luật cốt lõi

Trong một loại có `n` cầu thì cầu thứ `i` đứng ở góc:

```
angle(i) = phase + i × 360/n
```

`phase` là góc chung của cả vòng, tăng theo `degreesPerSecond`. **Vị trí là hàm của chỉ số**, không
phải trạng thái từng quả tự giữ.

Nhờ vậy **thêm cầu là chia lại tức thì**: 2 quả ở 0°/180°, thêm 2 nữa thành 0°/90°/180°/270° — không
cần ai đi sắp lại, không có quả nào "trôi" về chỗ mới. Nếu mỗi quả tự nhớ góc của nó thì thêm quả
thứ ba là một bài toán phân bố lại, và nó sẽ trông như cả vòng giật một cái.

⚠️ Điểm này quyết định luôn cấu trúc: `OrbitRing` **không** hỏi từng quả cầu nó ở đâu, mà **đặt** vị
trí cho chúng mỗi frame. Quả cầu là thứ được đặt, không phải thứ tự đi.

---

## Nhịp chạm lại — tại sao lưu trên từng quả

Mỗi quả cầu giữ **sổ riêng**: `mục tiêu → thời điểm được đánh lần cuối`. Chạm một con đã có trong sổ
mà chưa quá `hitInterval` thì bỏ qua.

**Sổ nằm trên QUẢ CẦU, không nằm trên vòng.** Bốn quả bay qua cùng một con quái là **bốn lần chạm**
— đó chính là phần thưởng của việc có nhiều cầu. Nếu sổ chung cho cả vòng thì quả thứ hai trở đi
không làm gì cả, và thẻ thứ hai thành vô nghĩa.

Sổ **dọn theo thời gian**, không giữ mãi: một run giết vài nghìn con, giữ hết thì rò bộ nhớ và tra
sổ ngày càng chậm. Quét bỏ mục quá hạn mỗi lần chạm là đủ — sổ không bao giờ dài quá số địch đang
đứng cạnh.

---

## Sát thương là % của attack power, không phải số phẳng

`damageShare = 0.5` nghĩa là nửa một nhát chém. Cầu **lớn lên theo build**: thẻ tăng attack cũng làm
cầu mạnh hơn, nên nó không thành vô dụng sau vài thẻ.

Số phẳng thì đúng ở đúng một thời điểm của run rồi sai mãi về sau — hoặc quá mạnh lúc đầu, hoặc thành
trang trí lúc cuối.

### Crit là một thẻ riêng, không phải mặc định

Cầu **không crit** cho tới khi người chơi nhận thẻ *"cầu có thể crit"*. Nhận rồi thì mỗi lần chạm roll
theo đúng `CritPoints`/`CritDamage` của nhân vật.

*Vì sao đáng làm vậy:* nó biến một thẻ thành **cái khoá**, và giá trị của cái khoá đó **phụ thuộc vào
phần còn lại của build**. Lấy sớm khi chưa có điểm crit nào thì gần như vô dụng; lấy sau khi đã gom
ba thẻ crit thì là cú nhân đôi sát thương cả vòng cầu. Một thẻ mà giá trị do người chơi tự tạo ra là
thứ đáng đọc lại ở mọi lượt rút — khác hẳn "+ attack" luôn đúng ngần ấy.

Nó cũng làm crit và cầu **giao nhau** thay vì chạy song song: hai nhánh nâng cấp không liên quan gì
bỗng có một điểm nối, và người chơi là người tìm ra nó.

**Một cờ của RUN, không phải của `OrbitKind`.** Nó mở crit cho *mọi* loại cầu cùng lúc — mở riêng
từng loại sẽ là bốn thẻ gần giống nhau, và người chơi phải nhớ mình đã mở cho loại nào.

⚠️ Thẻ này phải đặt `maxPerRun = 1`: lấy lần hai không làm gì, và một thẻ vô dụng nằm trong bộ bài là
một lượt rút bị phí.

---

## Vòng đời: sống chết theo run

Cầu là thứ **thẻ arena** cho, nên chúng chết cùng run — đúng luật lớn nhất của `GATE_RUN.md`.

`OrbitRing` nằm trên thân MC (thân sống lâu hơn run), nên **`RunUpgrades.Dispose` phải dọn nó**, y
như cách nó gỡ modifier bằng `RemoveBySource`. Không dọn thì người chơi mang cầu về overworld.

Chết giữa run → `PlayerSystem.SwitchTo` dựng thân mới → cầu mất theo thân cũ. **Đúng ý**: chết là
mất build, và mất luôn cả cầu.

---

## Va chạm: hỏi `CombatWorld`, không dùng collider

Mỗi frame, mỗi quả cầu hỏi `CombatWorld.Overlap(vị trí, hitRadius, team)` — đúng đường mà
`ShapeAttack` và `Knife` đang dùng. Không thêm `Collider`, không thêm rigidbody: game này không có
physics collider cho combat, và thêm một đường thứ hai là hai luật va chạm tự do bất đồng.

Team lấy từ chủ sở hữu (`Teams.Player`), nên bộ lọc sẵn có tự lo chuyện không đánh nhầm phe.

⚠️ `Overlap` cảnh báo nếu bán kính vượt ô hash — `hitRadius` nhỏ nên không lo, nhưng đừng author to.

---

## Thẻ cấp cầu

`GrantOrbitEffect : IUpgradeEffect` — cắm vào ô `effect` của `RunUpgradeCard`, đúng chỗ
`SkillBuffEffect` đang ngồi. Mang: `OrbitKind` nào, cộng thêm mấy quả.

Nghĩa là **không cần class thẻ mới**, không cần đường đi mới trong `RunUpgrades` — nó đã gọi
`effect.Apply(context)` sẵn.

⚠️ **Nhưng `UpgradeContext` hiện không với tới thân MC.** Nó chỉ cầm `Stats`, `Source`,
`UnlockedSkills`, `SkillBuffs`. Cần thêm một đường: hoặc `UpgradeContext` mang thêm `MCController`,
hoặc effect gom lại thành một danh sách "cầu cần cấp" giống cách `SkillBuffs` đang làm rồi
`RunUpgrades` áp lên thân. **Cách thứ hai đúng hình hơn** — nó là mẫu đã có trong file, và nó giữ
được tính chất "effect không giữ tham chiếu tới thân thể có thể đã bị thay".

---

## Số cầu tối đa

Chưa chốt. Vòng chia đều nên 12 quả vẫn chạy, chỉ là mỗi quả cách nhau 30° và trông như một cái đĩa.
Nếu cần trần thì đặt trên `OrbitKind` (`maxCount`) chứ không phải trên thẻ — nó là đặc tính của loại
cầu đó, và thẻ hết tác dụng khi chạm trần thì `maxPerRun` trên `RunUpgradeCard` đã lo được.

---

## Đầu việc

- [ ] **`OrbitKind`** — `Config`: prefab, bán kính vòng, tốc độ quay, `damageShare`, `hitRadius`,
      `knockback`, `hitInterval`.
- [ ] **`OrbitSphere`** — component trên prefab cầu: hỏi `CombatWorld` mỗi frame, giữ sổ nhịp chạm
      lại, gây sát thương + knockback. **Không tự bay** — vòng đặt nó.
- [ ] **`OrbitRing`** — trên thân MC: `Add(kind, count)`, nhóm theo loại, mỗi frame tiến `phase` và
      đặt lại vị trí mọi quả theo `phase + i × 360/n`. `Clear()` cho lúc kết thúc run.
- [ ] **`GrantOrbitEffect`** — `IUpgradeEffect`, cộng N quả loại X.
- [ ] **Đường dẫn từ effect tới thân MC** — thêm danh sách gom trong `UpgradeContext`, đúng mẫu
      `SkillBuffs`; `RunUpgrades` áp lên thân sau khi effect chạy xong.
- [ ] **Dọn lúc kết thúc run** — `RunUpgrades.Dispose` gọi `OrbitRing.Clear()`.
- [ ] **Thẻ mở crit cho cầu** — một cờ sống trong `RunUpgrades` (cùng chỗ `_unlocked` skill đang ở),
      `OrbitSphere` hỏi trước khi roll. Thẻ `maxPerRun = 1`.
- [ ] **Author 4 loại**: sức mạnh, điện, lửa, nước — mỗi loại một `OrbitKind` + một prefab + một thẻ.
      Bán kính và chiều quay nên khác nhau để bốn vòng đọc được khi cùng chạy.
