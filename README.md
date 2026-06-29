# KidzDev Unity Popup

Modal dialogs for Unity you **await for a typed result**. Show a confirm dialog, `await` the `bool`; stack a
warning over it; mix popups loaded from **Resources**, an **inspector prefab**, or **Addressables** on the same
layer. UniTask-only, Addressables stays optional, zero singleton coupling.

```csharp
bool ok = await PopupService.Default.ShowAsync<bool>(PopupRef.Resources("Popups/Confirm"), "Delete this mail?");
if (ok) Delete();
```

## Why a separate package from the navigator

The screen-navigator handles **navigation history** (a back stack). A popup is a different concern: it's
**modal**, sits above whatever is showing, **returns a result you await**, and **stacks** (a warning over a
confirm) without touching the navigation history. So it ships standalone — it works on its own or alongside the
navigator.

## Install

Add to `Packages/manifest.json` (UniTask comes from the OpenUPM scoped registry):

```json
{
  "scopedRegistries": [
    { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": ["com.cysharp.unitask"] }
  ],
  "dependencies": {
    "com.kidzdev.unity.popup": "https://github.com/knabsiraphop/kidzdev-unity-popup.git#v1.0.0"
  }
}
```

Requires Unity `6000.0`+. Dependencies: `com.cysharp.unitask`, `com.unity.ugui`. Addressables is **not** required —
the Addressables loader ships in a sample.

## Core model

A popup is any prefab whose root has an `IPopup`. The manager loads it, instantiates a fresh instance on a
high-sort-order canvas layer behind a dimming backdrop, runs an enter transition, **awaits the result**, then
runs the exit transition and destroys the instance.

| Type | Role |
| --- | --- |
| `IPopup` / `Popup` | The view contract / convenient `MonoBehaviour` base. `Close(result)` from a button. |
| `PopupResult` | `Confirmed` / `Cancelled` / `Dismissed` — the common button outcomes. |
| `PopupRef` + `PopupSource` | *Which* popup and *where it loads from* (`Resources` / `Direct` / `Addressables`). |
| `IPopupLoader` | Loads a `PopupRef` to a prefab. `CompositePopupLoader` routes by source. |
| `IPopupService` / `PopupService` / `PopupManager` | The seam, static facade, and default impl. |
| `IPopupTransition` | `Instant` (default) / `Fade` / `Scale` — no third-party animation dependency. |
| `PopupOptions` | Backdrop color, dismiss-on-backdrop, dismiss-on-back, transition override, sort order. |

## Ready-made popups

- `ConfirmPopup : Popup` → `bool` (wire Yes→`Confirm()`, No→`Cancel()`).
- `AlertPopup : Popup` → `PopupResult` (wire OK→`Ok()`).

The system accepts **any** prefab with an `IPopup`, so your project-specific dialogs work the same way.

## Split load by situation

The point of `PopupRef` + `PopupSource` + `CompositePopupLoader`: **one app mixes loaders, chosen per popup.**

| Situation | Source | Why |
| --- | --- | --- |
| Always present, tiny (Confirm, Alert) | `Resources` | zero setup, instant, shipped in the build |
| Pre-wired in a scene/prefab | `Direct` | no IO at all — inspector-assigned `GameObject` |
| Large / remote / seasonal | `Addressables` | downloaded on demand, patchable without an app update |

**Declared once in a registry** (call sites stay clean):

```csharp
var registry = new PopupRegistry()
    .Map(PopupId.Confirm,       PopupRef.Resources("Popups/Confirm"))
    .Map(PopupId.QuickToastYN,  PopupRef.Direct(quickPrefab))
    .Map(PopupId.SeasonalEvent, PopupRef.Addressables("event_popup"));
PopupService.Default = new PopupManager(registry: registry);

bool ok = await PopupService.Default.ShowAsync<bool>(PopupId.Confirm);
```

**Or per-call** — the same logical popup can switch source by build flavor / remote config:

```csharp
var rumorRef = useRemote ? PopupRef.Addressables("rumor_popup") : PopupRef.Resources("Popups/Rumor");
await PopupService.Default.ShowAsync<PopupResult>(rumorRef);
```

A project that never uses Addressables wires only Resources + Direct and pulls in **zero** extra dependency:

```csharp
var loader = new CompositePopupLoader()
    .With(PopupSource.Resources, new ResourcesPopupLoader())
    .With(PopupSource.Direct,    new DirectPopupLoader());
    // .With(PopupSource.Addressables, new AddressablesPopupLoader());  // only if used (Samples~)
PopupService.Default = new PopupManager(loader);
```

## Stacking, Back, and teardown

- Popups **stack** (reentrant): `ShowAsync` a warning while a confirm is still awaited; the newest is interactive
  and dims the ones beneath it.
- `TryHandleBack()` dismisses the top popup (when its options allow) and returns `true` if a popup was open — call
  it from your Android-back / Esc handler **before** the navigator's back handling so Back closes a popup first.
- `CloseAll()` dismisses every open popup; `Dispose()` cancels in-flight shows and tears down the layer.

## Writing a custom popup

```csharp
public sealed class RewardPopup : Popup
{
    [SerializeField] private Text _amountLabel;

    public override void OnOpened(object arg)
    {
        if (arg is int amount) _amountLabel.text = $"+{amount}";
    }

    public void Claim()  => Close(true);   // wire to the Claim button
    public void Skip()   => Close(false);  // wire to the Skip button
    protected override object DismissResult => false; // Back / backdrop = skipped
}
```

```csharp
bool claimed = await PopupService.Default.ShowAsync<bool>(PopupRef.Resources("Popups/Reward"), arg: 100);
```

## Samples

- **Demo** — a scene wiring confirm/alert dialogs, a stacked warning over a confirm, and the same layer mixing
  Resources- and Direct-loaded popups.
- **Addressables Loader** — `AddressablesPopupLoader` (import only if you load popups via Addressables).

## License

MIT — see [LICENSE.md](LICENSE.md).
