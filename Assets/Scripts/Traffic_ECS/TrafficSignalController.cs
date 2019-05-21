using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    public class TrafficSignalController : MonoBehaviour
    {
        public BakedPathSet[] sets;
        public TrafficSignalSequence sequence;
    }


    [System.Serializable]
    public class BakedPathSet
    {
        public byte id;
        public BakedPathInfo[] bakedPaths;
    }

    [System.Serializable]
    public class BakedPathInfo
    {
        public BakedTrafficPath path;
        public int stopPos;
    }
}