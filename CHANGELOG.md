# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-07-04

### Added
- `IPopupStack` / `PopupStackController` — a capped, queued group of concurrently-visible popups (e.g. "you got 5 rewards"), distinct from `PopupManager`'s modal LIFO where only the top popup is ever interactive. Admission is queued past `IStackLayout.MaxVisible`: a card is not loaded or instantiated (and its `OnOpened` never runs) until a slot frees.
- `IStackLayout` swaps a stack's arrangement/interactivity/capacity policy without touching card content or the controller's queueing logic. Two ready-made layouts: `DeckStackLayout` (cards peek behind each other with a depth offset/scale, only the frontmost is interactive, slides forward on collect — no third-party animation dependency) and `GroupStackLayout` (delegates positioning to a caller-supplied `LayoutGroup`; every visible card is independently interactive).
- `PopupStack` — a static facade over `IPopupStack` mirroring `PopupService`'s pattern; `Default` must be assigned (there's no ready-made default layout choice).
- Auto-dismiss timers: `PopupOptions.AutoDismissAfter` (seconds after which a `PopupManager` popup auto-closes as if dismissed) and `IPopupStack.Enqueue`'s `autoDismissAfter` parameter. Both route through `IPopup.TryDismiss`, so a vetoing popup/card is not auto-closed, and both default to `UniTask.Delay` with `ignoreTimeScale: true` so a timed popup still closes while the game is paused. `PopupManager` and `PopupStackController` each accept an optional `autoDismissTimer` constructor parameter to substitute scaled, server-driven, or test-controlled timing.
- Demo sample additions: `AnnouncementPopup` and `RewardCardPopup` prefabs/scripts showing a reward-stack flow (`DemoAnnouncementPopup`, `DemoRewardCard`) driven from `PopupDemoController`.

### Fixed
- A shared stack backdrop is now raised in the same canvas the cards render in (via `IStackLayout.BackdropAnchor`). Previously a layout that relocates cards into an externally-owned canvas (e.g. `GroupStackLayout`) left the backdrop in the stack's own overlay layer; whichever canvas sorted higher won every raycast, so the full-screen backdrop could silently swallow clicks meant for the cards.
- `Selectable.interactable` (Button, Toggle, ...) is now set directly on reflow, alongside the `CanvasGroup` toggle. `UnityEngine.UI.Selectable` caches whether its ancestor `CanvasGroup`s allow interaction and only re-evaluates that cache on specific lifecycle events, not on a bare `CanvasGroup.interactable` assignment — without this fix, a card reparented by a layout (e.g. `GroupStackLayout`) could end up with a `Button` that silently refused clicks even though its ancestor `CanvasGroup` correctly read `interactable = true`.

## [1.0.1] - 2026-07-03

### Added
- `PopupManager` constructor accepts an optional `layerFactory` to supply a custom popup layer (e.g. a canvas with a configured `CanvasScaler`); the default layer scales at constant pixel size. The manager owns the returned object and destroys it on `Dispose`.
- EditMode coverage for the dismiss-veto path (`TryHandleBack` / `CloseAll` against a popup whose `TryDismiss` returns `false`).

### Changed
- A popup completing with a value that doesn't match the awaited `TResult` now throws an `InvalidCastException` naming the `PopupRef`, the delivered type, and the awaited type (was a bare cast failure; `null` into a value-type `TResult` threw `NullReferenceException`). Teardown behavior is unchanged.

### Fixed
- README no longer claims `PopupOptions` carries the overlay sort order (it is a `PopupManager` constructor parameter).

## [1.0.0] - 2026-06-29

### Added
- Initial release: modal dialog service for Unity.
- `PopupService.Default` facade over `IPopupService` / `PopupManager` — `ShowAsync<TResult>(...)` you await for a typed result.
- Reentrant stacking — show a warning over a confirm without touching the navigation history.
- Per-popup loader split via `PopupRef` + `PopupSource` routed through `CompositePopupLoader`: `ResourcesPopupLoader` and `DirectPopupLoader` ship in core; `AddressablesPopupLoader` lives in `Samples~` so Addressables stays an optional dependency.
- Optional `PopupRegistry` mapping a stable id to a `PopupRef`.
- Transitions with no third-party animation dependency: `InstantPopupTransition` (default), `FadePopupTransition`, `ScalePopupTransition`.
- `PopupBackdrop` (dim + raycast block + optional tap-to-dismiss) and `PopupOptions` (backdrop color, dismiss-on-backdrop, dismiss-on-back, transition override, sort order).
- Ready-made `ConfirmPopup` (→ `bool`) and `AlertPopup` (→ `PopupResult`); the system accepts any prefab whose root has an `IPopup`.
- `TryHandleBack()`, `CloseAll()`, and `Dispose()` for back-button / teardown handling.
- EditMode and PlayMode test suites; a Demo sample scene.
