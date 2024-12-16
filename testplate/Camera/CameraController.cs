﻿using System;
using System.Collections.Generic;
using System.Reflection;
using CameraMod.Button;
using CameraMod.Button.Buttons;
using CameraMod.Camera.Comps;
using CameraMod.Camera.Pages;
using Cinemachine;
using GorillaLocomotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//using BepInEx;

#pragma warning disable CS0618
namespace CameraMod.Camera {
    public enum UpdateMode {
        Patch,
        Update,
        LateUpdate
    }

    public class CameraController : MonoBehaviour {
        public enum TpvModes {
            Back,
            Front
        }

        public static CameraController Instance;

        public static UpdateMode UpdateMode = UpdateMode.Patch;
        public static bool BindEnabled = true;
        
        public Transform cameraTabletT;
        public Transform cameraFollowerT;
        public Transform tpvBodyFollowerT;
        
        public Transform thirdPersonCameraT;
        public Transform fakeWebCamT;
        public Transform tabletCameraT;
        
        public MainPage mainPage;
        public MiscPage miscPage;
        
        public GameObject colorScreenGo;
        private readonly List<BaseButton> buttons = new List<BaseButton>();
        public List<Material> screenMats = new List<Material>();
        public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

        public UnityEngine.Camera tabletCamera;
        public UnityEngine.Camera thirdPersonCamera;

        public bool followheadrot = true;
        public bool isFaceCamera;
        
        public bool tpv;
        public bool fpv = true;
        public bool fp;
        
        public float minDist = 2f;
        public float fpspeed = 0.01f;
        public float smoothing = 0.07f;
        public TpvModes tpvMode = TpvModes.Back;
        
        private bool init;
        private Vector3 velocity = Vector3.zero;

        private void Awake() {
            Instance = this;
        }

        private void Update() {
            if (UpdateMode == UpdateMode.Update) AnUpdate();
        }

        private void LateUpdate() {
            if (UpdateMode == UpdateMode.LateUpdate) AnUpdate();
        }

        public void SetNearClip(float val) {
            var tabletCam = tabletCamera;
            tabletCam.nearClipPlane = val;
            if (tabletCam.nearClipPlane < 0.01) {
                tabletCam.nearClipPlane = 1f;
                thirdPersonCamera.nearClipPlane = 1f;
            }
            if (tabletCam.nearClipPlane > 1.0) {
                tabletCam.nearClipPlane = 0.01f;
                thirdPersonCamera.nearClipPlane = 0.01f;
            }

            thirdPersonCamera.nearClipPlane = tabletCamera.nearClipPlane;
            mainPage.NearClipText.text = tabletCamera.nearClipPlane.ToString("#.##");
        }

        public void ChangeNearClip(float diff) {
            SetNearClip(tabletCamera.nearClipPlane + diff);
            PlayerPrefs.SetFloat("CameraNearClip", tabletCamera.nearClipPlane);
        }

        private const float MIN_SMOOTHING = 0.01f;
        private const float MAX_SMOOTHING = 1f;

        public void SetSmoothing(float val) {
            smoothing = val;
            if (smoothing < MIN_SMOOTHING) smoothing = MIN_SMOOTHING;
            if (smoothing > MAX_SMOOTHING) smoothing = MAX_SMOOTHING;
            mainPage.SmoothText.text = smoothing.ToString("#.##");
        }
        public void ChangeSmoothing(float change) {
            SetSmoothing(smoothing + change);
            PlayerPrefs.SetFloat("CameraSmoothing", smoothing);
        }
        public void ChangeFov(float difference) {
            var controller = Instance;

            var max = 130;
            var min = 20;

            var newFov = Mathf.Clamp(controller.tabletCamera.fieldOfView + difference, min, max);
            SetFov(newFov);
            PlayerPrefs.SetInt("CameraFov", (int) newFov);
        }

        public void SetFov(float fov) {
            var newFov = fov;
            tabletCamera.fieldOfView = newFov;
            thirdPersonCamera.fieldOfView = newFov;
            mainPage.FOVText.text = tabletCamera.fieldOfView.ToString("#.##");
        }
        
