using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Linq;
using System;

namespace CivilFX.TrafficECS
{
    #region inner classes

    #endregion

    public unsafe class TrafficSettings : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        [Header("--------------------")]
        public int totalVehicles;

        [Header("--------------------")]
        public VehicleCollector vehiclesCollector;

        [Header("--------------------")]
        public BakedTrafficPathCollector pathsCollector;

        [Header("--------------------")]
        public TrafficSignalController[] signalControllers;


        //
        public static int VEHICLE_ID_POOL = 0;
        public static int PATH_ID_POOL = 0;

        private List<CustomMemoryManagerBase> unsafeMemoryReferences;

        public unsafe void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            Debug.Log("Converting");

            unsafeMemoryReferences = new List<CustomMemoryManagerBase>();

            List<VehicleObject> vehiclePrefabs;
            InitializeTotalVehicles(out vehiclePrefabs);

            for (int i = 0; i < vehiclePrefabs.Count; i++)
            {
                Entity bodyEntity = conversionSystem.CreateAdditionalEntity(this);
                //convert vehicle body part to ECS
                var vehicleBody = new VehicleBody
                {
                    prefab = conversionSystem.GetPrimaryEntity(vehiclePrefabs[i].body),
                    length = vehiclePrefabs[i].bodyLength,
                    id = VEHICLE_ID_POOL
                };
                dstManager.AddComponentData(bodyEntity, vehicleBody);

                //convert vehicle wheel(s) part to ECS
                var wheels = vehiclePrefabs[i].wheels;
                for (int j = 0; j < wheels.Length; j++)
                {
                    Entity wheelEntity = conversionSystem.CreateAdditionalEntity(this);
                    var vehicleWheel = new VehicleWheel
                    {
                        prefab = conversionSystem.GetPrimaryEntity(wheels[j]),
                        positionOffset = wheels[j].gameObject.transform.position,
                        rotationOffset = wheels[j].gameObject.transform.eulerAngles,
                        id = VEHICLE_ID_POOL
                    };
                    dstManager.AddComponentData(wheelEntity, vehicleWheel);
                }
                VEHICLE_ID_POOL++;
            }
            //

            /*********** WORKING ON PATHS ********/

            //assign path's ID
            for (int i = 0; i < pathsCollector.bakedTrafficPaths.Length; i++)
            {
                pathsCollector.bakedTrafficPaths[i].id = (byte)PATH_ID_POOL;
                PATH_ID_POOL++;
            }

            //conver path asset to ecs data

