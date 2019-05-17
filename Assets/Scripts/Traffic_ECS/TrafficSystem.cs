using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace CivilFX.TrafficECS
{
    [UpdateAfter(typeof(SpawnVehicleSystem))]
    [AlwaysUpdateSystem]
    public partial class TrafficSystem : JobComponentSystem
    {

        [NativeDisableUnsafePtrRestriction]
        private NativeArray<Path> paths;
        
        private bool isDoneSetup;
        BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

        private float delayForStability = 1.0f;
        private int framesToSkip = 1;

        protected override void OnCreateManager()
        {
            m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        private JobHandle OneTimeSetup(JobHandle inputDeps)
        {
            //cache references to paths data
            var queries = GetEntityQuery(ComponentType.ReadOnly(typeof(Path)));
            paths = queries.ToComponentDataArray<Path>(Allocator.Persistent);




            //schedule job to populate vehicles to paths
            JobHandle job = new OnetimePopulateVehicleToPathJob
            {
                path = paths[0],
            }.Schedule(this, inputDeps);

            return job;
        }



        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //skip to next frame to make sure all the vehicles are initialized
            if (framesToSkip > 0)
            {
                framesToSkip--;
                return inputDeps;
            }

            if (!isDoneSetup)
            {
                isDoneSetup = true;
                return OneTimeSetup(inputDeps);
            }

            //schedule resolve next path
            JobHandle resolveNodeJob = new GetVehicleBodyPositionJob
            {
                commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                paths = this.paths,
            }.Schedule(this, inputDeps);

            //schedule clear paths occupancy
            JobHandle clearPathsJob = new ClearPathsOccupancyJob
            {

            }.Schedule(this, resolveNodeJob);

            NativeMultiHashMap<int, VehiclePosition> hashMap = new NativeMultiHashMap<int, VehiclePosition>(10000, Allocator.TempJob);

            //schedule move vehicle job
            JobHandle moveVehicleJob = new MoveVehicleBodyJob
            {
                commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                map = hashMap.ToConcurrent(),
            }.Schedule(this, clearPathsJob);
            moveVehicleJob.Complete();
            NativeArray<VehicleBodyMoveAndRotate> vehicleBodies = GetEntityQuery(ComponentType.ReadOnly(typeof(VehicleBodyMoveAndRotate))).ToComponentDataArray<VehicleBodyMoveAndRotate>(Allocator.TempJob, out moveVehicleJob);

            //schedule move wheel job
            JobHandle moveVehicleWheelJob = new MoveVehicleWheelJob
            {
                deltaTime = Time.deltaTime,
                commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                bodies = vehicleBodies,
            }.Schedule(this, moveVehicleJob);

            //Schedule filljob
            JobHandle fillPathOccupancy = new FillPathsOccupancyJob
            {
                map = hashMap,
            }.Schedule(this, moveVehicleJob);

            fillPathOccupancy.Complete();
            moveVehicleWheelJob.Complete();
            hashMap.Dispose();
            vehicleBodies.Dispose();


            return fillPathOccupancy;
        }

        protected override void OnDestroyManager()
        {
            paths.Dispose();
        }

    }
}