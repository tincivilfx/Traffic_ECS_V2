using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Threading;

namespace CivilFX.TrafficECS
{
    internal enum BakeStatus
    {
        Preparing,
        InProgress,
        Finished
    }


    [CustomEditor(typeof(TrafficPathBaker))]
    public class TrafficPathBakerEditor : Editor
    {
        internal class BakedPathInfo
        {
            public TrafficPath path;
            public float step;
            public float progress;
            public List<Vector3> bakedNodes = new List<Vector3>();
            public Thread threadHandle;
        }


        private int fieldCount;
        private TrafficPathBaker _target;
        private List<TrafficPath> _paths;
        private List<BakedPathInfo> pathInfos;
        private BakeStatus status;

        private float fixedTime;

        private void OnEnable()
        {
            _target = (TrafficPathBaker)target;
            _target.doBakeWhenPlaying = false;
            pathInfos = new List<BakedPathInfo>();
            fixedTime = Time.fixedDeltaTime;
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty scriptNameProp = serializedObject.FindProperty("m_Script");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(scriptNameProp);

            }

            var pp = serializedObject.FindProperty("paths");

            GUILayout.BeginVertical();
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Load All", GUILayout.MaxWidth(200)))
            {

                //remove all
                pp.ClearArray();

                //find all paths
                var allPaths = Resources.FindObjectsOfTypeAll<TrafficPath>();

                //add
                for (int i = 0; i < allPaths.Length; i++)
                {
                    pp.InsertArrayElementAtIndex(pp.arraySize == 0 ? 0 : pp.arraySize - 1);
                }

                //set
                for (int i = 0; i < allPaths.Length; i++)
                {
                    pp.GetArrayElementAtIndex(i).objectReferenceValue = allPaths[i];
                }
                serializedObject.FindProperty("paths").isExpanded = true;
            }
            if (GUILayout.Button("Load Active", GUILayout.MaxWidth(200)))
            {
                //remove all
                pp.ClearArray();

                //find all paths
                var allPaths = Resources.FindObjectsOfTypeAll<TrafficPath>();

                //add
                for (int i = 0; i < allPaths.Length; i++)
                {
                    if (allPaths[i].gameObject.activeInHierarchy)
                    {
                        pp.InsertArrayElementAtIndex(pp.arraySize == 0 ? 0 : pp.arraySize - 1);
                    }
                }

                int currentIndex = 0;
                for (int i = 0; i < allPaths.Length; i++)
                {
                    if (allPaths[i].gameObject.activeInHierarchy)
                    {
                        pp.GetArrayElementAtIndex(currentIndex).objectReferenceValue = allPaths[i];
                        currentIndex++;
                    }
                }
                serializedObject.FindProperty("paths").isExpanded = true;
            }

            if (GUILayout.Button("Remove All", GUILayout.MaxWidth(200)))
            {
                pp.ClearArray();
                serializedObject.FindProperty("paths").isExpanded = false;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            //Add button
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add", GUILayout.MaxWidth(200)))
            {
                pp.InsertArrayElementAtIndex(pp.arraySize == 0 ? 0 : pp.arraySize - 1);
                serializedObject.FindProperty("paths").isExpanded = true;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            GUILayout.EndVertical();

            //show paths
            SerializedProperty pathsProp = serializedObject.FindProperty("paths");
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(pathsProp, new GUIContent(""), false, GUILayout.MaxWidth(1));
            EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            if (pathsProp.isExpanded)
            {

                EditorGUI.indentLevel++;
                for (int i = 0; i < pathsProp.arraySize; i++)
                {
                    GUILayout.BeginHorizontal();
                    SerializedProperty pathProp = pathsProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(pathProp, new GUIContent(""));
                    if (pathProp.objectReferenceValue != null)
                    {
                        var so = new SerializedObject(pathProp.objectReferenceValue);
                        if (so != null)
                        {
                            EditorGUIUtility.labelWidth = 50;
                            so.Update();
                            EditorGUILayout.IntSlider(so.FindProperty("bakedResolution"), 1, 100, new GUIContent("Res"), GUILayout.MaxWidth(200));
                            so.ApplyModifiedProperties();
                            EditorGUIUtility.labelWidth = 0;
                        }
                    }
                    //delete button
                    if (GUILayout.Button("X", GUILayout.MaxWidth(50)))
                    {

                        pp.MoveArrayElement(i, pp.arraySize - 1);
                        pp.arraySize -= 1;
                    }
                    GUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

            }

            //show path resolution
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useOneResolutionForAll"), new GUIContent("Enable"));
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("useOneResolutionForAll").boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("resolution"));
            }

            //show saved location
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Location for all baked assets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("savedLocation"));
            GUILayout.EndVertical();

