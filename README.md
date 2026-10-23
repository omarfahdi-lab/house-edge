# House Edge

A blackjack roguelike deckbuilder made in Unity 6. You climb a cursed casino one floor at a time. Every table is a game
of blackjack against a score target, but you build the deck, collect Vices that bend the rules, cheat when you can get
away with it, and chase huge Chips x Mult combos. Beat The House on floor 8 to win the run.

The twist: **the dealer draws from your deck too.** Stack it with 10s and you hit 20 more often, but so does the dealer.

![The balance bot playing floor 3: the shop, the floor map, dealer tables and a Pit Boss](docs/gameplay.gif)

**Play in the browser:** _coming soon on itch.io_ <!-- TODO: replace with https://<user>.itch.io/house-edge -->

| A floor-3 High Table | The shop |
| -------------------- | -------- |
| ![Table](docs/table.png) | ![Shop](docs/shop.png) |
| **Pick your path on the floor map** | **The Purist, a Pit Boss** |
| ![Rooms](docs/rooms.png) | ![Boss](docs/boss.png) |

## Highlights

- **Skill on top of luck.** A **count** by the shoe shows how rich the remaining cards are in 10s and Aces next to
  your own deck, and you can **Raise** to x2 or x3 before a deal: a raised win multiplies Mult, a lost raise costs extra
  Hands, and raising into a hot shoe draws security's Heat. **Trick cards** (Peek, Take Back, Steal, Second Thoughts,
  Ace Up the Sleeve) are one-use plays in the middle of a hand. **Vices level up** as they trigger (+25%, then +50%),
  three **Legendary rule-breakers** change blackjack itself (Tiebreaker, Bottomless Burns, Four-Card Charlie), and
  six **Feats** (The Black Jack, Triple Sevens, a Six-Card Charlie...) are rare named hands with a fanfare and a Mult
  bonus. Every boss has a **second phase** past half the target, a **slot machine** room sits on the map, and the
  **Daily Run** gets a twist each day (Wild Royals, Hot Night, Short Shoe, High Rollers, Tight Table).
- **A guided first run.** A new player's first run (or the menu's Tutorial button) stacks the first table's opening
  hands in Core (`Tutorial.Script`), so every lesson is guaranteed: a plain win, a bust saved by a Burn, then a Flush
  that clears the table on its own. A coach panel points at the one button to press and lets only that through, then
  gives one-time tips on the map and in the shop. Tests play the script and check each lesson lands.
- **Engine-free, deterministic rules.** Every rule lives in `HouseEdge.Core`, a plain C# assembly with no UnityEngine
  reference. One seeded RNG drives a whole run, so a run is just a seed plus a list of inputs: run codes, the Daily Run
  and full replays all come from that. **428 NUnit tests** run on plain `dotnet test` in CI, no Unity license needed.
- **Combos, so winning isn't enough.** A plain win scores little. The big numbers come from poker patterns inside a
  blackjack hand (Pair, Two Pair, Three of a Kind, Suited, Flush, Run, Royal Court, Low Ball) that pay only when the hand
  beats the dealer, plus a win streak. Comps level combos up like Balatro's Planet cards, combo Vices multiply them,
  and your hand shows its combos live, so Hit or Stand becomes "do I chase the Flush?". `Combos.Find` is pure, so the
  rules are tested card by card.
- **Card packs, Elites and events.** Shops sell Card, Suit, Court and Lucky packs: open one and keep 1 of 3 cards
  (Suit packs carry a Wild card that counts as any suit, for Flushes). The map also has optional **Elite tables**
  (a random Pit Boss's rule against the next table's target with fewer Hands: win a Rare Vice, lose half your gold
  but keep going) and **"?" stops** with eight short events and real trade-offs.
- **A collection and unlocks.** The profile remembers every Vice seen, boss faced and combo won. Five combo Vices
  unlock by playing (win with a Flush to unlock Flush Royale), and locked Vices are passed into the run as
  `RunOptions.LockedVices`, so a run stays a pure function of its seed and options.
