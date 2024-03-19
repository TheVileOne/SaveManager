using BepInEx;
using BepInEx.Logging;
using MulitversionSupport;
using System;
using System.IO;
using UnityEngine;

namespace MultiversionSupport
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "fluffball.multiversionsupport"; // This should be the same as the id in modinfo.json!
        public const string PLUGIN_NAME = "Multiversion Support"; // This should be a human-readable version of your mod's name. This is used for log files and also displaying which mods get loaded. In general, it's a good idea to match this with your modinfo.json as well.
        public const string PLUGIN_VERSION = "0.5.0";

        public static new ManualLogSource Logger { get; private set; }

        private bool isInitialized;

        public void OnEnable()
        {
            if (isInitialized) return;

            Logger = base.Logger;
            isInitialized = true;
        }
    }
}