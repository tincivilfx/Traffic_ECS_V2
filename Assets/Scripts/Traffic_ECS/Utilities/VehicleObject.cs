using UnityEngine;

namespace CivilFX.TrafficECS
{

    [CreateAssetMenu(fileName = "VehicleObject", menuName = "CivilFX/TrafficECS/VehicleObject")]
    public class VehicleObject : ScriptableObject
    {
        public GameObject body;
        public int bodyLength;
        public GameObject[] wheels;
    }
}