- **Pit Bosses are characters.** Each has a portrait drawn in code, a title, a line, one named ability in plain words
  and a tip, shown in an entrance before their table. Bosses announce their ability when it fires ("Swaps your 7 for
  his Q") through `Table.Announce`, so the rule is never invisible.
- **Effects are small classes on hooks.** 84 Vices, 15 bosses, 6 dealers, 7 enhancements, 4 stamps and 4 curse cards
  each override only the hooks they need (`OnCardDrawn`, `OnHit`, `OnStand`, `OnBust`, `OnHandScored`, ...). Adding a
  new one never touches the table's loop.
- **Balance tuned with a bot.** A headless bot plays the real `Run` API: 1,000 full runs in about a second. It showed
  the first draft of the design couldn't be won and why, and drove every number change (see [Balance](#balance)).
- **Everything from the design doc:** 8 floors with a floor map of 8 room types, 12 Pit Bosses plus The Eye in the Sky,
  The Enforcer and a 3-phase House, Heat and casino security, side bets, Cocktails, Comps, archetype set bonuses,
  6 starting decks, 10 stakes, unlocks, the Daily Run, run codes and Endless.
- **Built entirely in code.** One bootstrap method builds the camera, a UI Toolkit interface and synthesized sound.
  Cards, suits, icons, poker chips, boss portraits and the table's felt are drawn in code (`Painter2D` vector shapes and
  generated textures, so they work on WebGL with no font fallback). Kings, Queens and Jacks are illustrated royals,
  mirrored head to toe like a real deck, and each starting deck has its own card back. A lamp lights the felt and
  cigar smoke drifts through it. The only assets are two open-source fonts and three music tracks.
- **Balatro-style scoring playback.** The dealer draws one card at a time, then each hand scores beat by beat: your
  cards count in, every Vice, Comp and card upgrade that fires shows its "+4 Mult" or "x3 Mult" where it came from, the
  Chips and Mult boxes tick up with a rising pitch, and the total lands in the score. Core records each step
  (`HandScore.Steps`), so the playback is the real calculation, not a guess. 1x/2x/4x speed, and a click skips it.
