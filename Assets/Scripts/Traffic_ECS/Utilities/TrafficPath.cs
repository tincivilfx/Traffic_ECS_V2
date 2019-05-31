using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    public enum TrafficPathType : byte
    {
        Main,
        MainConnector,
        LeftTurn,
        RightTurn,
        Connector
    }
    public class TrafficPath : MonoBehaviour
    {


        #region private fields
        private GoSpline spline;
        #endregion

        #region public fields
        public List<Vector3> nodes;
        public int pathSpeedMPH = 65;
        public string pathName;
        public bool smartTraffic;
        public BakedPathType pathType;
        [Range(1, 100)]
        public int bakedResolution = 2;
        [Range(0, 100)]
        public int splitChance;
        public TrafficPathType type;
        public bool allowRespawn;

        public string notes;

        #region fields used only for inspector
        public bool displayInEditor = true;
        public bool forceStraightLinePath = false;
        public Color lineColor = Color.yellow;
        public bool addMultiple;
        [Range(1, 99)]
        public int addMultipleCount;
        #endregion
        #endregion


        public void ProjectNodesOntoMesh()
        {
            for (int i = 0; i < nodes.Count; i++)
            {

            }
        }

        public void OnDrawGizmos()
        {
            if (displayInEditor && nodes != null && nodes.Count > 1)
            {
                Gizmos.color = lineColor;
                if (forceStraightLinePath)
                {
                    for (int i = 0; i < nodes.Count - 1; i++)
                    {

                        Gizmos.DrawLine(nodes[i], nodes[i + 1]);
                    }
                }
                else
                {
                    var spline = new GoSpline(nodes);
                    spline.drawGizmos(50);
                }
            }
        }

        public GoSpline Spline()
        {
            if (spline == null)
            {
                spline = new GoSpline(nodes);
                spline.buildPath();
            }
            return spline;
        }

    }
}