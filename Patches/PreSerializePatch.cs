using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RagdollMod.Patches
{
    [HarmonyPatch(typeof(PhotonNetwork), "RunViewUpdate")]
    public class PreSerialize
    {
        public static void Prefix()
        {
            if (Plugin.isDead && Plugin.Ragdoll != null)
            {
                Plugin.SyncRigToRagdoll(VRRig.LocalRig);
                if (!Plugin.freeMoveEnabled)
                {
                    Transform ragdollBody = Plugin.Ragdoll.transform.Find("Stand/Gorilla Rig/body");
                    if (ragdollBody != null)
                    {
                        GorillaTagger.Instance.transform.position = Plugin.World2Player(ragdollBody.position + (Plugin.startForward * 2f) + new Vector3(0f, 2f, 0f));
                        GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                        GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                    }
                }
            }
        }

        public static void Postfix()
        {
            if (Plugin.isDead && Plugin.Ragdoll != null)
            {
                Plugin.instance.UpdateRigPos();
            }
        }
    }
}
