# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
