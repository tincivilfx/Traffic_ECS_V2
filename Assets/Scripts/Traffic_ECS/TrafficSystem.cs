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
    [DisableAutoCreation] //fixed timestep workaround
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
        EntityQuery pathMergingEntities;
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
            pathMergingEntities = GetEntityQuery(ComponentType.ReadOnly(typeof(PathMerge)));

            //cache entities
            //vehicleBodidesEntities = GetEntityQuery(typeof(VehicleBodyMoveAndRotate));
            vehicleBodidesEntities = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] {
                    typeof(VehicleBodyIDAndSpeed),
                    typeof(VehicleBodyRawPosition),
                    typeof(VehicleBodyIndexPosition),
                    typeof(VehicleBodyPathID),
                    typeof(VehicleBodyLength),
                    typeof(VehicleBodyWaitingStatus)
                }
            });
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
            var vehiclesBodies = vehicleBodidesEntities.ToComponentDataArray<VehicleBodyIDAndSpeed>(Allocator.TempJob);
            var vehiclesIndexPosition = vehicleBodidesEntities.ToComponentDataArray<VehicleBodyIndexPosition>(Allocator.TempJob);
            var vehiclesPathID = vehicleBodidesEntities.ToComponentDataArray<VehicleBodyPathID>(Allocator.TempJob);

            //calculate the actual number of vehicles on a single path base on the percentage
            for (int i = 0; i < paths.Length; i++)
            {
                vehiclesCounts.Add(Mathf.CeilToInt(nodesPercentage[i] * vehiclesBodies.Length / 100.0f));
            }
            
            //assign vehicle to each path along with the position
            for (int i = 0; i < paths.Length; i++)
            {
                //Debug.Log(vehiclesCounts[i]);
                for (int j=0; j<vehiclesCounts[i]; j++)
                {
                    if (currentVehicleCount >= vehiclesBodies.Length)
                    {
                        break;
                    }
                    vehiclesIndexPosition[currentVehicleCount] = new VehicleBodyIndexPosition { value = j * (paths[i].nodesCount / vehiclesCounts[i]) };
                    vehiclesPathID[currentVehicleCount] = new VehicleBodyPathID { value = paths[i].id };

                    hashMap.TryAdd(vehiclesBodies[currentVehicleCount].id, new VehicleInitData { pathID = paths[i].id, pos = vehiclesIndexPosition[currentVehicleCount].value });
                    currentVehicleCount++;
                }
            }
            vehiclesBodies.Dispose();
            vehiclesIndexPosition.Dispose();
            vehiclesPathID.Dispose();

            var bodyIDType = GetArchetypeChunkComponentType<VehicleBodyIDAndSpeed>(true); //readonly
            var bodyPathIDType = GetArchetypeChunkComponentType<VehicleBodyPathID>();
            var bodyIndexType = GetArchetypeChunkComponentType<VehicleBodyIndexPosition>();


            //schedule job to populate vehicles to paths
            JobHandle job = new OnetimePopulateVehicleToPathJob
            {
                map = hashMap,
                vehicleBodyIDType = bodyIDType,
                vehicleBodyIndexPositionType = bodyIndexType,
                vehicleBodyPathIDType = bodyPathIDType
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

            NativeArray<VehicleBodyIDAndSpeed> bodyIDAndSpeed = GetEntityQuery(ComponentType.ReadOnly(typeof(VehicleBodyIDAndSpeed))).ToComponentDataArray<VehicleBodyIDAndSpeed>(Allocator.TempJob, out JobHandle job);

            //job to get the next position in the path
            var bodyIDSpeedType = GetArchetypeChunkComponentType<VehicleBodyIDAndSpeed>();
            var bodyRawPositionType = GetArchetypeChunkComponentType<VehicleBodyRawPosition>();
            var bodyIndexPositionType = GetArchetypeChunkComponentType<VehicleBodyIndexPosition>();
            var bodyPathIDType = GetArchetypeChunkComponentType<VehicleBodyPathID>();
            var bodyLengthType = GetArchetypeChunkComponentType<VehicleBodyLength>(true); //readonly
            JobHandle resolveNextNodeJob = new ResolveNextPositionForVehicleJob
            {
                bodyIDSpeedType = bodyIDSpeedType,
                bodyIndexPositionType =bodyIndexPositionType,
                bodyPathIDType = bodyPathIDType,
                bodyRawPositionType = bodyRawPositionType,
                bodyLengthType = bodyLengthType,
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
            //job to apply vehicle position         
            var rotationType = GetArchetypeChunkComponentType<Rotation>(false);
            var translationType = GetArchetypeChunkComponentType<Translation>(false);
            JobHandle moveVehicleJob = new MoveVehicleBodyJob
            {
                vehicleRotationType = rotationType,
                vehicletranslateType = translationType,
                bodyLengthType = bodyLengthType,
                bodyRawPositionType = bodyRawPositionType
            }.Schedule(vehicleBodidesEntities, resolveNextNodeJob);

            //***********************************************
            //job to fill hashmap fill the hashmap
            NativeMultiHashMap<int, VehiclePosition> hashMap = new NativeMultiHashMap<int, VehiclePosition>(10000, Allocator.TempJob);
            JobHandle fillHashmapJob = new FillHasMapJob
            {
                map = hashMap.ToConcurrent(),
                bodyIndexPositionType = bodyIndexPositionType,
                bodyLengthType = bodyLengthType,
                bodyPathIDType = bodyPathIDType,
            }.Schedule(vehicleBodidesEntities, JobHandle.CombineDependencies(clearPathsJob, resolveNextNodeJob));

            //***********************************************
            //Schedule filljob
            JobHandle fillPathOccupancy = new FillPathsOccupancyJob
            {
                map = hashMap,
                pathType = pathType
            }.Schedule(pathEntities, fillHashmapJob);

            //***********************************************
            //schedule move wheel job
            var wheelType = GetArchetypeChunkComponentType<VehicleWheelMoveAndRotate>(false);
            var wheelLocationType = GetArchetypeChunkComponentType<LocalToWorld>(false);
            JobHandle moveVehicleWheelJob = new MoveVehicleWheelJob
            {
                cameraPosition = camTrans.position,
                deltaTime = 0.02f,
                bodyIDAndSpeed = bodyIDAndSpeed,
                wheelRotationType = rotationType,
                vehicleWheelType = wheelType,
                wheelLocationType = wheelLocationType,
            }.Schedule(vehicleWheelsEntities, moveVehicleJob);

            fillPathOccupancy.Complete();
            hashMap.Dispose();

            //job to check for merging
            var mergeType = GetArchetypeChunkComponentType<PathMerge>(true);
            JobHandle resolveMergingJob = new ResolveMergingForPath
            {
                pathMergeType = mergeType,
                paths = paths
            }.Schedule(pathMergingEntities, fillPathOccupancy);

            return JobHandle.CombineDependencies(resolveMergingJob, moveVehicleJob, moveVehicleWheelJob);
        }

        protected override void OnDestroyManager()
        {
            paths.Dispose();
        }

    }
}