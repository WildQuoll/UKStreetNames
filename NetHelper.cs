using System.Collections.Generic;
using ColossalFramework.Math;
using ColossalFramework;
using UnityEngine;

namespace UKStreetNames
{
    public class NetHelper
    {
        NetManager manager;

        public NetHelper()
        {
            manager = Singleton<NetManager>.instance;
        }
        
        public bool IsValidSegment(ushort id)
        {
            return manager.m_segments.m_buffer[id].Info != null;
        }

        public NetSegment GetSegmentByID(ushort id)
        {
            return manager.m_segments.m_buffer[id];
        }

        public ushort GetSegmentNameSeed(ushort id)
        {
            return manager.m_segments.m_buffer[id].m_nameSeed;
        }

        public NetNode GetNodeByID(ushort id)
        {
            return manager.m_nodes.m_buffer[id];
        }

        public bool IsNodeDeadEnd(ushort id)
        {
            return (manager.m_nodes.m_buffer[id].m_flags & NetNode.Flags.End) != 0;
        }

        public bool IsNodeCrossroad(ushort id)
        {
            return manager.m_nodes.m_buffer[id].CountSegments() > 2;
        }

        public float GetSegmentTurningAngleDeg(ushort id)
        {
            var segment = manager.m_segments.m_buffer[id];
            return Vector3.Angle(segment.m_startDirection, -segment.m_endDirection);
        }

        public float GetSegmentLength(ushort id)
        {
            return manager.m_segments.m_buffer[id].m_averageLength;
        }

        public float GetSegmentTotalLaneLength(ushort id)
        {
            var segment = manager.m_segments.m_buffer[id];
            var laneCount = segment.Info.m_backwardVehicleLaneCount + segment.Info.m_forwardVehicleLaneCount;
            return segment.m_averageLength * laneCount;
        }

        public float GetStraightLineDistanceBetweenNodes(ushort id1, ushort id2)
        {
            var startNode = manager.m_nodes.m_buffer[id1];
            var endNode = manager.m_nodes.m_buffer[id2];

            return VectorUtils.LengthXZ(startNode.m_position - endNode.m_position);
        }

        public float AngleDegBetweenSegmentsAtNode(ushort nodeId, ushort segId1, ushort segId2)
        {
            var seg1 = manager.m_segments.m_buffer[segId1];
            var seg2 = manager.m_segments.m_buffer[segId2];

            var segDir1 = (seg1.m_startNode == nodeId) ? seg1.m_startDirection : seg1.m_endDirection;
            var segDir2 = (seg2.m_startNode == nodeId) ? seg2.m_startDirection : seg2.m_endDirection;

            return Vector3.Angle(segDir1, -segDir2);
        }

        public bool IsSegmentStraight(ushort id)
        {
            return manager.m_segments.m_buffer[id].IsStraight();
        }

        public float GetNodeElevation(ushort id)
        {
            return manager.m_nodes.m_buffer[id].m_position.y;
        }

        public float GetSegmentSlope(ushort id)
        {
            var segment = manager.m_segments.m_buffer[id];
            var startNodeElevation = manager.m_nodes.m_buffer[segment.m_startNode].m_position.y;
            var endNodeElevation = manager.m_nodes.m_buffer[segment.m_endNode].m_position.y;

            return Mathf.Abs(startNodeElevation - endNodeElevation) / segment.m_averageLength;
        }

        public void GetBounds(ref HashSet< ushort > nodeIds, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            minZ = float.MaxValue;

            maxX = float.MinValue;
            maxZ = float.MinValue;

            foreach(var nodeId in nodeIds)
            {
                var node = manager.m_nodes.m_buffer[nodeId];
                minX = Mathf.Min(minX, node.m_position.x);
                minZ = Mathf.Min(minZ, node.m_position.z);
                maxX = Mathf.Max(maxX, node.m_position.x);
                maxZ = Mathf.Max(maxZ, node.m_position.z);
            }
        }

        public Bounds GetSegmentBounds(ushort segmentId)
        {
            return manager.m_segments.m_buffer[segmentId].m_bounds;
        }

