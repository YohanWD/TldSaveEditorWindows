# In-game test: "EDITOR TEST" save (sandbox4)

`Survival\sandbox4` is a full copy of `sandbox3` ("1000Days", day 323, Mystery Lake), written by the editor.
`sandbox3` itself was not modified. Delete `sandbox4` when you're done.

Quit the game completely before starting, then launch it and load **EDITOR TEST**.

## 1. Basics
- [ ] The save appears in the load menu as "EDITOR TEST"
- [ ] It loads without an error and the world looks normal (same place, day 323)
- [ ] You're overweight. This is expected: 66 items were added.

## 2. Edits to existing items
- [ ] Softwood: the stack you already had now shows **50**
- [ ] Hatchet: your original one is at **42%**. A new 100% Hatchet was also added.
- [ ] Your old **Cloth ×2** stack is gone. A new **Cloth ×10** was added.

## 3. Added items: check each one exists and works
| Group | Items | What to try |
|---|---|---|
| Fire starting | Hardwood ×10, WoodMatches ×30, PackMatches ×12, Accelerant ×3, MagnifyingLens, Firestriker | Start a fire with matches + accelerant |
| First aid | HeavyBandage ×5, Antibiotics ×6, PainKillers ×6, WaterPurificationTablets ×10, EmergencyStim | Counts in the First aid tab |
| Food & drink | CannedBeans, MRE, Soda, CookedMeatDeer, RawMeatDeer, Water (potable), Water1000ml, Flour, Salt, MapleSyrup | Full calories, not spoiled; eat one, cook raw meat |
| Clothing | BearSkinCoat, MuklukBoots, WolfSkinCape, BallisticVest, Crampons | Shown as **not worn**, 100%, dry; put one on |
| Weapons | Rifle, Revolver, FlareGun, Bow_Bushcraft + Arrow ×10, RifleAmmo ×20, RevolverAmmo ×20, FlareGunAmmo ×5 | Load the rifle, fire once |
| Light | FlareA, BlueFlare, Flashlight, KeroseneLampB, LampFuelFull, Torch | Light the lamp, refuel it with the lamp fuel |
| Tools | Hatchet, Knife, CookingPot, BedRoll, Snare, HandheldShortwave, SprayPaintCan, BookCooking, Hacksaw, Prybar, SewingKit, SharpeningStone, Rope, Travois, TechnicalBackpack | Sleep in the bedroll, read the book, place the snare |
| **Insulated flask** | **Variants A to F** (6 flasks) | All 6 shown, empty; **fill one** with tea or coffee |
| Materials | Cloth ×10, ScrapMetal ×5, Gut ×4, LeatherDried ×3, MapleSapling ×2 | Put the gut down to cure |

## 4. The round trip (most important)
- [ ] Play a bit, then save (sleep or change area), and quit the game
- [ ] Open **EDITOR TEST** in the editor: it loads, and the changes you made in game show up (e.g. the filled flask, ammo fired)
- [ ] Edit something small again, save, and reload in game

Write down anything missing, renamed, or that behaves oddly (item name + what happened).
