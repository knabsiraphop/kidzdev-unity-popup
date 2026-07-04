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
    "com.kidzdev.unity.popup": "https://github.com/knabsiraphop/kidzdev-unity-popup.git#v1.0.2"
  }
}
```

Requires Unity `6000.0`+. Dependencies: `com.cysharp.unitask`, `com.unity.ugui`. Addressables is **not** required —
the Addressables loader ships in a sample.

## Core model

A popup is any prefab whose root has an `IPopup`. The manager loads it, instantiates a fresh instance on a
high-sort-order canvas layer behind a dimming backdrop, runs an enter transition, **awaits the result**, then
runs the exit transition and destroys the instance.

The default layer is a `ScreenSpaceOverlay` canvas at constant pixel size (`sortOrder` constructor parameter).
To control scaling, pass a `layerFactory` to the `PopupManager` constructor and return your own configured
canvas (e.g. a `CanvasScaler` with a reference resolution) — the manager owns it and destroys it on `Dispose`.

| Type | Role |
| --- | --- |
| `IPopup` / `Popup` | The view contract / convenient `MonoBehaviour` base. `Close(result)` from a button. |
| `PopupResult` | `Confirmed` / `Cancelled` / `Dismissed` — the common button outcomes. |
| `PopupRef` + `PopupSource` | *Which* popup and *where it loads from* (`Resources` / `Direct` / `Addressables`). |
| `IPopupLoader` | Loads a `PopupRef` to a prefab. `CompositePopupLoader` routes by source. |
| `IPopupService` / `PopupService` / `PopupManager` | The seam, static facade, and default impl. |
| `IPopupTransition` | `Instant` (default) / `Fade` / `Scale` — no third-party animation dependency. |
| `PopupOptions` | Backdrop color, dismiss-on-backdrop, dismiss-on-back, transition override, `AutoDismissAfter`. |
| `IPopupStack` / `PopupStack` / `PopupStackController` | Capped, queued group of *concurrently*-visible popups (a reward stack) — see [below](#reward-stacks-several-popups-visible-at-once). |
| `IStackLayout` | The reward stack's arrangement/interactivity policy — `DeckStackLayout` (peeking) or `GroupStackLayout` (your own `LayoutGroup`). |

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

This is a modal **LIFO** — only the newest popup is ever interactive. For several popups visible and interactive
**at once** (a reward stack), see [`PopupStack`](#reward-stacks-several-popups-visible-at-once) below.

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

## Timed popups (auto-dismiss)

`PopupOptions.AutoDismissAfter` closes a popup on its own after N seconds — the countdown starts once the enter
transition finishes, is cancelled if the popup closes first, and routes through `IPopup.TryDismiss` (a popup that
vetoes dismissal is not auto-closed). It uses `ignoreTimeScale: true` internally, so a timed popup still closes
while the game is paused.

```csharp
var result = await PopupService.Default.ShowAsync<PopupResult>(
    PopupRef.Resources("Popups/Announcement"),
    new AnnouncementPopup.Content("Daily bonus!", "+50 coins", seconds: 3f),
    new PopupOptions
    {
        AutoDismissAfter = 3f,
        DismissOnBackdropClick = true,           // let an impatient player tap it away early
        BackdropColor = new Color(0f, 0f, 0f, 0.3f),
    });

// PopupResult.Confirmed  → the player tapped it
// PopupResult.Dismissed  → it timed out (or the player tapped the backdrop)
```

Because popups stack, two timed announcements with different durations can be shown concurrently and each closes
independently on its own schedule:

```csharp
var lower = PopupService.Default.ShowAsync<PopupResult>(
    PopupRef.Resources("Popups/Announcement"),
    new AnnouncementPopup.Content("Event ends soon", "Closes in 6s", 6f),
    new PopupOptions { AutoDismissAfter = 6f });

var upper = PopupService.Default.ShowAsync<PopupResult>(
    PopupRef.Resources("Popups/Announcement"),
    new AnnouncementPopup.Content("On top", "Closes in 3s", 3f),
    new PopupOptions { AutoDismissAfter = 3f });

await UniTask.WhenAll(lower, upper); // both auto-close, the shorter one first
```

## Reward stacks (several popups visible at once)

`IPopupStack` (facade: `PopupStack`) is a different shape from `IPopupService`: a **capped, queued group** of
concurrently-visible, concurrently-interactive cards — "you got 5 rewards" — instead of a single top-of-LIFO
modal. Pick a layout, assign the facade once, then `Enqueue` each card; cards past the layout's `maxVisible` are
queued and not even loaded until a slot frees, so a queued card's `OnOpened` never runs early.

### Deck — cards peek behind each other, one at a time

```csharp
PopupStack.Default = new PopupStackController(new DeckStackLayout(maxVisible: 3));

var pending = new UniTask<PopupResult>[5];
for (int i = 0; i < pending.Length; i++)
    pending[i] = PopupStack.Default.Enqueue<PopupResult>(PopupRef.Resources("Popups/RewardCard"), $"Reward #{i + 1}");

await UniTask.WhenAll(pending); // 3 peek at once; collecting the front card promotes the next queued one
```

### Group — every card independently interactive in your own layout

Swap `DeckStackLayout` for `GroupStackLayout(myLayoutGroup)` to lay cards out in a caller-owned `LayoutGroup`
(e.g. a horizontal row or a scroll view's content) instead of a peeking deck — every visible card accepts input
at once, not just the frontmost:

```csharp
PopupStack.Default = new PopupStackController(new GroupStackLayout(rewardRowLayoutGroup, maxVisible: 3));

foreach (var reward in rewards)
    await PopupStack.Default.Enqueue<PopupResult>(PopupRef.Resources("Popups/RewardCard"), reward);
```

### One-at-a-time, timed notification queue

`maxVisible: 1` on `DeckStackLayout` turns it into a sequential notification queue — nothing to peek behind since
only one card ever exists. Combine with `autoDismissAfter` so each reward shows, counts down, and — collected or
not — the next one takes its place automatically:

```csharp
var notifications = new PopupStackController(new DeckStackLayout(maxVisible: 1));

const float seconds = 4f;
int collected = 0;
for (int i = 0; i < rewards.Count; i++)
{
    var result = await notifications.Enqueue<PopupResult>(
        PopupRef.Resources("Popups/RewardCard"),
        new RewardCardPopup.Content($"Reward #{i + 1}", seconds),
        autoDismissAfter: seconds);
    if (result == PopupResult.Confirmed) collected++;
}
```

A card's own countdown label is purely cosmetic — the real deadline is always enforced by `autoDismissAfter`, the
same render/enforce split `AutoDismissAfter` uses on `PopupManager`.

## Samples

- **Demo** — a scene wiring confirm/alert dialogs, a stacked warning over a confirm, timed announcements (single
  and stacked), and a reward stack shown three ways: `DeckStackLayout` (3 peeking), `GroupStackLayout` (a list),
  and a one-at-a-time timed notification queue.
- **Addressables Loader** — `AddressablesPopupLoader` (import only if you load popups via Addressables).

## License

MIT — see [LICENSE.md](LICENSE.md).
