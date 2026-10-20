**HOUSE EDGE**

*A blackjack roguelike deckbuilder. Game design document, v0.2*

# **The Pitch**

You're a gambler climbing a cursed casino one floor at a time. Every
table is a game of blackjack, but you build the deck, collect Vices that
bend the rules, cheat when you can get away with it, and chase huge
combo scores. Beat The House on floor 8 to win the run.

## **What makes it different**

- **Shared deck:** the dealer draws from your deck too. Stack it with
  10s and you hit 20 more often, but so does the dealer. Every
  deckbuilding choice is a tradeoff.

- **Heat:** the strongest Vices are cheats, and cheating draws
  attention. Push your luck too far and casino security comes for you.

- **A living casino:** a floor map with a bar, a back room, a pawn shop
  and a loan shark who really wants his money back.

- **Real blackjack depth:** side bets, insurance, surrender, splitting
  and card counting all matter.

# **Core Loop**

- **Run:** 8 floors. Each floor is a short branching path that ends in a
  Pit Boss.

- **Table:** Reach the target score within a limited number of Hands.
  Win and you earn gold. Run out of Hands and the run is over.

- **Between tables:** pick a path on the floor map, visit the shop, and
  manage your Heat and debt.

# **Playing a Hand**

Standard blackjack. You and the dealer get 2 cards each, and one dealer
card is face down. The dealer stands on all 17s. The deck is shuffled at
the start of each table, not each hand, so counting cards actually
matters.

## **Actions**

| **Action**      | **When**            | **Effect**                                                         |
|-----------------|---------------------|--------------------------------------------------------------------|
| **Hit**         | Any time            | Take a card.                                                       |
| **Stand**       | Any time            | Lock in your total. The dealer plays.                              |
| **Double Down** | First two cards     | x2 Mult, but you take exactly one more card.                       |
| **Split**       | Starting pair       | Turn the pair into two hands, scored separately.                   |
| **Burn**        | After a hit         | Discard the card you just drew and draw again. Limited per table.  |
| **Surrender**   | First two cards     | Fold a bad start. Costs 3 gold, but the Hand isn't used up.        |
| **Insurance**   | Dealer shows an Ace | Pay 2 gold. If the dealer has blackjack, your hand becomes a push. |
| **Ante Up**     | Before the deal     | x2 Mult this hand. If you lose, you also lose 3 gold.              |

## **Resources (starting values)**

| **Resource**       | **Start** | **What it does**                                                 |
|--------------------|-----------|------------------------------------------------------------------|
| **Hands**          | 4         | Rounds you get per table to reach the target.                    |
| **Burns**          | 3         | Redraws. Resets each table.                                      |
| **Vice slots**     | 5         | Passive rule-breaking items. The heart of every build.           |
| **Cocktail slots** | 2         | One-use consumables.                                             |
| **Gold**           | 4         | Shop currency. Also used for side bets, insurance and surrender. |
| **Heat**           | 0         | How suspicious the casino is of you. 0 to 100.                   |

## **Scoring**

**Score = Chips x Mult.** Chips start as the total of your cards plus
the outcome's base Chips. Mult starts as the outcome's base Mult. Vices,
card enhancements and Comps then add to both, triggering left to right
so Vice order matters.

| **Outcome**     | **Base Chips** | **Base Mult** | **Notes**                                    |
|-----------------|----------------|---------------|----------------------------------------------|
| **Win**         | 10             | 2             | Your total beats the dealer's.               |
| **21**          | 20             | 3             | A win with exactly 21 using 3+ cards.        |
| **Blackjack**   | 30             | 4             | Ace + 10-value card as your first two.       |
| **Charlie**     | 40             | 4             | 5 cards without busting. Wins automatically. |
| **Dealer Bust** | +15            | +0            | Bonus added on top of your win.              |
| **Push**        | cards only     | 1             | A tie. Small score, no bonuses.              |
| **Lose / Bust** | 0              | 0             | Scores nothing and uses up the Hand.         |

**Example:** you win with 20. Chips = 20 + 10 = 30, Mult = 2, so the
hand scores 60. With Lucky Penny (+4 Mult) and High Roller (two 10s, +4
Mult), it scores 30 x 10 = 300.

# **Side Bets**

Before each hand you can place one side bet with gold. They pay out
whether you win the hand or not, which makes losing hands still feel
exciting.

