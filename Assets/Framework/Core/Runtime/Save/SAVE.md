# Core.Save

Per-key local save. An `ISavable` declares a `SaveKey`; its data lives in `{SaveKey}.json`
under `Application.persistentDataPath`, in a section named after its type. Savables that share
a `SaveKey` are written into the same file, each under its own section.

## Usage

```csharp
public sealed class Settings : ISavable
{
    public string SaveKey => "cold";
    float _volume = 1f;

    public void Save(SaveBag bag) => bag.Set("volume", _volume);
    public void Load(SaveBag bag) => _volume = bag.Get("volume", 1f);
}

saveService.Register(settings);   // loads current values
saveService.Save("cold");         // flush just this file
saveService.SaveAll();            // flush everything
```

## DI

```csharp
builder.Register<SaveService>(Lifetime.Singleton);
```

Call `SaveService.SaveAll()` on `OnApplicationPause(true)` / quit. `Dispose()` also flushes.

## Tiers (hot / warm / cold)

`SaveKey` doubles as the tier bucket: flush hot often, cold rarely. Separate files mean a hot
write can never corrupt the precious cold file.

## Other formats (later)

`ISaveSerializer` is the format seam — a binary/encrypted serializer can replace
`JsonSaveSerializer` without touching the store or savables. Caveat: `SaveBag` carries
Newtonsoft attributes, so a fully format-agnostic bag would drop those. Not needed yet.

## Files

- `ISavable` — persists itself (`SaveKey` + `Save`/`Load(SaveBag)`).
- `SaveBag` — nested string→object bag, one per file.
- `ISaveSerializer` / `JsonSaveSerializer` — format seam (JSON via Newtonsoft).
- `SaveFileStore` — one file; atomic write (tmp → move, `.bak` recovery), never throws on load.
- `SaveService` — registers savables, groups by `SaveKey`, save/clear per key or all.
