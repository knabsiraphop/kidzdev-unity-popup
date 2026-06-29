# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
