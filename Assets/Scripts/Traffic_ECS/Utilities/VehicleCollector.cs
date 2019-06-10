using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS
{

    [CreateAssetMenu(fileName = "VehicleObjects Collector", menuName = "CivilFX/TrafficECS/VehicleObjectCollector")]
    public class VehicleCollector : ScriptableObject
    {
        public VehicleObject[] vehicles;
        public int[] percentage;
    }
}