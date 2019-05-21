using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS
{

    [CreateAssetMenu(fileName = "Sequence", menuName = "CivilFX/TrafficECS/Signal Sequence")]
    public class TrafficSignalSequence : ScriptableObject
    {
        public FrameInfo[] sequences;
    }


    [System.Serializable]
    public class FrameInfo
    {
        public float time;
        public byte setID;
        public TrafficPathType type;
        public bool active;
    }
}
