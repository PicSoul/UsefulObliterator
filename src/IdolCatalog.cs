using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace UsefulObliterator
{
    /// <summary>
    /// The idols the Forge of Potential takes, and which of them a player has earned the right to
    /// see - for the Boss Jackpot's bonus idol.
    /// </summary>
    /// <remarks>
    /// Idols are not marked on the item. The game knows one by its use: a recipe requirement with
    /// m_upgraderResource set is what the forge consumes. So the list is read off the recipes once
    /// the item database is ready - every idol the game has, and any a mod adds the same way.
    ///
    /// Each idol has a tier, named for a material - $item_upgrader_tier0 Wooden up to tier7
    /// Bloodgold - and each tier is matched to the biome that material belongs to, so a player is
    /// only given idols from biomes they have already set foot in: no Deep North idol for someone
    /// who has not seen the Deep North.
    ///
    /// The spin runs on whoever owns the Obliterator, often the server, which cannot see the
    /// player's discovered biomes. So the player's game sends them just before asking to spin,
    /// and they are kept here, by player, for the spin to use.
    /// </remarks>
    internal static class IdolCatalog
    {
        internal const string BiomesRpc = "RPC_UsefulObliterator_KnownBiomes";

        /// <summary>Tier by tier, the biome each idol tier belongs to.</summary>
        private static readonly Heightmap.Biome[] TierBiome =
        {
            Heightmap.Biome.Meadows,     // 0 Wooden
            Heightmap.Biome.BlackForest, // 1 Bronze
            Heightmap.Biome.Swamp,       // 2 Iron
            Heightmap.Biome.Mountain,    // 3 Silver
            Heightmap.Biome.Plains,      // 4 Black Metal
            Heightmap.Biome.Mistlands,   // 5 Black Marble
            Heightmap.Biome.AshLands,    // 6 Flametal
            Heightmap.Biome.DeepNorth    // 7 Bloodgold
        };

        private static readonly Heightmap.Biome[] Biomes =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
            Heightmap.Biome.Ocean
        };

        private static readonly Regex TierInName = new Regex(@"tier(\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex DigitsInPrefab = new Regex(@"(\d+)(?!.*\d)");

        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownBiomeNames =
            AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownBiome");

        /// <summary>What each player's game last said it had discovered, by player id.</summary>
        private static readonly Dictionary<long, int> KnownByPlayer = new Dictionary<long, int>();

        private static List<(GameObject prefab, int tier)> _idols;

        // ------------------------------------------------------------------ the idols

        private static List<(GameObject prefab, int tier)> Idols()
        {
            if (_idols != null || ObjectDB.instance == null)
            {
                return _idols ?? new List<(GameObject, int)>();
            }

            _idols = new List<(GameObject, int)>();
            HashSet<string> seen = new HashSet<string>();

            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe?.m_resources == null)
                {
                    continue;
                }

                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (requirement == null || !requirement.m_upgraderResource || requirement.m_resItem == null)
                    {
                        continue;
                    }

                    GameObject prefab = ObjectDB.instance.GetItemPrefab(requirement.m_resItem.gameObject.name)
                        ?? requirement.m_resItem.gameObject;
                    if (!seen.Add(prefab.name))
                    {
                        continue;
                    }

                    _idols.Add((prefab, TierOf(requirement.m_resItem)));
                }
            }

            List<string> listed = new List<string>();
            foreach (var idol in _idols)
            {
                listed.Add($"{idol.prefab.name} (tier {idol.tier}, {BiomeOf(idol.tier)})");
            }

            UsefulObliteratorPlugin.Log.LogInfo($"[IdolCatalog] {_idols.Count} idols found: {string.Join(", ", listed)}");
            return _idols;
        }

        /// <summary>
        /// An idol's tier, from its name token - "$item_upgrader_tier3" - or failing that the last
        /// number in its prefab name. An idol whose tier cannot be read counts as the highest, so
        /// it is never handed out early.
        /// </summary>
        private static int TierOf(ItemDrop item)
        {
            Match match = TierInName.Match(item.m_itemData?.m_shared?.m_name ?? string.Empty);
            if (!match.Success)
            {
                match = DigitsInPrefab.Match(item.gameObject.name);
            }

            return match.Success && int.TryParse(match.Groups[1].Value, out int tier)
                ? Mathf.Clamp(tier, 0, TierBiome.Length - 1)
                : TierBiome.Length - 1;
        }

        private static Heightmap.Biome BiomeOf(int tier)
        {
            return TierBiome[Mathf.Clamp(tier, 0, TierBiome.Length - 1)];
        }

        /// <summary>
        /// A random idol from the biomes this player has discovered - or null if none qualify.
        /// A player whose biomes were never sent is treated as knowing only the Meadows.
        /// </summary>
        internal static GameObject PickFor(long playerID)
        {
            int known = KnownByPlayer.TryGetValue(playerID, out int mask) ? mask : (int)Heightmap.Biome.Meadows;

            List<GameObject> allowed = new List<GameObject>();
            foreach (var idol in Idols())
            {
                if ((known & (int)BiomeOf(idol.tier)) != 0)
                {
                    allowed.Add(idol.prefab);
                }
            }

            UsefulObliteratorPlugin.Log.LogInfo(
                $"[IdolCatalog] Player {playerID} knows biomes {(Heightmap.Biome)known}; {allowed.Count} idols allowed.");

            return allowed.Count == 0 ? null : allowed[UnityEngine.Random.Range(0, allowed.Count)];
        }

        // ------------------------------------------------------------------ discovered biomes

        /// <summary>
        /// The local player's discovered biomes, as a biome mask. The game keeps them as names -
        /// "$biome_swamp", or with a prefix or suffix for the newer variants of a biome - so each
        /// base biome is counted as found if any discovered name carries its name.
        /// </summary>
        private static int LocalKnownBiomes()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return 0;
            }

            HashSet<string> names;
            try
            {
                names = KnownBiomeNames(player);
            }
            catch (Exception ex)
            {
                UsefulObliteratorPlugin.Log.LogWarning($"[IdolCatalog] Could not read discovered biomes: {ex.Message}");
                return (int)Heightmap.Biome.Meadows;
            }

            int mask = (int)Heightmap.Biome.Meadows;
            foreach (Heightmap.Biome biome in Biomes)
            {
                string token = BiomeSector.GetBiomeName(biome);
                foreach (string name in names)
                {
                    if (name != null && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mask |= (int)biome;
                        break;
                    }
                }
            }

            return mask;
        }

        /// <summary>
        /// Tells whoever runs this Obliterator which biomes the local player has discovered - sent
        /// just before the spin request, so it arrives first.
        /// </summary>
        internal static void SendKnownBiomes(Incinerator incinerator)
        {
            ZNetView view = incinerator != null ? incinerator.GetComponent<ZNetView>() : null;
            Player player = Player.m_localPlayer;
            if (view == null || !view.IsValid() || player == null)
            {
                return;
            }

            view.InvokeRPC(BiomesRpc, player.GetPlayerID(), LocalKnownBiomes());
        }

        /// <summary>The Obliterator's owner hears which biomes a player has discovered.</summary>
        internal static void Remember(long playerID, int mask)
        {
            KnownByPlayer[playerID] = mask | (int)Heightmap.Biome.Meadows;
        }
    }
}
