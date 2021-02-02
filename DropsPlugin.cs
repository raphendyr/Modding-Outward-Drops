using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SideLoader;
using SideLoader.UI.Modules;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nm1fiOutward.Drops
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(SL.GUID, "3.3.3")]
    public class DropsPlugin : BaseUnityPlugin
    {
        internal static DropsPlugin Instance { get; private set; }

        public const string GUID = "github.raphendyr.droptablealterations";
        public const string NAME = "DropTableAlterations";
        public const string FEATURE_VERSION = "0.2";
        public const string VERSION = FEATURE_VERSION + ".0";

        public static bool IsEnabled => Instance != null && Instance.isEnabled;

        internal static new DropsConfig Config;

        // Logging helpers

        private static string LogLevelColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning: return ColorUtility.ToHtmlStringRGB(Color.yellow);
                case LogLevel.Error: return ColorUtility.ToHtmlStringRGB(Color.red);
                default: return ColorUtility.ToHtmlStringRGB(Color.white);
            }
        }

        internal static void Log(string message, LogLevel level = LogLevel.Message)
        {
            Instance.Logger.Log(level, message);
            DebugConsole.Log(NAME + ": " + message, LogLevelColor(level));
        }

        internal static void LogInfo(string message) => Log(message, LogLevel.Info);
        internal static void LogWarning(string message) => Log(message, LogLevel.Warning);
        internal static void LogError(string message) => Log(message, LogLevel.Error);

        // Instance

        internal void Awake()
        {
            if (Instance != null)
                return;
            Instance = this;
            Log($"Version {VERSION} loading...");
            Config = new DropsConfig(base.Config);

            if (Config.Enabled)
                SetEnabled();
            else
                Logger.Log(LogLevel.Message, "Mod is not enabled, not injecting game hooks.");
        }

        private bool isEnabled = false;

        internal void SetEnabled(bool enabled = true)
        {
            if (enabled == isEnabled)
                return;
            if (enabled)
            {
                try
                {
                    new Harmony(GUID).PatchAll();
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warning, "Exception applying Harmony patches!");
                    SL.LogInnerException(e);

                    Logger.Log(LogLevel.Warning, "Mod is not enabled!");
                    return;
                }

                SL.OnSceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                Logger.Log(LogLevel.Message, "Enabled game hooks");
            }
            else
            {
                SL.OnSceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                StopAllCoroutines();
                new Harmony(GUID).UnpatchAll(GUID);
                Logger.Log(LogLevel.Message, "Disabled game hooks"); ;
            }
            isEnabled = enabled;
        }

        private void OnSceneUnloaded(Scene current)
            => StopAllCoroutines();

        private void OnSceneLoaded()
        {
            if (!Config.Enabled || PhotonNetwork.isNonMasterClientInRoom)
                return;

            DropsPatcher.UpdatedMerchantInventories.Clear();

            if (Config.Simulate)
                StartCoroutine(DropTableLogger.LogAllCoro());

            var treasureChests = Resources.FindObjectsOfTypeAll<TreasureChest>()
                .Where(chest => chest.gameObject?.scene != null)
                .ToList();
            if (treasureChests.Any())
                StartCoroutine(DropsPatcher.PatchTreasureChestsCoro(treasureChests));
        }
    }
}
