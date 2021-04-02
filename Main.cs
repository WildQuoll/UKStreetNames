using ICities;
using CitiesHarmony.API;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.Plugins;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace UKStreetNames
{
    public class Mod : IUserMod
    {
        public string Name => "UK Street Names";
        public string Description => "Replaces generic street names with British-flavoured ones.";

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }

        public static string GetDllDirectory()
        {
            var results = new Dictionary<uint, int>();
            var manager = Singleton<PluginManager>.instance;
            foreach (var plugin in manager.GetPluginsInfo())
            {
                if (plugin.name == "2253945873" || plugin.name == "UKStreetNames")
                {
                    return plugin.modPath;
                }
            }

            Debug.Log("WQ:UKSN: Mod DLL folder not found!");
            return "";
        }
    }
}
