# Gate Run — survivors-like mode

Thay đổi lớn về hướng game. Phần nền đã dựng (xem "Đầu việc"); phần còn lại là thiết kế.
Doc này **mâu thuẫn với `DESIGN.md` hiện tại** ở vài chỗ nền tảng — xem mục "Va chạm với DESIGN.md".

---

## Tóm tắt một câu

Overworld giữ nguyên là thế giới đi bộ, nhưng **không còn chiến đấu**. Toàn bộ combat chuyển vào
**gate** — cổng kiểu *Solo Leveling* đặt trên map, bước vào là bắt đầu một **run survivors-like**:
sống sót qua X ngày, giết quái lấy EXP để nâng cấp, gom tài nguyên dựng công trình phòng thủ.

---

## Vòng lặp

```
OVERWORLD (an toàn)                     GATE RUN (mọi thứ nguy hiểm)
 đi lại, chuyển map        ── vào ──►    sống sót X ngày
 tìm/mở gate                             ngày: farm quái, chặt cây phá đá, dựng công trình
 chuẩn bị                                đêm: tầm nhìn hẹp, quái khác, co cụm sau phòng thủ
        ▲                                       │
        └────────── thưởng ◄── clear/chết ──────┘
```

- Overworld = **vùng an toàn tuyệt đối**. Không quái, không combat, không chặt cây (chặt = đánh).
  Nó là hub: đi lại, chuyển map, tương tác, tiêu thưởng.
- Gate = **nơi duy nhất có gameplay chiến đấu**.
- Mọi thứ kiếm được **trong** run (level, upgrade) chết theo run. Cái chảy ra ngoài là **phần thưởng
  clear gate** — thứ nuôi meta-progression ở overworld.

---

## Overworld

Không đổi về cơ chế di chuyển/map: map là prefab, `MapService` swap, `Portal` → `Gate` (spawn point).

Cái đổi:
- **Không spawn quái.** `SpawnZone` trên map overworld thành vô nghĩa.
- **Không chặt/phá.** Không có node tài nguyên trên map overworld. Thứ tiêu ở đây (`PayGate`,
  `Bridge`) chạy bằng **tài nguyên overworld**, mà tài nguyên overworld chỉ đến từ **thưởng cuối
  run** — nên mọi đồng tiền ngoài này đều phải đi qua một cái gate.
- **Thêm cổng gate**: một đối tượng mới trên map, cầm **gate id**. Bước vào → vào run. Về mặt kỹ
  thuật nó *không* phải `Portal` hiện tại: `Portal` warp trong cùng một thế giới, còn cái này
  mở/đóng một vòng đời riêng.

---

## Gate run

### Điều kiện thắng
Sống sót qua **X ngày** (X do gate quy định). Hết ngày cuối → clear → nhận thưởng → về overworld.

### Kết thúc run

Hai lối ra, **cùng một cơ chế**: clear đủ X ngày, hoặc chết. Cả hai đều đưa người chơi về overworld.

**Thưởng tính theo số ngày đã sống sót**, không phải theo thắng/thua. Chết ở ngày cuối vẫn được gần
như trọn vẹn; chết ngày đầu thì gần như trắng tay. Không có màn "thua sạch".

*Vì sao đáng giữ:* nó biến chết từ một cái công tắc thành một cái thang. Người chơi luôn có lý do đi
tiếp thêm một ngày nữa, và một run hỏng vẫn là một run có giá trị — đúng tinh thần "chết không lấy đi
cái đã leo được" mà `DESIGN.md` đang giữ cho overworld.

**Gate lặp lại được** — clear rồi vào lại vẫn được.

### Thưởng: lần đầu là chính, cày lại là vét

Mỗi gate có **thưởng phá đảo lần đầu** — món lớn, trả đúng một lần cho cả save. Vào lại vẫn có
thưởng theo ngày sống sót, nhưng ở mức thấp hẳn, đủ để không phí công chứ không đáng cày.

**Đây chính là luật chống-cày mà `DESIGN.md` đang giữ, dịch sang ngôn ngữ gate.** Doc cũ nói phần
lớn EXP phải đến từ *firsts* — lần đầu vào một map, lần đầu giết một loài — vì một cái "lần đầu"
không cày được **do bản chất**, không cần tuning nào giữ nó. Ở đây cũng vậy: thứ đáng giá là *mở
được gate mới*, không phải *ở lại gate cũ*. Áp lực vẫn hướng ra ngoài.

Hạ tầng đã có sẵn và đúng hình: `ExperienceSystem.AwardOnce(key, amount)` với key namespaced
(`"kind:mewfrog"`, `"map:Map_2"`). Thưởng phá đảo gate là đúng một key nữa —
`"arena:<id>"` — chứ không phải một hệ ghi nhớ thứ hai.

### Người chơi mang gì vào
Đủ moveset hiện có: **di chuyển, đánh thường, dash, skill**.
→ **Giữ skill** (lý lẽ ở mục "Quyết định" bên dưới).

### Arena là thứ có dữ liệu, cổng chỉ là cái cửa

**`ArenaConfig` cầm tất cả**: rank, map, số ngày, độ dài ngày, quái nào sống ở đây (ngày/đêm), thưởng.
**`ArenaGate` chỉ cầm: đi tới arena nào** — kéo thả asset vào, hết.

*Vì sao không để luật trên cổng:* quái của một khu rừng mà do một cái cổng đứng ở đồng cỏ nào đó
quyết định thì ngược.

**Một arena, một cổng.** Không dựng nhiều cửa vào cùng một nơi.

### Ba luật của một run

1. **Vào là bắt đầu từ số không.** Level 0, không upgrade, túi rỗng, chưa có công trình nào,
   **máu đầy** và **dạ dày đầy**. Ba cái đầu tự đúng vì `RunScope` vừa mới sinh ra — chúng chưa từng tồn tại. Riêng
   thân thể là thứ duy nhất mang từ ngoài vào, nên máu và dạ dày phải được đặt lại tay lúc vào.
