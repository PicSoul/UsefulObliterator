using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace UsefulObliterator
{
    [HarmonyPatch]
    public static class IncineratorPatches
    {
        [HarmonyPatch(typeof(Hud), "Awake")]
        [HarmonyPostfix]
        private static void Hud_Awake_Postfix()
        {
            SlotUI.EnsureCreated();
        }

        [HarmonyPatch(typeof(Incinerator), "Awake")]
        [HarmonyPostfix]
        private static void Incinerator_Awake_Postfix(Incinerator __instance)
        {
            SlotEngine.EnsureValidated();

            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || nview.GetZDO() == null) return;

            nview.Register<ZPackage>("RPC_UsefulObliterator_StartSpin", 
                (long sender, ZPackage pkg) =>
                {
                    RPC_StartSpin(__instance, pkg);
                });

            nview.Register<int>("RPC_UsefulObliterator_Celebration", 
                (long sender, int outcomeInt) =>
                {
                    RPC_PlayCelebration(__instance, (OutcomeType)outcomeInt);
                });

            nview.Register<string>("RPC_UsefulObliterator_Summary", 
                (long sender, string summary) =>
                {
                    RPC_ShowSummary(summary);
                });
        }

        [HarmonyPatch(typeof(Incinerator), "GetLeverHoverText")]
        [HarmonyPostfix]
        private static void Incinerator_GetLeverHoverText_Postfix(Incinerator __instance, ref string __result)
        {
            if (!PrivateArea.CheckAccess(__instance.transform.position)) return;

            // Hide lever text once pulled or while incinerator is in use
            bool inUse = (bool)Traverse.Create(__instance).Field("isInUse").GetValue();
            if (inUse || (__instance.m_container != null && __instance.m_container.IsInUse()))
            {
                __result = "";
                return;
            }

            string useKey = Localization.instance.Localize("<color=yellow><b>$KEY_Use</b></color>");
            string mode = ModConfig.OnlyAllowCraftedItems.Value ? "(Recycle Crafted Items)" : "(Slots & Salvage)";
            __result = Localization.instance.Localize($"[{useKey}] Pull lever <color=#66e0ff>{mode}</color>");
        }

        [HarmonyPatch(typeof(Incinerator), "OnIncinerate")]
        [HarmonyPrefix]
        private static bool Incinerator_OnIncinerate_Prefix(Incinerator __instance, Switch sw, Humanoid user, ItemDrop.ItemData item)
        {
            bool inUse = (bool)Traverse.Create(__instance).Field("isInUse").GetValue();
            if (inUse || (__instance.m_container != null && __instance.m_container.IsInUse()))
            {
                return false;
            }

            if (__instance.m_container != null)
            {
                ZNetView cView = __instance.m_container.GetComponent<ZNetView>() ?? __instance.m_container.m_rootObjectOverride?.GetComponent<ZNetView>();
                if (cView != null && !cView.IsOwner())
                {
                    cView.ClaimOwnership();
                }
            }

            if (ModConfig.OnlyAllowCraftedItems.Value && !ModConfig.AllowGambleMaterials.Value && ObjectDB.instance != null && __instance.m_container != null)
            {
                var allItems = __instance.m_container.GetInventory().GetAllItems();
                int craftedCount = 0;
                foreach (var itm in allItems)
                {
                    if (itm == null) continue;
                    Recipe recipe = ObjectDB.instance.GetRecipe(itm);
                    if (recipe != null && recipe.m_resources != null && recipe.m_resources.Length > 0)
                    {
                        craftedCount++;
                    }
                }

                if (craftedCount == 0)
                {
                    user?.Message(MessageHud.MessageType.Center, "Thor requires at least one crafted item to spin! Cannot spin materials only.");
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(Incinerator), "RPC_RequestIncinerate")]
        [HarmonyPrefix]
        private static bool Incinerator_RPC_RequestIncinerate_Prefix(Incinerator __instance, long uid, long playerID)
        {
            ZNetView nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return false;

            if (__instance.m_container != null)
            {
                ZNetView cView = __instance.m_container.GetComponent<ZNetView>() ?? __instance.m_container.m_rootObjectOverride?.GetComponent<ZNetView>();
                if (cView != null && !cView.IsOwner())
                {
                    cView.ClaimOwnership();
                }
            }

            bool inUse = (bool)Traverse.Create(__instance).Field("isInUse").GetValue();
            if (__instance.m_container.IsInUse() || inUse)
            {
                nview.InvokeRPC(uid, "RPC_IncinerateRespons", (int)Incinerator.Response.Fail);
                return false;
            }

            if (__instance.m_container.GetInventory().NrOfItems() == 0)
            {
                nview.InvokeRPC(uid, "RPC_IncinerateRespons", (int)Incinerator.Response.Empty);
                return false;
            }

            // Server-side enforcement: require at least one crafted item only if gambling materials is explicitly disabled
            if (ModConfig.OnlyAllowCraftedItems.Value && !ModConfig.AllowGambleMaterials.Value && ObjectDB.instance != null)
            {
                var allItems = __instance.m_container.GetInventory().GetAllItems();
                int craftedCount = 0;
                foreach (var itm in allItems)
                {
                    if (itm == null) continue;
                    Recipe recipe = ObjectDB.instance.GetRecipe(itm);
                    if (recipe != null && recipe.m_resources != null && recipe.m_resources.Length > 0)
                    {
                        craftedCount++;
                    }
                }

                if (craftedCount == 0)
                {
                    string rejectMsg = "Thor requires at least one crafted item to spin! Cannot spin materials only.";
                    UsefulObliteratorPlugin.Log.LogInfo("[UsefulObliterator] Blocked lever pull: container contains no crafted items.");
                    nview.InvokeRPC(uid, "RPC_UsefulObliterator_Summary", rejectMsg);
                    nview.InvokeRPC(uid, "RPC_IncinerateRespons", (int)Incinerator.Response.Fail);
                    return false;
                }
            }

            // Start our custom slot spin and incineration sequence
            __instance.StartCoroutine(UsefulIncinerateRoutine(__instance, uid, playerID));
            return false;
        }

        private static IEnumerator UsefulIncinerateRoutine(Incinerator incinerator, long uid, long playerID)
        {
            Traverse.Create(incinerator).Field("isInUse").SetValue(true);
            ZNetView nview = incinerator.GetComponent<ZNetView>();

            try
            {
                // Animate lever and start sound effects
                nview.InvokeRPC(ZNetView.Everybody, "RPC_AnimateLever");
                incinerator.m_leverEffects.Create(incinerator.transform.position, incinerator.transform.rotation);

                // Roll the slot outcome!
                RollOutcome outcome = SlotEngine.Roll();
                UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] Roll outcome: [{outcome.Reel1.PrefabName}, {outcome.Reel2.PrefabName}, {outcome.Reel3.PrefabName}, {outcome.Reel4.PrefabName}] -> {outcome.Outcome} ({outcome.Title}), Payout: {outcome.PayoutRatio:P0}");

                // Pack outcome into ZPackage for reliable network broadcast
                ZPackage pkg = new ZPackage();
                pkg.Write(outcome.Reel1.PrefabName);
                pkg.Write(outcome.Reel2.PrefabName);
                pkg.Write(outcome.Reel3.PrefabName);
                pkg.Write(outcome.Reel4.PrefabName);
                pkg.Write((int)outcome.Outcome);
                pkg.Write(outcome.PayoutRatio);
                pkg.Write(outcome.Title ?? "");
                pkg.Write(outcome.MatchedSymbol?.PrefabName ?? "");
                pkg.Write(playerID);

                // Broadcast roll start to all clients so nearby players can see and hear the spin
                nview.InvokeRPC(ZNetView.Everybody, "RPC_UsefulObliterator_StartSpin", pkg);

                // Spin duration is 5.2 seconds
                yield return new WaitForSeconds(5.2f);

                // Return lever
                nview.InvokeRPC(ZNetView.Everybody, "RPC_AnimateLeverReturn");

                if (!nview.IsValid() || !nview.IsOwner() || incinerator.m_container.IsInUse())
                {
                    yield break;
                }

                // Post-spin visual effects:
                // - Bust: Mechanical backfire & soot belch (zero player knockback!)
                // - Pair: clean and quiet (no effects)
                // - Triple: subtle celebration (divine levelup + single golden rocket)
                // - Quad / Boss Jackpot: grand firework celebration launched straight up from the top!
                if (outcome.Outcome == OutcomeType.Bust || outcome.Outcome == OutcomeType.Triple || outcome.Outcome == OutcomeType.Quad || outcome.Outcome == OutcomeType.BossJackpot)
                {
                    nview.InvokeRPC(ZNetView.Everybody, "RPC_UsefulObliterator_Celebration", (int)outcome.Outcome);
                }

                // Execute recycling
                RecycleEngine.RecycleResult result = RecycleEngine.ProcessInventory(
                    incinerator.m_container, outcome, incinerator.transform.position);

                // Send custom salvage summary message to the pulling player
                UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] Finished recycling. Summary: {result.SummaryText}");
                nview.InvokeRPC(uid, "RPC_UsefulObliterator_Summary", result.SummaryText);
            }
            finally
            {
                // CRITICAL: Always reset isInUse back to false so the lever prompt re-appears and can be pulled again!
                Traverse.Create(incinerator).Field("isInUse").SetValue(false);
                UsefulObliteratorPlugin.Log.LogInfo("[UsefulObliterator] Reset isInUse = false (Obliterator ready for next spin).");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, GameObject> _cachedEffectPrefabs = new System.Collections.Generic.Dictionary<string, GameObject>();

        public static GameObject GetEffectPrefab(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cachedEffectPrefabs.TryGetValue(name, out GameObject cached) && cached != null)
            {
                return cached;
            }

            // 1. ZNetScene
            GameObject go = ZNetScene.instance?.GetPrefab(name);
            if (go != null)
            {
                _cachedEffectPrefabs[name] = go;
                return go;
            }

            // 2. Fireplaces in memory
            Fireplace[] fireplaces = Resources.FindObjectsOfTypeAll<Fireplace>();
            foreach (Fireplace fp in fireplaces)
            {
                if (fp != null && fp.m_fireworkItemList != null)
                {
                    foreach (var item in fp.m_fireworkItemList)
                    {
                        if (item.m_fireworksEffects != null && item.m_fireworksEffects.m_effectPrefabs != null)
                        {
                            foreach (var eff in item.m_fireworksEffects.m_effectPrefabs)
                            {
                                if (eff.m_prefab != null && string.Equals(eff.m_prefab.name, name, StringComparison.OrdinalIgnoreCase))
                                {
                                    _cachedEffectPrefabs[name] = eff.m_prefab;
                                    return eff.m_prefab;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Search all loaded GameObjects
            GameObject[] allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject g in allGos)
            {
                if (g != null && string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _cachedEffectPrefabs[name] = g;
                    return g;
                }
            }

            return null;
        }

        private static void RPC_PlayCelebration(Incinerator incinerator, OutcomeType outcomeType)
        {
            if (incinerator == null) return;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] RPC_PlayCelebration received for {outcomeType}");
            if (outcomeType == OutcomeType.Bust)
            {
                incinerator.StartCoroutine(BustMisfireRoutine(incinerator));
            }
            else
            {
                incinerator.StartCoroutine(FireworkCelebrationRoutine(incinerator, outcomeType));
            }
        }

        private static IEnumerator BustMisfireRoutine(Incinerator incinerator)
        {
            if (incinerator == null) yield break;

            Vector3 obliteratorTop = incinerator.transform.position + Vector3.up * 2.8f;
            Vector3 obliteratorChamber = incinerator.transform.position + Vector3.up * 1.0f;
            UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] Playing Bust mechanical misfire effect at {obliteratorTop}");

            // 1. Mechanical shudder (brief minor screen shake, zero player displacement)
            GameCamera.instance?.AddShake(obliteratorChamber, 4.0f, 0.4f, true);

            // 2. Loud metallic fail clank sound
            GameObject failSfx = GetEffectPrefab("sfx_forgeofpotential_fail") 
                              ?? GetEffectPrefab("sfx_metalbars_break") 
                              ?? GetEffectPrefab("sfx_bomblava_fail")
                              ?? GetEffectPrefab("sfx_metal_blocked")
                              ?? GetEffectPrefab("sfx_build_hammer_metal");
            if (failSfx != null)
            {
                UnityEngine.Object.Instantiate(failSfx, obliteratorChamber, Quaternion.identity);
            }

            // 3. Dense cloud of black coal soot & ash bursting from the chimney top
            GameObject sootVfx = GetEffectPrefab("vfx_CoalHit") 
                              ?? GetEffectPrefab("vfx_SawDust_Ashlands") 
                              ?? GetEffectPrefab("vfx_AshPieceDestroyed")
                              ?? GetEffectPrefab("vfx_Smoked")
                              ?? GetEffectPrefab("vfx_corpse_destruction_small");
            if (sootVfx != null)
            {
                UnityEngine.Object.Instantiate(sootVfx, obliteratorTop, Quaternion.Euler(-90f, 0f, 0f));
            }

            // 4. Fiery flying sparks from the misfiring chimney
            GameObject sparksVfx = GetEffectPrefab("vfx_HitSparks") 
                                ?? GetEffectPrefab("vfx_Firework_Sparkler");
            if (sparksVfx != null)
            {
                UnityEngine.Object.Instantiate(sparksVfx, obliteratorTop, Quaternion.identity);
            }

            // Wait a brief fraction and spawn a secondary trailing soot puff as it settles
            yield return new WaitForSeconds(0.18f);
            if (incinerator == null) yield break;

            if (sootVfx != null)
            {
                UnityEngine.Object.Instantiate(sootVfx, obliteratorTop + Vector3.up * 0.4f, Quaternion.identity);
            }
        }

        private static IEnumerator FireworkCelebrationRoutine(Incinerator incinerator, OutcomeType outcomeType)
        {
            if (incinerator == null) yield break;

            Vector3 obliteratorTop = incinerator.transform.position + Vector3.up * 2.8f;
            UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] Starting celebration at {obliteratorTop} for {outcomeType}");

            if (outcomeType == OutcomeType.Triple)
            {
                // Small noticeable celebration: divine levelup light burst + 1 golden rocket
                GameObject sparkVfx = GetEffectPrefab("vfx_skilllevelup") ?? GetEffectPrefab("vfx_lootspark");
                if (sparkVfx != null)
                {
                    UnityEngine.Object.Instantiate(sparkVfx, incinerator.transform.position + Vector3.up * 0.8f, Quaternion.identity);
                }

                GameObject rocketPrefab = GetEffectPrefab("vfx_FireWork_ThunderStone") ?? GetEffectPrefab("vfx_Firework_Rocket_Yellow");
                if (rocketPrefab != null)
                {
                    UnityEngine.Object.Instantiate(rocketPrefab, obliteratorTop, Quaternion.Euler(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-4f, 4f)));
                    UsefulObliteratorPlugin.Log.LogInfo("[UsefulObliterator] Launched single celebration rocket for Triple Match!");
                }
                yield break;
            }

            bool isBossJackpot = (outcomeType == OutcomeType.BossJackpot);

            // Divine light / explosion effect at base
            string baseVfxName = isBossJackpot ? "vfx_GodExplosion" : "vfx_skilllevelup";
            GameObject godVfx = GetEffectPrefab(baseVfxName) ?? GetEffectPrefab("vfx_skilllevelup");
            if (godVfx != null)
            {
                UnityEngine.Object.Instantiate(godVfx, incinerator.transform.position + Vector3.up * 0.8f, Quaternion.identity);
            }

            if (isBossJackpot)
            {
                // Heavy screen shake rumbles the earth on Boss Jackpot!
                GameCamera.instance?.AddShake(obliteratorTop, 15f, 1.4f, true);
            }

            string[] rocketPrefabs = {
                "vfx_FireWork_ThunderStone",
                "vfx_Firework_Rocket_Cyan",
                "vfx_Firework_Rocket_Red",
                "vfx_Firework_Rocket_Blue",
                "vfx_Firework_Rocket_Green",
                "vfx_Firework_Rocket_Purple",
                "vfx_Firework_Rocket_Yellow",
                "vfx_FireWork_SurtlingCore"
            };

            int rocketCount = isBossJackpot ? 13 : 4;
            float delayBetweenRockets = isBossJackpot ? 0.22f : 0.45f;

            for (int i = 0; i < rocketCount; i++)
            {
                if (incinerator == null) yield break;

                string prefabName;
                if (i == 0 || (isBossJackpot && i == rocketCount - 1))
                {
                    prefabName = "vfx_FireWork_ThunderStone";
                }
                else
                {
                    prefabName = rocketPrefabs[UnityEngine.Random.Range(0, rocketPrefabs.Length)];
                }

                GameObject prefab = GetEffectPrefab(prefabName);
                if (prefab == null)
                {
                    foreach (string r in rocketPrefabs)
                    {
                        prefab = GetEffectPrefab(r);
                        if (prefab != null) break;
                    }
                }

                if (prefab != null)
                {
                    float spreadRange = isBossJackpot ? 12f : 6f;
                    float spreadX = UnityEngine.Random.Range(-spreadRange, spreadRange);
                    float spreadZ = UnityEngine.Random.Range(-spreadRange, spreadRange);
                    Quaternion launchRot = Quaternion.Euler(spreadX, UnityEngine.Random.Range(0f, 360f), spreadZ);

                    GameObject spawned = UnityEngine.Object.Instantiate(prefab, obliteratorTop, launchRot);
                    UsefulObliteratorPlugin.Log.LogInfo($"[UsefulObliterator] Launched firework {i + 1}/{rocketCount}: {prefab.name}");
                }
                else
                {
                    UsefulObliteratorPlugin.Log.LogWarning($"[UsefulObliterator] Could not resolve firework prefab '{prefabName}'");
                }

                yield return new WaitForSeconds(delayBetweenRockets);
            }
        }

        private static void RPC_StartSpin(Incinerator incinerator, ZPackage pkg)
        {
            if (Player.m_localPlayer == null || pkg == null) return;

            string r1 = pkg.ReadString();
            string r2 = pkg.ReadString();
            string r3 = pkg.ReadString();
            string r4 = pkg.ReadString();
            OutcomeType outcomeType = (OutcomeType)pkg.ReadInt();
            float payout = pkg.ReadSingle();
            string title = pkg.ReadString();
            string matchedPrefab = pkg.ReadString();

            long pullerPlayerID = 0L;
            if (pkg.GetArray() != null && pkg.Size() > pkg.GetPos())
            {
                pullerPlayerID = pkg.ReadLong();
            }

            bool isPuller = (Player.m_localPlayer.GetPlayerID() == pullerPlayerID);
            float dist = Vector3.Distance(Player.m_localPlayer.transform.position, incinerator.transform.position);
            float maxDist = ModConfig.SlotDisplayDistance != null ? ModConfig.SlotDisplayDistance.Value : 5.0f;

            // Non-pullers must be within the configured distance (default 5m) to see the spin HUD and hear reel audio
            if (!isPuller && dist > maxDist) return;

            TrophySymbol s1 = SlotEngine.FindSymbol(r1) ?? SlotEngine.Symbols[0];
            TrophySymbol s2 = SlotEngine.FindSymbol(r2) ?? SlotEngine.Symbols[1];
            TrophySymbol s3 = SlotEngine.FindSymbol(r3) ?? SlotEngine.Symbols[2];
            TrophySymbol s4 = SlotEngine.FindSymbol(r4) ?? SlotEngine.Symbols[3];

            Color titleColor;
            switch (outcomeType)
            {
                case OutcomeType.BossJackpot:
                    titleColor = new Color(1f, 0.85f, 0.2f);
                    break;
                case OutcomeType.Quad:
                    titleColor = new Color(1f, 0.75f, 0.3f);
                    break;
                case OutcomeType.Triple:
                    titleColor = new Color(0.4f, 1f, 0.5f);
                    break;
                case OutcomeType.Pair:
                    titleColor = new Color(1f, 0.95f, 0.4f);
                    break;
                default:
                    titleColor = new Color(1f, 0.22f, 0.22f);
                    break;
            }

            RollOutcome outcome = new RollOutcome
            {
                Reel1 = s1,
                Reel2 = s2,
                Reel3 = s3,
                Reel4 = s4,
                Outcome = outcomeType,
                PayoutRatio = payout,
                Title = !string.IsNullOrEmpty(title) ? title : outcomeType.ToString(),
                TitleColor = titleColor,
                MatchedSymbol = !string.IsNullOrEmpty(matchedPrefab) ? SlotEngine.FindSymbol(matchedPrefab) : null
            };

            SlotUI.EnsureCreated();
            if (SlotUI.Instance != null)
            {
                SlotUI.Instance.TriggerSpin(outcome, incinerator, isPuller);
            }
        }

        private static void RPC_ShowSummary(string summary)
        {
            if (Player.m_localPlayer == null || string.IsNullOrEmpty(summary)) return;

            // Display large yellow payout banner directly above the spin graphic via SlotUI
            if (SlotUI.Instance != null)
            {
                SlotUI.Instance.UpdateSummary(summary);
            }
        }
    }
}
