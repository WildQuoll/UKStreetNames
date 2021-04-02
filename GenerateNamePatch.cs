using HarmonyLib;
using UnityEngine;

namespace UKStreetNames
{
    public static class RoadNameGenerator
    {
        private static RoadNameCache roadCache = new RoadNameCache();
        private static RoadNameCache tunnelCache = new RoadNameCache();
        private static RoadNameCache bridgeCache = new RoadNameCache();

        public static void ClearCache()
        {
            roadCache.Clear();
            tunnelCache.Clear();
            bridgeCache.Clear();
        }

        public static string GenerateName(ushort segmentID, ushort nameSeed, RoadCategory? overrideCategory)
        {
            var cache = overrideCategory == null ? roadCache : (overrideCategory.Value == RoadCategory.BRIDGE ? bridgeCache : tunnelCache);

            string cachedName = cache.FindName(segmentID);
            if (cachedName == null)
            {
                var road = new Road(segmentID);

                if (road.m_predominantCategory == RoadCategory.MOTORWAY)
                {
                    NameListManager.TryToMatchToExistingMotorway(segmentID, ref road);
                }

                string name = NameListManager.GenerateRoadName(ref road, overrideCategory);
                cache.AddToCache(road, name);
                return name;
            }
            else
            {
                return cachedName;
            }
        }
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(RoadAI), "GenerateName")]
    public static class GenerateRoadNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ushort segmentID, ref NetSegment data, NetInfo ___m_info, ref string __result)
        {
            //Debug.Log("Generating name for " + segmentID);
            __result = RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, null);
        }
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(RoadTunnelAI), "GenerateName")]
    public static class GenerateRoadTunnelNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ushort segmentID, ref NetSegment data, NetInfo ___m_info, ref string __result)
        {
            __result = RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, RoadCategory.TUNNEL);
        }
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(RoadBridgeAI), "GenerateName")]
    public static class GenerateRoadBridgeNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ushort segmentID, ref NetSegment data, NetInfo ___m_info, ref string __result)
        {
            __result = RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, RoadCategory.BRIDGE);
        }
    }
}