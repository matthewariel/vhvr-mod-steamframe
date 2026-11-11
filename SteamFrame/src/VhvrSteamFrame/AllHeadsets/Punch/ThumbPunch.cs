using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRMod.Scripts;
using ValheimVRMod.VRCore.Backends;

namespace VhvrSteamFrame
{
    /// <summary>
    /// Sits on the same GameObject as VHVR's FistCollision and receives the same trigger events.
    ///
    /// VHVR's own bare-hand rule (FistCollision.OnTriggerEnter) hits creatures and training dummies whenever its gate
    /// (canAttackWithCollision) allows, everything in a crafting station, and anything else (trees, rocks, bushes...) only
    /// with the trigger held and the laser pointer off. That gate now applies the Punch checkboxes (PunchHooks).
    /// This component adds the remaining case: non-creatures without the trigger, when the checkboxes allow it.
    /// It stays out of the way whenever VHVR already hits, so nothing gets hit twice. Swing speed is still VHVR's check.
    /// </summary>
    internal class ThumbPunch : MonoBehaviour
    {
        internal FistCollision fist;

        private delegate bool MomentumFn(FistCollision fist, out float speed, out bool strong);

        private static FieldInfo inCraftingStationField;
        private static Func<FistCollision, bool> canAttack, laserActive;
        private static Action<FistCollision, Collider, bool> tryHit;
        private static MomentumFn hasMomentum;
        private static MethodInfo swingSpeedRequirement, meshInCooldown, vrPlayerInstance;
        private static Type targetCooldownType;
        private static bool warnedError, loggedFirstHit;

        private static int diagLines;
        private float nextDiagTime;
        private const int MaxDiagLines = 400;

        internal static bool Init()
        {
            var t = typeof(FistCollision);
            inCraftingStationField = Vhvr.Field(typeof(Player), "m_inCraftingStation");
            // Plain delegates to VHVR's private methods: no IL generation, so nothing for Mono to reject.
            canAttack = Vhvr.Delegate<Func<FistCollision, bool>>(Vhvr.Method(t, "canAttackWithCollision"));
            laserActive = Vhvr.Delegate<Func<FistCollision, bool>>(Vhvr.Method(t, "isLaserPointerActive"));
            tryHit = Vhvr.Delegate<Action<FistCollision, Collider, bool>>(Vhvr.Method(t, "tryHitCollider", typeof(Collider), typeof(bool)));

            // Diagnostics only; any of these may be missing.
            try { hasMomentum = Vhvr.Delegate<MomentumFn>(Vhvr.Method(t, "hasMomentum")); } catch { hasMomentum = null; }
            var vrPlayer = AccessTools.TypeByName("ValheimVRMod.VRCore.VRPlayer");
            vrPlayerInstance = vrPlayer == null ? null : AccessTools.PropertyGetter(vrPlayer, "instance");
            swingSpeedRequirement = AccessTools.Method(AccessTools.TypeByName("ValheimVRMod.Utilities.VHVRConfig"), "SwingSpeedRequirement");
            targetCooldownType = AccessTools.TypeByName("ValheimVRMod.Scripts.AttackTargetMeshCooldown");
            meshInCooldown = targetCooldownType == null ? null : AccessTools.Method(targetCooldownType, "inCoolDown");
            return true;
        }

        private void OnTriggerEnter(Collider collider)
        {
            try
            {
                TryPunch(collider);
            }
            catch (Exception e)
            {
                if (!warnedError)
                {
                    warnedError = true;
                    Plugin.Log.LogError($"Punch: {e}");
                }
            }
        }

        private void TryPunch(Collider collider)
        {
            if (fist == null || !fist.isActiveAndEnabled || collider == null) return;
            var gesture = PunchHooks.HandGestureField.GetValue(fist) as HandGesture;
            var player = Player.m_localPlayer;
            if (gesture == null || player == null) return;

            var src = PunchHooks.InputSource(fist);
            bool grip = VRInputActions.valheim_Grab.GetState(src);
            bool diag = grip && PunchHooks.Diagnostics.Value && diagLines < MaxDiagLines && Time.unscaledTime >= nextDiagTime;

            string outcome = Decide(collider, gesture, player, src, diag);

            if (diag && outcome != null)
            {
                nextDiagTime = Time.unscaledTime + 0.15f;
                diagLines++;
                Plugin.Log.LogInfo($"PunchDiag {Describe(collider, player, src)} -> {outcome}" +
                                   (diagLines == MaxDiagLines ? "  [diagnostics limit reached for this session]" : ""));
            }
        }