| **Side Bet**     | **Cost** | **Pays**                                                                                               |
|------------------|----------|--------------------------------------------------------------------------------------------------------|
| **Perfect Pair** | 2g       | Your first two cards are a pair: +8 gold. Same suit: +20 gold.                                         |
| **21+3**         | 2g       | Your two cards plus the dealer's up card make a flush (+6g), straight (+8g) or three of a kind (+15g). |
| **Bust It**      | 2g       | The dealer busts: +5 gold. Busts with 6+ cards: +25 gold.                                              |
| **Lucky Ladies** | 3g       | Your first two total 20: +10 gold. Two Queens of Hearts: +100 gold.                                    |
| **Hot Hand**     | 1g       | You win the hand: +2 gold and +5 Mult.                                                                 |

# **Heat**

Cheater Vices are some of the strongest in the game, but every time they
trigger, the casino gets more suspicious. Huge hands also draw eyes:
scoring over 3x the table target in a single hand adds +10 Heat. Heat
drops by 10 after each table.

| **Heat**  | **Status** | **Effect**                                                                                                                                            |
|-----------|------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| **0-24**  | Clean      | No effect.                                                                                                                                            |
| **25-49** | Watched    | The dealer reshuffles every 2 hands, which wrecks card counting.                                                                                      |
| **50-74** | Suspected  | Shop prices go up 25%.                                                                                                                                |
| **75-99** | Flagged    | A random Cheater Vice is disabled at each table.                                                                                                      |
| **100**   | Busted     | Security drags you to The Eye in the Sky, a surprise boss table. Beat it and your Heat resets and you get a free Rare Vice. Lose and the run is over. |

This gives every run a push-your-luck layer outside the cards: how much
are you willing to cheat?

# **The Casino Floor**

Each floor is a small map. After the Low Table you pick one of two
rooms, then the High Table, then another room, then the Pit Boss. The
shop opens after every table.

| **Room**              | **What happens**                                                                                                     |
|-----------------------|----------------------------------------------------------------------------------------------------------------------|
| **The Bar**           | Cocktails are half price, or take one random Cocktail for free.                                                      |
| **Back Room**         | One hand of high-stakes blackjack. Win and your gold doubles. Lose and it's gone.                                    |
| **Pawn Shop**         | Sell Vices for full price, or buy a used Rare (it might be Cursed).                                                  |
| **Loan Shark**        | Borrow 10, 20 or 30 gold. Debt grows 50% each table. Unpaid by the end of the next floor, and The Enforcer shows up. |
| **Card Room**         | Remove, duplicate or enhance one card in your deck.                                                                  |
| **Lost and Found**    | Take a free random Vice. 1 in 3 chance it's Cursed.                                                                  |
| **Security Office**   | Pay 2 gold per 10 Heat to clear it, or bribe a guard for a Cheater Vice (+20 Heat).                                  |
| **High Limit Lounge** | Skip a table entirely, but the next table's target is 1.5x.                                                          |

# **Dealers**

Every Low and High Table has a random dealer with their own style and a
tell. Tells are subtle hints about the hole card. The Tell Reader Vice
spells them out.

| **Dealer**       | **Style**                                        | **Tell**                                         |
|------------------|--------------------------------------------------|--------------------------------------------------|
| **Rookie Ray**   | Nervous. 1 in 5 chance he hits on 17 by mistake. | Fidgets when his hole card is a 10.              |
| **Silent Sofia** | Hits on soft 17.                                 | No tell. Ever.                                   |
| **Chatty Chen**  | Talks nonstop.                                   | Lets slip whether the hole card is red or black. |
| **Old Pete**     | Slow and friendly. You get +1 Burn at his table. | Sighs when his hole card is low.                 |
| **Vegas Val**    | Generous. You win all pushes.                    | Smiles when she's close to busting.              |
| **Lucky Lou**    | Reshuffles after every dealer bust.              | Taps the table when he has an Ace.               |

# **Floors and Targets**

Targets are a starting point to tune with the balance bot.

| **Floor** | **Low Table** | **High Table** | **Pit Boss**       |
|-----------|---------------|----------------|--------------------|
| **1**     | 150           | 225            | 300                |
| **2**     | 300           | 450            | 600                |
| **3**     | 600           | 900            | 1,200              |
| **4**     | 1,200         | 1,800          | 2,400              |
| **5**     | 2,500         | 3,750          | 5,000              |
| **6**     | 5,000         | 7,500          | 10,000             |
| **7**     | 10,000        | 15,000         | 20,000             |
| **8**     | 20,000        | 30,000         | 60,000 (The House) |