        private static uint GetNumCarLanes(ref NetInfo info)
        {
            uint count = 0;

            var carLaneTypes = (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
            var carVehicleTypes = (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Trolleybus);

            foreach (var lane in info.m_lanes)
            {
                if ((lane.m_laneType & carLaneTypes) != 0)
                {
                    if ((lane.m_vehicleType & carVehicleTypes) != 0)
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }

        public RoadCategory CategoriseRoadSegment(ushort segmentId)
        {
            var segment = manager.m_segments.m_buffer[segmentId];

            var info = segment.Info;
            if (info == null || !info.m_netAI.GetType().IsSubclassOf(typeof(RoadBaseAI)))
            {
                return RoadCategory.NONE;
            }

            bool highwayRules = (info.m_netAI as RoadBaseAI).m_highwayRules;

            if (highwayRules && info.m_averageVehicleLaneSpeed > 1.2)
            {
                if (info.m_hasForwardVehicleLanes && info.m_hasBackwardVehicleLanes)
                {
                    return RoadCategory.MAJOR_RURAL;
                }
                else
                {
                    return RoadCategory.MOTORWAY;
                }
            }

            uint numCarLanes = GetNumCarLanes(ref info);

            if (numCarLanes == 0)
            {
                if (!info.m_hasPedestrianLanes)
                {
                    return RoadCategory.NONE;
                }

                if (info.m_halfWidth < 4.0f)
                {
                    return RoadCategory.MINOR_PEDESTRIAN;
                }
                else
                {
                    return RoadCategory.MAJOR_PEDESTRIAN;
                }
            }

            if (info.m_averageVehicleLaneSpeed <= 0.5f && info.m_hasPedestrianLanes) // <=25km/h
            {
                if (info.m_halfWidth < 4.0f)
                {
                    return RoadCategory.MINOR_PEDESTRIAN;
                }
                else
                {
                    return RoadCategory.MAJOR_PEDESTRIAN;
                }
            }

            if (!info.m_createPavement)
            {
                if (info.m_averageVehicleLaneSpeed > 1.2 || numCarLanes > 2)
                {
                    return RoadCategory.MAJOR_RURAL;
                }
                else
                {
                    return RoadCategory.MINOR_RURAL;
                }
            }

            if (numCarLanes > 3)
            {
                return RoadCategory.MAJOR_URBAN;
            }

            if (info.m_halfWidth >= 12.0f)
            {
                return RoadCategory.MAJOR_URBAN;
            }

            return RoadCategory.MINOR_URBAN;
        }

        public ushort GetSegmentStartNodeId(ushort segmentId)
        {
            return manager.m_segments.m_buffer[segmentId].m_startNode;
        }

        public ushort GetSegmentEndNodeId(ushort segmentId)
        {
            return manager.m_segments.m_buffer[segmentId].m_endNode;
        }

        public Vector3 GetSegmentStartNodePosition(ushort segmentId)
        {
            return manager.m_nodes.m_buffer[manager.m_segments.m_buffer[segmentId].m_startNode].m_position;
        }

        public Vector3 GetSegmentEndNodePosition(ushort segmentId)
        {
            return manager.m_nodes.m_buffer[manager.m_segments.m_buffer[segmentId].m_endNode].m_position;
        }

        public Vector3 GetNodePosition(ushort nodeId)
        {
            return manager.m_nodes.m_buffer[nodeId].m_position;
        }

        public Vector3 GetSegmentStartDirection(ushort segmentId)
        {
            return manager.m_segments.m_buffer[segmentId].m_startDirection;
        }

        public Vector3 GetSegmentEndDirection(ushort segmentId)
        {
            return manager.m_segments.m_buffer[segmentId].m_endDirection;
        }

        public void SetSegmentNameSeed(ushort segmentId, ushort nameSeed)
        {
            manager.m_segments.m_buffer[segmentId].m_nameSeed = nameSeed;
        }

        public Vector2 GetSegmentOrientation(ushort segmentId)
        {
            var segment = manager.m_segments.m_buffer[segmentId];
            var startNodePosition = VectorUtils.XZ(manager.m_nodes.m_buffer[segment.m_startNode].m_position);
            var endNodePosition = VectorUtils.XZ(manager.m_nodes.m_buffer[segment.m_endNode].m_position);

            var orientation = startNodePosition - endNodePosition;
            return orientation.normalized;
        }

        public HashSet<ushort> GetClosestSegmentIds(ushort segmentId)
        {
            var segment = manager.m_segments.m_buffer[segmentId];

            var segmentsArray = new ushort[16];
            int count;
            manager.GetClosestSegments(segment.m_middlePosition, segmentsArray, out count);

            var segments = new HashSet<ushort>();

            for(int i = 0; i < count; ++i)
            {
                segments.Add(segmentsArray[i]);
            }

            return segments;
        }

        public bool IsOneWay(ushort segmentId)
        {
            var segment = manager.m_segments.m_buffer[segmentId];
            return segment.Info.m_hasForwardVehicleLanes ^ segment.Info.m_hasBackwardVehicleLanes;
        }
    }
}
