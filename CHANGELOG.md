# Changelog

All notable changes to **Useful Obliterator** will be documented in this file.

## 0.2.1

- **Translations.** Every word the mod shows - the lever, the slot machine, the result banners, the
  payout summary and its messages - now comes from a translation file, through
  [LocalizationManager](https://github.com/blaxxun-boop/LocalizationManager). The mod follows the
  language set in the game and ships with every language the game offers, the community translations
  included, except Abenaki: 35 in all. Translations other than English are machine-made; corrections
  from native speakers are welcome. Anyone can add a language, or correct one, by dropping a
  `Useful Obliterator.<Language>.yml` file into the BepInEx folder - see the README. Thanks to the
  player who asked for this.
- The payout summary and result banner are now put into words on each player's own screen. The spin
  runs wherever the Obliterator does, often the server, so text made there would have arrived in the
  server's language; it now arrives as keys, and item and creature names come out as each player's
  game names them.
- Changing the language in the settings changes the mod's words straight away, the slot machine's
  labels included, and the creature named in the result banner follows it too.
- The pair banner now reads "PAIR! 2x BOAR!", matching the other results, instead of "PAIR OF BOARS!",
  whose English plural could not be carried into other languages.

## 0.2.0

- A Boss Jackpot now also brings an idol for the Forge of Potential. It is picked at random from
  the idols of biomes the player who pulled the lever has already discovered - Wooden for the
  Meadows up to Bloodgold for the Deep North - so a jackpot never hands out a spoiler. The idols
  are read from the forge's own recipes, so ones added by other mods count too. The spin is
  worked out wherever the Obliterator runs, usually the server, which cannot see a player's
  discovered biomes, so each player's game sends them just before its spin. New server setting
  `JackpotIdol`, on by default.
- The slot machine now shows what is inside the Obliterator, in a strip beneath the cabinet. It
  is counted when the spin starts and updates as the payout lands: a green +N for what was won,
  a red -N for what was lost, and anything taken completely stays on the strip, faded. Nearby
  players watching the spin see it too. The spin-again banner moves below it. New client
  setting `ShowObliteratorContents`, on by default.
- The README had fallen behind the mod - it still described three reels and the old payout
  rates. It now covers the four reels, 4 of a Kind, the current defaults, every setting, and what
  a Boss Jackpot actually pays out.

## 0.1.2

- The slot panel no longer fills the log with font warnings. Every label was created as an active
  object, so TextMeshPro woke up, found no font, and reached for one the game does not ship - a
  warning each time, nineteen per panel. The real font was assigned a line later, so the text was
  always correct and only the log suffered. Labels are now built with their font already in place.

## 0.1.1

- The high-resolution trophy icons are found again when the mod is installed from a package
  rather than copied in by hand. They were looked for in an `icons` folder beside the plugin,
  which is where they sit after a hand copy and is not where an installer puts them - Gale lays
  a package out flat, next to the DLL. The result was a mod that showed its own artwork all
  through testing and quietly fell back to the game's low-resolution trophies the moment it was
  installed the way a player installs it, with identical code in both. The folder is searched
  now instead of assumed, so it no longer matters how an installer arranges things.

## 0.1.0

- Initial release.
- Transforms the Obliterator into a Norse 3-reel slot machine with spinning creature heads (trophies).
- Recipe-aware item salvage: refunds crafting materials based on quality levels and stack sizes.
- Four payout tiers:
  - **Bust**: 25% refund + consolation Coal.
  - **Pair**: 60% refund.
  - **Triple**: 100% full refund.
  - **Boss Jackpot**: 125% refund + bonus treasure loot.
- Animated HUD slot reels with sequential stops and audio clinks/thuds synced to Thor's lightning strike.
- Full ServerSync and BepInEx configuration support for payout rates, odds, and audio settings.