## **Economy**

- Beating a table pays 3 / 4 / 5 gold (Low / High / Boss).

- +1 gold for each unused Hand.

- Interest: +1 gold per 5 held, up to +5.

# **Pit Bosses**

Each boss table adds one twist. Some bosses also shuffle Curse cards
into your deck.

| **Boss**               | **Twist**                                                                                                  |
|------------------------|------------------------------------------------------------------------------------------------------------|
| **The Twins**          | The dealer plays two hands. You have to beat both to win.                                                  |
| **The Iceman**         | The dealer stands on 15.                                                                                   |
| **The Cheat**          | The dealer wins every push.                                                                                |
| **The Mime**           | Your second card stays face down until you stand.                                                          |
| **The Taxman**         | Every hit costs 1 gold.                                                                                    |
| **The Purist**         | Aces only count as 1.                                                                                      |
| **The Ceiling**        | Your bust limit drops to 19.                                                                               |
| **The Sweeper**        | Burns are disabled.                                                                                        |
| **The Silencer**       | Your leftmost Vice is disabled.                                                                            |
| **The Card Mechanic**  | After you stand, he swaps your highest card with his face-up card.                                         |
| **The Hoarder**        | Shuffles 3 Dead Cards into your deck for the rest of the run.                                              |
| **The Pit Boss**       | Every hand gives +10 Heat.                                                                                 |
| **The Eye in the Sky** | Appears at 100 Heat. All Cheater Vices are disabled and the target is 2x.                                  |
| **The Enforcer**       | Appears if you skip a loan. Takes 3 gold for every hand you don't win. If you hit 0 gold, he takes a Vice. |
| **The House (final)**  | Three phases. The twist changes every 2 hands, pulling from all the bosses above.                          |

# **Vices**

Passive items that bend the rules. You hold up to 5, and they trigger
left to right, so order matters. Shop prices: Common 4g, Uncommon 6g,
Rare 8g. Legendaries only come from rare packs and boss rewards.

## **Archetypes**

Every Vice belongs to an archetype. Holding 3 Vices of the same
archetype unlocks a set bonus, which pushes players toward building
around a playstyle.

| **Archetype** | **Playstyle**             | **Set bonus (3 Vices)**                            |
|---------------|---------------------------|----------------------------------------------------|
| **Lucky**     | Chance and big swings     | All chance effects get +1 (1 in 4 becomes 2 in 4). |
| **Risky**     | Hitting on high totals    | Your bust limit goes up by 1.                      |
| **Cheater**   | Information and rigging   | All Heat you gain is halved.                       |
| **Greedy**    | Gold and side bets        | Interest cap goes up by 3.                         |
| **Steady**    | Safe play and consistency | +1 Hand at every table.                            |
| **Combo**     | Scaling and synergies     | Scaling Vices grow twice as fast.                  |

## **All Vices**

