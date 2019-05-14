//used to be a system... now it's not

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace CivilFX.TrafficECS {

    public unsafe partial class TrafficSystem
    {
        public static float Map(float value, float lowerLimit, float uperLimit, float lowerValue, float uperValue)
        {
            return lowerValue + ((uperValue - lowerValue) / (uperLimit - lowerLimit)) * (value - lowerLimit);
        }


        public struct OnetimePopulateVehicleToPathJob : IJobForEachWithEntity<VehicleBodyMoveAndRotate>
        {
            public Path path;
            public void Execute(Entity entity, int index, ref VehicleBodyMoveAndRotate vehicle)
            {
                vehicle.currentPathID = path.id;
                vehicle.currentPos = index * vehicle.length * 2;
                Debug.Log(index + ":::" + vehicle.currentPos);
            }
        }

        public struct GetVehicleBodyPositionJob : IJobForEachWithEntity<VehicleBodyMoveAndRotate>
        {
            [ReadOnly] public NativeArray<Path> paths;
            public unsafe void Execute(Entity entity, int index, ref VehicleBodyMoveAndRotate vehicle)
            {
                Path currentPath = new Path { };

                int next = -1;
                int frontPos;
                int scanDis = 3000;
                int currentSpeed;

                //get the current path of this vehicle
                for (int i=0; i<paths.Length; i++)
                {
                    if (vehicle.currentPathID == paths[i].id)
                    {
                        currentPath = paths[i];
                    }
                }

                //resolve next position
                frontPos = vehicle.currentPos + (vehicle.length / 2);

                if (frontPos >= currentPath.nodesCount)
                {
                    //this vehicle is at the end of this path
                    vehicle.speed = 0;
                    return;
                }


                //check how many nodes can this vehicle move to
                //i.e. : scan distance

                scanDis = frontPos + scanDis < currentPath.nodesCount ? scanDis : currentPath.nodesCount - frontPos; //rescale scan distance

                next = scanDis;
                for (int i=1; i< scanDis; i++)
                {
                    if (currentPath.occupied[frontPos + i])
                    {
                        next = i;
                        break;
                    }
                }

                //map the speed
                currentSpeed = (int)(Map(next, 0, scanDis, 0, currentPath.maxSpeed));
                //clamp speed
                currentSpeed = math.clamp(currentSpeed, 0, currentPath.maxSpeed);

                //set current speed
                vehicle.speed = currentSpeed;
                vehicle.currentPos = vehicle.currentPos + currentSpeed;
                vehicle.location = currentPath.pathNodes[vehicle.currentPos];
                vehicle.lookAtLocation = vehicle.currentPos < currentPath.nodesCount - currentPath.maxSpeed ? currentPath.pathNodes[vehicle.currentPos + currentPath.maxSpeed] : vehicle.lookAtLocation;
            }
        }


        //Job to clear out all occupancy
        public struct ClearPathsOccupancyJob : IJobForEachWithEntity<Path>
        {
            public void Execute(Entity entity, int index, ref Path path)
            {
                for (int i=0; i< path.nodesCount; i++)
                {
                    path.occupied[i] = false;
                }
            }
        }

        public struct FillPathsOccupancyJob : IJobForEachWithEntity<Path>
        {
            [ReadOnly] public NativeMultiHashMap<int, VehiclePosition> map;

            public void Execute(Entity entity, int index, ref Path path)
            {
                VehiclePosition fillPosition;
                NativeMultiHashMapIterator<int> iterator;
                bool found = map.TryGetFirstValue(path.id, out fillPosition, out iterator);
                int fillStart = 0;
                int fillEnd = 0;

                while (found)
                {
                    fillStart = fillPosition.pos - fillPosition.length / 2;
                    fillStart = fillStart < 0 ? 0 : fillStart;

                    fillEnd = fillPosition.pos + fillPosition.length / 2;
                    fillEnd = fillEnd >= path.nodesCount ? path.nodesCount : fillEnd;

                    //start fill
                    for (int i= fillStart; i< fillEnd; i++)
                    {
                        path.occupied[i] = true;
                    }
                    found = map.TryGetNextValue(out fillPosition, ref iterator);
                }
            }
        }

        //Job to move vehicle
        public struct MoveVehicleBodyJob : IJobForEachWithEntity<VehicleBodyMoveAndRotate, Rotation>
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            [WriteOnly] public NativeMultiHashMap<int, VehiclePosition>.Concurrent map;
            public void Execute(Entity entity, int index, ref VehicleBodyMoveAndRotate vehicle, ref Rotation rotation)
            {
                //set location
                commandBuffer.SetComponent(index, entity, new Translation { Value = vehicle.location });

                //add info to hashmap
                map.Add(vehicle.currentPathID, new VehiclePosition { pos = vehicle.currentPos, length = vehicle.length });

                //set rotation
                var rot = quaternion.LookRotation(vehicle.lookAtLocation - vehicle.location, new float3(0, 1, 0));
                vehicle.rotation = rot;
                    
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
                    Debug.LogError("Failed to rotate wheel");
                    return;
                }

                rotation.Value = math.mul(math.normalize(rotation.Value), quaternion.AxisAngle(new float3 (0,0,1), body.speed * deltaTime));

                /*
                //set location
                var a = body.location == float3.zero;
                if (a.x && a.y && a.z)
                {
                    Debug.LogError("Failed to obtain body");
                    return;
                }

                commandBuffer.SetComponent(index, entity, new Translation { Value = body.location + wheel.positionOffset});
                
                
                //set rotation
                var rot = new quaternion(rotation.Value.value.x + body.rotation.value.x,
                                        rotation.Value.value.y + body.rotation.value.y,
                                        rotation.Value.value.z + body.rotation.value.z,
                                        rotation.Value.value.w + body.rotation.value.w);

                rot = quaternion.LookRotation((body.lookAtLocation + wheel.positionOffset) - (body.location + wheel.positionOffset), new float3(0, 1, 0));

                commandBuffer.SetComponent(index, entity, new Rotation { Value = rot });
                */
            }
        }

    }
}