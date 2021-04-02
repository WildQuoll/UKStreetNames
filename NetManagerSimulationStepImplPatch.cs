using ColossalFramework;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UKStreetNames
{
    // This patch ensures that whenever a road segment is updated, all other segments which form part
    // of the same named road are also updated.
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(NetManager), "SimulationStepImpl")]
    class NetManagerSimulationStepImplPatch
    {
        [HarmonyPrefix]
        public static void Prefix(int subStep, bool ___m_segmentsUpdated, ulong[] ___m_updatedSegments)
        {
            if (!___m_segmentsUpdated)
            {
                return;
            }

            RoadNameGenerator.ClearCache();

            var segmentIds = GetCorrespondingSegmentIds(___m_updatedSegments);

            var affectedSegmentIds = new HashSet<ushort>();

            var netHelper = new NetHelper();
            while(segmentIds.Count > 0)
            {
                var segmentId = segmentIds.First();

                if ( !netHelper.IsValidSegment(segmentId) )
                {
                    segmentIds.Remove(segmentId);
                    continue;
                }

                var road = new Road(segmentId);
                affectedSegmentIds.UnionWith(road.m_segmentIds);
                segmentIds.ExceptWith(road.m_segmentIds);
            }
            FlagSegmentsAsUpdated(affectedSegmentIds, ref ___m_updatedSegments);
        }

        private static HashSet<ushort> GetCorrespondingSegmentIds(ulong[] segmentInternalIdStore)
        {
            var segmentIds = new HashSet<ushort>();

            // From NetManager.SimulationStepImpl
            int count = segmentInternalIdStore.Length;
            for (int i = 0; i < count; i++)
            {
                ulong segmentInternalStoreId = segmentInternalIdStore[i];
                if (segmentInternalStoreId != 0UL)
                {
                    for (int m = 0; m < 64; m++)
                    {
                        if ((segmentInternalStoreId & 1UL << m) != 0UL)
                        {
                            segmentIds.Add((ushort)(i << 6 | m));
                        }
                    }
                }
            }

            return segmentIds;
        }

        public static void FlagSegmentsAsUpdated(HashSet<ushort> ids, ref ulong[] store)
        {
            // From NetManager.UpdateSegment
            foreach (var id in ids)
            {
                store[id >> 6] |= (ulong)(1L << id);
            }
        }
    }
}