2. **Ra là arena quên sạch.** Cây đã chặt, tường đã dựng, quái đã dọn — lần sau vào lại y như lúc
   author. Cũng không phải luật ai đi canh: prefab map bị huỷ lúc bước ra và dựng lại lúc bước vào,
   còn mọi thứ run sở hữu thì nằm trong một scope không còn tồn tại.
   ⚠️ **Nên không thứ gì trong arena được ghi vào save.** `PayGate` author trong đó sẽ nhớ xuyên
   run — đúng lý do điểm build cần một bản song sinh sống trong `RunScope`.
3. **Không có đường ra tự nguyện.** Chết hoặc clear, không có lựa chọn thứ ba. `ArenaRunner` quét
   map lúc vào và báo đỏ nếu tìm thấy `Portal` hay `ArenaGate` nào trong arena.

**Rank nằm trên arena** và chỉ là **nhãn cho người chơi đọc trước khi bước vào**, không phải khoá tra
bảng. Cái gì thật sự sống ở đây và tràn ra nhanh chậm thế nào đều author thẳng trên chính asset đó —
nên nhãn không thể nói dối nội dung.

### Cái gì sống trong run, cái gì không

| Thứ | Phạm vi |
|---|---|
| Level + EXP trong run | **run** — sinh ra khi vào, mất khi ra |
| Upgrade nhặt được | **run** |
| Tài nguyên gom trong gate | **run** — không mang ra được |
| Công trình đã dựng | **run** |
| Thưởng cuối run (tài nguyên ngoài) | **thế giới** — cái duy nhất chảy ra ngoài |
| Level/upgrade tree của nhân vật (hệ hiện có) | **thế giới**, không đụng vào run |

**Đây là ranh giới quan trọng nhất của cả thiết kế.** Hai hệ tiến triển cùng tên gọi ("level",
"upgrade") nhưng khác vòng đời hoàn toàn, và chúng **không được dùng chung một hệ thống**.

---

## Năm hệ mới — hình của chúng

### 1. `ArenaConfig` — một arena là data

Kế thừa `Config`. Mang:

- **rank** (nhãn)
- **map id** của arena + spawn point vào
- **số ngày phải sống sót**, độ dài ngày, giờ mở
- **pool quái ban ngày / ban đêm** + nhịp leo thang theo ngày
- **thưởng phá đảo lần đầu**, **thưởng theo số ngày sống sót**

`ArenaGate` **kéo thẳng asset vào**, không gõ id. `MapService` dùng id vì prefab map nặng và chỉ
được có một cái trong bộ nhớ; `ArenaConfig` là asset bé, không có luật đó — và project đã có tiền lệ
ngược (`PayGate` cầm thẳng `ResourceDef`, `SpawnZone` cầm thẳng danh sách quái). Một id gõ tay ở đây
chỉ là thêm một cái tên nữa phải tự khớp, mà Inspector không báo được khi nó lệch.

`mapId` thì **vẫn là chuỗi** — chỗ đó nó thật sự đi qua `Resources.LoadAsync`.

### 2. Arena là nơi, gate là cửa — và không cần cờ nào cả

**Đặt tên map theo NƠI CHỐN, không theo gate**: `Arena_Forest`, `Arena_Cave`. Một arena phục vụ được
nhiều gate — quái và thưởng đến từ `ArenaConfig`, không đến từ mặt đất — nên một map tên `Gate_1` sẽ
nói dối ngay ngày cái gate thứ hai trỏ vào nó. Cũng đừng nhét rank hay số ngày vào tên, cùng lý do.
Overworld giữ `Map_1`, `Map_2`.

Còn ở runtime thì không thêm `IsGateMap`. Map chỉ là hình học; **thứ quyết định là có `RunScope`
sống hay không**.
Đang trong run thì đang trong gate — một cờ trên map sẽ là câu trả lời thứ hai, tự do bất đồng với
câu thứ nhất ngay ngày ai đó mở arena đó bằng đường khác.

`ArenaConfig` trỏ tới map id, `MapService` load đúng như mọi map khác. Cái arena mang mà map
overworld không mang chỉ là **điểm build** và **node tài nguyên** — hai component, không phải một
danh tính.

### 3. `ArenaRunner` — hệ vận hành một run

Sống ở `GameScope` (nó sống lâu hơn bất kỳ run nào) và sở hữu `RunScope`. Một run đi qua:

```
đọc ArenaConfig
  → warp sang arena
  → dựng RunScope (clock, run level, ví run, build state, director)
  → chạy: clock đếm ngày, director đẻ quái
  → kết thúc: hết ngày X, hoặc người chơi chết
  → chốt sổ (số ngày sống sót) → tháo RunScope → warp về overworld → trả thưởng
```

**Trả thưởng SAU khi tháo scope**, không phải trước: thưởng thuộc về thế giới, và làm đúng thứ tự
này thì không tồn tại khoảnh khắc nào cả hai ví cùng mở.

### 4. Spawn director — sống ở `RunScope`, không nằm trên map

Đọc **lịch đợt** từ `ArenaConfig` của arena đang chạy, lấy ô hợp lệ từ `TerrainGrid` của map. Prefab
map chỉ là hình học; ai đến và đến lúc nào là chuyện của asset arena.

Một đợt: `{ enemyId, day, fromHour, toHour, count }`. Số lượng rải đều trong khung giờ; hai giờ bằng
nhau = ập xuống một lúc. Mỗi dòng là **một ngày** — muốn đợt giống nhau mỗi đêm thì viết nhiều dòng,
và đó là cái giá phải trả để đêm bốn khác được đêm ba mà không đụng gì khác.

Khác `SpawnZone` ở đúng chỗ cốt lõi: `SpawnZone` **giữ một dân số tĩnh** (fill tới capacity, chết
thì đẻ bù). Director **chạy một lịch**: cái gì tới, lúc nào, bao nhiêu con — nhắm vào người chơi,
từ ngoài màn hình. Một bên là thế giới có thú sống trong đó; bên kia là một cái đêm ai đó viết ra.
Không có cấu hình nào biến cái thứ nhất thành cái thứ hai.

### 5. EXP: hai đường, tuyệt đối không nối

`ExpOnDeath` hiện gọi thẳng `ExperienceSystem.Award(cfg.Exp)` — EXP **thế giới**, cộng vào level
nhân vật, có save. Trong run phải đi đường khác: rơi thành vật thể, tự hút, cộng vào **run level**.

