using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace CivilFX.TrafficECS
{

    public struct TrafficSettingsData : IComponentData
    {
        public int vehicleCount;
    }

    public struct VehicleBody : IComponentData
    {
        public Entity prefab;
        public int length;
        public int id;
    }

    public struct VehicleBodyMoveAndRotate : IComponentData
    {
        public static VehicleBodyMoveAndRotate Null { get; }
        public bool waiting;
        public int length;
        public int speed;
        public int currentPos;
        public int id;
        public int currentPathID;
        public float3 location;     //used for wheels
        public float3 lookAtLocation;
    }

    public struct VehicleWheelMoveAndRotate : IComponentData
    {
        public Entity parent;
        public int id;
        public float3 positionOffset;
        public float3 rotationOffset;
    }


    public struct VehicleWheel : IComponentData
    {
        public Entity prefab;
        public float3 positionOffset;
        public float3 rotationOffset;
        public int id;
    }

    public unsafe struct Path : IComponentData
    {
        public static Path Null { get; }
        public byte id;
        public byte maxSpeed;
        public byte linkedCount;
        public TrafficPathType type;
        public int nodesCount;

        [NativeDisableUnsafePtrRestriction]
        public float3* pathNodes;

        [NativeDisableUnsafePtrRestriction]
        public byte* occupied;
      
        [NativeDisableUnsafePtrRestriction]
        public PathLinkedData* linked;       
    }

    public unsafe struct PathMerge : IComponentData
    {
        public byte id;
        public byte linkedID;
        public int startScanPos;
        public int endScanPos;
        public int stopPos;
    }

    //used to controll an intersection
    public unsafe struct TrafficSignalNode : IComponentData
    {
        public byte currentFrameIndex;
        public byte setsCount;
        public byte sequenceCount;
        public float currentTime;
        public float elapsedTime;
        [NativeDisableUnsafePtrRestriction]
        public SignalSet* sets;

        [NativeDisableUnsafePtrRestriction]
        public SignalFrame* sequence;
    }

    public unsafe struct SignalSet
    {
        public byte id;
        public byte pathsCount;
        [NativeDisableUnsafePtrRestriction]
        public byte* pathIDs;

        [NativeDisableUnsafePtrRestriction]
        public int* stopPoses;
    }


    public struct SignalFrame
    {
        public float time;
        public byte setID;
        public TrafficPathType type;
        public bool active;
    }

    public struct WaitingVehicle : IComponentData
    {
        //for tagging purposing
    }


    /*Raw data
     */

    public struct VehiclePosition
    {
        public int pos;
        public int length;
    }

    public struct VehicleInitData
    {
        public byte pathID;
        public int pos;
    }

    public struct PathLinkedData
    {
        public byte linkedID;
        public byte chance;
        public int transitionNode;
        public int connectingNode;
    }



}