            for (int i = 0; i < pathsCollector.bakedTrafficPaths.Length; i++)
            {
                //get current path
                var currentPath = pathsCollector.bakedTrafficPaths[i];
                //Debug.Log(currentPath.PathName);

                //create entity
                var pathEntity = conversionSystem.CreateAdditionalEntity(this);

                //allocate native memory
                CustomMemoryManager<float3> pathMem = new CustomMemoryManager<float3>();
                pathMem.AllocateMemory(currentPath.PathNodes.Count, 32, Allocator.Persistent);

                CustomMemoryManager<byte> occupiedSlotMem = new CustomMemoryManager<byte>();
                occupiedSlotMem.AllocateMemory(currentPath.PathNodes.Count, 8, Allocator.Persistent);

                //add to list
                unsafeMemoryReferences.Add(pathMem);
                unsafeMemoryReferences.Add(occupiedSlotMem);


                //populate allocated memory
                var nodesPtr = pathMem.GetPointer();
                var occupiedPtr = occupiedSlotMem.GetPointer();
                for (int j = 0; j < currentPath.PathNodes.Count; j++)
                {
                    nodesPtr[j] = currentPath.PathNodes[j];
                    occupiedPtr[j] = 0;
                }


                //populate linked count
                byte linkedCount = 0;

                //add splitting paths count
                linkedCount += currentPath.splittingPaths == null ? (byte)0 : (byte)currentPath.splittingPaths.Count;
                //add connecting (merging) paths count
                linkedCount += currentPath.connectingPaths == null ? (byte)0 : (byte)currentPath.connectingPaths.Count;

                CustomMemoryManager<PathLinkedData> linkedData = new CustomMemoryManager<PathLinkedData>();
                unsafeMemoryReferences.Add(linkedData);

                if (linkedCount > 0)
                {
                    linkedData.AllocateMemory(linkedCount, 32, Allocator.Persistent);
                    var linkedDataPtr = linkedData.GetPointer();

                    //add splitting paths
                    if (currentPath.splittingPaths != null)
                    {                   
                        for (int j = 0; j < currentPath.splittingPaths.Count; j++)
                        {
                            linkedDataPtr[j].chance = (byte)currentPath.splittingPaths[j].turnedChance;
                            linkedDataPtr[j].connectingNode = currentPath.splittingPaths[j].startNode;
                            linkedDataPtr[j].transitionNode = currentPath.splittingPaths[j].transitionNode;
                            linkedDataPtr[j].linkedID = currentPath.splittingPaths[j].turnedPath.id;
                        }
                    }

                    //add merging path
                    if (currentPath.connectingPaths != null && currentPath.connectingPaths.Count == 1)
                    {
                        linkedDataPtr[linkedCount - 1].connectingNode = currentPath.connectingPaths[0].startNode;
                        linkedDataPtr[linkedCount - 1].transitionNode = currentPath.connectingPaths[0].transitionNode;
                        linkedDataPtr[linkedCount - 1].linkedID = currentPath.connectingPaths[0].turnedPath.id;
                        linkedDataPtr[linkedCount - 1].chance = 255;

                        //add merging entity
                        var pathMergeEntity = conversionSystem.CreateAdditionalEntity(this);
                        var mergeData = new PathMerge
                        {
                            id = currentPath.id,
                            linkedID = currentPath.connectingPaths[0].turnedPath.id,
                            //TODO: check for array size
                            //startScanPos = currentPath.connectingPaths[0].startScanNode,
                            startScanPos = math.clamp((currentPath.connectingPaths[0].startScanNode == 0 ? currentPath.connectingPaths[0].startNode - (int)(currentPath.connectingPaths[0].turnedPath.PathNodes.Count * 0.061f) : currentPath.connectingPaths[0].startScanNode), 0, currentPath.connectingPaths[0].turnedPath.PathNodes.Count),
                            //endScanPos = currentPath.connectingPaths[0].endScanNode,
                            endScanPos = math.clamp(currentPath.connectingPaths[0].endScanNode == 0 ? currentPath.connectingPaths[0].startNode - (int)(currentPath.connectingPaths[0].turnedPath.PathNodes.Count * 0.0133f) : currentPath.connectingPaths[0].endScanNode,0 , currentPath.connectingPaths[0].turnedPath.PathNodes.Count),
                            stopPos = currentPath.connectingPaths[0].yieldNode == 0 ? (int)(currentPath.PathNodes.Count * 0.89f) : currentPath.connectingPaths[0].yieldNode,
                        };
                        //currentPath.connectingPaths[0].startScanNode = mergeData.startScanPos;
                        //currentPath.connectingPaths[0].endScanNode = mergeData.endScanPos;
                        //currentPath.connectingPaths[0].yieldNode = mergeData.stopPos;
                        dstManager.AddComponentData(pathMergeEntity, mergeData);
                    }

                    
                }
                //create ecs data
                var pathData = new Path
                {
                    id = currentPath.id,
                    maxSpeed = (byte)currentPath.actualSpeedLimit,
                    type = currentPath.type,
                    nodesCount = currentPath.PathNodes.Count,
                    pathNodes = nodesPtr,
                    occupied = occupiedPtr,
                    allowedRespawn = currentPath.allowRespawn,
                    linkedCount = linkedCount,
                    linked = linkedData.GetPointer()
                };

                //add ecs data
                dstManager.AddComponentData(pathEntity, pathData);
            }



