using Valve.Newtonsoft.Json.Linq;
using VhvrSteamFrame;

int failures = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) failures++; }

// Flatten a binding doc into "set | path mode sub | input -> output" lines (ignores formatting and order).
HashSet<string> Flat(JObject root)
{
    var set = new HashSet<string>();
    foreach (var actionSet in ((JObject)root["bindings"]).Properties())
    {
        foreach (var s in ((JArray)actionSet.Value["sources"]).OfType<JObject>())
            foreach (var inp in ((JObject)s["inputs"]).Properties())
                set.Add($"{actionSet.Name} | {s["path"]} {s["mode"]} {s["parameters"]?["sub_mode"]} | {inp.Name} -> {inp.Value["output"]}");
        foreach (var c in (actionSet.Value["chords"] as JArray ?? new JArray()).OfType<JObject>())
            set.Add($"{actionSet.Name} | CHORD {string.Join(" + ", ((JArray)c["inputs"]).Select(i => string.Join(":", i)))} -> {c["output"]}");
    }
    return set;
}

string original = File.ReadAllText(args[0]);
string handEdited = File.ReadAllText(args[1]);

// 1. All tweaks on, from VHVR's original, should equal the hand-edited file.
var patched = JObject.Parse(original);
var problems = FrameBindingPatcher.Apply(patched, new FrameBindingOptions());
Check(problems.Count == 0, "no skipped tweaks: " + string.Join("; ", problems));
var a = Flat(patched); var b = Flat(JObject.Parse(handEdited));
// args[1] is now the file the plugin wrote into the game on its first launch: it must be exactly the patcher's output.
Check(a.SetEquals(b), "installed Frame binding == patcher output (all tweaks on)");
foreach (var x in a.Except(b)) Console.WriteLine("   only in patched: " + x);
foreach (var x in b.Except(a)) Console.WriteLine("   only in installed: " + x);
Check(a.Any(l => l.Contains("thumbstick dpad touch | north -> /actions/valheim/in/stopgesturedlocomotion")) &&
      a.Any(l => l.Contains("thumbstick dpad touch | south -> /actions/valheim/in/stopgesturedlocomotion")), "tilt chord directions defined");
Check(!a.Any(l => l.Contains("dpad touch | north -> /actions/valheim/in/togglerun") || l.Contains("dpad touch | south -> /actions/valheim/in/togglecrouch")), "no flick sprint/crouch");

// 2. Idempotent.
var twice = JObject.Parse(patched.ToString());
FrameBindingPatcher.Apply(twice, new FrameBindingOptions());
Check(Flat(twice).SetEquals(a), "applying twice changes nothing");

// 3. All tweaks off == original.
var none = JObject.Parse(original);
FrameBindingPatcher.Apply(none, new FrameBindingOptions { ThumbTouchSensing = false, SprintToggleOnLeftStickClick = false, CrouchOnRightStickClick = false, NoStickFlickRunCrouch = false });
Check(Flat(none).SetEquals(Flat(JObject.Parse(original))), "all tweaks off == VHVR original");

// 4. One tweak off keeps VHVR's binding for that button.
var noCrouch = JObject.Parse(original);
FrameBindingPatcher.Apply(noCrouch, new FrameBindingOptions { CrouchOnRightStickClick = false, NoStickFlickRunCrouch = false });
var nc = Flat(noCrouch);
Check(nc.Any(l => l.Contains("right/input/thumbstick dpad touch | south -> /actions/valheim/in/togglecrouch")), "crouch tweaks off keep VHVR's flick-down crouch");
Check(!nc.Any(l => l.Contains("right/input/thumbstick button") && l.Contains("togglecrouch")), "crouch tweak off adds no stick-click crouch");

