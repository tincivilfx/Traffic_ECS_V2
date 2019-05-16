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
    public unsafe class CustomMemory
    {

    }
    public unsafe class CustomMemoryManager <T> where T : unmanaged
    {
        private void* ptr;
        private Allocator allocator;

        public CustomMemoryManager()
        {
            ptr = null;
        }

        public void AllocateMemory(long size, int alignment, Allocator _allocator)
        {
            allocator = _allocator;

            try
            {
                ptr = UnsafeUtility.Malloc(size * sizeof(T), alignment, allocator);
            } catch (Exception ex) {
                Debug.LogError("Failed to allocate memory: " + ex.Message);
            }
        }

        public T* GetPointer()
        {
            return (T*)ptr;
        }


        ~CustomMemoryManager()
        {
            if (ptr != null)
            {
                Debug.Log("Deallocate");
                UnsafeUtility.Free(ptr, allocator);
            }
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
        public BakedTrafficPath path;

        //
        public static int VEHICLE_ID_POOL = 0;
        public static int PATH_ID_POOL = 0;


        private List<CustomMemoryManager<float3>> unsafeMemoriesFloat3;
        private List<CustomMemoryManager<bool>> unsafeMemoriesBool;


        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            Debug.Log("Converting");

            unsafeMemoriesFloat3 = new List<CustomMemoryManager<float3>>();
            unsafeMemoriesBool = new List<CustomMemoryManager<bool>>();

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
            
            //convert path asset to ecs
            Entity pathEntity = conversionSystem.CreateAdditionalEntity(this);

            unsafe
            {
                //path
                CustomMemoryManager<float3> pathMem = new CustomMemoryManager<float3>();
                pathMem.AllocateMemory(path.PathNodes.Count, 32, Allocator.Persistent);
                //add to list
                unsafeMemoriesFloat3.Add(pathMem);

                //occupied
                CustomMemoryManager<bool> occupiedSlotMem = new CustomMemoryManager<bool>();
                occupiedSlotMem.AllocateMemory(path.PathNodes.Count, 8, Allocator.Persistent);

                //add to list
                unsafeMemoriesBool.Add(occupiedSlotMem);

                //var nodes = (float3*)UnsafeUtility.Malloc(path.PathNodes.Count * sizeof(float3), 32, Allocator.Persistent);
                //var occupiedSlot = (bool*)UnsafeUtility.Malloc(path.PathNodes.Count * sizeof(bool), 8, Allocator.Persistent);

                for (int i=0; i<path.PathNodes.Count; i++)
                {
                    pathMem.GetPointer()[i] = path.PathNodes[i];
                    occupiedSlotMem.GetPointer()[i] = false;
                }

                Debug.Log("Finished");

                var pathData = new Path
                {
                    id = 0,
                    maxSpeed = (byte)path.actualSpeedLimit,
                    nodesCount = path.PathNodes.Count,
                    pathNodes = pathMem.GetPointer(),
                    occupied = occupiedSlotMem.GetPointer()
                };
                dstManager.AddComponentData(pathEntity, pathData);
            }
        
            //add total vehicles to ECS
            var traffSettings = new TrafficSettingsData
            {
                vehicleCount = totalVehicles,
            };
            dstManager.AddComponentData(entity, traffSettings);



            //Done with paths
            //upload them
            Resources.UnloadAsset(path);

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