using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    public class BakedTrafficPathVisualizer : MonoBehaviour
    {
        public enum VisualizedType
        {
            SingleNode,
            Length
        }


        public BakedTrafficPath path;
        public VisualizedType type;
        public int startNode;
        public int endNode;
        public int node;
    }
}