**Hệ quả đáng chú ý: `ExperienceSystem.Award` mất sạch caller.** Combat chỉ còn trong gate, và mọi
mạng quái trong gate trả EXP run. Nghĩa là **EXP thế giới trở thành 100% "firsts"** — lần đầu vào
map, lần đầu giết một loài, lần đầu phá đảo một gate.

Đó đúng là thứ `DESIGN.md` muốn nhưng chưa bao giờ làm trọn: nó viết *"tiền thưởng mỗi mạng phải đủ
nhỏ để đứng cày một bãi đã dọn là một cách sống tồi"* — tức là vẫn phải tuning một con số để giữ
luật. Giờ luật đúng **do cấu trúc**, không còn con số nào phải canh.

`AwardOnce($"kind:{id}")` (bestiary, lần đầu giết một loài) thì **ở lại** và vẫn trả EXP thế giới:
nó là một "first", không cày được, và nó thưởng cho việc đi nhiều loại gate khác nhau thay vì cắm
đầu vào một cái. Đúng hướng cần đẩy.

## Ngày / đêm

- **Đêm giới hạn tầm nhìn.** Hệ veil + light buffer sẵn có (`LightManager`, `DarknessMask`,
  `VisionLight`) đã làm được việc này — chủ yếu là tuning và cho công trình phát sáng vào buffer.
- **Bảng spawn ngày ≠ đêm**: loại quái, mật độ, hành vi có thể khác.
- Đuốc/nguồn sáng vì thế là công trình **có giá trị cơ chế**, không phải trang trí.
- Nhịp tự nhiên của một ngày: sáng bung ra farm + gom tài nguyên + dựng đồ → tối co về vùng sáng và
  chống chịu.

### Hai đồng hồ, hai vai trò khác hẳn

- **Overworld**: vẫn có ngày/đêm, nhưng **thuần trang trí**. Không có gameplay nào phụ thuộc nó —
  không quái, không tầm nhìn hạn chế, không spawn table. Chỉ là ánh sáng đổi màu cho thế giới sống.
- **Trong gate**: ngày/đêm là **logic**. Tầm nhìn, bảng spawn, buff điều kiện, mốc thưởng qua đêm,
  và cả điều kiện thắng đều đọc từ nó.

`DayNightClock` hiện tại là clock toàn cục, hard-code 300s/ngày, `ITickable` ở scope App — nó ở đúng
chỗ để làm cái đồng hồ trang trí của overworld. Run cần **clock thứ hai**, sống trong `RunScope`:
bắt đầu buổi sáng ngày 0 khi vào gate, đếm tới X, kết thúc run.

**Cái phải làm cho sạch: `DayNightLighting` đang đọc thẳng `DayNightClock`.** Nó phải đọc qua một
interface và được trỏ vào đồng hồ nào đang cầm quyền, nếu không màn hình trong gate sẽ sáng tối theo
giờ của overworld. Đây là chỗ duy nhất hai đồng hồ chạm nhau — và cũng là chỗ duy nhất bắt buộc phải
trừu tượng hoá.

---

## Nâng cấp trong run

### Nguồn: **EXP từ giết quái** (đã chốt)

Không phát upgrade theo mốc ngày, vì: nếu giết quái không trả thưởng trực tiếp thì lối chơi tối ưu
là **né quái và câu giờ cho hết ngày** — đúng thứ thể loại này không được phép cho phép. EXP-per-kill
buộc vòng "giết → mạnh → giết nhanh hơn" tự chạy, và tự cân theo mức liều của người chơi.

Phụ: nhịp. Sống X ngày mà chỉ có X lần chọn thì snowball quá thưa để cảm nhận được.

### Mốc qua đêm vẫn thưởng, nhưng **khác loại**

- **Level-up (EXP)** → upgrade **chiến đấu cá nhân**: dmg, tốc, hành vi vũ khí, buff theo pha
  ngày/đêm.
- **Qua được một đêm** → thưởng nghiêng về **phòng thủ/hạ tầng**: tài nguyên, mở loại công trình,
  sửa/nâng công trình.

Tách vậy hai vòng lặp không giẫm chân: đánh quái nuôi *bản thân*, sống sót nuôi *căn cứ*.

### Upgrade đọc được trạng thái ngày/đêm

Ví dụ đã nêu: **Lén lút** — tăng mạnh tốc chạy khi trời tối.
→ Hệ upgrade run không chỉ cộng stat phẳng: nó cần **buff có điều kiện**, bật/tắt theo pha thời gian.

---

## Tài nguyên & xây dựng

### Hai loại tài nguyên, hai ví riêng

| | Kiếm ở đâu | Tiêu vào đâu | Sống bao lâu |
|---|---|---|---|
| **Tài nguyên trong run** | chặt/phá node trong gate | điểm build trong gate | chết theo run |
| **Tài nguyên ngoài** | thưởng cuối run (phá đảo lần đầu + theo ngày sống sót) | `PayGate`/`Bridge` và mọi thứ tiêu tiền ở overworld | vĩnh viễn, có save |

**Hai bộ `ResourceDef` tách hẳn, không trùng loại nào.** Gỗ chặt trong gate và tài nguyên overworld
là hai thứ khác nhau, không quy đổi.

*Vì sao đáng làm vậy:* nếu dùng chung loại thì mỗi lần cân bằng phải trả lời "một khúc gỗ trong gate
đáng bao nhiêu ngoài overworld", và mọi thay đổi giá ở một bên rò sang bên kia. Tách ra thì hai bảng
giá độc lập, và người chơi cũng đọc được ngay cái nào tiêu ở đâu mà không cần học luật.

Hệ quả kỹ thuật: **hai ví `Inventory` riêng** — ví run sống trong `RunScope`, ví ngoài giữ nguyên
qua `InventorySystem` (có save). Không có đường nào nối chúng ngoài bảng thưởng cuối run.

### Tài nguyên **không rơi từ quái thường**

Kiếm bằng **đập vật thể trên map**: chặt cây ra gỗ, phá đá ra đá.
- Node **có máu** — cơ chế này **đã code sẵn** (`Damageable` + `PropConfig` + `DropOnDeath`,
  "chopping is attacking").
