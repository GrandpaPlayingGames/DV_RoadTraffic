using System;
using UnityEngine;
using UnityModManagerNet;
using HarmonyLib;

namespace DV_RoadTraffic { 
    public static class Main
    {
        public static UnityModManager.ModEntry Mod;
        public static Settings Settings;
        public static Harmony Harmony;

        private static bool _loaded;

        private static System.Collections.Generic.List<VehicleFactory> _factories
            = new System.Collections.Generic.List<VehicleFactory>();
        
        private static TrafficRouteDatabase _loadedRoutes;
        private static bool _routesLoaded = false;

        private static CharacterControllerProvider _cachedProvider;
        private static bool _gameLoadedFired = false;
        private static bool _sessionInitialized = false;
        private static bool _fastTravelInProgress = false;
        private static bool _wasGameLoaded = false;
        public static bool SessionActive => _sessionInitialized;

        public static bool IsGameLoaded
        {
            get
            {
                return _cachedProvider != null && _cachedProvider.IsGameLoaded;
            }
        }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                Mod = modEntry;

                Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);                

                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
                modEntry.OnUpdate = Update;   // ← your preferred naming

                Harmony = new Harmony(modEntry.Info.Id);
                Harmony.PatchAll();
                Main.Log("[DVRT] Harmony.PatchAll complete");

                DVRT_SoundLibrary.Initialize();
                DVRT_ParticleLibrary.Initialize();

                var test = DVRT_SoundLibrary.GetRandomEngine("Car");

                if (test != null)
                    Main.Log("[DVRT] Engine sound retrieved successfully");

                _loaded = true;

                var go = new GameObject("DVRT_RuntimeUI");
                GameObject.DontDestroyOnLoad(go);
                go.AddComponent<DVRT_RuntimeUI>();

                if (GameObject.Find("DVRT_GUI") == null)
                {
                    var gui_go = new GameObject("DVRT_GUI");
                    UnityEngine.Object.DontDestroyOnLoad(gui_go);
                    gui_go.AddComponent<DVRT_GUI>();
                }

                var warden = new GameObject("DVRT_TrafficWarden");
                GameObject.DontDestroyOnLoad(warden);
                warden.AddComponent<TrafficWarden>();

                Log("Mod loaded successfully.");
                modEntry.Logger.Log("DVRT Load() reached.");

                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.Log($"DVRT Load FAILED: {ex}");
                return false;
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {                        
            Settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }

        private static void Update(UnityModManager.ModEntry modEntry, float dt)
        {
  
            if (!_loaded)
                return;

            if (!Settings.enabled)
                return;

            CheckGameLoadedOnce();

            DVRT_Manager.Update(dt);
        }
       
        private static void CheckGameLoadedOnce()
        {
            if (_cachedProvider == null)
            {
                _cachedProvider = UnityEngine.Object.FindObjectOfType<CharacterControllerProvider>();
                if (_cachedProvider == null)
                    return;
            }

            bool isLoaded = _cachedProvider.IsGameLoaded;

            // =========================================================
            // UNLOAD TRANSITION
            // =========================================================
            if (_wasGameLoaded && !isLoaded)
            {
                Log("[DVRT] Game unloading...");

                DVRT_Manager.ResetEditorState();

                if (_sessionInitialized && !FastTravelController.IsFastTravelling)
                {
                    Log("[DVRT] TRUE EXIT → ResetRuntime()");

                    DVRT_Manager.ResetRuntime();
                    _sessionInitialized = false;
                }
                else
                {
                    Log("[DVRT] Fast travel unload → skipping ResetRuntime()");
                }
            }

            if (!_wasGameLoaded && isLoaded)
            {
                Log("[DVRT] Game loaded");
                _gameLoadedFired = false;
            }

            if (!isLoaded)
            {
                _wasGameLoaded = false;
                return;
            }

            if (!_gameLoadedFired)
            {
                _gameLoadedFired = true;
                OnGameLoaded();
            }

            _wasGameLoaded = true;
        }

        private static void OnGameLoaded()
        {
            if (_sessionInitialized)
                return;
            DVRT_Manager.ResetRuntime();
            _loadedRoutes = DVRT_RoutePersistence.LoadRoutes();
            DVRT_RoutePersistence.SpawnRoutes(_loadedRoutes);

            _sessionInitialized = true;

            Log("[DVRT] Traffic routes spawned.");
        }


        // =========================================================
        // LOGGER
        // =========================================================

        public static void Log(string message, bool force = false)
        {
            if (!force && (Settings == null || !Settings.debugLogging))
                return;

            Mod?.Logger?.Log(message);
        }
        public static void SetSessionActive(bool active)
        {
            _sessionInitialized = active;
        }

        public static void MarkFastTravelStarted()
        {
            _fastTravelInProgress = true;
            Log("[DVRT] Fast travel detected.");
        }
    }    
}
