using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Transforms;

namespace CivilFX.TrafficECS
{
    [UpdateAfter(typeof(SpawnVehicleSystem))]
    [AlwaysUpdateSystem]
    public partial class TrafficSystem : JobComponentSystem
    {

        private NativeArray<Path> paths;
        
        private bool isDoneSetup;
        BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

        private float delayForStability = 1.0f;
        private int framesToSkip = 1;

        private Transform camTrans;

        #region caches entities
        EntityQuery pathEntities;
        EntityQuery vehicleBodidesEntities;
        EntityQuery vehicleWheelsEntities;
        #endregion

        protected override void OnCreate()
        {
            m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        private JobHandle OneTimeSetup(JobHandle inputDeps)
        {
            camTrans = Camera.main.transform;
            //Debug.Log(cam.gameObject.name);
            //cache references to paths data
            pathEntities = GetEntityQuery(ComponentType.ReadOnly(typeof(Path)));
            paths = pathEntities.ToComponentDataArray<Path>(Allocator.Persistent);

            //cache entities
            vehicleBodidesEntities = GetEntityQuery(typeof(VehicleBodyMoveAndRotate));
            vehicleWheelsEntities = GetEntityQuery(typeof(VehicleWheelMoveAndRotate));


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
            var vehicles = vehicleBodidesEntities.ToComponentDataArray<VehicleBodyMoveAndRotate>(Allocator.TempJob);

            //calculate the actual number of vehicles on a single path base on the percentage
            for (int i = 0; i < paths.Length; i++)
            {
                vehiclesCounts.Add(Mathf.CeilToInt(nodesPercentage[i] * vehicles.Length / 100.0f));
            }
            
            //assign vehicle to each path along with the position
            for (int i = 0; i < paths.Length; i++)
            {
                //Debug.Log(vehiclesCounts[i]);
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

            var bodyType = GetArchetypeChunkComponentType<VehicleBodyMoveAndRotate>(false);
            //schedule job to populate vehicles to paths
            JobHandle job = new OnetimePopulateVehicleToPathJob
            {
                map = hashMap,
                vehicleBodyType = bodyType
            }.Schedule(vehicleBodidesEntities, inputDeps);

            job.Complete();
            hashMap.Dispose();
            return job;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //skip to next frames to make sure all the vehicles are initialized
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

            //job to check for merging
            NativeArray<VehicleBodyMoveAndRotate> vehicleBodies = GetEntityQuery(ComponentType.ReadOnly(typeof(VehicleBodyMoveAndRotate))).ToComponentDataArray<VehicleBodyMoveAndRotate>(Allocator.TempJob, out JobHandle job);
            //job to get the next position in the path
            var bodyType = GetArchetypeChunkComponentType<VehicleBodyMoveAndRotate>(false);
            JobHandle resolveNextNodeJob = new ResolveNextPositionForVehicleJob
            {
                vehicleBodyType = bodyType,
                paths = paths,
            }.Schedule(vehicleBodidesEntities, JobHandle.CombineDependencies(job, inputDeps));

            //***********************************************


            //job to clear paths occupancy
            //only clear vehicle mode
            var pathType = GetArchetypeChunkComponentType<Path>(false);
            JobHandle clearPathsJob = new ClearPathsOccupancyJob
            {
                pathType = pathType
            }.Schedule(pathEntities, resolveNextNodeJob);

            //***********************************************

            //job to move vehicle job
            //also fill the hashmap
            NativeMultiHashMap<int, VehiclePosition> hashMap = new NativeMultiHashMap<int, VehiclePosition>(10000, Allocator.TempJob);   
            var rotationType = GetArchetypeChunkComponentType<Rotation>(false);
            var translationType = GetArchetypeChunkComponentType<Translation>(false);
            JobHandle moveVehicleJob = new MoveVehicleBodyJob
            {
                map = hashMap.ToConcurrent(),
                vehicleRotationType = rotationType,
                vehicletranslateType = translationType,
                vehicleBodyType = bodyType
            }.Schedule(vehicleBodidesEntities, clearPathsJob);
            //moveVehicleJob.Complete();

            
            //schedule move wheel job
            var wheelType = GetArchetypeChunkComponentType<VehicleWheelMoveAndRotate>(false);
            var wheelLocationType = GetArchetypeChunkComponentType<LocalToWorld>(false);
            JobHandle moveVehicleWheelJob = new MoveVehicleWheelJob
            {
                cameraPosition = camTrans.position,
                deltaTime = Time.deltaTime,
                bodies = vehicleBodies,
                wheelRotationType = rotationType,
                vehicleWheelType = wheelType,
                wheelLocationType = wheelLocationType,
            }.Schedule(vehicleWheelsEntities, moveVehicleJob);
            

            //Schedule filljob
            JobHandle fillPathOccupancy = new FillPathsOccupancyJob
            {
                map = hashMap,
                pathType = pathType
            }.Schedule(pathEntities, moveVehicleJob);

            fillPathOccupancy.Complete();
            hashMap.Dispose();


            return JobHandle.CombineDependencies( fillPathOccupancy, moveVehicleWheelJob);
        }

        protected override void OnDestroyManager()
        {
            paths.Dispose();
        }

    }
}