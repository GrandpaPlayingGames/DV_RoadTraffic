using DV_RoadTraffic;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DV_RoadTraffic
{
    public class TrafficVehicleController : MonoBehaviour, ITrafficDestructible
    {        
        // =====================================================
        // CORE COMPONENTS
        // =====================================================
        private Rigidbody rb;
        public VehicleFactory Factory;
        private TrafficVehicleAudio TrafficVehicleAudio;


        // =====================================================
        // LIFECYCLE / STATE
        // =====================================================
        private bool initialized = false;
        private bool physicsMode = false;

        private float ttl;
        private float spawnTime;


        // =====================================================
        // MOVEMENT
        // =====================================================
        private float baseSpeed;
        private float targetSpeed;
        private float currentSpeed;
        private float routeTargetSpeed;
        public float CurrentSpeed => currentSpeed;
        public float MaxSpeed => Mathf.Max(baseSpeed, targetSpeed);

        private float speed;
        private Vector3 lastPosition;
        private Vector3 lastVelocity;


        // =====================================================
        // TURNING / PATH FOLLOWING
        // =====================================================
        private bool turning = false;
        private float targetHeading = 0f;
        private float turnTimer = 0f;
        private float turnDuration = 2.0f;
        private float turnRadius = 6f;

        private Transform activeTurnTarget;
        private Transform pendingTurnTarget;

        private Quaternion startRotation;
        private Quaternion targetRotation;

        private bool aligningToExit = false;
        private float exitHeading = 0f;

        private float steeringBias = 0f;


        // =====================================================
        // VEHICLE DIMENSIONS
        // =====================================================
        private float vehicleLength;
        private float vehicleHalfLength;

        // =====================================================
        // VEHICLE COLLISION PREVENTION
        // =====================================================
        private HashSet<TrafficVehicleController> ignoredVehicles = new HashSet<TrafficVehicleController>();
        private float ignoreTimer = 0f;

        private const float IGNORE_RADIUS = 25f;
        private const float IGNORE_INTERVAL = 0.5f;

        private int dvrtMask;
        private Collider[] cachedColliders;
        private bool ignoreInitialized = false;

        private const int IGNORE_BUFFER_SIZE = 32;
        private readonly Collider[] _ignoreOverlapBuffer =
            new Collider[IGNORE_BUFFER_SIZE];


        // =====================================================
        // TRAFFIC / INTERACTION
        // =====================================================
        private float trafficDetectDistance = 15f;
        private float trafficSlowDistance = 6f;
        private float trafficSpeedLimit = float.MaxValue;

        private TrafficVehicleController vehicleAhead;


        // =====================================================
        // BLOCKING / DEADLOCK
        // =====================================================
        private bool isWaitingAtStopMarker = false;
        private bool deadlockOverride = false;

        private float mutualBlockTime = 0f;
        private TrafficVehicleController mutualBlockOther = null;


        // =====================================================
        // BARRIER SYSTEM
        // =====================================================
        private float barrierDetectDistance = 12f;
        private Transform barrierAhead = null;
        Transform lastDetectedBarrier = null;
        bool hasPassedFirstBarrier = false;
        Transform firstBarrier = null;
        Transform secondBarrier = null;


        // =====================================================
        // TRAINCAR DETECTION
        // =====================================================
        private TrainCar trainAhead = null;
        private bool cautiousForTrain = false;
        private bool isGhosted = false;        
        private float ghostEndTime;       
        private List<Renderer> ghostRenderers = new List<Renderer>();

        // =====================================================
        // RATE-LIMITED DETECTION
        // =====================================================
        private const float TRAFFIC_DETECTION_INTERVAL = 0.10f;
        private const float TRAIN_DETECTION_INTERVAL = 0.10f;

        private float nextTrafficDetectionTime;
        private float nextTrainDetectionTime;

        // =====================================================
        // STUCK DETECTION
        // =====================================================
        private float stuckTime = 0f;
        private const float stuckThreshold = 0.3f;
        private const float stuckTTL = 10f;
        private const float stuckTTL_Normal = 10f;
        private const float stuckTTL_Blocking = 60f;

        private bool isLegitimatelyBlocked = false;


        // =====================================================
        // WORLD SHIFT
        // =====================================================
        private Vector3 _canonicalPosition;
        private Quaternion _canonicalRotation;
        private Vector3 _lastKnownCanonical;


        // =====================================================
        // ROUTE / IDENTITY
        // =====================================================
        private string routeName;
        private string cleanName;
        private string lastMarkerEncountered = null;


        // =====================================================
        // AUDIO / EFFECTS
        // =====================================================
        private float nextHornTime = 0f;

        // =====================================================
        // HEADLIGHTS
        // =====================================================
        private Light headlight;
        private bool headlightsInitialized = false;
        private float headlightOnTime;
        private float headlightOffTime;
        private static object _weatherDriver;
        private static PropertyInfo _todProp;
        private static PropertyInfo _realProp;

        // =====================================================
        // COLLISION / IMPACT
        // =====================================================
        float impactCooldown = 0f;


        // =====================================================
        // DEBUG
        // =====================================================
        private bool debugAlignment = false;
        private float debugNextLogTime = 0f;
        private float debugDesiredHeading = 0f;



        public static void _______________SYSTEM_________________()
        {
        }
        public void Initialize(float moveSpeed, float lifetime, string route, string archetypeName, VehicleFactory factory)
        {
            routeName = route;
            cleanName = archetypeName;

            if (string.IsNullOrEmpty(routeName))
            {
                Main.Log($"[DVRT] WARNING: {gameObject.name} initialized with EMPTY route!");
            }
            else
            {
                Main.Log($"[DVRT] Vehicle {gameObject.name} assigned to route '{routeName}'");
            }

            Factory = factory;

            speed = moveSpeed;
            ttl = lifetime;
            spawnTime = Time.time;

            baseSpeed = moveSpeed;
            targetSpeed = moveSpeed;
            currentSpeed = moveSpeed;

            BuildSingleCollider();
            SetupRigidbody();
            SetupHeadlight();

            IgnoreDVLevelCrossingBarriers(Factory.NearbyBarriers);
            
            lastPosition = transform.position;

            string group = DVRT_SoundLibrary.DetermineVehicleGroup(cleanName);

            var engineClip = DVRT_SoundLibrary.GetRandomEngine(group);

            TrafficVehicleAudio = gameObject.AddComponent<TrafficVehicleAudio>();
            TrafficVehicleAudio.Initialize(this, engineClip);

            DVRT_WorldShiftManager.OnWorldShift += ApplyWorldShift;

            Vector3 shift = DVRT_WorldShiftManager.CurrentMove;
            _lastKnownCanonical = transform.position - shift;

            ghostRenderers = GetComponentsInChildren<Renderer>(true).ToList();

            cachedColliders = GetComponentsInChildren<Collider>();

            int layer = gameObject.layer;

            Main.Log($"[Collisions] Vehicle using layer {layer}");

            dvrtMask = 1 << layer;

            headlightOnTime = 18f + UnityEngine.Random.Range(0f, 0.25f);
            headlightOffTime = 7f + UnityEngine.Random.Range(0f, 0.25f);

            Main.Log($"[DVRT] {name} headlights ON at {headlightOnTime:F2}, OFF at {headlightOffTime:F2}");

            nextTrafficDetectionTime =
                Time.time + UnityEngine.Random.Range(0f, TRAFFIC_DETECTION_INTERVAL);

            nextTrainDetectionTime =
                Time.time + UnityEngine.Random.Range(0f, TRAIN_DETECTION_INTERVAL);


            initialized = true;
        }

        private int lastBlockState = 0;

        private void FixedUpdate()
        {
            if (!initialized)
                return;

            if (physicsMode)
                return;

            float now = Time.time;

            if (now >= nextTrafficDetectionTime)
            {
                nextTrafficDetectionTime =
                    now + TRAFFIC_DETECTION_INTERVAL;

                DetectVehicleAhead();
                DetectBarrierAhead();
            }

            bool runTrainChecks = now >= nextTrainDetectionTime;

            if (runTrainChecks)
            {
                nextTrainDetectionTime =
                    now + TRAIN_DETECTION_INTERVAL;

                DetectTrainAhead();
                DetectTrainTooClose();
                DetectTrainContact();
            }

            DetectDeadlock();

            int currentBlockState =
                vehicleAhead != null ? 1 :
                barrierAhead != null ? 2 :
                trainAhead != null ? 3 :
                isWaitingAtStopMarker ? 4 :
                0;

            if (currentBlockState != lastBlockState)
            {
                stuckTime = 0f;
            }

            lastBlockState = currentBlockState;

            isLegitimatelyBlocked = currentBlockState != 0;

            ApplyTrafficFollowing();
            MoveForwardDeterministic();

            if (runTrainChecks)
            {
                DetectTrainImpact();
            }

            CheckTTL();
            CheckIfStuck();
            TryRandomHorn();

            ignoreTimer += Time.fixedDeltaTime;

            if (!ignoreInitialized)
            {
                ignoreInitialized = true;
                UpdateNearbyIgnores();
            }
            else if (ignoreTimer >= IGNORE_INTERVAL)
            {
                ignoreTimer = 0f;
                UpdateNearbyIgnores();
            }
        }
        void LateUpdate()
        {
            Vector3 shift = DVRT_WorldShiftManager.CurrentMove;

            _lastKnownCanonical = transform.position - shift;

            Vector3 expected = _lastKnownCanonical + shift;

            if ((transform.position - expected).sqrMagnitude > 25f)
            {
                transform.position = expected;
            }
        }

        void OnDestroy()
        {
            DVRT_WorldShiftManager.OnWorldShift -= ApplyWorldShift;

            DVRT_Manager.ActiveVehicles.Remove(this);

            if (Factory != null)
                Factory.ActiveVehicles.Remove(this);
        }

        void UpdateNearbyIgnores()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                IGNORE_RADIUS,
                _ignoreOverlapBuffer,
                dvrtMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _ignoreOverlapBuffer[i];

                if (hit == null)
                    continue;

                TrafficVehicleController other =
                    hit.GetComponentInParent<TrafficVehicleController>();

                if (other == null || other == this)
                    continue;

                if (ignoredVehicles.Contains(other))
                    continue;

                IgnoreBetween(this, other);

                ignoredVehicles.Add(other);
                other.ignoredVehicles.Add(this);
            }

            for (int i = 0; i < hitCount; i++)
                _ignoreOverlapBuffer[i] = null;
        }

        public static void _______________SETUP_________________()
        {
        }

        private void SetupRigidbody()
        {
            rb = gameObject.GetComponent<Rigidbody>();

            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.mass = gameObject.name.StartsWith("Bus") ? 9000f : 1500f;

            rb.useGravity = true;
            rb.drag = 0f;
            rb.angularDrag = 2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        }

        public Bounds BuildSingleCollider()
        {
            var existing = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < existing.Length; i++)
                Destroy(existing[i]);

            var renderers = GetComponentsInChildren<MeshRenderer>(true);

            bool hasBounds = false;
            Bounds bounds = new Bounds();

            foreach (var r in renderers)
            {
                if (r.name.Contains("_01b"))
                    continue;

                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Bounds meshBounds = filter.sharedMesh.bounds;

                Vector3 worldCenter = r.transform.TransformPoint(meshBounds.center);
                Vector3 worldSize = Vector3.Scale(meshBounds.size, r.transform.lossyScale);

                Bounds worldBounds = new Bounds(worldCenter, worldSize);

                if (!hasBounds)
                {
                    bounds = worldBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(worldBounds);
                }
            }

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;

            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);

            localCenter.x = 0f;

            localCenter.y += bounds.size.y * 0.025f;

            box.center = localCenter;

            Vector3 size = bounds.size;

            float length = Mathf.Max(size.x, size.z);
            float width = Mathf.Min(size.x, size.z);

            if (width > 2.8f)
                width = 2.8f;

            vehicleLength = length;

            box.size = new Vector3(width, size.y, length);

            return bounds;
        }

        private void SetupHeadlight()
        {
            if (headlightsInitialized)
                return;

            headlightsInitialized = true;

            var col = GetComponent<Collider>();
            if (col == null)
            {
                Main.Log($"[DVRT] No collider found for headlight setup on {name}");
                return;
            }

            float halfLength = col.bounds.extents.z;

            Vector3 lightPos =
                transform.position +
                transform.forward * (halfLength + 0.3f) +
                Vector3.up * 0.8f;

            GameObject lightGO = new GameObject("Headlight");
            lightGO.transform.SetParent(transform);

            lightGO.transform.position = lightPos;
            lightGO.transform.rotation =
                Quaternion.LookRotation(transform.forward + Vector3.down * 0.15f, Vector3.up);

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 35f;
            light.spotAngle = 50f;
            light.intensity = 3f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.None;
            light.enabled = false;

            headlight = light;

            Main.Log($"[DVRT] Headlight created for {name}");
        }

        public void IgnoreDVLevelCrossingBarriers(List<Transform> nearbyBarriers)
        {
            if (nearbyBarriers == null || nearbyBarriers.Count == 0)
                return;

            Collider[] myColliders = GetComponentsInChildren<Collider>(true);

            if (myColliders == null || myColliders.Length == 0)
                return;

            foreach (var barrierTransform in nearbyBarriers)
            {
                if (barrierTransform == null)
                    continue;

                Collider barrierCol = barrierTransform.GetComponent<Collider>();

                if (barrierCol == null)
                    continue;

                foreach (var myCol in myColliders)
                {
                    if (myCol == null)
                        continue;

                    Physics.IgnoreCollision(myCol, barrierCol, true);
                }
            }
        }

        public static void _______________MOVEMENT_________________()
        {
        }

        private void MoveForwardDeterministic()
        {
            if (debugAlignment && Time.time >= debugNextLogTime)
            {
                float current = transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(current, debugDesiredHeading);

                string direction = delta > 0f ? "RIGHT" : "LEFT";

                Main.Log(
                    $"[DVRT] ALIGN STEP | {gameObject.name} ({GetInstanceID()}) | current={current:F1} target={debugDesiredHeading:F1} delta={delta:F1} turning={direction}",
                    false);

                debugNextLogTime = Time.time + 1f;

                if (Mathf.Abs(delta) < 1f)
                {
                    Main.Log(
                        $"[DVRT] ALIGN COMPLETE | {gameObject.name} ({GetInstanceID()}) | final={current:F1}",
                        false);

                    debugAlignment = false;
                }
            }

            if (activeTurnTarget != null && pendingTurnTarget == null)
            {
                Vector3 local = transform.InverseTransformPoint(activeTurnTarget.position);

                if (local.z < 0f)
                {
                    pendingTurnTarget = activeTurnTarget;

                    Main.Log(
                        $"[DVRT] SAFETY TURN ACTIVATED | {gameObject.name} ({GetInstanceID()})");
                }
            }

            if (pendingTurnTarget != null)
            {
                Vector3 local = transform.InverseTransformPoint(pendingTurnTarget.position);

                if (local.z < vehicleHalfLength)
                {                    
                    activeTurnTarget = null;

                    exitHeading = pendingTurnTarget.eulerAngles.y;
                    aligningToExit = true;

                    pendingTurnTarget = null;
                }
            }

            if (activeTurnTarget != null)
            {
                Vector3 targetPos = activeTurnTarget.position;
                
                Vector3 localTarget = transform.InverseTransformPoint(targetPos);

                float distance = localTarget.magnitude;

                if (distance > 0.1f)
                {
                    float curvature = (2f * localTarget.x) / (distance * distance);
                    steeringBias = Mathf.Clamp(curvature * 5f, -1f, 1f);

                    float angularVelocity = speed * curvature;

                    float yaw = angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime;

                    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));
                }
            }

            if (activeTurnTarget == null)
            {
                steeringBias = Mathf.Lerp(steeringBias, 0f, 5f * Time.fixedDeltaTime);
            }

            if (aligningToExit)
            {
                float currentYaw = transform.eulerAngles.y;

                float delta = Mathf.DeltaAngle(currentYaw, exitHeading);

                float step = 45f * Time.fixedDeltaTime; 

                float turn = Mathf.Clamp(delta, -step, step);

                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
                if (Mathf.Abs(delta) < 1f)
                {
                    Vector3 rot = transform.eulerAngles;
                    rot.y = exitHeading;
                    transform.eulerAngles = rot;

                    aligningToExit = false;
                }
            }
 
            float desiredSpeed = Mathf.Min(targetSpeed, trafficSpeedLimit);
            currentSpeed = Mathf.Lerp(currentSpeed, desiredSpeed, 1.5f * Time.fixedDeltaTime);

            Vector3 forwardMove = transform.forward * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + forwardMove);
        }          

        public static void _______________TRAFFIC_CONTROL_________________()
        {
        }
        private System.Collections.IEnumerator HandleStop(float seconds)
        {
            float originalTarget = targetSpeed;

            isWaitingAtStopMarker = true;

            targetSpeed = 0f;

            while (currentSpeed > 0.1f)
                yield return null;

            yield return new WaitForSeconds(seconds);

            targetSpeed = originalTarget;

            isWaitingAtStopMarker = false;
        }


        private System.Collections.IEnumerator HandleStopAndDespawn(TrafficMarker marker)
        {
            float stopTime = marker.StopSeconds;

            float originalTarget = targetSpeed;

            isWaitingAtStopMarker = true;

            targetSpeed = 0f;

            while (currentSpeed > 0.1f)
                yield return null;

            yield return new WaitForSeconds(stopTime);

            isWaitingAtStopMarker = false;

            Destroy(gameObject);
        }

        void DetectBarrierAhead()
        {
            if (isGhosted)
                return;

            Transform detectedAhead = null;
            barrierAhead = null;

            float closestDistSq = float.MaxValue;
            float detectDistSq =
                barrierDetectDistance * barrierDetectDistance;

            int validBarrierCount = 0;
            float closestAnyDistance = float.MaxValue;
            float closestAnyForwardDot = 0f;
            Transform closestAnyBarrier = null;

            foreach (var barrier in Factory.NearbyBarriers)
            {
                if (barrier == null)
                    continue;

                validBarrierCount++;

                Vector3 toBarrier =
                    barrier.position - transform.position;

                float distance = toBarrier.magnitude;
                float forwardDot =
                    Vector3.Dot(transform.forward, toBarrier);

                if (distance < closestAnyDistance)
                {
                    closestAnyDistance = distance;
                    closestAnyForwardDot = forwardDot;
                    closestAnyBarrier = barrier;
                }

                if (forwardDot <= 0f)
                    continue;

                float distSq = toBarrier.sqrMagnitude;

                if (distSq > detectDistSq)
                    continue;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    detectedAhead = barrier;
                }
            }
 
            if (detectedAhead == null)
            {
                if (firstBarrier != null && secondBarrier != null)
                {
                    firstBarrier = null;
                    secondBarrier = null;
                }

                return;
            }

            float z = detectedAhead.eulerAngles.z;
            bool isLowered = z <= 45f;

            if (!Main.Settings.allowVehicleEscape)
            {
                firstBarrier = null;
                secondBarrier = null;

                if (isLowered)
                {
                    barrierAhead = detectedAhead; 
                }

                return;
            }

            // 🔥 FIRST barrier
            if (firstBarrier == null)
            {
                firstBarrier = detectedAhead;

                Main.Log("[DVRT] First barrier detected");

                if (isLowered)
                {
                    barrierAhead = detectedAhead; 
                }

                return;
            }

            if (detectedAhead == firstBarrier)
            {
                if (isLowered)
                {
                    barrierAhead = detectedAhead; 
                }

                return;
            }

            if (secondBarrier == null)
            {
                secondBarrier = detectedAhead;

                Main.Log("[DVRT] Second barrier detected → entering escape mode");

                barrierAhead = null; 

                return;
            }

            barrierAhead = null;
        }


        void DetectVehicleAhead()
        {
            vehicleAhead = null;

            if (Factory == null)
                return;

            List<TrafficVehicleController> vehicles =
                Factory.ActiveVehicles;

            float closestDistSq = float.MaxValue;

            Vector3 forwardDir = transform.forward;

            if (activeTurnTarget != null)
            {
                Vector3 toTarget =
                    activeTurnTarget.position - transform.position;

                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.1f)
                    forwardDir = toTarget.normalized;
            }

            Vector3 rightDir =
                Vector3.Cross(Vector3.up, forwardDir);

            float laneWidth =
                activeTurnTarget != null ? 3.0f : 2.0f;

            Vector3 myPosition = transform.position;

            const float lengthFactor = 0.75f;

            for (int i = 0; i < vehicles.Count; i++)
            {
                TrafficVehicleController other =
                    vehicles[i];

                if (other == null || other == this)
                    continue;

                Vector3 toOther =
                    other.transform.position - myPosition;

                float localZ =
                    Vector3.Dot(toOther, forwardDir);

                if (localZ <= 0f)
                    continue;

                float localX =
                    Vector3.Dot(toOther, rightDir);

                if (Mathf.Abs(localX) > laneWidth)
                    continue;

                float clearance =
                    localZ
                    - (vehicleLength * 0.5f * lengthFactor)
                    - (other.vehicleLength * 0.5f * lengthFactor);

                if (clearance > trafficDetectDistance)
                    continue;

                if (clearance < 0f)
                {
                    vehicleAhead = other;
                    break;
                }

                float distSq =
                    clearance * clearance;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    vehicleAhead = other;
                }
            }
        }

        void DetectTrainAhead()
        {
            if (!Main.Settings.stopIfTrainAhead)
                return;

            if (isGhosted) return;

            trainAhead = null;
            cautiousForTrain = false;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 direction = transform.forward;

            float detectDistance = trafficDetectDistance; 
            float laneWidth = 2.5f; 

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                1.2f, // 
                direction,
                detectDistance
            );

            if (hits == null || hits.Length == 0)
                return;

            float closestDistSq = float.MaxValue;
            TrainCar closestTrain = null;
            bool closestInLane = false;

            var lastLoco = PlayerManager.LastLoco;

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                    continue;

                TrainCar tc = hit.collider.GetComponentInParent<TrainCar>();
                if (tc == null)
                    continue;

                Vector3 toHit = hit.point - transform.position;

                float forwardDot = Vector3.Dot(transform.forward, toHit.normalized);
                if (forwardDot < 0.5f)
                    continue;

                float distSq = toHit.sqrMagnitude;
                if (distSq > detectDistance * detectDistance)
                    continue;

                Vector3 local = transform.InverseTransformPoint(hit.point);

                bool isInLane = Mathf.Abs(local.x) < laneWidth;

                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestTrain = tc;
                    closestInLane = isInLane;
                }
            }

            if (closestTrain == null)
                return;
            
            if (closestInLane)
            {
                trainAhead = closestTrain;
                return;
            }

            if (lastLoco != null && closestTrain == lastLoco)
            {
                float dist = Mathf.Sqrt(closestDistSq);

                if (dist < 25f) 
                {
                    cautiousForTrain = true;
                }
            }
        }

        void DetectTrainTooClose()
        {
            if (!Main.Settings.ignoreTrainImpact)
                return;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 direction = transform.forward;

            float detectDistance = trafficDetectDistance;
            float laneWidth = 2.5f;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                1.2f,
                direction,
                detectDistance
            );

            float closestForwardDist = float.MaxValue;
            TrainCar closestTrain = null;
            bool closestInLane = false;

            Collider col = GetComponent<Collider>();

            float halfLength = 2.5f; 
            if (col != null)
                halfLength = Mathf.Max(halfLength, col.bounds.extents.z);

            Vector3 front = transform.position + transform.forward * halfLength;
            Vector3 back = transform.position - transform.forward * halfLength;

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                    continue;

                TrainCar tc = hit.collider.GetComponentInParent<TrainCar>();
                if (tc == null)
                    continue;

                Vector3 toHit = hit.point - front;
                float forwardDist = Vector3.Dot(transform.forward, toHit);

                if (forwardDist < 0f)
                    continue; 

                Vector3 local = transform.InverseTransformPoint(hit.point);
                bool isInLane = Mathf.Abs(local.x) < laneWidth;

                if (forwardDist < closestForwardDist)
                {
                    closestForwardDist = forwardDist;
                    closestTrain = tc;
                    closestInLane = isInLane;
                }
            }

            float ghostEnterDistance = 3.0f;

            if (!isGhosted)
            {
                if (closestTrain != null && closestInLane && closestForwardDist < ghostEnterDistance)
                {
                    Main.Log($"[DVRT] ENTER GHOST | dist={closestForwardDist:F2} | {gameObject.name}");

                    EnableGhostMode(closestTrain);

                    float baseTime = 3.0f; 

                    float vehicleLength = halfLength * 2f;

                    float extraTimePerMeter = 0.15f;

                    float duration = baseTime + (vehicleLength * extraTimePerMeter);

                    ghostEndTime = Time.time + duration;
                }

                return;
            }

            // EXIT (timer only)
            if (Time.time >= ghostEndTime)
            {
                Main.Log($"[DVRT] EXIT GHOST (timer) | {gameObject.name}");

                DisableGhostMode();
            }
        }

        void DetectTrainContact()
        {
            if (!Main.Settings.ignoreTrainImpact)
                return;

            Collider col = GetComponent<Collider>();
            if (col == null)
                return;

            BoxCollider box = col as BoxCollider;
            if (box == null)
                return;

            Vector3 worldCenter = transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, transform.lossyScale);

            float padding = 0.25f;

            Collider[] overlaps = Physics.OverlapBox(
                worldCenter,
                halfExtents + Vector3.one * padding,
                transform.rotation
            );

            bool trainNearby = false;
            TrainCar detectedTrain = null;

            foreach (var c in overlaps)
            {
                if (c == null)
                    continue;

                TrainCar tc = c.GetComponentInParent<TrainCar>();
                if (tc == null)
                    continue;

                trainNearby = true;
                detectedTrain = tc;
                break;
            }

            if (trainNearby)
            {
                if (!isGhosted)
                {
                    Main.Log($"[DVRT] CONTACT → ENTER GHOST | {gameObject.name}");
                    EnableGhostMode(detectedTrain);
                }

                ghostEndTime = Time.time + 2.0f;

                return;
            }

            if (isGhosted && Time.time >= ghostEndTime)
            {
                Main.Log($"[DVRT] CLEAR → EXIT GHOST | {gameObject.name}");
                DisableGhostMode();
            }
        }

        void DetectDeadlock()
        {
            deadlockOverride = false;

            if (vehicleAhead == null)
            {
                mutualBlockTime = 0f;
                mutualBlockOther = null;
                return;
            }

            var other = vehicleAhead;

            if (other == null)
            {
                mutualBlockTime = 0f;
                mutualBlockOther = null;
                return;
            }

            bool mutualBlock = other.vehicleAhead == this;

            if (!mutualBlock)
            {
                mutualBlockTime = 0f;
                mutualBlockOther = null;
                return;
            }

            if (barrierAhead != null || isWaitingAtStopMarker ||
                other.barrierAhead != null || other.isWaitingAtStopMarker)
            {
                mutualBlockTime = 0f;
                mutualBlockOther = null;
                return;
            }

            if (mutualBlockOther != other)
            {
                mutualBlockOther = other;
                mutualBlockTime = 0f;
                return;
            }

            mutualBlockTime += Time.fixedDeltaTime;

            if (mutualBlockTime < 0.75f)
                return;

            if (GetInstanceID() < other.GetInstanceID())
            {
                deadlockOverride = true;

                Main.Log(
                    $"[DVRT] DEADLOCK OVERRIDE | {cleanName} proceeds, {other.cleanName} yields",
                    false);
            }
        }

        void ApplyTrafficFollowing()
        {
            trafficSpeedLimit = float.MaxValue;

            if (vehicleAhead != null && !deadlockOverride)
            { 
                Vector3 myFront = transform.position + transform.forward * (vehicleLength * 0.5f);
                Vector3 otherBack = vehicleAhead.transform.position - vehicleAhead.transform.forward * (vehicleAhead.vehicleLength * 0.5f);

                float distance = Vector3.Distance(myFront, otherBack);

                float minGap = 2.0f; 
                float ratio = Mathf.Clamp01((distance - minGap) / (trafficDetectDistance - minGap));

                float limit =
                    Mathf.Lerp(0f, routeTargetSpeed, ratio);

                trafficSpeedLimit = Mathf.Min(trafficSpeedLimit, limit);
            }

            if (barrierAhead != null)
            {
                float dist =
                   Vector3.Distance(transform.position, barrierAhead.position);

                float brakeStart = 8f;     
                float stopBuffer = 3.5f;   

                float ratio =
                    Mathf.Clamp01((dist  - stopBuffer) / (brakeStart - stopBuffer));

                float limit =
                    Mathf.Lerp(0f, routeTargetSpeed, ratio);

                trafficSpeedLimit =
                    Mathf.Min(trafficSpeedLimit, limit);

            }

            if (trainAhead != null)
            {
                Vector3 myFront =
                    transform.position + transform.forward * (vehicleLength * 0.5f);

                Vector3 trainPos = trainAhead.transform.position;

                float dist =
                    Vector3.Distance(myFront, trainPos);

                float brakeStart = 8f;
                float stopBuffer = 3.5f;

                float ratio =
                    Mathf.Clamp01((dist - stopBuffer) / (brakeStart - stopBuffer));

                float limit =
                    Mathf.Lerp(0f, routeTargetSpeed, ratio);

                trafficSpeedLimit =
                    Mathf.Min(trafficSpeedLimit, limit);
            }

            if (cautiousForTrain)
            {
                float cautiousSpeed = routeTargetSpeed * 0.6f;

                trafficSpeedLimit =
                    Mathf.Min(trafficSpeedLimit, cautiousSpeed);
            }
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

        private bool gtaImpactHandled = false;
  
        private void OnCollisionEnter(Collision collision)
        {
            if (gtaImpactHandled)
                return;

            if (Time.time < impactCooldown)
                return;

            var train = collision.collider.GetComponentInParent<TrainCar>();
            if (train == null)
                return;

            gtaImpactHandled = true;
            impactCooldown = Time.time + 1f;

            Main.Log($"[DVRT_Collision] OnCollisionEnter triggered on {gameObject.name}");

            GTAImpactHandler.HandleTrainImpact(this, train, collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (gtaImpactHandled)
                return;

            if (Time.time < impactCooldown)
                return;

            var train = collision.collider.GetComponentInParent<TrainCar>();
            if (train == null)
                return;

            gtaImpactHandled = true;
            impactCooldown = Time.time + 1f;

            Main.Log($"[DVRT_Collision] OnCollisionStay fallback triggered on {gameObject.name}");

            GTAImpactHandler.HandleTrainImpact(this, train, collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            var markerComponent = other.GetComponent<TrafficMarkerComponent>();
            if (markerComponent == null)
                return;

            var marker = markerComponent.Marker;
            if (marker == null)
                return;

            Main.Log(
                $"[DVRT] VEHICLE {gameObject.name} ({GetInstanceID()}) " +
                $"route='{routeName}' encountered marker {marker.MarkerID} " +
                $"route='{marker.RouteName}' type={marker.Type}",
                false);


            if (marker.RouteName != routeName)
                return;        

            if (marker.Type == TrafficMarker.MarkerType.TurnTo)
            {
                if (!string.IsNullOrEmpty(lastMarkerEncountered) &&
    marker.MarkerID == lastMarkerEncountered)
                {
                    return;
                }

                lastMarkerEncountered = marker.MarkerID;

                if (string.IsNullOrEmpty(marker.TargetMarkerID))
                    return;

                TrafficMarker target = DVRT_Manager.FindMarkerByID(marker.TargetMarkerID);
                if (target == null)
                    return;

                pendingTurnTarget = null;
                aligningToExit = false;

                activeTurnTarget = target.Root.transform;
                return;
            }

            if (marker.Type == TrafficMarker.MarkerType.RandomlyTurnTo)
            {
                if (!string.IsNullOrEmpty(lastMarkerEncountered) &&
                    marker.MarkerID == lastMarkerEncountered)
                {
                    return;
                }

                lastMarkerEncountered = marker.MarkerID;

                string chosenID;

                if (Random.value < 0.5f)
                    chosenID = marker.TargetMarkerID1;
                else
                    chosenID = marker.TargetMarkerID2;

                if (string.IsNullOrEmpty(chosenID))
                    return;

                Main.Log($"[DVRT] RANDOM TURN chose {chosenID}");

                TrafficMarker target = DVRT_Manager.FindMarkerByID(chosenID);
                if (target == null)
                    return;

                pendingTurnTarget = null;
                aligningToExit = false;

                activeTurnTarget = target.Root.transform;
                return;
            }

            if (marker.Type == TrafficMarker.MarkerType.TurnTarget)
            {
                if (activeTurnTarget == null || marker.Root.transform != activeTurnTarget)
                    return;

                if (pendingTurnTarget == marker.Root.transform)
                    return;

                Vector3 pivot = transform.position;
                Vector3 target = marker.Root.transform.position;

                float distance = Vector3.Distance(pivot, target);

                float current = transform.eulerAngles.y;
                float desired = marker.Root.transform.eulerAngles.y;

                Main.Log(
                    $"[DVRT] TURN TARGET CONFIRMED | {gameObject.name} ({GetInstanceID()}) | " +
                    $"pivotDist={distance:F2}m | current={current:F1}° | target={desired:F1}°");

                pendingTurnTarget = marker.Root.transform;

                debugAlignment = true;
                debugNextLogTime = Time.time;
                debugDesiredHeading = desired;
                Main.Log(
                    $"[DVRT] ALIGN START | {gameObject.name} ({GetInstanceID()}) | current={current:F1} target={desired:F1}");
            }
           
            if (marker.Type == TrafficMarker.MarkerType.SpeedUp)
            {
                int level = marker.SpeedLevel;
                float delta = level * 2.8f;

                targetSpeed += delta;

                return;
            }

            if (marker.Type == TrafficMarker.MarkerType.SlowDown)
            {
                int level = marker.SpeedLevel;
                float delta = level * 2.8f;

                targetSpeed -= delta;

                targetSpeed = Mathf.Max(0.5f, targetSpeed);

                return;
            }

            if (marker.Type == TrafficMarker.MarkerType.Stop)
            {
                StartCoroutine(HandleStop(marker.StopSeconds));
                return;
            }

            if (marker.Type == TrafficMarker.MarkerType.StopAndDespawn)
            {
                StartCoroutine(HandleStopAndDespawn(marker));
            }

            if (marker.Type == TrafficMarker.MarkerType.Despawn)
            {
                Main.Log($"Destroying {gameObject.name} after Despawn marker hit");
                Destroy(gameObject);
                return;
            }

        }

        public static void _______________SOUNDS_________________()
        {
        }

        void TryRandomHorn()
        {
            if (TrafficVehicleAudio == null)
                return;

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            float dist = Vector3.Distance(transform.position, Camera.main.transform.position);

            if (dist > 40f)
                return;

            if (Time.time < nextHornTime)
                return;

            if (UnityEngine.Random.value < 0.0005f)
            {
                string group = DVRT_SoundLibrary.DetermineVehicleGroup(cleanName);

                var horn = DVRT_SoundLibrary.GetRandomHorn(group);

                TrafficVehicleAudio.PlayHorn(horn);

                nextHornTime = Time.time + UnityEngine.Random.Range(5f, 15f);
            }
        }

        public static void _______________HEADLIGHTS_________________()
        {
        }

        public void UpdateHeadlights(float currentHour)
        {
            if (headlight == null)
                return;

            bool shouldBeOn;

            if (currentHour >= headlightOnTime || currentHour < headlightOffTime)
                shouldBeOn = true;
            else
                shouldBeOn = false;

            if (headlight.enabled != shouldBeOn)
                headlight.enabled = shouldBeOn;
        }

        public void SetHeadlights(bool on)
        {
            if (headlight == null)
                return;

            headlight.enabled = on;
        }

        public static void __________BLOWING_SHIT_UP_______________()
        {
        }

        public bool destroyed = false;
        private void DetectTrainImpact()
        {
            if (Main.Settings.ignoreTrainImpact)
                return;

            if (Time.time < impactCooldown)
                return;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                return;

            Collider[] hits = Physics.OverlapSphere(transform.position, 3f);

            foreach (var col in hits)
            {
                if (col == null)
                    continue;
                
                TrainCar tc = col.GetComponentInParent<TrainCar>();
               
                if (tc == null)
                {
                    string name = col.name.ToLower();

                    if (name.Contains("bogie") ||
                        name.Contains("wheel") ||
                        name.Contains("train"))
                    {
                        Main.Log($"[DVRT DEBUG] Train-like collider detected: {col.name}");
                        tc = col.GetComponentInParent<TrainCar>(); // retry
                    }
                }

                if (tc == null)
                    continue;

                Main.Log($"[DVRT GTA] Impact detected on {gameObject.name}");

                impactCooldown = Time.time + 1f;

                tc = col.GetComponentInParent<TrainCar>();

                if (tc != null)
                {
                    GTAImpactHandler.HandleTrainImpact(this, tc);
                }
                return;
            }
        }

        bool ghostDebug = true;

        private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

        private int ghostEnterCount = 0;

        private Color[] ghostDebugColors = new Color[]
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.cyan
        };

          void EnableGhostMode(TrainCar tc)
        {
            if (isGhosted)
                return;

            isGhosted = true;

            if (ghostRenderers.Count == 0)
                ghostRenderers = GetComponentsInChildren<Renderer>(true).ToList();

            foreach (var r in ghostRenderers)
            {
                if (r != null)
                    r.enabled = false;
            }

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;

                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Main.Log("[DVRT] GHOST ENABLED");
        }

        void DisableGhostMode()
        {
            if (!isGhosted)
                return;

            isGhosted = false;

            foreach (var r in ghostRenderers)
            {
                if (r != null)
                    r.enabled = true;
            }

            transform.position += Vector3.up * 0.1f;

            Physics.SyncTransforms();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.detectCollisions = true;
            }

            StartCoroutine(RestorePhysicsAfterSettle());

            Main.Log("[DVRT] GHOST DISABLED (deferred physics)");
        }

        IEnumerator RestorePhysicsAfterSettle()
        {
            yield return new WaitForFixedUpdate();

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.isKinematic = false;
            }
        }

        public void DestroyVehicle(Vector3 impulse, bool explosive)
        {
            if (destroyed) return;
            destroyed = true;

            Main.Log("[DVRT] Vehicle destroyed");

            gtaImpactHandled = false;

            physicsMode = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.detectCollisions = true;

            rb.WakeUp();

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 launchVelocity;

            if (impulse == Vector3.zero)
            {
                Vector3 randomDir = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(0.8f, 1.4f),
                    Random.Range(-1f, 1f)
                ).normalized;

                float launchSpeed = Random.Range(8f, 14f);

                launchVelocity = randomDir * launchSpeed;
            }
            else
            {
                launchVelocity = impulse;
            }

            rb.velocity = launchVelocity;

            rb.angularVelocity = Random.insideUnitSphere * 6f;

            if (explosive)
            {
                PlayExplosionSound();
                SpawnExplosion();
            }

            Destroy(gameObject, 3.5f);
        }

        void SpawnExplosion()
        {
            Vector3 pos = transform.position + Vector3.up * 0.5f;

            GameObject explosion = new GameObject("DVRT_Explosion");
            explosion.transform.position = pos;

            var ps = explosion.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.duration = 0.6f;
            main.startLifetime = 0.4f;
            main.startSpeed = 6f;
            main.startSize = 2.0f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
        new ParticleSystem.Burst(0f, 50)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = DVRT_ParticleLibrary.ExplosionMaterial;

            ps.Play();

            Destroy(explosion, 2f);

            SpawnFire();
        }

        void SpawnFire()
        {
            GameObject fire = new GameObject("DVRT_Fire");

            fire.transform.SetParent(transform);

            var col = GetComponent<Collider>();

            float height = 1.2f;

            if (col != null)
                height = col.bounds.size.y * 0.6f;

            fire.transform.localPosition = new Vector3(0, height, 0);

            var ps = fire.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.5f;
            main.startSize = 1.2f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = DVRT_ParticleLibrary.FireMaterial;

            ps.Play();

            Destroy(fire, 4f);
        }
        void PlayExplosionSound()
        {
            var clip = DVRT_SoundLibrary.GetRandomExplosion();

            if (clip == null)
                return;

            GameObject audioGO = new GameObject("DVRT_ExplosionAudio");
            audioGO.transform.position = transform.position;

            var src = audioGO.AddComponent<AudioSource>();

            src.clip = clip;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 80f;
            src.minDistance = 4f;

            src.pitch = UnityEngine.Random.Range(0.9f, 1.1f);

            src.Play();

            Destroy(audioGO, clip.length + 0.5f);
        }

        public static void _______________HELPERS_________________()
        {
        }
        public static float GetGameHour()
        {
            try
            {
                if (_weatherDriver == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

                    for (int i = 0; i < all.Length; i++)
                    {
                        var mb = all[i];
                        if (mb == null)
                            continue;

                        if (mb.GetType().FullName == "DV.WeatherSystem.WeatherDriver")
                        {
                            _weatherDriver = mb;

                            _todProp = mb.GetType().GetProperty("TimeOfDayHours",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                            if (_todProp != null)
                            {
                                var tod = _todProp.GetValue(mb, null);
                                if (tod != null)
                                {
                                    _realProp = tod.GetType().GetProperty("RealValue",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                }
                            }

                            break;
                        }
                    }
                }

                if (_weatherDriver != null && _todProp != null && _realProp != null)
                {
                    var tod = _todProp.GetValue(_weatherDriver, null);
                    if (tod != null)
                        return (float)_realProp.GetValue(tod, null);
                }
            }
            catch { }

            return 12f;
        }

        void IgnoreBetween(TrafficVehicleController a, TrafficVehicleController b)
        {
            if (a == null || b == null)
                return;

            var colsA = a.cachedColliders;
            var colsB = b.cachedColliders;

            if (colsA == null || colsB == null)
                return;

            for (int i = 0; i < colsA.Length; i++)
            {
                var colA = colsA[i];
                if (colA == null)
                    continue;

                for (int j = 0; j < colsB.Length; j++)
                {
                    var colB = colsB[j];
                    if (colB == null)
                        continue;

                    Physics.IgnoreCollision(colA, colB, true);
                }
            }
        }

        public void ApplyWorldShift(Vector3 delta)
        {
            transform.position += delta;
        }

        private void CheckTTL()
        {
            if (Time.time - spawnTime > ttl)
                Destroy(gameObject);
        }

        private void CheckIfStuck()
        {
            if (isGhosted)
            {
                stuckTime = 0f;
                lastPosition = transform.position;
                return;
            }

            if (Time.time - spawnTime < 2f)
                return;

            Vector3 delta = transform.position - lastPosition;

            Vector3 flatDelta = delta;
            flatDelta.y = 0f;

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
                flatForward.Normalize();

            float forwardProgress = Vector3.Dot(flatDelta, flatForward);
            float sqrMove = flatDelta.sqrMagnitude;

            float ttl = isLegitimatelyBlocked
                ? stuckTTL_Blocking   // 60s
                : stuckTTL_Normal;    // 10s

            bool tryingToMove = targetSpeed > 0.5f;

            bool noForwardProgress = forwardProgress < 0.02f;   
            bool lowMovement = sqrMove < 0.001f;                

            bool isStuckCondition = tryingToMove && (noForwardProgress || lowMovement);

            if (isStuckCondition)
            {
                stuckTime += Time.fixedDeltaTime;
            }
            else
            {
                stuckTime -= Time.fixedDeltaTime * 0.5f;
                if (stuckTime < 0f)
                    stuckTime = 0f;
            }

            if (stuckTime >= ttl)
            {
                Main.Log("[DVRT] Vehicle destroyed (stuck)");
                Destroy(gameObject);
            }

            lastPosition = transform.position;
        }        
    }
}
