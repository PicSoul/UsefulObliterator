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
        public const string PluginVersion = "0.2.1";

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

            // Every word a player reads comes from translations/<Language>.yml, or from a
            // "Useful Obliterator.<Language>.yml" a translator drops anywhere in BepInEx.
            LocalizationManager.Localizer.Load();

            // And again whenever the language is changed in the settings. LocalizationManager is
            // meant to reload by itself as the game reloads its own text, but in testing a switch
            // to Spanish left this mod's words in English while the game's changed. The game says
            // when the language changes, so the reload hangs off that too. Subscribed here, before
            // the slot machine's own relabelling, so the new words are in place when it asks.
            Localization.OnLanguageChange += LocalizationManager.Localizer.Load;

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

