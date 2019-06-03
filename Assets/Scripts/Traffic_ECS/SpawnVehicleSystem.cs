﻿/*
 * This system handles:
 *      Spawning the body part of vehicles
 *      Spawning the wheel parts of vehicles
 *      Assemble them together
 * This system stops when those 3 jobs are done
*/

using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;

namespace CivilFX.TrafficECS
{

    enum SystemStatus
    {
        Preparing,
        Spawned,
        Done
    }

    [AlwaysUpdateSystem]
    [UpdateBefore(typeof(TrafficSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class SpawnVehicleSystem : JobComponentSystem
    {

        BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;
        //static int x, y;
        //private static float currentTime;
        JobHandle spawnVehicleBodyJob;
        JobHandle spawnVehicleWheelJob;

        bool isSpawned;

        SystemStatus status = SystemStatus.Preparing;

        protected override void OnCreate()
        {
            //Debug.Log("On Create");
            m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        struct SpawnVehicleBodyJob : IJobForEachWithEntity<VehicleBody>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            public EntityArchetype bodyType;
            public void Execute(Entity entity, int index, [ReadOnly] ref VehicleBody body)
            {
                var instance = commandBuffer.Instantiate(index, body.prefab);

                commandBuffer.AddComponent(index, instance, new VehicleBodyIDAndSpeed { speed = 0, id = body.id });
                commandBuffer.AddComponent(index, instance, new VehicleBodyRawPosition { });
                commandBuffer.AddComponent(index, instance, new VehicleBodyIndexPosition { });
                commandBuffer.AddComponent(index, instance, new VehicleBodyPathID { value = TrafficSystem.BYTE_INVALID });
                commandBuffer.AddComponent(index, instance, new VehicleBodyLength { value = body.length });
                commandBuffer.AddComponent(index, instance, new VehicleBodySplittingPath { linkedPathID = TrafficSystem.BYTE_INVALID });
                commandBuffer.AddComponent(index, instance, new VehicleBodyWaitingStatus { });
                commandBuffer.DestroyEntity(index, entity);
            }
        }

        struct SpawnVehicleWheelJob : IJobForEachWithEntity<VehicleWheel>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity entity, int index, [ReadOnly] ref VehicleWheel wheel)
            {
                var instance = commandBuffer.Instantiate(index, wheel.prefab);
                commandBuffer.AddComponent(index, instance, new VehicleWheelMoveAndRotate {id = wheel.id });
                commandBuffer.DestroyEntity(index, entity);
            }
        }

        struct AssembleVehicleJob : IJobParallelFor
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            [ReadOnly] public NativeArray<VehicleBodyIDAndSpeed> bodyComponents;
            [ReadOnly] public NativeArray<VehicleWheelMoveAndRotate> wheelComponents;
            [ReadOnly] public NativeArray<Entity> bodyEntites;
            [ReadOnly] public NativeArray<Entity> wheelEntites;
           
            public void Execute(int index)
            {
                Entity body = Entity.Null;
                //find body
                for (int i=0; i< bodyComponents.Length; i++)
                {
                    if (bodyComponents[i].id == wheelComponents[index].id)
                    {
                        body = bodyEntites[i];
                        break;
                    }
                }
                if (body.Equals(Entity.Null))
                {
                    Debug.LogError("Failed to assemble vehicle");
                    return;
                }

                //set components
                commandBuffer.AddComponent(index, wheelEntites[index], new Parent { Value = body });
                commandBuffer.AddComponent(index, wheelEntites[index], new LocalToParent { });
            }

        }
        

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (status == SystemStatus.Done)
            {
                return inputDeps;
            } else

            if (status == SystemStatus.Preparing)
            {
                spawnVehicleBodyJob = new SpawnVehicleBodyJob
                {
                    commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
                }.Schedule(this, inputDeps);

                spawnVehicleWheelJob = new SpawnVehicleWheelJob
                {
                    commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
                }.Schedule(this, inputDeps);

                m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnVehicleBodyJob);
                m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnVehicleWheelJob);

                status = SystemStatus.Spawned;
                return JobHandle.CombineDependencies(spawnVehicleBodyJob, spawnVehicleWheelJob);
            } else if (status == SystemStatus.Spawned && spawnVehicleBodyJob.IsCompleted && spawnVehicleWheelJob.IsCompleted)
            {
                //both job completed
                var queries = GetEntityQuery(ComponentType.ReadOnly(typeof(VehicleBodyIDAndSpeed)));
                var _bodyComponents = queries.ToComponentDataArray<VehicleBodyIDAndSpeed>(Allocator.TempJob);
                var _bodyEntities = queries.ToEntityArray(Allocator.TempJob);
                queries = GetEntityQuery(typeof(VehicleWheelMoveAndRotate));
                var _wheelComponents = queries.ToComponentDataArray<VehicleWheelMoveAndRotate>(Allocator.TempJob);
                var _wheelEntities = queries.ToEntityArray(Allocator.TempJob);

                var assembleJob = new AssembleVehicleJob
                {
                    commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                    bodyComponents = _bodyComponents,
                    bodyEntites = _bodyEntities,
                    wheelComponents = _wheelComponents,
                    wheelEntites = _wheelEntities

                }.Schedule(_wheelComponents.Length, 64);
                assembleJob.Complete();
                _bodyComponents.Dispose();
                _bodyEntities.Dispose();
                _wheelComponents.Dispose();
                _wheelEntities.Dispose();
                status = SystemStatus.Done;
                Enabled = false; //disable this system at this point
                return assembleJob;
            }

            //no reach
            return inputDeps;
        }

        protected override void OnDestroy()
        {
            Debug.Log("SpawnVehicleSystem Destroyed");
        }

    }
}