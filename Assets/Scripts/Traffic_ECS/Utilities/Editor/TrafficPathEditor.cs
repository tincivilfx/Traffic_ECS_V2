using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CivilFX.TrafficECS
{

    [CanEditMultipleObjects]
    [CustomEditor(typeof(TrafficPath))]
    public class TrafficPathEditor : Editor
    {

        private TrafficPath _target;
        private SerializedObject so;
        private GUIStyle labelStyle;
        private bool displayNodes;
        private bool moreTools;
        private bool drawBorder;
        private float length = 1.7f;

        private int projectNodesCount;
        private int from, to;

        //circle tools
        private Transform focusedPoint;
        private float height, radius, arcAngle;

        //combined path
        private List<TrafficPath> combinedPaths;
        private int combinedPathsSize;

        //smoothing path
        private int smoothValue;

        private void OnEnable()
        {
            _target = (TrafficPath)target;
            so = serializedObject;

            so.Update();

            var nodesProp = so.FindProperty("nodes");

            while (nodesProp.arraySize < 2)
            {
                nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize == 0 ? 0 : nodesProp.arraySize - 1);
                nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1).vector3Value = _target.transform.position + Vector3.one;
            }

            //disable add multiple
            so.FindProperty("addMultiple").boolValue = false;

            so.ApplyModifiedProperties();


            labelStyle = new GUIStyle();
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 16;

            combinedPaths = new List<TrafficPath>();
        }

        public override void OnInspectorGUI()
        {
            so.Update();

            //show script name
            SerializedProperty currentProp = so.FindProperty("m_Script");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(currentProp);
            }


            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Path Properties:", EditorStyles.boldLabel);

            //show path name
            currentProp = so.FindProperty("pathName");
            EditorGUILayout.PropertyField(currentProp);

            /*
            //show path type
            currentProp = so.FindProperty("pathType");
            EditorGUILayout.PropertyField(currentProp);
            */

            //smart Traffic
            currentProp = so.FindProperty("smartTraffic");
            EditorGUILayout.PropertyField(currentProp);


            //show path speed
            EditorGUILayout.BeginHorizontal();
            currentProp = so.FindProperty("pathSpeedMPH");
            //EditorGUILayout.PropertyField(currentProp);
            //EditorGUILayout.LabelField(new GUIContent("Path Speed MPH"));
            EditorGUILayout.IntSlider(currentProp, 1, 100);
            EditorGUILayout.EndHorizontal();

            bool smart = so.FindProperty("smartTraffic").boolValue;
            //show baked resolution
            currentProp = so.FindProperty("bakedResolution");
            using (new EditorGUI.DisabledScope(!smart))
            {
                EditorGUILayout.PropertyField(currentProp);
            }
            if (!smart)
            {
                currentProp.intValue = so.FindProperty("pathSpeedMPH").intValue;
            }

            //split chance
            currentProp = so.FindProperty("splitChance");
            EditorGUILayout.PropertyField(currentProp, new GUIContent("Chance to split into this path"));

            
            //path type
            currentProp = so.FindProperty("type");
            EditorGUILayout.PropertyField(currentProp, new GUIContent("Type"), true);
            
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            //nodes
            currentProp = so.FindProperty("nodes");
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(currentProp, new GUIContent(""), false, GUILayout.MaxWidth(1));
            EditorGUILayout.LabelField("Nodes:", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();
            displayNodes = currentProp.isExpanded;
            if (currentProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < currentProp.arraySize; i++)
                {
                    GUILayout.BeginHorizontal();
                    if (i == 0)
                    {
                        EditorGUILayout.LabelField("Begin", GUILayout.MaxWidth(50));
                    }
                    else if (i == currentProp.arraySize - 1)
                    {
                        EditorGUILayout.LabelField("End", GUILayout.MaxWidth(50));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(i.ToString(), GUILayout.MaxWidth(50));
                    }
                    SerializedProperty nodeProp = currentProp.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(nodeProp, new GUIContent(""));

                    //delete button
                    if (GUILayout.Button(new GUIContent("X", "Delete this node"), GUILayout.MaxWidth(50)))
                    {
                        currentProp.MoveArrayElement(i, currentProp.arraySize - 1);
                        currentProp.arraySize -= 1;
                    }
                    GUILayout.EndHorizontal();
                }


                EditorGUI.indentLevel--;
            }

            //reverse nodes button
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            if (GUILayout.Button("Reverse Nodes"))
            {

                SerializedProperty nodesProp = so.FindProperty("nodes");
                List<Vector3> nodes = new List<Vector3>(nodesProp.arraySize);

                var iterator = nodesProp.GetEnumerator();
                while (iterator.MoveNext())
                {
                    nodes.Add(((SerializedProperty)iterator.Current).vector3Value);
                }

                iterator = nodesProp.GetEnumerator();
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    iterator.MoveNext();
                    ((SerializedProperty)iterator.Current).vector3Value = nodes[i];
                }


            }
            EditorGUILayout.EndVertical();

            //inspector only fields
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inspector Only variables:", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            displayNodes = EditorGUILayout.Toggle(new GUIContent("Show nodes"), displayNodes);
            if (EditorGUI.EndChangeCheck())
            {
                so.FindProperty("nodes").isExpanded = displayNodes;
            }
            currentProp = so.FindProperty("displayInEditor");
            EditorGUILayout.PropertyField(currentProp);
            currentProp = so.FindProperty("forceStraightLinePath");
            EditorGUILayout.PropertyField(currentProp);

            currentProp = so.FindProperty("lineColor");
            EditorGUILayout.PropertyField(currentProp);

            currentProp = so.FindProperty("addMultiple");
            EditorGUILayout.PropertyField(currentProp, new GUIContent("Add Multiple", "Enable to add multiple nodes between the last two nodes."));
            using (new EditorGUI.DisabledScope(!currentProp.boolValue))
            {
                currentProp = so.FindProperty("addMultipleCount");
                EditorGUILayout.PropertyField(currentProp);
            }

            //project button
            EditorGUILayout.Space();
            if (GUILayout.Button("Project Nodes Onto Mesh"))
            {
                projectNodesCount = ProjectNodes(so.FindProperty("nodes"));
                Debug.Log(projectNodesCount);
            }

            //project stat
            using (new EditorGUI.DisabledScope(true))
            {
                int arraySize = so.FindProperty("nodes").arraySize;
                string stat = (arraySize - projectNodesCount).ToString() + " out of " + arraySize.ToString();
                EditorGUILayout.TextField(new GUIContent("Last Projected Stat"), stat);
            }


            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            //extra tools
            #region extra tools
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            GUIStyle style = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            moreTools = EditorGUILayout.Foldout(moreTools, new GUIContent("Show More Cool Tools:"), true, style);
            if (moreTools)
            {
                //draw border tool
                EditorGUILayout.Space();
                drawBorder = EditorGUILayout.Toggle(new GUIContent("Draw Border:"), drawBorder);
                using (new EditorGUI.DisabledScope(!drawBorder))
                {
                    EditorGUIUtility.labelWidth = 70;
                    length = EditorGUILayout.FloatField(new GUIContent("Length:"), length, GUILayout.MaxWidth(150));
                    EditorGUIUtility.labelWidth = 0;
                }


                //remove multiple nodes tool
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Dangerous Territory!!!", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUIUtility.labelWidth = 50;
                from = EditorGUILayout.IntField(new GUIContent("From:"), from, GUILayout.MaxWidth(150));
                to = EditorGUILayout.IntField(new GUIContent("To:"), to, GUILayout.MaxWidth(150));
                EditorGUIUtility.labelWidth = 0;
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Remove Nodes"))
                {
                    int nodesToRemove = to - from;
                    if (EditorUtility.DisplayDialog("Remove nodes", "Removing " + nodesToRemove + " node(s)", "OK", "Cancel"))
                    {
                        var nodesProp = so.FindProperty("nodes");
                        for (int i = 0; i < nodesToRemove; i++)
                        {
                            nodesProp.MoveArrayElement(from, nodesProp.arraySize - 1);
                            nodesProp.arraySize -= 1;
                        }
                    }
                }
                EditorGUILayout.Space();

                //making circle tool
                EditorGUILayout.LabelField("Creating Circle Path", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("(Note: These values will not be saved!)");
                focusedPoint = (Transform)EditorGUILayout.ObjectField(new GUIContent("Center"), focusedPoint, typeof(Transform), true);
                EditorGUILayout.BeginHorizontal();
                EditorGUIUtility.labelWidth = 80;
                height = EditorGUILayout.FloatField(new GUIContent("Height:"), height, GUILayout.MaxWidth(150));
                radius = EditorGUILayout.FloatField(new GUIContent("Radius:"), radius, GUILayout.MaxWidth(150));
                EditorGUILayout.EndHorizontal();
                arcAngle = EditorGUILayout.FloatField(new GUIContent("Arc Angle:"), arcAngle, GUILayout.MaxWidth(200));
                EditorGUIUtility.labelWidth = 0;
                if (GUILayout.Button("Cyclize"))
                {
                    if (focusedPoint != null)
                    {
                        var nodesProp = so.FindProperty("nodes");
                        nodesProp.ClearArray();

                        List<Vector3> nodes = new List<Vector3>(5);

                        Vector3 rot = Vector3.zero;
                        focusedPoint.eulerAngles = rot;
                        //nodes.Add(focusedPoint.position + focusedPoint.forward * radius);
                        while (rot.y <= 360.0f)
                        {
                            focusedPoint.eulerAngles = rot;
                            nodes.Add(focusedPoint.position + focusedPoint.forward * radius);
                            rot.y += arcAngle;
                        }

                        Vector3 mid = Vector3.Lerp(nodes[0], nodes[nodes.Count - 1], 0.5f);
                        nodes[0] = mid;
                        nodes[nodes.Count - 1] = mid;

                        //Vector3 endNode = nodes[nodes.Count - 1];
                        Vector3 firstNode = mid + Vector3.back * (radius / 4.0f * arcAngle / 70.0f) + Vector3.left * (radius / 10.0f);
                        Vector3 lastNode = mid + Vector3.back * (radius / 4.0f * arcAngle / 70.0f) + Vector3.right * (radius / 10.0f);
                        /*
                        //first node
                        rot.y = 350.0f;
                        focusedPoint.eulerAngles = rot;
                        nodes.Insert(0, focusedPoint.position + focusedPoint.forward * (radius - arcAngle));
                        //last node
                        rot.y = 10.0f;
                        focusedPoint.eulerAngles = rot;
                        nodes.Add(focusedPoint.position + focusedPoint.forward * (radius - arcAngle));
                        */
                        nodes.Insert(0, firstNode);
                        nodes.Add(lastNode);

                        for (int i = 0; i < nodes.Count; i++)
                        {
                            nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize == 0 ? 0 : nodesProp.arraySize - 1);
                            nodesProp.GetArrayElementAtIndex(i).vector3Value = nodes[i] + new Vector3(0, height, 0);
                        }

                    }
                    else
                    {
                        Debug.LogError("Assign center transform");
                    }
                }
                EditorGUILayout.Space();

                //make new path tool
                EditorGUILayout.LabelField("Making New Path From This Path:", EditorStyles.boldLabel);
                if (GUILayout.Button("Make New Path"))
                {
                    var nodesProp = so.FindProperty("nodes");
                    GameObject go = new GameObject(_target.name + "-NewPath");
                    go.transform.SetParent(_target.transform.parent);
                    go.transform.position = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1).vector3Value + new Vector3(5, 5, 5);
                    var newPathSO = new SerializedObject(go.AddComponent<TrafficPath>());

                    newPathSO.Update();

                    //adding first 2 nodes
                    var newPathProp = newPathSO.FindProperty("nodes");
                    newPathProp.InsertArrayElementAtIndex(0);
                    newPathProp.InsertArrayElementAtIndex(1);

                    newPathProp.GetArrayElementAtIndex(0).vector3Value = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1).vector3Value;
                    newPathProp.GetArrayElementAtIndex(1).vector3Value = newPathProp.GetArrayElementAtIndex(0).vector3Value + new Vector3(5, 0, 5);

                    //setting path color
                    newPathProp = newPathSO.FindProperty("lineColor");
                    newPathProp.colorValue = so.FindProperty("lineColor").colorValue;


                    //setting path speed
                    newPathProp = newPathSO.FindProperty("pathSpeedMPH");
                    newPathProp.intValue = so.FindProperty("pathSpeedMPH").intValue;

                    Selection.activeGameObject = go;

                    newPathSO.ApplyModifiedProperties();

                }
                EditorGUILayout.Space();

                //combine path tool
                //make new path tool
                EditorGUILayout.LabelField("Combine path(s):", EditorStyles.boldLabel);

                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Size:", GUILayout.MaxWidth(100));
                EditorGUI.BeginChangeCheck();
                combinedPathsSize = EditorGUILayout.DelayedIntField(combinedPathsSize, GUILayout.MaxWidth(200));
                if (EditorGUI.EndChangeCheck())
                {
                    int newSize = combinedPathsSize - combinedPaths.Count;

                    while (newSize > 0)
                    {
                        combinedPaths.Add(null);
                        newSize--;
                    }

                    while (newSize < 0)
                    {
                        combinedPaths.RemoveAt(combinedPaths.Count - 1);
                        newSize++;
                    }

                }
                EditorGUILayout.EndHorizontal();

                //show combined paths
                EditorGUI.indentLevel++;

                for (int i = 0; i < combinedPaths.Count; i++)
                {
                    combinedPaths[i] = (TrafficPath)EditorGUILayout.ObjectField(new GUIContent(""), combinedPaths[i], typeof(TrafficPath), true);
                }

                EditorGUI.indentLevel--;

                EditorGUI.indentLevel--;

                if (GUILayout.Button("Combine"))
                {
                    var nodesProp = so.FindProperty("nodes");

                    for (int i = 0; i < combinedPaths.Count; i++)
                    {
                        if (combinedPaths[i] != null)
                        {
                            var combinedPathSO = new SerializedObject(combinedPaths[i]);
                            combinedPathSO.Update();

                            var combinedNodesProp = combinedPathSO.FindProperty("nodes");

                            for (int j = 1; j < combinedNodesProp.arraySize; j++)
                            {
                                nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize - 1);
                                nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1).vector3Value = combinedNodesProp.GetArrayElementAtIndex(j).vector3Value;
                            }

                            combinedPathSO.ApplyModifiedProperties();
                        }
                    }
                }

                //smoothing path
                smoothValue = EditorGUILayout.IntField(smoothValue);
                if (GUILayout.Button("Smooth Path"))
                {
                    float t = 1.0f / (smoothValue + 1);
                    List<Vector3> nodes = new List<Vector3>(_target.nodes.Count + (smoothValue * (_target.nodes.Count - 1)));
                    
                    for (int i=0; i<_target.nodes.Count - 1; i++)
                    {
                        nodes.Add(_target.nodes[i]);

                        for (int j=0; j<smoothValue; j++)
                        {
                            nodes.Add(Vector3.Lerp(_target.nodes[i], _target.nodes[i + 1], (j + 1) * t));
                        }
                    }

                    //insert extra element to array
                    var nodesProp = so.FindProperty("nodes");
                    var oldSize = nodesProp.arraySize;
                    for (int i=0; i<nodes.Count - oldSize; i++)
                    {
                        nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize - 1);
                    }

                    Debug.Log(nodesProp.arraySize);

                    //copy content
                    for (int i=0; i<nodes.Count; i++)
                    {
                        nodesProp.GetArrayElementAtIndex(i).vector3Value = nodes[i];
                    }

                }


            }





            EditorGUILayout.EndVertical();
            #endregion
            //end extra tools





            // instructions
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hotkeys:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("\n\n" +
                "Space: Invoke ProjectOntoMesh.\n\n" +
                "Double Left Mouse Click: Add new node at the end.\n\n" +
                "Left Mouse Drag: Adjust single node.\n\n" +
                "Control + Left Mouse Drag: Adjust all nodes.\n\n" +
                "Control + F: Move Scene camera to first node.\n\n" +
                "Control + X: Delete node at mouse position.\n\n" +
                "Alt + Left Mouse Click : Move Scene camera to selected node.\n\n" +
                ""
                , MessageType.None);


            //notes
            currentProp = so.FindProperty("notes");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Notes:", EditorStyles.boldLabel);
            currentProp.stringValue = EditorGUILayout.TextArea(currentProp.stringValue);



            so.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {

            //short cut to project nodes
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
            {
                if (e.control)
                {
                    drawBorder = !drawBorder;
                }
                else
                {
                    projectNodesCount = ProjectNodes(_target.nodes);
                }
            }

            //short cut to delete node
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.X && e.control)
            {
                int index = LocateNearestNode(_target.nodes, e.mousePosition);
                Undo.RecordObject(target, "DeleteNode");
                _target.nodes.RemoveAt(index);
            }

            //move scene camera to begin of node
            if (e.control && e.type == EventType.KeyDown && e.keyCode == KeyCode.F)
            {
                MoveSceneView(_target.nodes[0]);
            }

            //move scene camera to selected node
            if (e.alt && e.type == EventType.MouseDown && e.clickCount == 1)
            {
                Vector3 pos = _target.nodes[(LocateNearestNode(_target.nodes, e.mousePosition))];
                MoveSceneView(pos);
            }

            //double click to add
            if (e.type == EventType.MouseDown && e.clickCount > 1)
            {

                //check to see if add single or multiple nodes
                if (_target.addMultiple)
                {
                    Vector3 firstNode = _target.nodes[_target.nodes.Count - 2];
                    Vector3 secondNode = _target.nodes[_target.nodes.Count - 1];

                    float t = 0;
                    float step = 1.0f / _target.addMultipleCount;

                    List<Vector3> nodes = new List<Vector3>();
                    while (t < 1.0f)
                    {
                        t += step;
                        nodes.Add(Vector3.Lerp(firstNode, secondNode, t));
                    }
                    Undo.RecordObject(target, "AddNodesAll");
                    _target.nodes.RemoveAt(_target.nodes.Count - 1);
                    _target.nodes.AddRange(nodes);
                    _target.addMultiple = false;
                }
                else
                {
                    int index = LocateNearestNode(_target.nodes, e.mousePosition);
                    Undo.RecordObject(target, "AddNodeSingle");
                    _target.nodes.Insert(index, _target.nodes[index]);
                }

            }

            //draw nodes handle
            for (int i = 0; i < _target.nodes.Count; i++)
            {

                Vector3 currentPos = _target.nodes[i];

                //draw label
                if (i == 0)
                {
                    Handles.Label(currentPos, "Begin", labelStyle);
                }
                else if (i == _target.nodes.Count - 1)
                {
                    Handles.Label(currentPos, "End", labelStyle);
                }
                else
                {
                    Handles.Label(currentPos, i.ToString(), labelStyle);
                }

                //draw handle
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(currentPos, Quaternion.identity);
                if (drawBorder)
                {
                    Handles.color = Color.green;
                    Vector3 p0, p1, p2, p3;
                    p0 = newPos + Vector3.right * length;
                    p1 = newPos - Vector3.right * length;
                    p2 = newPos + Vector3.forward * length;
                    p3 = newPos - Vector3.forward * length;

                    Handles.DrawLine(p0, p3);
                    Handles.DrawLine(p3, p1);
                    Handles.DrawLine(p1, p2);
                    Handles.DrawLine(p2, p0);

                    /*
                    Handles.DrawLine(newPos, newPos + Vector3.right * length);
                    Handles.DrawLine(newPos, newPos - Vector3.right * length);
                    Handles.DrawLine(newPos, newPos + Vector3.forward * length);
                    Handles.DrawLine(newPos, newPos - Vector3.forward * length);
                    */
                }
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "MoveSingleNode");
                    if (e.control)
                    {
                        MoveAllNodes(_target.nodes, newPos - currentPos);
                    }
                    else
                    {
                        _target.nodes[i] = newPos;
                    }
                }

            }
        }

        private int ProjectNodes(SerializedProperty prop)
        {
            Debug.Log("Projecting nodes");
            var iterator = prop.GetEnumerator();
            int count = 0;
            while (iterator.MoveNext())
            {
                SerializedProperty nodeProp = (SerializedProperty)iterator.Current;

                Vector3 currentPos = nodeProp.vector3Value;
                RaycastHit hit;

                if (Physics.Raycast(currentPos + Vector3.up * 5.0f, Vector3.down, out hit, 10000f))
                {
                    //cast down
                    nodeProp.vector3Value = hit.point;
                }
                else if (Physics.Raycast(currentPos + Vector3.down * 5.0f, Vector3.up, out hit, 10000f))
                {
                    nodeProp.vector3Value = hit.point;
                }
                else
                {
                    count++;
                    Debug.Log("Not Hit");
                }
            }
            return count;
        }

        private int ProjectNodes(List<Vector3> nodes)
        {
            Undo.RecordObject(target, "ProjectNodes");
            Vector3 currentPos;
            RaycastHit hit;
            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                currentPos = nodes[i];
                if (Physics.Raycast(currentPos + Vector3.up, Vector3.down, out hit, 10000f))
                {
                    //cast down
                    nodes[i] = hit.point;
                }
                else if (Physics.Raycast(currentPos + Vector3.down, Vector3.up, out hit, 10000f))
                {
                    nodes[i] = hit.point;
                }
                else
                {
                    count++;
                    Debug.Log("Not Hit");
                }
            }
            return count;
        }

        private void MoveAllNodes(List<Vector3> nodes, Vector3 delta)
        {
            Undo.RecordObject(target, "MoveAllNodes");
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i] += delta;
            }
        }

        private int LocateNearestNode(SerializedProperty prop, Vector2 mousePos)
        {
            int index = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < prop.arraySize; i++)
            {
                var nodeToGUI = HandleUtility.WorldToGUIPoint(prop.GetArrayElementAtIndex(i).vector3Value);
                var dis = Vector2.Distance(nodeToGUI, mousePos);
                if (dis < minDistance)
                {
                    minDistance = dis;
                    index = i;
                }
            }
            return index;
        }

        private int LocateNearestNode(List<Vector3> nodes, Vector2 mousePos)
        {
            int index = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                var nodeToGUI = HandleUtility.WorldToGUIPoint(nodes[i]);
                var dis = Vector2.Distance(nodeToGUI, mousePos);
                if (dis < minDistance)
                {
                    minDistance = dis;
                    index = i;
                }
            }
            return index;
        }


        private void MoveSceneView(Vector3 pos)
        {
            var view = SceneView.currentDrawingSceneView;
            if (view != null)
            {
                var target = new GameObject();
                target.transform.position = pos + new Vector3(1, 1, 1);
                target.transform.LookAt(pos);
                view.AlignViewToObject(target.transform);
                GameObject.DestroyImmediate(target);
            }
        }

    }
}