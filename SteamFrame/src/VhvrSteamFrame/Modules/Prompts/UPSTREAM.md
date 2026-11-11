# Prompts → VHVR (optional)

VHVR shows keyboard prompts ("[E] Pick up") because Valheim's `ZInput.IsGamepadActive()` is false under VR controls.
Upstream version: in VHVR's own patches, make the prompt-text paths (`Localization.Translate`, `Hud.UpdateCrosshair`,
`UIInputHint.UpdateInputHints`, a few `GetHoverText`s) treat VR controls as a gamepad, and pick labels per controller
type from the active binding (`PromptsPolicy.FrameLabels` is the Frame table; `BindingTable` could generate it).
Larger and more opinionated than the others; propose after the smaller PRs land.
