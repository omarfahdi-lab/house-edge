# Design decisions

Rules that [DESIGN.md](../DESIGN.md) doesn't spell out, and the numbers the balance sim changed. Each one is
covered by a test in `Assets/Tests/EditMode`.

## Changed by the balance sim

| What | DESIGN.md | Now | Why |
| ---- | --------- | --- | --- |
| Starting Hands | 4 | **7** | Blackjack scoring is lumpy (about half of all hands score 0). With few Hands, any target that needs more than one win is a coin flip. 7 Hands lets a table ask for ~3 good hands. |
| Table targets | 150 at floor 1, doubling every floor | **90 at floor 1, roughly doubling** (High 1.5x Low, boss ~2x, The House 26,000) | The draft curve: the bot won 0%. A gentler curve then made tables end on the first winning hand (2 deals, 2-9x the target). Now tables take ~3 deals and the bot wins ~14%. |
| Boss targets | same for every boss | **0.6x (The Ceiling) to 1.4x (The Mime, The Pit Boss)** | Scaled to each rule so every boss is beaten 83-91% of the time. |
| Plain wins | Win 10 Chips x2, 21 20x3, Blackjack 30x4, Charlie 40x4, Dealer Bust +15 | **Win 5x1, 21 15x2, Blackjack 25x3, Charlie 30x3, Dealer Bust +10** | Players said winning alone carried the game. Combos and streaks now make the big numbers. |
| Combos | not in DESIGN.md | **Pair, Two Pair, Three of a Kind, Suited, Flush, Run, Royal Court, Low Ball; winning hands only; they stack; the bigger pattern replaces the smaller (Three of a Kind over Pair, Flush over Suited)** | Gives Hit/Stand a second question (chase the pattern?) and gives deck building a direction, like Balatro's poker hands. Comps level them up. |
| Win streak | not in DESIGN.md | **+1 Mult per consecutive winning deal at a table, up to +6. A loss breaks it, a push doesn't** | Rewards beating the dealer again and again. |
| Stakes | 6 (White to Gold, one rule each) | **10 (adds Blue, Orange, Silver, Platinum); Green 1.1x instead of 1.25x, Black 2 Dead Cards, Purple also +1 price** | The original jumps were uneven (Green halved the win rate, Black to Gold barely moved it). Now each stake is a similar step, from 10.9% to 0.1% for the bot. |
| Archetype balance | | **Cheater Heat costs cut ~35%; Lucky Penny 5, Hot Streak 3, Coin Flip 12, Pocket Pair 8, Suit Up 6, Face Value +2 Mult, Burnout 4, Snowball 7, Smooth Talker per 3 Heat; All In x3 (was x4); the Combo set also gives +2 Mult per combo** | Focus win rates went from 2-22% to 4-17%. |
| Card packs | not in DESIGN.md | **one pack in every shop (Card 3g, Suit 4g, Court 4g, Lucky 5g): open it and keep 1 of 3 cards, or none. Suit packs always hold a Wild card (counts as any suit for combos)** | Makes deck building the way you set up combos. The bot skips packs: it never builds toward a combo, so for it they only cost Vices and interest. |
| Elite tables | not in DESIGN.md | **an optional map stop: a random Pit Boss's rule (never the floor's boss), the next table's target, 2 fewer Hands. Win: paid like a High Table, pick a Rare or Legendary Vice. Lose: the run goes on, but you lose half your gold** | Slay the Spire's elites: a risk you choose for a better reward. |
| "?" events | not in DESIGN.md | **eight events with one or two choices each (Lucky Chip, Hustler, Spilled Drinks, Back Alley, Shredder, Photo Booth, Security Sweep, Comp Ticket)** | Variety and small decisions between tables. |
| Vice unlocks | not in DESIGN.md | **Matchmaker, Straight Shooter, Court Jester, Flush Royale and Combo King start locked and unlock by winning with Three of a Kind, a Run, a Royal Court, a Flush, and 50 combos in total. The Daily Run has everything unlocked** | Something to work toward between runs. |
| The Cheat | the dealer wins pushes | **once a hand, the dealer palms the card that would bust him and draws again** | A push rule is invisible; a palmed card is a moment you see and remember. |

See the README's Balance section for the results.

## The hand

