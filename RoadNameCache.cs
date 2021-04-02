using System.Collections.Generic;

namespace UKStreetNames
{
    public class RoadNameCache
    {
        private Dictionary<ushort, string> segmentIdsToNames = new Dictionary<ushort, string>();

        public void Clear()
        {
            segmentIdsToNames.Clear();
        }

        public string FindName(ushort segmentId)
        {
            if (!segmentIdsToNames.ContainsKey(segmentId))
            {
                return null;
            }

            return segmentIdsToNames[segmentId];
        }

        public void AddToCache(Road road, string name)
        {
            foreach (var segmentId in road.m_segmentIds)
            {
                if (segmentIdsToNames.ContainsKey(segmentId))
                {
                    segmentIdsToNames[segmentId] = name;
                }
                else
                {
                    segmentIdsToNames.Add(segmentId, name);
                }
            }
        }
    }
}
