//used to be a system... now it's not

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace CivilFX.TrafficECS {

    public unsafe partial class TrafficSystem
    {
        public enum OccupiedType
        {
            Vehicle,
            TrafficSignal
        }

        public static readonly byte VEHICLE_OCCUPIED_BIT = 1;
        public static readonly byte TRAFFIC_SIGNAL_OCCUPIED_BIT = 2;
        public static readonly byte YIELD_OCCUPIED_BIT = 4;

        public static float Map(float value, float lowerLimit, float uperLimit, float lowerValue, float uperValue)
        {
            return lowerValue + ((uperValue - lowerValue) / (uperLimit - lowerLimit)) * (value - lowerLimit);
        }

        public static byte SetOccupied(byte value, bool occupied, OccupiedType type)
        {
            switch (type)
            {
                case OccupiedType.Vehicle:
                    if (occupied)
                    {
                        //set bit
                        value = (byte)(value | VEHICLE_OCCUPIED_BIT);
                    } else
                    {
                        //clear bit
                        value = (byte)(value & (~VEHICLE_OCCUPIED_BIT));
                    }
                    break;
                case OccupiedType.TrafficSignal:
                    if (occupied)
                    {
                        value = (byte)(value | TRAFFIC_SIGNAL_OCCUPIED_BIT);
                    } else
                    {
                        value = (byte)(value & (~TRAFFIC_SIGNAL_OCCUPIED_BIT));
                    }
                    break;
            }
            return value;
        }

        //return    true if occupied
        //          false otherwise
        public static bool CheckOccupied(byte value, OccupiedType type)
        {
            switch (type)
            {
                case OccupiedType.Vehicle:
                    return (value & VEHICLE_OCCUPIED_BIT) != 0;
                case OccupiedType.TrafficSignal:
                    return (value & TRAFFIC_SIGNAL_OCCUPIED_BIT) != 0;
            }
            return true;
        }




        //job to place all the vehicles on all the paths when starting up
        [BurstCompile]
        public struct OnetimePopulateVehicleToPathJob : IJobChunk
        {
            [ReadOnly] public NativeHashMap<int, VehicleInitData> map;
            public ArchetypeChunkComponentType<VehicleBodyMoveAndRotate> vehicleBodyType;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkVehicles = chunk.GetNativeArray(vehicleBodyType);

                for (int i=0; i< chunk.Count; i++)
                {
                    var vehicleData = chunkVehicles[i];
                    
                    if (map.TryGetValue(vehicleData.id, out VehicleInitData data))
                    {
                        vehicleData.currentPathID = data.pathID;
                        vehicleData.currentPos = data.pos;
                        chunkVehicles[i] = vehicleData;

                        /*
                        chunkVehicles[i] = new VehicleBodyMoveAndRotate
                        {
                            waiting = vehicleData.waiting,
                            length = vehicleData.length,
                            speed = vehicleData.speed,
                            id = vehicleData.id,
                            currentPathID = data.pathID,
                            currentPos = data.pos,
                            location = vehicleData.location,
                            lookAtLocation = vehicleData.lookAtLocation
                        };
                        */
                    }
                }

            }
        }

        [BurstCompile]
        public struct ResolveNextPositionForVehicleJob : IJobChunk
        {
            public ArchetypeChunkComponentType<VehicleBodyMoveAndRotate> vehicleBodyType;
            [ReadOnly] public NativeArray<Path> paths;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkVehicles = chunk.GetNativeArray(vehicleBodyType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var vehicleData = chunkVehicles[i];
                    Path currentPath = new Path { id = 255 };

                    //get the current path of this vehicle
                    for (int j = 0; j < paths.Length; j++)
                    {
                        if (vehicleData.currentPathID == paths[j].id)
                        {
                            currentPath = paths[j];
                        }
                    }

                    //resolve next position
                    var frontPos = vehicleData.currentPos + (vehicleData.length / 2);
                    if (frontPos >= currentPath.nodesCount)
                    {
                        //this vehicle is at the end of this path
                        vehicleData.speed = 0;

                        /*
                        //hiding this vehicle
                        vehicle.waiting = true;
                        commandBuffer.AddComponent(index, entity, new Frozen { });
                        */
                        continue;
                    }

                    //check how many nodes can this vehicle move to
                    //i.e. : scan distance

                    var scanDis = 3000;
                    scanDis = frontPos + scanDis < currentPath.nodesCount ? scanDis : currentPath.nodesCount - frontPos; //rescale scan distance

                    var next = scanDis;
                    for (int j = 1; j < scanDis; j++)
                    {
                        if (CheckOccupied(currentPath.occupied[frontPos + j], OccupiedType.Vehicle))
                        {
                            next = j;
                            break;
                        }
                    }

                    //map the speed
                    var currentSpeed = (int)(Map(next, 0, scanDis, 0, currentPath.maxSpeed));
                    //clamp speed
                    currentSpeed = math.clamp(currentSpeed, 0, currentPath.maxSpeed);

                    //set current speed
                    vehicleData.speed = currentSpeed;
                    vehicleData.currentPos = vehicleData.currentPos + currentSpeed;
                    vehicleData.location = currentPath.pathNodes[vehicleData.currentPos];
                    vehicleData.lookAtLocation = vehicleData.currentPos < currentPath.nodesCount - currentPath.maxSpeed ? currentPath.pathNodes[vehicleData.currentPos + currentPath.maxSpeed] : vehicleData.lookAtLocation;

                    //assign value back
                    chunkVehicles[i] = vehicleData;
                } 
            }
        }

        //Job to clear out all occupancy
        [BurstCompile]
        public struct ClearPathsOccupancyJob : IJobChunk
        {
            public ArchetypeChunkComponentType<Path> pathType;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkPath = chunk.GetNativeArray(pathType);
                for (int i=0; i<chunk.Count; i++)
                {
                    var path = chunkPath[i];
                    for (int j=0; j<path.nodesCount; j++)
                    {
                        path.occupied[j] = 0;
                    }
                }
            }
        }

        [BurstCompile]
        public struct FillPathsOccupancyJob : IJobChunk
        {
            public ArchetypeChunkComponentType<Path> pathType;
            [ReadOnly] public NativeMultiHashMap<int, VehiclePosition> map;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkPath = chunk.GetNativeArray(pathType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var path = chunkPath[i];

                    bool found = map.TryGetFirstValue(path.id, out VehiclePosition fillPosition, out NativeMultiHashMapIterator<int> iterator);
                    while (found)
                    {
                        var fillStart = fillPosition.pos - fillPosition.length / 2;
                        fillStart = fillStart < 0 ? 0 : fillStart;

                        var fillEnd = fillPosition.pos + fillPosition.length / 2;
                        fillEnd = fillEnd >= path.nodesCount ? path.nodesCount : fillEnd;

                        //start fill
                        for (int j = fillStart; j < fillEnd; j++)
                        {
                            path.occupied[j] = SetOccupied(path.occupied[j], true, OccupiedType.Vehicle);
                        }
                        found = map.TryGetNextValue(out fillPosition, ref iterator);
                    }
                }
            }
        }


        //TODO: set location value here instead of waiting for main thread
        //Job to move vehicle
        public struct MoveVehicleBodyJob : IJobForEachWithEntity<VehicleBodyMoveAndRotate, Rotation>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            [WriteOnly] public NativeMultiHashMap<int, VehiclePosition>.Concurrent map;
            public void Execute(Entity entity, int index, ref VehicleBodyMoveAndRotate vehicle, ref Rotation rotation)
            {
                if (vehicle.waiting)
                {
                    return;
                }
                //set location
                commandBuffer.SetComponent(index, entity, new Translation { Value = vehicle.location });

                //add info to hashmap
                map.Add(vehicle.currentPathID, new VehiclePosition { pos = vehicle.currentPos, length = vehicle.length });

                //set rotation
                var rot = quaternion.LookRotation(vehicle.lookAtLocation - vehicle.location, new float3(0, 1, 0));                
                commandBuffer.SetComponent(index, entity, new Rotation { Value = rot });
            }
        }

        //job to rotate wheels
        //location will be handled by unity built-in system
        public struct MoveVehicleWheelJob : IJobForEachWithEntity<VehicleWheelMoveAndRotate, Rotation>
        {
            public float deltaTime;
            public EntityCommandBuffer.Concurrent commandBuffer;
            [ReadOnly] public NativeArray<VehicleBodyMoveAndRotate> bodies;
            public void Execute(Entity entity, int index, ref VehicleWheelMoveAndRotate wheel, ref Rotation rotation)
            {

                VehicleBodyMoveAndRotate body = VehicleBodyMoveAndRotate.Null;

                for (int i=0; i<bodies.Length; i++)
                {
                    if (wheel.id == bodies[i].id)
                    {
                        body = bodies[i];
                        break;
                    }
                }

               
                
                //safety check
                if (body.Equals(VehicleBodyMoveAndRotate.Null))
                {
                    //Debug.LogError("Failed to rotate wheel");
                    return;
                }
                /*
                if (body.waiting)
                {
                    commandBuffer.AddComponent(index, entity, new Frozen { });
                    return;
                }
                */
                rotation.Value = math.mul(math.normalize(rotation.Value), quaternion.AxisAngle(new float3 (1,0,0), body.speed * deltaTime));
            }
        }

    }
}