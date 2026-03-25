using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DV_RoadTraffic
{
    [Serializable]
    public class TrafficRouteDatabase
    {
        public int version = 1;
        public List<VehicleFactoryData> factories = new List<VehicleFactoryData>();
    }

    [Serializable]
    public class VehicleFactoryData
    {
        public string routeName;
        public int trafficRate;
        public float ttl;
        public float[] position;
        public float[] rotation;
        public bool spawnCars = true;
        public bool spawnTrucks = true;
        public bool spawnBuses = true;
        public bool spawnExcavators = true;

        [JsonIgnore]
        public string routeFileName;

        public List<MarkerData> markers = new List<MarkerData>();
    }

    [Serializable]
    public class MarkerData
    {
        public string markerID;
        public string type;
        public float[] position;
        public float[] rotation;
        public string targetMarkerID;
        public string targetMarkerID1;
        public string targetMarkerID2;
        public int speedLevel = 1;
        public float stopSeconds = 3f;
    }

    public static class DVRT_RoutePersistence
    {
        private static string SavePath =>
            Path.Combine(Main.Mod.Path, "TrafficRoutes.json");

        public static void SaveRoutes(List<VehicleFactory> factories)
        {
            string routesFolder = Path.Combine(
                Path.GetDirectoryName(SavePath),
                "Routes");

            if (!Directory.Exists(routesFolder))
                Directory.CreateDirectory(routesFolder);

            HashSet<string> writtenFiles = new HashSet<string>();

            foreach (var vf in factories)
            {
                if (vf == null || vf.Root == null)
                    continue;

                VehicleFactoryData vfData = new VehicleFactoryData();

                vfData.routeName = vf.RouteName;
                vfData.trafficRate = vf.TrafficRate;
                vfData.ttl = vf.TTL;
                vfData.spawnCars = vf.SpawnCars;
                vfData.spawnTrucks = vf.SpawnTrucks;
                vfData.spawnBuses = vf.SpawnBuses;
                vfData.spawnExcavators = vf.SpawnExcavators;

                Vector3 vfPos = vf.Root.transform.position - DVRT_WorldShiftManager.CurrentMove;
                Vector3 vfRot = vf.Root.transform.eulerAngles;

                vfData.position = new float[] { vfPos.x, vfPos.y, vfPos.z };
                vfData.rotation = new float[] { vfRot.x, vfRot.y, vfRot.z };

                foreach (var marker in vf.Markers)
                {
                    if (marker == null || marker.Root == null)
                        continue;

                    MarkerData md = new MarkerData();

                    md.markerID = marker.MarkerID;
                    md.type = marker.Type.ToString();

                    Vector3 mPos = marker.Root.transform.position - DVRT_WorldShiftManager.CurrentMove;
                    Vector3 mRot = marker.Root.transform.eulerAngles;

                    md.position = new float[] { mPos.x, mPos.y, mPos.z };
                    md.rotation = new float[] { mRot.x, mRot.y, mRot.z };

                    md.targetMarkerID = marker.TargetMarkerID;
                    md.targetMarkerID1 = marker.TargetMarkerID1;
                    md.targetMarkerID2 = marker.TargetMarkerID2;

                    md.speedLevel = marker.SpeedLevel;
                    md.stopSeconds = marker.StopSeconds;

                    vfData.markers.Add(md);
                }

                string fileName;

                if (!string.IsNullOrEmpty(vf.RouteFileName))
                {
                    fileName = vf.RouteFileName;
                }
                else
                {
                    string safeName = SanitizeRouteName(vf.RouteName);
                    fileName = safeName + ".json";

                    int suffix = 1;
                    string candidate = fileName;

                    while (writtenFiles.Contains(candidate) || File.Exists(Path.Combine(routesFolder, candidate)))
                    {
                        candidate = safeName + "_" + suffix + ".json";
                        suffix++;
                    }

                    fileName = candidate;
                    vf.RouteFileName = fileName;
                }

                string filePath = Path.Combine(routesFolder, fileName);

                string json = JsonConvert.SerializeObject(vfData, Formatting.Indented);
                File.WriteAllText(filePath, json);

                writtenFiles.Add(fileName);

                Main.Log($"[DVRT] Saved route file {fileName}");
            }

            string[] existingFiles = Directory.GetFiles(routesFolder, "*.json");

            foreach (string file in existingFiles)
            {
                string name = Path.GetFileName(file);

                if (!writtenFiles.Contains(name))
                {
                    File.Delete(file);
                    Main.Log($"[DVRT] Removed deleted route file {name}");
                }
            }

            Main.Log("[DVRT] Route save complete.");
        }

        public static TrafficRouteDatabase LoadRoutes()
        {
            var folderDb = LoadRoutesFromFolder();

            if (folderDb == null || folderDb.factories == null || folderDb.factories.Count == 0)
            {
                Main.Log("[DVRT] No route files found in Routes folder.");
                return null;
            }

            Main.Log("[DVRT] Using per-route JSON system.");
            return folderDb;
        }

        public static TrafficRouteDatabase LoadRoutesFromFolder()
        {
            string routesFolder = Path.Combine(
                Path.GetDirectoryName(SavePath),
                "Routes");

            if (!Directory.Exists(routesFolder))
            {
                Main.Log("[DVRT] Routes folder not found.");
                return null;
            }

            string[] files = Directory.GetFiles(routesFolder, "*.json");

            if (files.Length == 0)
            {
                Main.Log("[DVRT] Routes folder exists but contains no route files.");
                return null;
            }

            TrafficRouteDatabase db = new TrafficRouteDatabase();
            db.factories = new List<VehicleFactoryData>();

            int markerCount = 0;
            HashSet<string> usedRouteNames = new HashSet<string>();

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);

                    var vf = JsonConvert.DeserializeObject<VehicleFactoryData>(json);

                    if (vf == null)
                        continue;

                    vf.routeFileName = Path.GetFileName(file);

                    if (string.IsNullOrEmpty(vf.routeName))
                        vf.routeName = Path.GetFileNameWithoutExtension(file);

                    if (vf.trafficRate <= 0)
                        vf.trafficRate = 5;

                    string baseName = vf.routeName;
                    string uniqueName = baseName;
                    int suffix = 1;

                    while (usedRouteNames.Contains(uniqueName))
                    {
                        uniqueName = baseName + "_" + suffix;
                        suffix++;
                    }

                    vf.routeName = uniqueName;
                    usedRouteNames.Add(uniqueName);

                    if (vf.markers != null)
                        markerCount += vf.markers.Count;

                    db.factories.Add(vf);

                    Main.Log($"[DVRT] Loaded route file {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    Main.Log($"[DVRT] ERROR loading route file {file}: {ex.Message}");
                }
            }

            Main.Log($"[DVRT] Loaded {db.factories.Count} factories with {markerCount} markers from Routes folder.");

            return db;
        }

        private static void SpawnSingleRoute(VehicleFactoryData vfData, Vector3 shift)
        {
            if (vfData.position == null || vfData.position.Length < 3)
                return;

            Vector3 canonicalVF = new Vector3(
                vfData.position[0],
                vfData.position[1],
                vfData.position[2]);

            Vector3 vfPos = canonicalVF + shift;

            Main.Log($"[DVRT] Player position {Camera.main.transform.position}");
            Main.Log($"[DVRT] Canonical VF {canonicalVF}");
            Main.Log($"[DVRT] Shift {shift}");
            Main.Log($"[DVRT] Final spawn {vfPos}");

            VehicleFactory vf = new VehicleFactory(vfPos);

            vf.RouteFileName = vfData.routeFileName;

            vf.RouteName = string.IsNullOrEmpty(vfData.routeName) ? "Route" : vfData.routeName;
            vf.TrafficRate = vfData.trafficRate <= 0 ? 5 : vfData.trafficRate;
            vf.TTL = vfData.ttl > 0 ? vfData.ttl : 240f;
            vf.SpawnCars = vfData.spawnCars;
            vf.SpawnTrucks = vfData.spawnTrucks;
            vf.SpawnBuses = vfData.spawnBuses;
            vf.SpawnExcavators = vfData.spawnExcavators;

            vf.CacheNearbyBarriers();

            Quaternion canonicalRot = Quaternion.Euler(
                vfData.rotation[0],
                vfData.rotation[1],
                vfData.rotation[2]);

            vf.SetCanonicalRotation(canonicalRot);

            if (vf.Root != null)
            {
                var r = vf.Root.GetComponent<Renderer>();
                if (r != null)
                    r.enabled = false;
            }

            DVRT_Manager.RegisterFactory(vf);
            vf.SetVisible(false);

            if (vfData.markers != null)
            {
                foreach (var md in vfData.markers)
                {
                    if (md.position == null || md.position.Length < 3)
                        continue;

                    Vector3 canonicalMarker = new Vector3(
                        md.position[0],
                        md.position[1],
                        md.position[2]);

                    Vector3 mPos = canonicalMarker + shift;

                    TrafficMarker marker = new TrafficMarker(mPos);

                    marker.RouteName = vf.RouteName;

                    if (marker.Root != null && md.rotation != null && md.rotation.Length >= 3)
                    {
                        Quaternion rot = Quaternion.Euler(
                            md.rotation[0],
                            md.rotation[1],
                            md.rotation[2]);

                        marker.Root.transform.rotation = rot;
                        marker.SetCanonicalRotation(rot);
                    }

                    marker.ForceSetMarkerID(md.markerID);

                    TrafficMarker.MarkerType parsedType;
                    if (Enum.TryParse(md.type, true, out parsedType))
                    {
                        marker.ForceSetType(parsedType);
                        marker.SetSpeedLevel(md.speedLevel);
                        marker.SetStopSeconds(md.stopSeconds);
                    }

                    marker.ForceSetTargetMarkerID(md.targetMarkerID);
                    marker.TargetMarkerID1 = md.targetMarkerID1;
                    marker.TargetMarkerID2 = md.targetMarkerID2;

                    vf.Markers.Add(marker);
                }

                foreach (var marker in vf.Markers)
                {
                    DVRT_Manager.RegisterMarker(marker);
                }
            }

            DVRT_Manager.SetMarkerVisibility(vf, false);
        }

        public static void SpawnRoutes(TrafficRouteDatabase db)
        {
            if (db == null || db.factories == null)
                return;

            if (DVRT_RuntimeUI.Instance == null)
            {
                Vector3 shift = DVRT_WorldShiftManager.CurrentMove;

                foreach (var vfData in db.factories)
                {
                    SpawnSingleRoute(vfData, shift);
                }

                DVRT_Manager.ValidateAllMarkerLinks();
                return;
            }

            DVRT_RuntimeUI.Instance.StartCoroutine(StartSpawnWhenUIReady(db));
        }

        private static IEnumerator SpawnRoutesIncremental(TrafficRouteDatabase db)
        {
            Vector3 shift = DVRT_WorldShiftManager.CurrentMove;

            int total = db.factories.Count;
            int current = 0;

            var ui = DVRT_RuntimeUI.Instance;

            ui?.ShowLoading($"DV_RoadTraffic\nLoading Routes 0/{total}");

            yield return null;

            foreach (var vfData in db.factories)
            {
                current++;
                ui?.ShowLoading($"DV_RoadTraffic\nLoading Route {current}/{total}");

                SpawnSingleRoute(vfData, shift);

                yield return null;
            }

            ui?.HideLoading();

            DVRT_Manager.ValidateAllMarkerLinks();
        }

        private static IEnumerator StartSpawnWhenUIReady(TrafficRouteDatabase db)
        {
            while (DVRT_RuntimeUI.Instance == null ||
                   !DVRT_RuntimeUI.Instance.gameObject.activeInHierarchy)
            {
                yield return null;
            }

            yield return null;

            yield return SpawnRoutesIncremental(db);
        }

        public static string SanitizeRouteName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Route";

            string sanitized = name.Replace(" ", "_");

            foreach (char c in Path.GetInvalidFileNameChars())
                sanitized = sanitized.Replace(c.ToString(), "");

            return sanitized;
        }

        public static void ApplyRouteRename(VehicleFactory vf, string newName)
        {
            if (vf == null)
                return;

            string routesFolder = Path.Combine(
                Path.GetDirectoryName(SavePath),
                "Routes");

            string safeName = SanitizeRouteName(newName);
            string candidateFile = safeName + ".json";

            int suffix = 1;

            while (File.Exists(Path.Combine(routesFolder, candidateFile)) &&
                   candidateFile != vf.RouteFileName)
            {
                candidateFile = safeName + "_" + suffix + ".json";
                suffix++;
            }

            if (suffix > 1)
            {
                newName = newName + "_" + (suffix - 1);
            }

            vf.RouteName = newName;
            vf.RouteFileName = candidateFile;
        }
    }
}