- **Chips from cards** = the hand's blackjack total (a 20 gives 20 Chips), as in DESIGN.md's example.
- **Outcome priority:** Charlie > Blackjack > 21 > Win. Dealer Bust adds +15 Chips on top of any win.
- **Charlie** ends your turn as soon as you have 5 cards without busting. The dealer doesn't draw against it.
- **Dealer blackjack:** the dealer peeks when showing an Ace or a 10-value card. A dealer blackjack ends the hand at
  once: a push if you took insurance or also have blackjack, otherwise a loss.
- **Your blackjack** ends your turn. Natural Born makes it beat a dealer blackjack.
- **Split:** matching ranks only (Split Personality: any two), one split per deal, both hands play out fully, a 21 on
  a split hand is 21 (not Blackjack), double down is allowed after splitting, and the deal uses one Hand.
- **Burn:** only right after a Hit, can be repeated while you have Burns, and works on a bust (the hand waits for you
  to Burn or Stand). Not after a Double Down. The burned card goes to the discards.
- **Surrender** doesn't use a Hand. A side bet already placed is lost.
- **Insurance** is only offered if you can pay for it.
- **A hand dealt over your bust limit** (The Ceiling) ends at once.
- **Running out of cards:** the discards are shuffled back in with the run's RNG.
- **Numbers:** Chips are whole numbers, Mult is a decimal, the hand's score rounds down.

## Scoring order

1. Base Chips and Mult for the outcome (+ Dealer Bust bonus, + Hot Hand's +5 Mult).
2. Every card, left to right: its enhancement and curse. A Red Stamp scores the card again (its value as Chips plus
   its enhancement).
3. Comps that apply to the outcome.
4. Bosses, dealers and stake rules, then Vices left to right.
5. Chain Reaction (after everything else, so it can count what triggered).
6. Double Down x2 (wins only) and Ante Up x2 (any hand that scores), so they double the whole result.

Hooks added beyond DESIGN.md's list: `OnTableStart`, `OnBeforeDeal`, `OnDeal`, `OnBurn`, `OnBust` (an effect can still
rescue the hand), `OnDealEnd` and `OnAfterScoring`.

## Side bets and Ante Up

- One side bet per deal, paid when placed. Bust It makes the dealer play even if every hand is already settled.
- Ante Up costs 3 gold only if no hand at least pushed.

## Cards

- **Curse values:** Dead Card 10, Snake Eyes 1, Tax Card and Jinx 2. Curses can't be enhanced or duplicated, and only split with Split Personality.
- **Snake Eyes** ends the turn of the hand that drew it. **Jinx** turns off a random active Vice until the next deal.
- **Blue and Gold Stamps** only pay when *you* draw them. A **Black Stamp** drawn for the dealer goes to the bottom.
- **Marked** cards add 2 Heat whoever draws them. **Hot** cards add +1 Mult per draw at this table.
- **Fragile** gives x2 Mult on any hand that scores and breaks (leaves the deck) after its third winning hand.
- **Flex** fills whatever gap is left after Aces.
- **Heavy** Aces count 2 or 12.

## Cocktails and Comps

- Cocktails that change deck cards work between tables (shop, rooms, rewards). Ice Water only at a table. Whiskey
  Sour, Cold Brew and Wild Card work anytime.
- Long Island and Last Word move a card's rank (K up to A, A down to K). They can't raise an Ace or lower a 2.
- Comps only apply to winning hands.

## Shop and economy

- 3 Vices (70% Common, 25% Uncommon, 5% Rare), 2 different Cocktails (3g), 1 Comp (4g).
- Reroll costs 2g, +1 per reroll in the same shop. Vices sell for half price, at least 1. Cursed Vices sell for 1.
- At Suspected Heat or higher, prices (including rerolls and room purchases) are 25% higher, rounded up.
- **Legendaries** come from boss rewards: beat a Pit Boss and pick 1 of 2 Vices, each Rare or (1 in 4) Legendary.
  "Rare packs" aren't in the game; boss rewards are the only source.

## Heat

- Levels stack: Suspected and Flagged also reshuffle every 2 hands, like Watched.
- Heat cools by 10 after each table (5 on Purple stake).
- At 100, The Eye in the Sky starts right after the table that pushed you there, before the shop. Its target is 2x
  the current table's. Beating it clears your Heat and gives a free Rare Vice (if you have room).
- Poker Face and the Cheater set multiply Heat gains. The result is rounded.

## Bosses

