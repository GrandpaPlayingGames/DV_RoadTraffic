using DV;
using DV.CabControls;
using DV.Interaction.Inputs;
using DV.InventorySystem;
using DV.UI.Inventory;
using DV.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DV_RoadTraffic
{
    public static class DVRT_Manager
    {
        // ==============================
        // FACTORIES & REGISTRIES
        // ==============================

        public static readonly List<VehicleFactory> _factories =
            new List<VehicleFactory>();

        private static readonly Dictionary<string, TrafficMarker> _markerRegistry =
            new Dictionary<string, TrafficMarker>();

        public static List<TrafficVehicleController> ActiveVehicles =
            new List<TrafficVehicleController>();


        // ==============================
        // SELECTION STATE
        // ==============================

        private static VehicleFactory _selectedGroup = null;
        private static TrafficMarker _selectedMarker = null;


        // ==============================
        // EDITING STATE
        // ==============================

        private static bool _globalEditingMode = false;
        private static bool _editingMode = false;

        private static bool _editingRouteName = false;
        private static string _routeNameBuffer = "";

        public static bool IsTypingRouteName => _editingRouteName;
        private static bool _skipTypingFrame;


        // ==============================
        // PREVIEW / RENDERING
        // ==============================

        private static List<LineRenderer> _routePreviewSegments =
            new List<LineRenderer>();


        // ==============================
        // PLAYER / CONTEXT
        // ==============================

        private static CharacterControllerProvider _playerController;
        private static bool _layerCheckDone = false;


        // ==============================
        // MARKER TARGETING
        // ==============================

        public static string TargetMarkerID { get; private set; } = null;

        public static string TargetMarkerID1;
        public static string TargetMarkerID2;

        private static int _randomTurnEditIndex = 0; // 0 = Target 1, 1 = Target 2


        // ==============================
        // STATE FLAGS
        // ==============================

        private static bool _unsavedChanges = false;


        // ==============================
        // AUTO SPAWN
        // ==============================

        private static bool autoSpawnEnabled = false;
        private static float autoSpawnTimer = 0f;
        private const float AUTO_SPAWN_INTERVAL = 6f;


        // ==============================
        // REGISTRATION
        // ==============================

        public static void RegisterFactory(VehicleFactory vf)
        {
            _factories.Add(vf);
        }

        public static void _______________SYSTEM_________________()
        {
        }


        public static void Update(float dt)
        {
             if (!_layerCheckDone)
            {
                bool ignored = Physics.GetIgnoreLayerCollision(0, 0);
                Main.Log($"[DVRT] IgnoreLayerCollision(0,0) = {ignored}");
                _layerCheckDone = true;
            }

            if (!_globalEditingMode)
            {                
                foreach (var vf in _factories)
                {
                    vf.UpdateActivation();

                    //if (vf.TrafficRate > 0)
                    if (vf.TrafficRate > 0 && vf.IsActivated)
                        vf.TryAutoSpawn();
                }              
            }

            DVRT_WorldShiftManager.Update();
            if (_editingRouteName && _selectedGroup != null)
            {
                HandleRouteNameTyping();
                return;
            }

            HandleGlobalEditToggle();
            HandleF9Action();
            HandleMarkerSpawn();
            HandleMarkerType();
            HandleMovementInput(dt);

            if (Main.Settings.AutoSpawnTestVehicle.IsPressed())
            {
                if (!_globalEditingMode)
                {
                    Main.Log("[DVRT] AutoSpawn blocked (not in edit mode).");
                    return;
                }

                autoSpawnEnabled = !autoSpawnEnabled;
                autoSpawnTimer = 0f;

                Main.Log($"[DVRT] AutoSpawn {(autoSpawnEnabled ? "ENABLED" : "DISABLED")}");
            }

            // --- Manual spawn (F8) ---
            if (Main.Settings.SpawnTestVehicle.IsPressed())
            {
                if (_selectedGroup != null && _globalEditingMode)
                    SpawnFromFactory(_selectedGroup);
            }

            // --- Auto spawn loop ---
            if (autoSpawnEnabled)
            {
                // stop if we leave edit mode
                if (!_globalEditingMode)
                {
                    autoSpawnEnabled = false;
                    Main.Log("[DVRT] AutoSpawn stopped (edit mode exited).");
                    return;
                }

                // 🔥 NEW: stop if route deselected
                if (_selectedGroup == null)
                {
                    autoSpawnEnabled = false;
                    Main.Log("[DVRT] AutoSpawn stopped (no route selected).");
                    return;
                }

                autoSpawnTimer += Time.deltaTime;

                if (autoSpawnTimer >= AUTO_SPAWN_INTERVAL)
                {
                    autoSpawnTimer = 0f;
                    SpawnFromFactory(_selectedGroup);
                }
            }

            if (Main.Settings.SaveRoutes.IsPressed())
            {
                if (_editingMode)
                {
                    DVRT_RoutePersistence.SaveRoutes(_factories);
                    _unsavedChanges = false;
                }
            }


            if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlockGameplayInput(false);
                _editingRouteName = false;

                Main.Log("[DVRT] Route naming cancelled.");
                return;
            }

            foreach (var vf in _factories)
            {
                vf.Update();

                foreach (var marker in vf.Markers)
                    marker.Update();
            }

            DrawRoutePreview();
        }

        public static void _______________INPUTS_________________()
        {
        }

        // ========================================================
        // F9 ACTION
        // ========================================================
  
        private static void HandleF9Action()
        {
            if (!_globalEditingMode)
                return;

            if (_editingRouteName)
                return;

            if (!Main.Settings.RouteInteract.IsPressed())
                return;

            RaycastHit hit;
            bool hitSomething = RaycastFromCamera(out hit);

            // ----------------------------------------------------
            // NOT EDITING
            // ----------------------------------------------------

            if (!_editingMode)
            {
                if (hitSomething)
                {
                    VehicleFactory vf = GetFactoryFromHit(hit);

                    if (vf != null)
                    {
                        EnterEditMode(vf);
                        return;
                    }
                }

                SpawnVehicleFactory(hitSomething ? hit.point : GetFallbackSpawn());
                DVRT_Manager.MarkUnsavedChanges();
                return;
            }

            // ----------------------------------------------------
            // EDITING MODE
            // ----------------------------------------------------

            if (hitSomething)
            {
                // first check markers
                TrafficMarker marker = GetMarkerFromHit(hit);

                if (marker != null)
                {
                    if (_selectedMarker == marker)
                    {
                        ExitEditMode();
                        return;
                    }

                    SelectMarker(marker);
                    return;
                }

                VehicleFactory vf = GetFactoryFromHit(hit);

                if (vf == _selectedGroup)
                {
                    if (_selectedMarker == null)
                    {
                        ExitEditMode();
                        return;
                    }

                    SelectFactory();
                    return;
                }
            }
        }

        private static void HandleMarkerType()
        {
            if (!_globalEditingMode)
                return;

            if (_editingRouteName)
                return;

            if (!_editingMode)
                return;

            if (_selectedMarker == null)
                return;      

            if (_selectedMarker != null)
            {
                if (Main.Settings.ChangeMarkerType.IsPressed())
                {
                    _selectedMarker.NextType();
                    DVRT_Manager.MarkUnsavedChanges();
                    ValidateAllMarkerLinks();
                    return;
                }

                if (Main.Settings.PrevMarkerType.IsPressed())
                {
                    _selectedMarker.PrevType();
                    DVRT_Manager.MarkUnsavedChanges();
                    ValidateAllMarkerLinks();
                    return;
                }
            }
        }

        // ========================================================
        // MOVEMENT / ROTATION
        // ========================================================
        
        private static void HandleMovementInput(float dt)
        {
            if (!_globalEditingMode)
                return;

            if (!_editingMode)
                return;

            if (IsInFreeCam())
                return;

            Transform cam = Camera.main?.transform;

            if (cam == null)
                return;

            Vector3 camForward = cam.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = cam.right;
            camRight.y = 0;
            camRight.Normalize();

            float moveSpeed = Input.GetKey(KeyCode.LeftShift) ? 0.5f : 2f;

            Vector3 move = Vector3.zero;

            if (Main.Settings.MoveForward.IsHeld())
                move += camForward * moveSpeed * dt;

            if (Main.Settings.MoveBackward.IsHeld())
                move -= camForward * moveSpeed * dt;

            if (Main.Settings.MoveRight.IsHeld())
                move += camRight * moveSpeed * dt;

            if (Main.Settings.MoveLeft.IsHeld())
                move -= camRight * moveSpeed * dt;

            if (Main.Settings.RaiseObject.IsHeld())
                move += Vector3.up * moveSpeed * dt;

            if (Main.Settings.LowerObject.IsHeld())
                move -= Vector3.up * moveSpeed * dt;

            if (move != Vector3.zero)
            {
                DVRT_Manager.MarkUnsavedChanges();
                MoveSelected(move);
            }

            if (Main.Settings.DeleteObject.IsPressed())
            {
                DVRT_Manager.MarkUnsavedChanges();
                HandleDelete();
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scroll) > 0.001f)
            {
                if (DVRT_Manager.IsScrollBlockedByDV())
                    return;

                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                // ------------------------------------
                // ROTATION (NO ALT)
                // ------------------------------------
                if (!alt)
                {
                    float rotateStep = 20f;

                    if (shift && ctrl)
                        rotateStep = 0.5f;
                    else if (shift)
                        rotateStep = 5f;

                    if (_selectedMarker != null)
                    {
                        DVRT_Manager.MarkUnsavedChanges();
                        _selectedMarker.Rotate(scroll * rotateStep);
                    }
                    else if (_selectedGroup != null)
                    {
                        DVRT_Manager.MarkUnsavedChanges();
                        _selectedGroup.Rotate(scroll * rotateStep);
                    }

                    return;
                }

                // ------------------------------------
                // ALT + SCROLL : VEHICLE FACTORY PARAMETERS
                // ------------------------------------
                if (_selectedGroup != null && _selectedMarker == null)
                {
                    if (shift)
                    {
                        int dir = scroll > 0 ? -1 : 1;
                        _selectedGroup.CycleParameter(dir);
                        return;
                    }

                    DVRT_Manager.MarkUnsavedChanges();
                    _selectedGroup.AdjustSelectedParameter(scroll);
                    return;
                }

                // ------------------------------------
                // ALT + SCROLL : PARAMETER EDITING
                // ------------------------------------
                if (_selectedMarker != null)
                {
                    if (_selectedMarker.Type == TrafficMarker.MarkerType.TurnTo)
                    {
                        DVRT_Manager.MarkUnsavedChanges();
                        AdjustTurnTargetSelection(scroll);
                        return;
                    }

                    if (_selectedMarker.Type == TrafficMarker.MarkerType.RandomlyTurnTo)
                    {
                        if (shift)
                        {
                            int dir = scroll > 0 ? 1 : -1;
                            _randomTurnEditIndex += dir;

                            if (_randomTurnEditIndex < 0)
                                _randomTurnEditIndex = 1;
                            else if (_randomTurnEditIndex > 1)
                                _randomTurnEditIndex = 0;

                            _selectedMarker.UpdateLabel(_randomTurnEditIndex);
                            return;
                        }

                        DVRT_Manager.MarkUnsavedChanges();
                        AdjustRandomTurnTargetSelection(scroll, _randomTurnEditIndex);
                        return;
                    }

                    if (_selectedMarker.Type == TrafficMarker.MarkerType.SpeedUp ||
                        _selectedMarker.Type == TrafficMarker.MarkerType.SlowDown)
                    {
                        DVRT_Manager.MarkUnsavedChanges();
                        _selectedMarker.AdjustSpeedLevel(scroll);
                        return;
                    }

                    if (_selectedMarker.Type == TrafficMarker.MarkerType.Stop ||
                        _selectedMarker.Type == TrafficMarker.MarkerType.StopAndDespawn)
                    {
                        DVRT_Manager.MarkUnsavedChanges();
                        _selectedMarker.AdjustStopSeconds(scroll);
                        return;
                    }

                    float step = 5f;

                    if (shift)
                        step = 1f;

                    DVRT_Manager.MarkUnsavedChanges();
                    _selectedMarker.AdjustTurnDegrees(scroll * step);
                    return;
                }
            }
        }

        private static void HandleMarkerSpawn()
        {
            if (!_globalEditingMode)
                return;

            if (_editingRouteName) return;

            if (!_editingMode)
                return;

            if (!Main.Settings.SpawnMarker.IsPressed())
                return;

            if (_selectedGroup == null)
                return;

            RaycastHit hit;

            if (!RaycastFromCamera(out hit))
                return;

            Vector3 spawnPos = hit.point + Vector3.up * 0.5f;

            TrafficMarker marker = new TrafficMarker(spawnPos);

            marker.RouteName = _selectedGroup.RouteName;
            Main.Log($"[DVRT] Marker {marker.MarkerID} assigned to route '{marker.RouteName}'");
            DVRT_Manager.MarkUnsavedChanges();

            _selectedGroup.Markers.Add(marker);

            RegisterMarker(marker);

            SelectMarker(marker);

            Main.Log($"[DVRT] Marker spawned {marker.MarkerID}");
        }


        private static void HandleDelete()
        {
            if (!_globalEditingMode)
                return;

            if (_editingRouteName) return;

            if (!_editingMode || _selectedGroup == null)
                return;

            // delete marker
            if (_selectedMarker != null)
            {
                UnregisterMarker(_selectedMarker);
                _selectedMarker.Destroy();
                _selectedGroup.Markers.Remove(_selectedMarker);

                _selectedMarker = null;
                ValidateAllMarkerLinks();

                SelectFactory();

                Main.Log("[DVRT] Marker deleted.");
                return;
            }

            // delete VF only if empty
            if (_selectedGroup.Markers.Count == 0)
            {
                _selectedGroup.Destroy();
                _factories.Remove(_selectedGroup);

                _selectedGroup = null;
                _editingMode = false;

                Main.Log("[DVRT] VehicleFactory deleted.");
            }
        }

       
        private static void HandleGlobalEditToggle()
        {
            if (!Main.Settings.ToggleEditMode.IsPressed())
                return;

            if (_globalEditingMode && _unsavedChanges)
            {
                Main.Log("[DVRT] Unsaved changes detected, showing dialog.");
                UnsavedChangesDialog.Show(
                    () =>
                    {
                        DVRT_RoutePersistence.SaveRoutes(_factories);
                        _unsavedChanges = false;
                        PerformToggle();
                    },
                    () =>
                    {
                        PerformToggle();
                    });
                BlockGameplayInput(false);
                return;
            }

            PerformToggle();

            void PerformToggle()
            {
                BlockGameplayInput(false);
                _globalEditingMode = !_globalEditingMode;

                if (_globalEditingMode)
                {
                    Main.Log("[DVRT] Global editing mode ON.");
                    Main.Log($"[DVRT] Factory count = {_factories.Count}");

                    foreach (var vf in _factories)
                    {
                        vf.SetGroupSelected(false);
                        vf.SetActiveEditing(false);

                        if (vf.Root != null)
                        {
                            vf.SetVisible(true);
                        }

                        SetMarkerVisibility(vf, false);
                    }

                    _selectedGroup = null;
                    _selectedMarker = null;
                    _editingMode = false;
                }
                else
                {
                    Main.Log("[DVRT] Runtime mode ON.");

                    ExitEditMode();

                    foreach (var vf in _factories)
                    {
                        vf.SetVisible(false);
                        SetMarkerVisibility(vf, false);
                    }
                }
            }
        }

        private static void StartRouteRename()
        {
            if (_editingRouteName)
                return;

            if (!_globalEditingMode || !_editingMode)
                return;

            if (_selectedGroup == null)
                return;

            _editingRouteName = true;

            _routeNameBuffer = _selectedGroup.RouteName;

            _skipTypingFrame = true;

            BlockGameplayInput(true);

            Main.Log("[DVRT] Enter route name. Press Enter to confirm.");
        }

        private static void HandleRouteNameTyping()
        {
            if (!_editingRouteName)
                return;

            // allow ESC cancel even though DV input is blocked
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Main.Log("[DVRT] Route rename cancelled.");

                BlockGameplayInput(false);
                _editingRouteName = false;

                var controller = GetController();
                if (controller != null)
                    controller.enabled = true;

                return;
            }

            // Ignore the first frame (contains the Alt+N keypress)
            if (_skipTypingFrame)
            {
                _skipTypingFrame = false;
                return;
            }

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (_routeNameBuffer.Length > 0)
                        _routeNameBuffer =
                            _routeNameBuffer.Substring(0, _routeNameBuffer.Length - 1);
                }
                else if (c == '\n' || c == '\r')
                {
                    DVRT_RoutePersistence.ApplyRouteRename(_selectedGroup, _routeNameBuffer);

                    _routeNameBuffer = _selectedGroup.RouteName;

                    DVRT_Manager.MarkUnsavedChanges();

                    foreach (var marker in _selectedGroup.Markers)
                    {
                        marker.RouteName = _selectedGroup.RouteName;
                    }

                    _selectedGroup.UpdateLabel();

                    Main.Log($"[DVRT] Route name set to '{_selectedGroup.RouteName}' and {_selectedGroup.Markers.Count} markers updated.");

                    BlockGameplayInput(false);
                    _editingRouteName = false;

                    var controller = GetController();
                    if (controller != null)
                        controller.enabled = true;
                }
                else if (!char.IsControl(c))
                {
                    _routeNameBuffer += c;
                }
            }
        }

        public static void _______________SPAWN_________________()
        {
        }
    
        private static void SpawnVehicleFactory(Vector3 pos)
        {
            DVRT_Manager.MarkUnsavedChanges();
            Vector3 spawnPos = pos + Vector3.up * 2f;

            Main.Log($"[DVRT] F9 spawn requested.");
            Main.Log($"[DVRT] Raycast pos = {pos}");
            Main.Log($"[DVRT] Spawn pos (after +2y) = {spawnPos}");
            Main.Log($"[DVRT] WorldMover.currentMove = {WorldMover.currentMove}");

            var vf = new VehicleFactory(spawnPos);

            vf.RouteName = $"Route {_factories.Count + 1}";
            vf.TrafficRate = 5;

            vf.CacheNearbyBarriers();

            if (vf == null)
            {
                Main.Log("[DVRT] ERROR: VehicleFactory constructor returned null.");
                return;
            }

            _factories.Add(vf);

            Main.Log($"[DVRT] Factory count now = {_factories.Count}");

            if (vf.Root != null)
            {
                Main.Log($"[DVRT] VF Root position after constructor = {vf.Root.transform.position}");
            }
            else
            {
                Main.Log("[DVRT] WARNING: VF.Root is NULL");
            }

            vf.InitializeLabel();

            _editingMode = true;
            _selectedGroup = vf;
            _selectedGroup.ResetParameterEditing();
            _selectedMarker = null;

            vf.SetGroupSelected(true);
            vf.SetActiveEditing(true);
            vf.SetLabelVisible(true);
            vf.UpdateLabel();

            Main.Log("[DVRT] Vehicle Factory spawned and selected.");
        }

        public static bool SpawnFromFactory(VehicleFactory vf)
        {
            if (vf == null || vf.Root == null)
                return false;

            if (!vf.IsPlayerWithinActivationRange())
                return false;

            Transform player = Camera.main?.transform;

            if (player == null)
                return false;

            GameObject archetype = vf.GetRandomArchetype();

            if (archetype == null)
            {
                Main.Log("[DVRT] No archetype available. Rebuilding local archetype cache.");

                vf.CacheLocalTrafficArchetypes();

                archetype = vf.GetRandomArchetype();
                if (archetype == null)
                {
                    Main.Log("[DVRT] Still no archetypes after rescan.");
                    return false;
                }
            }

            if (archetype.name.Contains("TrafficClone"))
            {
                Main.Log("[DVRT] Ignoring cloned archetype.");
                return false;
            }

            Quaternion spawnRot = vf.GetSpawnRotation();
            Vector3 vfBasePos = vf.GetSpawnPosition();
            Vector3 targetPos = vfBasePos + (spawnRot * Vector3.forward * 6f);

            Vector3 rayStart = targetPos + Vector3.up * 50f;

            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            targetPos = hit.point + Vector3.up * 0.5f;

            if (!SpawnAreaClear(targetPos))
            {
                return false;
            }

            GameObject clone = UnityEngine.Object.Instantiate(archetype, targetPos, spawnRot);
            clone.name = archetype.name + "_TrafficClone";
            clone.transform.SetParent(null, true);
            clone.transform.localScale = Vector3.one;

            SetLayerRecursively(clone, 27);
            Physics.IgnoreLayerCollision(27, 27, true);

            var controller = clone.AddComponent<TrafficVehicleController>();
            controller.Initialize(4f, vf.TTL, vf.RouteName, archetype.name, vf);

            DVRT_Manager.ActiveVehicles.Add(controller);
            return true;
        }

        public static bool SpawnAreaClear(Vector3 position)
        {
            const float radius = 3.5f;

            Collider[] hits = Physics.OverlapSphere(position, radius);

            foreach (var col in hits)
            {
                if (col.isTrigger)
                    continue;

                if (col.GetComponentInParent<TrafficVehicleController>() != null)
                    return false;
            }

            return true;
        }

        public static void _______________EDITING_________________()
        {
        }


        private static void MoveSelected(Vector3 delta)
        {
            if (_selectedMarker != null)
            {
                DVRT_Manager.MarkUnsavedChanges();
                _selectedMarker.Move(delta);
                return;
            }

            if (_selectedGroup != null)
            {
                DVRT_Manager.MarkUnsavedChanges();
                _selectedGroup.Move(delta);
            }
        }

        private static void EnterEditMode(VehicleFactory vf)
        {
            if (_selectedMarker != null)
            {
                _selectedMarker.SetActiveEditing(false);
                _selectedMarker = null;
            }

            if (_selectedGroup != null && _selectedGroup != vf)
            {
                _selectedGroup.SetGroupSelected(false);

                SetMarkerVisibility(_selectedGroup, false);
                UpdateAllMarkerArrows(vf);

                _selectedGroup.SetLabelVisible(false);
            }

            _editingMode = true;
            _selectedGroup = vf;
            _selectedGroup.ResetParameterEditing();

            SetMarkerVisibility(vf, true);

            foreach (var marker in vf.Markers)
            {
                if (marker != null)
                    marker.UpdateArrowVisibility();
            }

            vf.SetLabelVisible(true);
            vf.UpdateLabel();

            SelectFactory();

            Main.Log("[DVRT] Editing VF group.");
        }

       

        private static void ExitEditMode()
        {
            if (_selectedMarker != null)
            {
                _selectedMarker.SetActiveEditing(false);
                _selectedMarker = null;
            }

            if (_selectedGroup != null)
            {
                _selectedGroup.SetGroupSelected(false);

                SetMarkerVisibility(_selectedGroup, false);

                _selectedGroup.SetLabelVisible(false);
            }

            HideRoutePreview();

            _selectedGroup = null;
            _editingMode = false;

            Main.Log("[DVRT] Exit edit mode.");
        }

        private static void SelectMarker(TrafficMarker marker)
        {
            if (_selectedGroup == null)
                return;

            if (_selectedMarker != null)
                _selectedMarker.SetActiveEditing(false);

            _selectedGroup.SetActiveEditing(false);

            _selectedMarker = marker;

            _randomTurnEditIndex = 0;
            _selectedMarker.UpdateLabel(_randomTurnEditIndex);

            marker.SetActiveEditing(true);

            Main.Log("[DVRT] Marker selected.");
        }

        private static void SelectFactory()
        {
            if (_selectedGroup == null)
                return;

            if (_selectedMarker != null)
            {
                _selectedMarker.SetActiveEditing(false);
                _selectedMarker = null;
            }

            _selectedGroup.SetActiveEditing(true);

            Main.Log("[DVRT] Factory selected.");
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        public static void MarkUnsavedChanges()
        {
            _unsavedChanges = true;
        }

        public static void _______________VF_HELPERS_________________()
        {
        }

        private static VehicleFactory GetFactoryFromHit(RaycastHit hit)
        {
            foreach (var vf in _factories)
            {
                if (vf?.Root == null)
                    continue;

                if (hit.collider.gameObject == vf.Root)
                    return vf;
            }

            return null;
        }

        public static void _______________MARKER_HELPERS_________________()
        {
        }

        private static void UpdateAllMarkerArrows(VehicleFactory vf)
        {
            if (vf == null)
                return;

            foreach (var marker in vf.Markers)
            {
                if (marker == null)
                    continue;

                marker.UpdateArrowVisibility();
            }
        }

        private static string GetNextTurnTargetIDOnRoute(string routeName, string currentID, float scroll)
        {
            int direction = scroll > 0 ? 1 : -1;

            var targets = _markerRegistry.Values
    .Where(m => m.RouteName == routeName &&
                m.Type == TrafficMarker.MarkerType.TurnTarget)
    .ToList();

            if (targets.Count == 0)
                return null;

            int index = -1;

            if (!string.IsNullOrEmpty(currentID))
                index = targets.FindIndex(m => m.MarkerID == currentID);

            index += direction;

            if (index < 0) index = targets.Count - 1;
            if (index >= targets.Count) index = 0;

            return targets[index].MarkerID;
        }

        private static void AdjustRandomTurnTargetSelection(float scroll, int editIndex)
        {
            if (_selectedMarker == null)
                return;

            if (editIndex == 0)
            {
                _selectedMarker.TargetMarkerID1 =
                    DVRT_Manager.GetNextTurnTargetIDOnRoute(
                        _selectedMarker.RouteName,
                        _selectedMarker.TargetMarkerID1,
                        scroll);
            }
            else
            {
                _selectedMarker.TargetMarkerID2 =
                    DVRT_Manager.GetNextTurnTargetIDOnRoute(
                        _selectedMarker.RouteName,
                        _selectedMarker.TargetMarkerID2,
                        scroll);
            }

            _selectedMarker.UpdateLabel(editIndex);
        }


        public static void SetMarkerVisibility(VehicleFactory vf, bool visible)
        {
            if (vf == null)
                return;

            foreach (var m in vf.Markers)
                m.SetGroupVisible(visible);

            SetRoutePreviewVisible(visible);
        }

        private static TrafficMarker GetMarkerFromHit(RaycastHit hit)
        {
            if (_selectedGroup == null)
                return null;

            foreach (var marker in _selectedGroup.Markers)
            {
                if (marker?.Root == null)
                    continue;

                if (hit.collider.gameObject == marker.Root)
                    return marker;
            }

            return null;
        }

        public static void RegisterMarker(TrafficMarker marker)
        {
            if (marker == null || string.IsNullOrEmpty(marker.MarkerID))
                return;

            _markerRegistry[marker.MarkerID] = marker;
        }

        public static void UnregisterMarker(TrafficMarker marker)
        {
            if (marker == null || string.IsNullOrEmpty(marker.MarkerID))
                return;

            _markerRegistry.Remove(marker.MarkerID);
        }

        public static void ClearMarkers()
        {
            _markerRegistry.Clear();
        }
        public static TrafficMarker FindMarkerByID(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            TrafficMarker marker;
            if (_markerRegistry.TryGetValue(id, out marker))
                return marker;

            return null;
        }

        public static int GetFriendlyMarkerIndex(TrafficMarker marker)
        {
            if (marker == null)
                return -1;

            foreach (var vf in _factories)
            {
                for (int i = 0; i < vf.Markers.Count; i++)
                {
                    if (vf.Markers[i] == marker)
                        return i + 1;
                }
            }

            return -1;
        }

        private static void AdjustTurnTargetSelection(float scroll)
        {
            if (_selectedMarker == null)
                return;

            if (_selectedMarker.Type != TrafficMarker.MarkerType.TurnTo)
                return;

            List<TrafficMarker> targets = new List<TrafficMarker>();

            foreach (var vf in _factories)
            {
                if (vf.RouteName != _selectedMarker.RouteName)
                    continue;

                foreach (var m in vf.Markers)
                {
                    if (m.Type == TrafficMarker.MarkerType.TurnTarget)
                        targets.Add(m);
                }
            }

            if (targets.Count == 0)
                return;

            int currentIndex = -1;

            if (!string.IsNullOrEmpty(_selectedMarker.TargetMarkerID))
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i].MarkerID == _selectedMarker.TargetMarkerID)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            if (scroll > 0f)
                currentIndex++;
            else
                currentIndex--;

            if (currentIndex < 0)
                currentIndex = targets.Count - 1;

            if (currentIndex >= targets.Count)
                currentIndex = 0;

            _selectedMarker.SetTargetMarker(targets[currentIndex].MarkerID);

            Main.Log($"[DVRT] Turn target set to {targets[currentIndex].GetFriendlyLabel()}");
        }

        public static void ValidateAllMarkerLinks()
        {
            foreach (var vf in _factories)
            {
                foreach (var marker in vf.Markers)
                {
                    if (marker == null)
                        continue;

                    if (marker.Type != TrafficMarker.MarkerType.TurnTo)
                        continue;

                    if (string.IsNullOrEmpty(marker.TargetMarkerID))
                    {
                        Main.Log($"[DVRT] Marker {marker.MarkerID} has empty target.");
                        continue;
                    }

                    TrafficMarker target =
                        vf.Markers.Find(m => m.MarkerID == marker.TargetMarkerID);

                    if (target == null)
                    {
                        Main.Log($"[DVRT] Target not found for {marker.MarkerID} → {marker.TargetMarkerID}");
                        marker.ClearTargetMarker();
                        continue;
                    }

                    if (target.Type != TrafficMarker.MarkerType.TurnTarget)
                    {
                        Main.Log($"[DVRT] Target wrong type: {target.MarkerID} type={target.Type}");
                        marker.ClearTargetMarker();
                        continue;
                    }
                }
            }
        }

        private static void DrawRoutePreview()
        {
            if (!_editingMode || _selectedGroup == null)
                return;

            if (_selectedGroup.Markers == null)
                return;

            HideRoutePreview();

            int segmentIndex = 0;

            foreach (var marker in _selectedGroup.Markers)
            {
                if (marker == null)
                    continue;

                if (marker.Root == null)
                    continue;

                if (marker.Type != TrafficMarker.MarkerType.TurnTo &&
                    marker.Type != TrafficMarker.MarkerType.RandomlyTurnTo)
                    continue;                

                Vector3 start = marker.Root.transform.position + Vector3.up * 0.2f;

                if (marker.Type == TrafficMarker.MarkerType.TurnTo)
                {
                    if (string.IsNullOrEmpty(marker.TargetMarkerID))
                        continue;

                    TrafficMarker target = FindMarkerByID(marker.TargetMarkerID);

                    if (target == null || target.Root == null)
                        continue;

                    Vector3 end = target.Root.transform.position + Vector3.up * 0.2f;

                    LineRenderer lr = GetPreviewSegment(segmentIndex);

                    lr.enabled = true;
                    lr.positionCount = 2;
                    lr.SetPosition(0, start);
                    lr.SetPosition(1, end);

                    segmentIndex++;
                }
                else if (marker.Type == TrafficMarker.MarkerType.RandomlyTurnTo)
                {
                    string[] targets = { marker.TargetMarkerID1, marker.TargetMarkerID2 };

                    foreach (var id in targets)
                    {
                        if (string.IsNullOrEmpty(id))
                            continue;

                        TrafficMarker target = FindMarkerByID(id);

                        if (target == null || target.Root == null)
                            continue;

                        Vector3 end = target.Root.transform.position + Vector3.up * 0.2f;

                        LineRenderer lr = GetPreviewSegment(segmentIndex);

                        lr.enabled = true;
                        lr.positionCount = 2;
                        lr.SetPosition(0, start);
                        lr.SetPosition(1, end);

                        segmentIndex++;
                    }
                }
            }
            DisableUnusedSegments(segmentIndex);
        }

        private static void SetRoutePreviewVisible(bool visible)
        {
            if (_routePreviewSegments == null)
                return;

            foreach (var lr in _routePreviewSegments)
            {
                if (lr != null)
                    lr.enabled = visible;
            }
        }
        private static LineRenderer GetPreviewSegment(int index)
        {
            while (_routePreviewSegments.Count <= index)
            {
                GameObject go = new GameObject("DVRT_RoutePreviewSegment");
                go.hideFlags = HideFlags.HideAndDontSave;

                LineRenderer lr = go.AddComponent<LineRenderer>();

                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startWidth = 0.15f;
                lr.endWidth = 0.15f;
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.startColor = Color.green;
                lr.endColor = Color.green;

                _routePreviewSegments.Add(lr);
            }

            return _routePreviewSegments[index];
        }

        private static void DisableUnusedSegments(int usedCount)
        {
            for (int i = usedCount; i < _routePreviewSegments.Count; i++)
            {
                if (_routePreviewSegments[i] != null)
                    _routePreviewSegments[i].enabled = false;
            }
        }

        public static void _______________OTHER_HELPERS_________________()
        {
        }

        private static bool IsInFreeCam()
        {
            var cam = Camera.main;
            if (cam == null)
                return false;

            return cam.transform.parent == null;
        }
        
        public static bool IsScrollBlockedByDV()
        {
            return IsCommsRadioActive() || IsHotbarOpen();
        }

        public static bool IsCommsRadioActive()
        {
            var radios = Resources.FindObjectsOfTypeAll<CommsRadioController>();

            if (radios == null || radios.Length == 0)
                return false;

            foreach (var radio in radios)
            {
                if (radio == null)
                    continue;

                var item = radio.GetComponent<ItemBase>();
                if (item == null)
                    continue;

                if (item.IsGrabbed() &&
                    !SingletonBehaviour<InventoryViewBase>.Instance.BigInventoryOpen)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsHotbarOpen()
        {
            var hotbar = SingletonBehaviour<HotbarController>.Instance;

            if (hotbar == null)
                return false;

            return hotbar.IsOpen;
        }

        public static void DrawEditorOverlay()
        {
            if (!_globalEditingMode)
                return;

            float width = 380f;
            float x = (Screen.width - width) * 0.5f;
            float y = 10f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.UpperCenter;
            boxStyle.fontSize = 14;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.padding = new RectOffset(10, 10, 10, 10);

            float height = _editingRouteName ? 120f : 70f;

            GUI.Box(new Rect(x, y, width, height), "DV_RoadTraffic Editing Mode: ON", boxStyle);

            float labelY = y + 30;

            string routeText = "<none>";

            if (_selectedGroup != null)
                routeText = _selectedGroup.RouteName;

            GUI.Label(
                new Rect(x + 20, labelY, 240, 25),
                $"Route: {routeText}"
            );

            if (_selectedGroup != null && !_editingRouteName)
            {
                if (GUI.Button(
                    new Rect(x + width - 80, labelY, 60, 25),
                    "Edit"))
                {
                    StartRouteRename();
                }
            }

            if (_editingRouteName)
            {
                string cursor = ((int)(Time.realtimeSinceStartup * 2) % 2 == 0) ? "|" : " ";

                GUI.Label(
                    new Rect(x + 20, y + 60, width - 40, 25),
                    "Rename Route:"
                );

                GUI.Label(
                    new Rect(x + 20, y + 80, width - 40, 25),
                    _routeNameBuffer + cursor
                );

                GUI.Label(
                    new Rect(x + 20, y + 100, width - 40, 20),
                    "[Enter = confirm   Esc = cancel]"
                );
            }
        }


        private static bool RaycastFromCamera(out RaycastHit hit)
        {
            hit = default;

            if (Camera.main == null)
                return false;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            return Physics.Raycast(ray, out hit, 500f, ~0, QueryTriggerInteraction.Collide);
        }

        private static readonly int[] blockedActions =
        {
            InputManager.Actions.MoveHorizontal,
            InputManager.Actions.MoveVertical,
            InputManager.Actions.Crouch,
            InputManager.Actions.Run,
            InputManager.Actions.Sit,
            InputManager.Actions.Jump,
            InputManager.Actions.Lean,
            InputManager.Actions.Teleport,
            InputManager.Actions.MouseLookHorizontal,
            InputManager.Actions.MouseLookVertical,
            InputManager.Actions.MouseLook,

            InputManager.Actions.InventorySlot1,
            InputManager.Actions.InventorySlot2,
            InputManager.Actions.InventorySlot3,
            InputManager.Actions.InventorySlot4,
            InputManager.Actions.InventorySlot5,
            InputManager.Actions.InventorySlot6,
            InputManager.Actions.InventorySlot7,
            InputManager.Actions.InventorySlot8,
            InputManager.Actions.InventorySlot9,
            InputManager.Actions.InventorySlot10,
            InputManager.Actions.InventorySlot11,
            InputManager.Actions.InventorySlot12,

            InputManager.Actions.Escape,

            InputManager.Actions.Hotbar,

            InputManager.Actions.InventoryOpen
        };

        private static void BlockGameplayInput(bool state)
        {
            foreach (var action in blockedActions)
                InputManager.Actions.SetActionDisabled(action, state);
        }

        public static void ResetRuntime()
        {

            Main.Log("[DVRT] **************** reset Runtime Called ************");
            DestroyAllVehicles();
            foreach (var vf in _factories)
            {
                if (vf != null)
                    vf.Destroy();   // remove GO + unsubscribe WOS
            }

            _factories.Clear();
            DVRT_Manager.ClearMarkers();
            Main.SetSessionActive(false);
        }

        private static CharacterControllerProvider GetController()
        {
            if (_playerController != null)
                return _playerController;

            _playerController = UnityEngine.Object.FindObjectOfType<CharacterControllerProvider>();
            return _playerController;
        }

        private static Vector3 GetFallbackSpawn()
        {
            Transform cam = Camera.main?.transform;

            if (cam == null)
                return Vector3.zero;

            return cam.position + cam.forward * 10f;
        }

        public static void ResetEditorState()
        {
            _globalEditingMode = false;
            _editingMode = false;
            _editingRouteName = false;
            _routeNameBuffer = "";

            _selectedGroup = null;
            _selectedMarker = null;

            if (_routePreviewSegments != null)
            {
                foreach (var lr in _routePreviewSegments)
                {
                    if (lr == null)
                        continue;

                    lr.enabled = false;
                    lr.positionCount = 0;
                }
            }
        }
        
        private static void HideRoutePreview()
        {
            if (_routePreviewSegments == null)
                return;

            foreach (var lr in _routePreviewSegments)
            {
                if (lr == null)
                    continue;

                lr.positionCount = 0;
                lr.enabled = false;
            }
        }

        public static void DestroyAllVehicles()
        {
            var vehicles = ActiveVehicles.ToList(); 

            foreach (var v in vehicles)
            {
                if (v == null)
                    continue;

                try
                {
                    GameObject.Destroy(v.gameObject);
                }
                catch { }
            }

            ActiveVehicles.Clear();

            Main.Log($"[DVRT] Destroyed {vehicles.Count} active vehicles");
        }
    }
}


