using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VhvrSteamFrame
{
    /// <summary>
    /// VHVR measures your eye height only on its own recenter (game start, Home key, or its hands-to-face recenter pose).
    /// SteamVR's recenter (holding the Steam button) moves the tracking space but never tells VHVR.
    ///
    /// This watches SteamVR's event queue for a recenter and then does what VHVR's own recenter does, minus asking SteamVR
    /// to recenter again: re-centre the view on the character and re-measure eye height. Nothing in SteamVR is changed;
    /// this only reads events SteamVR already sends to Valheim.
    /// </summary>
    internal static class SteamVRRecenter
    {
        // Valve.VR.EVREventType values.
        private const uint ChaperoneUniverseHasChanged = 801;
        private const uint SeatedZeroPoseReset = 804;
        private const uint StandingZeroPoseReset = 808;

        private const float SettleSeconds = 0.75f; // let SteamVR finish moving the tracking space before measuring
        private const float MinSecondsBetween = 3f;

        private static MethodInfo requestRecentering, requestPelvis, vrPlayerInstance, resetRoomscaleCamera, shouldPause;
        private static volatile uint pendingEvent;
        private static uint dueEvent;
        private static float dueAt = -1f, lastRecenter = -100f;
        private static readonly HashSet<uint> seenEvents = new HashSet<uint>();
        private static bool warnedError;

        /// <summary>When the last SteamVR recenter / chaperone event was seen (unscaled time); -100 before the first.</summary>
        internal static float LastEventAt = -100f;

        /// <summary>Throws (MissingMemberException) when VHVR or the SteamVR plugin changed.</summary>
        internal static void Init(Harmony harmony)
        {
            requestRecentering = Vhvr.Method(VhvrApi.VrPlayerRequestRecentering);
            vrPlayerInstance = Vhvr.Method(VhvrApi.VrPlayerVrPlayerInstance);
            requestPelvis = Vhvr.TryMethod(VhvrApi.VrPlayerRequestPelvis);
            resetRoomscaleCamera = Vhvr.TryMethod(VhvrApi.VrPlayerResetRoomscaleCamera);
            shouldPause = Vhvr.TryMethod(VhvrApi.VrPlayerShouldPauseMovement);

            var poll = AccessTools.Method(typeof(Valve.VR.CVRSystem), nameof(Valve.VR.CVRSystem.PollNextEvent),
                new[] { typeof(Valve.VR.VREvent_t).MakeByRefType(), typeof(uint) });
            if (poll == null) throw new MissingMemberException("CVRSystem.PollNextEvent not found");

            harmony.Patch(poll, postfix: new HarmonyMethod(typeof(SteamVRRecenter), nameof(PollNextEventPostfix)));
        }

        // Runs for every event Valheim pulls from SteamVR. Doesn't consume or change anything.
        public static void PollNextEventPostfix(bool __result, ref Valve.VR.VREvent_t __0)
        {
            if (!__result) return;
            uint type = __0.eventType;

            // One line per distinct event type per session, so the log shows what the Frame's recenter actually sends.
            if (PostureHooks.LogSteamVREvents.Value && seenEvents.Add(type))
                Plugin.Log.LogInfo($"SteamVR event seen: {(Valve.VR.EVREventType)type} ({type})");

            if (type == SeatedZeroPoseReset || type == StandingZeroPoseReset || type == ChaperoneUniverseHasChanged)
                pendingEvent = type;
        }

        // Called from PostureHooks.Tick on the main thread.
        internal static void Tick()
        {
            uint ev = pendingEvent;
            if (ev != 0)
            {
                pendingEvent = 0;
                dueEvent = ev;
                dueAt = Time.unscaledTime + SettleSeconds;
                LastEventAt = Time.unscaledTime;
            }

            if (dueAt < 0f || Time.unscaledTime < dueAt) return;

            try
            {
                if (!PostureHooks.RecalibrateOnSteamVRRecenter.Value || !(vrPlayerInstance.Invoke(null, null) is UnityEngine.Object vrp) || vrp == null)
                {
                    dueAt = -1f; // VHVR not running yet (menus, loading); it measures again when it starts
                    return;
                }
                if (shouldPause != null && (bool)shouldPause.Invoke(null, null)) return; // SteamVR menu still open: wait

                dueAt = -1f;
                if (Time.unscaledTime - lastRecenter < MinSecondsBetween) return;
                lastRecenter = Time.unscaledTime;

                // Same steps as VHVR's SteamVRBackend.tryRecenter, without its XR TryRecenter (SteamVR already did that).
                requestRecentering.Invoke(null, new object[] { true });
                requestPelvis?.Invoke(null, null);
                resetRoomscaleCamera?.Invoke(vrp, null);

                Plugin.Log.LogInfo($"Height: SteamVR recentered ({(Valve.VR.EVREventType)dueEvent}); VHVR is re-measuring your eye height and re-centring your view.");
            }
            catch (Exception e)
            {
                dueAt = -1f;
                if (!warnedError)
                {
                    warnedError = true;
                    Plugin.Log.LogWarning($"Height: couldn't recenter VHVR after SteamVR's recenter: {e}");
                }
            }
        }
    }
}
