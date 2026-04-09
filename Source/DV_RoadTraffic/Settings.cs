
using UnityEngine;
using UnityModManagerNet;
using System;

namespace DV_RoadTraffic
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public bool enabled = true;
        public bool debugLogging = false;
        public int vehicleLayer = 27;

        public KeyBind ToggleEditMode = new KeyBind(KeyCode.F9, true, true);
        public KeyBind ToggleTrafficWarden = new KeyBind(KeyCode.W, false, true);
        
        public bool ignoreTrainImpact = false;
        public bool stopIfTrainAhead = true;

        public float impactOomph = 1.0f;

        public float engineVolume = 1.0f;
        public float hornVolume = 1.0f;

        public KeyBind RouteInteract = new KeyBind(KeyCode.F9);

        public KeyBind SpawnMarker = new KeyBind(KeyCode.F6);
        public KeyBind ChangeMarkerType = new KeyBind(KeyCode.F7);
        public KeyBind DeleteObject = new KeyBind(KeyCode.Delete);

        public KeyBind SpawnTestVehicle = new KeyBind(KeyCode.F8);
        public KeyBind AutoSpawnTestVehicle = new KeyBind(KeyCode.F8, false, true); // Alt+F8

        public KeyBind MoveForward = new KeyBind(KeyCode.UpArrow);
        public KeyBind MoveBackward = new KeyBind(KeyCode.DownArrow);
        public KeyBind MoveLeft = new KeyBind(KeyCode.LeftArrow);
        public KeyBind MoveRight = new KeyBind(KeyCode.RightArrow);

        public KeyBind ToggleGunType = new KeyBind(KeyCode.G, false, true);

        public KeyBind SaveRoutes = new KeyBind(KeyCode.F10);

        public KeyBind PrevMarkerType = new KeyBind(KeyCode.F7, false, true); // Alt+F7

        public KeyBind RaiseObject = new KeyBind(KeyCode.PageUp);
        public KeyBind LowerObject = new KeyBind(KeyCode.PageDown);

        public void ResetToDefaults()
        {
            ToggleEditMode = new KeyBind(KeyCode.F9, true, true);
            ToggleTrafficWarden = new KeyBind(KeyCode.W, false, true);
            ignoreTrainImpact = false;
            stopIfTrainAhead = true;

            engineVolume = 1.0f;
            hornVolume = 1.0f;

            impactOomph = 1.0f;

            RouteInteract = new KeyBind(KeyCode.F9);

            SpawnMarker = new KeyBind(KeyCode.F6);
            ChangeMarkerType = new KeyBind(KeyCode.F7);
            DeleteObject = new KeyBind(KeyCode.Delete);

            MoveForward = new KeyBind(KeyCode.UpArrow);
            MoveBackward = new KeyBind(KeyCode.DownArrow);
            MoveLeft = new KeyBind(KeyCode.LeftArrow);
            MoveRight = new KeyBind(KeyCode.RightArrow);

            ToggleGunType = new KeyBind(KeyCode.G, false, true);

            SaveRoutes = new KeyBind(KeyCode.F10);

            SpawnTestVehicle = new KeyBind(KeyCode.F8);
            AutoSpawnTestVehicle = new KeyBind(KeyCode.F8, false, true);

            PrevMarkerType = new KeyBind(KeyCode.F7, false, true);

            RaiseObject = new KeyBind(KeyCode.PageUp);
            LowerObject = new KeyBind(KeyCode.PageDown);
        }

        public void Draw(UnityModManager.ModEntry modEntry)
        {
            SettingsUI.Draw(this);
        }

        public void OnChange() { }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}