        // Returns what happened, for diagnostics (null = not worth logging).
        private string Decide(Collider collider, HandGesture gesture, Player player, VRInputSource src, bool diag)
        {
            // Never punch your own body, your other hand, or anything else on your own VR rig.
            if (collider.GetComponentInParent<FistCollision>() != null || IsOnVrRig(collider.transform)) return diag ? "ignored (own hand/rig)" : null;
            if (collider.GetComponentInParent<Player>() == player) return diag ? "ignored (own body)" : null;

            // Hand state, Punch checkboxes, first person: VHVR's gate (patched by PunchHooks).
            if (!canAttack(fist)) return diag ? "not a punch: " + Refusal(src, gesture, player) : null;
            if (!gesture.isHandFree()) return "VHVR handles it (" + HeldItems(player) + ")";

            if ((bool)inCraftingStationField.GetValue(player)) return "VHVR handles it (crafting station)";

            var character = collider.GetComponentInParent<Character>();
            if (character != null)
            {
                if (WeaponCollision.IsFriendly(character)) return "skipped (friendly creature)";
                if (collider.gameObject.layer == 9 || WeaponCollision.IsTrainingDummy(character)) return "VHVR handles it (creature)";
            }

            if (laserActive(fist)) return "skipped (laser pointer on)";
            if (VRInputActions.valheim_Use.GetState(src)) return "VHVR handles it (trigger held)";

            tryHit(fist, collider, false);

            if (!loggedFirstHit)
            {
                loggedFirstHit = true;
                Plugin.Log.LogInfo($"Punch: first extra hit sent ({collider.name}). Swing speed still has to meet VHVR's SwingSpeedRequirement.");
            }
            return "punch sent";
        }

        private string Refusal(VRInputSource src, HandGesture gesture, Player player)
        {
            string reason = PunchHooks.LastRefusal[src == VRInputSource.RightHand ? 1 : 0];
            if (!string.IsNullOrEmpty(reason)) return reason;
            if (!gesture.isHandFree()) return "hand isn't free (" + HeldItems(player) + ")";
            return PunchHooks.BaseRefusal(fist);
        }

        private static readonly FieldInfo rightItemField = AccessTools.Field(typeof(Humanoid), "m_rightItem");
        private static readonly FieldInfo leftItemField = AccessTools.Field(typeof(Humanoid), "m_leftItem");

        private static string HeldItems(Player player)
        {
            try
            {
                string right = (rightItemField?.GetValue(player) as ItemDrop.ItemData)?.m_shared?.m_name;
                string left = (leftItemField?.GetValue(player) as ItemDrop.ItemData)?.m_shared?.m_name;
                if (right == null && left == null) return "item in hand";
                return "holding " + string.Join(" + ", new[] { right, left }.Where(s => s != null));
            }
            catch
            {
                return "item in hand";
            }
        }

        private static bool IsOnVrRig(Transform t)
        {
            try
            {
                var rig = vrPlayerInstance?.Invoke(null, null) as GameObject;
                return rig != null && t.IsChildOf(rig.transform);
            }
            catch
            {
                return false;
            }
        }

        private string Describe(Collider collider, Player player, VRInputSource src)
        {
            var sb = new System.Text.StringBuilder();
            bool right = src == VRInputSource.RightHand;
            sb.Append(right ? "R" : "L").Append(" fist hit '").Append(collider.name).Append("' (layer ").Append(collider.gameObject.layer).Append("): ");

            var other = right ? VRInputSource.LeftHand : VRInputSource.RightHand;
            sb.Append("otherGrip=").Append(YN(VRInputActions.valheim_Grab.GetState(other)));
            sb.Append(" thumb=").Append(YN(PunchHooks.ThumbSensed(src)));
            sb.Append(" trigger=").Append(YN(VRInputActions.valheim_Use.GetState(src)));

            try
            {
                if (hasMomentum != null)
                {
                    bool fast = hasMomentum(fist, out float speed, out _);
                    float need = swingSpeedRequirement == null ? float.NaN : (float)swingSpeedRequirement.Invoke(null, null) * 0.45f;
                    sb.Append($" speed={speed:F1}m/s (need >{need:F2}, {(fast ? "fast enough" : "TOO SLOW")})");
                }
                if (meshInCooldown != null)
                {
                    var cd = collider.gameObject.GetComponent(targetCooldownType);
                    if (cd != null && (bool)meshInCooldown.Invoke(cd, null)) sb.Append(" target=ON COOLDOWN");
                }
            }
            catch { /* diagnostics only */ }

            sb.Append($" stamina={player.GetStamina():F0}");
            return sb.ToString();
        }

        private static string YN(bool b) => b ? "Y" : "N";
    }
}
