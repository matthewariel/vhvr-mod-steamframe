# Posture → VHVR (PR 3)

**Problem:** `VRPlayer.referencePlayerHeight` = measured eye height clamped to PlayerMin/MaxEyeHeight, and the view sits
that high above the character's feet. Recentering while seated (~1.2 m) makes you chest-height to other players; the
only workaround is raising `PlayerMinEyeHeight`. SteamVR's own recenter (Steam button) never re-measures.

**Change:**
- `VHVRConfig`: `PlayPosture` (Auto / Standing / Seated), `StandingEyeHeight` and `SeatedEyeHeight` (set by the
  player, never learned), `SeatedBelowPercent`.
- Settings UI: "Set standing height" / "Set seated height" buttons that save the HMD's current height above the
  SteamVR floor (`Player.eyeHeight`, the same quantity `maybeCaliberatePlayerHeight` samples), checked with
  `PostureCalibrator.RejectReason` / `GapWarning`.
- `VRPlayer.referencePlayerHeight`: `PostureTracker.Reference(PlayerMinEyeHeight, PlayerMaxEyeHeight)` (copy
  `PostureCalibrator.cs`), fed `OnMeasured(raw)` from `maybeCaliberatePlayerHeight` and `Update(eyeHeight, time,
  isRoomscaleSneaking)` each frame. Only the gap between the two heights sets the view, so a wrong SteamVR floor
  doesn't; the absolute heights only classify. Keep the floor-reference bookkeeping (`PostureHooks.FollowFloor`).
- `VRPlayer.maybeCaliberatePlayerHeight`: after a measurement, show which posture it picked. Don't learn heights from
  it: the 0.3.x plugin did, and a single 1.98 m reading broke Auto until it was set by hand.
- SteamVR backend: listen for `VREvent_StandingZeroPoseReset` / `SeatedZeroPoseReset` / `ChaperoneUniverseHasChanged`
  and call `RequestRecentering(true)` + `RequestPelvisCaliberation()` + `ResetRoomscaleCamera()` (what
  `SteamVRRecenter` does via a PollNextEvent postfix). Check the log line "SteamVR event seen" to confirm which event
  the Frame's recenter sends before proposing.