namespace DV_RoadTraffic
{
    public class DVRT_RuntimeUI : MonoBehaviour
    {
        public static DVRT_RuntimeUI Instance;
        private Image reticleImage;
        private Text weaponText;
        private Text scoreText;
        private Text headerText;
        private Canvas canvas;
        private Text loadingText;


        void Awake()
        {
            Instance = this;
            CreateReticle();
            CreateLoadingText();

        }
        void OnGUI()
        {
            if (!Main.IsGameLoaded)
                return;

            if (!Main.SessionActive)
                return;

            DVRT_Manager.DrawEditorOverlay();
        }

        void Update()
        {
   
        }

        private void CreateReticle()
        {
            string path = System.IO.Path.Combine(
                Main.Mod.Path,
                "Assets",
                "Images",
                "Reticle",
                "reticle-white.png");

            if (!System.IO.File.Exists(path))
            {
                Main.Log("[DVRT] Reticle image not found");
                return;
            }

            byte[] data = System.IO.File.ReadAllBytes(path);

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));

            Canvas canvas = gameObject.GetComponent<Canvas>();

            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;

                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            GameObject go = new GameObject("DVRT_Reticle");
            go.transform.SetParent(transform, false);

            reticleImage = go.AddComponent<Image>();
            reticleImage.sprite = sprite;
            reticleImage.raycastTarget = false;

