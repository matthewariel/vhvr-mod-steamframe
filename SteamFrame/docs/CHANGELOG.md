# Changelog

What changed in each version of Steam Frame for VHVR. The full feature list, compared with plain VHVR, is in
[FEATURES.md](FEATURES.md). Headset testing status is in [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md).

## 0.3.1 (2026-11-10): built, not yet tested in the headset

### Fixed
- **Big map hints:** three labels were wrong, found from the first 0.3.0 play session's log.
  - Zoom showed LT / RT; it now shows D-pad down / up (out / in).
  - Remove pin and Cross off pin were swapped; Remove pin now shows the bumper and Cross off pin the trigger.

## 0.3.0 (2026-11-10): installed, not yet tested in the headset

**No gameplay changes.** Every 0.2.0 feature, setting name and default is the same. This version is about where the code
lives and how it's organised.

### Changed
- **Moved into the GitHub fork** `matthewariel/vhvr-mod-steamframe`, as its own `SteamFrame/` folder. VHVR's files are
  untouched, so the fork syncs with VHVR without conflicts. Players install official VHVR plus this plugin's DLL.
- **Code split by who it's for:** All headsets / Steam Frame / Extras. All-headset code no longer names the Steam Frame;
  it asks the active controller profile. The Steam Frame is the only profile so far, and all-headset features are
  tested on Steam Frame only.
- **Settings tab grouped by scope:** every section header has an **ALL HEADSETS**, **STEAM FRAME** or **EXTRA** badge,
  with a legend under the status line. All-headset settings are on the left, Frame bindings and detection on the right,
  extras below them. The button guide is titled after the controller in use.
- **Startup log:** shows the VHVR version (with a warning when it isn't a tested one) and a `Modules:` line grouped by
  scope.

### Added
- **"Share map position on join" row** on the settings tab. The setting (`[Map] SharePositionOnJoin`) already existed;
  it just had no row.
- **Unit tests** for all the pure rules (77 tests), run automatically on GitHub without game files.
- **VHVR compatibility checker** (`scripts\Check-VhvrCompat.ps1`): checks an installed VHVR and Valheim against
  everything the plugin reaches into, without starting the game.
- **Scripts** to build, test, install (with a backup of the previous DLL) and package the plugin.
- **Docs:** [ARCHITECTURE.md](ARCHITECTURE.md), [MAINTAINING.md](MAINTAINING.md), [UPSTREAM-NOTES.md](UPSTREAM-NOTES.md).

## 0.2.0 (2026-11-09): installed, not yet tested in the headset

### Added
- **Crouch guard:** the crouch button is ignored while a chest, inventory, store, the big map, a text box or the pause
  menu is open. The Frame's right stick click is also VHVR's "middle click" there, so it used to crouch you on every
  click.
- **Double-click to crouch** (optional, off by default): going down needs two quick clicks; standing up is one.
- **Big map hints** show the Frame controls that work with VHVR's laser:
  - zoom: left D-pad up/down;
  - ping: right stick click;
  - add pin: grip + trigger;
  - remove pin: bumper.

  This replaces Valheim's Xbox map controls. The first map open each session logs the exact hint keys.
- **Saved server passwords:** remembered after a successful join (Windows DPAPI-encrypted) and filled in next time.
  A rejected saved password is forgotten, and there's a "Forget saved passwords" option.
- **Seated eye height:** learned from seated recenters, with sliders for both standing and seated heights. With both
  known, the seated/standing split is halfway between them instead of a fixed 80%.
- **Steam Frame tab pages:** Settings, Button guide and Recenter & height. The guide page adds the big map and chest
  controls, and a pointer to VHVR's wrist minimap angle setting.
- **Recenter pose picture** with instructions, plus the last height measurement and the current seated/standing split.

### Changed
- Tab renamed from "Frame" to "Steam Frame", on two lines like "Motion Control".
- More settings on the tab: crouch, server passwords, prompt style, map hints and Frame detection.
- The crouch prompt shows "2x" when double-click crouch is on.

### Fixed
- The "thumb-on-stick can't be sensed" message no longer appears at startup. SteamVR reports no binding for a moment
  while it starts; the message now waits until thumb sensing has been missing for 30 s.

## 0.1.0 (2026-11-08): first combined plugin

Replaced the earlier local "VR Gamepad Prompts" plugin and hand edits to the Frame binding, with every feature split into
its own module. It imports settings from VR Gamepad Prompts once.

### Added
- **Steam Frame detection** through SteamVR.
- **Frame button prompts:** gamepad glyphs in prompts and menu hints, labelled with the Frame button VHVR uses. This
  carries over from VR Gamepad Prompts.
- **Chest buttons:** right / left stick click = Place stacks / Take all. Carried over.
- **Map position sharing** turned on at each join. Carried over.
- **Punch checkboxes:** require grip / trigger / thumb on stick, with a looseness warning. One rule for creatures and
  objects; diagnostics option.
- **Seated / standing:** Auto mode keeps your standing height when you recenter seated; it learns the standing height.
  VHVR's `PlayerMinEyeHeight` workaround is no longer needed.
- **Steam button recenter** also runs VHVR's recenter and height measurement.
- **Frame binding tweaks** applied by the plugin from VHVR's untouched copy, each with its own switch. They take effect
  after a restart:
  - left stick click = sprint;
  - right stick click = crouch;
  - no right-stick flick sprint/crouch;
  - thumb touch sensing.
- **Frame tab** in VHVR's settings with these settings and a live button guide.

### Fixed
- **Build-piece tilt:** grip + right stick up/down (face piece away/toward) works again. The earlier hand edit that
  removed stick-flick sprint/crouch had also deleted the up/down directions those chords need.

## Before 0.1.0: VR Gamepad Prompts (local only)
- A local plugin that switched Valheim's prompts to gamepad glyphs under VR, added the chest stick-click buttons and
  public map position, plus early punch and height experiments.
- The Frame binding was edited by hand. VHVR's original is kept as the base copy.
