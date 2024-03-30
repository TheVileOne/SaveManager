using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using SaveManager.Helpers;

namespace SaveManager
{
    public static class Config
    {
        /// <summary>
        /// Indicates that Config data is safe to be accessed from the OptionInterface (initialized OnModsInIt)  
        /// </summary>
        public static bool SafeToLoad;

        /// <summary>
        /// Contains config values that are managed by the OptionInterface. This should only be interacted with after RainWorld has initialized to avoid errors.
        /// </summary>
        public static OptionInterface.ConfigHolder ConfigData
        {
            get
            {
                if (SafeToLoad)
                    return Plugin.OptionInterface.config;
                return null;
            }
        }

        /// <summary>
        /// Contains config values read directly from the mod config. This data may be accessed at any time in the mod load process.
        /// </summary>
        public static StringDictionary ConfigDataRaw;

        public static Configurable<bool> cfgEnablePerVersionSaves;
        public static Configurable<bool> cfgInheritVersionSaves;

        public static bool PerVersionSaving
        {
            get
            {
                if (SafeToLoad)
                    return cfgEnablePerVersionSaves.Value;
                return GetValue(nameof(cfgEnablePerVersionSaves), false);
            }
        }

        public static void Load()
        {
            ConfigDataRaw = ConfigReader.ReadFile(Plugin.ConfigFilePath);
        }

        public static void Initialize()
        {
            SafeToLoad = true;
            ConfigData.configurables.Clear();

            //Define config options
            cfgEnablePerVersionSaves = ConfigData.Bind(nameof(cfgEnablePerVersionSaves), false,
                new ConfigInfo("Save file data will be stored for each game version", new object[]
            {
                "Store save data per game version"
            }));
            cfgInheritVersionSaves = ConfigData.Bind(nameof(cfgInheritVersionSaves), true,
                new ConfigInfo("Save file data from other game versions will be retained on a game version change", new object[]
            {
                "Inherit save data on game version change (if compatible)"
            }));
        }

        public static T GetValue<T>(string settingName, T expectedDefault) where T : IConvertible
        {
            try
            {
                if (!SafeToLoad)
                {
                    if (ConfigDataRaw.ContainsKey(settingName))
                        return ConfigDataRaw[settingName].ConvertParse<T>();
                }
                else
                {
                    //Use reflection to get the correct configurable and return its value
                    Configurable<T> configSetting = (Configurable<T>)typeof(Config).GetField(settingName).GetValue(null);
                    return configSetting.Value;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("Error occurred while retrieving config settings");
                Plugin.Logger.LogError(ex);
            }
            return expectedDefault;
        }

        /// <summary>
        /// Gets the string that appears on the label associated with a config option
        /// </summary>
        public static string GetDescription(ConfigurableBase option)
        {
            return option.info.description;
        }

        /// <summary>
        /// Gets the string that appears on the bottom of the screen and describes the function of the config option when hovered
        /// </summary>
        public static string GetOptionLabel(ConfigurableBase option)
        {
            return option.info.Tags[0] as string;
        }

        /// <summary>
        /// Searches the mod config file for a config setting and returns the stored setting value if one exists, expectedDefault otherwise
        /// </summary>
        public static T ReadFromDisk<T>(string settingName, T expectedDefault) where T : IConvertible
        {
            if (!File.Exists(Plugin.ConfigFilePath))
                return expectedDefault;

            return ConfigReader.ReadFromDisk(Plugin.ConfigFilePath, settingName, expectedDefault);
        }

        public class ConfigInfo : ConfigurableInfo
        {
            public ConfigInfo(string description, params object[] tags) : base(description, null, string.Empty, tags)
            {
            }
        }
    }
}
