using DV;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DV_RoadTraffic
{
    public class DVRT_Comms : MonoBehaviour, ICommsRadioMode
    {
        private enum CommsState
        {
            Root,
            SelectRoute
        }

        private CommsState currentState;

        private CommsRadioController Controller;
        private CommsRadioDisplay display;

        private List<VehicleFactory> routes = new List<VehicleFactory>();
        private int selectedIndex;

        private AudioClip clickClip;
        private AudioClip scrollClip;

        public ButtonBehaviourType ButtonBehaviour =>
            currentState == CommsState.SelectRoute
                ? ButtonBehaviourType.Override
                : ButtonBehaviourType.Regular;

        public void Enable()
        {
            ResolveRefs();
            RefreshRoutes();
            UpdateDisplay();
        }

        public void Disable()
        {
        }

        public void SetStartingDisplay()
        {
            ResolveRefs();
            RefreshRoutes();
            UpdateDisplay();
        }

        public void OverrideSignalOrigin(Transform origin) { }

        public void OnUpdate() { }

        public void OnUse()
        {
            switch (currentState)
            {
                case CommsState.Root:

                    PlayClick();

                    RefreshRoutes();

                    if (routes.Count == 0)
                    {
                        display.SetDisplay(
                            "DV_ROADTRAFFIC",
                            "NO ROUTES LOADED",
                            "");
                        return;
                    }

                    selectedIndex = Mathf.Clamp(selectedIndex, 0, routes.Count - 1);

                    currentState = CommsState.SelectRoute;
                    UpdateDisplay();
                    break;


                case CommsState.SelectRoute:

                    PlayClick();

                    if (routes.Count == 0)
                    {
                        currentState = CommsState.Root;
                        UpdateDisplay();
                        return;
                    }

                    VehicleFactory vf = routes[selectedIndex];

                    if (vf != null)
                        TeleportToRoute(vf);

                    currentState = CommsState.Root;
                    UpdateDisplay();
                    break;
            }
        }

        public bool ButtonACustomAction()
        {
            if (currentState != CommsState.SelectRoute)
                return false;

            selectedIndex =
                (selectedIndex - 1 + routes.Count) % routes.Count;

            PlayScroll();
            UpdateDisplay();

            return true;
        }

        public bool ButtonBCustomAction()
        {
            if (currentState != CommsState.SelectRoute)
                return false;

            selectedIndex =
                (selectedIndex + 1) % routes.Count;

            PlayScroll();
            UpdateDisplay();

            return true;
        }

        private void TeleportToRoute(VehicleFactory vf)
        {
            if (vf == null)
                return;

            Vector3 worldPos =
                vf.CanonicalPosition + DVRT_WorldShiftManager.CurrentMove;

            // --- USE FADE SYSTEM ---
            if (DVRT_FadeUI.Instance != null)
            {
                DVRT_FadeUI.Instance.StartTeleport(
                    worldPos,
                    vf.RouteName   
                );
            }
            else
            {               
                PlayerManager.TeleportPlayer(
                    worldPos + Vector3.up * 1.6f,
                    Quaternion.identity,
                    null,
                    true,
                    false
                );
            }

            Main.Log($"[DVRT] Teleporting to route '{vf.RouteName}'");
        }

        private void RefreshRoutes()
        {
            routes.Clear();

            foreach (var vf in DVRT_Manager._factories)
            {
                if (vf != null)
                    routes.Add(vf);
            }

            if (selectedIndex >= routes.Count)
                selectedIndex = 0;
        }

        private void UpdateDisplay()
        {
            if (display == null)
                return;

            

            switch (currentState)
            {
                case CommsState.Root:

                    display.SetDisplay(
                        "DV_ROADTRAFFIC",
                        "TELEPORT TO ROUTE",
                        "PROCEED");

                    break;

                case CommsState.SelectRoute:

                    string name =
                        routes.Count > 0
                            ? routes[selectedIndex].RouteName
                            : "NONE";

                    display.SetDisplay(
                        "DV_ROADTRAFFIC",
                        $"SELECT ROUTE\n\n{name}",
                        "TELEPORT");

                    break;
            }
        }

        private void ResolveRefs()
        {
            if (Controller == null)
                Controller = GetComponentInParent<CommsRadioController>();

            if (display == null)
                display = Controller.GetComponentInChildren<CommsRadioDisplay>(true);

            ResolveAudioClips();
        }

        private void ResolveAudioClips()
        {
            if (clickClip == null)
                clickClip = Controller.selectionAction;

            if (scrollClip == null)
                scrollClip = clickClip;
        }

        private void PlayClick()
        {
            if (clickClip == null)
                return;

            CommsRadioController.PlayAudioFromRadio(clickClip, transform);
        }

        private void PlayScroll()
        {
            if (scrollClip == null)
                return;

            CommsRadioController.PlayAudioFromRadio(scrollClip, transform);
        }

        public Color GetLaserBeamColor()
        {
            return Color.clear;
        }
    }
}


namespace DV_RoadTraffic
{
    [HarmonyPatch(typeof(CommsRadioController), "Awake")]
    public static class DVRT_CommsInjector
    {
        private static void Postfix(CommsRadioController __instance)
        {
            try
            {
                GameObject go = new GameObject("CommsRadioDVRT");
                go.transform.SetParent(__instance.transform, false);
                go.SetActive(false);

                var comms = go.AddComponent<DVRT_Comms>();

                var controllerField =
                    AccessTools.Field(typeof(DVRT_Comms), "Controller");

                controllerField.SetValue(comms, __instance);

                var allModesField =
                    AccessTools.Field(typeof(CommsRadioController), "allModes");

                var allModes =
                    allModesField.GetValue(__instance) as List<ICommsRadioMode>;

                allModes.Add(comms);

                __instance.ReactivateModes();

                go.SetActive(true);

                Debug.Log("[DVRT] Comms radio injected");
            }
            catch (Exception ex)
            {
                Debug.LogError("[DVRT] Radio injection failed\n" + ex);
            }
        }
    }
}