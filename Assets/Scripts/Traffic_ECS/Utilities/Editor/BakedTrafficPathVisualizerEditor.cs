using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    [CustomEditor(typeof(BakedTrafficPathVisualizer))]
    public class BakedTrafficPathVisualizerEditor : Editor
    {
        private SerializedObject so;
        private SerializedProperty sp;
        private BakedTrafficPath path;
        private BakedTrafficPathVisualizer pathVisualizer;
        private int node;
        private int start;
        private int end;
        private int length;
        private BakedTrafficPathVisualizer.VisualizedType type;

        private void OnEnable()
        {
            so = serializedObject;
            pathVisualizer = (BakedTrafficPathVisualizer)target;
        }

        public override void OnInspectorGUI()
        {
            so.Update();

            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.PropertyField(so.FindProperty("m_Script"));
            }

            sp = so.FindProperty("path");
            EditorGUILayout.PropertyField(sp);
            path = (BakedTrafficPath)sp.objectReferenceValue;

            sp = so.FindProperty("type");
            EditorGUILayout.PropertyField(sp);
            
            switch (sp.enumValueIndex)
            {
                //single
                case 0:
                    node = so.FindProperty("node").intValue;
                    using (new EditorGUI.DisabledGroupScope(path == null))
                    {
                        node = EditorGUILayout.IntField(new GUIContent("node"), node);
                        node = Mathf.Clamp(node, 0, path == null ? 0 : path.PathNodes.Count - 1);
                    }
                    so.FindProperty("node").intValue = node;
                    break;

                //length
                case 1:

                    start = so.FindProperty("startNode").intValue;
                    end = so.FindProperty("endNode").intValue;
                    using (new EditorGUI.DisabledGroupScope(path == null))
                    {
                        start = EditorGUILayout.IntField(new GUIContent("start"), start);
                        start = Mathf.Clamp(start, 0, path == null ? 0 : path.PathNodes.Count - 1);

                        end = EditorGUILayout.IntField(new GUIContent("end"), end);
                        end = Mathf.Clamp(end, 0, path == null ? 0 : path.PathNodes.Count - 1);

                        length = end - start;
                        using (new EditorGUI.DisabledGroupScope(true))
                        {
                            EditorGUILayout.IntField(new GUIContent("length"), length);
                        }

                    }
                    so.FindProperty("startNode").intValue = start;
                    so.FindProperty("endNode").intValue = end;
                    break;
            }
            so.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (path != null)
            {
                type = pathVisualizer.type;

                switch (type)
                {
                    case BakedTrafficPathVisualizer.VisualizedType.SingleNode:
                        Handles.PositionHandle(path.PathNodes[node], Quaternion.identity);
                        Handles.ArrowHandleCap(0, path.PathNodes[node] + new Vector3(0f, 1.1f, 0f), Quaternion.LookRotation(-Vector3.up), 1.0f, EventType.Repaint);
                        break;

                    case BakedTrafficPathVisualizer.VisualizedType.Length:
                        Handles.PositionHandle(path.PathNodes[start], Quaternion.identity);
                        Handles.ArrowHandleCap(0, path.PathNodes[start] + new Vector3(0f, 1.1f, 0f), Quaternion.LookRotation(-Vector3.up), 1.0f, EventType.Repaint);
                        Handles.PositionHandle(path.PathNodes[end], Quaternion.identity);
                        Handles.ArrowHandleCap(0, path.PathNodes[end] + new Vector3(0f, 1.1f, 0f), Quaternion.LookRotation(-Vector3.up), 1.0f, EventType.Repaint);
                        break;

                }

            }
        }

    }
}