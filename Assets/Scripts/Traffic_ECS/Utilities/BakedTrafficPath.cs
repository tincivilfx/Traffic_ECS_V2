using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace CivilFX.TrafficECS
{

    public enum BakedPathType
    {
        Main,
        MainConnector,
        LeftTurn,
        RightTurn,
        TurnedConnector
    }

    [CreateAssetMenu(menuName = "CivilFX/Traffic/BakedTrafficPath", fileName = "BakedPath")]
    public class BakedTrafficPath : ScriptableObject
    {

        #region serialized private fields

        [SerializeField]
        private string pathName;

        [SerializeField]
        private List<Vector3> pathNodes;

        [HideInInspector]
        [SerializeField]
        private string filePath;
        #endregion

        [HideInInspector]
        public int actualSpeedLimit;

        public TrafficPathType type;
        #region properties
        public List<Vector3> PathNodes
        {
            get { return pathNodes; }
            set { pathNodes = value; }
        }

        public string PathName
        {
            get { return pathName.Equals("", System.StringComparison.Ordinal) ? "<Empty>" : pathName; }
            set { pathName = value; }
        }

        public string FilePath
        {
            set { filePath = value; }
        }

        #endregion

        [HideInInspector]
        public int bakedResolution;

        #region public fields
        [Header("List of splitting paths:")]
        public List<BakedTrafficPathTurnedInfo> splittingPaths;

        [Header("List of connecting paths:")]
        public List<BakedTrafficPathMergedInfo> connectingPaths;

        [Header("List of merge paths:")]
        public BakedTrafficPath leftPath;
        public BakedTrafficPath rightPath;

        [Header("Total vehicles on path:")]
        public int vehiclesCount;

        [Header("Default Speed Limit")]
        [Range(1, 100)]
        public int speedLitmit;

        [Header("Default Vehicles' Rotaion:")]
        public Vector3 defaultRotation;

        [Header("Camera Path Settings:")]
        public bool cameraPath;

        [Header("Smart Vehicles On This Path:")]
        public bool enableSmartVehicles;

        [Header("Notes:")]
        public string notes;

        [HideInInspector]
        public int averageOffset;

        [HideInInspector]
        public int splitToChance;

        [System.NonSerialized]
        [HideInInspector]
        public int splitToCount;

        //ECS
        [HideInInspector]
        public byte id;


        #endregion

        public void Init(List<Vector3> _pathNodes, string _pathName, TrafficPathType _pathType, int pathSpeed, int _resolution, int _splitToChance ,string _notes)
        {
            pathNodes = _pathNodes;
            pathName = _pathName;
            type = _pathType;
            speedLitmit = pathSpeed;
            bakedResolution = _resolution;
            actualSpeedLimit = speedLitmit / _resolution;
            splitToChance = _splitToChance;
            notes = _notes;
        }

        public void CreateAndSave(string location)
        {
#if UNITY_EDITOR
            BakedTrafficPath path = (BakedTrafficPath)AssetDatabase.LoadAssetAtPath(BuildFilePath(location, pathName), typeof(BakedTrafficPath));
            if (path != null)
            {
                Debug.Log("Refreshing: " + BuildFilePath(location, pathName));
                path.PathNodes = pathNodes;
                path.PathName = pathName;
                path.type = type;
                path.speedLitmit = speedLitmit;
                path.bakedResolution = bakedResolution;
                path.actualSpeedLimit = actualSpeedLimit;
                path.enableSmartVehicles = enableSmartVehicles;
                path.splitToChance = splitToChance;
                path.notes = notes;
                path.vehiclesCount = vehiclesCount;
                EditorUtility.SetDirty(path);
            }
            else
            {
                Debug.Log("Creating: " + BuildFilePath(location, pathName));
                AssetDatabase.CreateAsset(this, BuildFilePath(location, pathName));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }

        public static string BuildFilePath(string location, string _pathName)
        {
            Debug.Log(System.IO.Path.Combine(location, _pathName + ".asset"));
            return location + "/" + _pathName + ".asset";

        }

    }

    public struct MergingNodeInfo
    {
        [System.NonSerialized]
        public Vector3 node;

        [System.NonSerialized]
        public bool status;
    }


    [System.Serializable]
    public class BakedTrafficPathTurnedInfo
    {
        public BakedTrafficPath turnedPath;
        public int transitionNode;
        public int startNode;
        public int turnedChance;
    }

    [System.Serializable]
    public class BakedTrafficPathMergedInfo
    {
        public BakedTrafficPath turnedPath;
        public int transitionNode;
        public int startNode;
        public int startScanNode;
        public int endScanNode;
        public int yieldNode;
    }

}