            RectTransform rt = go.GetComponent<RectTransform>();

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            rt.sizeDelta = new Vector2(64, 64);
            rt.anchoredPosition = Vector2.zero;

            reticleImage.enabled = false;
            Main.Log("[DVRT] Reticle created");

            CreateReticleText();
        }

        private void CreateReticleText()
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject headerGO = new GameObject("DVRT_HeaderText");
            headerGO.transform.SetParent(transform, false);

            headerText = headerGO.AddComponent<Text>();
            headerText.font = font;
            headerText.fontSize = 20;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color = Color.white;
            headerText.text = "Traffic Warden";
            headerText.enabled = false;

            RectTransform hrt = headerGO.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0.5f);
            hrt.anchorMax = new Vector2(0.5f, 0.5f);
            hrt.pivot = new Vector2(0.5f, 0.5f);

            hrt.anchoredPosition = new Vector2(0, -90);
            hrt.sizeDelta = new Vector2(220, 25);
            
            GameObject weaponGO = new GameObject("DVRT_WeaponText");
            weaponGO.transform.SetParent(reticleImage.transform.parent, false);

            weaponText = weaponGO.AddComponent<Text>();
            weaponText.font = font;
            weaponText.fontSize = 18;
            weaponText.alignment = TextAnchor.MiddleCenter;
            weaponText.color = Color.white;
            weaponText.text = "";
            weaponText.enabled = false;

            RectTransform wrt = weaponGO.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.anchoredPosition = new Vector2(0, -125);
            wrt.sizeDelta = new Vector2(200, 25);
            
            GameObject scoreGO = new GameObject("DVRT_ScoreText");
            scoreGO.transform.SetParent(reticleImage.transform.parent, false);

            scoreText = scoreGO.AddComponent<Text>();
            scoreText.font = font;
            scoreText.fontSize = 18;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = Color.yellow;
            scoreText.text = "";
            scoreText.enabled = false;

            RectTransform srt = scoreGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0, -150);
            srt.sizeDelta = new Vector2(200, 25);
           
        }
  
        public void SetTrafficWardenVisible(bool visible)
        {
            if (reticleImage != null)
                reticleImage.enabled = visible;

            if (headerText != null)
                headerText.enabled = visible;

            if (weaponText != null)
                weaponText.enabled = visible;

            if (scoreText != null)
                scoreText.enabled = visible;
        }

        public void SetReticleVisible(bool visible)
        {
            if (reticleImage != null)
                reticleImage.enabled = visible;
        }

        public void SetWeaponText(string text)
        {
            if (weaponText != null)
                weaponText.text = text;
        }

        public void SetScore(int score)
        {
            if (scoreText != null)
                scoreText.text = $"Score: {score}";
        }
 
        private void CreateLoadingText()
        {
            Main.Log($"[DVRT DEBUG] CreateLoadingText canvas is null? {canvas == null}");


            if (canvas == null)
            {
                canvas = GameObject.FindObjectOfType<Canvas>();

                if (canvas == null)
                {
                    Main.Log("[DVRT] No Canvas found for loading UI");
                    return;
                }

                Main.Log("[DVRT] Found Canvas for loading UI");
            }

            Main.Log($"[DVRT DEBUG] CreateLoadingText canvas is null? {canvas == null}");

            GameObject go = new GameObject("DVRT_LoadingText");
            go.transform.SetParent(canvas.transform, false);

            loadingText = go.AddComponent<Text>();
            loadingText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            loadingText.fontSize = 26;
            loadingText.alignment = TextAnchor.MiddleCenter;
            loadingText.color = Color.white;

            RectTransform rt = loadingText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600, 120);

            loadingText.gameObject.SetActive(false);
        }

        public void ShowLoading(string text)
        {
            if (loadingText == null)
                return;

            loadingText.text = text;
            loadingText.gameObject.SetActive(true);
        }

        public void HideLoading()
        {
            if (loadingText == null)
                return;

            loadingText.gameObject.SetActive(false);
        }
    }
}

