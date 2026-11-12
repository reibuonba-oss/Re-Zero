# Rem NPC — Probably Stolen

A standalone maid-merchant NPC mod for **Probably Stolen**.

## Features

- Rem is guaranteed to be included in the customer pool on Day 1.
- From Day 2 onward, the mod makes one 10% appearance roll per in-game day.
- Rem sells a rotating selection of valid drinks, food, and selected luxury goods.
- Every offered item is validated against the game's item directory and positive value before it is placed on the counter. Missing/invalid IDs are skipped and logged rather than shown as broken `?` items.
- The first time you meet Rem, she gives you a business card with her private number (**622202**).
- Calling Rem and choosing the appointment option queues her for the next in-game day. The first call marks her as dialed, so she appears in the game's phone book afterward.
- Vietnamese and English localization are included.

## Installation

1. Copy the contents of the packaged `RemMaidNPC` folder into a mod folder so `manifest.xml`, `RemMaidNPC.dll`, `Localization/`, and `Textures/` sit at the mod root.
2. Enable **Rem — Maid Merchant** in the game's mod manager.

## Compatibility note

The repository's `Manager.zip` reference assembly identifies itself as **VERSION 100-Rev3**. The DLL is compiled against that supplied API surface. A successful build does not replace an in-game smoke test on the exact game version installed by the player. Portrait registration follows the same `SpriteDict` extension pattern as the supplied Rei/Yue NPC sample; the game does not expose a first-party API for custom customer portrait registration.
