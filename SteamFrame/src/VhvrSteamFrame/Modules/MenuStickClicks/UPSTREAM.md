# MenuStickClicks → VHVR (PR 2)

**Target:** `ValheimVRMod/VRCore/UI/VRControls.cs`. VHVR logs `Unmapped ZInput Key` for `JoyRStick` / `JoyLStick`, so
Valheim's on-screen "Place stacks" / "Take all" buttons (UIGamePad) never fire in VR.

**Change:** in VRControls' GetButtonDown handling, while `InventoryGui.IsVisible()`, answer `JoyRStick` with the right
hand's stick-click action and `JoyLStick` with the left's (here: `valheim_ToggleCrouch` right / `valheim_ToggleRun`
left, per the Frame binding). `MenuStickClicksPolicy.ActionFor` is the whole rule; the Harmony postfix on
`ZInput.GetButtonDown` disappears. Consider dedicated actions so it's binding-independent.
