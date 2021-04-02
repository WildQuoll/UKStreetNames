using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.Math;
using System;

namespace UKStreetNames
{
    static class NameListManager
    {
        public static void TryToMatchToExistingMotorway(ushort segmentId, ref Road road)
        {
            var helper = new NetHelper();

            var thisSegmentNameSeed = helper.GetSegmentNameSeed(segmentId);

            HashSet<ushort> nearbySegmentIds = helper.GetClosestSegmentIds(segmentId);

            float bestScore = 0.0f;
            ushort bestNameSeed = 0;

            while (nearbySegmentIds.Count > 0)
            {
                var id = nearbySegmentIds.First();
                var seedId = helper.GetSegmentNameSeed(id);

                if (thisSegmentNameSeed == seedId)
                {
                    nearbySegmentIds.Remove(id);
                    continue;
                }

                var otherRoad = new Road(id);
                nearbySegmentIds.ExceptWith(otherRoad.m_segmentIds);

                if (otherRoad.m_predominantCategory != RoadCategory.MOTORWAY)
                {
                    continue;
                }

                var thisMotorway = new MotorwayInfo(road);
                var otherMotorway = new MotorwayInfo(otherRoad);

                float similarity = thisMotorway.CalculateSimilarity(otherMotorway);

                if (similarity > bestScore)
                {
                    bestScore = similarity;
                    bestNameSeed = otherRoad.m_nameSeed;
                }
            }

            if (bestScore > 0.75f)
            {
                //Debug.Log("Renaming " + road.m_nameSeed + "/" + GenerateMotorwayName(road.m_nameSeed) + " to " + bestNameSeed + "/" + GenerateMotorwayName(bestNameSeed));
                road.SetNameSeed(bestNameSeed);
            }
        }

        private static string GenerateMotorwayName(ushort seed)
        {
            string name = "";
            var randomiser = new Randomizer(seed);

            if (seed % 7 == 0)
            {
                name = "A" + Math.Min(randomiser.UInt32(10, 999), randomiser.UInt32(10, 999)) + "(M)";
            }
            else
            {
                name = "M" + Math.Min(randomiser.UInt32(1, 179), randomiser.UInt32(1, 179));
            }

            return name;
        }

        public static string GenerateRoadName(ref Road road, RoadElevation elevation)
        {
            var seed = road.m_nameSeed;
            var category = road.m_predominantCategory;

            if (category == RoadCategory.NONE)
            {
                return "";
            }

            if (category == RoadCategory.MOTORWAY)
            {
                var laneLength = road.CalculateTotalLaneLength();

                if (laneLength < 2000.0f)
                {
                    return "";
                }

                return GenerateMotorwayName(seed);
            }

            if (elevation == RoadElevation.BRIDGE)
            {
                category = RoadCategory.BRIDGE;
            }
            else if (elevation == RoadElevation.TUNNEL)
            {
                category = RoadCategory.TUNNEL;
            }

            Randomizer prefixRandomiser = new Randomizer(seed);
            Randomizer suffixRandomiser = new Randomizer(seed);

            string suffix = GetRandomSuffix(ref suffixRandomiser, category, road.m_roadFeatures);

            string prefix;

            int attempts = 0;
            do
            {
                prefix = GetRandomPrefix(ref prefixRandomiser);
                attempts += 1;

                if (attempts > 30)
                {
                    Debug.Log("WQ:UKSN: Could not find suitable prefix!");
                    break;
                }
            }
            while (prefix.ToUpperInvariant().Contains(suffix.ToUpperInvariant()) || !IsValidPrefix(prefix, road.m_roadFeatures, category));

            if (prefix.EndsWith("$"))
            {
                return prefix.TrimEnd('$');
            }

            return prefix + " " + suffix;
        }

        private static string GetRandomPrefix(ref Randomizer randomiser)
        {
            var prefixIndexOffset = Singleton<SimulationManager>.instance.m_metaData.m_startingDateTime.Millisecond;
            var index = (randomiser.Int32(0, ROAD_PREFIXES.Count - 1) + prefixIndexOffset) % ROAD_PREFIXES.Count;
            return ROAD_PREFIXES[index];
        }

        private static string GetRandomSuffix(ref Randomizer randomiser, RoadCategory category, RoadFeature features)
        {
            if (!ROAD_SUFFIXES[category].ContainsKey(features))
            {
                ROAD_SUFFIXES[category].Add(features, new SuffixList(ROAD_SUFFIX_DATA, category, features));
            }

            return ROAD_SUFFIXES[category][features].GetRandomSuffix(ref randomiser);
        }

