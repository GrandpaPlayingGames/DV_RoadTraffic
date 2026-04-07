using UnityEngine;

namespace DV_RoadTraffic
{
    /*
    public static class GTAImpactHandler
    {
        
        const float EXPLOSION_THRESHOLD = 3f;
        const float GTA_IMPULSE_MULTIPLIER = 5f;

        public static void HandleTrainImpact(
            TrafficVehicleController vehicle,
            Collision collision)
        {
            if (vehicle == null)
                return;

            if (collision == null)
            {
                Main.Log("[DVRT] IMPACT via NULL collision path");
            }
            else
            {
                Main.Log("[DVRT] IMPACT via COLLISION path");
            }

            // nOTE: oNLY THE imp{act VIA null COLLISION BRANCH EXECUTES

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            float impactSpeed;
            Vector3 launch;

            if (collision != null)
            {
                impactSpeed = collision.relativeVelocity.magnitude;

                // 🔥 HARD CLAMP — THIS IS THE FIX
                float clampedSpeed = Mathf.Clamp(impactSpeed, 0f, 20f);

                float massFactor = Mathf.Clamp(rb.mass / 1500f, 0.5f, 3f);

                // Use direction only, not raw velocity magnitude
                Vector3 direction = collision.relativeVelocity.normalized;

                Vector3 lateral =
                    (direction * clampedSpeed * 1.5f) / massFactor;

                float vertical =
                    Mathf.Min(clampedSpeed * 0.15f, 4f);

                launch =
                    lateral + Vector3.up * vertical;
            }
            else
            {
                impactSpeed = rb.velocity.magnitude;

                float oomph = Main.Settings.impactOomph; // 🔥 master multiplier

                // 🔥 BASELINE SHIFT (this is the key line)
                float finalOomph = oomph * 3.0f;

                // 🔥 HARD CLAMP (keep stable, do NOT scale this)
                float clampedSpeed = Mathf.Clamp(impactSpeed, 0f, 12f);

                float massFactor = Mathf.Clamp(rb.mass / 1500f, 0.5f, 3f);

                Vector3 direction = rb.velocity.normalized;

                // 🔥 SCALE LATERAL FORCE
                Vector3 lateral =
                    (direction * clampedSpeed * 0.8f * finalOomph) / massFactor;

                // 🔥 SCALE VERTICAL FORCE + CAP
                float vertical =
                    Mathf.Min(clampedSpeed * 0.08f * finalOomph, 2.0f * finalOomph);

                launch =
                    lateral + Vector3.up * vertical;
            }

            bool explosive = impactSpeed > EXPLOSION_THRESHOLD;

            vehicle.DestroyVehicle(launch, explosive);

            Main.Log($"[DVRT] Impact {impactSpeed:F1} m/s | oomph={Main.Settings.impactOomph:F2}");
        }
        */

        public static class GTAImpactHandler
        {
            const float EXPLOSION_THRESHOLD = 3f;
            const float GTA_IMPULSE_MULTIPLIER = 5f;

            public static void HandleTrainImpact(
                TrafficVehicleController vehicle,
                TrainCar train,                // 🔥 CHANGED: pass train instead of Collision
                Collision collision = null)    // optional (keep compatibility)
            {
                if (vehicle == null)
                    return;

                Rigidbody rb = vehicle.GetComponent<Rigidbody>();
                if (rb == null)
                    return;

                // 🔥 STEP 1 — TAKE CONTROL (critical fix)
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                float impactSpeed = 10f; // default fallback
                Vector3 launch;

                // =========================================================
                // 🔹 COLLISION PATH (if you ever wire it back in)
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
                // 🔹 NULL COLLISION PATH (THIS IS YOUR MAIN FIX)
                // =========================================================
                else
                {
                    Main.Log("[DVRT] IMPACT via NULL collision path");

                    float oomph = Main.Settings.impactOomph;
                    float finalOomph = oomph * 3.0f;

                    //float massFactor = Mathf.Clamp(rb.mass / 1500f, 0.5f, 3f);

                    // 🔥 CRITICAL FIX — use TRAIN RELATIVE DIRECTION
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
                        // fallback safety
                        direction = -vehicle.transform.forward;
                    }

                    // 🔥 use a stable "impact speed" instead of rb.velocity
                    impactSpeed = 12f; // consistent visual force (tune if needed)

                    float clampedSpeed = Mathf.Clamp(impactSpeed, 0f, 12f);

                    Vector3 lateral =
                    //    (direction * clampedSpeed * 0.8f * finalOomph) / massFactor;
                    (direction * clampedSpeed * 0.8f * finalOomph)

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
