using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;

namespace UsefulObliterator
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UsefulObliteratorPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.pics0ul.valheim.usefulobliterator";
        public const string PluginName = "Useful Obliterator";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private readonly ConfigSync _configSync = new ConfigSync(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            MinimumRequiredVersion = PluginVersion,
            ModRequired = false
        };

        private void Awake()
        {
            Log = Logger;

            ModConfig.Bind(Config, _configSync);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded successfully.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}

