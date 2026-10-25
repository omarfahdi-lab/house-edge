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
- **The map comes out before every table**, not just each new floor. Leaving the last stop on a path (often an Elite)
  used to drop you straight into the next table, so the Pit Boss felt like it came out of nowhere. Now the map shows
  where you're headed and the table (and a boss's entrance) waits until you press Continue. Elite, Eye in the Sky and
  Enforcer tables skip it. UI only: Core and replays are unchanged.
- **Special cards look special.** An enhanced card's whole face is tinted in its enhancement's color (Wild is violet),
  with a thick border and a label strip; a stamp is a large wax seal in the top right. When a pack opens, upgraded or
  stamped cards glow in their color and their names are colored, so you see what's special before reading.
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

## Prototype: Keep 2 of 3 (off by default)

Not in DESIGN.md yet. It's a menu switch so it can be playtested before anyone decides whether it belongs in the design.

- **The rule.** Each deal gives you three cards and the dealer one face-up card. You give one of your three to the
  dealer as his hole card and play the other two. Cards come out of the shoe in the usual order, so the third card is
  the one that would have been his hole card.
- **The hole card is face up,** since you chose it. No insurance is offered for the same reason. Give a 10 to an Ace
  and he has blackjack.
- **The dealer can't take a Black Stamp,** so a Black Stamp can't be given away, unless all three cards have one.
- **Effects see the cards in deal order** once you choose: your two cards, then his hole card. Nothing fires while the
  three cards are still on offer.
- **2 fewer Hands per table** (`Balance.KeepTwoOfThreeHandsLess`). Without the cut, the bot's win rate goes from 10% to
  45%. With it, over 1,000 White Chip runs:

  | How the bot gives cards away | Runs won |
  | --- | ---: |
  | Normal game, rule off | 10.0% |
  | Best choice | 11.4% |
  | Random | 1.3% |
  | Worst choice | 0.4% |

  A good player ends up with about today's difficulty, but the choice now decides much more of the outcome.
- **The tutorial table** keeps the normal deal and its normal Hands. Its stacked shoe teaches the two-card game.
- **The run code** marks the rule with `K`.

## Prototype: House cards (off by default)

Not in DESIGN.md yet. It's a menu switch so it can be playtested before anyone decides whether it belongs in the design.

- **The casino shuffles its own cards into the shoe** at every table: 1 on floor 1, 1 more on each floor after that,
  and 2 extra at boss tables (`Balance.HouseCards`). Each one is a normal card with a random rank and suit, plus a
  House effect. They're made fresh for each table and never enter your deck. The tutorial table has none.
- **Each one is good in one hand and bad in the other:**

  | Card | In your hand | In the dealer's hand |
  | --- | --- | --- |
  | Bounty | Win: +5 gold and x2 Mult. Lose: 1 extra Hand lost | +3 gold for each hand that beats him |
  | Mirror | Copies the dealer's face-up card | Copies your first card |
  | Fuse | Only 1 more card after it (no Double once it's spent) | He stops drawing, even below 17 |
  | Loaded Die | Your showdown total +1, never past 21 | His total +1, so a 21 becomes a bust |
  | Snitch | +8 Heat | His hole card turns face up |
  | Leech | +3 Mult on a win | Steals 1 of your Burns |

- **A Mirror copies at draw time** and again once the opening cards are all out, since your first card is dealt
  before the dealer's face-up card. Keep 2 of 3's preview already counts a Mirror as what it will copy.
- **The Loaded Die only counts at the showdown.** It never changes when the dealer stops drawing.
- **The table code has a few hooks for them:** Hit and Double legality (Fuse), the dealer's draw (Fuse) and the
  showdown comparison (Loaded Die). Everything else is in one `HouseRules` effect.
- **Difficulty:** they cut both ways, so they add decisions, not difficulty. Over 1,000 bot runs each, the win rate
  was 10.0% without them and 10.2% with them. With Keep 2 of 3 too, it was 13.4%, because Keep 2 of 3 lets you
  steer a House card to whichever side it helps you on. Raising floor 1–3 targets is the lever that would make early
  floors harder. At x2, floors 1–2 tables drop from about 97% won to about 85%, and whole runs drop to 3–6%. That
  hasn't been applied.

## Harder first floors

- **Floors 1-3 targets went up x1.5, x1.3 and x1.15** (Low/High/Boss: 130/190/255, 200/300/405, 290/425/575). The bot
  used to clear floor 1-2 tables 96-97% of the time, in about 2 deals, so early play never mattered. Now the floor 1
  Low/High/Boss tables are won 92/90/85% of the time, and about 1 run in 4 ends on floor 1. Floors 4-8 are unchanged.
- **The bot now wins 6.8% and 5.7%** on two seed ranges, down from about 10%. It never builds combos or buys packs,
  so a real player lands above it. The balance guard test now allows 7-25% wins over its 300 seeds, and requires that
  more than 65% of runs reach floor 2 (it was 80%).
- **Tried and rejected:** x1.8/x1.5/x1.2 ended 40% of the bot's runs on floor 1. A run that dies before the first
  shop matters too often isn't fun.
- **With the prototypes:** Keep 2 of 3 wins 8.0%, House cards 6.2%, and both together 11.0%.

## Prototype: the Rival (off by default)

Not in DESIGN.md yet. It's a menu switch so it can be playtested.

- **One rival per run,** picked from the seed (no random draw), takes the dealer's seat at the High Table on floors 2, 4,
  6 and 8. The gallery gives the rival a painting like any other character, so the rival's text avoids pronouns.
- **The duel:** both hands are face up, and the rival draws until beating your best standing hand, past 17 or
  standing on 13 to beat your 12. A Fuse still stops the draw, and a Loaded Die counts.
- **Each win against the rival brings back a new trick:** meeting 2 also hits on ties, meeting 3 also palms the first
  card that would bust (like The Cheat), and meeting 4 costs you 1 Hand. Beating a rival table pays +8 gold.
- **The target is x1.7.** Chasing past 17 busts the rival often enough that at x1 the tables were as easy as a
  dealer's. At x1.7 the four meetings are won about 84/93/85/81% of the time by the bot, close to the Pit Bosses.
  The bot's run win rate with the Rival on is 7.1%, against 6.8% without, because the gold makes up for the harder
  tables.
- **All three prototypes together** win 9.5% of runs.
- **The menu shows the three prototypes on one line,** each explained on hover. The panel overflowed with a full
  description for each.
- **The rival has a voice.** Every meeting opens with an entrance card in the bosses' frame. From the second meeting
  on, it remembers the last one ("Floor 2. 412 against my 340."). The rival speaks after every hand, not 40% of the
  time like dealers, and gets angry, not sad, when you win. Beating a rival table gets a parting line that names the
  next floor; the last one is "Go on, then. Tell The House I said hello." The banner and badge say RIVAL, not DEALER.
- **How to play has a "Prototype rules" page** covering all three. Long How to play pages now scroll inside the panel
  and say "Scroll for more"; before, the boss list and the new page ran past the panel's bottom edge.