- **Không respawn** → tài nguyên trong gate là hữu hạn, người chơi phải bung ra xa dần theo ngày.
  Áp lực không gian tự sinh, không cần luật thêm.
- Bù nguồn về sau (chưa chốt, để dành): upgrade sinh tài nguyên, hoặc **quái đặc biệt** rơi/tạo
  tài nguyên.

**Lý do tách khỏi drop quái:** EXP thì tự hút về khi lại gần; tài nguyên thì **phải đi tới, nhặt,
mang lên điểm build**. Hai cảm giác vận động khác nhau — EXP thưởng cho *xông vào*, tài nguyên
thưởng cho *rời tuyến an toàn đi kiếm*. Nếu quái rơi cả hai thì chỉ còn đúng một hành vi: cày quái.

### Sức chứa vô hạn
Không có quản lý kho, không có bỏ đồ. Khớp với `Inventory` hiện tại (không cap, có chủ ý).

### Điểm build

- Map gate có **điểm build đặt sẵn** (authored), mỗi điểm ghi rõ **yêu cầu tài nguyên**.
- Người chơi **đứng vào và nạp dần**, từng phần một, thấy số đếm lên.
- Đủ → công trình mọc lên.

**Cơ chế này đã tồn tại gần như nguyên vẹn: `PayGate`.** Nó đã làm đúng: nạp từng nhịp khi đứng
trong zone, giữ tiến độ dở, có visual bay từ người vào hố, bật `unlocks` / tắt `preview` khi đủ.
Khác biệt duy nhất là **`PayGateSystem` lưu vĩnh viễn vào save** — trong run thì phải là bộ nhớ
sống-chết theo run.

### Loại công trình (danh sách mở)
- **Đuốc** — nguồn sáng, chống lại giới hạn tầm nhìn ban đêm
- **Hàng rào** — chặn đường
- **Tháp bắn** — sát thương tự động
- **Bẫy** — sát thương theo vùng/kích hoạt

### Công trình đứng phe nào — và vì sao `Teams` phải rework

Ý muốn:
- **Công trình chiến đấu** (tháp bắn, bẫy): quái **chủ động đánh**, gãy thì phải xây lại.
- **Công trình không chiến đấu** (tường, đuốc): quái **không nhắm vào**, nhưng **vẫn ăn sát thương
  lạc** (nổ, quét, AoE).
- **Người chơi không bao giờ đánh trúng công trình của mình.**

**Không số phe nào hiện tại diễn tả được cái ở giữa.** `Teams` đã tách đúng hai câu hỏi
("đánh trúng được không" vs "có đáng săn không"), nhưng câu thứ hai đang **suy ra từ chính con số
phe** (`IsPrey(team) = team < Resource`), nên ba lựa chọn đều hỏng:

| Đặt tường ở | Player đánh trúng? | Quái đánh trúng? | Quái nhắm vào? |
|---|---|---|---|
| `Player` (1) | không ✅ | có ✅ | **có ❌** |
| `Universal` (0) | **có ❌** | có ✅ | **có ❌** |
| `Resource` (10000) | **có ❌** | có ✅ | không ✅ |

`Universal` còn tệ hơn ở chỗ nó đã mang một nghĩa khác hẳn — *nguồn sát thương không thuộc phe nào*
(bẫy, lửa, đá rơi) — và một `Unit` để ở 0 là dấu hiệu "ai đó quên set phe". Mượn nó làm "công trình
không bị nhắm" là phá lại đúng thứ vừa được dọn.

**Cách sửa: mọi công trình đều là phe `Player`, còn "có đáng săn không" chuyển thành thuộc tính của
chính đối tượng, thôi suy ra từ con số phe.** Thêm một cờ vào `IDamageable` (kiểu `IsHuntable`),
mặc định lấy đúng `Teams.IsPrey(Team)` như hiện nay nên **không có gì đang chạy bị đổi hành vi**;
tường và đuốc trả `false`.

Được cả ba yêu cầu cùng lúc: cùng phe player nên player không đánh trúng; khác phe quái nên đòn
quái vẫn ăn vào (kể cả AoE lạc); cờ `false` nên `FindHostile` không bao giờ chọn nó.
`Teams.IsPrey` vẫn ở lại làm mặc định — nó đúng cho cây đá, chỉ sai khi bị dùng làm câu trả lời
cuối cùng.

Sửa ở `AIContext.FindHostile`: đổi `Teams.IsPrey(d.Team)` → `d.IsHuntable`. Một dòng.

---

### Tường: quái đi vòng — và đó là lúc phải làm tìm đường

Tường **chặn**, quái **đi vòng**, không đập (nó không phải mục tiêu). Nghĩa là AI cần tìm đường thật,
chứ `StraightPursuit` hiện tại sẽ đâm đầu vào tường mãi.

**Tìm đường là một hành vi, không phải một luật toàn cục.** Kiến trúc AI hiện có (`AIStrategies`,
behaviour cắm bằng `[SerializeReference]`) đã đúng hình cho việc này: `StraightPursuit` **ở lại**
nguyên vẹn cho quái ngu, thêm một `PathPursuit` cho quái khôn. Loại nào biết đi vòng là một quyết
định author trên brain của loài đó.

**Đây chính là cái làm tường có nghĩa.** Tường không phải là "cản một chút" — nó là **khắc chế
tuyệt đối quái ngu** và **vô dụng trước quái khôn**. Vai trò rõ ràng, người chơi đọc được ngay:
xây tường là câu trả lời cho bầy đông ngu, không phải cho con săn mồi biết nghĩ.

Kéo theo, phải làm:
- **Nav từ lưới ô đi bộ được.** `TerrainGrid` + ô walkable đã bake sẵn → A* trên lưới là lựa chọn tự
  nhiên, không cần NavMesh.
- **Cập nhật nav khi tường mọc/gãy.** Chỉ vá quanh footprint, không rebuild cả lưới. Đây là bản
  đảo ngược của `WalkableSurface` mà `DESIGN.md` đã mô tả cho cầu: cầu **thêm** ô đi được, tường
  **bỏ** ô đi được.
- **Quái ngu gặp tường thì làm gì?** Nó không nhắm tường nên không đập; đứng ép vào tường là kết
  quả mặc định. Chấp nhận được và đúng ý đồ, nhưng cần nhìn thực tế xem một bầy dồn cục vào tường
  trông có ổn không.

