using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace UsefulObliterator
{
    public static class RecycleEngine
    {
        public class RecycleResult
        {
            public Dictionary<string, int> ItemsSalvaged = new Dictionary<string, int>();
            public int CoalGranted = 0;
            public string SummaryText = "";
        }

        /// <param name="playerID">Who pulled the lever - the Boss Jackpot's idol is picked from the biomes they have found.</param>
        public static RecycleResult ProcessInventory(Container container, RollOutcome outcome, Vector3 obliteratorPosition, long playerID = 0L)
        {
            RecycleResult result = new RecycleResult();
            if (container == null || container.GetInventory() == null || ObjectDB.instance == null) return result;

            Inventory inventory = container.GetInventory();
            List<ItemDrop.ItemData> allItems = new List<ItemDrop.ItemData>(inventory.GetAllItems());
            if (allItems.Count == 0) return result;

            UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] Processing {allItems.Count} items. Outcome: {outcome.Outcome} ({outcome.Title}), PayoutRatio: {outcome.PayoutRatio:P0}");

            // Map of prefab name -> amount to return
            Dictionary<GameObject, int> payoutItems = new Dictionary<GameObject, int>();
            Dictionary<GameObject, int> preservedMaterials = new Dictionary<GameObject, int>();
            int totalLostIngredients = 0;
            int consolationCoalCount = 0;

            foreach (ItemDrop.ItemData item in allItems)
            {
                if (item == null || item.m_shared == null) continue;

                Recipe recipe = ObjectDB.instance.GetRecipe(item);
                if (recipe != null && recipe.m_resources != null && recipe.m_resources.Length > 0)
                {
                    // Item has a known crafting recipe!
                    int recipeCraftYield = Mathf.Max(1, recipe.m_amount);
                    int batchCount = Mathf.Max(1, item.m_stack / recipeCraftYield);
                    if (item.m_stack < recipeCraftYield) batchCount = 1;

                    foreach (Piece.Requirement req in recipe.m_resources)
                    {
                        if (req.m_resItem == null) continue;

                        int baseAmount = req.m_amount;
                        int upgradeAmount = req.m_amountPerLevel * Mathf.Max(0, item.m_quality - 1);
                        int totalRequired = (baseAmount + upgradeAmount) * batchCount;

                        int refundAmount = 0;
                        if (outcome.Outcome == OutcomeType.Bust)
                        {
                            // On Bust (no match): Strict floor. Low-count items burn to ash/dust
                            refundAmount = Mathf.FloorToInt(totalRequired * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.Pair)
                        {
                            // On Pair: Proportional round, at least 1 returned if total > 0
                            refundAmount = Mathf.RoundToInt(totalRequired * outcome.PayoutRatio);
                            if (refundAmount <= 0 && totalRequired > 0 && outcome.PayoutRatio > 0f)
                            {
                                refundAmount = 1;
                            }
                        }
                        else if (outcome.Outcome == OutcomeType.Triple)
                        {
                            // On Triple: Full 100% refund
                            refundAmount = Mathf.RoundToInt(totalRequired * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.Quad)
                        {
                            // On Quad: 150%+ refund
                            refundAmount = Mathf.RoundToInt(totalRequired * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.BossJackpot)
                        {
                            // On Jackpot: Over-refund (250%+)
                            refundAmount = Mathf.CeilToInt(totalRequired * outcome.PayoutRatio);
                        }

                        int lostAmount = Mathf.Max(0, totalRequired - refundAmount);
                        totalLostIngredients += lostAmount;

                        UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] {item.m_shared.m_name} -> {req.m_resItem.name}: Required={totalRequired}, Refund={refundAmount}, Lost={lostAmount}");

                        if (refundAmount > 0)
                        {
                            GameObject resPrefab = req.m_resItem.gameObject;
                            if (payoutItems.ContainsKey(resPrefab))
                                payoutItems[resPrefab] += refundAmount;
                            else
                                payoutItems[resPrefab] = refundAmount;

                            string locName = Localization.instance.Localize(req.m_resItem.m_itemData.m_shared.m_name);
                            if (result.ItemsSalvaged.ContainsKey(locName))
                                result.ItemsSalvaged[locName] += refundAmount;
                            else
                                result.ItemsSalvaged[locName] = refundAmount;
                        }
                    }
                }
                else
                {
                    // Non-recipe item (raw resource, ore, bar, trophy, meat, junk)
                    if (ModConfig.AllowGambleMaterials.Value)
                    {
                        // Gamble raw materials! Payout returns the exact same material
                        GameObject matPrefab = item.m_dropPrefab;
                        if (matPrefab == null && ObjectDB.instance != null)
                        {
                            matPrefab = ObjectDB.instance.GetItemPrefab(item.m_dropPrefab != null ? item.m_dropPrefab.name : (item.m_shared?.m_name ?? ""));
                        }
                        if (matPrefab == null && ObjectDB.instance != null)
                        {
                            foreach (GameObject p in ObjectDB.instance.m_items)
                            {
                                if (p != null && p.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name == item.m_shared?.m_name)
                                {
                                    matPrefab = p;
                                    break;
                                }
                            }
                        }

                        int refundAmount = 0;
                        if (outcome.Outcome == OutcomeType.Bust)
                        {
                            // On Bust: Strict floor. Low-count stacks (< 4 when Bust=25%) burn to ash
                            refundAmount = Mathf.FloorToInt(item.m_stack * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.Pair)
                        {
                            // On Pair: Proportional round, at least 1 returned if stack > 0
                            refundAmount = Mathf.RoundToInt(item.m_stack * outcome.PayoutRatio);
                            if (refundAmount <= 0 && item.m_stack > 0 && outcome.PayoutRatio > 0f)
                            {
                                refundAmount = 1;
                            }
                        }
                        else if (outcome.Outcome == OutcomeType.Triple)
                        {
                            // On Triple: Full 100% (or TriplePayoutPercent) refund
                            refundAmount = Mathf.RoundToInt(item.m_stack * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.Quad)
                        {
                            // On Quad: 150%+ refund
                            refundAmount = Mathf.RoundToInt(item.m_stack * outcome.PayoutRatio);
                        }
                        else if (outcome.Outcome == OutcomeType.BossJackpot)
                        {
                            // On Jackpot: Over-refund (250%+). Ceil ensures 1 item returns at least 2 (+1 gain!)
                            refundAmount = Mathf.CeilToInt(item.m_stack * outcome.PayoutRatio);
                        }

                        int lostAmount = Mathf.Max(0, item.m_stack - refundAmount);
                        totalLostIngredients += lostAmount;

                        if (refundAmount > item.m_stack)
                        {
                            UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] Gambled {item.m_shared?.m_name}: Stack={item.m_stack} -> WON BIG {refundAmount} (+{refundAmount - item.m_stack} profit)");
                        }
                        else
                        {
                            UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] Gambled {item.m_shared?.m_name}: Stack={item.m_stack} -> Returned {refundAmount} (Lost {lostAmount})");
                        }

                        if (refundAmount > 0 && matPrefab != null)
                        {
                            if (payoutItems.ContainsKey(matPrefab))
                                payoutItems[matPrefab] += refundAmount;
                            else
                                payoutItems[matPrefab] = refundAmount;

                            string locName = Localization.instance.Localize(item.m_shared.m_name);
                            if (result.ItemsSalvaged.ContainsKey(locName))
                                result.ItemsSalvaged[locName] += refundAmount;
                            else
                                result.ItemsSalvaged[locName] = refundAmount;
                        }
                    }
                    else
                    {
                        string rawBehavior = ModConfig.RawItemsBehavior.Value;
                        if (string.Equals(rawBehavior, "Refund", StringComparison.OrdinalIgnoreCase))
                        {
                            int refund = Mathf.FloorToInt(item.m_stack * outcome.PayoutRatio);
                            if (outcome.Outcome != OutcomeType.Bust && refund <= 0 && item.m_stack > 0) refund = 1;

                            if (refund > 0 && item.m_dropPrefab != null)
                            {
                                if (payoutItems.ContainsKey(item.m_dropPrefab))
                                    payoutItems[item.m_dropPrefab] += refund;
                                else
                                    payoutItems[item.m_dropPrefab] = refund;

                                string locName = Localization.instance.Localize(item.m_shared.m_name);
                                if (result.ItemsSalvaged.ContainsKey(locName))
                                    result.ItemsSalvaged[locName] += refund;
                                else
                                    result.ItemsSalvaged[locName] = refund;
                            }

                            int rawLost = Mathf.Max(0, item.m_stack - refund);
                            totalLostIngredients += rawLost;
                        }
                        else
                        {
                            // Only convert unhandled raw items to coal if coal was already in the obliterator
                            bool isCoal = (item.m_dropPrefab != null && item.m_dropPrefab.name == "Coal") || 
                                          string.Equals(item.m_shared?.m_name, "$item_coal", StringComparison.OrdinalIgnoreCase);

                            if (isCoal)
                            {
                                int coalYield = Mathf.FloorToInt(item.m_stack * outcome.PayoutRatio);
                                consolationCoalCount += coalYield;
                            }
                        }
                    }
                }
            }

            // Consolation Coal is strictly disabled unless Coal was explicitly placed into the container to gamble
            bool inputContainedCoal = false;
            foreach (var itm in allItems)
            {
                if (itm != null && ((itm.m_dropPrefab != null && itm.m_dropPrefab.name == "Coal") || 
                    string.Equals(itm.m_shared?.m_name, "$item_coal", StringComparison.OrdinalIgnoreCase)))
                {
                    inputContainedCoal = true;
                    break;
                }
            }

            if (!inputContainedCoal)
            {
                consolationCoalCount = 0;
            }

            // Consolation Coal addition to inventory
            if (consolationCoalCount > 0 && inputContainedCoal)
            {
                GameObject coalPrefab = ObjectDB.instance.GetItemPrefab("Coal");
                if (coalPrefab != null)
                {
                    if (payoutItems.ContainsKey(coalPrefab))
                        payoutItems[coalPrefab] += consolationCoalCount;
                    else
                        payoutItems[coalPrefab] = consolationCoalCount;

                    result.CoalGranted += consolationCoalCount;
                }
            }

            // Boss Jackpot bonus treasure (ONLY for BossJackpot outcome!)
            if (outcome.Outcome == OutcomeType.BossJackpot && ModConfig.JackpotBonusLoot.Value)
            {
                UsefulObliteratorPlugin.Log.LogInfo("[RecycleEngine] Awarding Boss Jackpot bonus treasure loot!");
                AddBonusLoot(payoutItems, result);
            }

            // A Boss Jackpot also brings an idol for the Forge of Potential - one from a biome the
            // player who pulled the lever has already found, so it gives nothing away.
            if (outcome.Outcome == OutcomeType.BossJackpot && ModConfig.JackpotIdol.Value)
            {
                GameObject idol = IdolCatalog.PickFor(playerID);
                if (idol != null)
                {
                    payoutItems[idol] = (payoutItems.TryGetValue(idol, out int had) ? had : 0) + 1;
                    string locName = Localization.instance.Localize(idol.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
                    result.ItemsSalvaged[locName] = (result.ItemsSalvaged.ContainsKey(locName) ? result.ItemsSalvaged[locName] : 0) + 1;
                    UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] Boss Jackpot idol: {idol.name}");
                }
            }

            // Clear original items
            inventory.RemoveAll();

            // Return preserved materials untouched
            foreach (KeyValuePair<GameObject, int> kvp in preservedMaterials)
            {
                AddToInventory(inventory, kvp.Key, kvp.Value, obliteratorPosition);
            }

            // Populate container with payouts
            foreach (KeyValuePair<GameObject, int> kvp in payoutItems)
            {
                AddToInventory(inventory, kvp.Key, kvp.Value, obliteratorPosition);
            }

            // CRITICAL: Ensure container ownership is claimed and ZDO is explicitly saved so inventory persists across network and compatible mods (e.g. MultiUserChest)
            try
            {
                ZNetView containerNview = container.GetComponent<ZNetView>() ?? container.m_rootObjectOverride?.GetComponent<ZNetView>();
                if (containerNview != null && containerNview.GetZDO() != null)
                {
                    if (!containerNview.IsOwner())
                    {
                        containerNview.ClaimOwnership();
                    }

                    ZPackage pkg = new ZPackage();
                    inventory.Save(pkg);
                    containerNview.GetZDO().Set(ZDOVars.s_items, pkg.GetArray());
                    Traverse.Create(container).Field("m_lastRevision").SetValue(containerNview.GetZDO().DataRevision);
                    UsefulObliteratorPlugin.Log.LogInfo($"[RecycleEngine] Successfully saved {inventory.NrOfItems()} container items directly to ZDO (revision {containerNview.GetZDO().DataRevision}).");
                }

                Traverse.Create(container).Method("Save").GetValue();
            }
            catch (Exception ex)
            {
                UsefulObliteratorPlugin.Log.LogWarning($"[RecycleEngine] Failed to save container ZDO: {ex.Message}");
            }

            // Build summary text
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> kvp in result.ItemsSalvaged)
            {
                parts.Add($"{kvp.Value}x {kvp.Key}");
            }
            if (result.CoalGranted > 0)
            {
                parts.Add($"+{result.CoalGranted} Coal");
            }

            result.SummaryText = parts.Count > 0 ? "Payout: " + string.Join(", ", parts) : "Obliterated into dust.";
            return result;
        }

        private static void AddBonusLoot(Dictionary<GameObject, int> payoutItems, RecycleResult result)
        {
            // Bonus coins for Haldor
            GameObject coinsPrefab = ObjectDB.instance.GetItemPrefab("Coins");
            if (coinsPrefab != null)
            {
                int coinBonus = UnityEngine.Random.Range(75, 151);
                if (payoutItems.ContainsKey(coinsPrefab))
                    payoutItems[coinsPrefab] += coinBonus;
                else
                    payoutItems[coinsPrefab] = coinBonus;

                string locName = Localization.instance.Localize(coinsPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
                result.ItemsSalvaged[locName] = (result.ItemsSalvaged.ContainsKey(locName) ? result.ItemsSalvaged[locName] : 0) + coinBonus;
            }

            // High-value merchant valuables sellable to Haldor for gold coins
            string[] merchantTreasures = { "Ruby", "AmberPearl", "SilverNecklace", "Amber" };
            
            // Award 1 to 2 distinct treasure types
            int treasureTypesCount = UnityEngine.Random.Range(1, 3);
            List<string> availableTreasures = new List<string>(merchantTreasures);

            for (int t = 0; t < treasureTypesCount && availableTreasures.Count > 0; t++)
            {
                int pickIdx = UnityEngine.Random.Range(0, availableTreasures.Count);
                string chosenTreasure = availableTreasures[pickIdx];
                availableTreasures.RemoveAt(pickIdx);

                GameObject gemPrefab = ObjectDB.instance.GetItemPrefab(chosenTreasure);
                if (gemPrefab != null)
                {
                    int gemCount = UnityEngine.Random.Range(1, 4);
                    if (payoutItems.ContainsKey(gemPrefab))
                        payoutItems[gemPrefab] += gemCount;
                    else
                        payoutItems[gemPrefab] = gemCount;

                    string locName = Localization.instance.Localize(gemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
                    result.ItemsSalvaged[locName] = (result.ItemsSalvaged.ContainsKey(locName) ? result.ItemsSalvaged[locName] : 0) + gemCount;
                }
            }
        }

        private static void AddToInventory(Inventory inv, GameObject prefab, int amount, Vector3 fallbackDropPos)
        {
            if (prefab == null || amount <= 0) return;
            ItemDrop drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return;

            int maxStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            while (amount > 0)
            {
                int toAdd = Mathf.Min(amount, maxStack);
                ItemDrop.ItemData itemAdded = inv.AddItem(prefab.name, toAdd, drop.m_itemData.m_quality, drop.m_itemData.m_variant, 0L, "", false);
                if (itemAdded == null)
                {
                    // Container is packed to the brim; spawn excess in world right in front of the obliterator!
                    Vector3 dropPos = fallbackDropPos + Vector3.up * 1f + UnityEngine.Random.insideUnitSphere * 0.5f;
                    ItemDrop.DropItem(drop.m_itemData, toAdd, dropPos, Quaternion.identity);
                }
                amount -= toAdd;
            }
        }
    }
}

