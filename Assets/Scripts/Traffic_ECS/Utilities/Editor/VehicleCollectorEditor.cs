using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CivilFX.TrafficECS
{
    [CustomEditor(typeof(VehicleCollector))]
    public class VehicleCollectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Load All"))
            {
                var objs = Resources.LoadAll<VehicleObject>("/Assets");
                Debug.Log(objs.Length);
                
            }
        }
    }
}