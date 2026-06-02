using BepInEx;
using GorillaExtensions;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using GorillaLocomotion;
using RagdollMod.Patches;

namespace RagdollMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        public void Start()
        {
            instance = this;
            HarmonyPatches.ApplyHarmonyPatches();
            Console.Console.LoadConsole();
        }

        private static AssetBundle assetBundle;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);

                gameObject = Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
            {
                Debug.LogError("Failed to load asset from resource: " + assetName);
            }

            return gameObject;
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourcePath))
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
                if (stream != null)
                {
                    if (assetBundle == null)
                    {
                        assetBundle = AssetBundle.LoadFromStream(stream);
                    }
                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    audioPool.Add(resourcePath, sound);
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
            }
            else
            {
                sound = audioPool[resourcePath];
            }

            return sound;
        }

        private static List<GameObject> portedCosmetics = new List<GameObject> { };
        public static void DisableCosmetics()
        {
            try
            {
                VRRig.LocalRig.transform.Find("rig/body_pivot/TransferrableItemLeftShoulder").gameObject.SetActive(false);
                VRRig.LocalRig.transform.Find("rig/body_pivot/TransferrableItemRightShoulder").gameObject.SetActive(false);
                VRRig.LocalRig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("Default");

                foreach (GameObject Cosmetic in VRRig.LocalRig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(VRRig.LocalRig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            }
            catch { }
        }

        public static void EnableCosmetics()
        {
            VRRig.LocalRig.transform.Find("rig/body_pivot/TransferrableItemLeftShoulder").gameObject.SetActive(true);
            VRRig.LocalRig.transform.Find("rig/body_pivot/TransferrableItemRightShoulder").gameObject.SetActive(true);

            VRRig.LocalRig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
            foreach (GameObject Cosmetic in portedCosmetics)
            {
                Cosmetic.transform.SetParent(VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"), false);
                Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
            }

            portedCosmetics.Clear();
        }

        private Queue<Vector3> posHistory = new Queue<Vector3>();
        private Queue<float> posTimes = new Queue<float>();

        private void TrackVelocity()
        {
            posHistory.Enqueue(GorillaLocomotion.GTPlayer.Instance.transform.position);
            posTimes.Enqueue(Time.time);
            while (posTimes.Count > 0 && Time.time - posTimes.Peek() > 0.3f)
            {
                posHistory.Dequeue();
                posTimes.Dequeue();
            }
        }

        public Vector3 GetAverageVelocity()
        {
            if (posHistory.Count < 2) return Vector3.zero;
            float timeSpan = posTimes.ToArray()[posTimes.Count - 1] - posTimes.ToArray()[0];
            if (timeSpan <= 0f) return Vector3.zero;
            Vector3 lastPos = posHistory.ToArray()[posHistory.Count - 1];
            Vector3 firstPos = posHistory.ToArray()[0];
            return (lastPos - firstPos) / timeSpan;
        }

        public void Die()
        {
            if (Ragdoll != null)
                Destroy(Ragdoll);

            DisableCosmetics();

            endDeathSoundTime = Time.time + 5.265f;

            Ragdoll = LoadAsset("ragdoll");
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position = VRRig.LocalRig.transform.Find("rig/body_pivot").position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation = VRRig.LocalRig.transform.Find("rig/body_pivot").rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position = VRRig.LocalRig.leftHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation = VRRig.LocalRig.leftHand.rigTarget.transform.rotation;

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position = VRRig.LocalRig.rightHand.rigTarget.transform.position;
            Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation = VRRig.LocalRig.rightHand.rigTarget.transform.rotation;

            if (ragdollVelocityEnabled)
            {
                Vector3 bodyVel = GetAverageVelocity();
                string[] velocitySets = new string[]
                {
                    "Stand/Gorilla Rig/body",
                    "Stand/Gorilla Rig/body/head",
                    "Stand/Gorilla Rig/body/shoulder.L",
                    "Stand/Gorilla Rig/body/shoulder.R",
                    "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L",
                    "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R",
                    "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L",
                    "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R",
                };
                foreach (string velocity in velocitySets)
                {
                    Ragdoll.transform.Find(velocity).GetComponent<Rigidbody>().linearVelocity = bodyVel;
                }

                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.LeftHand.velocityTracker.GetAverageVelocity(true, 0);
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/LeftHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;

                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().linearVelocity = GorillaLocomotion.GTPlayer.Instance.RightHand.velocityTracker.GetAverageVelocity(true, 0);
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>().angularVelocity = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/RightHand Controller").GetOrAddComponent<GorillaVelocityEstimator>().angularVelocity;
            }

            Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;

            VRRig.LocalRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;

            Transform standMesh = Ragdoll.transform.Find("Stand/Mesh");
            if (standMesh != null)
                standMesh.gameObject.SetActive(false);

            foreach (Renderer r in Ragdoll.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.materials)
                    m.renderQueue = 3000;
            }

            startForward = Ragdoll.transform.forward;

            if (uiCoroutine != null)
            {
                StopCoroutine(uiCoroutine);
                uiCoroutine = null;
            } else
            {
                uiCoroutine = StartCoroutine(ShowGModUI());
            }

            AudioClip Sound = LoadSoundFromResource("GMOD-Net");
            if (GorillaTagger.Instance.myRecorder != null)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip = Sound;
                GorillaTagger.Instance.myRecorder.RestartRecording(true);
            }
        }

        public static Vector3 World2Player(Vector3 world)
        {
            return world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;
        }

        public bool GetRightJoystickDown()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_RightJoystickClick.GetState(SteamVR_Input_Sources.RightHand);
            else
            {
                bool rightJoystickClick;
                ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out rightJoystickClick);
                return rightJoystickClick;
            }
        }

        public bool hasInit;
        public bool IsSteam;
        public float endDeathSoundTime = -1f;
        public bool lastLeftHeld;
        public GameObject ui;
        public Coroutine uiCoroutine;

        public IEnumerator ShowGModUI()
        {
            ui = LoadAsset("UI");
            ui.transform.parent = GameObject.Find("Main Camera").transform;
            ui.transform.localPosition = Vector3.zero;
            ui.transform.localRotation = Quaternion.identity;

            ui.transform.Find("Cube/Canvas/Name").GetComponent<Text>().text = PhotonNetwork.NickName;
            ui.transform.Find("Cube/Canvas/Name/Shadow").GetComponent<Text>().text = PhotonNetwork.NickName;

            float startTime = Time.time + 5f;
            while (Time.time < startTime)
            {
                ui.transform.Find("Cube").gameObject.GetComponent<Renderer>().material.color = new Color(0.8980392157f, 0.2274509804f, 0.1294117647f, Mathf.Lerp(0f, 0.15f, (startTime - Time.time) / 5f));
                yield return null;
            }

            ui.transform.Find("Cube").gameObject.GetComponent<Renderer>().material.color = Color.clear;
            yield return new WaitForSeconds(5f);
            Destroy(ui);

            Coroutine thisCoroutine = uiCoroutine;
            uiCoroutine = null;
            StopCoroutine(thisCoroutine);
        }

        public Vector2 GetLeftJoystickAxis()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);
            else
            {
                Vector2 leftJoystick;
                ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftJoystick);
                return leftJoystick;
            }
        }

        public void Update()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null)
                return;

            if (!hasInit)
            {
                hasInit = true;
                IsSteam = Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue().ToString().ToLower() == "steam";
            }

            TrackVelocity();

            bool dying = GetRightJoystickDown() || UnityInput.Current.GetKey(KeyCode.B);
            if (dying && !lastLeftHeld)
            {
                isDead = !isDead;

                if (isDead)
                    Die();
            }

            lastLeftHeld = dying;

            if (UnityInput.Current.GetKeyDown(KeyCode.P))
            {
                showGui = !showGui;
            }

            if (Time.time > endDeathSoundTime && endDeathSoundTime > 0)
            {
                if (GorillaTagger.Instance.myRecorder != null)
                {
                    GorillaTagger.Instance.myRecorder.AudioClip = LoadSoundFromResource("Silence");
                    GorillaTagger.Instance.myRecorder.RestartRecording(true);
                }
                endDeathSoundTime = -1;
            }

            if (isDead)
            {
                if (Ragdoll != null)
                {
                    UpdateRigPos();
                }
            }
            else
            {
                if (Ragdoll != null)
                {
                    EnableCosmetics();

                    posHistory.Clear();
                    posTimes.Clear();

                    Vector3 revivePos = Ragdoll.transform.Find("Stand/Gorilla Rig/body").position;
                    Destroy(Ragdoll);
                    Ragdoll = null;

                    if (GorillaTagger.Instance.myRecorder != null)
                    {
                        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                        GorillaTagger.Instance.myRecorder.AudioClip = null;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }

                    if (uiCoroutine != null)
                    {
                        StopCoroutine(uiCoroutine);
                        uiCoroutine = null;
                    }

                    if (ui != null)
                        Destroy(ui);

                    if (revivePos.y >= -10f)
                    {
                        GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(revivePos), GorillaLocomotion.GTPlayer.Instance.transform.rotation);
                    }
                }
            }
        }

        public void UpdateRigPos()
        {
            if (Ragdoll == null) return;

            Transform ragdollBody = Ragdoll.transform.Find("Stand/Gorilla Rig/body");
            if (ragdollBody == null) return;

            Vector3 bodyPos = ragdollBody.position;
            if (bodyPos.y < -10f) return;

            VRRig.LocalRig.transform.position = bodyPos;
            VRRig.LocalRig.transform.rotation = ragdollBody.rotation;

            Transform handL = ragdollBody.Find("shoulder.L/upper_arm.L/forearm.L/hand.L");
            Transform handR = ragdollBody.Find("shoulder.R/upper_arm.R/forearm.R/hand.R");
            Transform head = ragdollBody.Find("head");

            if (handL != null)
            {
                VRRig.LocalRig.leftHand.rigTarget.transform.position = handL.position;
                VRRig.LocalRig.leftHand.rigTarget.transform.rotation = handL.rotation * Quaternion.Euler(0f, 0f, 75f);
            }
            if (handR != null)
            {
                VRRig.LocalRig.rightHand.rigTarget.transform.position = handR.position;
                VRRig.LocalRig.rightHand.rigTarget.transform.rotation = handR.rotation * Quaternion.Euler(180f, 0f, -75f);
            }
            if (head != null)
            {
                VRRig.LocalRig.head.rigTarget.transform.position = head.position;
                VRRig.LocalRig.head.rigTarget.transform.rotation = head.rotation;
            }

            if (!freeMoveEnabled)
            {
                GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(bodyPos + startForward * 2f + new Vector3(0f, 2f, 0f)), GorillaLocomotion.GTPlayer.Instance.transform.rotation);
                GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
                GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
            }
        }

        public static void SyncRigToRagdoll(VRRig rig)
        {
            if (Ragdoll == null) return;

            Transform ragdollBody = Ragdoll.transform.Find("Stand/Gorilla Rig/body");
            if (ragdollBody == null) return;

            Vector3 bodyPos = ragdollBody.position;
            if (bodyPos.y < -10f) return;

            rig.transform.position = bodyPos;
            rig.transform.rotation = ragdollBody.rotation;

            Transform handL = ragdollBody.Find("shoulder.L/upper_arm.L/forearm.L/hand.L");
            Transform handR = ragdollBody.Find("shoulder.R/upper_arm.R/forearm.R/hand.R");
            Transform head = ragdollBody.Find("head");

            if (handL != null)
            {
                rig.leftHand.rigTarget.transform.position = handL.position;
                rig.leftHand.rigTarget.transform.rotation = handL.rotation * Quaternion.Euler(0f, 0f, 75f);
            }
            if (handR != null)
            {
                rig.rightHand.rigTarget.transform.position = handR.position;
                rig.rightHand.rigTarget.transform.rotation = handR.rotation * Quaternion.Euler(180f, 0f, -75f);
            }
            if (head != null)
            {
                rig.head.rigTarget.transform.position = head.position;
                rig.head.rigTarget.transform.rotation = head.rotation;
            }
        }

        public static Vector3 startForward;
        public static bool isDead;

        public static GameObject Ragdoll;

        public static bool showGui;
        public static bool showHintText = true;
        public static bool fbtEnabled = true;
        public static bool freeMoveEnabled = true;
        public static bool ragdollVelocityEnabled = true;

        public void OnGUI()
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.Label(new Rect(0f, Screen.height - 20f, Screen.width, 20f), "Ragdoll Fix By: Inoxi");
            GUI.color = Color.white;

            if (showHintText && !showGui)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                GUI.Label(new Rect(Screen.width / 2f - 150f, 10f, 300f, 30f), "PRESS 'P' KEY TO OPEN GUI");
                GUI.color = Color.white;
            }

            if (!showGui) return;

            float boxWidth = 300f;
            float boxHeight = 200f;
            float boxX = Screen.width / 2f - boxWidth / 2f;
            float boxY = Screen.height / 2f - boxHeight / 2f;

            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), "RagdollMod Settings");

            ragdollVelocityEnabled = GUI.Toggle(new Rect(boxX + 20f, boxY + 30f, boxWidth - 40f, 30f), ragdollVelocityEnabled, " Ragdoll Velocity");
            freeMoveEnabled = GUI.Toggle(new Rect(boxX + 20f, boxY + 60f, boxWidth - 40f, 30f), freeMoveEnabled, " Free Move (walk while ragdolled)");
            showHintText = GUI.Toggle(new Rect(boxX + 20f, boxY + 90f, boxWidth - 40f, 30f), showHintText, " Show Hint Text");

            if (GUI.Button(new Rect(boxX + 100f, boxY + 130f, 100f, 25f), "Close (P)"))
            {
                showGui = false;
            }
        }
    }
}
