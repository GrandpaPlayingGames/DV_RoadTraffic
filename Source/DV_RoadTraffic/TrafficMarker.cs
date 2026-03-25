
using System;
using UnityEngine;

namespace DV_RoadTraffic
{
    public class TrafficMarker
    {

        public enum MarkerType
        {
            TurnTo,
            RandomlyTurnTo,
            TurnTarget,
            SpeedUp,
            SlowDown,
            Stop,
            Despawn,
            StopAndDespawn
        }
        public GameObject Root { get; private set; }

        private Vector3 _canonicalPosition;
        private Quaternion _canonicalRotation;

        private Material _material;

        private Color _groupColor = Color.cyan;

        private float _turnDegrees = 0f;
        private int _speedLevel = 1;
        public int SpeedLevel => _speedLevel;
        private float _stopSeconds = 3f;
        public float StopSeconds => _stopSeconds;
        private TextMesh _label;

        public float TurnDegrees => _turnDegrees;

        public string MarkerID { get; private set; }
        public MarkerType Type { get; private set; } = MarkerType.TurnTo;

        private GameObject _forwardIndicator;

        public string TargetMarkerID { get; private set; } = null;

        public string TargetMarkerID1;
        public string TargetMarkerID2;

        public string RouteName;

        public TrafficMarker(Vector3 worldPos)
        {
            _canonicalPosition = worldPos - DVRT_WorldShiftManager.CurrentMove;
            _canonicalRotation = Quaternion.identity;

            Root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Root.transform.localScale = new Vector3(2.5f, 1f, 0.8f);

            CreateForwardIndicator();
            UpdateArrowVisibility();

            MarkerID = Guid.NewGuid().ToString();

            var collider = Root.GetComponent<Collider>();
            collider.isTrigger = true;

            var markerComponent = Root.AddComponent<TrafficMarkerComponent>();
            markerComponent.Marker = this;

            _material = Root.GetComponent<Renderer>().material;
            _material.color = _groupColor;

            Root.GetComponent<Renderer>().enabled = false;

            CreateLabel();

            ApplyTransform();

            DVRT_WorldShiftManager.OnWorldShift += HandleWorldShift;
        }

        public void SetSpeedLevel(int level)
        {
         
            _speedLevel = Mathf.Clamp(level, 1, 5);
            UpdateLabel();
        }

        public void AdjustSpeedLevel(float scroll)
        {
        
            int step = scroll > 0 ? 1 : -1;

            int newLevel = Mathf.Clamp(_speedLevel + step, 1, 5);

            if (newLevel == _speedLevel)
                return;

            _speedLevel = newLevel;
            DVRT_Manager.MarkUnsavedChanges();
            UpdateLabel();
        }

        public void AdjustStopSeconds(float scroll)
        {
            int step = scroll > 0 ? 1 : -1;

            float newValue = Mathf.Clamp(_stopSeconds + step, 1f, 20f);

            if (Mathf.Approximately(newValue, _stopSeconds))
                return;

            _stopSeconds = newValue;
            DVRT_Manager.MarkUnsavedChanges();
            UpdateLabel();
        }

        public void SetStopSeconds(float seconds)
        {
            _stopSeconds = Mathf.Clamp(seconds, 1f, 20f);
            UpdateLabel();
        }
        private void CreateLabel()
        {
            GameObject labelObj = new GameObject("MarkerLabel");
            labelObj.transform.SetParent(Root.transform);
            labelObj.transform.localPosition = new Vector3(0f, 2f, 0f);

            _label = labelObj.AddComponent<TextMesh>();
            _label.fontSize = 64;
            _label.characterSize = 0.1f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;

            _label.text = "";

            labelObj.SetActive(false);
        }

