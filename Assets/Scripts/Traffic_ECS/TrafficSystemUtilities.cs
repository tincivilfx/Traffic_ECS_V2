using Unity.Mathematics;

namespace CivilFX.TrafficECS
{
    public partial class TrafficSystem
    {
        public enum OccupiedType
        {
            Vehicle,
            TrafficSignal,
            YieldMerging
        }

        public static readonly byte VEHICLE_OCCUPIED_BIT = 1;
        public static readonly byte TRAFFIC_SIGNAL_OCCUPIED_BIT = 2;
        public static readonly byte YIELD_FOR_MERGING_OCCUPIED_BIT = 4;

        public static readonly int MAX_SCAN_DISTANCE = 1000;
        public static readonly byte BYTE_INVALID = 255;

        public static readonly float3 OUT_OF_WORLD_POSITION = new float3(-1000, -1000, -1000);

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
                    }
                    else
                    {
                        //clear bit
                        value = (byte)(value & (~VEHICLE_OCCUPIED_BIT));
                    }
                    break;
                case OccupiedType.TrafficSignal:
                    if (occupied)
                    {
                        value = (byte)(value | TRAFFIC_SIGNAL_OCCUPIED_BIT);
                    }
                    else
                    {
                        value = (byte)(value & (~TRAFFIC_SIGNAL_OCCUPIED_BIT));
                    }
                    break;
                case OccupiedType.YieldMerging:
                    if (occupied)
                    {
                        value = (byte)(value | YIELD_FOR_MERGING_OCCUPIED_BIT);
                    }
                    else
                    {
                        value = (byte)(value & (~YIELD_FOR_MERGING_OCCUPIED_BIT));
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
                case OccupiedType.YieldMerging:
                    return (value & YIELD_FOR_MERGING_OCCUPIED_BIT) != 0;
            }
            return true;
        }


    }
}