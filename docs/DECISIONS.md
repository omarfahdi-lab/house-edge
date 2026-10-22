# Design decisions

Rules that [DESIGN.md](../DESIGN.md) doesn't spell out, and the numbers the balance sim changed. Each one is
covered by a test in `Assets/Tests/EditMode`.

## Changed by the balance sim

| What | DESIGN.md | Now | Why |
| ---- | --------- | --- | --- |
| Starting Hands | 4 | **5** | With 4 Hands, about 10% of tables end with no winning hand at all. Over 24 tables that luck alone ended most runs. |
| Table targets | 150 at floor 1, doubling every floor (60,000 for The House) | **50 at floor 1, about 1.7x a floor** (High 1.5x Low, boss 2x, The House 2.5x, so 5,100) | The draft curve: the bot won 0% and only 32% got past floor 1. |
| The Twins, The Ceiling, The Card Mechanic | same target as other bosses | **0.75x, 0.8x, 0.8x** | They ended 2-3x more runs than the other Pit Bosses. |

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