namespace DV_RoadTraffic
{
    public class DVRT_GUI : MonoBehaviour
    {
        void OnGUI()
        {
            UnsavedChangesDialog.Draw();
        }
    }
}

namespace DV_RoadTraffic
{
    public static class UnsavedChangesDialog
    {
        private static bool _show;
        private static System.Action _onSave;
        private static System.Action _onContinue;

        public static void Show(System.Action onSave, System.Action onContinue)
        {
            Main.Log("[DVRT] Unsaved dialog activated");
            _show = true;
            _onSave = onSave;
            _onContinue = onContinue;
        }

        public static void Draw()
        {
            if (!_show)
                return;

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float width = 420f;
            float height = 170f;

            Rect rect = new Rect(
                Screen.width / 2f - width / 2f,
                Screen.height / 2f - height / 2f,
                width,
                height);

            GUILayout.BeginArea(rect, GUI.skin.window);

            GUILayout.Space(10);

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            GUILayout.Label("Unsaved Changes Detected", titleStyle);

            GUILayout.Space(10);

            // Message text
            var textStyle = new GUIStyle(GUI.skin.label);
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.wordWrap = true;

            GUILayout.Label(
                "Your changes may be lost if you exit without saving.",
                textStyle);

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Save", GUILayout.Height(32)))
            {
                _show = false;
                _onSave?.Invoke();
            }

            if (GUILayout.Button("Continue", GUILayout.Height(32)))
            {
                _show = false;
                _onContinue?.Invoke();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
    public static class DVRT_LoadingUI
    {
        private static GameObject panel;
        private static Text text;

        public static void Show()
        {
            if (panel != null) return;

            var canvas = GameObject.FindObjectOfType<Canvas>();

            panel = new GameObject("DVRT_LoadingPanel");
            panel.transform.SetParent(canvas.transform, false);

            text = panel.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;

            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400, 100);
            rect.anchoredPosition = Vector2.zero;
        }

        public static void SetText(string t)
        {
            if (text != null)
                text.text = t;
        }

        public static void Hide()
        {
            if (panel != null)
                GameObject.Destroy(panel);
        }
    }
}


namespace DV_RoadTraffic
{
    public class DVRT_FadeUI : MonoBehaviour
    {
        public static DVRT_FadeUI Instance;