            //***************************************************
            //handle signal controllers
            if (signalControllers != null && signalControllers.Length > 0)
            {

                //iterate over each signal controller
                //each signalcontroller will have a set
                for (int i = 0; i < signalControllers.Length; i++)
                {
                    var signal = signalControllers[i];

                    //allocate memory for set
                    CustomMemoryManager<SignalSet> signalSetMem = new CustomMemoryManager<SignalSet>();
                    signalSetMem.AllocateMemory(signal.sets.Length);
                    var signalSetPtr = signalSetMem.GetPointer();

                    for (int j = 0; j < signal.sets.Length; j++)
                    {   
                        //allocate memory for pathsIDs
                        CustomMemoryManager<byte> pathsIDsMem = new CustomMemoryManager<byte>();
                        pathsIDsMem.AllocateMemory(signal.sets[j].bakedPaths.Length);
                        var pathsIDsMemPTr = pathsIDsMem.GetPointer();

                        CustomMemoryManager<int> stopPosesMem = new CustomMemoryManager<int>();
                        stopPosesMem.AllocateMemory(signal.sets[j].bakedPaths.Length);
                        var stopPosesMemPtr = stopPosesMem.GetPointer();

                        for (int k = 0; k < signal.sets[j].bakedPaths.Length; k++)
                        {
                            pathsIDsMemPTr[k] = signal.sets[j].bakedPaths[k].path.id;
                            stopPosesMemPtr[k] = signal.sets[j].bakedPaths[k].stopPos;
                        }

                        signalSetPtr[j].id = signal.sets[j].id;
                        signalSetPtr[j].pathsCount = (byte)signal.sets[j].bakedPaths.Length;
                        signalSetPtr[j].pathIDs = pathsIDsMemPTr;
                        signalSetPtr[j].stopPoses = stopPosesMemPtr;

                        //add to list for deallocation 
                        unsafeMemoryReferences.Add(stopPosesMem);
                        unsafeMemoryReferences.Add(pathsIDsMem);
                    }
                    unsafeMemoryReferences.Add(signalSetMem);

                    //allocate memory for sequence
                    CustomMemoryManager<SignalFrame> signalFrameMem = new CustomMemoryManager<SignalFrame>();
                    signalFrameMem.AllocateMemory(signal.sequence.sequences.Length);
                    var signalFramePtr = signalFrameMem.GetPointer();
                    unsafeMemoryReferences.Add(signalFrameMem);

                    for (int j=0; j<signal.sequence.sequences.Length; j++)
                    {
                        signalFramePtr[j].time = signal.sequence.sequences[j].time;
                        signalFramePtr[j].setID = signal.sequence.sequences[j].setID;
                        signalFramePtr[j].type = signal.sequence.sequences[j].type;
                        signalFramePtr[j].active = signal.sequence.sequences[j].active;
                    }

                    //create Entity
                    var signalEntity = conversionSystem.CreateAdditionalEntity(this);
                    var signalData = new TrafficSignalNode
                    {
                        sets = signalSetPtr,
                        setsCount = (byte)signal.sets.Length,
                        sequence = signalFramePtr,
                        sequenceCount = (byte)signal.sequence.sequences.Length
                    };
                    dstManager.AddComponentData(signalEntity, signalData);
                }
            }
            //***************************************************

            //add total vehicles to ECS
            var traffSettings = new TrafficSettingsData
            {
                vehicleCount = totalVehicles,
            };
            dstManager.AddComponentData(entity, traffSettings);

            Debug.Log("Finished");

            //Done with paths
            //upload them

            Resources.UnloadAsset(pathsCollector);
            pathsCollector = null;