| **Vice**              | **Rarity** | **Type** | **Effect**                                                                            |
|-----------------------|------------|----------|---------------------------------------------------------------------------------------|
| **Lucky Penny**       | Common     | Lucky    | +4 Mult on any win.                                                                   |
| **Hot Streak**        | Common     | Combo    | +2 Mult per consecutive win this table. Resets on a loss.                             |
| **Low Roller**        | Common     | Steady   | +8 Chips for each 2 through 5 in your hand.                                           |
| **High Roller**       | Common     | Risky    | +2 Mult for each 10-value card in your hand.                                          |
| **Card Sharp**        | Common     | Risky    | +25 Chips if your hand has 4 or more cards.                                           |
| **Photo Finish**      | Common     | Risky    | +10 Mult if you beat the dealer by exactly 1.                                         |
| **Sore Loser**        | Common     | Greedy   | Gain 3 gold whenever you lose a hand.                                                 |
| **Dealer's Friend**   | Common     | Steady   | +30 Chips when the dealer busts.                                                      |
| **Pocket Pair**       | Common     | Lucky    | +6 Mult if your first two cards are a pair.                                           |
| **Suit Up**           | Common     | Lucky    | +5 Mult if every card in your hand shares a suit.                                     |
| **Penny Pincher**     | Common     | Greedy   | +1 gold for each unused Burn at the end of a table.                                   |
| **Espresso**          | Common     | Steady   | +1 Hand at every table.                                                               |
| **Steady Hand**       | Common     | Steady   | +20 Chips if you stand on 17 or lower and still win.                                  |
| **Even Money**        | Common     | Lucky    | +8 Mult if your final total is even.                                                  |
| **Poker Face**        | Common     | Cheater  | All Heat you gain is reduced by 30%.                                                  |
| **Side Hustle**       | Common     | Greedy   | Side bets cost 1 less gold.                                                           |
| **Coin Flip**         | Common     | Lucky    | 1 in 2 chance for +10 Mult.                                                           |
| **Iron Will**         | Common     | Steady   | +3 Mult for each hand this table where you stood on 16 or less.                       |
| **Card Counter**      | Uncommon   | Cheater  | Always see the next card in the deck. +3 Heat per hand.                               |
| **X-Ray Specs**       | Uncommon   | Cheater  | The dealer's hole card is dealt face up. +5 Heat per hand.                            |
| **Soft Touch**        | Uncommon   | Combo    | Soft hands (an Ace counted as 11) get x2 Mult.                                        |
| **Split Personality** | Uncommon   | Combo    | You can split any two starting cards, not just pairs.                                 |
| **Bouncer**           | Uncommon   | Steady   | Your first bust at each table counts as a push instead.                               |
| **Stretch Limo**      | Uncommon   | Risky    | Your bust limit is 22. The dealer's stays 21.                                         |
| **Insurance Agent**   | Uncommon   | Greedy   | Gain 4 gold whenever the dealer shows an Ace.                                         |
| **Bankroll**          | Uncommon   | Greedy   | +1 Mult for every 5 gold you hold.                                                    |
| **Face Value**        | Uncommon   | Combo    | J, Q and K each give +10 Chips and +1 Mult.                                           |
| **Fresh Felt**        | Uncommon   | Steady   | The first hand at each table gets x3 Mult.                                            |
| **Burnout**           | Uncommon   | Combo    | Each Burn you use gives +3 Mult for the rest of the table.                            |
| **Pickpocket**        | Uncommon   | Cheater  | When the dealer busts, their last card joins your deck as a Gilded copy. +5 Heat.     |
| **Doubling Cube**     | Uncommon   | Risky    | Gains +1 Mult permanently every time you win a double down.                           |
| **Sleight of Hand**   | Uncommon   | Cheater  | Your first Burn each hand is free. +3 Heat when used.                                 |
| **Tell Reader**       | Uncommon   | Cheater  | See the dealer's tell every hand.                                                     |
| **Bookie**            | Uncommon   | Greedy   | Winning a side bet also gives +5 Mult to that hand.                                   |
| **Daredevil**         | Uncommon   | Risky    | +4 Mult for every card you hit on 15 or higher.                                       |
| **Horseshoe**         | Uncommon   | Lucky    | 1 in 4 chance a bust turns into a 21.                                                 |
| **Snowball**          | Uncommon   | Combo    | +5 Chips permanently every win. Resets when you bust.                                 |
| **Patience**          | Uncommon   | Steady   | Gains +1 Mult permanently each hand you win without hitting.                          |
| **Rigged Shoe**       | Rare       | Cheater  | The dealer must hit until they reach 18. +5 Heat per hand.                            |
| **Lucky Sevens**      | Rare       | Lucky    | Each 7 in your hand gives x1.5 Mult. Three 7s gives x7.                               |
| **Mirror**            | Rare       | Combo    | Copies the Vice to its right.                                                         |
| **Black Cat**         | Rare       | Risky    | When you bust, your next hand gets x3 Mult.                                           |
| **All In**            | Rare       | Risky    | x4 Mult on every hand, but you earn no interest.                                      |
| **Last Call**         | Rare       | Steady   | Your final hand at each table gets x3 Mult.                                           |
| **Twins**             | Rare       | Combo    | Split hands are scored twice.                                                         |
| **Cold Read**         | Rare       | Cheater  | After you stand, see the dealer's next card and choose whether they take it. +8 Heat. |
| **Card Mechanic**     | Rare       | Cheater  | Once per hand, swap your last card with the dealer's face-up card. +10 Heat.          |
| **Smooth Talker**     | Rare       | Cheater  | +1 Mult for every 5 Heat you have.                                                    |
| **Knife's Edge**      | Rare       | Risky    | x3 Mult at exactly 21, but x0.5 Mult at 18 or lower.                                  |
| **Double or Bust**    | Rare       | Risky    | You can Double Down after hitting.                                                    |
| **Rabbit's Foot**     | Rare       | Lucky    | Every chance-based effect rolls twice and keeps the better result.                    |
| **Loan Officer**      | Rare       | Greedy   | Loan Shark debt doesn't grow.                                                         |
| **Cashier**           | Rare       | Steady   | Unused Hands pay 3 gold each instead of 1.                                            |
| **Echo**              | Rare       | Combo    | Your first card in each hand is scored twice.                                         |
| **Midas**             | Legendary  | Greedy   | Every card in a winning hand becomes Gilded.                                          |
| **Natural Born**      | Legendary  | Lucky    | Blackjacks get x5 Mult and can't be tied by a dealer blackjack.                       |
| **Lady Luck**         | Legendary  | Lucky    | All odds are doubled (Horseshoe, Coin Flip, Fragile breaks, shop rarity).             |
| **High Stakes**       | Legendary  | Risky    | Gains x0.25 Mult each time you hit on 17 or higher and don't bust.                    |
| **The Dealer**        | Legendary  | Cheater  | Once per table, you decide whether the dealer hits or stands. +15 Heat.               |
| **Whale**             | Legendary  | Greedy   | x1 Mult for every 10 gold you hold.                                                   |
| **Chain Reaction**    | Legendary  | Combo    | Every time another Vice triggers, x1.1 Mult.                                          |

