# Bindings → VHVR (PR 1, smallest)

**Target:** `Unity/ValheimVR/Assets/StreamingAssets/SteamVR/bindings_frame_controller.json` (VHVR calls its Frame
binding "tentative, please customize"). No code changes; just the JSON, so `FrameBindingPatcher` isn't needed upstream.

Edits (what `FrameBindingPatcher.Apply` does, verified by a test against VHVR 0.11.1's file):
1. `/actions/default`: replace each stick's touch-flick-north → `teleport` with whole-stick `touch` → `teleport`
   (lets "thumb resting on stick" be sensed; see Punch). Better upstream: a dedicated action, e.g. `thumbrest`.
2. `/actions/valheim`, left stick joystick: `click` → `togglerun` (was `middleclick`).
3. `/actions/valheim`, right stick: new `button` source, `click` → `togglecrouch`.
4. `/actions/valheim`, right stick touch-dpad: `north`/`south` → `stopgesturedlocomotion` (was `togglerun`/`togglecrouch`).
   Keep them defined: the grip + stick up/down chords (`facepieceaway/toward`) need those directions.

PR text should explain: touch-flicks fire while turning; stick clicks are the Frame's natural crouch/sprint toggles and
also drive Valheim's "Place stacks"/"Take all" (see MenuStickClicks).