        private Canvas _canvas;
        private Image _fadeImage;
        private Text _fadeText;

        private bool _isBusy = false;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            CreateUI();
        }

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("DVRT_FadeCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            canvasGO.AddComponent<GraphicRaycaster>();

            // --- Fullscreen black image ---
            GameObject imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(canvasGO.transform, false);

            _fadeImage = imageGO.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);

            RectTransform rt = imageGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // --- Center text ---
            GameObject textGO = new GameObject("FadeText");
            textGO.transform.SetParent(canvasGO.transform, false);

            _fadeText = textGO.AddComponent<Text>();
            _fadeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _fadeText.fontSize = 32;
            _fadeText.alignment = TextAnchor.MiddleCenter;
            _fadeText.color = Color.white;
            _fadeText.text = "";

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            _canvas.gameObject.SetActive(false);
        }

        public void StartTeleport(Vector3 targetPos, string routeName)
        {
            if (_isBusy)
                return;

            StartCoroutine(TeleportRoutine(targetPos, routeName));
        }

        private IEnumerator TeleportRoutine(Vector3 targetPos, string routeName)
        {
            _canvas.gameObject.SetActive(true);

            _isBusy = true;

            _fadeText.text = "Teleporting to " + routeName;

            yield return Fade(1f, 0.35f);

            yield return new WaitForSeconds(0.1f);

            // --- TELEPORT ---
            PlayerManager.TeleportPlayer(
                targetPos + Vector3.up * 1.6f,
                Quaternion.identity,
                null,
                true,
                false
            );

            // --- WAIT FOR WORLD ---
            yield return WaitForWorldReady();

            yield return Fade(0f, 0.5f);

            _fadeText.text = "";

            _isBusy = false;

            _canvas.gameObject.SetActive(false);
        }

        private IEnumerator Fade(float targetAlpha, float duration)
        {
            float startAlpha = _fadeImage.color.a;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;

                float t = time / duration;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

                _fadeImage.color = new Color(0f, 0f, 0f, alpha);

                yield return null;
            }

            _fadeImage.color = new Color(0f, 0f, 0f, targetAlpha);
        }

        private IEnumerator WaitForWorldReady()
        {
            float timeout = 4f;
            float t = 0f;

            while (t < timeout)
            {
                t += Time.deltaTime;

                Transform cam = Camera.main != null ? Camera.main.transform : null;

                if (cam != null)
                {
                    Collider[] hits = Physics.OverlapSphere(cam.position, 5f);

                    if (hits != null && hits.Length > 10)
                        break;
                }

                yield return null;
            }
        }     
        
    }
}