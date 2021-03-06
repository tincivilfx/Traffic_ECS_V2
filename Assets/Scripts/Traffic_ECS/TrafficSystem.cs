﻿using System.Collections;
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

            //calculate location for all vehices to all paths
            ulong totalNodes = 0;
            List<float> nodesPercentage = new List<float>();
            List<int> vehiclesCounts = new List<int>();
            int currentVehicleCount = 0;

            NativeHashMap<int, VehicleInitData> hashMap = new NativeHashMap<int, VehicleInitData>(1000, Allocator.TempJob);

            //get the total of nodes of all paths
            for (int i=0; i<paths.Length; i++)
            {
                totalNodes += (ulong)paths[i].nodesCount;
            }

            //get the percentage of vehicles on a single path
            for (int i= 0; i<paths.Length; i++)
            {
                nodesPercentage.Add(paths[i].nodesCount * 100.0f / totalNodes);
            }

            //get the vehicles
            var vehiclesQuery = GetEntityQuery(ComponentType.ReadWrite< VehicleBodyMoveAndRotate >());
            var vehicles = vehiclesQuery.ToComponentDataArray<VehicleBodyMoveAndRotate>(Allocator.TempJob);

            //calculate the actual number of vehicles on a single path base on the percentage
            for (int i = 0; i < paths.Length; i++)
            {
                vehiclesCounts.Add(Mathf.CeilToInt(nodesPercentage[i] * vehicles.Length / 100.0f));
            }
            
            //assign vehicle to each path along with the position
            for (int i = 0; i < paths.Length; i++)
            {
                Debug.Log(vehiclesCounts[i]);
                for (int j=0; j<vehiclesCounts[i]; j++)
                {
                    if (currentVehicleCount >= vehicles.Length)
                    {
                        break;
                    }
                    var v = vehicles[currentVehicleCount];
                    v.currentPathID = paths[i].id;
                    v.currentPos = j * (paths[i].nodesCount / vehiclesCounts[i]);
                    hashMap.TryAdd(v.id, new VehicleInitData { pathID = paths[i].id, pos = v.currentPos });
                    currentVehicleCount++;
                }
            }
            vehicles.Dispose();
            
            //schedule job to populate vehicles to paths
            JobHandle job = new OnetimePopulateVehicleToPathJob
            {
                map = hashMap,
            }.Schedule(this, inputDeps);

            job.Complete();
            hashMap.Dispose();
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