## Va chạm với `DESIGN.md`

Doc hiện tại phải sửa, không phải bổ sung. Những chỗ mâu thuẫn:

1. **Trụ cột "exploration là điểm chính"** — vẫn đúng ở overworld, nhưng ý nghĩa đổi: khám phá giờ
   là *tìm và mở gate*, không phải *đẩy frontier để ăn drop tốt hơn*.
2. **"Core loop = đẩy ra xa, drop tốt hơn, mở checkpoint mới"** — không còn drop ngoài overworld.
   Nguồn phần thưởng giờ là **clear gate**, và thang đo độ khó là **rank gate**, không phải khoảng
   cách.
3. **`ExperienceSystem` và luật chống-cày** — hệ hiện tại cố tình đẩy phần lớn EXP vào "firsts"
   (lần đầu vào map, lần đầu giết một loài) để một map an toàn không cày được. Thiết kế mới đưa
   **EXP-per-kill** vào trung tâm. **Không mâu thuẫn nếu giữ đúng ranh giới**: EXP trong run không
   bao giờ vào ngân hàng thế giới — nó reset mỗi run nên không tồn tại khái niệm "cày". Nhưng phải
   viết ra rõ, nếu không hai hệ sẽ tự trôi vào nhau.
4. **"Chopping is attacking"** — vẫn giữ, nhưng giờ chỉ xảy ra trong gate. Nghĩa là **overworld
   không có nguồn tài nguyên**, mà `PayGate`/`Bridge` ở overworld lại đang tiêu tài nguyên. Xem
   câu hỏi mở #1.
5. **Death penalty ("ratchet: đất đã dọn thì ở lại dọn")** — nghĩa vẫn giữ nhưng đối tượng đổi:
   cái ở lại là **thưởng phá đảo lần đầu của mỗi gate**, không phải đất đã đi qua.
6. **Hunger giờ là cơ chế của arena, không phải của thế giới.** `DESIGN.md` đang bán nó như "thứ
   kết thúc một chuyến đi" — việc đó giờ do số ngày làm. Trong arena nó là áp lực bắt người chơi rời
   chỗ an toàn đi kiếm ăn; ngoài overworld nó không còn nghĩa gì và bị tắt hẳn.

---

## Đã chốt (quyết định + lý do)

**Giữ skill, không bỏ.**
- Vấn đề kinh điển của survivors-like là người chơi **chỉ còn mỗi việc di chuyển** — đánh là tự
  động. Game này không auto-attack; đánh thường + dash + skill là ba mức chủ động. Bỏ skill là tự
  đẩy mình về phía cái tẻ nhạt của thể loại mà không được lợi gì.
- `DESIGN.md` định nghĩa nhân vật = **stats + attack + skill**. Bỏ skill thì nhân vật sụp còn
  stats + attack, và trục phân biệt nhân vật mất một nửa.
- Hạ tầng đã có sẵn và rẻ: `CharacterSkill`, `AbilitySlot`, `SkillBuffEffect`, `UnlockSkillEffect`.
  Skill còn là **một trục pool upgrade rất tốt** trong run (buff skill, giảm cooldown, đổi hành vi).

**EXP từ quái là nguồn upgrade trong run** (lý do ở trên).

**Tài nguyên từ node trên map, không từ quái thường** (lý do ở trên).

**Tài nguyên không mang ra khỏi run; thưởng cuối run tính theo số ngày sống sót.**

**Clear và chết dùng chung một lối ra**, chỉ khác số ngày đã sống. Không có "thua trắng".

**Gate lặp lại được.**

**Công trình đều là phe `Player`; "có bị nhắm không" là thuộc tính riêng, không suy từ số phe.**

**Tường chặn đường, quái khôn đi vòng, quái ngu đứng lại** — tìm đường là một behaviour cắm vào
brain của từng loài, không phải luật chung.

**Mỗi gate có thưởng phá đảo lần đầu; cày lại chỉ còn thưởng vét.**

**Tài nguyên trong gate và tài nguyên overworld là hai bộ `ResourceDef` tách hẳn.**

**Overworld giữ ngày/đêm nhưng chỉ là trang trí; trong gate nó là logic.**

**Hunger chỉ sống trong arena** — ngoài overworld tắt hẳn, thanh máu cũng ẩn.

---

## Câu hỏi mở

Không còn cái nào chặn việc code. Những thứ dưới đây chốt lúc gặp là được:

- **Đồng hồ overworld có chạy tiếp khi đang ở trong gate không?** Nó chỉ là trang trí nên đằng nào
  cũng không sai — chốt khi thấy lúc bước ra khỏi gate trời nên là mấy giờ.
- **Bù nguồn tài nguyên trong run**: upgrade sinh tài nguyên, hay quái đặc biệt rơi/tạo? Để dành.
- **Danh sách công trình** còn mở — đuốc/tường/tháp/bẫy là bốn cái đầu, không phải bốn cái duy nhất.

---

## Đầu việc

### 🔜 Tìm đường — LÀM TRƯỚC

**Flow field, không phải A* mỗi con.** Trong survivors-like mọi con cùng đuổi một mục tiêu, nên A*
từng agent là 80 lần tìm ra 80 kết quả gần giống hệt nhau. Thay bằng **một lượt BFS từ người chơi lan
khắp lưới**, mỗi ô ghi "đi hướng nào để lại gần hơn"; quái chỉ đọc ô nó đang đứng — O(1), và **chi phí
không phụ thuộc số quái**. 32×32 = 1024 ô, tính lại 3–4 lần/giây là không đáng kể.

**Không dùng nửa ô.** ×4 số node chỉ để đổi lấy một cái cầu thang mịn hơn, vẫn là cầu thang. Độ mượt
đến từ hai thứ rẻ hơn: **(1) thấy thì đi thẳng** — kiểm một đường thẳng tới người chơi có xuyên tường
không, không xuyên thì đi thẳng và bỏ qua field hoàn toàn (trong arena thoáng đây là phần lớn thời
gian, và nó không có cảm giác đi theo lưới); **(2) nội suy field** giữa ô hiện tại và ô kế bên thay vì
bám 8 hướng.

