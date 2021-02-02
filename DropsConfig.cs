using BepInEx.Configuration;
using System;

namespace Nm1fiOutward.Drops
{
    internal class DropsConfig
    {
        internal bool Enabled => cEnabled.Value;
        private readonly ConfigEntry<bool> cEnabled;


        internal bool Debug =>
#if DEBUG
            true;
#else
            cDebug.Value;
#endif
        private readonly ConfigEntry<bool> cDebug;

        internal bool Simulate =>
#if DEBUG
            true;
#else
            cSimulate.Value;
#endif
        private readonly ConfigEntry<bool> cSimulate;

        internal DropsConfig(ConfigFile config)
        {
            cEnabled = config.Bind(
                "Base",
                "Enabled",
                true,
                new ConfigDescription(
                    "Enable manipulation of drop tables."
                    + " This allows mods using this framework to make balanced and unbalanced changes to any drop tables in the game."
                    + " Disabling this reduces integration of those mods, but ensures that drop chances are not manipulated via this framework.",
                    null,
                    new ConfigurationManagerAttributes {
                        Order = 1,
                    }
                )
            );
            cDebug = config.Bind(
                "Base",
                "Debug",
                false,
                new ConfigDescription(
                    "Enable debug. When enabled, the framework will write contents of altered drop tables to the log."
                    + " This helps modders to debug mistakes."
                    + " If you find possible bugs, enable this before providing logs to the developer.",
                    null,
                    new ConfigurationManagerAttributes {
                        IsAdvanced = true,
                        Order = 3,
                    }
                )
            );
            cSimulate = config.Bind(
                "Develop",
                "Simulate",
                false,
                new ConfigDescription(
                    "Enable simulate on scene load for debugging. When enabled, the framework will find all altered drop tables"
                    + " and write their contents after alterations to the log."
                    + " This option is aimed for the mod developer and less useful for the end user",
                    null,
                    new ConfigurationManagerAttributes {
                        IsAdvanced = true,
                        Order = 10,
                    }
                )
            );

            config.SettingChanged += OnSettingChanged;
        }

        private void OnSettingChanged(Object sender, SettingChangedEventArgs eventArgs)
        {
            var setting = eventArgs.ChangedSetting;
            var def = setting.Definition;
            DropsPlugin.Log($"Setting changed: {def.Section}.{def.Key} = {setting.BoxedValue}");
            if (def.Section == "General" && def.Key == "Enabled" && setting is ConfigEntry<bool> boolSetting)
                DropsPlugin.Instance.SetEnabled(boolSetting.Value);
        }
    }
}
