using UnityEngine;

namespace DV_RoadTraffic
{
    public interface ITrafficDestructible
    {
        void DestroyVehicle(Vector3 impulse, bool explosive);
    }
}