        public void Init() {
            var tagger = GorillaTagger.Instance;
            
            gameObject.AddComponent<InputManager>().gameObject.AddComponent<UI>();
            var assetsPath = Assembly.GetExecutingAssembly().GetName().Name + ".Camera.Assets";
            Debug.Log(assetsPath);
            colorScreenGo = LoadBundle("ColorScreen", assetsPath + ".colorscreen");
            cameraTabletT = LoadBundle("CameraTablet", assetsPath + ".pokrukcam").transform;

            thirdPersonCameraT = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera").transform;
            
            GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera/CM vcam1")
                    .GetComponent<CinemachineVirtualCamera>()
                    .enabled = false;
            
            tpvBodyFollowerT = tagger.bodyCollider.gameObject.transform;


            thirdPersonCamera = thirdPersonCameraT.GetComponent<UnityEngine.Camera>();

            cameraTabletT.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            cameraFollowerT =
                GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/Camera Follower")
                        .transform;


            tabletCameraT = cameraTabletT.Find("Camera");
            tabletCamera = tabletCameraT.GetComponent<UnityEngine.Camera>();
            
            fakeWebCamT = cameraTabletT.Find("FakeCamera");
            cameraTabletT.Find("LeftGrabCol").AddComponent<LeftGrabTrigger>();
            cameraTabletT.Find("RightGrabCol").AddComponent<RightGrabTrigger>();
            mainPage = new MainPage(cameraTabletT.Find("MainPage"));
            miscPage = new MiscPage(cameraTabletT.Find("MiscPage"));
            
            RegisterButtons();
            
            thirdPersonCameraT.SetParent(cameraTabletT, true);
            cameraTabletT.position = new Vector3(-65, 12, -82);
            var tabletT = tabletCamera.transform;
            thirdPersonCameraT.position = tabletT.position;
            thirdPersonCameraT.rotation = tabletT.rotation;
            cameraTabletT.Rotate(0, 180, 0);

            
            
            void SetColor(Color color) {
                foreach (var mat in screenMats) mat.color = color;
            }
            
            var colorButtonsT = GameObject.Find("ColorScreen(Clone)/Stuff").transform;
            Button(colorButtonsT.Find("RedButton"), () => SetColor(Color.red));
            Button(colorButtonsT.Find("GreenButton"), () => SetColor(Color.green));
            Button(colorButtonsT.Find("BlueButton"), () => SetColor(Color.blue));
            
            new [] {
                "ColorScreen(Clone)/Screen1",
                "ColorScreen(Clone)/Screen2",
                "ColorScreen(Clone)/Screen3"
            }.ForEach(screenMatPath=>
                screenMats.Add(
                    GameObject.Find(screenMatPath)
                    .GetComponent<MeshRenderer>()
                    .material)
            );

            new[] { "FakeCamera", "Tablet", "Handle", "Handle2" }.ForEach(meshPath => {
                meshRenderers.Add(cameraTabletT.Find(meshPath).GetComponent<MeshRenderer>());
            });

            colorScreenGo.transform.position = new Vector3(-54.3f, 16.21f, -122.96f);
            colorScreenGo.transform.Rotate(0, 30, 0);
            colorScreenGo.SetActive(false);
            miscPage.GO.SetActive(false);
            thirdPersonCamera.nearClipPlane = 0.1f;
            tabletCamera.nearClipPlane = 0.1f;
            fakeWebCamT.Rotate(-180, 180, 0);
            init = true;
            
            var fov = PlayerPrefs.GetInt("CameraFov", 100);
            SetFov(fov);
            
            var newSmoothing = PlayerPrefs.GetFloat("CameraSmoothing", 0.07f);
            SetSmoothing(newSmoothing);
            Binds.Init();
        }
        
        public void Flip() {
            isFaceCamera = !isFaceCamera;
            thirdPersonCameraT.Rotate(0.0f, 180f, 0.0f);
            tabletCameraT.Rotate(0.0f, 180f, 0.0f);
            fakeWebCamT.Rotate(-180f, 180f, 0.0f);
        }

        private float lastPageChangedTime;
        private readonly float pageChangeButtonsTimeout = 0.2f;
        public HeadCosmeticsHider HeadCosmeticsHider;

        public bool ButtonsTimeouted => Time.time - lastPageChangedTime < pageChangeButtonsTimeout;

        public void EnableFPV() {
            if (isFaceCamera) {
                Flip();
            }

            fp = false;
            fpv = true;
            UI.Instance.freecam = false;
        }
        
