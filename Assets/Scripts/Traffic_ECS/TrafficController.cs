using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;


namespace CivilFX.TrafficECS
{
    //Fixed Timestep workaround
    public class TrafficController : MonoBehaviour
    {

        // NOTE: Updating a manually-created system in FixedUpdate() as demonstrated below
        // is intended as a short-term workaround; the entire `SimulationSystemGroup` will
        // eventually use a fixed timestep by default.

        private TrafficSystem trafficSystem;

        // Start is called before the first frame update



        void Start()
        {

        }

        // Update is called once per frame
        private void FixedUpdate()
        {
            if (trafficSystem == null)
            {
                trafficSystem = World.Active.GetOrCreateSystem<TrafficSystem>();
            }
            trafficSystem.Update();
        }
    }
}