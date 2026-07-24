# Spawn zones — thiết kế (chưa triển khai)

Vùng đặt trên map, tự đẻ quái và tự giữ số lượng. Đây là bản thiết kế; code chưa động.

## Quyết định đã chốt

- **Setup nằm thẳng trên zone**, không có `SpawnZoneConfig` riêng. Một `SpawnZone` (MonoBehaviour trên map)
  ôm cả hình học lẫn luật. Chuyển sang SO sau nếu cần tái dùng — chưa cần.
- **Không có trần tổng toàn map.** Mỗi zone tự cap ở `capacity` của nó. Nhiều map nên một map không lo quá tải;
  tổng quái một map = tổng capacity các zone, do người thiết kế nắm.
- **Quái được rời vùng.** Sau khi spawn, di chuyển do **AI của quái** quyết — sống ở vùng, bỏ đi, quay lại đều
  là chuyện của quái. **Zone KHÔNG kéo quái về, không quản lý vị trí.** Zone chỉ *đẻ* và *đếm*.
- **Load map = reset.** Không lưu trạng thái spawn vào save. Mỗi lần vào map, zone khởi tạo lại từ đầu.

## Zone làm đúng hai việc

1. **Đẻ** quái tại một ô hợp lệ trong vùng, tới khi đủ `capacity`.
2. **Đếm** số con nó đã đẻ mà còn sống, để biết khi nào phải đẻ bù.

Ngoài hai việc đó, quái tự lo. Đây là ranh giới quan trọng — nó khiến zone đơn giản và không giẫm lên AI.

## Đếm theo QUYỀN SỞ HỮU, không theo không gian

Không query `CombatWorld` trong bán kính để đếm — vì quái được rời vùng, query sẽ **hụt con của mình đã đi xa**
và **cộng nhầm con vùng khác lạc vào**.

→ Zone giữ danh sách con nó đẻ ra, mỗi con đăng ký `Damageable.Died` để tự trừ khi chết. Chính xác tuyệt đối,
rẻ hơn query, và không quan tâm con đó đang đứng đâu — đúng với "quái được tự do rời vùng".

## Xác định khu vực: hình đơn giản → bake danh sách ô

**Không random-rồi-thử-lại lúc chạy** (vùng nằm phần lớn trên nước sẽ quay vòng vô tận, lỗi chỉ nổ trên map xấu).

Thay bằng **author hình đơn giản → bake ra danh sách ô hợp lệ**:

1. Đặt `SpawnZone` với hình **circle/box**, kéo thả, có gizmo.
2. **Bake**: giao hình với **ô walkable** (đã có từ `TerrainKind.Land` + `WalkBake`), loại ô sát tường
   (clearance), lưu **danh sách cell** vào zone (serialized, ẩn).
3. Runtime: random một index trong list → **O(1), luôn hợp lệ**.

Cái được: **"vùng không có ô hợp lệ" thành lỗi lúc bake, thấy ngay trên editor** — không phải bug lúc chơi.
Khớp văn hoá sẵn có (bake terrain mesh, bake walkable, bake registry).

**`SpawnArea` để là abstraction** (`SampleCell()` / `Contains(cell)`) — circle/box làm trước, **painted-mask
(vẽ vùng theo tile) nhét vào sau mà không sửa logic spawn**. Đây là chỗ duy nhất đáng trừu tượng hoá sớm, vì
cách vẽ vùng gần như chắc chắn sẽ đổi. Painted-mask để **sau**: nó thắng khi vùng ôm sát biome ngoằn ngoèo,
nhưng tốn thêm grid mask song song + palette chọn zone-id + bước bake — chưa đáng cho vài ổ quái tròn.

## Nạp lần đầu: warm / cold (per-zone)

- **Warm**: lúc load map fill thẳng tới `capacity` — thế giới có sẵn sự sống khi tới nơi.
- **Cold**: bắt đầu rỗng, nhỏ giọt theo `period` — hợp vùng gần điểm bắt đầu (đỡ ngợp) hoặc vùng "mới mở".

## Đẻ bù khi thiếu: nợ theo mạng + khoá khi dọn sạch

- **Nợ theo mạng**: mỗi con chết ghi một "nợ" kèm `respawnDelay`. Trả nợ nhỏ giọt → dọn 5 con mất đúng 5 lượt
  hồi, không mọc lại tức thì (nhỏ giọt thuần khiến giết mất ý nghĩa; theo đợt thì giả như wave).
