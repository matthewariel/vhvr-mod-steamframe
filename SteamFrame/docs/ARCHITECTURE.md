# Architecture

How Steam Frame for VHVR is put together, and the rules that keep it easy to maintain. For what it does, see
[FEATURES.md](FEATURES.md). For day-to-day commands, see [MAINTAINING.md](MAINTAINING.md).

## The big picture

- The plugin is **one DLL** (`VhvrSteamFrame.dll`) that BepInEx loads next to official VHVR.
- It **never changes VHVR's files**. It hooks into VHVR and Valheim while the game runs.
- Everything lives in the `SteamFrame/` folder of the fork. The rest of the repository is VHVR's code, untouched.

## Three layers

| Layer | Where | What it may reference | Why |
|---|---|---|---|
| **Policies** (the rules) | `src/Policies/` | Plain C# only. No Unity, BepInEx, VHVR or Valheim | Unit-testable without the game, and copy-pasteable into VHVR later |
| **Hooks** (the glue) | `src/VhvrSteamFrame/AllHeadsets`, `SteamFrame`, `Extras` | Everything | Harmony patches and reflection that feed the game's state into the rules |
| **UI** | `src/VhvrSteamFrame/UI/SettingsTab` | Everything | The tab in VHVR's settings dialog |

A hook should contain as little logic as possible: read the game state, ask a policy, apply the answer.

The policies are compiled straight into the plugin (there is no separate DLL to ship). They are also compiled into:
- `tests/SteamFrame.Tests`: the unit tests;
- `tests/PoliciesCompat`: a build with VHVR's limits (.NET Framework 4.6, C# 7.3). If it builds, the rules can still be
  pasted into VHVR.

The JSON code in the policies uses `Valve.Newtonsoft.Json` inside the game (`VHVR_GAME` is defined) and the NuGet
`Newtonsoft.Json` in tests. Same API, different namespace.

## Three scopes

Every module, folder, settings-tab section and doc row belongs to one scope.

| Scope | Folder | Meaning |
|---|---|---|
| **All headsets** | `AllHeadsets/` | Works with any VR controller VHVR supports. Tested on Steam Frame only |
| **Steam Frame** | `SteamFrame/` | Only for Steam Frame controllers |
| **Extra** | `Extras/` | Not about VR |

**The rule: all-headset code never names the Steam Frame. It asks the active controller profile.**