        private void BillboardLabel()
        {
            if (_label == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            _label.transform.rotation = cam.transform.rotation;
        }

        public void Destroy()
        {
            DVRT_WorldShiftManager.OnWorldShift -= HandleWorldShift;

            if (Root != null)
                GameObject.Destroy(Root);
        }

        private void HandleWorldShift(Vector3 delta)
        {
            ApplyTransform();
        }

        public void Move(Vector3 worldDelta)
        {
            _canonicalPosition += worldDelta;
            ApplyTransform();
        }

        public void Rotate(float degrees)
        {
            Root.transform.Rotate(Vector3.up, degrees, Space.World);
            _canonicalRotation = Root.transform.rotation;
        }
        public void SetCanonicalRotation(Quaternion rotation)
        {
            _canonicalRotation = rotation;
        }

        private void ApplyTransform()
        {
            if (Root == null)
                return;

            Root.transform.position =
                _canonicalPosition + DVRT_WorldShiftManager.CurrentMove;

            Root.transform.rotation = _canonicalRotation;
        }

        public void SetGroupVisible(bool visible)
        {
            
            var renderers = Root.GetComponentsInChildren<Renderer>(true);

            foreach (var r in renderers)
                r.enabled = visible;

            if (_label != null)
                _label.gameObject.SetActive(false);

            DisableGlow();
        }

        public void SetActiveEditing(bool active)
        {
            var renderer = Root.GetComponent<Renderer>();
            renderer.enabled = true;

            _material.color = _groupColor;

            if (active)
            {
                EnableGlow();

                if (_label != null)
                {
                    _label.gameObject.SetActive(true);
                    UpdateLabel();
                }
            }
            else
            {
                DisableGlow();

                if (_label != null)
                    _label.gameObject.SetActive(false);
            }
        }

        public void AdjustTurnDegrees(float delta)
        {
            _turnDegrees += delta;
            UpdateLabel();
        }

        public void UpdateLabel(int randomTurnEditIndex = -1)
        {
            if (_label == null)
                return;

            if (Type == MarkerType.RandomlyTurnTo && randomTurnEditIndex < 0)
            {
                randomTurnEditIndex = 0;
            }

            if (Type == MarkerType.TurnTo)
            {
                string targetText = "<undefined>";

                if (!string.IsNullOrEmpty(TargetMarkerID))
                {
                    TrafficMarker target = DVRT_Manager.FindMarkerByID(TargetMarkerID);
                    if (target != null)
                        targetText = target.GetFriendlyLabel();
                }

                _label.text = $"TURN TO\n{targetText}";
            }
            else if (Type == MarkerType.TurnTarget)
            {
                _label.text = $"TURN TARGET\n{GetFriendlyLabel()}";
            }
            else if (Type == MarkerType.SpeedUp)
            {
                _label.text = $"SPEED UP\n+{_speedLevel}";
            }
            else if (Type == MarkerType.SlowDown)
            {
                _label.text = $"SLOW DOWN\n-{_speedLevel}";
            }
            else if (Type == MarkerType.Stop)
            {
                _label.text = $"STOP\n{_stopSeconds:0}s";
            }
            else if (Type == MarkerType.Despawn)
            {
                _label.text = $"DESPAWN";
            }
            else if (Type == MarkerType.StopAndDespawn)
            {
                _label.text = $"STOP then DESPAWN\n{_stopSeconds:0}s";
            }
            else if (Type == MarkerType.RandomlyTurnTo)
            {
                string targetText1 = "<undefined>";
                string targetText2 = "<undefined>";

                if (!string.IsNullOrEmpty(TargetMarkerID1))
                {
                    TrafficMarker target1 = DVRT_Manager.FindMarkerByID(TargetMarkerID1);
                    if (target1 != null)
                        targetText1 = target1.GetFriendlyLabel();
                }

                if (!string.IsNullOrEmpty(TargetMarkerID2))
                {
                    TrafficMarker target2 = DVRT_Manager.FindMarkerByID(TargetMarkerID2);
                    if (target2 != null)
                        targetText2 = target2.GetFriendlyLabel();
                }

                string prefix1 = randomTurnEditIndex == 0 ? "> " : "  ";
                string prefix2 = randomTurnEditIndex == 1 ? "> " : "  ";

                _label.text =
                    $"RANDOM TURN\n" +
                    $"{prefix1}Target 1: {targetText1}\n" +
                    $"{prefix2}Target 2: {targetText2}";
            }
        }

        private void CreateForwardIndicator()
        {
            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);

            arrow.name = "ForwardIndicator";
            arrow.transform.SetParent(Root.transform, false);

            UnityEngine.Object.Destroy(arrow.GetComponent<Collider>());

            arrow.GetComponent<Renderer>().material.color = Color.green;

            arrow.transform.localScale = new Vector3(0.2f, 0.2f, 1.2f);
            arrow.transform.localPosition = new Vector3(0f, 0f, 1.1f);

            // 🔥 STORE REFERENCE
            _forwardIndicator = arrow;
        }

