using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS {
    [CreateAssetMenu(menuName = "CivilFX/Traffic/BakedTrafficBath Collector", fileName = "BakedTrafficBathCollector")]
    public class BakedTrafficPathCollector : ScriptableObject {
        public BakedTrafficPath[] bakedTrafficPaths;
    }
}
