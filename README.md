# Useful Obliterator

**Turn the Valheim Obliterator into a Norse slot machine with recipe-aware recycling payouts.**

In vanilla Valheim, the Obliterator turns everything into Coal. Other recycling mods often reduce the game to an instant, sterile button click. **Useful Obliterator** makes recycling an exciting gamble: drop your obsolete armor, weapons, or spare gear into the Obliterator, pull the lever, and watch four creature-head reels spin during Thor's lightning wind-up!

---

## How It Works

1. **Load Up**: Place any crafted items, gear, weapons, or materials into the Obliterator's chest.
2. **Pull the Lever**: When you pull the handle, the slot reels appear on your screen and begin spinning with authentic clinking sounds.
3. **The Roll**: Four creature heads (Boar, Troll, Wolf, Eikthyr, and more) clink to a stop one by one over ~5 seconds.
4. **Thor Strikes**: Thor's lightning bolt crashes down on the Obliterator, resolving the payout into the chest and displaying a summary of your salvaged materials.
5. **Spin Again?**: If anything is left in the chest, press your Use key while the result is showing to spin again - or walk away with your loot.

While the machine is on screen, a strip beneath it shows **what is inside the Obliterator**. When the payout lands it updates on the spot: what you won is marked with a green **+N**, what you lost with a red **-N**, and anything the spin took completely stays on the strip, faded, so you can see what went. Nearby players watching the spin see it too.

---

## Payout Tiers

| Roll Result | Example | Default Refund Rate | Notes |
| :--- | :--- | :---: | :--- |
| **Bust** | Boar • Troll • Skeleton • Wolf | **25%** | No matching heads. Returns a quarter of all crafting ingredients + consolation Coal for what was lost. |
| **Pair** | Wolf • Wolf • Greydwarf • Boar | **50%** | Two matching heads. Recovers half of all original crafting materials. |
| **Triple Match** | Troll • Troll • Troll • Boar | **100%** | Three matching heads. Full refund of all crafting materials and upgrade costs! |
| **4 of a Kind** | Wolf • Wolf • Wolf • Wolf | **150%** | Four matching creature heads. Over-refunds materials. |
| **Boss Jackpot** | Eikthyr • Eikthyr • Eikthyr • Eikthyr | **250%** | Four matching Boss heads. Over-refunds materials and adds bonus treasure and a Forge of Potential idol (see below). |

*All payout percentages, odds, and consolation rewards are fully customizable in the config file.*

### Boss Jackpot rewards

On top of the 250% refund, a Boss Jackpot brings:

- **Treasure**: 75-150 Coins, plus one or two of Ruby, Amber Pearl, Silver Necklace or Amber.
- **An idol for the Forge of Potential**: one idol at random - Battle or Protection - from the biomes **the player who pulled the lever has already discovered**. Nobody is handed a Deep North idol before they have set foot in the Deep North, so the jackpot never spoils what is ahead.

| Idol | Found once you have discovered |
| :--- | :--- |
| Wooden | Meadows |
| Bronze | Black Forest |
| Iron | Swamp |
| Silver | Mountain |
| Black Metal | Plains |
| Black Marble | Mistlands |
| Flametal | Ashlands |
| Bloodgold | Deep North |

The idols are read from the Forge of Potential's own recipes, so any idol another mod adds the same way is included too.

---

## Recipe-Aware Breakdown

- **Quality Level Aware**: Upgraded items (e.g. Level 4 Iron Atgeir) calculate total ingredients used across all upgrade levels before applying the roll multiplier.
- **Bulk Items**: Arrows and consumables are scaled proportionally to their recipe batch sizes.
- **Non-Recipe Items**: Raw materials (wood, stone, meat, etc.) convert to Coal (scaled by your roll multiplier) or can be configured to be returned safely.

---

## Configuration

The config file is created at `BepInEx/config/com.pics0ul.valheim.usefulobliterator.cfg`. When running on a dedicated server, settings in sections 1-3 sync automatically to all connected clients.

