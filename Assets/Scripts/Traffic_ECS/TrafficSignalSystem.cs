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
    public class TrafficSignalSystem : JobComponentSystem
    {

        private EntityQuery signalEntities;

        private NativeArray<Path> paths;

        protected override void OnCreate()
        {
            signalEntities = GetEntityQuery(ComponentType.ReadOnly<TrafficSignalNode>());
            var pathEntities = GetEntityQuery(ComponentType.ReadOnly(typeof(Path)));
            paths = pathEntities.ToComponentDataArray<Path>(Allocator.Persistent);
        }

        [BurstCompile]
        public struct SignalControllJob : IJobChunk
        {
            public float deltaTime;
            public ArchetypeChunkComponentType<TrafficSignalNode> signalNodeType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunksignalNode = chunk.GetNativeArray(signalNodeType);
                for (int i=0; i<chunk.Count; i++)
                {
                    var signalNode = chunksignalNode[i];
                    
                }
            }
        }



        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }

        protected override void OnDestroy()
        {
            paths.Dispose();
        }
    }
}