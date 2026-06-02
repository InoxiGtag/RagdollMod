using HarmonyLib;
using UnityEngine;

namespace Atlas_Remade.Patches.Internal
{
    [HarmonyPatch(typeof(VRRig), "PostTick")]
    public class TorsoPatch
    {
        public static bool enabled = true;
        public static int mode;

        public static void Postfix(VRRig __instance)
        {
            if (!enabled) return;
            if (!__instance.isLocal) return;
            if (!RagdollMod.Plugin.isDead || RagdollMod.Plugin.Ragdoll == null) return;
            RagdollMod.Plugin.SyncRigToRagdoll(__instance);
            if (RagdollMod.Plugin.fbtEnabled)
            {
                ApplyBodyTracking(__instance);
            }
        }

        private static void ApplyBodyTracking(VRRig rig)
        {
            Vector3 headForward = rig.head.rigTarget.transform.forward;
            headForward.y = 0f;
            if (headForward.sqrMagnitude > 0.001f)
            {
                headForward.Normalize();
                Quaternion bodyRot = Quaternion.LookRotation(headForward, Vector3.up);
                rig.transform.rotation = Quaternion.Euler(
                    rig.transform.rotation.eulerAngles.x,
                    bodyRot.eulerAngles.y,
                    rig.transform.rotation.eulerAngles.z
                );
            }
        }
    }
}
