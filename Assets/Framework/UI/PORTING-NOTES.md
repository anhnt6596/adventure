# NPU Framework UI — Ported to Adventure (decoupled)

Bê từ `NpuCore/npu-unity/npu/framework-ui`, đã gỡ toàn bộ coupling nội bộ NPU.

## Đã thay / gỡ

| Gốc (NPU) | Thay bằng |
|---|---|
| `Npu.IoC.IContainer` | `VContainer.IObjectResolver` |
| `Npu.Core.IInjectable` | `Npu.UI.IInjectable` (nhận `IObjectResolver`) |
| `Npu.Core.IMessageService` + messages | `Npu.UI.IEventBus` / `EventBus` + `UIShownEvent`/`UIHiddenEvent`/`PopupAppearCompletedEvent` |
| `Npu.Core.IViewSettingService` | bỏ (chỉ ResourceUI dùng) |
| `Npu.Core.BaseService` (PopupQueue) | class thường + `IInjectable`/`IDisposable` |
| `Npu.View` ResourceUI (currency) | xóa |
| `Input` cũ | Unity Input System (`Mouse.current`/`Keyboard.current`) |

Localization (`L`, `LocalizedLabel`, `DynamicLabel`) giữ nguyên — vốn chạy trên
package chính chủ `com.unity.localization`, không dính NPU.

## Package yêu cầu

- `jp.hadashikick.vcontainer` (đã thêm vào manifest)
- `com.cysharp.unitask` (đã thêm vào manifest)
- `com.unity.inputsystem` (đã có sẵn)
- **`com.unity.localization`** — BẮT BUỘC cài để assembly compile:
  Window → Package Manager → Unity Registry → "Localization" → Install.

## Cách wiring (VContainer)

Xem `Assets/Script/UIRootLifetimeScope.cs`.

1. Trong Scene tạo GameObject "UISystem": thêm `UIDocument` + `UISystem`, gán
   `UIRegistry` asset và Panel Settings cho UIDocument.
2. Tạo GameObject khác, thêm `UIRootLifetimeScope`, kéo UISystem vào field.
3. Play → `Show<TView>()` / `Hide<TView>()` qua `IUISystem`.

## Dùng EventBus

```csharp
_eventBus.Subscribe<UIShownEvent>(e => Debug.Log(e.UI));
_eventBus.Publish(new UIHiddenEvent(view));
_eventBus.Unsubscribe<UIShownEvent>(handler);
```
