# Steam Frame for VHVR

A BepInEx companion plugin for [VHVR](https://github.com/brandonmousseau/vhvr-mod) (Valheim VR) that makes the
Steam Frame controllers feel right: Frame button prompts, configurable bare-hand punching, seated/standing play,
safer Frame bindings, and a **Steam Frame** tab in VHVR's settings with a button guide and the recenter pose.

Built against VHVR 0.11.1 (signatures checked against VHVR master, Nov 2026). Hard dependency on VHVR.

- [FEATURES.md](FEATURES.md): everything it adds or changes compared with plain VHVR, every setting, files it writes,
  limitations, and what to do before a public release.
- [CHANGELOG.md](CHANGELOG.md): what changed in each version.
- [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md): what still needs trying in the headset.

## Modules
Each module lives in `Modules/<Name>/`: a `*Policy.cs` with the rules in plain C# (what would move into VHVR), a
`*Hooks.cs` with the Harmony/reflection glue (what a VHVR PR replaces with direct calls), and an `UPSTREAM.md`.
A module that can't find the VHVR code it needs disables itself and logs why; the others keep working.

| Module | What it does | Config section |
|---|---|---|
| Prompts | Gamepad glyphs in prompts, labelled with the Frame button VHVR actually uses; big-map hints show the laser controls | `[Prompts]` |
| MenuStickClicks | Right/left stick click = Place stacks / Take all in chests | `[Menus]` |
| Crouch | Crouch button ignored in chests/menus/map (the same click is VHVR's middle click there); optional double-click crouch | `[Crouch]` |
| MapSharing | Turns on "public position" on the map when you join | `[Map]` |
| ServerPasswords | Remembers a server's password after a successful join (Windows DPAPI-encrypted); forget-all option | `[Servers]` |
| Punch | Checkboxes for what a bare-hand punch needs (grip / trigger / thumb on stick) + looseness warning | `[Punch]` |
| Posture | Auto seated/standing from learned standing + seated eye heights; Steam-button recenter re-measures | `[Posture]` |
| Bindings | Frame binding tweaks applied to Valheim's copy of VHVR's Frame binding (restart to apply) | `[Bindings]` |
| FrameTab | "Steam Frame" tab in VHVR's settings: pages for settings, button guide, recenter pose + height | `[FrameTab]` |

Settings file: `BepInEx/config/local.vhvr.steamframe.cfg` (or the Steam Frame tab in Settings > VHVR, saved with OK).
The recenter-pose picture is `Assets/recenter_pose.png` (embedded), drawn by `Assets/draw_recenter_pose.ps1`.

## What it changes outside the game's memory
Only Valheim's folder: `valheim_Data/StreamingAssets/SteamVR/bindings_frame_controller.json` (VHVR's untouched copy is
kept in `BepInEx/config/VhvrSteamFrame/` and every write starts from it), and `BepInEx/config/VhvrSteamFrame/server-passwords.dat`
(only when a password is saved). Nothing in SteamVR's own settings.

## Build
`dotnet build -c Release` (references the game's DLLs via `ValheimDir`, default Steam path). Copy
`bin/Release/netstandard2.1/VhvrSteamFrame.dll` to `BepInEx/plugins` with Valheim closed.

## Contributing upstream
Suggested order, one small PR each (see each module's UPSTREAM.md): Frame bindings JSON (+ crouch-in-menus fix) →
stick-click menu buttons → seated/standing → punch requirements → Frame/controller settings tab. ServerPasswords and
MapSharing aren't VR-specific and probably stay out of VHVR. VHVR is GPL-3.0; add the GPL-3.0 LICENSE file
here before publishing this plugin. VHVR's dev setup: `README.development.md` in their repo (Git, .NET SDK 8+,
`scripts/Test-BackendRefactor.ps1`; new input actions go in `VRInputActions.cs` + `actions.json` + default bindings).
