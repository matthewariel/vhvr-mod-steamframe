# Notes for contributing to VHVR (later, maybe)

**No pull requests to VHVR are planned now.** The plugin stays its own DLL in this fork. These notes exist so that, if
that changes, working out what to offer is not a big review job.

Before any pull request: **ask on the VHVR Discord first** whether the maintainers want the change.

## Why it would be easy

- The rules of each feature are plain C# in `src/Policies/`, already compiled with VHVR's limits
  (.NET Framework 4.6, C# 7.3) by `tests/PoliciesCompat`. They can be pasted into VHVR.
- The hooks (Harmony patches, reflection) exist only because the plugin is outside VHVR. Inside VHVR each becomes a
  direct call.
- Each module has its own `UPSTREAM.md` saying what the VHVR change would look like.

## Module notes

| Module | Scope | Notes |
|---|---|---|
| Crouch (ignore in menus, double click) | All headsets | [UPSTREAM.md](../src/VhvrSteamFrame/AllHeadsets/Crouch/UPSTREAM.md) |
| Posture + Steam-button recenter | All headsets | [UPSTREAM.md](../src/VhvrSteamFrame/AllHeadsets/Posture/UPSTREAM.md) |
| Punch requirements | All headsets | [UPSTREAM.md](../src/VhvrSteamFrame/AllHeadsets/Punch/UPSTREAM.md) |
| Chest stick-click buttons | All headsets | [UPSTREAM.md](../src/VhvrSteamFrame/AllHeadsets/MenuStickClicks/UPSTREAM.md) |
| Prompts and map hints | All headsets | [UPSTREAM.md](../src/VhvrSteamFrame/AllHeadsets/Prompts/UPSTREAM.md) |
| Settings tab + recenter picture | All headsets UI | [UPSTREAM.md](../src/VhvrSteamFrame/UI/SettingsTab/UPSTREAM.md) |
| Frame binding tweaks | Steam Frame | [UPSTREAM.md](../src/VhvrSteamFrame/SteamFrame/Bindings/UPSTREAM.md) |
| Server passwords | Extra | [UPSTREAM.md](../src/VhvrSteamFrame/Extras/ServerPasswords/UPSTREAM.md). Stays out of VHVR |
| Map position sharing | Extra | [UPSTREAM.md](../src/VhvrSteamFrame/Extras/MapSharing/UPSTREAM.md). Stays out of VHVR |
| Session logs | Extra | [UPSTREAM.md](../src/VhvrSteamFrame/Extras/SessionLogs/UPSTREAM.md). Stays out of VHVR |

## Order, if it ever happens

One small pull request each. All-headset changes first, because they help every VHVR player and are the easiest to
accept:

1. Crouch ignored in menus (a small bug fix).
2. Seated/standing posture, with the Steam-button recenter.
3. Punch requirements.
4. Settings tab and the recenter pose picture.
5. Then the Steam Frame binding JSON edits.

The extras (server passwords, map sharing) are not VR features and stay in this plugin.

Everything all-headset here has been **tested on Steam Frame only**. Say so in any pull request; the maintainers can
test other headsets.

## Avoiding double application ("native takeover")

If VHVR takes a feature, players could run a VHVR that has it together with a plugin version that also has it. The plan,
not built yet:

- Each such VHVR change adds a recognisable config member (for example a `VHVRConfig` method for the new setting).
- The matching module here looks for that member at startup. If it exists, the module stands down and logs that VHVR
  now does this itself.

That way the feature can never be applied twice, whichever versions a player combines. The lookup would be one optional
entry in `Core/VhvrApi.cs`.

## VHVR's developer setup

See `README.development.md` at the repository root. New input actions go in `VRInputActions.cs`, `actions.json` and the
default bindings.