- `Controllers/ControllerDetector` asks SteamVR which controllers are in use and exposes `ActiveProfile`
  (a `ControllerProfile`, or null when the controller isn't known or detection is Off).
- A `ControllerProfile` is data only: display name, SteamVR controller type, binding file name, prompt labels, map
  labels.
- `Controllers/BindingFacts` tells all-headset modules what the stick clicks currently do. The Steam Frame bindings
  module fills it in.
- With no profile, an all-headset module does nothing special (prompts show Valheim's own labels).

Check it with:

```bash
git grep -n "Frame" -- SteamFrame/src/VhvrSteamFrame/AllHeadsets SteamFrame/src/Policies/AllHeadsets
```

That should only find the namespace, comments, setting names and descriptions. Setting names such as
`[Prompts] FrameMapHints` and the value `ButtonLabels = Frame` stay, because renaming them would reset players' settings.

Two files are allowed to know about every profile: `src/Policies/ControllerProfiles.cs` (the list) and
`Controllers/ControllerDetector.cs` (what "Force" means).

## The module contract

Every feature is a class deriving from `Core/Module`:

| Member | When it runs | What to do |
|---|---|---|
| `Name` | | Short name used in the log |
| `Scope` | | `AllHeadsets`, `SteamFrame` or `Extra` |
| `BindConfig(config)` | At startup, for every module, before any `Start` | Bind settings. Nothing else |
| `Start(harmony)` | At startup | Look up VHVR/Valheim members and install patches. **Throw to disable the module** |
| `Tick()` | Every frame while active | Optional. An exception stops this module only |

- Each module has its own Harmony id. If `Start` throws, only that module's patches are removed.
- A module that can't find what it needs logs why and switches itself off. The others keep working.
- Modules are created in `Core/Plugin.cs`.

## Settings are an API

Players' settings live in `BepInEx/config/local.vhvr.steamframe.cfg`. Never change:
- the plugin GUID `local.vhvr.steamframe` (it names that file);
- a section or setting name;
- a default value.

Labels on the tab and setting descriptions may change freely.

## VHVR and Valheim internals: one list

`Core/VhvrApi.cs` lists every type and member the plugin looks up by name, patches, or (for VHVR) uses directly.
Hooks never write a member name as a string; they use the entry: `Vhvr.Method(VhvrApi.FistCanAttack)`.

`scripts\Check-VhvrCompat.ps1` reads the same list and checks it against an installed game without starting it.

### The Harmony traps

Learned the hard way; don't relearn them.

- **Harmony cannot patch a VHVR method that touches a private Valheim member.** VHVR is compiled against a "publicized"
  Valheim, so the patched copy fails with an IL compile error.
  - Known safe: `FistCollision.canAttackWithCollision`, `VRPlayer.get_referencePlayerHeight`,
    `ConfigSettings.CreateTab(string,int)`, `ConfigSettings.CreateFullBodyTrackingTab(int)`.
  - Known unsafe: `ConfigSettings.createModSettings`, `FistCollision.OnTriggerEnter`.
  - For an unsafe method, call it through a plain delegate instead (see `AllHeadsets/Punch/ThumbPunch.cs`).
  - The compatibility checker tests this for every patched VHVR method.
- **The plugin compiles against the normal (non-publicized) Valheim.** Reach private members through `VhvrApi` +
  `Vhvr.Field/Method`, otherwise the compiler reports CS0122.
- **TextMeshPro:** use `textWrappingMode`, not `enableWordWrapping`.
- `MSB3277` warnings about System.IO.Compression / System.Net.Http are expected noise.

## How to add a module

1. Decide its scope. That picks the folder.
2. Put the rules in `src/Policies/<Scope>/` as plain C#, and write tests in `tests/SteamFrame.Tests`.
3. Add `<Name>Hooks.cs` in `src/VhvrSteamFrame/<Scope>/<Name>/`, deriving from `Module`.
4. Add every VHVR/Valheim member it needs to `Core/VhvrApi.cs`.
5. Register it in `Core/Plugin.cs`.
6. If it has settings, add rows to the tab under the matching scope header (`UI/SettingsTab/FrameTabView.cs`).
7. Add an `UPSTREAM.md` next to the hooks, and update [FEATURES.md](FEATURES.md), [CHANGELOG.md](CHANGELOG.md) and
   [IN-GAME-CHECKS.md](IN-GAME-CHECKS.md).

## How to add a controller profile (another headset)

1. Write a class deriving from `ControllerProfile` in `src/Policies/<HeadsetName>/`:
   - `ControllerType`: SteamVR's `controller_type` string (see VHVR's `actions.json`);
   - `BindingFileName`: VHVR's binding file for it;
   - `PromptLabels` and `MapLabels`: which button does what.
2. Add it to the list in `src/Policies/ControllerProfiles.cs`.
3. Add tests like `SteamFrameProfileTests`.
4. Optional: a binding tweaks module in `src/VhvrSteamFrame/<HeadsetName>/`, like `SteamFrame/Bindings`.

That's all the all-headset modules need: they pick the new profile up through `ControllerDetector.ActiveProfile`.

## What can't be tested without other headsets

- Whether SteamVR reports the expected `controller_type` for them.
- Whether their VHVR bindings put crouch and sprint where the chest-button and crouch-guard modules expect.
- Thumb-on-stick punch sensing: it needs a stick-touch binding. Without one the requirement is ignored.
- The Steam-button recenter events: other headsets may send different SteamVR events.
- Everything visual on the tab.

Until someone with another headset tries it, "All headsets" means "written to be generic, tested on Steam Frame only".
