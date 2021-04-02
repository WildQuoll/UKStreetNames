using System.Collections.Generic;
using ColossalFramework.Math;
using UnityEngine;

namespace UKStreetNames
{
    class MotorwayInfo
    {
        // road should belong to RoadCategory.MOTORWAY
        public MotorwayInfo(Road road)
        {
            var helper = new NetHelper();

            foreach (var nodeId in road.m_endNodeIds)
            {
                m_endNodes.Add(VectorUtils.XZ(helper.GetNodePosition(nodeId)));
            }

            if (m_endNodes.Count > 1)
            {
                var horizontalVector = new Vector2(1.0f, 0.0f);

                foreach (var segmentId in road.m_segmentIds)
                {
                    var angle = Vector2.Angle(helper.GetSegmentOrientation(segmentId), horizontalVector);

                    if (angle > 90.0f)
                    {
                        angle = 180.0f - angle;
                    }

                    var segmentLanesLength = helper.GetSegmentTotalLaneLength(segmentId);
                    m_totalLaneLength += segmentLanesLength;
                    m_orientationAngle += angle * segmentLanesLength;
                }
                m_orientationAngle /= m_totalLaneLength;

                m_length = road.m_length;
            }

            segmentIds = road.m_segmentIds;
        }

        public HashSet<ushort> segmentIds;

        private float m_orientationAngle = 0.0f; // 0 (horizontal) to 90 (vertical)
        private float m_totalLaneLength = 0.0f;
        private float m_length = 0.0f;
        private List<Vector2> m_endNodes = new List<Vector2>();

        public float CalculateSimilarity(MotorwayInfo other)
        {
            if (m_endNodes.Count < 2 || other.m_endNodes.Count < 2)
            {
                return 0.0f;
            }

            float score = Mathf.Min(m_totalLaneLength, other.m_totalLaneLength) / Mathf.Max(m_totalLaneLength, other.m_totalLaneLength);
            //Debug.Log("Length similarity: " + score);
            var orientationScore = 1.0f - Mathf.Abs(m_orientationAngle - other.m_orientationAngle) / 90.0f;
            orientationScore *= orientationScore;
            //Debug.Log("Orientation similarity: " + orientationScore);
            score *= orientationScore;

            // Only compare 2 closest matching node pairs.
            var nodeDistances = new List<float>();
            foreach (var node in m_endNodes)
            {
                float bestDistanceSq = float.MaxValue;
                foreach (var otherNode in other.m_endNodes)
                {
                    var distanceSq = (node - otherNode).sqrMagnitude;
                    //Debug.Log("Distance between " + node + " and " + otherNode + " is " + Mathf.Sqrt(distanceSq));

                    bestDistanceSq = Mathf.Min(bestDistanceSq, distanceSq);
                }
                nodeDistances.Add(bestDistanceSq);
            }

            nodeDistances.Sort();
            //Debug.Log("Lengths:" + m_length + " vs " + other.m_length);
            var length = Mathf.Min(m_length, other.m_length);
            var nodeDistanceThreshold = Mathf.Max(64000.0f, Mathf.Pow(length * 0.3125f, 2.0f));
            //Debug.Log("Node distance threshold is: " + Mathf.Sqrt(nodeDistanceThreshold));
            for (int i = 0; i < 2; ++i)
            {
                var nodeDistanceScore = Mathf.Max(0.0f, 1.0f - nodeDistances[i] / nodeDistanceThreshold);
                //Debug.Log(i + "-th node closeness: " + nodeDistanceScore + " (Distance: " + Mathf.Sqrt(nodeDistances[i]) + ")");
                score *= nodeDistanceScore;
            }

            return score;
        }
    }
}