        private static bool IsValidPrefix(string prefix, RoadFeature features, RoadCategory category)
        {
            return ROAD_PREFIX_DATA[prefix].IsValidFor(features, category);
        }

        class SuffixList
        {
            public SuffixList(Dictionary<string, SuffixProperties> list, RoadCategory category, RoadFeature features)
            {
                //Debug.Log("Generating suffix list for " + category + "/" + features);

                var probabilities = new SortedDictionary<string, double>();
                double combinedP = 0.0;
                foreach(var l in list)
                {
                    double p = l.Value.GetProbability(category, features);

                    if(p > 0.0)
                    {
                        combinedP += p;
                        probabilities.Add(l.Key, combinedP);
                    }
                }

                //Debug.Log("Options are:");
                //var s = "";
                double prev = 0.0;

                suffixes = new SortedList<uint, string>();
                foreach(var p in probabilities)
                {
                    //s += ((p.Value - prev) / combinedP * 100.0) + "% " + p.Key + "\n";
                    prev = p.Value;

                    suffixes.Add((uint)Math.Round((p.Value / combinedP) * uint.MaxValue), p.Key);
                }

                if(suffixes.Count == 0)
                {
                    Debug.Log("WQ:UKSN: No suitable suffixes found for " + category + "/" + features);
                    suffixes.Add(uint.MaxValue, "STREET_SUFFIX_MISSING");
                }

                //Debug.Log(s);
            }

            private static int BinarySearch<T>(IList<T> list, T value)
            {
                var comp = Comparer<T>.Default;
                int lo = 0, hi = list.Count - 1;
                while (lo < hi)
                {
                    int m = (hi + lo) / 2;
                    if (comp.Compare(list[m], value) < 0) lo = m + 1;
                    else hi = m - 1;
                }
                if (comp.Compare(list[lo], value) < 0) lo++;
                return lo;
            }

            public string GetRandomSuffix(ref Randomizer randomiser)
            {
                var v = randomiser.UInt32(uint.MaxValue);
                int index = BinarySearch(suffixes.Keys, v);
                return suffixes[suffixes.Keys[index]];
            }

            SortedList<uint, string> suffixes;
        }

        class SuffixProperties
        {
            public SuffixProperties(Dictionary<RoadCategory, double> categoryFrequencies,
                                    Dictionary<RoadFeature, double> featureModifiers,
                                    Dictionary<RoadFeature, double> negativeFeatureModifiers)
            {
                this.categoryFrequencies = categoryFrequencies;
                this.featureModifiers = featureModifiers;
                this.negativeFeatureModifiers = negativeFeatureModifiers;
            }

            Dictionary<RoadCategory, double> categoryFrequencies;
            Dictionary<RoadFeature, double> featureModifiers;
            Dictionary<RoadFeature, double> negativeFeatureModifiers;

            public double GetProbability(RoadCategory category, RoadFeature features)
            {
                if(!categoryFrequencies.ContainsKey(category))
                {
                    return 0.0;
                }

                double probability = categoryFrequencies[category];

                foreach (var modifier in featureModifiers)
                {
                    if ((modifier.Key & features) != 0)
                    {
                        probability *= modifier.Value;
                    }
                }

                foreach (var modifier in negativeFeatureModifiers)
                {
                    if ((modifier.Key & features) == 0)
                    {
                        probability *= modifier.Value;
                    }
                }

                return probability;
            }
        }

        class PrefixProperties
        {
            public PrefixProperties(uint probability, bool noSuffix, RoadCategory allowedCategories = RoadCategory.ALL, RoadFeature forbiddenFeatures = RoadFeature.NONE, RoadFeature requiredFeatures = RoadFeature.NONE)
            {
                this.probability = probability;
                this.forbiddenFeatures = forbiddenFeatures;
                this.requiredFeatures = requiredFeatures;
                this.allowedCategories = allowedCategories;
                this.noSuffix = noSuffix;
            }

            public uint probability;
            RoadFeature forbiddenFeatures;
            RoadFeature requiredFeatures;
            RoadCategory allowedCategories;
            bool noSuffix;

            public bool IsValidFor(RoadFeature features, RoadCategory category)
            {
                if (noSuffix && (category == RoadCategory.BRIDGE || category == RoadCategory.TUNNEL))
                {
                    return false;
                }

                return (features & forbiddenFeatures) == 0 &&
                       (features & requiredFeatures) == requiredFeatures &&
                       (allowedCategories & category) == category;
            }
        }

        private static List<string> GeneratePrefixList(Dictionary<string, PrefixProperties> entries)
        {
            var list = new List<string>();

            foreach (var entry in entries)
            {
                for (int i = 0; i < entry.Value.probability; ++i)
                {
                    list.Add(entry.Key);
                }
            }

            return list;
        }

