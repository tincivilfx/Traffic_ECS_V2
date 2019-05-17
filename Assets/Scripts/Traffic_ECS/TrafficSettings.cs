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
    public unsafe class CustomMemoryManagerBase
    {
        protected void* ptr;
        protected Allocator allocator;

        public CustomMemoryManagerBase()
        {
            ptr = null;
        }

        ~CustomMemoryManagerBase()
        {
            if (ptr != null)
            {
                Debug.Log("Deallocate");
                UnsafeUtility.Free(ptr, allocator);
            }
        }
    }

    public unsafe class CustomMemoryManager <T> : CustomMemoryManagerBase where T : unmanaged
    {
        public CustomMemoryManager() : base()
        {

        }

        public void AllocateMemory(long size, int alignment, Allocator _allocator)
        {
            allocator = _allocator;
            try
            {
                ptr = UnsafeUtility.Malloc(size * UnsafeUtility.SizeOf<T>(), alignment, allocator);
                Debug.Log("Allocate: " + size * UnsafeUtility.SizeOf<T>());
            } catch (Exception ex) {
                Debug.LogError("Failed to allocate memory: " + ex.Message);
            }
        }

        public T* GetPointer()
        {
            return ptr == null ? null : (T*)ptr;
        }
    }
    #endregion

    public unsafe class TrafficSettings : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
    {
        [Header("--------------------")]
        public int totalVehicles;

        [Header("--------------------")]
        public VehicleCollector collector;

        [Header("--------------------")]
        public BakedTrafficPathCollector pathsCollector;

        //
        public static int VEHICLE_ID_POOL = 0;
        public static int PATH_ID_POOL = 0;

        private List<CustomMemoryManagerBase> unsafeMemoryReferences;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            Debug.Log("Converting");

            unsafeMemoryReferences = new List<CustomMemoryManagerBase>();

            List<VehicleObject> vehiclePrefabs;
            InitializeTotalVehicles(out vehiclePrefabs);

            for (int i=0; i<vehiclePrefabs.Count; i++)
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
                for (int j=0; j<wheels.Length; j++)
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
            for (int i=0; i<pathsCollector.bakedTrafficPaths.Length; i++)
            {
                pathsCollector.bakedTrafficPaths[i].id = (byte)PATH_ID_POOL;
                PATH_ID_POOL++;
            }

            //conver path asset to ecs data
            unsafe
            {
                for (int i = 0; i < pathsCollector.bakedTrafficPaths.Length; i++)
                {
                    //get current path
                    var currentPath = pathsCollector.bakedTrafficPaths[i];
                    Debug.Log(currentPath.PathName);

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
                    CustomMemoryManager<PathLinkedData> linkedData = new CustomMemoryManager<PathLinkedData>();
                    unsafeMemoryReferences.Add(linkedData);

                    if (currentPath.splittingPaths != null && currentPath.splittingPaths.Count > 0)
                    {
                        linkedData.AllocateMemory(currentPath.splittingPaths.Count, 32, Allocator.Persistent);
                        var linkedDataPtr = linkedData.GetPointer();

                        for (int j=0; j<currentPath.splittingPaths.Count; j++)
                        {
                            linkedDataPtr[j].chance = (byte)currentPath.splittingPaths[j].turnedChance;
                            linkedDataPtr[j].connectingNode = currentPath.splittingPaths[j].startNode;
                            linkedDataPtr[j].transitionNode = currentPath.splittingPaths[j].transitionNode;
                            linkedDataPtr[j].linkedID = currentPath.splittingPaths[j].turnedPath.id;
                        }
                    }
                    

                    //create ecs data
                    var pathData = new Path
                    {
                        id = currentPath.id,
                        maxSpeed = (byte)currentPath.actualSpeedLimit,
                        nodesCount = currentPath.PathNodes.Count,
                        pathNodes = nodesPtr,
                        occupied = occupiedPtr,
                        linkedCount = linkedCount,
                        linked = linkedData.GetPointer()
                    };

                    //add ecs data
                    dstManager.AddComponentData(pathEntity, pathData);
                }
            }

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
            for (int i = 0; i < collector.vehicles.Length; i++)
            {
                var vehiclePrefabs = collector.vehicles[i];

                //add body;
                referencedPrefabs.Add(vehiclePrefabs.body);

                //add wheels
                referencedPrefabs.AddRange(vehiclePrefabs.wheels);
            }
        }


        private void InitializeTotalVehicles(out List<VehicleObject> objs)
        {
            objs = new List<VehicleObject>(totalVehicles);

            int lightVehiclesCount = Mathf.CeilToInt((float)totalVehicles * collector.percentage[0] / 100);
            int mediumVehiclesCount = Mathf.CeilToInt((float)totalVehicles * collector.percentage[1] / 100);
            int heavyVehiclesCount = Mathf.CeilToInt((float)totalVehicles * collector.percentage[2] / 100);

            var lightVehiclePrefabs = new List<VehicleObject>();
            var mediumVehiclePrefabs = new List<VehicleObject>();
            var heavyVehiclePrefabs = new List<VehicleObject>();

            //isolate each type
            for (int i = 0; i < collector.vehicles.Length; i++)
            {
                switch (collector.vehicles[i].type)
                {
                    case VehicleType.Light:
                        lightVehiclePrefabs.Add(collector.vehicles[i]);
                        break;
                    case VehicleType.Medium:
                        mediumVehiclePrefabs.Add(collector.vehicles[i]);
                        break;
                    case VehicleType.Heavy:
                        heavyVehiclePrefabs.Add(collector.vehicles[i]);
                        break;
                }
            }

            if (lightVehiclePrefabs.Count > 0) {

                while (lightVehiclePrefabs.Count < lightVehiclesCount)
                {
                    lightVehiclePrefabs.AddRange(lightVehiclePrefabs);
                }
                lightVehiclePrefabs = lightVehiclePrefabs.OrderBy(a => UnityEngine.Random.Range(0, int.MaxValue)).ToList();
                lightVehiclePrefabs.RemoveRange(0, lightVehiclePrefabs.Count - lightVehiclesCount);
            }

            if (mediumVehiclePrefabs.Count > 0) {
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

            Debug.Log(lightVehiclePrefabs.Count);
            Debug.Log(mediumVehiclePrefabs.Count);
            Debug.Log(heavyVehiclePrefabs.Count);


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