using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CivilFX.TrafficECS {
    public class Test : MonoBehaviour
    {
        public Transform body;
        public Transform[] wheels;
        public BakedTrafficPath path;

        private int currentPos;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (currentPos < path.PathNodes.Count - 1)
            {
                body.position = path.PathNodes[currentPos];
                body.LookAt(path.PathNodes[currentPos + 1]);

                for (int i=0; i<wheels.Length; i++)
                {
                    wheels[i].position = path.PathNodes[currentPos];
                }


            }
        }
    }
}