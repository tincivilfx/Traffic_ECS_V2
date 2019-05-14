using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace CivilFX.TrafficECS {

    public class TestPathSystem : JobComponentSystem
    {
        bool isDone = false;

        struct DebugJob : IJobForEach<Path>
        {
            public unsafe void Execute(ref Path c0)
            {
                //Debug.Log(c0.pathNodes[0]);
                //Debug.Log(c0.pathNodes[c0.nodesCount-1]);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            
            //Debug.Log("Test System Update");
            if (!isDone)
            {
                //isDone = true;
                DebugJob job = new DebugJob();
                return job.ScheduleSingle(this, inputDeps);
            }
            
            return inputDeps;
        }
    }
}