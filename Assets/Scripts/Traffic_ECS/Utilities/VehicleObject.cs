using UnityEngine;

namespace CivilFX.TrafficECS
{

    public enum VehicleType
    {
        Light,
        Medium,
        Heavy
    }

    [CreateAssetMenu(fileName = "VehicleObject", menuName = "CivilFX/TrafficECS/VehicleObject")]
    public class VehicleObject : ScriptableObject
    {
        public GameObject body;
        public int bodyLength;
        public GameObject[] wheels;
        public VehicleType type = VehicleType.Medium;
    }
}