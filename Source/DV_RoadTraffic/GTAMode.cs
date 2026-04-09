using UnityEngine;

namespace DV_RoadTraffic
{
    public static class GTAImpactHandler
    {
        const float EXPLOSION_THRESHOLD = 3f;
        const float GTA_IMPULSE_MULTIPLIER = 5f;

        public static void HandleTrainImpact(
            TrafficVehicleController vehicle,
            TrainCar train,                
            Collision collision = null)    
        {
            if (vehicle == null)
                return;

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null)
                return;
           
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            float impactSpeed = 10f; // default fallback
            Vector3 launch;

            // =========================================================
            // COLLISION PATH (NOT CURRENTLY USED / COLLISIONS INACTIVE)
            // =========================================================
            if (collision != null)
            {
                Main.Log("[DVRT] IMPACT via COLLISION path");

                impactSpeed = collision.relativeVelocity.magnitude;

                float clampedSpeed = Mathf.Clamp(impactSpeed, 0f, 20f);
                float massFactor = Mathf.Clamp(rb.mass / 1500f, 0.5f, 3f);

                Vector3 direction = collision.relativeVelocity.normalized;

                Vector3 lateral =
                    (direction * clampedSpeed * 1.5f) / massFactor;

                float vertical =
                    Mathf.Min(clampedSpeed * 0.15f, 4f);

                launch = lateral + Vector3.up * vertical;
            }

            // =========================================================
            // 🔹 NULL COLLISION PATH 
            // =========================================================
            else
            {
                Main.Log("[DVRT] IMPACT via NULL collision path");

                float oomph = Main.Settings.impactOomph;
                float finalOomph = oomph * 3.0f;

                Vector3 direction;

                if (train != null)
                {
                    direction = (vehicle.transform.position - train.transform.position);
                    direction.y = 0f;

                    if (direction.sqrMagnitude < 0.001f)
                        direction = -vehicle.transform.forward;

                    direction.Normalize();
                }
                else
                {                        
                    direction = -vehicle.transform.forward;
                }
                
                impactSpeed = 12f; // consistent visual force (tune if needed)

                float clampedSpeed = Mathf.Clamp(impactSpeed, 0f, 12f);

            Vector3 lateral =                
            (direction * clampedSpeed * 0.8f * finalOomph);

                float vertical =
                    Mathf.Min(clampedSpeed * 0.08f * finalOomph, 2.0f * finalOomph);

                launch = lateral + Vector3.up * vertical;
            }

            bool explosive = impactSpeed > EXPLOSION_THRESHOLD;

            vehicle.DestroyVehicle(launch, explosive);

            Main.Log($"[DVRT] Impact {impactSpeed:F1} | oomph={Main.Settings.impactOomph:F2}");
        }
    }
    

}