        private static Dictionary<RoadCategory, Dictionary<RoadFeature, SuffixList>> InitSuffixLists()
        {
            var d = new Dictionary<RoadCategory, Dictionary<RoadFeature, SuffixList>>();

            foreach (var cat in new []{
                RoadCategory.MINOR_PEDESTRIAN,
                RoadCategory.MAJOR_PEDESTRIAN,
                RoadCategory.MINOR_URBAN,
                RoadCategory.MAJOR_URBAN,
                RoadCategory.MINOR_RURAL,
                RoadCategory.MAJOR_RURAL,
                RoadCategory.MOTORWAY,
                RoadCategory.SQUARE,
                RoadCategory.CIRCLE,
                RoadCategory.OVAL,
                RoadCategory.LOOP,
                RoadCategory.BRIDGE,
                RoadCategory.TUNNEL })
            {
                d.Add(cat, new Dictionary<RoadFeature, SuffixList>());
            }

            return d;
        }

        private static readonly Dictionary<string, SuffixProperties> ROAD_SUFFIX_DATA = LoadSuffixesFromCsv("suffixes.csv");
        private static readonly Dictionary<RoadCategory, Dictionary<RoadFeature, SuffixList>> ROAD_SUFFIXES = InitSuffixLists();

        private static readonly Dictionary<string, PrefixProperties> ROAD_PREFIX_DATA = LoadPrefixesFromCsv("prefixes.csv");
        private static readonly List<string> ROAD_PREFIXES = GeneratePrefixList(ROAD_PREFIX_DATA);

        private static double FrequencyFromString(string s)
        {
            switch (s)
            {
                case "VERY_COMMON":
                    return 9.0;
                case "COMMON":
                    return 3.0;
                case "INFREQUENT":
                    return 1.0;
                case "RARE":
                    return 1.0 / 3.0;
                case "VERY_RARE":
                    return 1.0 / 9.0;
                case "UNUSED":
                    return 0.0;
            }

            return 1.0;
        }

        private static RoadFeature RoadFeatureFromString(string s)
        {
            switch (s)
            {
                case "DEADEND":
                    return RoadFeature.DEADEND;
                case "CRESCENT":
                    return RoadFeature.CRESCENT;
                case "HILL":
                    return RoadFeature.HILL;
                case "WATERFRONT":
                    return RoadFeature.WATERFRONT;
                case "NEAR_WATER":
                    return RoadFeature.NEAR_WATER;
                case "WITH_BRIDGE":
                    return RoadFeature.INCLUDES_BRIDGE;
                case "WITH_TUNNEL":
                    return RoadFeature.INCLUDES_TUNNEL;
                case "CROSSES_WATER":
                    return RoadFeature.CROSSES_WATER;
                case "SHORT":
                    return RoadFeature.SHORT;
                case "LONG":
                    return RoadFeature.LONG;
                case "ONE_WAY":
                    return RoadFeature.ONE_WAY;
            }

            return RoadFeature.NONE;
        }

        private static RoadCategory RoadCategoryFromString(string s)
        {
            switch (s)
            {
                case "MINOR_PEDESTRIAN":
                    return RoadCategory.MINOR_PEDESTRIAN;
                case "MAJOR_PEDESTRIAN":
                    return RoadCategory.MAJOR_PEDESTRIAN;
                case "MINOR_RURAL":
                    return RoadCategory.MINOR_RURAL;
                case "MAJOR_RURAL":
                    return RoadCategory.MAJOR_RURAL;
                case "MINOR_URBAN":
                    return RoadCategory.MINOR_URBAN;
                case "MAJOR_URBAN":
                    return RoadCategory.MAJOR_URBAN;
                case "MOTORWAY":
                    return RoadCategory.MOTORWAY;
                case "PEDESTRIAN":
                    return RoadCategory.MINOR_PEDESTRIAN | RoadCategory.MAJOR_PEDESTRIAN;
                case "URBAN":
                    return RoadCategory.MINOR_URBAN | RoadCategory.MAJOR_URBAN;
                case "RURAL":
                    return RoadCategory.MINOR_RURAL | RoadCategory.MAJOR_RURAL;
                case "SQUARE":
                    return RoadCategory.SQUARE;
                case "CIRCLE":
                    return RoadCategory.CIRCLE;
                case "OVAL":
                    return RoadCategory.OVAL;
                case "LOOP":
                    return RoadCategory.LOOP;
                case "BRIDGE":
                    return RoadCategory.BRIDGE;
                case "TUNNEL":
                    return RoadCategory.TUNNEL;
            }

            return RoadCategory.NONE;
        }