- **Khoá khi wipe**: dọn sạch cả vùng → khoá lâu hơn hẳn rồi mới hồi (có thể hồi **warm**). Đây là chỗ **thưởng
  cho việc dọn vùng**.

## Điều kiện chặn spawn (thứ quyết định game có "thật")

Trả nợ chỉ diễn ra khi ô đẻ được chọn thoả:

- **Cách player tối thiểu `minPlayerDist`** — không đẻ ngay trước mặt.
- **Ngoài tầm camera** — tránh pop-in. Nợ cứ tích, **trả khi player đi khỏi rồi quay lại**.
- **Zone quá xa player thì không tick** — tiết kiệm CPU (không cap tổng, nên cái này là van CPU chính).

Ba luật này áp cho **điểm sắp đẻ**, không phải quái đang sống — quái đã ra đời thì đi đâu là việc của nó.

## Hình dạng dữ liệu (dự kiến)

`SpawnZone` (MonoBehaviour trên map) serialize:

- **Hình học**: shape (Circle/Box) + kích thước → bake ra `cells` (ẩn).
- **Quái**: list `{ id, weight }` — một loại là list một phần tử; trộn thì đặt trọng số. Chốt.
- **Số lượng**: `capacity`.
- **Nhịp**: `respawnDelay` (nợ/mạng), `wipeLockDuration` (khoá khi dọn sạch), `warm` (bool).
- **Chặn**: `minPlayerDist`, cờ off-camera, `activeRadius` (xa hơn thì ngừng tick).

## Quái trong zone: bảng trọng số (chốt)

Zone giữ list `{ id, weight }`; mỗi lần đẻ, roll theo trọng số. Một loại thì list một phần tử (weight bất kỳ),
không tốn gì thêm. Trộn "60% sói / 40% gấu" cho sẵn nên không phải migrate khi cần. `id` là khoá spawn quái
(khớp cách load prefab quái — xem tiên quyết).

## Thứ tự triển khai

Chia theo cái gì **bị chặn** và cái gì **làm được ngay**:

1. **`SpawnArea` + author + bake** — *làm được ngay*, chỉ phụ thuộc terrain/walkable đã có:
   - `SpawnArea` abstraction (`SampleCell()` / `Contains(cell)`), impl Circle + Box.
   - Gizmo vẽ vùng + kích thước, kéo thả trên map.
   - Bake: giao hình ∩ walkable (trừ clearance) → `cells`; **báo lỗi editor nếu 0 ô**.
   - Nút bake ở inspector + auto-bake khi cần (giống terrain/walkable).
2. **`SpawnZone` state machine** — *chờ enemy runtime + cách load prefab quái*:
   - Warm/cold init lúc load map.
   - Ownership: đăng ký `Damageable.Died`, list con còn sống.
   - Hàng nợ theo mạng + `respawnDelay`; khoá `wipeLockDuration` khi dọn sạch.
   - Gating: `minPlayerDist`, off-camera, `activeRadius` (ngừng tick khi xa).
   - Roll `{id, weight}` → load prefab qua cơ chế đã chốt → đặt tại `SampleCell()`.
3. **Nối pool + prefab loader** — dùng lại đúng đường enemy runtime đã dựng, không đẻ đường riêng.

→ Bắt đầu được **bước 1 ngay bây giờ** mà không chờ gì. Bước 2–3 mở khoá sau khi có enemy chạy được.

## Điều kiện tiên quyết (phải xong trước khi code zone)

- **Enemy runtime** (di chuyển/tấn công/AI) — chưa có. Zone đẻ ra vỏ rỗng thì không test được gì. Đồng thời AI
  chính là thứ quyết định "sống ở vùng / rời / quay lại" mà zone đã nhường.
- **Pool reset**: `Damageable._hp` set trong `Awake`, mà `Awake` không chạy lại khi lấy từ pool → con tái dùng
  sống dậy với HP ≤ 0. Cần `Init/Bind` reset trước khi pool quái (TODO cũ đã ghi).
- **Cách load prefab quái**: kế hoạch `Resources` + convention (xem TODO). Spawn zone là **khách hàng đầu tiên**.

## Không làm (chốt phạm vi)

- Không tether/kéo quái về vùng — AI lo.
- Không trần tổng toàn map — zone tự cap.
- Không lưu save — reset theo map.
- Không painted-mask ở bản đầu — circle/box trước, `SpawnArea` để ngỏ đường thêm sau.
