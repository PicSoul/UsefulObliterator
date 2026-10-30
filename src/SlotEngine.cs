using System;
using System.Collections.Generic;
using UnityEngine;

namespace UsefulObliterator
{
    public enum OutcomeType
    {
        Bust,
        Pair,
        Triple,
        Quad,
        BossJackpot
    }

    public enum TrophyTier
    {
        Common,
        Uncommon,
        Rare,
        Boss
    }

    public class TrophySymbol
    {
        public string PrefabName { get; }
        public string FallbackName { get; }
        public TrophyTier Tier { get; }
        public int Weight { get; }

        private Sprite _cachedSprite;
        private string _nameKey;

        public TrophySymbol(string prefabName, string fallbackName, TrophyTier tier, int weight)
        {
            PrefabName = prefabName;
            FallbackName = fallbackName;
            Tier = tier;
            Weight = weight;
        }


        /// <summary>
        /// Every icon shipped with the mod, found once, wherever the installer put it.
        /// </summary>
        /// <remarks>
        /// The icons were looked for in an "icons" folder beside the assembly, which is where
        /// they sit when the mod is copied in by hand and is not where they end up when it is
        /// installed from a package - Gale lays them out flat, next to the DLL. The result was a
        /// mod that showed its high-resolution trophies while being tested and fell back to the
        /// game's own low-resolution ones the moment it was installed the way a player would
        /// install it, with the same code in both.
        ///
        /// So the folder is searched rather than assumed. A mod manager may flatten a package,
        /// keep its folders, or nest them one deeper, and none of that is worth caring about:
        /// what matters is a file named after the prefab, somewhere under the mod's own
        /// directory. Read once, because this is asked for every trophy on every slot redraw.
        /// </remarks>
        private static Dictionary<string, string> _iconFiles;

        private static string FindIcon(string prefabName)
        {
            if (_iconFiles == null)
            {
                _iconFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string pluginDir = System.IO.Path.GetDirectoryName(
                    typeof(UsefulObliteratorPlugin).Assembly.Location);

                if (string.IsNullOrEmpty(pluginDir) || !System.IO.Directory.Exists(pluginDir))
                {
                    return null;
                }

                foreach (string file in System.IO.Directory.GetFiles(
                    pluginDir, "*.*", System.IO.SearchOption.AllDirectories))
                {
                    string extension = System.IO.Path.GetExtension(file);
                    if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                        && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string name = System.IO.Path.GetFileNameWithoutExtension(file);

                    // First one wins, so a png is preferred over a jpg of the same name only by
                    // the order the directory hands them back - which is fine, they are the same
                    // picture. What must not happen is a later file quietly replacing an earlier.
                    if (!_iconFiles.ContainsKey(name))
                    {
                        _iconFiles[name] = file;
                    }
                }

                UsefulObliteratorPlugin.Log.LogInfo(
                    $"[SlotEngine] Found {_iconFiles.Count} icon file(s) under {pluginDir}.");
            }

            return _iconFiles.TryGetValue(prefabName, out string path) ? path : null;
        }

        public Sprite GetSprite()
        {
            if (_cachedSprite != null) return _cachedSprite;

            // 1. Check for a custom high-resolution icon shipped with the mod
            try
            {
                string iconPath = FindIcon(PrefabName);
                if (iconPath != null)
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(iconPath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, fileData))
                    {
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        _cachedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        return _cachedSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                UsefulObliteratorPlugin.Log.LogWarning($"[SlotEngine] Failed loading custom icon for {PrefabName}: {ex.Message}");
            }

            // 2. Fallback to vanilla ObjectDB trophy icon
            if (ObjectDB.instance != null)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(PrefabName);
                if (itemPrefab != null)
                {
                    ItemDrop drop = itemPrefab.GetComponent<ItemDrop>();
                    if (drop != null && drop.m_itemData?.m_shared?.m_icons?.Length > 0)
                    {
                        _cachedSprite = drop.m_itemData.m_shared.m_icons[0];
                    }
                }
            }
            return _cachedSprite;
        }

