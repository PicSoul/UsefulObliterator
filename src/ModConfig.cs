using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace UsefulObliterator
{
    internal static class ModConfig
    {
        // Server Sync Lock
        public static ConfigEntry<bool> LockServerSettings;

        // Payout Ratios & Odds (Server-enforced)
        public static ConfigEntry<float> WinRatePercent;
        public static ConfigEntry<float> BustPayoutPercent;
        public static ConfigEntry<float> PairPayoutPercent;
        public static ConfigEntry<float> TriplePayoutPercent;
        public static ConfigEntry<float> QuadPayoutPercent;
        public static ConfigEntry<float> BossJackpotPayoutPercent;

        // Mechanics (Server-enforced)
        public static ConfigEntry<bool> AllowGambleMaterials;
        public static ConfigEntry<bool> OnlyAllowCraftedItems;
        public static ConfigEntry<bool> ConsolationCoal;
        public static ConfigEntry<bool> JackpotBonusLoot;
        public static ConfigEntry<string> RawItemsBehavior;

        // Client Display & Audio (Client-side)
        public static ConfigEntry<bool> ShowReelHUD;
        public static ConfigEntry<bool> EnableSlotAudio;
        public static ConfigEntry<float> SlotDisplayDistance;
        public static ConfigEntry<float> ResultDisplayDuration;

        private static ConfigSync _sync;

        private static ConfigEntry<T> Synced<T>(ConfigEntry<T> entry)
        {
            if (_sync != null)
            {
                SyncedConfigEntry<T> synced = _sync.AddConfigEntry(entry);
                synced.SynchronizedConfig = true;
            }
            return entry;
        }

        internal static void Bind(ConfigFile config, ConfigSync sync = null)
        {
            _sync = sync;

            LockServerSettings = config.Bind("1 - Server", "LockSettings", false,
                new ConfigDescription("Whether server settings are strictly enforced on clients (admins only)."));
            if (_sync != null)
            {
                _sync.AddLockingConfigEntry(LockServerSettings);
            }

            // Payout percentages & Odds
            WinRatePercent = Synced(config.Bind("2 - Payouts", "WinRatePercent", 40.0f,
                new ConfigDescription("Base chance (0-100%) to win (Pair, Triple, Quad, or Boss Jackpot). Higher rates increase matches and decrease Busts. At 100%, losses never occur.",
                    new AcceptableValueRange<float>(0f, 100f))));

            BustPayoutPercent = Synced(config.Bind("2 - Payouts", "BustPayoutPercent", 25.0f,
                new ConfigDescription("Percent of crafting materials returned on a Bust (0-1 matching heads).",
                    new AcceptableValueRange<float>(0f, 100f))));

            PairPayoutPercent = Synced(config.Bind("2 - Payouts", "PairPayoutPercent", 50.0f,
                new ConfigDescription("Percent of crafting materials returned on a Pair (2 matching heads).",
                    new AcceptableValueRange<float>(0f, 100f))));

            TriplePayoutPercent = Synced(config.Bind("2 - Payouts", "TriplePayoutPercent", 100.0f,
                new ConfigDescription("Percent of crafting materials returned on a Triple Match (3 matching creature heads).",
                    new AcceptableValueRange<float>(0f, 200f))));

            QuadPayoutPercent = Synced(config.Bind("2 - Payouts", "QuadPayoutPercent", 150.0f,
                new ConfigDescription("Percent of crafting materials returned on 4 of a Kind (4 matching regular creature heads).",
                    new AcceptableValueRange<float>(0f, 300f))));

            BossJackpotPayoutPercent = Synced(config.Bind("2 - Payouts", "BossJackpotPayoutPercent", 250.0f,
                new ConfigDescription("Percent of crafting materials returned on a Boss Jackpot (4 matching Boss heads).",
                    new AcceptableValueRange<float>(0f, 500f))));

            // Mechanics
            AllowGambleMaterials = Synced(config.Bind("3 - Mechanics", "AllowGambleMaterials", true,
                new ConfigDescription("Allow players to gamble raw materials (ores, ingots, wood, stone, food, etc.) in the Obliterator. Winning rolls grant more of that material; losing rolls reduce or destroy them.")));

            OnlyAllowCraftedItems = Synced(config.Bind("3 - Mechanics", "OnlyAllowCraftedItems", false,
                new ConfigDescription("When enabled, strictly requires at least one crafted item in the container to spin. When disabled (default), players can spin with crafted items, raw materials, or both.")));

            ConsolationCoal = Synced(config.Bind("3 - Mechanics", "ConsolationCoal", true,
                new ConfigDescription("Whether to grant consolation Coal for lost materials on non-jackpot rolls.")));

            JackpotBonusLoot = Synced(config.Bind("3 - Mechanics", "JackpotBonusLoot", true,
                new ConfigDescription("Whether a Boss Jackpot drops bonus treasure (Coins, Amber, Ruby, or Thunderstone).")));

            RawItemsBehavior = Synced(config.Bind("3 - Mechanics", "RawItemsBehavior", "Coal",
                new ConfigDescription("How items without crafting recipes (wood, stone, meat, dandelions) are treated: 'Coal' (standard conversion scaled by roll) or 'Refund' (return the items).",
                    new AcceptableValueList<string>("Coal", "Refund"))));

            // Client UI / Audio
            ShowReelHUD = config.Bind("4 - Interface", "ShowReelHUD", true,
                new ConfigDescription("Show the slot machine HUD banner when the Obliterator handle is pulled."));

            EnableSlotAudio = config.Bind("4 - Interface", "EnableSlotAudio", true,
                new ConfigDescription("Play mechanical slot reel clicking and stop sound effects during the roll."));

            SlotDisplayDistance = config.Bind("4 - Interface", "SlotDisplayDistance", 5.0f,
                new ConfigDescription("Maximum distance (in meters) from the Obliterator for nearby players to see the slot reels spin and hear mechanical audio effects.",
                    new AcceptableValueRange<float>(1.0f, 50.0f)));

            ResultDisplayDuration = config.Bind("4 - Interface", "ResultDisplayDuration", 3.0f,
                new ConfigDescription("How long (in seconds) the slot machine results and payout banner remain visible on screen after the spin completes before fading out.",
                    new AcceptableValueRange<float>(0.5f, 10.0f)));
        }
    }
}