**Quái ngu vẫn ngu — vì thiết kế, không vì tiết kiệm.** Đã chốt: tường là khắc chế tuyệt đối bầy ngu
và vô dụng trước con biết nghĩ. Con nào cũng đi vòng được thì tường mất sạch ý nghĩa.

- [ ] **`FlowField`** trong `RunScope`: BFS từ người chơi trên ô walkable, nội suy, tính lại theo nhịp.
- [ ] **`FlowPursuit`** — behaviour mới cắm cạnh `StraightPursuit` (cái cũ **ở lại** cho quái ngu).
      Loài nào biết đi vòng là quyết định author trên brain.
- [ ] **Nhánh "thấy thì đi thẳng"** trước khi đọc field.
- [ ] **Đánh dấu bẩn khi tường mọc/gãy**, tính lại ở nhịp sau. Không rebuild ngay giữa frame.
- [ ] *(để dành)* A* thật sự cho từng agent — chỉ đáng làm khi có con cần đi tới **thứ khác ngoài
      người chơi** (một cái tháp, một điểm build). Lúc đó là behaviour thứ ba, không phải viết lại.

### Nền — phải xong trước, cái khác treo vào đây

- [x] **`RunScope`: vòng đời một run.** ✅ Không phải một `LifetimeScope` prefab — một scope con dựng
      lúc chạy bằng `container.CreateScope(...)`, đúng mẫu `EnemySpawner` đã dùng cho scope-per-kind.
      `ArenaRunner` sở hữu nó; hiện đăng ký `ArenaConfig` + `RunClock`, và run level / ví run / build
      state cắm thêm vào đây khi tới lượt.
- [x] **`ArenaConfig`.** ✅ `Config`:
      rank, map id, gate index, số ngày, độ dài ngày, giờ mở cổng. Pool quái và bảng thưởng thêm
      vào khi có director và có hệ thưởng — chưa ai đọc thì chưa dựng field.
- [x] **`Gate` — miệng cổng trên map overworld.** ✅ `InteractZone`, cầm **gate id** + spawn point
      để bước ra. Không kế thừa `Portal`: `Portal` di chuyển trong cùng một thế giới, cái này
      mở/đóng một vòng đời. Tự `LogError` lúc `Start` nếu spawn point đi ra nằm trong vùng của
      chính nó — không thì run vừa xong là mở run tiếp, vòng vô tận.
- [x] **Dọn tên "gate".** ✅ Nó đang mang ba nghĩa. `Gate` cũ (điểm hạ cánh) → **`SpawnPoint`**,
      `Map.GetGate`/`gates`/`gateIndex`/`targetGateIndex` → `GetSpawnPoint`/`spawnPoints`/
      `spawnIndex`/`targetSpawnIndex` (kèm `[FormerlySerializedAs]` nên prefab cũ không mất dữ
      liệu). Tên `Gate` trả về cho thứ mà cả người chơi lẫn doc đều gọi là cổng. `PayGate` ở lại —
      từ ghép, không lẫn được.
- [x] **`ArenaRunner`.** ✅ Sống ở `GameScope`, `ITickable`, sở hữu `RunScope`. Vào: dựng scope →
      trỏ đồng hồ → warp (map inject **qua run scope**). Ra (hết ngày X **hoặc** chết): chốt sổ →
      hồi sinh nếu chết → warp về → tháo scope → **rồi mới** bắn `Ended`.
- [x] **`RunClock`.** ✅ Ngày/đêm riêng của run. Một "ngày" là **một chu kỳ đầy đủ tính từ lúc bước
      vào**, không phải ngày lịch kết thúc lúc nửa đêm — nếu không thì ngày đầu ngắn hơn mọi ngày
      khác và đêm rơi vào chỗ khác nhau tuỳ giờ cổng mở. Chỉ lưu số giây đã trôi; giờ và số ngày là
      phép tính trên đó.
- [x] **Tách `DayNightLighting` khỏi `DayNightClock` cụ thể.** ✅ `ITimeOfDay` + `ActiveTimeOfDay`
      (uỷ nhiệm, không phải công tắc). `DayNightLighting` và `ShadowSun` giờ inject `ITimeOfDay` và
      không biết gate tồn tại. `DayNightDebug` vẫn giữ `DayNightClock` cụ thể — nó cố ý là công cụ
      của overworld.
- [x] **`MapService` nhận scope để inject map.** ✅ `WarpAsync(mapId, gateIndex, into = null)`.
      Map gate được inject qua run scope nên thứ author trong đó (điểm build, node) sẽ resolve dịch
      vụ của run và chết cùng run. Đổi sang instantiate rồi `InjectGameObject` — `resolver.Instantiate`
      trên scope con sẽ đi vòng qua scope cha (cái bẫy `PlayerSystem.Spawn` đã ghi lại).

- [x] **Cây nâng cấp bỏ ăn theo level.** ✅ `UpgradePoints` — một loại điểm riêng, per-character, có
      save. `UpgradeSystem.Available = Earned − Spent`, không đụng `CharacterLevels` nữa.
      *Vì sao:* level leo vì lý do của nó; buộc cây vào đó nghĩa là mọi thay đổi về cách tính EXP
      đều âm thầm là thay đổi về việc cây làm ông mạnh tới đâu. Hai thứ lớn vì hai lý do thì cần hai
      con số.
      ⚠️ **Chưa có gì phát điểm** — thưởng cuối run sẽ là nguồn. Tạm thời có cheat "Add points"
      trong bảng dev.
- [ ] **Chỗ đặt quái: hai lỗ đã biết, chưa vá.** Hiện chọn chỗ bằng: random góc + random bán kính
      trong `spawnRing` → `WorldToCell` (trượt nếu ra ngoài lưới) → `IsWalkable` (trượt nếu
      nước/tường/hố; `TerrainKind.Water` và `None` đều chặn) → đặt vào **tâm ô**. Thử 12 lần rồi bỏ.
  - **Vòng spawn phần lớn là nước thì đợt nghẽn.** Người chơi đứng trên mỏm đất chìa ra biển là
    phần lớn vòng tròn thành nước, 12 lần trượt hết. Vá bằng một lượt **quét có hệ thống** khi
    random thất bại: chạy vòng góc đều ở vài bán kính, lấy ô hợp lệ đầu tiên.
  - **Không có `clearance`.** Chỉ kiểm đúng một ô, nên quái sinh ra dính sát vách, hoặc lọt vào một
    hốc đất một ô giữa nước. `SpawnZone` cũ có khái niệm này; director thì chưa.