- **The Twins:** the second dealer hand is dealt when the dealer plays. You need to beat both. The Dealer Bust bonus
  needs both to bust.
- **The Mime** only hides the card on screen. The rules see it.
- **The Taxman:** you need 1 gold to hit.
- **The Hoarder's** Dead Cards stay in your deck for the run.
- **The House** has 2 extra Hands and picks a new Pit Boss twist every 2 hands (not the Hoarder or the Silencer).
- **The Enforcer** appears when you leave the shop after the boss of the floor your loan is due, if you still owe.
  His target is that floor's boss target. Beating him clears the debt. He takes 3 gold for every hand you don't win,
  or a random Vice if you're broke.

## Floor map and rooms

- **The floor map** (changed from DESIGN.md's "pick 1 of 2 rooms", at the player's request): between the Low and High
  Tables, and between the High Table and the Pit Boss, there are two rows of three stops. Each stop is a Shop
  (30% each, and every first row has at least one) or one of the rooms, no repeats in a row. You can start in any lane;
  each stop leads straight on and sometimes diagonally to a neighbouring lane, never with two paths crossing.
- Shops are stops on the map now, not automatic after every table. The only automatic shop is the one after a Pit Boss.
  The table's payout shows on the map. The balance sim barely moved (21.1% to 21.5%), so no numbers changed.
- The High Limit Lounge only appears in the row right before a table, and never right before The House.
- **The Bar:** buy any of 3 Cocktails at 2g each, *or* take one random Cocktail free.
- **Back Room:** one hand with no Vices. Any win doubles your gold, a loss or bust takes all of it, a push changes nothing.
- **Pawn Shop:** sells Vices at full shop price (Cursed for 1). The used Rare costs 6g and is a random Cursed Vice 1 time in 3.
- **Loan Shark:** one loan per visit, due at the end of the next floor. Debt grows 50% after every table (rounded up),
  unless you hold Loan Officer. You can repay in shops and rooms.
- **Card Room:** one service per visit. Enhance gives a random enhancement (never Marked). Your deck can't go below 20 cards.
- **Security Office:** 2 gold per 10 Heat (a partial 10 costs a full 2g). The bribe gives a random Cheater Vice and +20 Heat.
- **High Limit Lounge:** the next table is skipped (no payout, no shop). The table after it has a 1.5x target. Skipping a
  boss also skips its reward, but not the Enforcer.

## Dealers

Tells, as the Tell Reader spells them out: Ray fidgets over a 10-value hole card, Chen talks about roses (red) or the
night sky (black), Pete sighs over a 6 or lower (not an Ace), Val smiles when she has 12 to 16, Lou taps for an Ace.
Sofia never has one.

## Vices

- Scaling Vices (Hot Streak, Iron Will, Burnout) reset each table. Doubling Cube, Snowball, Patience and High Stakes last
  the whole run. The Combo set bonus doubles their growth.
- **Bouncer:** the saved push scores 21 x1.
- **Stretch Limo** adds 1 to whatever your bust limit is (so 20 under The Ceiling).
- **Rigged Shoe:** the dealer stands on 18 or more.
- **Black Cat:** a bust arms x3 Mult for every hand of the next deal.
- **Last Call:** the hand you play with 1 Hand left.
- **Twins (Vice):** split hands get x2 Mult.
- **Whale:** x(gold / 10), only once you hold 20 or more.
- **Chain Reaction:** x1.1 for each Vice that changed the hand's Chips or Mult.
- **Mirror** copies every hook of the Vice to its right (not another Mirror).
- **Lady Luck** doubles chances and rolls shop rarity twice, keeping the better.
- **Card Mechanic (Vice):** once per deal, +10 Heat. **Cold Read:** the first time the dealer would draw each deal,
  +8 Heat. **The Dealer:** once per table, +15 Heat.
