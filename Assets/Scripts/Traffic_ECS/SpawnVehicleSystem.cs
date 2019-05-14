/*
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

            public void Execute(Entity entity, int index, [ReadOnly] ref VehicleBody body)
            {
                //Debug.Log("Spawnning Body");
                var instance = commandBuffer.Instantiate(index, body.prefab);
                var position = new float3(0, 0, 0);
                //Debug.Log(position);
                commandBuffer.SetComponent(index, instance, new Translation { Value = position });
                commandBuffer.AddComponent(index, instance, new VehicleBodyMoveAndRotate {speed = 0, length = body.length, id = body.id });
                commandBuffer.DestroyEntity(index, entity);
            }
        }

        struct SpawnVehicleWheelJob : IJobForEachWithEntity<VehicleWheel>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;

            public void Execute(Entity entity, int index, [ReadOnly] ref VehicleWheel wheel)
            {
                //Debug.Log("Spawnning Wheel");
                var instance = commandBuffer.Instantiate(index, wheel.prefab);
                var position = new float3(0, 0, 0) + wheel.positionOffset;
                //Debug.Log(position);
                commandBuffer.SetComponent(index, instance, new Translation { Value = position });
                commandBuffer.AddComponent(index, instance, new VehicleWheelMoveAndRotate {id = wheel.id, positionOffset = wheel.positionOffset, rotationOffset = wheel.rotationOffset });
                commandBuffer.DestroyEntity(index, entity);
            }
        }

        struct AssembleVehicleJob : IJobParallelFor
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            public NativeArray<VehicleBodyMoveAndRotate> bodyComponents;
            public NativeArray<VehicleWheelMoveAndRotate> wheelComponents;
            public NativeArray<Entity> bodyEntites;
            public NativeArray<Entity> wheelEntites;
            /*
            public void Execute()
            {

                for (int i=0; i<bodyEntites.Length; i++)
                {
                    Entity e = bodyEntites[i];

                    for (int j=0; j< wheelComponents.Length; j++)
                    {
                        var wc = wheelComponents[j];

                        if (wc.id == bodyComponents[i].id)
                        { 
                            wc.parent = e;
                            commandBuffer.SetComponent(wheelEntites[j], wc);
                            commandBuffer.AddComponent(wheelEntites[j], new Parent { Value = e });
                            commandBuffer.AddComponent(wheelEntites[j], new LocalToParent { });
                        }
                    }
                }
            }
            */
            public void Execute(int index)
            {
                Entity body = Entity.Null;
                //find body
                for (int i=0; i<bodyEntites.Length; i++)
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
                }.ScheduleSingle(this, inputDeps);

                spawnVehicleWheelJob = new SpawnVehicleWheelJob
                {
                    commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
                }.ScheduleSingle(this, inputDeps);

                m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnVehicleBodyJob);
                m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnVehicleWheelJob);

                status = SystemStatus.Spawned;
                return JobHandle.CombineDependencies(spawnVehicleBodyJob, spawnVehicleWheelJob);
            } else if (status == SystemStatus.Spawned && spawnVehicleBodyJob.IsCompleted && spawnVehicleWheelJob.IsCompleted)
            {
                //both job completed
                var queries = GetEntityQuery(ComponentType.ReadOnly(typeof(VehicleBodyMoveAndRotate)));
                var _bodyComponents = queries.ToComponentDataArray<VehicleBodyMoveAndRotate>(Allocator.TempJob);
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