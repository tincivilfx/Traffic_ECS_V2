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
    [UpdateAfter(typeof(TrafficSystem))]
    public class TrafficSignalSystem : JobComponentSystem
    {

        private EntityQuery signalEntities;

        private NativeArray<Path> paths;

        private bool isInit;

        protected override void OnCreate()
        {
            signalEntities = GetEntityQuery(ComponentType.ReadOnly<TrafficSignalNode>());
            var pathEntities = GetEntityQuery(typeof(Path));
            paths = pathEntities.ToComponentDataArray<Path>(Allocator.Persistent);
        }

        //one time job to enable stop cell on every path
        [BurstCompile]
        public unsafe struct SignalControlInitJob : IJobChunk
        {
            public ArchetypeChunkComponentType<TrafficSignalNode> signalNodeType;
            public NativeArray<Path> paths;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunksignalNode = chunk.GetNativeArray(signalNodeType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var signalNode = chunksignalNode[i];
                    for (int j = 0; j < signalNode.setsCount; j++)
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
            }
        }

        [BurstCompile]
        public struct SignalControllJob : IJobChunk
        {
            public float deltaTime;
            public ArchetypeChunkComponentType<TrafficSignalNode> signalNodeType;
            public NativeArray<Path> paths;
            public unsafe void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {               
                var chunksignalNode = chunk.GetNativeArray(signalNodeType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var signalNode = chunksignalNode[i];
                    signalNode.currentTime = signalNode.currentTime - deltaTime;
                    //Debug.Log(signalNode.currentTime);
                    if (signalNode.currentTime <= 0)
                    {
                        //get current frame in the sequence
                        var frame = signalNode.sequence[signalNode.currentFrameIndex];

                        //get the set based on the frame ID
                        var set = signalNode.sets[frame.setID];

                        //handle this frame
                        for (int j=0; j<set.pathsCount; j++)
                        {
                            for (int k=0; k<paths.Length; k++)
                            {
                                if (paths[k].id == set.pathIDs[j] && paths[k].type == frame.type)
                                {                                 
                                    //set occupied
                                    var lvalue = paths[k].occupied[set.stopPoses[j]];
                                    lvalue = TrafficSystem.SetOccupied(lvalue, !frame.active, TrafficSystem.OccupiedType.TrafficSignal);
                                    paths[k].occupied[set.stopPoses[j]] = lvalue;
                                    break;
                                }
                            }
                        }

                        //set
                        signalNode.elapsedTime = frame.time;
                        signalNode.currentFrameIndex = (byte)(signalNode.currentFrameIndex + 1);
                        if (signalNode.currentFrameIndex < signalNode.sequenceCount)
                        {
                            signalNode.currentTime = signalNode.sequence[signalNode.currentFrameIndex].time - signalNode.elapsedTime;
                        } else
                        {
                            signalNode.currentFrameIndex = 0;
                            signalNode.elapsedTime = signalNode.sequence[0].time;
                            signalNode.currentTime = signalNode.elapsedTime;
                        }
                    }
                    chunksignalNode[i] = signalNode;
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

            if (!isInit)
            {

                var signalOneTimeJob = new SignalControlInitJob
                {
                    signalNodeType = signalType,
                    paths = paths
                }.Schedule(signalEntities, inputDeps);

                isInit = true;

                return signalOneTimeJob;
            }

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