        public void RegisterButtons() {
            AddTabletButton("MiscPage/BackButton", () => {
                mainPage.GO.SetActive(true);
                miscPage.GO.SetActive(false);
                lastPageChangedTime = Time.time;
            });
            
            AddTabletButton("MainPage/MiscButton", () => {
                mainPage.GO.SetActive(false);
                miscPage.GO.SetActive(true);
                lastPageChangedTime = Time.time;
            });
            
            AddTabletButton("MainPage/FPVButton", () => {
                EnableFPV();
            });
            
            AddHoldableTabletButton("MainPage/SmoothingDownButton", () => ChangeSmoothing(-0.01f));
            AddHoldableTabletButton("MainPage/SmoothingUpButton", () => ChangeSmoothing(0.01f));
            
            AddTabletButton("MainPage/FovUP", () => ChangeFov(5));
            AddTabletButton("MainPage/FovDown", () => ChangeFov(-5));
            
            AddTabletButton("MainPage/NearClipUp", () => ChangeNearClip(0.01f));
            AddTabletButton("MainPage/NearClipDown", () => ChangeNearClip(-0.01f));
            
            AddTabletButton("MainPage/FlipCamButton", () => {
                Flip();
            });
            
            AddTabletButton("MainPage/FPButton", () => fp = !fp);

            HeadCosmeticsHider = thirdPersonCameraT.AddComponent<HeadCosmeticsHider>();
            HeadCosmeticsHider.enabled = PlayerPrefs.GetInt("HeadCosmeticsHide", 0) == 1;
            AddTabletButton("MainPage/HideHeadCosmetics", () => {
                HeadCosmeticsHider.enabled = !HeadCosmeticsHider.enabled;
                PlayerPrefs.SetInt("HeadCosmeticsHide", HeadCosmeticsHider.enabled ? 1 : 0);
            });
            
            RollLock = PlayerPrefs.GetInt("RollLock", 1) == 1;
            AddTabletButton("MainPage/RollLock", () => {
                RollLock = !RollLock;
                PlayerPrefs.SetInt("RollLock", RollLock ? 1 : 0);
            });
            
            AddTabletButton("MainPage/TPVButton", () => {
                if (tpvMode == TpvModes.Back) {
                    if (isFaceCamera) {
                        Flip();
                    }
                } else if (tpvMode == TpvModes.Front) {
                    if (!isFaceCamera) {
                        Flip();
                    }
                }

                fp = false;
                fpv = false;
                tpv = true;
            });
            
            AddTabletButton("MiscPage/MinDistDownButton", () => {
                minDist -= 0.1f;
                if (minDist < 1) minDist = 1;
                miscPage.MinDistText.text = minDist.ToString("#.##");
            });
            AddTabletButton("MiscPage/MinDistUpButton", () => {
                minDist += 0.1f;
                if (minDist > 10) minDist = 10;
                miscPage.MinDistText.text = minDist.ToString("#.##");
            });
            AddTabletButton("MiscPage/SpeedUpButton", () => {
                fpspeed += 0.01f;
                if (fpspeed > 0.1) fpspeed = 0.1f;
                miscPage.SpeedText.text = fpspeed.ToString("#.##");
            });
            AddTabletButton("MiscPage/SpeedDownButton", () => {
                fpspeed -= 0.01f;
                if (fpspeed < 0.01) fpspeed = 0.01f;
                miscPage.SpeedText.text = fpspeed.ToString("#.##");
            });
            AddTabletButton("MiscPage/TPModeDownButton", () => {
                if (tpvMode == TpvModes.Back)
                    tpvMode = TpvModes.Front;
                else
                    tpvMode = TpvModes.Back;
                miscPage.TpText.text = tpvMode.ToString();
            });
            AddTabletButton("MiscPage/TPModeUpButton", () => {
                if (tpvMode == TpvModes.Back)
                    tpvMode = TpvModes.Front;
                else
                    tpvMode = TpvModes.Back;
                miscPage.TpText.text = tpvMode.ToString();
            });
            AddTabletButton("MiscPage/TPRotButton", () => {
                followheadrot = !followheadrot;
                miscPage.TpRotText.text = followheadrot.ToString().ToUpper();
            });
            AddTabletButton("MiscPage/TPRotButton1", () => {
                followheadrot = !followheadrot;
                miscPage.TpRotText.text = followheadrot.ToString().ToUpper();
            });

            var greenScreenButtonT = cameraTabletT.Find("MiscPage/GreenScreenButton");
            var colorScreenText = greenScreenButtonT.transform.Find("Text").GetComponent<TextMeshPro>();
            AddTabletButton(greenScreenButtonT, () => {
                colorScreenGo.active = !colorScreenGo.active;
                if (colorScreenGo.active)
                    colorScreenText.text = "GREEN SCREEN\n(ENABLED)";
                else
                    colorScreenText.text = "GREEN SCREEN\n(DISABLED)";
            });
        }

        public ClickButton Button(string buttonPath, Action onClick) {
            return Button(GameObject.Find(buttonPath), onClick);
        }
        public ClickButton Button(GameObject go, Action onClick) {
            var button = go.AddComponent<ClickButton>();
            button.OnClick(onClick);
            return button;
        }
        public ClickButton Button(Transform t, Action onClick) {
            var button = t.AddComponent<ClickButton>();
            button.OnClick(onClick);
            return button;
        }
        
