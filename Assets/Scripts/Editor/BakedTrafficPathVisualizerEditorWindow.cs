using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CivilFX.TrafficECS {

    public class BakedTrafficPathVisualizerEditorWindow : EditorWindow
    {
        public enum VisualizedType
        {
            SingleNode,
            Length
        }


        private BakedTrafficPath path = null;
        private VisualizedType type;
        private int startNode;
        private int endNode;
        private int node;

        [MenuItem("Window/CivilFX/Path Visualizer")]
        public static BakedTrafficPathVisualizerEditorWindow OpenEditorWindow()
        {
            return EditorWindow.GetWindow<BakedTrafficPathVisualizerEditorWindow>("Path Visualizer");
        }

        [UnityEditor.Callbacks.OnOpenAsset(1)]
        public static bool OnOpenDatabase(int instanceID, int line)
        {
            var _path = EditorUtility.InstanceIDToObject(instanceID) as BakedTrafficPath;
            if (_path != null)
            {
                var editorWindow  = OpenEditorWindow();
                editorWindow.path = _path;
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (path == null)
            {
                return;
            }

        }

        
    }
}