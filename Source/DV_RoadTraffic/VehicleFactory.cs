using System.Collections.Generic;
using UnityEngine;

namespace DV_RoadTraffic
{
    public class VehicleFactory
    {       
        // =====================================================
        // CORE
        // =====================================================
        public GameObject Root { get; private set; }


        // =====================================================
        // ACTIVATION / SCANNING
        // =====================================================
        private bool _hasScanned = false;
        private bool _isActivated = false;

        public bool IsActivated => _isActivated;

        private const float DiscoveryRadius = 800f;     // tuning
        private const float ActivationRadius = 1750f;  // player proximity required


        // =====================================================
        // ARCHETYPES / LOCAL CACHE
        // =====================================================
        private List<GameObject> _localArchetypes = new List<GameObject>();
        private HashSet<string> _localTypeNames = new HashSet<string>();


        // =====================================================
        // WORLD / POSITIONING
        // =====================================================
        private Vector3 _canonicalPosition;
        public Vector3 CanonicalPosition => _canonicalPosition;

        private Quaternion _canonicalRotation;


        // =====================================================
        // VISUALS / MATERIAL
        // =====================================================
        private Material _material;

        private Color _idleColor = Color.white;
        private Color _groupColor = Color.yellow;


        // =====================================================
        // EDITOR / SELECTION
        // =====================================================
        public bool IsSelected { get; private set; }

        private int _editParameterIndex = 0;


        // =====================================================
        // SPAWN FILTERS
        // =====================================================
        public bool SpawnCars = true;
        public bool SpawnTrucks = true;
        public bool SpawnBuses = true;
        public bool SpawnExcavators = true;


        // =====================================================
        // ROUTE / MARKERS
        // =====================================================
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


        // =====================================================
        // TRAFFIC SETTINGS
        // =====================================================
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

        public float TTL = 240f; // default

        private float _nextSpawnTime = -1f;


        // =====================================================
        // BARRIERS
        // =====================================================
        public readonly List<Transform> NearbyBarriers = new List<Transform>();


        // =====================================================
        // UI / LABEL
        // =====================================================
        private TextMesh _label;


        // =====================================================
        // SPAWN TIMING
        // =====================================================
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
            // unsubscribe from world shift events
            if (_wosSubscribed)
            {
                DVRT_WorldShiftManager.OnWorldShift -= HandleWorldShift;
                _wosSubscribed = false;
            }

            // destroy markers belonging to this factory
            foreach (var marker in Markers)
            {
                if (marker != null)
                {
                    marker.Destroy();
                    DVRT_Manager.UnregisterMarker(marker);
                }
            }
            Markers.Clear();

            // destroy the root object
            if (Root != null)
                GameObject.Destroy(Root);
        }

        public static void _______________BUILD_________________()
        {
        }

        public VehicleFactory(Vector3 worldPosition)
        {
            // worldPosition is already a SESSION position (raycast hit)
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

            Object.Destroy(arrow.GetComponent<Collider>());
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

        public void CacheNearbyBarriers()
        {
            if (!IsPlayerWithinActivationRange())
                return;

            NearbyBarriers.Clear();

            const float radius = 1000f;
            float radiusSq = radius * radius;

            Main.Log("******************************** [SCAN] **********************************");
            Main.Log($"[SCAN] Factory origin: {Root.transform.position}");

            var all = UnityEngine.Object.FindObjectsOfType<Transform>();
            Main.Log($"[SCAN] Found {all.Length} transforms in scene");

            foreach (var t in all)
            {
                if (t == null)
                    continue;

                // only interested in barrier roots
                if (!t.name.StartsWith("RailwayCrossingBarrierShort") &&
                    !t.name.StartsWith("RailwayCrossingBarrierLong"))
                    continue;

                Main.Log($"[SCAN] Candidate root found: {t.name} | rootPos={t.position}");

                if ((t.position - Root.transform.position).sqrMagnitude > radiusSq)
                {
                    Main.Log($"[SCAN] Rejected (out of range | rootPos={t.position})");
                    continue;
                }

                Main.Log($"[SCAN] Root is within range | rootPos={t.position}");

                var ramp = t.Find("Ramp");

                Transform collider = null;

                if (ramp != null)
                {
                    var coliders = ramp.Find("Coliders") ?? ramp.Find("Colliders");

                    if (coliders != null && coliders.childCount > 0)
                        collider = coliders.GetChild(0);
                }

                if (collider == null)
                {
                    Main.Log($"[SCAN] Collider path NOT found | rootPos={t.position}");
                    DumpBarrierChildren(t);
                    continue;
                }

                Main.Log($"[SCAN] Found Ramp/Colliders/Collider | rootPos={t.position}");

                NearbyBarriers.Add(collider);

                Main.Log($"[SCAN] Added barrier collider | rootPos={t.position} | colliderPos={collider.position}");
            }

            Main.Log($"[SCAN] Barrier scan complete | cached: {NearbyBarriers.Count}");
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

                // Must belong to a loaded scene
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;

                Transform root = lod.transform;
                if (root == null)
                    continue;

                // Distance filter
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
                    //cleanName.Contains("TruckSemi80s_01_Green") ||
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
                CacheNearbyBarriers();
            }

            if (_localArchetypes.Count == 0)
                return null;

            int index = Random.Range(0, _localArchetypes.Count);
            return _localArchetypes[index];
        }

        public void TryAutoSpawn()
        {
            if (Root == null)
                return;

            if (!IsPlayerNearEnough())   // 👈 ADD THIS
                return;

            if (!_isActivated)
                return;

            if (_trafficRate <= 0)
                return;

            if (_nextSpawnTime < 0f)
                _nextSpawnTime = Time.time + GetSpawnDelay();

            if (Time.time < _nextSpawnTime)
                return;            

            bool success = DVRT_Manager.SpawnFromFactory(this);

            if (success)
            {
                _nextSpawnTime = Time.time + GetSpawnDelay();
            }
            else
            {
                // retry soon
                _nextSpawnTime = Time.time + 1f;
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
                $"{p1}TTL: {TTL:0}s\n\n" +   // 👈 ADD THIS
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

        public static void ___________OTHER_HELPERS________________()
        {
        }                              

        private bool IsPlayerNearEnough()
        {
            Transform cam = Camera.main?.transform;
            if (cam == null)
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

        public void UpdateActivation()
        {
            bool inRange = IsPlayerNearEnough();

            if (inRange && !_isActivated)
            {
                _isActivated = true;

                // force immediate spawn
                _nextSpawnTime = Time.time;

                Main.Log($"[DVRT] VF activated at {Root.transform.position}");
            }
            else if (!inRange && _isActivated)
            {
                _isActivated = false;
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

