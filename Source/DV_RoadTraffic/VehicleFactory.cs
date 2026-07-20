using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DV_RoadTraffic
{
    public class VehicleFactory
    {       
        public GameObject Root { get; private set; }

        private bool _hasScanned = false;
        private bool _isActivated = false;

        public bool IsActivated => _isActivated;

        private const float DiscoveryRadius = 800f;     
        private const float ActivationRadius = 1750f;  

        private List<GameObject> _localArchetypes = new List<GameObject>();
        private HashSet<string> _localTypeNames = new HashSet<string>();

        private Vector3 _canonicalPosition;
        public Vector3 CanonicalPosition => _canonicalPosition;

        private Quaternion _canonicalRotation;


        private Material _material;

        private Color _idleColor = Color.white;
        private Color _groupColor = Color.yellow;

        public bool IsSelected { get; private set; }

        private int _editParameterIndex = 0;

        public bool SpawnCars = true;
        public bool SpawnTrucks = true;
        public bool SpawnBuses = true;
        public bool SpawnExcavators = true;

        public List<TrafficMarker> Markers = new List<TrafficMarker>();

        private string _routeName = "Route";
        public string RouteFileName;

        public string RouteName
        {
            get => _routeName;
            set
            {
                _routeName = value;
                if (_label != null)
                    UpdateLabel();
            }
        }

        public readonly List<TrafficVehicleController> ActiveVehicles =
            new List<TrafficVehicleController>();

        private int _trafficRate = 5;

        public int TrafficRate
        {
            get => _trafficRate;
            set
            {
                _trafficRate = value;
                if (_label != null)
                    UpdateLabel();
            }
        }

        public float TTL = 240f; 

        private float _nextSpawnTime = -1f;

        public readonly List<Transform> NearbyBarriers = new List<Transform>();

        private static bool _dvlcDatabaseReflectionResolved;
        private static bool _dvlcDatabaseReflectionAvailable;

        private static FieldInfo _dvlcLoadedDatabaseField;
 
        private static readonly List<DVLCCanonicalBarrier>
            _dvlcCanonicalBarriers =
                new List<DVLCCanonicalBarrier>();

        private static object _dvlcCachedDatabaseInstance;
        private static bool _dvlcCanonicalBarrierDataAvailable;

        private static float _nextDVLCDatabaseRetryTime;

        private const float DVLCDatabaseRetryInterval = 0.5f;

        private sealed class DVLCCanonicalBarrier
        {
            public string Path;
            public Vector3 CanonicalPosition;
        }

        private bool _barrierRefreshPending;
        private int _barrierRefreshAttempts;
        private float _nextBarrierRefreshAttemptTime;

        private bool _backgroundBarrierRefreshActive;
        private float _nextBackgroundBarrierRefreshTime;

        private const float BackgroundBarrierRefreshInterval = 2f;

        private const int MaxBarrierRefreshAttempts = 4;
        private const float BarrierRefreshRetryInterval = 0.5f;

        private TextMesh _label;

        private float GetSpawnDelay()
        {
            if (_trafficRate <= 0)
                return -1f;

            int baseRange = 50 - (_trafficRate * 5); // 50..5
            int rnd = UnityEngine.Random.Range(0, baseRange);

            return rnd + 5f;
        }

        public static void _______________SYSTEM_________________()
        {
        }

        public void Update()
        {
            BillboardLabel();
        }

        public void Destroy()
        {
            if (_wosSubscribed)
            {
                DVRT_WorldShiftManager.OnWorldShift -= HandleWorldShift;
                _wosSubscribed = false;
            }

            foreach (var marker in Markers)
            {
                if (marker != null)
                {
                    marker.Destroy();
                    DVRT_Manager.UnregisterMarker(marker);
                }
            }
            Markers.Clear();
            ActiveVehicles.Clear();

            if (Root != null)
                GameObject.Destroy(Root);
        }

        public static void _______________BUILD_________________()
        {
        }

        public VehicleFactory(Vector3 worldPosition)
        {
            Root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Root.name = "DVRT_VehicleFactory";

            DVRT_WorldShiftManager.OnWorldShift += HandleWorldShift;
            _wosSubscribed = true;

            Root.transform.localScale = new Vector3(4f, 4f, 4f);

            _material = Root.GetComponent<Renderer>().material;
            _material.color = _idleColor;

            CreateForwardIndicator();

            foreach (var col in Root.GetComponentsInChildren<Collider>())
            {
                col.isTrigger = true;
            }

            _canonicalPosition = worldPosition - DVRT_WorldShiftManager.CurrentMove;
            _canonicalRotation = Quaternion.identity;

            ApplyTransform();

            InitializeLabel();
        }

        private void CreateForwardIndicator()
        {
            const float worldThickness = 0.25f;
            const float worldHeight = 0.25f;
            const float worldLength = 3.0f;

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "ForwardIndicator";
            arrow.transform.SetParent(Root.transform, false);

            UnityEngine.Object.Destroy(arrow.GetComponent<Collider>());
            arrow.GetComponent<Renderer>().material.color = Color.green;

            Vector3 parentScale = Root.transform.localScale;

            Vector3 localScale = new Vector3(
                worldThickness / parentScale.x,
                worldHeight / parentScale.y,
                worldLength / parentScale.z
            );

            arrow.transform.localScale = localScale;

            float cubeFaceLocalZ = 0.5f;
            float arrowHalfLocalZ = localScale.z * 0.5f;

            const float overlapWorld = 0.05f;
            float overlapLocal = overlapWorld / parentScale.z;

            arrow.transform.localPosition = new Vector3(
                0f,
                0f,
                cubeFaceLocalZ + arrowHalfLocalZ - overlapLocal
            );

            arrow.transform.localRotation = Quaternion.identity;
        }

        void DumpBarrierChildren(Transform root)
        {
            Main.Log($"[SCAN] Dumping children for {root.name} | pos={root.position}");

            foreach (Transform c in root)
            {
                Main.Log($"[SCAN]  child: {c.name}");

                foreach (Transform gc in c)
                {
                    Main.Log($"[SCAN]    grandchild: {gc.name}");
                }
            }
        }


        public static void _______________EDITING_________________()
        {
        }

        public void SetActiveEditing(bool active)
        {
            if (_material == null)
                return;

            if (active)
            {
                _material.color = _groupColor;
                EnableGlow();
            }
            else
            {
                DisableGlow();
            }
        }

        public void SetGroupSelected(bool selected)
        {
            if (_material == null)
                return;

            _material.color = selected ? _groupColor : _idleColor;
            DisableGlow();
        }


        public void SetVisible(bool visible)
        {
            if (Root == null)
                return;

            var renderers = Root.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
                r.enabled = visible;
        }

        
        public void Move(Vector3 worldDelta)
        {
            if (Root == null)
                return;

            _canonicalPosition += worldDelta;
            ApplyTransform();
        }
               
        public void Rotate(float degrees)
        {
            if (Root == null)
                return;

            Root.transform.Rotate(Vector3.up, degrees, Space.World);
            _canonicalRotation = Root.transform.rotation;
        }

        public void SetTrafficRate(int value)
        {
            TrafficRate = Mathf.Clamp(value, 0, 10);
        }

        public void SetRouteName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            RouteName = name.Trim();
        }

        public void CycleParameter(int direction)
        {
            _editParameterIndex += direction;

            if (_editParameterIndex < 0)
                _editParameterIndex = 5;
            else if (_editParameterIndex > 5)
                _editParameterIndex = 0;

            UpdateLabel();
        }

        public void AdjustSelectedParameter(float scroll)
        {
            int step = scroll > 0 ? 1 : -1;
            bool rebuild = false;

            switch (_editParameterIndex)
            {
                case 0:
                    SetTrafficRate(Mathf.Clamp(TrafficRate + step, 0, 10));
                    Main.Log($"[DVRT] Traffic rate set to {TrafficRate}");
                    break;

                case 1: // ✅ TTL
                    TTL = Mathf.Clamp(TTL + (step * 30f), 30f, 360f);
                    Main.Log($"[DVRT] TTL set to {TTL}s");
                    break;

                case 2:
                    SpawnCars = !SpawnCars;
                    rebuild = true;
                    break;

                case 3:
                    SpawnTrucks = !SpawnTrucks;
                    rebuild = true;
                    break;

                case 4:
                    SpawnBuses = !SpawnBuses;
                    rebuild = true;
                    break;

                case 5:
                    SpawnExcavators = !SpawnExcavators;
                    rebuild = true;
                    break;
            }

            if (rebuild)
                CacheLocalTrafficArchetypes();

            UpdateLabel();
        }

        public static void ___________EDITING_HELPERS________________()
        {
        }

        private void EnableGlow()
        {
            _material.EnableKeyword("_EMISSION");
            _material.SetColor("_EmissionColor", _groupColor * 2f);
        }

        private void DisableGlow()
        {
            _material.DisableKeyword("_EMISSION");
        }

        public void ApplyTransform()
        {
            if (Root == null)
                return;

            Vector3 shift = DVRT_WorldShiftManager.CurrentMove;

            Root.transform.position = _canonicalPosition + shift;
            Root.transform.rotation = _canonicalRotation;
        }

        public void ResetParameterEditing()
        {
            _editParameterIndex = 0;
            UpdateLabel();
        }

        public static void ______________TRAFFIC__________________()
        {
        }

        public void CacheLocalTrafficArchetypes()
        {
            if (!IsPlayerWithinActivationRange())
                return;

            _localArchetypes.Clear();
            _localTypeNames.Clear();

            var lodGroups = Resources.FindObjectsOfTypeAll<LODGroup>();

            int added = 0;

            foreach (var lod in lodGroups)
            {
                if (lod == null)
                    continue;

                GameObject go = lod.gameObject;
                if (go == null)
                    continue;

                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                Transform root = lod.transform;
                if (root == null)
                    continue;

                float dist = Vector3.Distance(
                    root.position,
                    Root.transform.position
                );
                
                if (dist > DiscoveryRadius)
                    continue;

                string cleanName = StripInstanceSuffix(root.name);

                if (cleanName.Contains("TrafficClone"))
                    continue;
   
                // ------------------------------------
                // VEHICLE CATEGORY DETECTION
                // ------------------------------------
                bool isCar =
                       cleanName.StartsWith("CarMidsize")
                    || cleanName.StartsWith("CarCompact")
                    || cleanName.StartsWith("CarSports")
                    || cleanName.StartsWith("CarOffroad")
                    || cleanName.StartsWith("CarHatchback")
                    || cleanName.StartsWith("CarPickup")
                    || cleanName.StartsWith("CarCity")
                    || cleanName.StartsWith("VanSmall")
                    || cleanName.StartsWith("CarFullsize")
                    || cleanName.StartsWith("CarStationWagon")
                    ;

                bool isBus = cleanName.StartsWith("Bus");
                bool isTruck = cleanName.StartsWith("Truck")
                    || cleanName.StartsWith("MiningTruck");
                    
                bool isExcavator = cleanName.StartsWith("Excavator")
                    || cleanName.StartsWith("TankMilitary")
                    || cleanName.StartsWith("FarmTractor");

                // ------------------------------------
                // PER-VF SPAWN FILTER
                // ------------------------------------
                if (isCar && !SpawnCars) continue;
                if (isTruck && !SpawnTrucks) continue;
                if (isBus && !SpawnBuses) continue;
                if (isExcavator && !SpawnExcavators) continue;

                // ------------------------------------
                // PREFIX FILTER
                // ------------------------------------
                if (!(isCar || isBus || isTruck || isExcavator))
                {
                    continue;
                }
                         
                // Blacklist
                if (cleanName.Contains("Wreck") ||
                    cleanName.Contains("_dmg") ||
                    cleanName.Contains("Trailer") ||
                    cleanName.Contains("Station") ||
                    cleanName.Contains("TruckMedium90sTrailer_01_Orange") ||
                    cleanName.Contains("MiningTruckWheelOld") ||
                    cleanName.Contains("FarmTractor1")||
                    cleanName.Contains("[interior]")) 
                    continue;

                if (root.GetComponentInParent<TrainCar>() != null)
                    continue;

                if (cleanName.Contains("Flatcar") ||
                    cleanName.Contains("Gondola") ||
                    isCar && cleanName.Contains("Tank") ||
                    cleanName.Contains("Refrigerator") ||
                    cleanName.Contains("Boxcar"))
                {
                    continue;
                }

                if (_localTypeNames.Contains(cleanName))
                    continue;

                _localTypeNames.Add(cleanName);
                Main.Log($"[DVRT_VF] {RouteName} Added {cleanName} to archetypes");
                _localArchetypes.Add(root.gameObject);
                added++;
            }

            _hasScanned = true;

            Main.Log(
                $"[DVRT] VF at {Root.transform.position} {RouteName} cached {added} local archetypes.",
                false
            );

            if (_localArchetypes == null || _localArchetypes.Count == 0)
            {
                return;
            }
        }
        public GameObject GetRandomArchetype()
        {
            if (!IsPlayerWithinActivationRange())
                return null;

            if (_localArchetypes.Count == 0)
            {
                CacheLocalTrafficArchetypes();
            }

            if (_localArchetypes.Count == 0)
                return null;

            int index = UnityEngine.Random.Range(0, _localArchetypes.Count);
            return _localArchetypes[index];
        }

        public void TryAutoSpawn(Vector3 playerPosition)
        {
            if (Root == null)
                return;

            if (!_isActivated)
                return;

            if (_trafficRate <= 0)
                return;

            UpdateDVLCCanonicalDatabaseCache();

            UpdateBackgroundBarrierRefresh();

            if (ShouldHoldSpawnForBarrierRefresh())
            {
                return;
            }

            if (_nextSpawnTime < 0f)
            {
                _nextSpawnTime =
                    Time.time + GetSpawnDelay();
            }

            if (Time.time < _nextSpawnTime)
                return;

            bool success =
                DVRT_Manager.SpawnFromFactory(this);

            if (success)
            {
                _nextSpawnTime =
                    Time.time + GetSpawnDelay();
            }
            else
            {
                _nextSpawnTime =
                    Time.time + 1f;
            }
        }

        public static void ______________LABELS__________________()
        {
        }


        private void CreateLabel()
        {
            GameObject labelObj = new GameObject("FactoryLabel");
            labelObj.transform.SetParent(Root.transform);

            // position just above cube
            labelObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            _label = labelObj.AddComponent<TextMesh>();
            _label.fontSize = 64;
            _label.characterSize = 0.06f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;

            _label.text = "";

            labelObj.SetActive(false);
        }

        public void InitializeLabel()
        {
            CreateLabel();
            UpdateLabel();
        }

        public void UpdateLabel()
        {
            if (_label == null)
                return;

            string p0 = _editParameterIndex == 0 ? "> " : "  "; // Traffic
            string p1 = _editParameterIndex == 1 ? "> " : "  "; // TTL
            string p2 = _editParameterIndex == 2 ? "> " : "  "; // Cars
            string p3 = _editParameterIndex == 3 ? "> " : "  "; // Trucks
            string p4 = _editParameterIndex == 4 ? "> " : "  "; // Buses
            string p5 = _editParameterIndex == 5 ? "> " : "  "; // Others

            _label.text =
                $"{RouteName}\n" +
                $"{p0}Traffic: {TrafficRate}\n" +
                $"{p1}TTL: {TTL:0}s\n\n" +   
                $"{p2}Spawn Cars: {(SpawnCars ? "Y" : "N")}\n" +
                $"{p3}Spawn Trucks: {(SpawnTrucks ? "Y" : "N")}\n" +
                $"{p4}Spawn Buses: {(SpawnBuses ? "Y" : "N")}\n" +
                $"{p5}Spawn Others: {(SpawnExcavators ? "Y" : "N")}";

        }

        public void SetLabelVisible(bool visible)
        {
            if (_label != null)
                _label.gameObject.SetActive(visible);
        }

        private void BillboardLabel()
        {
            if (_label == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            _label.transform.rotation =
                Quaternion.LookRotation(_label.transform.position - cam.transform.position);
        }

        public static void ___________POSITIONING________________()
        {
        }

        private bool _wosSubscribed = false;

        public void SetCanonicalRotation(Quaternion rotation)
        {
            _canonicalRotation = rotation;
            ApplyTransform();
        }

        public Vector3 GetSpawnPosition()
        {
            Transform t = Root.transform;
            float halfDepth = t.localScale.z * 0.5f;

            return t.position
                   + t.forward * halfDepth
                   + Vector3.up * 0.5f;
        }

        public Quaternion GetSpawnRotation()
        {
            return Root.transform.rotation;
        }


        private void HandleWorldShift(Vector3 delta)
        {
            ApplyTransform();
        }

        public static void ___________NEW_DVLC_STUFF________________()
        {
        }

        public void CacheNearbyBarriers()
        {
            if (!IsPlayerWithinActivationRange())
                return;

            CacheNearbyBarriersFromDVLC();
        }             

        private static void ResolveDVLCDatabaseReflection()
        {
            if (_dvlcDatabaseReflectionResolved)
                return;

            _dvlcDatabaseReflectionResolved = true;
            _dvlcDatabaseReflectionAvailable = false;

            Type dvlcMainType = null;

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];

                if (assembly == null)
                    continue;

                dvlcMainType =
                    assembly.GetType(
                        "DV_LevelCrossings.Main",
                        false);

                if (dvlcMainType != null)
                    break;
            }

            if (dvlcMainType == null)
            {
                Main.Log(
                    "[DVRT] DVLC canonical database unavailable: " +
                    "DV_LevelCrossings.Main type not found.");

                return;
            }

            _dvlcLoadedDatabaseField =
                dvlcMainType.GetField(
                    "_loadedDatabase",
                    BindingFlags.Public |
                    BindingFlags.Static);

            if (_dvlcLoadedDatabaseField == null)
            {
                Main.Log(
                    "[DVRT] DVLC canonical database unavailable: " +
                    "_loadedDatabase field not found.");

                return;
            }

            _dvlcDatabaseReflectionAvailable = true;

            Main.Log(
                "[DVRT] DVLC canonical database reflection resolved.");
        }

        private static bool TryCacheDVLCCanonicalBarrierData()
        {            
            ResolveDVLCDatabaseReflection();

            if (!_dvlcDatabaseReflectionAvailable)
                return false;

            object database =
                _dvlcLoadedDatabaseField.GetValue(null);

            if (database == null)
                return false;

            if (_dvlcCanonicalBarrierDataAvailable &&
                ReferenceEquals(
                    database,
                    _dvlcCachedDatabaseInstance))
            {
                return true;
            }

            Type databaseType =
                database.GetType();

            FieldInfo crossingsField =
                databaseType.GetField(
                    "crossings",
                    BindingFlags.Public |
                    BindingFlags.Instance);

            if (crossingsField == null)
            {
                Main.Log(
                    "[DVRT] DVLC canonical database invalid: " +
                    "crossings field not found.");

                return false;
            }

            IEnumerable crossings =
                crossingsField.GetValue(database)
                as IEnumerable;

            if (crossings == null)
            {
                Main.Log(
                    "[DVRT] DVLC canonical database invalid: " +
                    "crossings collection unavailable.");

                return false;
            }

            _dvlcCanonicalBarriers.Clear();

            int crossingCount = 0;
            int barrierCount = 0;

            foreach (object crossing in crossings)
            {
                if (crossing == null)
                    continue;

                crossingCount++;

                Type crossingType =
                    crossing.GetType();

                FieldInfo barriersField =
                    crossingType.GetField(
                        "barriers",
                        BindingFlags.Public |
                        BindingFlags.Instance);

                if (barriersField == null)
                    continue;

                IEnumerable barriers =
                    barriersField.GetValue(crossing)
                    as IEnumerable;

                if (barriers == null)
                    continue;

                foreach (object barrier in barriers)
                {
                    if (barrier == null)
                        continue;

                    Type barrierType =
                        barrier.GetType();

                    FieldInfo pathField =
                        barrierType.GetField(
                            "path",
                            BindingFlags.Public |
                            BindingFlags.Instance);

                    FieldInfo posXField =
                        barrierType.GetField(
                            "posX",
                            BindingFlags.Public |
                            BindingFlags.Instance);

                    FieldInfo posYField =
                        barrierType.GetField(
                            "posY",
                            BindingFlags.Public |
                            BindingFlags.Instance);

                    FieldInfo posZField =
                        barrierType.GetField(
                            "posZ",
                            BindingFlags.Public |
                            BindingFlags.Instance);
   
                    if (pathField == null ||
                        posXField == null ||
                        posYField == null ||
                        posZField == null)
                    {
                        continue;
                    }

                    float posX =
                        Convert.ToSingle(
                            posXField.GetValue(barrier));

                    float posY =
                        Convert.ToSingle(
                            posYField.GetValue(barrier));

                    float posZ =
                        Convert.ToSingle(
                            posZField.GetValue(barrier));
 
                    string path =
                        pathField.GetValue(barrier) as string;

                    if (string.IsNullOrEmpty(path))
                        continue;

                    DVLCCanonicalBarrier record =
                        new DVLCCanonicalBarrier
                        {
                            Path = path,

                            CanonicalPosition =
                                new Vector3(
                                    posX,
                                    posY,
                                    posZ)
                        };

                    _dvlcCanonicalBarriers.Add(record);

                    barrierCount++;

                }
            }

            _dvlcCachedDatabaseInstance = database;
            _dvlcCanonicalBarrierDataAvailable = true;

            Main.Log(
                 $"[DVRT] DVLC canonical barrier database available | " +
                 $"Crossings={crossingCount} | " +
                 $"Barriers={barrierCount} | " +
                 $"CachedRecords={_dvlcCanonicalBarriers.Count}");

            return true;
        }

        private static void UpdateDVLCCanonicalDatabaseCache()
        {
            if (_dvlcCanonicalBarrierDataAvailable)
                return;

            if (Time.time < _nextDVLCDatabaseRetryTime)
                return;

            _nextDVLCDatabaseRetryTime =
                Time.time + DVLCDatabaseRetryInterval;

            TryCacheDVLCCanonicalBarrierData();
        }

        private static Transform FindLiveBarrierByCanonicalPath(
    DVLCCanonicalBarrier record,
    List<GameObject> loadedSceneRoots)
        {
            if (record == null)
                return null;

            if (string.IsNullOrEmpty(record.Path))
                return null;

            if (loadedSceneRoots == null ||
                loadedSceneRoots.Count == 0)
            {
                return null;
            }

            string[] parts =
                record.Path.Split('/');

            if (parts.Length == 0)
                return null;

            Vector3 expectedWorldPosition =
                record.CanonicalPosition +
                WorldMover.currentMove;

            for (int i = 0; i < loadedSceneRoots.Count; i++)
            {
                GameObject rootObject =
                    loadedSceneRoots[i];

                if (rootObject == null)
                    continue;

                Transform root =
                    rootObject.transform;

                if (root == null)
                    continue;

                if (root.name != parts[0])
                    continue;

                Transform match =
                    FindCanonicalPathMatch(
                        root,
                        parts,
                        1,
                        expectedWorldPosition);

                if (match != null)
                    return match;
            }

            return null;
        }

        private static Transform FindCanonicalPathMatch(
    Transform current,
    string[] parts,
    int index,
    Vector3 expectedWorldPosition)
        {
            if (current == null)
                return null;

            if (parts == null ||
                parts.Length == 0)
            {
                return null;
            }

            if (index >= parts.Length)
            {
                float positionDifferenceSq =
                    (current.position -
                     expectedWorldPosition)
                    .sqrMagnitude;

                if (positionDifferenceSq < 0.01f)
                    return current;

                return null;
            }

            string requiredName =
                parts[index];

            int childCount =
                current.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child =
                    current.GetChild(i);

                if (child == null)
                    continue;

                if (child.name != requiredName)
                    continue;

                Transform result =
                    FindCanonicalPathMatch(
                        child,
                        parts,
                        index + 1,
                        expectedWorldPosition);

                if (result != null)
                    return result;
            }

            return null;
        }

        private static List<GameObject> GetLoadedSceneRoots()
        {
            List<GameObject> roots =
                new List<GameObject>();

            int sceneCount =
                SceneManager.sceneCount;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene =
                    SceneManager.GetSceneAt(i);

                if (!scene.IsValid())
                    continue;

                if (!scene.isLoaded)
                    continue;

                GameObject[] sceneRoots =
                    scene.GetRootGameObjects();

                if (sceneRoots == null)
                    continue;

                for (int r = 0; r < sceneRoots.Length; r++)
                {
                    GameObject root =
                        sceneRoots[r];

                    if (root != null)
                        roots.Add(root);
                }
            }

            return roots;
        }

        private bool CacheNearbyBarriersFromDVLC()
        {
            NearbyBarriers.Clear();

            if (Root == null)
                return true;

            bool databaseAvailable =
                TryCacheDVLCCanonicalBarrierData();

            if (!databaseAvailable)
                return false;

            const float radius = 1000f;
            float radiusSq =
                radius * radius;

            Vector3 factoryCanonicalPosition =
                Root.transform.position -
                WorldMover.currentMove;

            List<GameObject> loadedSceneRoots =
                GetLoadedSceneRoots();

            int nearbyDatabaseRecords = 0;
            int resolvedBarrierRoots = 0;
            int unresolvedBarrierRoots = 0;

            for (int i = 0;
                 i < _dvlcCanonicalBarriers.Count;
                 i++)
            {
                DVLCCanonicalBarrier record =
                    _dvlcCanonicalBarriers[i];

                if (record == null)
                    continue;

                float canonicalDistanceSq =
                    (record.CanonicalPosition -
                     factoryCanonicalPosition)
                    .sqrMagnitude;

                if (canonicalDistanceSq > radiusSq)
                    continue;

                nearbyDatabaseRecords++;

                Transform barrierRoot =
                    FindLiveBarrierByCanonicalPath(
                        record,
                        loadedSceneRoots);

                if (barrierRoot == null)
                {
                    unresolvedBarrierRoots++;
                    continue;
                }

                resolvedBarrierRoots++;

                Transform ramp =
                    barrierRoot.Find("Ramp");

                if (ramp == null)
                    continue;

                Transform colliders =
                    ramp.Find("Coliders") ??
                    ramp.Find("Colliders");

                if (colliders == null ||
                    colliders.childCount == 0)
                {
                    continue;
                }

                Transform collider =
                    colliders.GetChild(0);

                if (collider == null)
                    continue;

                if (!NearbyBarriers.Contains(collider))
                {
                    NearbyBarriers.Add(collider);
                }
            }

            Main.Log(
                $"[DVRT] Canonical DVLC barrier refresh | " +
                $"Route={RouteName} | " +
                $"NearbyRecords={nearbyDatabaseRecords} | " +
                $"ResolvedRoots={resolvedBarrierRoots} | " +
                $"UnresolvedRoots={unresolvedBarrierRoots} | " +
                $"Cached={NearbyBarriers.Count}");

            return true;
        }

        private void BeginBarrierRefreshAfterActivation()
        {
            TryCacheDVLCCanonicalBarrierData();

            _barrierRefreshPending = true;
            _barrierRefreshAttempts = 0;
            _nextBarrierRefreshAttemptTime = Time.time;

            _backgroundBarrierRefreshActive = false;
            _nextBackgroundBarrierRefreshTime = -1f;
        }

        public void RequestBarrierRefresh()
        {
            NearbyBarriers.Clear();

            _barrierRefreshPending = true;
            _barrierRefreshAttempts = 0;
            _nextBarrierRefreshAttemptTime =
                Time.time + 0.5f;

            _backgroundBarrierRefreshActive = false;
            _nextBackgroundBarrierRefreshTime = -1f;

            Main.Log(
                $"[DVRT] Barrier refresh requested | Route={RouteName}");
        }

        private bool ShouldHoldSpawnForBarrierRefresh()
        {
            if (!_barrierRefreshPending)
                return false;

            if (Time.time < _nextBarrierRefreshAttemptTime)
                return true;

            _barrierRefreshAttempts++;

            bool integrationAvailable =
                CacheNearbyBarriersFromDVLC();

            if (!integrationAvailable)
            {
                _barrierRefreshPending = false;
                _backgroundBarrierRefreshActive = false;
                return false;
            }

            if (NearbyBarriers.Count > 0)
            {
                _barrierRefreshPending = false;
                _backgroundBarrierRefreshActive = false;

                Main.Log(
                    $"[DVRT] Barrier refresh ready before spawn | " +
                    $"Route={RouteName} | " +
                    $"Attempts={_barrierRefreshAttempts} | " +
                    $"Cached={NearbyBarriers.Count}");

                return false;
            }

            if (_barrierRefreshAttempts < MaxBarrierRefreshAttempts)
            {
                _nextBarrierRefreshAttemptTime =
                    Time.time + BarrierRefreshRetryInterval;

                return true;
            }

            _barrierRefreshPending = false;
            _backgroundBarrierRefreshActive = true;
            _nextBackgroundBarrierRefreshTime =
                Time.time + BackgroundBarrierRefreshInterval;

            Main.Log(
                $"[DVRT] Barrier refresh retry limit reached | " +
                $"Route={RouteName} | Cached=0 | " +
                $"Starting background refresh");

            return false;
        }

        private void UpdateBackgroundBarrierRefresh()
        {
            if (!_backgroundBarrierRefreshActive)
                return;

            if (!_isActivated)
                return;

            if (NearbyBarriers.Count > 0)
            {
                _backgroundBarrierRefreshActive = false;
                return;
            }

            if (Time.time < _nextBackgroundBarrierRefreshTime)
                return;

            _nextBackgroundBarrierRefreshTime =
                Time.time + BackgroundBarrierRefreshInterval;

            bool integrationAvailable =
                CacheNearbyBarriersFromDVLC();

            if (!integrationAvailable)
            {
                _backgroundBarrierRefreshActive = false;
                return;
            }

            if (NearbyBarriers.Count > 0)
            {
                _backgroundBarrierRefreshActive = false;

                Main.Log(
                    $"[DVRT] Background barrier refresh succeeded | " +
                    $"Route={RouteName} | " +
                    $"Cached={NearbyBarriers.Count}");
            }
        }


        public static void ___________OTHER_HELPERS________________()
        {
        }


        bool IsDVLevelCrossingBarrier(Transform barrierRoot)
        {
            if (barrierRoot == null)
                return false;

            var signal = barrierRoot.Find("RailwayCrossingSignal");
            if (signal == null)
                return false;

            return signal.GetComponent<AudioSource>() != null;
        }
        private bool IsPlayerNearEnough()
        {
            Transform cam = Camera.main?.transform;
            if (cam == null)
                return false;

            if (Root == null)
                return false;

            var rootTransform = Root.transform;
            if (rootTransform == null)
                return false;

            float dist = Vector3.Distance(
                cam.position,
                Root.transform.position
            );

            return dist <= ActivationRadius;
        }
        public bool IsPlayerWithinActivationRange()
        {
            Transform cam = Camera.main?.transform;
            if (cam == null)
                return false;

            float dist = Vector3.Distance(cam.position, Root.transform.position);
            return dist <= ActivationRadius;
        }

        public void UpdateActivation(Vector3 playerPosition)
        {
            if (Root == null)
                return;

            Vector3 offset =
                playerPosition - Root.transform.position;

            bool inRange =
                offset.sqrMagnitude <=
                ActivationRadius * ActivationRadius;

            if (inRange && !_isActivated)
            {
                _isActivated = true;

                BeginBarrierRefreshAfterActivation();

                _nextSpawnTime = Time.time;

                Main.Log(
                    $"[DVRT] VF activated at {Root.transform.position}");
            }
            else if (!inRange && _isActivated)
            {
                _isActivated = false;

                _barrierRefreshPending = false;
                _barrierRefreshAttempts = 0;
                _nextBarrierRefreshAttemptTime = -1f;

                _backgroundBarrierRefreshActive = false;
                _nextBackgroundBarrierRefreshTime = -1f;

                NearbyBarriers.Clear();
            }
        }

        private static string StripInstanceSuffix(string name)
        {
            int index = name.IndexOf(" (");
            if (index >= 0)
                return name.Substring(0, index);

            return name;
        }


        public static void ___________LEGACY_OR_UNUSED________________()
        {
        }

        
    }
}