namespace DV_RoadTraffic
{
    public static class SettingsUI
    {
        private static KeyBind waitingForKey;

        
        public static void Draw(Settings settings)
        {
                      
            // =========================================================

            GUILayout.Label("Game Options", GUI.skin.box);

            // -------------------------
            // CONTROLS
            // -------------------------
            GUILayout.Label("<b>Controls</b>");

            DrawKeyBind("Toggle Edit Mode", settings.ToggleEditMode);
            DrawKeyBind("Toggle Traffic Warden", settings.ToggleTrafficWarden);

            // -------------------------
            // TRAIN INTERACTION
            // -------------------------
            GUILayout.Space(6);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(4);

            GUILayout.Label("<b>Train Interaction</b>");

            Main.Settings.ignoreTrainImpact = GUILayout.Toggle(
                Main.Settings.ignoreTrainImpact,
                "Ignore Train Collisions"
            );

            Main.Settings.stopIfTrainAhead = GUILayout.Toggle(
                Main.Settings.stopIfTrainAhead,
                "Stop if Train Detected Ahead"
            );

            GUILayout.Label($"Train Impact Oomph: {Main.Settings.impactOomph:F2}");

            Main.Settings.impactOomph = GUILayout.HorizontalSlider(
                Main.Settings.impactOomph,
                0.5f,   // minimum
                3.0f    // maximum
            );

            // -------------------------
            // AUDIO
            // -------------------------
            GUILayout.Space(6);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(4);

            GUILayout.Label("<b>Audio</b>");

            // Engine Volume
            GUILayout.Label($"Engine Volume: {(Main.Settings.engineVolume * 100f):F0}%");
            Main.Settings.engineVolume = GUILayout.HorizontalSlider(
                Main.Settings.engineVolume,
                0f,
                1.5f
            );

            // Horn Volume
            GUILayout.Label($"Horn Volume: {(Main.Settings.hornVolume * 100f):F0}%");
            Main.Settings.hornVolume = GUILayout.HorizontalSlider(
                Main.Settings.hornVolume,
                0f,
                1.5f
            );
            
            GUILayout.Space(10);

            GUILayout.Label("Main Edit Options", GUI.skin.box);

            DrawKeyBind("Route Interaction (Raycast Context)", settings.RouteInteract);
            DrawKeyBind("Save Routes", settings.SaveRoutes);

            GUILayout.Label("Create Route:        Raycast + Interaction Key");
            GUILayout.Label("Select Route:        Raycast on Vehicle Factory");
            GUILayout.Label("Deselect Route:      Press Interaction Key again on selected object");

            GUILayout.Space(10);

            GUILayout.Label("Route Edit Options", GUI.skin.box);

            DrawKeyBind("Spawn Marker", settings.SpawnMarker);
            DrawKeyBind("Change Selected Marker to Next Marker Type", settings.ChangeMarkerType);
            DrawKeyBind("Change Selected Marker to Prev Marker Type", settings.PrevMarkerType);
            DrawKeyBind("Delete Selected Object", settings.DeleteObject);
            DrawKeyBind("Spawn Test Vehicle", settings.SpawnTestVehicle);
            DrawKeyBind("Spawn Test Vehicle", settings.SpawnTestVehicle);
            DrawKeyBind("Auto Spawn Test Vehicles", settings.AutoSpawnTestVehicle);

            GUILayout.Label("Select Marker:       Raycast + Interaction Key");

            GUILayout.Space(10);

            GUILayout.Label("Movement", GUI.skin.box);

            DrawKeyBind("Move Forward", settings.MoveForward);
            DrawKeyBind("Move Backward", settings.MoveBackward);
            DrawKeyBind("Move Left", settings.MoveLeft);
            DrawKeyBind("Move Right", settings.MoveRight);
            DrawKeyBind("Raise Selected Marker/VehicleFactory", settings.RaiseObject);
            DrawKeyBind("Lower Selected Marker/VehicleFactory", settings.LowerObject);

            GUILayout.Space(10);

            GUILayout.Label("Rotation", GUI.skin.box);

            GUILayout.Label("Rotate Object:              Scroll Wheel");
            GUILayout.Label("Fine Rotation:              Shift + Scroll Wheel");

            GUILayout.Space(10);

            GUILayout.Label("Marker Parameters", GUI.skin.box);

            GUILayout.Label("Cycle Parameters:           Alt + Shift + Scroll Wheel");
            GUILayout.Label("Adjust Parameter Value:     Alt + Scroll Wheel");

            GUILayout.Space(10);

            GUILayout.Label("Traffic Warden", GUI.skin.box);

            DrawKeyBind("Toggle Gun Types", settings.ToggleGunType);

            

            GUILayout.Space(20);

            GUILayout.BeginVertical("box");


            GUILayout.Label("Developer Options", UnityModManager.UI.h2);

            /*
            bool newDebug = GUILayout.Toggle(settings.debugLogging, "Enable Debug Logging");

            if (newDebug != settings.debugLogging)
            {
                settings.debugLogging = newDebug;
                Main.Log($"[DVRT] Debug logging {(settings.debugLogging ? "ENABLED" : "DISABLED")}", true);
            }

            */

            GUILayout.Label($"Vehicle Layer: {Main.Settings.vehicleLayer}");

            int newLayer = Mathf.RoundToInt(GUILayout.HorizontalSlider(
                Main.Settings.vehicleLayer,
                0,
                27
            ));

            if (newLayer != Main.Settings.vehicleLayer)
            {
                Main.Settings.vehicleLayer = newLayer;
                DVRT_Manager.ApplyVehicleLayerChange(newLayer);
            }

            GUILayout.EndVertical();            

            GUILayout.Space(20);

            if (GUILayout.Button("Reset To Defaults", GUILayout.Height(30)))
            {
                settings.ResetToDefaults();
                Main.Settings.Save(Main.Mod);
            }


        }
        