// 5. Punch policy.
Check(PunchPolicy.Rate(new PunchRequirements { Grip = true, ThumbOnStick = true }) == PunchLooseness.Ok, "grip+thumb is OK");
Check(PunchPolicy.Rate(new PunchRequirements { Grip = true, ThumbOnStick = false }) == PunchLooseness.Loose, "grip only is loose");
Check(PunchPolicy.Rate(new PunchRequirements { Grip = false, Trigger = false, ThumbOnStick = true }) == PunchLooseness.VeryLoose, "thumb only is very loose");
Check(PunchPolicy.Rate(new PunchRequirements { Grip = false, Trigger = true, ThumbOnStick = true }) == PunchLooseness.Ok, "trigger+thumb is OK");
var req = new PunchRequirements { Grip = true, Trigger = false, ThumbOnStick = true };
Check(PunchPolicy.Allows(req, true, false, true) && !PunchPolicy.Allows(req, true, false, false) && !PunchPolicy.Allows(req, false, true, true), "grip+thumb allows only with both held");
Check(PunchPolicy.MissingInput(req, true, false, false) == "thumb on stick", "missing input named");

// 6. Posture.
Check(PostureCalibrator.Classify(1.20f, PostureMode.Auto, 1.67f, 0.8f) == Posture.Seated, "1.20 m vs 1.67 standing = seated");
Check(PostureCalibrator.Classify(1.64f, PostureMode.Auto, 1.67f, 0.8f) == Posture.Standing, "1.64 m = standing");
Check(PostureCalibrator.ReferenceOverride(Posture.Seated, 1.67f) == 1.67f, "seated uses standing height");
Check(PostureCalibrator.ReferenceOverride(Posture.Standing, 1.67f) < 0, "standing leaves VHVR alone");
Check(PostureCalibrator.Classify(1.20f, PostureMode.Auto, 0f, 0.8f) == Posture.Seated && PostureCalibrator.ReferenceOverride(Posture.Seated, 0f) < 0, "unknown standing height: seated but no override");
Check(PostureCalibrator.ShouldLearn(Posture.Standing, PostureMode.Auto, 1.67f) && !PostureCalibrator.ShouldLearn(Posture.Seated, PostureMode.Auto, 1.2f), "learns only standing");
Check(PostureCalibrator.Classify(1.6f, PostureMode.Seated, 1.67f, 0.8f) == Posture.Seated, "Seated mode always seated");

// 6b. Posture with a learned seated height.
Check(Math.Abs(PostureCalibrator.SeatedThreshold(1.67f, 1.20f, 0.8f) - 1.435f) < 0.001f, "both heights known: split halfway (1.435)");
Check(Math.Abs(PostureCalibrator.SeatedThreshold(1.67f, 0f, 0.8f) - 1.336f) < 0.001f, "seated unknown: 80% of standing");
Check(Math.Abs(PostureCalibrator.SeatedThreshold(1.67f, 1.60f, 0.8f) - 1.336f) < 0.001f, "heights too close: falls back to 80%");
Check(Math.Abs(PostureCalibrator.SeatedThreshold(0f, 1.20f, 0.8f) - 1.45f) < 0.001f, "only seated known: seated + 0.25");
Check(PostureCalibrator.Classify(1.40f, PostureMode.Auto, 1.67f, 1.20f, 0.8f) == Posture.Seated, "1.40 with 1.67/1.20 = seated (old 80% rule said standing)");
Check(PostureCalibrator.Classify(1.50f, PostureMode.Auto, 1.67f, 1.20f, 0.8f) == Posture.Standing, "1.50 with 1.67/1.20 = standing");
Check(PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Auto, 1.2f, 1.67f), "learns seated height when seated");
Check(!PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Auto, 0.4f, 1.67f), "ignores a 0.4 m glitch");
Check(!PostureCalibrator.ShouldLearnSeated(Posture.Seated, PostureMode.Seated, 1.66f, 1.67f), "forced Seated mode doesn't learn a standing-height reading as seated");
Check(!PostureCalibrator.ShouldLearnSeated(Posture.Standing, PostureMode.Auto, 1.6f, 1.67f), "doesn't learn seated from standing");

// 6c. Crouch gate.
var crouchGate = new CrouchGate { DoubleClickWindow = 0.45f };
Check(crouchGate.Decide(true, false, true, true, true, false, 1f) == CrouchDecision.BlockInMenu, "crouch click in a chest is blocked");
Check(crouchGate.Decide(true, true, true, true, true, false, 1f) == CrouchDecision.BlockInMenu, "stand-up click in a chest is blocked too");
Check(crouchGate.Decide(true, false, true, true, false, false, 1f) == CrouchDecision.Allow, "blocking off: chest click crouches");
Check(crouchGate.Decide(true, false, true, false, true, false, 1f) == CrouchDecision.Allow, "single click crouches when double click is off");
Check(crouchGate.Decide(true, false, true, false, true, true, 10f) == CrouchDecision.WaitForSecondClick &&
      crouchGate.Decide(true, false, true, false, true, true, 10.3f) == CrouchDecision.Allow, "double click within window crouches");
