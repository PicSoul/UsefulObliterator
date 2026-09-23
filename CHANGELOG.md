# Changelog

All notable changes to **Useful Obliterator** will be documented in this file.

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