        /// <summary>
        /// The trophy's name in the current language. The game's key is remembered, never the
        /// words: remembering the words kept whatever language was set the first time, so a switch
        /// in the settings left the banner naming the creature in the old one.
        /// </summary>
        public string GetDisplayName()
        {
            if (_nameKey == null && ObjectDB.instance != null)
            {
                GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(PrefabName);
                ItemDrop drop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
                if (drop != null && !string.IsNullOrEmpty(drop.m_itemData?.m_shared?.m_name))
                {
                    _nameKey = drop.m_itemData.m_shared.m_name;
                }
            }

            return _nameKey != null && Localization.instance != null
                ? Localization.instance.Localize(_nameKey)
                : FallbackName;
        }
    }

    public class RollOutcome
    {
        public TrophySymbol Reel1 { get; set; }
        public TrophySymbol Reel2 { get; set; }
        public TrophySymbol Reel3 { get; set; }
        public TrophySymbol Reel4 { get; set; }
        public OutcomeType Outcome { get; set; }
        public float PayoutRatio { get; set; }
        public TrophySymbol MatchedSymbol { get; set; }
        public string Title { get; set; }
        public Color TitleColor { get; set; }
    }

    public static class SlotEngine
    {

        /// <summary>
        /// The banner over a finished spin, in this player's language. Built on each player's
        /// screen rather than sent, so the words and the creature's name are their own.
        /// </summary>
        public static string TitleFor(OutcomeType outcome, TrophySymbol symbol)
        {
            Localization loc = Localization.instance;
            string creature = symbol != null ? symbol.GetDisplayName().ToUpper() : "";
            switch (outcome)
            {
                case OutcomeType.BossJackpot: return loc.Localize("$uo_result_jackpot", creature);
                case OutcomeType.Quad: return loc.Localize("$uo_result_four", creature);
                case OutcomeType.Triple: return loc.Localize("$uo_result_three", creature);
                case OutcomeType.Pair: return loc.Localize("$uo_result_two", creature);
                default: return "<b>" + loc.Localize("$uo_result_bust") + "</b>";
            }
        }

        public static readonly List<TrophySymbol> Symbols = new List<TrophySymbol>();
        private static readonly List<TrophySymbol> _activeSymbols = new List<TrophySymbol>();
        private static int _totalWeight;
        private static int _activeTotalWeight;
        private static bool _validated;

        static SlotEngine()
        {
            // Common (weight 50)
            Add("TrophyBoar", "Boar", TrophyTier.Common, 50);
            Add("TrophyDeer", "Deer", TrophyTier.Common, 50);
            Add("TrophyNeck", "Neck", TrophyTier.Common, 50);
            Add("TrophyGreydwarf", "Greydwarf", TrophyTier.Common, 50);

            // Uncommon (weight 30)
            Add("TrophySkeleton", "Skeleton", TrophyTier.Uncommon, 30);
            Add("TrophyDraugr", "Draugr", TrophyTier.Uncommon, 30);
            Add("TrophyWolf", "Wolf", TrophyTier.Uncommon, 30);
            Add("TrophyLox", "Lox", TrophyTier.Uncommon, 30);

            // Rare (weight 15) - Valheim's standard blue troll trophy is TrophyFrostTroll
            Add("TrophyFrostTroll", "Troll", TrophyTier.Rare, 15);
            Add("TrophyFenring", "Fenring", TrophyTier.Rare, 15);
            Add("TrophySerpent", "Serpent", TrophyTier.Rare, 15);
            Add("TrophySeeker", "Seeker", TrophyTier.Rare, 15);

            // Boss / Jackpot (weight 5)
            Add("TrophyEikthyr", "Eikthyr", TrophyTier.Boss, 5);
            Add("TrophyTheElder", "The Elder", TrophyTier.Boss, 5);
            Add("TrophyBonemass", "Bonemass", TrophyTier.Boss, 5);
            Add("TrophyDragonQueen", "Moder", TrophyTier.Boss, 5);
            Add("TrophyGoblinKing", "Yagluth", TrophyTier.Boss, 5);
            Add("TrophySeekerQueen", "The Queen", TrophyTier.Boss, 5);
            Add("TrophyFader", "Fader", TrophyTier.Boss, 5);
        }

        private static void Add(string prefab, string name, TrophyTier tier, int weight)
        {
            Symbols.Add(new TrophySymbol(prefab, name, tier, weight));
            _totalWeight += weight;
        }

        public static void EnsureValidated()
        {
            if (_validated || ObjectDB.instance == null) return;

            _activeSymbols.Clear();
            _activeTotalWeight = 0;

            foreach (TrophySymbol s in Symbols)
            {
                if (s.GetSprite() != null)
                {
                    _activeSymbols.Add(s);
                    _activeTotalWeight += s.Weight;
                }
                else
                {
                    UsefulObliteratorPlugin.Log.LogWarning($"[SlotEngine] Symbol '{s.PrefabName}' has no sprite in ObjectDB; excluded from active reels.");
                }
            }

            if (_activeSymbols.Count > 0)
            {
                _validated = true;
                UsefulObliteratorPlugin.Log.LogInfo($"[SlotEngine] Validated {_activeSymbols.Count} trophy symbols for slot reels.");
            }
        }

        public static TrophySymbol PickRandomSymbol(System.Random rng)
        {
            EnsureValidated();

            List<TrophySymbol> pool = (_activeSymbols.Count > 0) ? _activeSymbols : Symbols;
            int totalWeight = (_activeSymbols.Count > 0) ? _activeTotalWeight : _totalWeight;

            if (totalWeight <= 0) return Symbols[0];

            int roll = rng.Next(totalWeight);
            int cumulative = 0;
            foreach (TrophySymbol s in pool)
            {
                cumulative += s.Weight;
                if (roll < cumulative)
                {
                    return s;
                }
            }
            return pool[0];
        }

        public static TrophySymbol FindSymbol(string prefabName)
        {
            foreach (TrophySymbol s in Symbols)
            {
                if (string.Equals(s.PrefabName, prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }

        public static RollOutcome EvaluateRoll(TrophySymbol s1, TrophySymbol s2, TrophySymbol s3, TrophySymbol s4)
        {
            RollOutcome result = new RollOutcome
            {
                Reel1 = s1,
                Reel2 = s2,
                Reel3 = s3,
                Reel4 = s4
            };

            TrophySymbol[] all = { s1, s2, s3, s4 };
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TrophySymbol> map = new Dictionary<string, TrophySymbol>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in all)
            {
                if (s == null) continue;
                map[s.PrefabName] = s;
                if (counts.ContainsKey(s.PrefabName))
                    counts[s.PrefabName]++;
                else
                    counts[s.PrefabName] = 1;
            }

            int maxCount = 0;
            string bestPrefab = s1 != null ? s1.PrefabName : "";
            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    bestPrefab = kvp.Key;
                }
            }

            TrophySymbol bestSym = (bestPrefab != null && map.ContainsKey(bestPrefab)) ? map[bestPrefab] : s1;
            result.MatchedSymbol = bestSym;

            if (maxCount == 4)
            {
                if (bestSym.Tier == TrophyTier.Boss)
                {
                    result.Outcome = OutcomeType.BossJackpot;
                    result.PayoutRatio = Mathf.Max(0f, ModConfig.BossJackpotPayoutPercent.Value / 100f);
                    result.Title = TitleFor(result.Outcome, bestSym);
                    result.TitleColor = new Color(1f, 0.85f, 0.2f); // Brilliant Gold
                }
                else
                {
                    result.Outcome = OutcomeType.Quad;
                    result.PayoutRatio = Mathf.Max(0f, ModConfig.QuadPayoutPercent.Value / 100f);
                    result.Title = TitleFor(result.Outcome, bestSym);
                    result.TitleColor = new Color(0.25f, 1f, 0.9f); // Electric Turquoise
                }
            }
            else if (maxCount == 3)
            {
                result.Outcome = OutcomeType.Triple;
                result.PayoutRatio = Mathf.Max(0f, ModConfig.TriplePayoutPercent.Value / 100f);
                result.Title = TitleFor(result.Outcome, bestSym);
                result.TitleColor = new Color(0.3f, 0.9f, 1f); // Sky Cyan
            }
            else if (maxCount == 2)
            {
                result.Outcome = OutcomeType.Pair;
                result.PayoutRatio = Mathf.Max(0f, ModConfig.PairPayoutPercent.Value / 100f);
                result.Title = TitleFor(result.Outcome, bestSym);
                result.TitleColor = new Color(1f, 0.95f, 0.4f); // Warm Yellow
            }
            else
            {
                result.Outcome = OutcomeType.Bust;
                result.PayoutRatio = Mathf.Max(0f, ModConfig.BustPayoutPercent.Value / 100f);
                result.Title = TitleFor(result.Outcome, bestSym);
                result.TitleColor = new Color(1f, 0.22f, 0.22f); // High-contrast Vivid Red
            }

            return result;
        }

        public static RollOutcome Roll(System.Random rng = null)
        {
            if (rng == null) rng = new System.Random();
            EnsureValidated();

            List<TrophySymbol> pool = (_activeSymbols.Count > 0) ? _activeSymbols : Symbols;
            List<TrophySymbol> bossSymbols = pool.FindAll(s => s.Tier == TrophyTier.Boss);
            List<TrophySymbol> nonBossSymbols = pool.FindAll(s => s.Tier != TrophyTier.Boss);
            if (bossSymbols.Count == 0) bossSymbols = pool;
            if (nonBossSymbols.Count == 0) nonBossSymbols = pool;

            // Calculate win probabilities based on ModConfig.WinRatePercent (0-100)
            float winRate = Mathf.Clamp(ModConfig.WinRatePercent.Value, 0f, 100f);
            float w = winRate / 100f;

            // Realistic slot machine probability pyramid:
            // Boss Jackpots are rare and thrilling (~1.4% at default w=0.40)
            // Quads are uncommon (~4% at default w=0.40)
            // Triples are solid payouts (~11.2% at default w=0.40)
            // Pairs are common minor returns (~23.4% at default w=0.40)
            // Bust represents the remainder (60% at default w=0.40)
            float pBossJackpot = w * 0.035f;
            float pQuad = w * 0.10f;
            float pTriple = w * 0.28f;
            float pPair = Mathf.Max(0f, w - pBossJackpot - pQuad - pTriple);

            double roll = rng.NextDouble();
            OutcomeType targetTier;

            if (roll < pBossJackpot)
            {
                targetTier = OutcomeType.BossJackpot;
            }
            else if (roll < pBossJackpot + pQuad)
            {
                targetTier = OutcomeType.Quad;
            }
            else if (roll < pBossJackpot + pQuad + pTriple)
            {
                targetTier = OutcomeType.Triple;
            }
            else if (roll < pBossJackpot + pQuad + pTriple + pPair)
            {
                targetTier = OutcomeType.Pair;
            }
            else
            {
                targetTier = OutcomeType.Bust;
            }

            TrophySymbol s1, s2, s3, s4;

            switch (targetTier)
            {
                case OutcomeType.BossJackpot:
                {
                    TrophySymbol boss = bossSymbols[rng.Next(bossSymbols.Count)];
                    s1 = boss;
                    s2 = boss;
                    s3 = boss;
                    s4 = boss;
                    break;
                }

                case OutcomeType.Quad:
                {
                    TrophySymbol sym = nonBossSymbols[rng.Next(nonBossSymbols.Count)];
                    s1 = sym;
                    s2 = sym;
                    s3 = sym;
                    s4 = sym;
                    break;
                }

                case OutcomeType.Triple:
                {
                    TrophySymbol matchSym = pool[rng.Next(pool.Count)];
                    TrophySymbol diffSym = pool[rng.Next(pool.Count)];
                    int tries = 0;
                    while (string.Equals(diffSym.PrefabName, matchSym.PrefabName, StringComparison.OrdinalIgnoreCase) && tries++ < 30)
                    {
                        diffSym = pool[rng.Next(pool.Count)];
                    }

                    int oddSlot = rng.Next(4);
                    s1 = (oddSlot == 0) ? diffSym : matchSym;
                    s2 = (oddSlot == 1) ? diffSym : matchSym;
                    s3 = (oddSlot == 2) ? diffSym : matchSym;
                    s4 = (oddSlot == 3) ? diffSym : matchSym;
                    break;
                }

                case OutcomeType.Pair:
                {
                    TrophySymbol matchSym = pool[rng.Next(pool.Count)];
                    TrophySymbol diff1 = pool[rng.Next(pool.Count)];
                    int tries = 0;
                    while (string.Equals(diff1.PrefabName, matchSym.PrefabName, StringComparison.OrdinalIgnoreCase) && tries++ < 30)
                    {
                        diff1 = pool[rng.Next(pool.Count)];
                    }
                    TrophySymbol diff2 = pool[rng.Next(pool.Count)];
                    int tries2 = 0;
                    while ((string.Equals(diff2.PrefabName, matchSym.PrefabName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(diff2.PrefabName, diff1.PrefabName, StringComparison.OrdinalIgnoreCase)) && tries2++ < 30)
                    {
                        diff2 = pool[rng.Next(pool.Count)];
                    }

                    List<int> slots = new List<int> { 0, 1, 2, 3 };
                    int slotA = slots[rng.Next(slots.Count)];
                    slots.Remove(slotA);
                    int slotB = slots[rng.Next(slots.Count)];
                    slots.Remove(slotB);
                    int slotC = slots[0];
                    int slotD = slots[1];

                    TrophySymbol[] arr = new TrophySymbol[4];
                    arr[slotA] = matchSym;
                    arr[slotB] = matchSym;
                    arr[slotC] = diff1;
                    arr[slotD] = diff2;
                    s1 = arr[0];
                    s2 = arr[1];
                    s3 = arr[2];
                    s4 = arr[3];
                    break;
                }

                default: // Bust
                {
                    List<TrophySymbol> chosen = new List<TrophySymbol>();
                    while (chosen.Count < 4)
                    {
                        TrophySymbol sym = pool[rng.Next(pool.Count)];
                        if (!chosen.Exists(c => string.Equals(c.PrefabName, sym.PrefabName, StringComparison.OrdinalIgnoreCase)))
                        {
                            chosen.Add(sym);
                        }
                    }
                    s1 = chosen[0];
                    s2 = chosen[1];
                    s3 = chosen[2];
                    s4 = chosen[3];
                    break;
                }
            }

            return EvaluateRoll(s1, s2, s3, s4);
        }
    }
}

