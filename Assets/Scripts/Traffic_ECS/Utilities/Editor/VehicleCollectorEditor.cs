using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    [CustomEditor(typeof(VehicleCollector))]
    public class VehicleCollectorEditor : Editor
    {
        private SerializedObject so;
        public void OnEnable()
        {
            so = serializedObject;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            so.Update();
            if (GUILayout.Button("Load All"))
            {
                int currentIndex = 0;
                var sp = so.FindProperty("vehicles");
                sp.ClearArray();          
                string[] guids = AssetDatabase.FindAssets("t:" + typeof(VehicleObject).Name);
                sp.arraySize = guids.Length;
                foreach (var s in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(s);
                    var asset = AssetDatabase.LoadAssetAtPath<VehicleObject>(assetPath);
                    sp.GetArrayElementAtIndex(currentIndex++).objectReferenceValue = asset;
                }
            }
            so.ApplyModifiedProperties();
        }
    }
}