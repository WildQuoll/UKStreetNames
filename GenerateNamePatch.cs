using HarmonyLib;
using UnityEngine;

namespace UKStreetNames
{
    public static class RoadNameGenerator
    {
        //private static long timeToDate = 0; // for debugging
        //private static int cacheHits = 0;
        //private static int cacheMisses = 0;

        private static RoadNameCache roadCache = new RoadNameCache();
        private static RoadNameCache tunnelCache = new RoadNameCache();
        private static RoadNameCache bridgeCache = new RoadNameCache();

        public static void ClearCache()
        {
            roadCache.Clear();
            tunnelCache.Clear();
            bridgeCache.Clear();
        }

        public static void GenerateName(ushort segmentID, ushort nameSeed, ref string result, RoadCategory? overrideCategory)
        {
            //var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var cache = overrideCategory == null ? roadCache : (overrideCategory.Value == RoadCategory.BRIDGE ? bridgeCache : tunnelCache);

            string cachedName = cache.FindName(segmentID);
            if (cachedName == null)
            {
                var road = new Road(segmentID);

                if (road.m_predominantCategory == RoadCategory.MOTORWAY)
                {
                    NameListManager.TryToMatchToExistingMotorway(segmentID, ref road);
                }

                result = NameListManager.GenerateRoadName(ref road, overrideCategory);
                //Debug.Log("Generated new name: " + result + " (" + road.m_predominantCategory + "/" + overrideCategory + "/" + road.m_roadFeatures + ")");
                cache.AddToCache(road, result);

                //cacheMisses += 1;
            }
            else
            {
                //Debug.Log("Retrieved: " + cachedName + " from cache");
                result = cachedName;
                //cacheHits += 1;
            }

            //stopwatch.Stop();
            //timeToDate += stopwatch.ElapsedMilliseconds;
            //Debug.Log("Total time taken generating names so far: " + timeToDate / 1000.0f + "s");
            //Debug.Log("Cache hits so far: " + (float)cacheHits / (cacheHits + cacheMisses) * 100.0f + "%");
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
            RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, ref __result, null);
        }
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(RoadTunnelAI), "GenerateName")]
    public static class GenerateRoadTunnelNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ushort segmentID, ref NetSegment data, NetInfo ___m_info, ref string __result)
        {
            RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, ref __result, RoadCategory.TUNNEL);
        }
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(RoadBridgeAI), "GenerateName")]
    public static class GenerateRoadBridgeNamePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ushort segmentID, ref NetSegment data, NetInfo ___m_info, ref string __result)
        {
            RoadNameGenerator.GenerateName(segmentID, data.m_nameSeed, ref __result, RoadCategory.BRIDGE);
        }
    }
}