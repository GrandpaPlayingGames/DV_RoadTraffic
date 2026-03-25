using System;
using UnityEngine;

namespace DV_RoadTraffic
{
    public static class DVRT_WorldShiftManager
    {
        public static event Action<Vector3> OnWorldShift;

        private static Vector3 _lastWorldMove;
        private static bool _primed;

        public static Vector3 CurrentMove { get; private set; }

        public static void Update()
        {
            Vector3 currentMove = WorldMover.currentMove;

            if (!_primed)
            {
                if (currentMove.sqrMagnitude < 1f)
                    return;

                _lastWorldMove = currentMove;
                CurrentMove = currentMove;
                _primed = true;

                Main.Log($"[DVRT WOS] Primed at {currentMove}");
                return;
            }

            Vector3 delta = currentMove - _lastWorldMove;

            if (delta.sqrMagnitude > 1f)
            {
                _lastWorldMove = currentMove;
                CurrentMove = currentMove;

                Main.Log($"[DVRT WOS] Detected delta={delta}");

                var handlers = OnWorldShift;
                if (handlers != null)
                {
                    foreach (var d in handlers.GetInvocationList())
                    {
                        try
                        {
                            ((Action<Vector3>)d)(delta);
                        }
                        catch (Exception ex)
                        {
                            Main.Log($"[DVRT WOS] Subscriber error: {ex}");
                        }
                    }
                }
            }
        }
    }
}

