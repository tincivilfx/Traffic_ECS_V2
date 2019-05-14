using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        public int totalVehicles;
        public List<VehicleObject> vehiclePrefabs;
        public BakedTrafficPath path;

        //
        public static int ID_POOL = 0;


        private List<CustomMemoryManager<float3>> unsafeMemoriesFloat3;
        private List<CustomMemoryManager<bool>> unsafeMemoriesBool;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            Debug.Log("Converting");

            unsafeMemoriesFloat3 = new List<CustomMemoryManager<float3>>();
            unsafeMemoriesBool = new List<CustomMemoryManager<bool>>();

            for (int i=0; i<vehiclePrefabs.Count; i++)
            {
                Entity bodyEntity = conversionSystem.CreateAdditionalEntity(this);
                //convert vehicle body part to ECS
                var vehicleBody = new VehicleBody
                {
                    prefab = conversionSystem.GetPrimaryEntity(vehiclePrefabs[i].body),
                    length = vehiclePrefabs[i].bodyLength,
                    id = ID_POOL
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
                        id = ID_POOL
                    };
                    dstManager.AddComponentData(wheelEntity, vehicleWheel);
                }
                ID_POOL++;
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

                var pathData = new Path
                {
                    id = 0,
                    maxSpeed = 65,
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


        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            for (int i=0; i<vehiclePrefabs.Count; i++)
            {
                referencedPrefabs.Add(vehiclePrefabs[i].body);
                referencedPrefabs.AddRange(vehiclePrefabs[i].wheels);
            }
        }

        private void OnDestroy()
        {
            //deallocate memory
            Debug.Log("TrafficSettings Destroyed");
        }
    }
}