- [ ] **Biên map phải là ô không đi được.** Va chạm tile chỉ phát mặt chắn từ **ô bị chặn**, mà ô
      ngoài lưới thì không được duyệt — nên nếu ô ngoài cùng của map là đất đi được, thân thể đi
      thẳng ra khỏi lưới được. Viền arena bằng nước/void là cái chặn thật. Không gây hỏng gì
      (`IsWalkable` chỉ dùng lúc đặt, đuổi là đường thẳng, combat là hash theo toạ độ) — chỉ là quái
      đi lạc ra ngoài rồi đứng đó.
- [ ] **Thưởng cuối run.** Bảng thưởng trên `ArenaConfig`, một đường ra chung cho cả clear lẫn chết:
      phần theo số ngày sống sót, cộng **phần phá đảo lần đầu** qua `ExperienceSystem.AwardOnce`
      với key `"arena:<id>"` — không dựng hệ ghi-nhớ-lần-đầu thứ hai.
      Chỗ cắm đã có: `ArenaRunner.Ended` mang `ArenaResult { ArenaId, DaysSurvived, Cleared }`.
- [ ] **Ví tài nguyên riêng cho run.** `Inventory` thứ hai sống trong `RunScope`, không đi qua
      `InventorySystem` (cái đó lưu save). Ví ngoài giữ nguyên như hiện tại.
- [ ] **Bộ `ResourceDef` cho tài nguyên overworld**, tách hẳn khỏi bộ trong gate.
- [ ] **Chết trong run: hồi sinh đang mượn `PlayerSystem.SwitchTo`.** Đúng nghĩa dưới mô hình
      possession (một thân thể mới cho cùng nhân vật) và không đẻ ra cơ chế mới, nhưng game vốn
      **chưa có xử lý chết** ở đâu cả. Khi làm màn hình chết / hiệu ứng chết thì nhìn lại chỗ này.

### Chiến đấu trong run

- [x] **Spawn director.** ✅ `ArenaDirector`, sống trong run, `ArenaRunner` tick. Lịch là **đợt được
      author**, không phải đường cong tỉ lệ: mỗi dòng `ArenaWave` = `{ enemyId, day, fromHour, toHour,
      count }` — *"đêm ngày 2, ba mươi con này, từ 19h tới 22h"*. Số lượng rải đều trong khung giờ;
      đặt hai giờ bằng nhau là cả đợt ập xuống một lúc.
      *Vì sao author chứ không suy ra:* một đường cong nói "khó dần lên" rồi để phần thật sự xảy ra
      cho phép tính không ai hình dung nổi; một đợt là thứ người thiết kế quyết được, đọc lại được,
      và sửa đúng một con số. Mỗi dòng giữ sổ riêng nên hai đợt trùng giờ chỉ là hai sổ cùng chảy.
- [x] **Đẻ ngoài tầm nhìn, đo chứ không author.** ✅ Bán kính tính từ **camera thật** mỗi lần (chiếu
      4 góc viewport xuống mặt đất), rồi vẫn kiểm lại bằng viewport. Xoay Q/E hay zoom đều đúng —
      một con số ring gõ tay sẽ sai ở mọi mức zoom trừ một.
- [x] **Quái luôn hung hăng.** ✅ `HuntAggro` — một `IAggro` mới, nhắm thẳng người chơi, không bán
      kính. `SightAggro` hỏi "quanh đây có gì không", câu đó bị chặn bởi ô hash của `CombatWorld` nên
      không nới ra thành "khắp map" được; hunter hỏi câu khác hẳn và có câu trả lời thẳng.
      ⚠️ Brain của quái arena phải để `leashRadius` rất lớn, không thì FSM bỏ đuổi rồi bắt lại liên
      tục thành giật cục.
- [x] **AI đọc đúng đồng hồ.** ✅ `EnemyAI` inject `ITimeOfDay` thay vì `DayNightClock` — quái ăn đêm
      trong arena phải thức theo **đêm của run**, không phải giờ của overworld.
- [ ] **Quyết định số phận `SpawnZone`.** Overworld hết quái, run dùng director ⇒ nó thành code
      chết → xoá, đừng để lại.
- [ ] **EXP drop từ quái + tự hút về.** `ExpOnDeath` hiện gọi thẳng `ExperienceSystem.Award` (EXP
      thế giới, có save). Trong run phải rơi thành vật thể nhặt được và vào **run level**.
      `AwardOnce("kind:<id>")` (bestiary) thì ở lại nguyên — nó là một "first".
- [ ] **Dọn `ExperienceSystem.Award` nếu hết caller.** Sau khi định tuyến lại, EXP thế giới chỉ còn
      đến từ firsts. Nếu không còn ai gọi `Award` thì xoá, đừng để một đường vào không dùng.