- **A Slay the Spire–style floor map.** Between tables, Shops and rooms sit on branching paths across three lanes
  (`FloorMap`, generated from the run's seed so replays still match). You see the whole floor and plan a route; paths
  never cross, the first row after a table always has a Shop, and the lounge only appears right before a table. The bot
  plans paths too, and the win rate held at about 21%.
- **A cast of old masters.** Every boss, dealer and host is a public-domain painting in a gilt frame, with a museum
  plaque underneath. The bosses keep their signature works (The Cheat is de La Tour's *Cheat with the Ace of
  Diamonds*, The Mime is Watteau's *Pierrot*, The House is Rigaud's *Louis XIV*); everyone else is dealt from a
  gallery of 320 famous portraits, from the *Mona Lisa* to *The Laughing Cavalier*, unseen ones first, so you rarely
  meet the same face twice ([the full list](docs/GALLERY.md)). Like Balatro's jokers they never walk but are always alive: they
  sway on their hooks, bob when they talk, and react (a gold glow when you lose, a grey droop when you win, an angry
  shake in a boss's phase two).
- **Rooms are scenes, not menus.** Every stop on the map is a drawn room (bottle shelves at the Bar, flickering
  monitors in the Security Office, a swinging lamp over the Loan Shark's safe, chasing bulbs in the slot hall) with
  its own host, drawn from the same parts as the bosses, who greets you in a typed speech bubble and reacts to what
  you pick. Choices are objects on the counter: drinks slide along the bar, boxes rattle and pop open, coins flip, and
  the slot machine has a lever to pull. On the map, your chip hops along the dotted path to the stop you chose.
- **Game feel.** Cards fly out of the shoe and the dealer's hole card flips over. Each result is acted out: winning
  cards hop and glow while the chips slide over to you, losing cards slump, busts tumble off the felt, and scoring
  hands pop a Chips x Mult breakdown over a turning sunburst. Losing the run rains cards and a sad trombone; winning it
  sets off fireworks.
- **Music that follows you.** Lounge jazz between tables, bossa nova at the tables and a spy-thriller track for Pit
  Bosses and security crossfade as you move through the casino. On your last Hand the band leans in and a synthesized
  heartbeat comes in. If a track is missing, synthesized loops stand in.
- **Dealers with a voice, and a run you can read.** Each named dealer comments on hands in their own way (Silent Sofia
  just raises an eyebrow), and the end screen graphs every table's score against its target.
- **Accessibility.** A four-color deck (blue diamonds, green clubs) keeps Flushes readable at a glance, and Reduced
  motion turns off screen shake, flashes, card wobble and the smoke. Every control explains itself on hover: UI
  Toolkit's own tooltips only show in the editor, so the game has a small tooltip system of its own.

**Tech:** Unity 6.6 · C# · URP 2D · UI Toolkit · Input System · NUnit · WebGL · GitHub Actions

## How to play

Each **table**: reach the target score before your 7 Hands run out. Win and you're paid gold. Run out of Hands and the
run is over. Each floor is a Low Table, two stops on a branching map, a High Table, two more stops, then a Pit Boss
with a twist and a shop. The stops are Shops and rooms laid out like Slay the Spire: you see the whole floor, start in
any lane, and follow the paths forward, so you plan where your gold goes.

**Score = Chips x Mult.** Chips start as your card total plus the outcome's base Chips; Mult starts as the outcome's
Mult. Then a winning hand adds its **combos** and **win streak**, and card enhancements, Comps and Vices add to both,
left to right.

| Outcome | Base Chips | Base Mult |
| ------- | ---------: | --------: |
| Win | 5 | 1 |
| 21 (3+ cards) | 15 | 2 |
| Blackjack | 25 | 3 |
| Five-card Charlie | 30 | 3 |
| Dealer Bust | +10 | +0 |
| Push | cards only | 1 |

| Combo (winning hands only) | Chips | Mult |
| -------------------------- | ----: | ---: |
| Pair / Suited (2 cards of one suit) | +10 | +1 |
| Two Pair | +20 | +2 |
| Flush (3+ of one suit) / Run (3+ in a row) | +30 | +3 |
| Three of a Kind | +35 | +3 |
| Royal Court (J, Q and K) / Low Ball (4+ cards, all 5 or lower) | +40 | +4 |

Each win in a row at a table adds +1 Mult to the next winning hand (up to +6).

- **Burn** throws away the card you just hit and draws again (even a bust). Burns reset every table.
- **Vices** (5 slots) are passive rule-breakers. Hold 3 of one archetype (Lucky, Risky, Cheater, Greedy, Steady, Combo)
  for a set bonus.
- **Heat:** Cheater Vices and huge hands draw attention. At 100, security drags you to The Eye in the Sky.
- **Rooms:** The Bar, Back Room (double or nothing), Pawn Shop, Loan Shark, Card Room, Lost and Found, Security
  Office, High Limit Lounge.

The full design is in [DESIGN.md](DESIGN.md). Rules it doesn't spell out are in [docs/DECISIONS.md](docs/DECISIONS.md).

## Controls

| Input | Action |
| ----- | ------ |
| `Space` | Deal, leave the shop or room, start a new run |
| `H` / `S` | Hit / Stand (or let the dealer draw / make him stand with Cold Read) |
| `D` / `P` / `B` / `R` | Double Down / Split / Burn / Surrender |
| `I` / `N` | Take / decline insurance |
| `W` | Swap with the dealer (Card Mechanic Vice) |
| `1` / `2` / `3` | Pick the next stop on the map |
| `Tab` | The map |
| Click / `Space` during scoring | Skip the playback |
| `M` | Mute music and sounds |
| `Esc` | Menu (pauses) |
| Mouse | Everything else: side bets, shop, Vices, Cocktails, the deck view |

## Architecture

```
Assets/
  Scripts/
    Core/    HouseEdge.Core   - rules only, noEngineReferences: true
      Table.cs        one blackjack table: a state machine driven by LegalActions() / Apply(action)
      Run.cs          8 floors: tables, shop, floor map, rooms, debt, Heat, bosses. Apply(RunInput)
      TableEffect.cs  the hooks. Vices, bosses, dealers and card effects all derive from it
      Vices/          84 Vices, one small class each; ViceCatalog lists them
      CardEffects.cs  enhancements, stamps and curses, run card by card before the Vices
      Balance.cs      every tunable number
      BalanceBot.cs   the bot and BalanceSimulation
    Game/    HouseEdge.Game   - GameBootstrap, UI Toolkit views, input, Feedback (sound, particles, juice)
      Ui.cs           fonts, colors, buttons, tweens, keyword coloring and the tooltip system
      Visuals.cs      Painter2D poker chips, felt markings, table lighting and smoke, the menu backdrop and particles
      CourtArt.cs     the illustrated Kings, Queens and Jacks
      Music.cs        recorded tracks crossfaded by situation, with a synthesized fallback and heartbeat
    Editor/  HouseEdge.Editor - command-line builds
  Tests/
    EditMode/ HouseEdge.Tests - NUnit tests for Core
Tools/
  CoreTests/  compiles Core + the tests for plain `dotnet test` (what CI runs)
  BalanceSim/ console runner for the balance simulation
```

- **The UI never changes game state.** Views read `Run`/`Table` and turn clicks and keys into `RunInput`s; `Run.Apply`
  checks they're legal. That's what makes replays exact: the same seed and inputs rebuild the same run.
- **Determinism:** Core never touches `UnityEngine.Random`, `System.Random`, `DateTime` or hash-order iteration. Its own
  `GameRandom` (xorshift seeded by splitmix) is passed through everything. The Daily Run's date comes in from the game.
- **Effects:** a table builds its effect list once: card rules, Comps, boss, dealer, stake rules, then your Vices in
  order. Each hook runs over that list, so trigger order is just list order.

## Balance

DESIGN.md's targets were "a starting point to tune with the balance bot". `BalanceBot` plays close to basic strategy
(and uses X-Ray Specs, Card Counter and Card Mechanic when it has them), buys Vices and Comps while keeping an interest
reserve, and plans a route across each floor's map. `Tools/BalanceSim` runs it over many seeds and reports, per table,
how often it's beaten, how many deals it took and how far past the target the score went, plus every Pit Boss's record.

What it found, in order:

1. **The draft targets couldn't be won.** Starting at 150 and doubling every floor, the bot won **0%**.
2. **Then the game was too easy, table by table.** After the first fix, tables were cleared in about 2 deals with
   the score 2-9x the target: your first winning hand usually ended the table. Players felt it before the numbers did.
3. **Blackjack scoring is lumpy.** About half of all hands score 0, so raising targets alone turned tables into coin
   flips (the bot fell to 2%). The fix was **7 Hands with targets that need ~3 good hands**, so a table is a series of
   decisions and luck evens out. Targets now roughly double every floor, like Balatro's antes.
4. **Bosses were uneven** (beaten 79-95%). Each boss's target is now scaled to its rule, so all 13 sit at 83-91%.

5. **Combos changed the curve again.** Plain wins now score far less (a win with 20 went from 60 to 25), so targets
   were scaled down to match. The bot plays plain blackjack and never chases a combo, so it's a floor, not an average.

Research targets: Slay the Spire 2's reported overall win rate is about 25%, and 16% at its base difficulty. The bot
now wins **about 10%** (10.6% on seeds 1-1000, 9.7% on 5001-6000) without ever building for combos, so a player who does
should land well above it. Floors 1-2 are forgiving (96-97% of tables) and every Pit Boss is a spike (83-91%).

| Floor | Low win | High win | Boss win | Runs reaching | Deals to clear a table |
| ----: | ------: | -------: | -------: | ------------: | ---------------------: |
| 1 | 96% | 97% | 91% | 100% | 2.8-3.0 |
| 2 | 97% | 96% | 87% | 84% | 2.4-3.2 |
| 3 | 96% | 92% | 85% | 68% | 2.6-3.2 |
| 4 | 94% | 94% | 83% | 51% | 2.7-3.0 |
| 5 | 94% | 92% | 83% | 37% | 2.7-3.2 |
| 6 | 95% | 94% | 83% | 27% | 2.7-3.5 |
| 7 | 92% | 92% | 83% | 20% | 2.7-3.5 |
| 8 | 94% | 89% | 84% (The House) | 14% | 2.7-4.5 |

**Every playstyle viable.** Focusing the bot on one archetype used to range from 2% (Cheater) to 22% (Risky).
A balance pass cut Cheater Heat costs, gave Lucky and Combo Vices more punch, toned All In down from x4 to x3,
added 8 suit and combo Vices, and gave the Combo set bonus +2 Mult per combo. Now (two seed ranges): Risky 16-17%,
Steady 14%, Greedy 10-12%, Lucky 7-9%, Cheater 7%, Combo 4-6%. Combo reads lowest because the bot never builds toward
combos, which is exactly what Combo Vices reward.

**A difficulty ladder.** Ten stakes, each keeping every rule before it, like Slay the Spire's Ascension. Each step
is a similar notch harder for the bot:

| Stake | Adds | Bot win rate |
| ----- | ---- | -----------: |
| White | base game | 10.9% |
| Red | Low Tables pay no gold | 7.1% |
| Green | targets 1.1x | 5.5% |
| Black | 2 Dead Cards in your deck | 4.6% |
| Purple | prices +1, Heat cools half as fast | 3.3% |
| Gold | the dealer hits soft 17 | 2.8% |
| Blue | 1 fewer Burn | 1.5% |
| Orange | targets 1.1x again | 1.1% |
| Silver | Pit Bosses and Elites 1.2x | 0.4% |
| Platinum | 1 fewer Hand, interest capped at 2 | 0.1% |

A test (`TunedBalance_KeepsTheBotNearItsTargetWinRate`) runs 300 seeded runs and fails if the bot's win rate leaves
7-25%, so CI catches balance regressions.

### Running the simulation

With any .NET 8 SDK (Unity ships one at `Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet`):

```sh
cd Tools/BalanceSim
dotnet run -c Release -- 1000 1              # runs, first seed
dotnet run -c Release -- 1000 1 archetypes   # plus one batch per archetype focus
```

## Running

Open the project in **Unity 6000.6.5f1** and press Play in any scene; the bootstrap builds the game.

## Tests

Without Unity (about a second, any .NET 8 SDK):

```sh
dotnet test Tools/CoreTests
```

In the editor: **Window > General > Test Runner > EditMode > Run All**. From the command line (editor closed):

```sh
/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## Building

```sh
Unity -batchmode -quit -projectPath . -executeMethod HouseEdge.Editor.BuildScript.WebGL   # Builds/WebGL
Unity -batchmode -quit -projectPath . -executeMethod HouseEdge.Editor.BuildScript.MacOS   # Builds/macOS
```

The WebGL build is Brotli-compressed with the decompression fallback on, so it runs on any static host (itch.io
included). For itch.io, zip the contents of `Builds/WebGL` with `index.html` at the top level and upload it as an
HTML game. A 960x600 embed matches the default canvas.

A built player also takes `-seed N -play Deal,Stand,... -capture shot.png`, which plays scripted steps, saves a
screenshot and quits (`Bot:N` lets the balance bot play N whole runs through the real UI as a smoke test).

## CI

`.github/workflows/ci.yml` runs `dotnet test Tools/CoreTests` on every push and pull request.

## Credits

Cards, suits, icons, UI and sounds are generated in code. Characters are public-domain paintings and room backdrops
are graded photos, all from Wikimedia Commons. Full list with authors and licenses: [docs/CREDITS.md](docs/CREDITS.md).

Music: "Lobby Time", "Bossa Antigua" and "Hitman" by Kevin MacLeod ([incompetech.com](https://incompetech.com)),
licensed under [Creative Commons: By Attribution 4.0](https://creativecommons.org/licenses/by/4.0/).

Fonts: [Limelight](https://fonts.google.com/specimen/Limelight)
by Sorkin Type and [Fira Sans](https://fonts.google.com/specimen/Fira+Sans) by Mozilla, both under the
SIL Open Font License (license files in `Assets/Resources/Fonts`).
