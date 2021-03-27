﻿using BepInEx;
using MonoMod.Cil;
using RoR2;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.UI;
using Mono.Cecil.Cil;
using RoR2.UI;
using BepInEx.Configuration;
using UnityEngine.XR;
using System.Collections;
using System;
using R2API.Utils;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: ManualNetworkRegistration]
namespace DrBibop
{
    [BepInPlugin("com.DrBibop.VRMod", "VRMod", "1.1.2")]
    public class VRMod : BaseUnityPlugin
    {
        private static readonly Vector3 menuPosition = new Vector3(0, 0, 15);
        private static readonly Vector3 characterSelectPosition = new Vector3(0, 0, 5);

        private static readonly Vector3 menuScale = new Vector3(0.01f, 0.01f, 0.01f);
        private static readonly Vector3 characterSelectScale = new Vector3(0.005f, 0.005f, 0.005f);

        private static readonly Vector2 menuPivot = new Vector2(0.5f, 0.5f);

        private static readonly Vector2 menuResolution = new Vector2(1500, 1000);
        private static readonly Vector2 hdResolution = new Vector2(1920, 1080);

        private static Camera uiCamera;

        private const string CONFIG_FILE_NAME = "VRMod.cfg";

        private new static readonly ConfigFile Config = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, CONFIG_FILE_NAME), true);
        public static ConfigEntry<bool> ConfigUseOculus { get; set; }

        private void Awake()
        {
            ConfigUseOculus = Config.Bind<bool>(
                "VR Settings",
                "Use Oculus mode",
                false,
                "Launches the game in Oculus mode if you don't like using SteamVR."
            );

            On.RoR2.UI.HUD.Awake += AdjustHUDAnchors;
            On.RoR2.CameraRigController.GetCrosshairRaycastRay += GetVRCrosshairRaycastRay;

            On.RoR2.RoR2Application.Awake += (orig, self) =>
            {
                orig(self);
                if (XRSettings.loadedDeviceName != (ConfigUseOculus.Value ? "Oculus" : "OpenVR"))
                    StartCoroutine(SetVRDevice());
            };

            On.RoR2.UI.MainMenu.BaseMainMenuScreen.OnEnter += (orig, self, controller) =>
            {
                orig(self, controller);
                SetRenderMode(self.gameObject, menuResolution, menuPosition, menuScale);
            };
            On.RoR2.UI.LogBook.LogBookController.Start += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.gameObject, hdResolution, menuPosition, menuScale);
            };
            On.RoR2.UI.EclipseRunScreenController.Start += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.gameObject, hdResolution, menuPosition, menuScale);
            };
            On.RoR2.UI.CharacterSelectController.Start += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.gameObject, hdResolution, characterSelectPosition, characterSelectScale);
            };
            On.RoR2.UI.PauseScreenController.OnEnable += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.gameObject, hdResolution, menuPosition, menuScale);
            };
            On.RoR2.UI.SimpleDialogBox.Start += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.transform.root.gameObject, hdResolution, menuPosition, menuScale);
            };
            On.RoR2.UI.GameEndReportPanelController.Awake += (orig, self) =>
            {
                orig(self);
                SetRenderMode(self.gameObject, hdResolution, menuPosition, menuScale);
            };
            On.RoR2.SplashScreenController.Start += (orig, self) =>
            {
                orig(self);
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
                GameObject splash = GameObject.Find("SpashScreenCanvas");
                if (splash)
                    SetRenderMode(splash, hdResolution, menuPosition, menuScale);
            };

            On.RoR2.GameOverController.Awake += (orig, self) =>
            {
                orig(self);
                self.gameEndReportPanelPrefab.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            };

            On.RoR2.MatchCamera.Awake += (orig, self) =>
            {
                self.matchFOV = false;
                orig(self);
            };

            On.RoR2.UI.CombatHealthBarViewer.UpdateAllHealthbarPositions += UpdateAllHealthBarPositionsVR;

            On.RoR2.TeleporterInteraction.Awake += AdjustTPIconSize;
            On.RoR2.UI.PingIndicator.RebuildPing += AdjustPingIconSize;
            On.RoR2.TeamComponent.Start += AdjustTeamIconSize;

            IL.RoR2.CameraRigController.SetCameraState += SetCameraStateIL;
            IL.RoR2.PositionIndicator.UpdatePositions += ILUpdatePositions;
        }

        private IEnumerator SetVRDevice()
        {
            XRSettings.LoadDeviceByName(ConfigUseOculus.Value ? "Oculus" : "OpenVR");
            yield return null;
            if (XRSettings.loadedDeviceName == (ConfigUseOculus.Value ? "Oculus" : "OpenVR"))
                XRSettings.enabled = true;
        }

        private void ILUpdatePositions(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchStfld<Vector3>("z")
                );

            c.Index--;

            c.Remove();

            c.Emit(OpCodes.Ldc_R4, 12.25f);
        }

        private void AdjustTeamIconSize(On.RoR2.TeamComponent.orig_Start orig, TeamComponent self)
        {
            orig(self);
            if (self.indicator)
                self.indicator.transform.localScale = 2 * Vector3.one;
        }

        private void AdjustPingIconSize(On.RoR2.UI.PingIndicator.orig_RebuildPing orig, PingIndicator self)
        {
            orig(self);
            self.positionIndicator.transform.localScale = 12.35f * Vector3.one;
        }

        private void AdjustTPIconSize(On.RoR2.TeleporterInteraction.orig_Awake orig, TeleporterInteraction self)
        {
            orig(self);
            self.teleporterPositionIndicator.transform.localScale = 4 * Vector3.one;
        }

        private void UpdateAllHealthBarPositionsVR(On.RoR2.UI.CombatHealthBarViewer.orig_UpdateAllHealthbarPositions orig, RoR2.UI.CombatHealthBarViewer self, Camera sceneCam, Camera uiCam)
        {
            foreach (CombatHealthBarViewer.HealthBarInfo healthBarInfo in self.victimToHealthBarInfo.Values)
            {
                Vector3 position = healthBarInfo.sourceTransform.position;
                position.y += healthBarInfo.verticalOffset;
                Vector3 vector = sceneCam.WorldToScreenPoint(position);
                Vector3 position2 = uiCam.ScreenToWorldPoint(vector);
                healthBarInfo.healthBarRootObjectTransform.position = position2;
                healthBarInfo.healthBarRootObjectTransform.localScale = 0.1f * Vector3.Distance(sceneCam.transform.position, position) * Vector3.one;
            }
        }

        private void SetCameraStateIL(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdfld<CameraState>("fov")
                );

            ILLabel breakLabel = c.IncomingLabels.ToList().First<ILLabel>();

            while (c.Next.OpCode.Code != Code.Ret)
            {
                c.Remove();
            }

            ILLabel retLabel = c.MarkLabel();

            c.GotoPrev(
                x => x.MatchBr(breakLabel)
                );
            c.Remove();
            c.Emit(OpCodes.Br_S, retLabel);
        }

        private void SetRenderMode(GameObject uiObject, Vector2 resolution, Vector3 positionOffset, Vector3 scale)
        {
            if (!uiCamera)
            {
                GameObject cameraObject = Camera.main.transform.parent.gameObject;
                uiCamera = cameraObject.GetComponent<CameraRigController>().uiCam;
            }

            Canvas canvas = uiObject.GetComponent<Canvas>();

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = uiCamera;

                if (transform.parent)
                    uiObject.transform.parent.position = uiCamera.transform.position + positionOffset;

                uiObject.transform.position = uiCamera.transform.position + positionOffset;
                uiObject.transform.localScale = scale;

                RectTransform rect = uiObject.GetComponent<RectTransform>();
                if (rect)
                {
                    rect.pivot = menuPivot;
                    rect.sizeDelta = resolution;
                }
            }
        }

        private void AdjustHUDAnchors(On.RoR2.UI.HUD.orig_Awake orig, RoR2.UI.HUD self)
        {
            orig(self);

            CanvasScaler scaler = self.canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.scaleFactor = 0.8f;

            Transform[] uiElements = new Transform[] { 
                self.mainUIPanel.transform.Find("SpringCanvas"),
                self.mainContainer.transform.Find("NotificationArea") 
            };

            foreach (Transform uiElement in uiElements)
            {
                RectTransform rect = uiElement.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.25f, 0.25f);
                rect.anchorMax = new Vector2(0.75f, 0.65f);
            }
        }

        private Ray GetVRCrosshairRaycastRay(On.RoR2.CameraRigController.orig_GetCrosshairRaycastRay orig, RoR2.CameraRigController self, Vector2 crosshairOffset, Vector3 raycastStartPlanePoint)
        {
            if (!self.sceneCam)
            {
                return default(Ray);
            }
            float fieldOfView = self.sceneCam.fieldOfView;
            float num = fieldOfView * self.sceneCam.aspect;
            Quaternion quaternion = Quaternion.Euler(crosshairOffset.y * fieldOfView, crosshairOffset.x * num, 0f);
            quaternion = self.sceneCam.transform.rotation * quaternion;
            return new Ray(Vector3.ProjectOnPlane(self.sceneCam.transform.position - raycastStartPlanePoint, self.sceneCam.transform.rotation * Vector3.forward) + raycastStartPlanePoint, quaternion * Vector3.forward);
        }
    }
}

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute { }
}