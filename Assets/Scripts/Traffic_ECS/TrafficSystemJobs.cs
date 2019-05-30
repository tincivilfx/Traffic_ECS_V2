//used to be a system... now it's not

using System.Collections;
using System.Collections.Generic;
//using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace CivilFX.TrafficECS {

    public unsafe partial class TrafficSystem
    {
        //job to place all the vehicles on all the paths when starting up
        [BurstCompile]
        public struct OnetimePopulateVehicleToPathJob : IJobChunk
        {
            [ReadOnly] public NativeHashMap<int, VehicleInitData> map;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyIDAndSpeed> vehicleBodyIDType;
            public ArchetypeChunkComponentType<VehicleBodyPathID> vehicleBodyPathIDType;
            public ArchetypeChunkComponentType<VehicleBodyIndexPosition> vehicleBodyIndexPositionType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkID = chunk.GetNativeArray(vehicleBodyIDType);
                var chunkPathID = chunk.GetNativeArray(vehicleBodyPathIDType);
                var chunkIndexPosition = chunk.GetNativeArray(vehicleBodyIndexPositionType);

                for (int i=0; i< chunk.Count; i++)
                {
                    var id = chunkID[i];
                    var pathId = chunkPathID[i];
                    var index = chunkIndexPosition[i];

                    if (map.TryGetValue(id.id, out VehicleInitData data))
                    {
                        pathId.value = data.pathID;
                        index.value = data.pos;

                        chunkPathID[i] = pathId;
                        chunkIndexPosition[i] = index;
                    }
                }

            }
        }


        [BurstCompile]
        struct RespawnVehicleJob : IJobChunk
        {
            [ReadOnly] public NativeArray<Path> paths;
            public NativeArray<Random> rands;
            public ArchetypeChunkComponentType<VehicleBodyIDAndSpeed> bodyIDSpeedType;
            public ArchetypeChunkComponentType<VehicleBodyRawPosition> bodyRawPositionType;
            public ArchetypeChunkComponentType<VehicleBodyIndexPosition> bodyIndexPositionType;
            public ArchetypeChunkComponentType<VehicleBodyPathID> bodyPathIDType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkBodyIDAndSpeed = chunk.GetNativeArray(bodyIDSpeedType);
                var chunkPathID = chunk.GetNativeArray(bodyPathIDType);
                var chunkRawPosition = chunk.GetNativeArray(bodyRawPositionType);
                var chunkIndexPosition = chunk.GetNativeArray(bodyIndexPositionType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var idAndSpeed = chunkBodyIDAndSpeed[i];
                    var rawPosition = chunkRawPosition[i];
                    var indexPosition = chunkIndexPosition[i];
                    var pathID = chunkPathID[i];

                    if (pathID.value == BYTE_INVALID)
                    {
                        Random rand = rands[0];
                        int pathIndex = rand.NextInt(0, paths.Length);
                        byte lvalue = 0;
                        Path currentPath = paths[pathIndex];
                        rands[0] = rand;
                        if (currentPath.type != TrafficPathType.Main)
                        {
                            //continue;
                        }

                        for (int j=0; j<MAX_SCAN_DISTANCE; j++)
                        {
                            lvalue |= currentPath.occupied[j];
                        }

                        //TODO: skip entire chunk
                        if (CheckOccupied(lvalue, OccupiedType.Vehicle))
                        {
                            continue;
                        }

                        pathID.value = currentPath.id;
                        idAndSpeed.speed = currentPath.maxSpeed;
                        indexPosition.value = 0;
                        rawPosition.position = currentPath.pathNodes[0];
                        rawPosition.lookAtPosition = currentPath.pathNodes[currentPath.maxSpeed];

                        chunkPathID[i] = pathID;
                        chunkBodyIDAndSpeed[i] = idAndSpeed;
                        chunkIndexPosition[i] = indexPosition;
                        chunkRawPosition[i] = rawPosition;
                    }
                }
            }
        }

        [BurstCompile]
        public struct ResolveNextPositionForVehicleJob : IJobChunk
        {
            [ReadOnly] public NativeArray<Path> paths;

            public ArchetypeChunkComponentType<VehicleBodyIDAndSpeed> bodyIDSpeedType;
            public ArchetypeChunkComponentType<VehicleBodyRawPosition> bodyRawPositionType;
            public ArchetypeChunkComponentType<VehicleBodyIndexPosition> bodyIndexPositionType;
            public ArchetypeChunkComponentType<VehicleBodyPathID> bodyPathIDType;
            public ArchetypeChunkComponentType<VehicleBodyWaitingStatus> bodyWaitingType;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyLength> bodyLengthType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkBodyIDAndSpeed = chunk.GetNativeArray(bodyIDSpeedType);
                var chunkPathID = chunk.GetNativeArray(bodyPathIDType);
                var chunkRawPosition = chunk.GetNativeArray(bodyRawPositionType);
                var chunkIndexPosition = chunk.GetNativeArray(bodyIndexPositionType);
                var chunkLength = chunk.GetNativeArray(bodyLengthType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var idAndSpeed = chunkBodyIDAndSpeed[i];
                    var rawPosition = chunkRawPosition[i];
                    var indexPosition = chunkIndexPosition[i];
                    var pathID = chunkPathID[i];
                    var length = chunkLength[i];

                    Path currentPath = new Path { id = BYTE_INVALID };
                    Path mergedPath = new Path { id = BYTE_INVALID };
                    PathLinkedData linkedData = new PathLinkedData { linkedID = BYTE_INVALID };
                    bool hasMergedPath = false;

                    //get the current path of this vehicle
                    for (int j = 0; j < paths.Length; j++)
                    {
                        if (pathID.value == paths[j].id)
                        {
                            currentPath = paths[j];
                            break;
                        }
                    }

                    //no path found
                    if (currentPath.id == BYTE_INVALID)
                    {
                        continue;
                    }

                    //get mergedPath (if any)
                    if (currentPath.linkedCount > 0 && currentPath.linked[currentPath.linkedCount - 1].chance == BYTE_INVALID)
                    {
                        linkedData = currentPath.linked[currentPath.linkedCount - 1];
                        for (int j = 0; j < paths.Length; j++)
                        {
                            if (linkedData.linkedID == paths[j].id)
                            {
                                mergedPath = paths[j];
                                hasMergedPath = true;
                                break;
                            }
                        }
                    }

                    //resolve next position
                    //get front position
                    var frontPos = indexPosition.value + (length.value / 2);

                    //Debug.Log(vehicleData.id + ":" + vehicleData.currentPos + ":" + vehicleData.speed + ":" + currentPath.nodesCount);
                    if (indexPosition.value + currentPath.maxSpeed >= currentPath.nodesCount)
                    {
                        //Debug.Log("Reach end");
                        //this vehicle is at the end of this path
                        if (hasMergedPath)
                        {
                            //Debug.Log("Merging");
                            pathID.value = mergedPath.id;
                            indexPosition.value = linkedData.connectingNode;
                            rawPosition.position = mergedPath.pathNodes[linkedData.connectingNode];
                            rawPosition.lookAtPosition = mergedPath.pathNodes[linkedData.connectingNode + mergedPath.maxSpeed];
                        } else
                        {
                            pathID.value = BYTE_INVALID;
                            indexPosition.value = -1;
                            idAndSpeed.speed = 0;
                            rawPosition.position = OUT_OF_WORLD_POSITION;
                            rawPosition.lookAtPosition = OUT_OF_WORLD_POSITION;
                        }
                        //set
                        chunkPathID[i] = pathID;
                        chunkBodyIDAndSpeed[i] = idAndSpeed;
                        chunkIndexPosition[i] = indexPosition;
                        chunkRawPosition[i] = rawPosition;
                        continue;
                    }

                    //check how many nodes can this vehicle move to
                    //i.e. : scan distance
                    var scanDis = MAX_SCAN_DISTANCE;
                    var scanDisLeftOver = 0;
                    //scale scanDis;
                    if (frontPos + scanDis > currentPath.nodesCount)
                    {
                        scanDis = currentPath.nodesCount - frontPos;
                        //there is merging path
                        if (hasMergedPath)
                        {
                            scanDisLeftOver = MAX_SCAN_DISTANCE - scanDis;
                        }
                    }

                    var hitDis = scanDis;
                    var hit = false;
                    byte lvalue = 0;
                    for (int j = 1; j < scanDis; j++)
                    {
                        lvalue = currentPath.occupied[frontPos + j];
                        if (CheckOccupied(lvalue, OccupiedType.Vehicle) || CheckOccupied(lvalue, OccupiedType.TrafficSignal)
                            || CheckOccupied(lvalue, OccupiedType.YieldMerging))
                        {
                            hitDis = j;
                            hit = true;
                            break;
                        }
                    }

                    //Debug.Log(vehicleData.id + ":" + hit + ":" + hitDis + ":" + scanDisLeftOver);
                    //if no vehicle seen on current path
                    //we check for the merged path
                    if (!hit && scanDisLeftOver > 0)
                    {
                        var startNode = linkedData.connectingNode;
                        while (scanDisLeftOver > 0)
                        {
                            lvalue = mergedPath.occupied[startNode];
                            if (CheckOccupied(lvalue, OccupiedType.Vehicle) || CheckOccupied(lvalue, OccupiedType.TrafficSignal)
                            || CheckOccupied(lvalue, OccupiedType.YieldMerging))
                            {
                                break;
                            }
                            hitDis++;
                            startNode++;
                            scanDisLeftOver--;
                        }
                    }
                    
                    //rescale hitDis to ensure vehicle move to out of path
                    if (frontPos + length.value >= currentPath.nodesCount && !hasMergedPath)
                    {
                        hitDis = MAX_SCAN_DISTANCE;
                    }
                    //map the speed
                    var currentSpeed = Map(hitDis, 0, MAX_SCAN_DISTANCE, 0, currentPath.maxSpeed);

                    //Debug.Log(vehicleData.id + ":" + hit + ":" + hitDis + ":" + scanDisLeftOver);
                    //Debug.Log(vehicleData.id + ":" + currentSpeed);

                    //accellerating
                    if (currentSpeed > idAndSpeed.speed)
                    {
                        currentSpeed = idAndSpeed.speed + 0.2f;
                    }

                    //clamp speed
                    currentSpeed = math.clamp(currentSpeed, 0, currentPath.maxSpeed);

                    //set current speed
                    idAndSpeed.speed = currentSpeed;
                    indexPosition.value = indexPosition.value + (int)currentSpeed;
                    rawPosition.position = currentPath.pathNodes[indexPosition.value];
                    rawPosition.lookAtPosition = indexPosition.value < currentPath.nodesCount - currentPath.maxSpeed ? currentPath.pathNodes[indexPosition.value + currentPath.maxSpeed] : rawPosition.position;

                    //assign value back
                    chunkBodyIDAndSpeed[i] = idAndSpeed;
                    chunkRawPosition[i] = rawPosition;
                    chunkIndexPosition[i] = indexPosition;
                }
            }
        }

        //TODO: clean up unused
        //Job to move vehicle
        [BurstCompile]
        public struct MoveVehicleBodyJob : IJobChunk
        {
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyRawPosition> bodyRawPositionType;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyLength> bodyLengthType;
            public ArchetypeChunkComponentType<Translation> vehicletranslateType;
            public ArchetypeChunkComponentType<Rotation> vehicleRotationType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkRotation = chunk.GetNativeArray(vehicleRotationType);
                var chunkTranslation = chunk.GetNativeArray(vehicletranslateType);
                var chunkRawPosition = chunk.GetNativeArray(bodyRawPositionType);
                var chunkLength = chunk.GetNativeArray(bodyLengthType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var rawPosition = chunkRawPosition[i];

                    //set location
                    chunkTranslation[i] = new Translation { Value = rawPosition.position };

                    //set rotation
                    if (!rawPosition.lookAtPosition.Equals(rawPosition.position))
                    {
                        chunkRotation[i] = new Rotation { Value = quaternion.LookRotation(rawPosition.lookAtPosition - rawPosition.position, new float3(0, 1, 0)) };
                    }
                    
                }
            }
        }

        [BurstCompile]
        public struct FillHasMapJob : IJobChunk
        {
            [WriteOnly] public NativeMultiHashMap<int, VehiclePosition>.Concurrent map;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyIndexPosition> bodyIndexPositionType;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyLength> bodyLengthType;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyPathID> bodyPathIDType;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkIndexPosition = chunk.GetNativeArray(bodyIndexPositionType);
                var chunkLength = chunk.GetNativeArray(bodyLengthType);
                var chunkPathID = chunk.GetNativeArray(bodyPathIDType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    //add info to hashmap
                    map.Add(chunkPathID[i].value, new VehiclePosition { pos = chunkIndexPosition[i].value, length = chunkLength[i].value });
                }
            }
        }

        [BurstCompile]
        public struct ResolveMergingForPath : IJobChunk
        {
            [ReadOnly] public NativeArray<Path> paths;
            [ReadOnly] public ArchetypeChunkComponentType<PathMerge> pathMergeType;
            
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var mergeChunk = chunk.GetNativeArray(pathMergeType);
                
                for (int i=0; i<chunk.Count; i++)
                {
                    var mergePath = mergeChunk[i];

                    var mainPathToCheck = Path.Null;
                    var mainPath = Path.Null;

                    //get the main path to check for occupancy
                    for (int j=0; j<paths.Length; j++)
                    {
                        if (mergePath.linkedID == paths[j].id)
                        {
                            mainPathToCheck = paths[j];
                        }
                    }

                    //get the main path to set occupancy
                    for (int j = 0; j < paths.Length; j++)
                    {
                        if (mergePath.id == paths[j].id)
                        {
                            mainPath = paths[j];
                        }
                    }

                    byte lvalue = 0;
                    for (int j=mergePath.startScanPos; j<mergePath.endScanPos; j++)
                    {
                        lvalue |= mainPathToCheck.occupied[j];
                    }

                    if (CheckOccupied(lvalue, OccupiedType.Vehicle))
                    {
                        lvalue = SetOccupied(mainPath.occupied[mergePath.stopPos], true, OccupiedType.YieldMerging);
                    } else
                    {
                        lvalue = SetOccupied(mainPath.occupied[mergePath.stopPos], false, OccupiedType.YieldMerging);
                    }
                    mainPath.occupied[mergePath.stopPos] = lvalue;                
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
                        var lvalue = path.occupied[j];
                        lvalue = SetOccupied(lvalue, false, OccupiedType.Vehicle);
                        path.occupied[j] = lvalue;
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

        //job to rotate wheels
        //location will be handled by unity built-in system
        [BurstCompile]
        public struct MoveVehicleWheelJob : IJobChunk
        {
            public float3 cameraPosition;
            public float deltaTime;
            [DeallocateOnJobCompletion]
            [ReadOnly] public NativeArray<VehicleBodyIDAndSpeed> bodyIDAndSpeed;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleWheelMoveAndRotate> vehicleWheelType;
            [ReadOnly] public ArchetypeChunkComponentType<LocalToWorld> wheelLocationType;
            public ArchetypeChunkComponentType<Rotation> wheelRotationType;

            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var rotationChunk = chunk.GetNativeArray(wheelRotationType);
                var wheelChunk = chunk.GetNativeArray(vehicleWheelType);
                var locationChunk = chunk.GetNativeArray(wheelLocationType);

                for (int i=0; i<chunk.Count; i++)
                {
                    var location = locationChunk[i];
                    if (math.distance(location.Position, cameraPosition) >= 50.0f)
                    {
                        continue;
                    }

                    //find speed
                    var speed = 0.0f;
                    for (int j=0; j<bodyIDAndSpeed.Length; j++)
                    {
                        if (wheelChunk[i].id == bodyIDAndSpeed[j].id)
                        {
                            speed = bodyIDAndSpeed[j].speed;

                            break;
                        }
                    }

                    var rot = math.mul(math.normalize(rotationChunk[i].Value), quaternion.AxisAngle(new float3(1, 0, 0), (int)speed * deltaTime));
                    rotationChunk[i] = new Rotation { Value = rot };
                }

            }
        }

        public struct HideOutofPathVehicleJob : IJobChunk
        {
            public EntityCommandBuffer.Concurrent commandBuffer;
            [ReadOnly] public ArchetypeChunkComponentType<VehicleBodyWaitingStatus> bodyWaitingType;
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
            {
                var chunkWaiting = chunk.GetNativeArray(bodyWaitingType);

                for (int i=0; i<chunk.Count; i++)
                {

                }

            }
        }

    }
}