        private static void DrawKeyBind(string label, KeyBind bind)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(label, GUILayout.Width(250));

            bind.Ctrl = GUILayout.Toggle(bind.Ctrl, "Ctrl", GUILayout.Width(50));
            bind.Alt = GUILayout.Toggle(bind.Alt, "Alt", GUILayout.Width(50));
            bind.Shift = GUILayout.Toggle(bind.Shift, "Shift", GUILayout.Width(50));

            if (waitingForKey == bind)
            {
                GUILayout.Button("Press key...", GUILayout.Width(120));

                Event e = Event.current;

                if (e.isKey)
                {
                    bind.Key = e.keyCode;
                    waitingForKey = null;
                    Main.Settings.Save(Main.Mod);
                }
            }
            else
            {
                if (GUILayout.Button(bind.Key.ToString(), GUILayout.Width(120)))
                {
                    waitingForKey = bind;
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}

namespace DV_RoadTraffic
{
    [System.Serializable]
    public class KeyBind
    {
        public KeyCode Key;
        public bool Ctrl;
        public bool Alt;
        public bool Shift;

        public KeyBind() { }

        public KeyBind(KeyCode key, bool ctrl = false, bool alt = false, bool shift = false)
        {
            Key = key;
            Ctrl = ctrl;
            Alt = alt;
            Shift = shift;
        }

        public bool IsPressed()
        {
            if (!Input.GetKeyDown(Key))
                return false;

            bool ctrlHeld =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            bool altHeld =
                Input.GetKey(KeyCode.LeftAlt) ||
                Input.GetKey(KeyCode.RightAlt);

            bool shiftHeld =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            // exact modifier match
            if (ctrlHeld != Ctrl)
                return false;

            if (altHeld != Alt)
                return false;

            if (shiftHeld != Shift)
                return false;

            return true;
        }

        public bool IsHeld()
        {
            if (!Input.GetKey(Key))
                return false;

            bool ctrlHeld =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            bool altHeld =
                Input.GetKey(KeyCode.LeftAlt) ||
                Input.GetKey(KeyCode.RightAlt);

            bool shiftHeld =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
           
            if (ctrlHeld != Ctrl)
                return false;

            if (altHeld != Alt)
                return false;

            if (shiftHeld != Shift)
                return false;

            return true;
        }

        public string ToDisplay()
        {
            string s = "";

            if (Ctrl) s += "Ctrl+";
            if (Alt) s += "Alt+";
            if (Shift) s += "Shift+";

            s += Key.ToString();

            return s;
        }
    }
}