        public void AddTabletButton(Transform t, Action onClick) {
            var button = t.AddComponent<ClickButton>();
            button.OnClick(onClick);
            buttons.Add(button);
        }
        public void AddTabletButton(string relativeButtonPath, Action onClick) {
            buttons.Add(Button(cameraTabletT.Find(relativeButtonPath), onClick));
        }
        public void AddHoldableTabletButton(string buttonPath, Action onClick) {
            buttons.Add(cameraTabletT.Find(buttonPath)
                    .AddComponent<HoldableButton>()
                    .OnClick(onClick)
            );
        }

        public void SetTabletVisibility(bool visible) {
            foreach (var mr in meshRenderers) mr.enabled = visible;
            tabletCamera.enabled = visible;
        }
        
        public static bool RollLock = true;
        public void AnUpdate() {
            if (!init) return;

            if (fpv) {
                if (mainPage.GO.active) {
                    SetTabletVisibility(false);
                    mainPage.GO.active = false;
                }
                var camera = cameraTabletT;
                var follower = cameraFollowerT;
                
                
                camera.position = follower.position;
                
                var newRotation = camera.rotation.Lerped(follower.rotation, smoothing);
                if (RollLock) {
                    newRotation = newRotation.eulerAngles.Scaled(new Vector3(1,1,0)).ToQuaternion();
                }
                camera.rotation = newRotation;
            }

            if (BindEnabled && Binds.Tablet() && cameraTabletT.parent == null) {
                fp = false;
                fpv = false;
                tpv = false;
                if (!mainPage.GO.active) {
                    foreach (var btns in buttons) btns.gameObject.SetActive(true);
                    SetTabletVisibility(true);

                    mainPage.GO.active = true;
                }

                var headTransform = Player.Instance.headCollider.transform;
                var headPos = headTransform.position;
                cameraTabletT.position = headPos + headTransform.forward;
                cameraTabletT.LookAt(headPos);
                cameraTabletT.Rotate(0f, -180f, 0f);
            }

            if (fp) {
                cameraTabletT.LookAt(2f * cameraTabletT.position - cameraFollowerT.position);
                if (!isFaceCamera) {
                    Flip();
                }

                var dist = Vector3.Distance(cameraFollowerT.position, cameraTabletT.position);
                if (dist > minDist)
                    cameraTabletT.position = Vector3.Lerp(
                        cameraTabletT.position,
                        cameraFollowerT.position, 
                        fpspeed);
            }

            if (tpv) {
                if (mainPage.GO.active) {
                    SetTabletVisibility(false);
                    mainPage.GO.active = false;
                }
                
                Vector3 targetPosition;
                switch (tpvMode) {
                    case TpvModes.Back: {
                        if (followheadrot)
                            targetPosition = cameraFollowerT.TransformPoint(new Vector3(0.3f, 0.1f, -1.5f));
                        else
                            targetPosition = tpvBodyFollowerT.TransformPoint(new Vector3(0.3f, 0.1f, -1.5f));
                        cameraTabletT.position = Vector3.SmoothDamp(cameraTabletT.position,
                            targetPosition, ref velocity, 0.1f);
                        cameraTabletT.LookAt(cameraFollowerT.position);
                        break;
                    }
                    case TpvModes.Front: {
                        if (followheadrot)
                            targetPosition = cameraFollowerT.TransformPoint(new Vector3(0.1f, 0.3f, 2.5f));
                        else
                            targetPosition = tpvBodyFollowerT.TransformPoint(new Vector3(0.1f, 0.3f, 2.5f));
                        cameraTabletT.position = Vector3.SmoothDamp(cameraTabletT.position,
                            targetPosition, ref velocity, 0.1f);
                        cameraTabletT.LookAt(2f * cameraTabletT.position - cameraFollowerT.position);
                        break;
                    }
                }

                if (Binds.Tablet()) {
                    var headT = Player.Instance.headCollider.transform;
                    
                    cameraTabletT.position = headT.position + headT.forward;
                    cameraTabletT.LookAt(headT.position);
                    cameraTabletT.Rotate(0f, -180f, 0f);
                    
                    SetTabletVisibility(true);
                    cameraTabletT.parent = null;
                    tpv = false;
                }
            }
        }

        private GameObject LoadBundle(string goname, string resourcename) {
            var str = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcename);
            var asb = AssetBundle.LoadFromStream(str);
            var go = Instantiate(asb.LoadAsset<GameObject>(goname));
            asb.Unload(false);
            str.Close();
            return go;
        }
    }
}