        public Vector3 GetTargetDirection()
        {
            float yaw = Root.transform.eulerAngles.y + _turnDegrees;
            float rad = yaw * Mathf.Deg2Rad;

            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        public Quaternion GetTargetRotation()
        {
            return Root.transform.rotation * Quaternion.Euler(0f, _turnDegrees, 0f);
        }

        public void SetTargetMarker(string targetMarkerID)
        {
            TargetMarkerID = targetMarkerID;
            UpdateLabel();
        }

        public void ClearTargetMarker()
        {
            TargetMarkerID = null;
            UpdateLabel();
        }

        public void Update()
        {
            if (_label == null || !_label.gameObject.activeSelf)
                return;

            BillboardLabel();

            if (Camera.main != null)
            {
                _label.transform.rotation =
                    Quaternion.LookRotation(
                        _label.transform.position - Camera.main.transform.position);
            }
        }

        public void NextType()
        {
            if (Type == MarkerType.TurnTo)
                Type = MarkerType.RandomlyTurnTo;
            else if (Type == MarkerType.RandomlyTurnTo)
                Type = MarkerType.TurnTarget;
            else if (Type == MarkerType.TurnTarget)
                Type = MarkerType.SpeedUp;
            else if (Type == MarkerType.SpeedUp)
                Type = MarkerType.SlowDown;
            else if (Type == MarkerType.SlowDown)
                Type = MarkerType.Stop;
            else if (Type == MarkerType.Stop)
                Type = MarkerType.Despawn;
            else if (Type == MarkerType.Despawn)
                Type = MarkerType.StopAndDespawn;
            else
                Type = MarkerType.TurnTo;
            DVRT_Manager.MarkUnsavedChanges();
            UpdateLabel();

            UpdateArrowVisibility();

            DVRT_Manager.ValidateAllMarkerLinks();
        }

        public void PrevType()
        {
            if (Type == MarkerType.TurnTo)
                Type = MarkerType.StopAndDespawn;
            else if (Type == MarkerType.RandomlyTurnTo)
                Type = MarkerType.TurnTo;
            else if (Type == MarkerType.TurnTarget)
                Type = MarkerType.RandomlyTurnTo;
            else if (Type == MarkerType.SpeedUp)
                Type = MarkerType.TurnTarget;
            else if (Type == MarkerType.SlowDown)
                Type = MarkerType.SpeedUp;
            else if (Type == MarkerType.Stop)
                Type = MarkerType.SlowDown;
            else if (Type == MarkerType.Despawn)
                Type = MarkerType.Stop;
            else if (Type == MarkerType.StopAndDespawn)
                Type = MarkerType.Despawn;
            DVRT_Manager.MarkUnsavedChanges();
            UpdateLabel();
            UpdateArrowVisibility(); // 🔥 important
            DVRT_Manager.ValidateAllMarkerLinks();
        }

        public void UpdateArrowVisibility()
        {
            if (_forwardIndicator == null)
                return;

            var renderer = _forwardIndicator.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.enabled = (Type == MarkerType.TurnTarget);
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

        public int GetFriendlyIndex()
        {
            return DVRT_Manager.GetFriendlyMarkerIndex(this);
        }

        public string GetFriendlyLabel()
        {
            return "#" + GetFriendlyIndex();
        }

        public void ForceSetMarkerID(string markerID)
        {
            if (string.IsNullOrEmpty(markerID))
                return;

            MarkerID = markerID;
        }

        public void ForceSetTargetMarkerID(string targetID)
        {
            if (string.IsNullOrEmpty(targetID))
                return;

            TargetMarkerID = targetID;
        }

        public void ForceSetType(MarkerType targetType)
        {
            Type = targetType;

            if (Type != MarkerType.TurnTo)
                TargetMarkerID = null;

            UpdateLabel();
            UpdateArrowVisibility();
        }        
    }
}


namespace DV_RoadTraffic
{
    public class TrafficMarkerComponent : MonoBehaviour
    {
        public TrafficMarker Marker;
    }
}

