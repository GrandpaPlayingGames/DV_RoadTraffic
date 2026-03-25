using UnityEngine;

namespace DV_RoadTraffic
{
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

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            float impactSpeed;
            Vector3 launch;

            if (collision != null)
            {
                impactSpeed = collision.relativeVelocity.magnitude;

                launch =
                    collision.relativeVelocity * 0.8f +
                    Vector3.up * impactSpeed * 0.4f;
            }
            else
            {
                impactSpeed = rb.velocity.magnitude;

                float baseForce = Mathf.Max(impactSpeed * 25f, 20f);
                float launchPower = baseForce * GTA_IMPULSE_MULTIPLIER;

                launch =
                    rb.velocity.normalized * launchPower +
                    Vector3.up * launchPower * 0.8f;
            }

            bool explosive = impactSpeed > EXPLOSION_THRESHOLD;

            vehicle.DestroyVehicle(launch, explosive);

            Main.Log($"[DVRT] Impact {impactSpeed:F1} m/s");
        }
    }
}
