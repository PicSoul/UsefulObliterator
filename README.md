# Useful Obliterator

**Turn the Valheim Obliterator into an authentic Norse slot machine with recipe-aware recycling payouts.**

In vanilla Valheim, the Obliterator turns everything into Coal. Other recycling mods often reduce the game to an instant, sterile button click. **Useful Obliterator** makes recycling an exciting gamble: drop your obsolete armor, weapons, or spare gear into the Obliterator, pull the lever, and watch three creature-head reels spin during Thor's lightning wind-up!

---

## How It Works

1. **Load Up**: Place any crafted items, gear, weapons, or materials into the Obliterator's chest.
2. **Pull the Lever**: When you pull the handle, the slot reels appear on your screen and begin spinning with authentic clinking sounds.
3. **The Roll**: Three creature heads (Boar, Troll, Wolf, Eikthyr, and more) clink to a stop one by one over ~5 seconds.
4. **Thor Strikes**: Thor's lightning bolt crashes down on the Obliterator, resolving the payout into the chest and displaying a summary of your salvaged materials.

---

## Payout Tiers

| Roll Result | Example | Default Refund Rate | Notes |
| :--- | :--- | :---: | :--- |
| **Bust** | Boar • Troll • Skeleton | **25%** | No matching heads. Returns a quarter of all crafting ingredients + consolation Coal for what was lost. |
| **Pair** | Wolf • Wolf • Greydwarf | **60%** | Two matching heads. Recovers over half of all original crafting materials. |
| **Triple Match** | Troll • Troll • Troll | **100%** | Three identical creature heads. Full 100% refund of all crafting materials and upgrade costs! |
| **Boss Jackpot** | Eikthyr • Eikthyr • Eikthyr | **125%** | Three matching Boss heads. Over-refunds materials (125%) and drops bonus treasure (Coins, Amber, Thunderstone). |

*All payout percentages, odds, and consolation rewards are fully customizable in the config file.*

---

## Recipe-Aware Breakdown

- **Quality Level Aware**: Upgraded items (e.g. Level 4 Iron Atgeir) calculate total ingredients used across all upgrade levels before applying the roll multiplier.
- **Bulk Items**: Arrows and consumables are scaled proportionally to their recipe batch sizes.
- **Non-Recipe Items**: Raw materials (wood, stone, meat, etc.) convert to Coal (scaled by your roll multiplier) or can be configured to be returned safely.

---

## Configuration

The config file is created at `BepInEx/config/com.pics0ul.valheim.usefulobliterator.cfg`. When running on a dedicated server, settings sync automatically to all connected clients.

- `WinRatePercent`: Base chance (0-100%) to win a Pair, Triple, or Boss Jackpot (default: `60.0`). Higher rates increase Triples and eliminate Busts; at 100%, Boss Jackpots have up to a 25% chance.
- `BustPayoutPercent`: Percent refund on no matches (default: `25.0`)
- `PairPayoutPercent`: Percent refund on a pair (default: `60.0`)
- `TriplePayoutPercent`: Percent refund on a triple match (default: `100.0`)
- `BossJackpotPayoutPercent`: Percent refund on three boss heads (default: `125.0`)
- `OnlyAllowCraftedItems`: Block spinning raw materials (wood, stone, coal) and force only crafted items (default: `true`)
- `ConsolationCoal`: Give coal for lost ingredients (default: `true`)
- `JackpotBonusLoot`: Drop bonus treasure on a Boss Jackpot (default: `true`)
- `ShowReelHUD`: Toggle on-screen slot reels display (default: `true`)
- `EnableSlotAudio`: Play mechanical reel clinking sound effects (default: `true`)

---

## Installation

- **Gale / Mod Manager**: Install via your mod manager.
- **Manual**: Place `UsefulObliterator.dll` into your `Valheim/BepInEx/plugins/` directory.