            //explicitly call garbage collector
            System.GC.Collect();
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            for (int i = 0; i < vehiclesCollector.vehicles.Length; i++)
            {
                var vehiclePrefabs = vehiclesCollector.vehicles[i];

                //add body;
                referencedPrefabs.Add(vehiclePrefabs.body);

                //add wheels
                referencedPrefabs.AddRange(vehiclePrefabs.wheels);
            }
        }


        private void InitializeTotalVehicles(out List<VehicleObject> objs)
        {
            objs = new List<VehicleObject>(totalVehicles);

            int lightVehiclesCount = Mathf.CeilToInt((float)totalVehicles * vehiclesCollector.percentage[0] / 100);
            int mediumVehiclesCount = Mathf.CeilToInt((float)totalVehicles * vehiclesCollector.percentage[1] / 100);
            int heavyVehiclesCount = Mathf.CeilToInt((float)totalVehicles * vehiclesCollector.percentage[2] / 100);

            var lightVehiclePrefabs = new List<VehicleObject>();
            var mediumVehiclePrefabs = new List<VehicleObject>();
            var heavyVehiclePrefabs = new List<VehicleObject>();

            //isolate each type
            for (int i = 0; i < vehiclesCollector.vehicles.Length; i++)
            {
                switch (vehiclesCollector.vehicles[i].type)
                {
                    case VehicleType.Light:
                        lightVehiclePrefabs.Add(vehiclesCollector.vehicles[i]);
                        break;
                    case VehicleType.Medium:
                        mediumVehiclePrefabs.Add(vehiclesCollector.vehicles[i]);
                        break;
                    case VehicleType.Heavy:
                        heavyVehiclePrefabs.Add(vehiclesCollector.vehicles[i]);
                        break;
                }
            }

            if (lightVehiclePrefabs.Count > 0)
            {

                while (lightVehiclePrefabs.Count < lightVehiclesCount)
                {
                    lightVehiclePrefabs.AddRange(lightVehiclePrefabs);
                }
                lightVehiclePrefabs = lightVehiclePrefabs.OrderBy(a => UnityEngine.Random.Range(0, int.MaxValue)).ToList();
                lightVehiclePrefabs.RemoveRange(0, lightVehiclePrefabs.Count - lightVehiclesCount);
            }

            if (mediumVehiclePrefabs.Count > 0)
            {
                while (mediumVehiclePrefabs.Count < mediumVehiclesCount)
                {
                    mediumVehiclePrefabs.AddRange(mediumVehiclePrefabs);
                }
                mediumVehiclePrefabs = mediumVehiclePrefabs.OrderBy(a => UnityEngine.Random.Range(0, int.MaxValue)).ToList();
                mediumVehiclePrefabs.RemoveRange(0, mediumVehiclePrefabs.Count - mediumVehiclesCount);
            }

            if (heavyVehiclePrefabs.Count > 0)
            {
                while (heavyVehiclePrefabs.Count < heavyVehiclesCount)
                {
                    heavyVehiclePrefabs.AddRange(heavyVehiclePrefabs);
                }
                heavyVehiclePrefabs = heavyVehiclePrefabs.OrderBy(a => UnityEngine.Random.Range(0, int.MaxValue)).ToList();
                heavyVehiclePrefabs.RemoveRange(0, heavyVehiclePrefabs.Count - heavyVehiclesCount);
            }

            //Debug.Log(lightVehiclePrefabs.Count);
            //Debug.Log(mediumVehiclePrefabs.Count);
            //Debug.Log(heavyVehiclePrefabs.Count);


            objs.AddRange(lightVehiclePrefabs);
            objs.AddRange(mediumVehiclePrefabs);
            objs.AddRange(heavyVehiclePrefabs);
            objs = objs.OrderBy(a => UnityEngine.Random.Range(0, int.MaxValue)).ToList();

            /*
            for (int i=0; i< objs.Count; i++)
            {
                Debug.Log(objs[i].body.gameObject.name);
            }
            */
        }


        private void OnDestroy()
        {
            //deallocate memory
            Debug.Log("TrafficSettings Destroyed");
        }



    }
}