            //
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Start baking when enter PlayMode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doBakeWhenPlaying"), new GUIContent("Auto Baking"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exitPlayMode"));
            GUILayout.EndVertical();



            //BAKE button
            GUILayout.BeginVertical();
            GUILayout.Space(50);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (status == BakeStatus.Preparing && GUILayout.Button("Bake", GUILayout.MaxWidth(150), GUILayout.MaxHeight(30)))
            {
                //show confirm dialog

                pathInfos.Clear();
                Debug.Log(pathInfos.Count);

                //preparing paths
                for (int i=0; i<_target.paths.Count; i++)
                {
                    BakedPathInfo info = new BakedPathInfo();
                    info.path = _target.paths[i];
                    info.step = GetBakedTime(info.path);
                    //var t = new Thread(new ThreadStart(BakeRoutine));
                    Thread t = new Thread(() => BakeRoutine(info));
                    t.Start();
                    info.threadHandle = t;
                    pathInfos.Add(info);
                }

                //
                status = BakeStatus.InProgress;
                EditorApplication.update += MonitorStatusRoutine;
                
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(20);
            GUILayout.EndVertical();


            //show status
            if (status == BakeStatus.InProgress)
            {
                for (int i=0; i<pathInfos.Count; i++)
                {
                    Rect r = EditorGUILayout.BeginVertical();
                    EditorGUI.ProgressBar(r, pathInfos[i].progress, pathInfos[i].path.gameObject.name);
                    GUILayout.Space(16);
                    EditorGUILayout.EndVertical();
                }
            }



            serializedObject.ApplyModifiedProperties();

        }

        private float GetBakedTime(TrafficPath path)
        {
            float speed = path.bakedResolution;
            GoSpline spline = path.Spline();
            float duration = spline.pathLength;
            return fixedTime / (duration / (speed * 0.44704f));
        }

        private void BakeRoutine(BakedPathInfo info)
        {
            GoSpline spline = info.path.Spline();
            while (info.progress < 1.0f)
            {
                Vector3 pos = spline.getPointOnPath(info.progress);
                info.bakedNodes.Add(pos);
                info.progress += info.step;
            }
        }

        private void MonitorStatusRoutine()
        {
            int aliveCount = 0;

            for (int i=0; i<pathInfos.Count; i++)
            {
                if (pathInfos[i].threadHandle.IsAlive)
                {
                    aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                Debug.Log("Baking Done");
                //all done
                //write data to asset

                for (int i=0; i<pathInfos.Count; i++)
                {
                    if (pathInfos[i].bakedNodes == null)
                    {
                        continue;
                    }
                    TrafficPath path = pathInfos[i].path;
                    Debug.Log(pathInfos[i].bakedNodes.Count);
                    BakedTrafficPath bakedPath = ScriptableObject.CreateInstance<BakedTrafficPath>();
                    bakedPath.Init(pathInfos[i].bakedNodes, path.gameObject.name, path.type, path.pathSpeedMPH, path.bakedResolution, path.splitChance, path.notes);
                    bakedPath.CreateAndSave(serializedObject.FindProperty("savedLocation").stringValue);
                    pathInfos[i].bakedNodes = null;
                    return;
                }

                //reset
                EditorApplication.update -= MonitorStatusRoutine;
                status = BakeStatus.Preparing;
            }

        }
    }
}
