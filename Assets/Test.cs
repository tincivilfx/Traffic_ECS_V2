using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace CivilFX.TrafficECS {
    public class Test : MonoBehaviour
    {


        // Start is called before the first frame update
        void Start()
        {
            float f = 1.0f / 8;
            int i = (int)math.floor(f);
            float frac = math.frac(f);
            Debug.Log(i);
            Debug.Log(frac);


            var go1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go1.name = "GO1";
            go1.transform.position = new Vector3(10, 10, 10);

            var go2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go2.name = "GO2";
            go2.transform.position = new Vector3(5, 10, 5);

            var go00 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go00.name = "GO00";
            go00.transform.position = go1.transform.position - go2.transform.position;

            var go0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go0.name = "GO0";
            go0.transform.position = go1.transform.position + (go1.transform.position - go2.transform.position);


        }

        // Update is called once per frame
        
    }
}