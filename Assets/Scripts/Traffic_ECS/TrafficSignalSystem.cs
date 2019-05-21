using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Transforms;
using Unity.Burst;


namespace CivilFX.TrafficECS
{
    [UpdateAfter(typeof(TrafficSettings))]
    public class TrafficSignalSystem : JobComponentSystem
    {

        private EntityQuery signalEntities;

        private NativeArray<Path> paths;

        protected override void OnCreate()
        {
            signalEntities = GetEntityQuery(ComponentType.ReadOnly<TrafficSignalNode>());
            var pathEntities = GetEntityQuery(typeof(Path));
            paths = pathEntities.ToComponentDataArray<Path>(Allocator.Persistent);
        }

        //[BurstCompile]
        public struct SignalControllJob : IJobChunk
        {
            public float deltaTime;
            public ArchetypeChunkComponentType<TrafficSignalNode> signalNodeType;
            public NativeArray<Path> paths;
            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                /*
                var chunksignalNode = chunk.GetNativeArray(signalNodeType);
                for (int i=0; i<chunk.Count; i++)
                {
                    var signalNode = chunksignalNode[i];
                    for (int j=0; j<signalNode.setsCount; j++)
                    {
                        for (int k = 0; k < signalNode.sets[j].pathsCount; k++)
                        {                        
                            for (int l = 0; l < paths.Length; l++)
                            {
                                var path = Path.Null;
                                if (signalNode.sets[j].pathIDs[k] == paths[l].id)
                                {
                                    path = paths[l];
                                    var lvalue = path.occupied[signalNode.sets[j].stopPoses[k]];
                                    lvalue = TrafficSystem.SetOccupied(lvalue, true, TrafficSystem.OccupiedType.TrafficSignal);
                                    path.occupied[signalNode.sets[j].stopPoses[k]] = lvalue;
                                }
                            }
                        }
                    }

                }
                */
                var chunksignalNode = chunk.GetNativeArray(signalNodeType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var signalNode = chunksignalNode[i];
                    signalNode.currentTime -= deltaTime;
                    if (signalNode.currentTime <= 0)
                    {
                       
                    }
                }
            }
        }


        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (paths.Length == 0)
            {
                paths.Dispose();
                var pathEntities = GetEntityQuery(typeof(Path));
                paths = pathEntities.ToComponentDataArray<Path>(Allocator.Persistent);
                return inputDeps;
            }

            var signalType = GetArchetypeChunkComponentType<TrafficSignalNode>(false);
            var signalJob = new SignalControllJob
            {
                deltaTime = Time.deltaTime,
                signalNodeType = signalType,
                paths = paths
            }.Schedule(signalEntities, inputDeps);
            return signalJob;
        }

        protected override void OnDestroy()
        {
            paths.Dispose();
        }
    }
}