**Payouts**
- `WinRatePercent`: Base chance (0-100%) to win a Pair, Triple, 4 of a Kind or Boss Jackpot (default: `40.0`). At 100%, losses never occur.
- `BustPayoutPercent`: Percent refund on no matches (default: `25.0`)
- `PairPayoutPercent`: Percent refund on a pair (default: `50.0`)
- `TriplePayoutPercent`: Percent refund on a triple match (default: `100.0`)
- `QuadPayoutPercent`: Percent refund on four matching creature heads (default: `150.0`)
- `BossJackpotPayoutPercent`: Percent refund on four boss heads (default: `250.0`)

**Mechanics**
- `AllowGambleMaterials`: Allow raw materials to be gambled - winning rolls grant more, losing rolls take some (default: `true`)
- `OnlyAllowCraftedItems`: Require at least one crafted item in the chest to spin (default: `false`)
- `ConsolationCoal`: Give coal for lost ingredients (default: `true`)
- `JackpotBonusLoot`: Add bonus treasure on a Boss Jackpot (default: `true`)
- `JackpotIdol`: Add a Forge of Potential idol from the puller's discovered biomes on a Boss Jackpot (default: `true`)
- `RawItemsBehavior`: `Coal` or `Refund` for items without a crafting recipe (default: `Coal`)

**Interface** (each player's own)
- `ShowReelHUD`: Toggle on-screen slot reels display (default: `true`)
- `EnableSlotAudio`: Play mechanical reel clinking sound effects (default: `true`)
- `SlotDisplayDistance`: How close nearby players must be to see and hear a spin, in metres (default: `5.0`)
- `ShowObliteratorContents`: Show the Obliterator's contents, with wins and losses, beneath the slot machine (default: `true`)
- `ResultDisplayDuration`: Seconds the result stays on screen after a spin (default: `3.0`)

---

## Multiplayer

Client and server must run the same version. The spin itself is worked out by whoever runs the Obliterator (usually the server), and each player's game tells it which biomes that player has discovered just before their spin, so the idol matches the player who pulled the lever.

---

## Translations

Useful Obliterator follows the language set in the game, and changes the moment you change it. It
ships with every language the game offers, the community translations included:

English, Swedish, French, Italian, German, Spanish, Russian, Romanian, Bulgarian, Macedonian, Finnish,
Danish, Norwegian, Icelandic, Turkish, Lithuanian, Czech, Hungarian, Slovak, Polish, Dutch, European
and Brazilian Portuguese, Simplified and Traditional Chinese, Japanese, Korean, Hindi, Thai, Croatian,
Georgian, Greek, Serbian, Ukrainian and Latvian.

The translations other than English are machine-made, so corrections from native speakers are very
welcome. Abenaki, which the game also offers, is left in English rather than guessed at - a speaker's
translation would be gladly included.

To add a language or correct one, no rebuild is needed:

1. Copy the [English file](https://github.com/PicSoul/UsefulObliterator/blob/main/translations/English.yml).
2. Name it `Useful Obliterator.<Language>.yml`, using the game's name for the language - for example
   `Useful Obliterator.Italian.yml` or `Useful Obliterator.Japanese.yml`.
3. Translate the text on the right of each line, leaving the key on the left alone and keeping `$1`
   where the mod fills in a value.
4. Put it anywhere inside the `BepInEx` folder. A file there wins over the built-in translation, so it
   also works for fixing one.

Send it over as an issue or pull request on GitHub and it will ship with the next version.

Translations are handled by [LocalizationManager](https://github.com/blaxxun-boop/LocalizationManager)
by blaxxun (MIT licence), the same system many Valheim mods use.

---

## Installation

- **Gale / Mod Manager**: Install via your mod manager.
- **Manual**: Place `UsefulObliterator.dll` into your `Valheim/BepInEx/plugins/` directory.
