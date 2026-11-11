# Steam Frame for VHVR

A BepInEx companion plugin for [VHVR](https://github.com/brandonmousseau/vhvr-mod) (Valheim VR). It makes the Valve
Steam Frame controllers feel right in Valheim: button prompts that show the real buttons, configurable bare-hand
punching, seated/standing play, safer Frame bindings, and a **Steam Frame** tab in VHVR's settings with a button guide
and the recenter pose. It runs next to official VHVR and changes none of VHVR's files and no SteamVR setting.

Version 0.3.1, built against VHVR 0.11.1. Everything is tested on a Steam Frame only.

## Scope legend

The code, the docs and the in-game tab sort everything into three groups:

| Scope | Meaning |
|---|---|
| **All headsets** | Works with any VR controller VHVR supports. Tested on Steam Frame only |
| **Steam Frame** | Only for Steam Frame controllers |
| **Extra** | Not about VR; a convenience that lives here |

## Install (players)

1. Install BepInEx and official VHVR as usual.
2. Put `VhvrSteamFrame.dll` in `Valheim\BepInEx\plugins\`, next to `ValheimVRMod.dll`.
3. Start Valheim. Settings are under Settings > VHVR > **Steam Frame**, or in
   `BepInEx\config\local.vhvr.steamframe.cfg`.

Binding changes need one restart of Valheim. To uninstall, delete the DLL; turning off "Apply binding tweaks" first puts
VHVR's own Frame binding back.

## Build (developers)

Needs the .NET SDK 8 or newer, and Valheim with BepInEx and VHVR installed (the plugin compiles against their DLLs).
From this folder:

```bash
powershell -ExecutionPolicy Bypass -File scripts\Test.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File scripts\Build.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File scripts\Install.ps1
```

Game not in the default Steam folder? Add `-ValheimDir "D:\Games\Valheim"` to Build and Install. The tests need no game
files. Full walkthrough: [docs/MAINTAINING.md](docs/MAINTAINING.md).

## Folder map

```
SteamFrame/
  README.md                 this file
  SteamFrame.sln            plugin + tests + tools (VHVR's own solution is untouched)
  Directory.Build.props     game paths and the plugin version
  docs/                     FEATURES, CHANGELOG, IN-GAME-CHECKS, ARCHITECTURE, MAINTAINING, UPSTREAM-NOTES
  src/
    Policies/               the rules, as plain C# with no game references (unit-tested)
      AllHeadsets/          punch, posture, crouch, prompts, button guide, controller profile
      SteamFrame/           the Steam Frame profile and its binding tweaks
      Extras/               server password store, map sharing
    VhvrSteamFrame/         the BepInEx plugin (the only DLL that ships)
      Core/                 plugin entry, module base, the list of VHVR internals (VhvrApi)
      Controllers/          which controller is in use
      AllHeadsets/          hooks for Prompts, MenuStickClicks, Crouch, Punch, Posture
      SteamFrame/           hooks for Bindings
      Extras/               hooks for ServerPasswords, MapSharing
      UI/SettingsTab/       the tab in VHVR's settings
      Assets/               the recenter pose picture
  tests/
    SteamFrame.Tests/       unit tests (no game files needed)
    PoliciesCompat/         checks the rules still compile the way VHVR would compile them
  tools/VhvrCompatCheck/    checks an installed VHVR/Valheim against what the plugin uses
  scripts/                  Build, Test, Install, Package, Check-VhvrCompat
```

## Modules

A module that can't find the VHVR code it needs switches itself off and logs why; the others keep working.

| Module | Scope | What it does | Config section |
|---|---|---|---|
| Prompts | All headsets | Gamepad glyphs in prompts, labelled with the button VHVR actually uses; big-map hints show the laser controls | `[Prompts]` |
| MenuStickClicks | All headsets | Right/left stick click = Place stacks / Take all in chests | `[Menus]` |
| Crouch | All headsets | Crouch button ignored in chests, menus and the map; optional double-click crouch | `[Crouch]` |
| Punch | All headsets | Checkboxes for what a bare-hand punch needs (grip / trigger / thumb on stick), with a looseness warning | `[Punch]` |
| Posture | All headsets | Auto seated/standing from learned eye heights; Steam-button recenter re-measures | `[Posture]` |
| FrameTab | All headsets (UI) | "Steam Frame" tab in VHVR's settings: settings by scope, button guide, recenter pose and height | `[FrameTab]` |
| Bindings | Steam Frame | Binding tweaks applied to Valheim's copy of VHVR's Frame binding (restart to apply) | `[Bindings]` |
| ServerPasswords | Extra | Remembers a server's password after a successful join (Windows DPAPI-encrypted) | `[Servers]` |
| MapSharing | Extra | Turns on "public position" on the map when you join | `[Map]` |

## What it writes

Only inside Valheim's folder: its settings file, Valheim's copy of the Frame binding
(`valheim_Data/StreamingAssets/SteamVR/bindings_frame_controller.json`, with VHVR's original kept in
`BepInEx/config/VhvrSteamFrame/`), and `server-passwords.dat` once a password is saved. Nothing in SteamVR's own
settings. Details: [docs/FEATURES.md](docs/FEATURES.md#files-it-writes).

## Docs

- [docs/FEATURES.md](docs/FEATURES.md): everything it adds or changes compared with plain VHVR, every setting,
  limitations, and the list for before a public release.
- [docs/CHANGELOG.md](docs/CHANGELOG.md): what changed in each version.
- [docs/IN-GAME-CHECKS.md](docs/IN-GAME-CHECKS.md): what still needs trying in the headset.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): layers, scopes, how to add a module or another controller.
- [docs/MAINTAINING.md](docs/MAINTAINING.md): the Git and GitHub routine, and what to do when VHVR updates.
- [docs/UPSTREAM-NOTES.md](docs/UPSTREAM-NOTES.md): notes for contributing pieces to VHVR one day. Nothing is planned.

## Licence

VHVR is GPL-3.0 and this plugin lives in a fork of it, so the `LICENSE` at the repository root applies.