        private static Dictionary<string, SuffixProperties> LoadSuffixesFromCsv(string filename)
        {
            string csvFile = Path.Combine(Mod.GetDllDirectory(), filename);
            var suffixes = new Dictionary<string, SuffixProperties>();

            if (!File.Exists(csvFile))
            {
                Debug.Log("WQ:UKSN: List of suffixes not found at: " + csvFile);
                return suffixes;
            }

            string line;

            // Read the file and display it line by line. 
            StreamReader file = new StreamReader(csvFile);
            while ((line = file.ReadLine()) != null)
            {
                var splitLine = line.Split(',');

                string name = "";
                var categoryFrequencies = new Dictionary<RoadCategory, double>();
                var featureModifiers = new Dictionary<RoadFeature, double>();
                var negativeFeatureModifiers = new Dictionary<RoadFeature, double>();

                try
                {
                    name = splitLine[0];

                    for (int i = 1; i < splitLine.Length; ++i)
                    {
                        var entry = splitLine[i].Split(':');
                        var s = entry[0];
                        double freq = FrequencyFromString(entry[1]);

                        RoadCategory category = RoadCategoryFromString(s);

                        if (category != RoadCategory.NONE)
                        {
                            categoryFrequencies.Add(category, freq);
                            continue;
                        }

                        bool invert = false;
                        if (s.StartsWith("NOT_"))
                        {
                            invert = true;
                            s = s.Replace("NOT_", "");
                        }

                        RoadFeature feature = RoadFeatureFromString(s);
                        if (feature != RoadFeature.NONE)
                        {
                            if (invert)
                            {
                                negativeFeatureModifiers.Add(feature, freq);
                            }
                            else
                            {
                                featureModifiers.Add(feature, freq);
                            }
                            continue;
                        }

                        Debug.Log("WQ:UKSN: Invalid prefix entry: " + line);
                    }
                }
                catch
                {
                    Debug.Log("WQ:UKSN: Invalid prefix entry: " + line);
                    continue;
                }

                suffixes.Add(name, new SuffixProperties(categoryFrequencies, featureModifiers, negativeFeatureModifiers));

            }

            file.Close();

            return suffixes;
        }

        private static Dictionary<string, PrefixProperties> LoadPrefixesFromCsv(string filename)
        {
            string csvFile = Path.Combine(Mod.GetDllDirectory(), filename);
            var prefixes = new Dictionary<string, PrefixProperties>();

            if (!File.Exists(csvFile))
            {
                Debug.Log("WQ:UKSN: List of prefixes not found at: " + csvFile);
                prefixes.Add("STREET_PREFIX_MISSING", new PrefixProperties(1, true));
                return prefixes;
            }

            string line;

            // Read the file and display it line by line. 
            StreamReader file = new StreamReader(csvFile);
            while ((line = file.ReadLine()) != null)
            {
                var splitLine = line.Split(',');

                string name = "";
                uint probability = 0;
                var allowedCategories = RoadCategory.NONE;
                var requiredFeatures = RoadFeature.NONE;
                var forbiddenFeatures = RoadFeature.NONE;

                try
                {
                    name = splitLine[0];
                    probability = uint.Parse(splitLine[1]);

                    for(int i = 2; i < splitLine.Length; ++i)
                    {
                        var s = splitLine[i];

                        RoadCategory category = RoadCategoryFromString(s);

                        if (category != RoadCategory.NONE)
                        {
                            allowedCategories |= category;
                            continue;
                        }

                        bool invert = false;
                        if (s.StartsWith("NOT_"))
                        {
                            invert = true;
                            s = s.Replace("NOT_", "");
                        }

                        RoadFeature feature = RoadFeatureFromString(s);
                        if(feature != RoadFeature.NONE)
                        {
                            if(invert)
                            {
                                forbiddenFeatures |= feature;
                            }
                            else
                            {
                                requiredFeatures |= feature;
                            }
                            continue;
                        }

                        Debug.Log("WQ:UKSN: Invalid prefix entry: " + line);
                    }
                }
                catch
                {
                    Debug.Log("WQ:UKSN: Invalid prefix entry: " + line);
                    continue;
                }

                if (allowedCategories == RoadCategory.NONE)
                {
                    allowedCategories = RoadCategory.ALL;
                }

                prefixes.Add(name, new PrefixProperties(probability, name.EndsWith("$"), allowedCategories, forbiddenFeatures, requiredFeatures));
            }

            file.Close();

            return prefixes;
        }
    }
}