- **Bad Habit:** you can't Stand until you've hit (unless you can't hit).
- **Borrowed Time:** your last 2 Hands each cost 2 gold when dealt.

## Meta

- **Stakes** unlock one at a time: winning at your highest stake unlocks the next.
- **Decks:** Short Deck unlocks on any win and Dealer's Deck on beating The House, as listed (the same thing outside Endless).
- **Daily Run:** the seed comes from the device's local date. The leaderboard is a local top 5 per day; there's no server.
- **Run codes:** base-34 seed, deck, stake and an `E` for Endless, e.g. `3F7K2-21E`. A finished run can be replayed
  from its recorded inputs.
- **Endless:** after The House, targets double every floor and Pit Bosses are picked at random.

## Presentation and accessibility

- **Recorded music over synthesized loops.** The code-made loops worked but sounded thin next to the rest of the polish.
  Three Kevin MacLeod tracks (CC BY 4.0) replace them: "Lobby Time" between tables, "Bossa Antigua" at tables and
  "Hitman" for bosses. Credit is in the README and on the menu. Tension only raises the pitch 3% now (it was 6%),
  because recorded music sounds sped up past that. The synth loops stay as a fallback and the heartbeat is still synthesized.
- **Court cards are drawn, not lettered.** Kings, Queens and Jacks are Painter2D royals mirrored head to toe, so a
  Royal Court combo is easy to spot.
- **Card backs follow the starting deck**, so the shoe tells you which deck you're playing.
- **Four-color deck and Reduced motion** are menu options saved in PlayerPrefs. They only change presentation, never
  Core, so replays and run codes are unaffected.
- **Dealer quips are UI only.** They use UnityEngine.Random in the Game layer, so they never touch the run's RNG.
  Blackjacks and big hands always get a line; other hands get one 40% of the time, so it stays fun and not noisy.
- **Run history** (`Run.History`) records each finished table's score and target for the end-of-run graph. A test
  checks that it adds up to `TotalScore`.

## Tutorial

- **Stacked, not simulated.** The first table of a tutorial run draws a scripted top of the shoe (`Tutorial.Script`),
  pulled out of the player's own deck, with the rest shuffled underneath. The run is still a pure function of its seed
  and options (`RunOptions.Tutorial`, a `T` in the run code), so replays work.
- **Order: plain win, Burn, Flush.** The Flush hand scores 330 against an 85 target, so it has to come last or it would
  end the table before the Burn lesson. Ending on it also makes the point: one combo outscores everything before it.
- **The coach only lets the pointed-at button through** on the scripted table. That's enforced in the Game layer
  (`Coach.Allows`), not Core, so Core has no notion of a tutorial beyond the stacked shoe. It can be skipped at any time.
- **It starts on its own** only for a brand-new profile (no runs, tutorial never finished). Everyone else can start it from the menu.

## Depth: the count, raises, tricks, levels, Feats, boss phases, slots, Daily twists

- **The count is relative to your own deck.** A Hi-Lo count assumes a standard deck, but here the deck is yours
  (Short Deck has no small cards at all). `Table.Count` compares the share of 10s and Aces left in the shoe with the
  share in your whole deck, scaled to -5..+5. The hole card isn't in the shoe, so the count never leaks it.
- **Raising costs Hands, not gold.** Gold is already the shop economy; Hands are the table's real resource, so a
  lost x2 raise costs 1 extra Hand and x3 costs 2. You can only raise as far as your Hands cover. Raising into a
  count of +3 or more adds Heat, which ties counting to the casino-security theme.
- **Tricks are separate from Cocktails.** Cocktails change your deck between tables; Tricks act inside a hand.
  Everyone starts with one, picked from the seed's value so the RNG stream (and every existing seed's run) is unchanged.
  Take Back keeps a bust open even with no Burns left, otherwise it could never be played on the bust it's for.
- **Vice levels are generic.** Rather than 84 hand-written upgrades, `Table.Credit` counts a trigger whenever a Vice
  adds Chips or Mult and adds 25% (level 2, 12 triggers) or 50% (level 3, 30 triggers) of what it added. Penalties
  (Knife's Edge's x0.5) are never boosted.
- **Feats multiply at the very end**, after Double Down, Ante Up and the raise, so a Feat is always a visible jump.
- **Boss second phases** fire once, when a deal ends with the score past half the target. Each one tightens the boss's
  own theme. The Ceiling's first draft (bust over 18) dropped it to 77% beaten in the sim, so it makes ties go to the
  house instead (79%). The House already changes every 2 hands and has no second phase.
- **Balance after all of it:** the bot (which raises into hot shoes and plays the obvious tricks) wins 9.6% and 9.3%
  of White Chip runs on two seed ranges, against about 10% before. The player gets stronger, the bosses fight back.
- **Daily twists** come from the Daily seed, so everyone gets the same one, and they ride in the run code as `M<n>`.
