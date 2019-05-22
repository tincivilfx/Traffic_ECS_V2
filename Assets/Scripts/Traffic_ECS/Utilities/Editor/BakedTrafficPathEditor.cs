using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace CivilFX.TrafficECS
{

    [CanEditMultipleObjects]
    [CustomEditor(typeof(BakedTrafficPath))]
    public class BakedTrafficPathEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool expanded = true;

            while (property.NextVisible(expanded))
            {
                //Debug.Log(property.propertyPath);
                using (new EditorGUI.DisabledScope("m_Script" == property.propertyPath))
                {
                    if (property.propertyPath.Equals("splittingPaths"))
                    {
                        //Turned Paths
                        EditorGUILayout.PropertyField(property, false);
                        if (property.isExpanded)
                        {
                            EditorGUI.indentLevel++;

                            //showing size of array
                            //also modifying the array
                            int oldSize = property.arraySize;
                            int newSize = EditorGUILayout.DelayedIntField(new GUIContent("Size: "), oldSize);
                            int allo = newSize - oldSize;
                            if (allo > 0)
                            {
                                //newSize is more than old Size
                                //increate the array
                                do
                                {
                                    property.InsertArrayElementAtIndex(oldSize);
                                    allo--;
                                } while (allo > 0);

                            }
                            else if (allo < 0)
                            {
                                //newSize is less than old Size
                                //remove last indicies
                                do
                                {
                                    property.DeleteArrayElementAtIndex(property.arraySize - 1);
                                    allo++;
                                } while (allo < 0);

                            }

                            var childProp = property.GetEnumerator();
                            while (childProp.MoveNext())
                            {
                                EditorGUI.indentLevel++;
                                var currentChild = childProp.Current as SerializedProperty;
                                //Element 0..n
                                EditorGUILayout.PropertyField(currentChild, false);
                                if (currentChild.isExpanded)
                                {
                                    var currentChildChildProp = currentChild.GetEnumerator();

                                    int currentIndex = 0;
                                    SerializedProperty pathProp = null;

                                    List<SerializedProperty> props = new List<SerializedProperty>();

                                    while (currentChildChildProp.MoveNext())
                                    {

                                        EditorGUI.indentLevel++;
                                        var current = currentChildChildProp.Current as SerializedProperty;
                                        using (new EditorGUI.DisabledScope(current.propertyPath.Contains("transitionNode")
                                            || current.propertyPath.Contains("startNode")))
                                        {
                                            EditorGUILayout.PropertyField(current);
                                        }
                                        if (currentIndex == 0)
                                        {
                                            pathProp = current.Copy();
                                        }
                                        props.Add(current.Copy());
                                        currentIndex++;
                                        EditorGUI.indentLevel--;
                                    }

                                    GUILayout.BeginHorizontal();
                                    GUILayout.FlexibleSpace();
                                    if (GUILayout.Button("Calculate", GUILayout.MaxWidth(100)))
                                    {

                                        props[1].intValue = GetTransitionNode((BakedTrafficPath)target, ((BakedTrafficPath)props[0].objectReferenceValue));
                                        props[2].intValue = 0;
                                        props[3].intValue = ((BakedTrafficPath)(props[0].objectReferenceValue)).splitToChance;
                                        /*
                                        GetTransitNodes((BakedTrafficPath)target, (BakedTrafficPath)props[0].objectReferenceValue, out int t, out int c);
                                        Debug.Log(t + "- " + c);
                                        props[1].intValue = t;
                                        props[2].intValue = c;
                                        */
                                    }
                                    GUILayout.FlexibleSpace();
                                    GUILayout.EndHorizontal();
                                }
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }

                    }
                    else if (property.propertyPath.Equals("connectingPaths"))
                    {
                        //Connecting paths
                        EditorGUILayout.PropertyField(property, false);
                        if (property.isExpanded)
                        {
                            EditorGUI.indentLevel++;

                            //showing size of array
                            //also modifying the array
                            int oldSize = property.arraySize;
                            int newSize = EditorGUILayout.DelayedIntField(new GUIContent("Size: "), oldSize);
                            int allo = newSize - oldSize;
                            if (allo > 0)
                            {
                                //newSize is more than old Size
                                //increate the array
                                do
                                {
                                    property.InsertArrayElementAtIndex(oldSize);
                                    allo--;
                                } while (allo > 0);

                            }
                            else if (allo < 0)
                            {
                                //newSize is less than old Size
                                //remove last indicies
                                do
                                {
                                    property.DeleteArrayElementAtIndex(property.arraySize - 1);
                                    allo++;
                                } while (allo < 0);

                            }

                            var childProp = property.GetEnumerator();
                            while (childProp.MoveNext())
                            {
                                EditorGUI.indentLevel++;
                                var currentChild = childProp.Current as SerializedProperty;
                                //Element 0..n
                                EditorGUILayout.PropertyField(currentChild, false);
                                if (currentChild.isExpanded)
                                {
                                    var currentChildChildProp = currentChild.GetEnumerator();

                                    int currentIndex = 0;
                                    SerializedProperty pathProp = null;

                                    List<SerializedProperty> props = new List<SerializedProperty>();

                                    while (currentChildChildProp.MoveNext())
                                    {
                                        EditorGUI.indentLevel++;
                                        var current = currentChildChildProp.Current as SerializedProperty;
                                        using (new EditorGUI.DisabledScope(current.propertyPath.Contains("transitionNode")
                                            || current.propertyPath.Contains("startNode")))
                                        {
                                            EditorGUILayout.PropertyField(current);
                                        }
                                        if (currentIndex == 0)
                                        {
                                            pathProp = current.Copy();
                                        }
                                        props.Add(current.Copy());

                                        currentIndex++;
                                        EditorGUI.indentLevel--;
                                    }

                                    GUILayout.BeginHorizontal();
                                    GUILayout.FlexibleSpace();
                                    if (GUILayout.Button("Calculate", GUILayout.MaxWidth(100)))
                                    {
                                        props[1].intValue = ((BakedTrafficPath)target).PathNodes.Count - 1;
                                        props[2].intValue = GetTransitionNode(((BakedTrafficPath)props[0].objectReferenceValue), (BakedTrafficPath)target, ((BakedTrafficPath)target).PathNodes.Count - 1);
                                    }
                                    GUILayout.FlexibleSpace();
                                    GUILayout.EndHorizontal();
                                }
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                    else if (property.propertyPath.Equals("pathNodes"))
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.TextField(new GUIContent("Path Nodes Count: "), property.arraySize.ToString());
                            EditorGUILayout.TextField(new GUIContent("Path Resolution: "), serializedObject.FindProperty("bakedResolution").intValue.ToString());
                            EditorGUILayout.TextField(new GUIContent("Actual Speed Limit: "), serializedObject.FindProperty("actualSpeedLimit").intValue.ToString());
                        }

                    } else if (property.propertyPath.Equals("type"))
                    {
                        using (new EditorGUI.DisabledScope(false))
                        {
                            EditorGUILayout.PropertyField(property);
                        }
                    }
                    else if (property.propertyPath.Equals("speedLitmit"))
                    {
                        EditorGUILayout.PropertyField(property, true);
                        int resolution = serializedObject.FindProperty("bakedResolution").intValue;
                        serializedObject.FindProperty("actualSpeedLimit").intValue = resolution == 0 ? 0 : property.intValue / resolution;
                    }
                    else if (property.propertyPath.Equals("notes"))
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField(new GUIContent("Notes:"), EditorStyles.boldLabel);
                        EditorGUILayout.TextArea(property.stringValue);
                        /*
                        using (new EditorGUI.DisabledScope(true))
                        {

                            EditorGUILayout.TextArea(property.stringValue);
                        }
                        */
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }

                }
                expanded = false;
            }

            //modify name
            string newName = serializedObject.FindProperty("pathName").stringValue;
            target.name = newName;
            AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(target), newName);

            //apply all changes
            serializedObject.ApplyModifiedProperties();
        }

        private int GetTransitionNode(BakedTrafficPath mainPath, BakedTrafficPath turnedPath, int indexToSearch = 0)
        {

            if (turnedPath == null)
            {
                return 0;
            }
            float minDistance = float.MaxValue;
            int node = 0;
            for (int i = 0; i < mainPath.PathNodes.Count; i++)
            {
                Vector3 v3 = mainPath.PathNodes[i];
                Vector3 v33 = turnedPath.PathNodes[indexToSearch];
                float currentDistance = Vector3.Distance(mainPath.PathNodes[i], turnedPath.PathNodes[indexToSearch]);

                if (currentDistance < minDistance)
                {
                    minDistance = currentDistance;
                    node = i;
                }
            }
            return node;


        }

        private void GetTransitNodes(BakedTrafficPath majorPath, BakedTrafficPath minorPath, out int transitionNode, out int connectingNode)
        {

            int tempI = -1;
            int tempJ = -1;
            float minDis = float.MaxValue;
            float dis;

            for (int i = 0; i < majorPath.PathNodes.Count; i++)
            {
                for (int j = 0; j < minorPath.PathNodes.Count; j++)
                {
                    dis = Vector3.Distance(majorPath.PathNodes[i], minorPath.PathNodes[j]);
                    if (dis < minDis)
                    {
                        minDis = dis;
                        tempI = i;
                        tempJ = j;
                    }
                }
            }
            transitionNode = tempI;
            connectingNode = tempJ;
        }

    }
}