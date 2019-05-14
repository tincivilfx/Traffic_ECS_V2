﻿using Unity.Entities;
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
        public int length;
        public int speed;
        public int currentPos;
        public int id;
        public int currentPathID;
        public float3 location;     //used for wheels
        public float3 lookAtLocation;
        public quaternion rotation;

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
        public byte id;
        public byte maxSpeed;
        public int nodesCount;

        [NativeDisableUnsafePtrRestriction]
        public float3* pathNodes;

        [NativeDisableUnsafePtrRestriction]
        public bool* occupied;
        
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

}