Check(crouchGate.Decide(true, false, true, false, true, true, 20f) == CrouchDecision.WaitForSecondClick &&
      crouchGate.Decide(true, false, true, false, true, true, 20.6f) == CrouchDecision.WaitForSecondClick, "clicks too far apart don't crouch");
Check(crouchGate.Decide(true, true, true, false, true, true, 30f) == CrouchDecision.Allow, "standing up is one click");
Check(crouchGate.Decide(true, false, false, false, true, true, 40f) == CrouchDecision.Allow, "physical ducking isn't double-click gated");
Check(crouchGate.Decide(false, false, true, true, true, true, 50f) == CrouchDecision.Allow, "no toggle requested: nothing to do");

// 6d. Map labels and prompt labels.
var mapL = PromptsPolicy.FrameMapLabels();
Check(mapL["MapZoomIn"].Contains("dpad_up") && mapL["mapzoomout"].Contains("dpad_down") && mapL["ButtonX"].Contains("button_rs"),
      "map: zoom = D-pad up/down, ping = right stick click");
Check(PromptsPolicy.ActionFromKey("KEY_JoyMapZoomIn") == "MapZoomIn" && mapL.ContainsKey(PromptsPolicy.ActionFromKey("KEY_ButtonX")),
      "map keys resolve whether Valheim asks for KEY_JoyX or KEY_X");
Check(PromptsPolicy.FrameLabels(true, true, true)["Crouch"].StartsWith("2x ") && !PromptsPolicy.FrameLabels(true, true, false)["Crouch"].StartsWith("2x"),
      "crouch prompt shows 2x only with double click");

// 6e. Server passwords: store round trip with real DPAPI, and outcomes.
var pwStore = new ServerPasswordStore();
pwStore.Set("steam/123/1.2.3.4:2456", "hunter2 ünïcode", Dpapi.Protect);
string serialized = pwStore.Serialize();
Check(!serialized.Contains("hunter2"), "saved file doesn't contain the password in plain text");
var reloaded = ServerPasswordStore.Parse(serialized + "garbage line\nv1\tnot base64\t!!\n");
Check(reloaded.Count == 1 && reloaded.TryGet("steam/123/1.2.3.4:2456", Dpapi.Unprotect, out var pwBack) && pwBack == "hunter2 ünïcode",
      "DPAPI round trip through the file format, damaged lines skipped");
Check(!reloaded.TryGet("socket/other:2456", Dpapi.Unprotect, out _), "unknown server has no password");
Check(!reloaded.TryGet("steam/123/1.2.3.4:2456", b => throw new Exception("wrong user"), out _), "undecryptable entry reads as missing");
Check(reloaded.Remove("steam/123/1.2.3.4:2456") && reloaded.Count == 0 && reloaded.Serialize() == "", "remove and clear");
Check(ServerPasswordPolicy.Outcome(2, false, 1f) == PasswordOutcome.Save, "connected: save");
Check(ServerPasswordPolicy.Outcome(6, true, 1f) == PasswordOutcome.ForgetSaved, "wrong saved password: forget it");
Check(ServerPasswordPolicy.Outcome(6, false, 1f) == PasswordOutcome.Drop, "wrong typed password: don't save");
Check(ServerPasswordPolicy.Outcome(1, false, 5f) == PasswordOutcome.Pending && ServerPasswordPolicy.Outcome(1, false, 200f) == PasswordOutcome.Drop,
      "still connecting: wait, then give up");
Check(ServerPasswordPolicy.Outcome(9, true, 1f) == PasswordOutcome.Drop, "server full: keep the saved password");

// 7. Button guide.
var rows = BindingTable.Build(JObject.Parse(handEdited));
Console.WriteLine("\n--- guide (" + rows.Count + " rows) ---");
Console.WriteLine(BindingTable.Format(rows, "Left"));
Console.WriteLine(BindingTable.Format(rows, "Right"));

Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
return failures == 0 ? 0 : 1;