## **Cursed Vices**

Found in the Pawn Shop and Lost and Found. Stronger than normal, with a
nasty downside. You can sell them, but only for 1 gold.

| **Cursed Vice**   | **Effect**                                                         |
|-------------------|--------------------------------------------------------------------|
| **Dirty Money**   | +12 Mult on every win, but +5 Heat per win.                        |
| **Devil's Deal**  | x3 Mult on every hand, but you start each table with 1 fewer Hand. |
| **Bad Habit**     | +50 Chips, but you must hit at least once every hand.              |
| **Borrowed Time** | +2 Hands per table, but each one costs 2 gold.                     |

# **Cards**

## **Enhancements (one per card)**

| **Enhancement** | **Effect**                                                                       |
|-----------------|----------------------------------------------------------------------------------|
| **Gilded**      | +3 gold when it's part of a winning hand.                                        |
| **Flex**        | Counts as any value from 1 to 11, picked automatically to get you closest to 21. |
| **Fragile**     | x2 Mult. Breaks after it's been in 3 winning hands.                              |
| **Marked**      | You see it when it's the next card in the deck. +2 Heat when drawn.              |
| **Ghost**       | Worth 0 but gives +15 Chips. A free hit.                                         |
| **Hot**         | +1 Mult every time it's drawn this table, stacking.                              |
| **Heavy**       | Counts as 1 higher and gives +20 Chips.                                          |

## **Stamps (one per card, stack with enhancements)**

| **Stamp**       | **Effect**                                                                      |
|-----------------|---------------------------------------------------------------------------------|
| **Red Stamp**   | The card is scored twice.                                                       |
| **Blue Stamp**  | Drawing it gives you +1 Burn.                                                   |
| **Gold Stamp**  | +2 gold when drawn.                                                             |
| **Black Stamp** | The dealer can never draw it. If they would, it goes to the bottom of the deck. |

## **Curse Cards**

Bad cards that bosses and Cursed effects shuffle into your deck. Since
the dealer draws from your deck too, they can also hurt the dealer.
Remove them at the Card Room.

| **Curse**      | **Effect**                                  |
|----------------|---------------------------------------------|
| **Dead Card**  | Counts as 10 and gives -2 Mult.             |
| **Snake Eyes** | Counts as 1 and ends your turn immediately. |
| **Tax Card**   | Lose 2 gold when drawn.                     |
| **Jinx**       | Disables a random Vice for this hand.       |

# **Cocktails**

One-use consumables, bought in the shop, at the Bar, or found in packs.