- [ ] **Bỏ combat khỏi overworld.** Gỡ spawn zone ở map overworld; chốt xem `Damageable` trên cây
      overworld còn giữ không (câu hỏi mở #1).

### Nâng cấp trong run

- [x] **`RunLevel`: EXP → level → mở lượt chọn.** ✅ Sống trong `RunScope`, không đụng
      `CharacterLevels`. Đường cong `expToNext` author trên `ArenaConfig` (x = level) chứ không phải
      công thức trong code — arena test ngắn và arena thật dài muốn hai đường leo khác hẳn nhau.
      Lên nhiều cấp một lúc thì trả từng cấp một, giữ phần dư.
- [x] **Draft upgrade khi level-up.** ✅ `RunUpgradePopup` — 3 thẻ, chọn 1, đóng. **Pause bằng cả hai
      cần**: `timeScale = 0` + `IInputGate`, đúng luật `ARCHITECTURE.md`. Không Esc, không bấm ra
      ngoài, không bỏ qua — lối ra duy nhất là chọn, vì một draft bỏ qua được là một cấp bị tiêu mất
      trong im lặng. Lên hai cấp cùng lúc = hai cửa sổ nối tiếp, không gộp.
- [x] **EXP giết quái vào run, không vào save.** ✅ `ExpOnDeath` rẽ nhánh theo `ArenaRunner.InRun`.
      Bestiary (`AwardOnce("kind:…")`) thì vẫn là của thế giới — một "first" không cày được nên trả
      từ trong run vẫn an toàn, và nó thưởng cho việc đi những arena có loài chưa gặp.
- [x] **Pool upgrade run** (data). ✅ `RunUpgradeDeck` (asset riêng, nhiều arena dùng chung) chứa
      `RunUpgradeCard` = tiêu đề + `IUpgradeEffect`. Tái dùng đúng `StatBuffEffect` và
      `SkillBuffEffect` của cây nhân vật, nên thẻ "+2 dash distance" chạy được mà không viết thêm
      class nào. **Không** tái dùng `UpgradeTreeConfig` (cây, ranks, points, save — sai vòng đời).
      ⚠️ **Mọi modifier gắn nhãn nguồn = object của run**, gỡ sạch bằng `RemoveBySource` lúc kết
      thúc. Thân thể sống lâu hơn run (người chơi bước ra bằng chính nó), nên một modifier còn sót
      lại là đường duy nhất sức mạnh của run có thể theo về thế giới.
      Hiện rút 3 thẻ **ngẫu nhiên có trọng số, không trùng nhau**; luật chọn thẻ phức tạp hơn để sau.
- [ ] **Buff có điều kiện ngày/đêm.** Một `IUpgradeEffect` mới đọc pha thời gian, bật/tắt modifier
      (vd *Lén lút*). `StatModifier` đã có, thiếu là cái công tắc.
- [ ] **Thưởng cuối mỗi đêm** (khác pool với level-up): tài nguyên / mở công trình / nâng công trình.

### Tài nguyên & xây dựng

- [ ] **Node tài nguyên trong arena.** Chủ yếu là author: đặt cây/đá với `PropConfig` +
      `DropOnDeath`, không respawn. Code gần như đã đủ.
- [ ] **`BuildSpot`.** Tách bản chạy-trong-run ra khỏi `PayGate`: cùng cơ chế nạp dần + visual,
      nhưng tiến độ nằm trong `RunScope` chứ không phải `PayGateSystem` (save vĩnh viễn).
      Cân nhắc: rút phần "nạp dần" thành cái dùng chung, giữ hai chủ sở hữu trạng thái khác nhau.
- [ ] **Công trình: đuốc.** Phát sáng vào light buffer, có HP, quái đập được.
- [ ] **Công trình: hàng rào.** Chặn di chuyển, có HP, không bị nhắm.
- [ ] **Công trình: tháp bắn.** Tự tìm mục tiêu trong tầm và bắn. `Projectile`/`ShapeAttack` +
      cách chọn mục tiêu của `AIContext` là chỗ để mượn.
- [ ] **Công trình: bẫy.** Sát thương khi quái chạm/vào vùng.
- [ ] **Tách "đáng săn" khỏi số phe.** Thêm `IsHuntable` vào `IDamageable`, mặc định
      `Teams.IsPrey(Team)` để không đổi hành vi cũ; `FindHostile` đọc cờ thay vì đọc phe. Tường và
      đuốc trả `false`. Cập nhật comment đầu `Teams.cs` — nó đang nói phe trả lời được câu hỏi thứ
      hai, và sau việc này thì không còn đúng.
- [ ] **Công trình gãy thì xây lại được.** Điểm build trở về trạng thái rỗng, tiến độ đã nạp mất.

### UI

- [ ] **HUD run**: ngày thứ mấy / X, đồng hồ trong ngày, thanh EXP + level, tài nguyên đang mang.
- [ ] **Popup chọn upgrade** khi level-up.
- [ ] **Màn kết run**: clear hay chết, sống được mấy ngày, thưởng nhận được.
- [ ] **Điểm build**: hiện yêu cầu tài nguyên và tiến độ ngay tại chỗ (`PayGate` đã có `preview` +
      `Changed`, cần view).

### Dọn

- [x] **Hunger khoá vào trong run.** ✅ Không xoá — nó là áp lực đúng chất survivors: bắt rời chỗ
      an toàn đi kiếm ăn. Nhưng ngoài overworld thì vô nghĩa (không combat, không đồ ăn, không chết
      được), nên `ArenaRunner` bật dạ dày lúc vào run và tắt lúc ra. `Hunger.BeginRun` đặt **đầy**,
      không phải một phân số author sẵn: một run phải bắt đầu ở cùng một chỗ mỗi lần, và "đầy" là
      điểm xuất phát duy nhất còn đúng khi upgrade làm dạ dày to ra. `startFullness` vì thế mất chỗ
      dùng cuối cùng → xoá khỏi config. HUD ẩn thanh khi không trong run.
- [x] **Nguồn thức ăn trong arena.** ✅ Đã có sẵn: quái chết → `DropOnDeath` → `Meat.prefab` →
      `FoodPayload` → `Picker` → `Hunger.Eat`. Ngoài overworld dạ dày đang đầy nên `CanDeliver` trả
      false, miếng thịt nằm yên thay vì bị nuốt vô ích — không phải thêm luật nào.
- [ ] **Cờ tắt hunger cho từng arena.** Một số arena sẽ không có chỉ số đói. Một bool trên
      `ArenaConfig` mà `ArenaRunner` đọc thay vì luôn gọi `BeginRun`. Chưa dựng — chưa cần.
- [ ] **Luật hiện/ẩn HUD theo overworld vs arena.** Thanh máu chỉ hiện trong arena; nhiều luật UI
      khác nữa. Gom lại làm một lượt.

### Doc

- [ ] **Viết lại `DESIGN.md`.** Trụ cột, core loop, nguồn phần thưởng, luật chống-cày đều đổi nghĩa.
      Không vá — viết lại mục nào sai.
- [ ] **Ghi `DECISIONS.md`**: vì sao EXP-per-kill trong run không phá luật chống-cày; vì sao tài
      nguyên không rơi từ quái; vì sao giữ skill; vì sao công trình không dùng team 0; vì sao hunger
      chỉ sống trong arena.
- [ ] **Cập nhật `SPAWN.md`** theo số phận của `SpawnZone`.