| **Cocktail**        | **Effect**                             |
|---------------------|----------------------------------------|
| **Old Fashioned**   | Make up to 2 cards Gilded.             |
| **Martini**         | Duplicate a card in your deck.         |
| **Screwdriver**     | Remove up to 2 cards from your deck.   |
| **Long Island**     | Raise a card's value by 1.             |
| **Last Word**       | Lower a card's value by 1.             |
| **Mojito**          | Make a card Flex.                      |
| **Champagne**       | Make a card Fragile.                   |
| **Dark and Stormy** | Add a Red Stamp to a card.             |
| **Ice Water**       | +2 Burns at this table.                |
| **Whiskey Sour**    | Gain 2 gold for each Vice you hold.    |
| **Cold Brew**       | Lower your Heat by 25.                 |
| **Wild Card**       | Create a random Vice if you have room. |

# **Comps**

Comps level up an outcome permanently for the run, like upgrading a
poker hand. Each level adds Chips and Mult to that outcome.

| **Comp**        | **Per level**                                   |
|-----------------|-------------------------------------------------|
| **Win**         | +10 Chips, +1 Mult                              |
| **21**          | +15 Chips, +1 Mult                              |
| **Blackjack**   | +20 Chips, +2 Mult                              |
| **Charlie**     | +25 Chips, +2 Mult                              |
| **Double Down** | +10 Chips, +1 Mult on doubled wins              |
| **Split Win**   | +15 Chips, +1 Mult on each split hand that wins |
| **Dealer Bust** | +15 Chips, +1 Mult                              |

# **Starting Decks**

| **Deck**          | **Change**                                                          | **Unlock**                       |
|-------------------|---------------------------------------------------------------------|----------------------------------|
| **Standard**      | A normal 52-card deck.                                              | Start                            |
| **Short Deck**    | No 2s through 5s. More busts for both sides, bigger Chip totals.    | Win a run                        |
| **Ace High**      | 4 extra Aces.                                                       | Score a Blackjack with x20+ Mult |
| **Lowball**       | No face cards. Safer hands, smaller Chips.                          | Reach floor 5                    |
| **Marked Deck**   | Every card starts Marked. You start with 30 Heat.                   | Get Busted by security           |
| **Dealer's Deck** | The dealer's hole card is always visible, but you get 1 fewer Hand. | Beat The House                   |

# **Stakes (difficulty)**

After a win, you unlock the next chip color. Each stake includes every
rule before it.

| **Stake**       | **Added rule**                                   |
|-----------------|--------------------------------------------------|
| **White Chip**  | Base game.                                       |
| **Red Chip**    | Low Tables pay no gold.                          |
| **Green Chip**  | Targets are 1.25x.                               |
| **Black Chip**  | You start the run with a Dead Card in your deck. |
| **Purple Chip** | Heat drops half as fast between tables.          |
| **Gold Chip**   | The dealer hits on soft 17.                      |

# **Modes**

- **Daily Run:** everyone plays the same seed each day, with a
  leaderboard for the highest score.

- **Run Codes:** every run can be shared as a seed code, and the game
  can replay a full run from its recorded inputs.

- **Endless:** after beating The House, keep going with scaling targets
  for a high score.

# **Engineering Plan**

- **Same proven setup:** all game rules in plain C# with no Unity
  dependency, seeded and deterministic, with NUnit tests and GitHub
  Actions CI.

- **Event-driven Vices:** Vices, enhancements, curses and bosses all
  hook into the same scoring events (OnCardDrawn, OnHit, OnHandScored,
  OnDealerBust, OnTableEnd). Each one is a small class, and adding a new
  one never touches the core loop.

- **Replays:** a run is just a seed plus a list of player inputs. That
  powers run codes, the Daily Run, and makes bugs easy to reproduce.

- **Balance bot:** a headless bot plays thousands of seeded runs to tune
  floor targets, find broken Vice combos, and check that each archetype
  has a fair win rate. This is the main Riot interview story.

- **Polish:** satisfying card deals and flips, a score counter that
  ramps up, screen shake on huge hands, chip sounds, and a security
  alarm when your Heat maxes out.

# **MVP Scope**

Build in this order and ship as soon as step 3 is fun:

- **1.** Blackjack hand vs dealer with the shared deck, all actions, and
  scoring.

- **2.** Tables, targets, Hands, Burns, and the 8-floor run.

- **3.** Shop, gold economy, and the first 20 Vices.

- **4.** Pit Bosses, Heat, side bets, enhancements, stamps, Cocktails
  and Comps.

- **5.** Floor map with rooms, dealers, the Loan Shark and curses.

- **6.** Remaining Vices, archetype bonuses, decks, stakes, Daily Run,
  balance bot, and polish.
