using System;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // how to play, one short page at a time
    public sealed class HowToView
    {
        private static readonly string[] Pages = { "The goal", "Blackjack", "Scoring", "Combos", "Count, raises & tricks", "Your deck", "Vices & the shop", "Heat", "The bosses", "Controls" };

        private readonly VisualElement _root, _tabs, _page;
        private int _current;

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public HowToView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0, 0, 0, 0.78f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            var panel = Ui.Column(_root, 12);
            panel.style.width = 940;
            panel.style.height = 600;
            panel.style.backgroundColor = Ui.PanelSolid;
            Ui.Rounded(panel, 18);
            Ui.Border(panel, 1, new Color(0.98f, 0.79f, 0.32f, 0.5f));
            Ui.Padding(panel, 20, 26);

            var head = Ui.Row(panel, 10);
            Ui.Title(head, "HOW TO PLAY", 30, Ui.Gold).style.flexGrow = 1;
            var close = Ui.Button(head, "Close", Close, ButtonKind.Normal, "Esc");
            close.style.height = 36;

            var body = Ui.Row(panel, 22);
            body.style.flexGrow = 1;
            body.style.alignItems = Align.Stretch;
            _tabs = Ui.Column(body, 6);
            _tabs.style.width = 190;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Ui.Add(body, scroll);
            _page = Ui.Column(null, 12);
            scroll.Add(_page);

            var nav = Ui.Row(panel, 10);
            nav.style.justifyContent = Justify.FlexEnd;
            Ui.Button(nav, "Back", () => Show(Math.Max(0, _current - 1)), ButtonKind.Ghost).style.height = 36;
            Ui.Button(nav, "Next", () => Show(Math.Min(Pages.Length - 1, _current + 1)), ButtonKind.Gold).style.height = 36;
            Ui.Show(_root, false);
        }

        public void Open(int page = -1)
        {
            Show(page >= 0 ? page : _current);
            Ui.Show(_root, true);
            Ui.Enter(_root, 20, 0, 0.25f);
        }

        public void Close() => Ui.Show(_root, false);

        private void Show(int index)
        {
            _current = index;
            _tabs.Clear();
            for (int i = 0; i < Pages.Length; i++)
            {
                int page = i;
                var tab = Ui.Button(_tabs, $"{i + 1}.  {Pages[i]}", () => Show(page), i == index ? ButtonKind.Gold : ButtonKind.Ghost);
                tab.style.height = 40;
                tab.style.justifyContent = Justify.FlexStart;
            }
            _page.Clear();
            switch (index)
            {
                case 0: Goal(); break;
                case 1: Blackjack(); break;
                case 2: Scoring(); break;
                case 3: ComboList(); break;
                case 4: Edge(); break;
                case 5: Deck(); break;
                case 6: Vices(); break;
                case 7: Heat(); break;
                case 8: BossList(); break;
                default: Controls(); break;
            }
            // no theme stylesheet: keep the page's rows from squashing each other
            foreach (var child in _page.Children()) child.style.flexShrink = 0;
            Ui.Enter(_page, 12, 0, 0.2f);
        }

        private void Heading(string text) => Ui.Title(_page, text, 24, Ui.Cream, glow: false);

        private void Para(string text) => Ui.Body(_page, text, 15, new Color(0.9f, 0.92f, 0.88f));

        private void Goal()
        {
            Heading("Beat the target, climb the casino");
            Para($"Every table has a target score. You get {Balance.StartingHands} Hands (deals) to reach it. Reach it and you're paid gold and go to the shop. Run out of Hands first and the run is over.");
            var floor = Ui.Row(_page, 6);
            floor.style.flexWrap = Wrap.Wrap;
            foreach (var (text, color) in new[] { ("LOW TABLE", Ui.Gold), ("2 stops", Ui.Dim), ("HIGH TABLE", Ui.Gold), ("2 stops", Ui.Dim), ("PIT BOSS", Ui.Bad) })
            {
                Ui.Pill(floor, text, color, filled: color != Ui.Dim, size: 12);
                if (text != "PIT BOSS") Ui.Text(floor, "→", 16, Ui.Dim, Weight.Bold);
            }
            Para("That's one floor. There are 8. Each Pit Boss changes the rules for its table, and on floor 8 you face The House. Beat it to win.");
            Para("Between tables you walk the floor's map, like Slay the Spire: Shops and rooms (deals, gambles and services) sit on branching paths. Pick your next stop from where your path leads. After a Pit Boss there's always a Shop.");
        }

        private void Blackjack()
        {
            Heading("Get closer to 21 than the dealer");
            Para("Number cards count their number. Jacks, Queens and Kings count 10. An Ace counts 11, or 1 if 11 would bust you. Go over 21 and you bust: the hand is lost.");
            Para("The dealer has one card face down. After you stand, it's revealed and the dealer draws until reaching 17 or more.");
            foreach (var (key, name, text) in new[]
            {
                ("H", "Hit", "Take another card."),
                ("S", "Stand", "Keep your total. The dealer plays."),
                ("D", "Double", "First two cards only: take exactly one more card. A win scores x2."),
                ("P", "Split", "With a pair: play it as two separate hands."),
                ("B", "Burn", "Right after a Hit: throw that card away and draw again. Limited per table."),
                ("R", "Surrender", "First two cards only: give up for 3 gold and keep the Hand."),
            })
            {
                var row = Ui.Row(_page, 10);
                Ui.Keycap(row, key).style.minWidth = 24;
                Ui.Text(row, name, 15, Ui.Gold, Weight.Bold).style.width = 90;
                Ui.Body(row, text, 14, Ui.Cream).style.flexShrink = 1;
            }
        }

        private void Scoring()
        {
            Heading("Score = Chips × Mult");
            var equation = Ui.Row(_page, 10);
            Box(equation, "35", "CHIPS", Ui.Chips);
            Ui.Title(equation, "×", 30, Ui.Cream, glow: false);
            Box(equation, "2", "MULT", Ui.Mult);
            Ui.Title(equation, "=", 30, Ui.Cream, glow: false);
            Ui.Title(equation, "70", 40, Ui.Gold);
            Para("Chips start as your card total plus a bonus for how you won, and Mult starts at the outcome's Mult. Combos add more. Above: you win with a King and Queen of hearts. 20 + 5 for the win + 10 for Suited = 35 Chips. 1 Mult for the win + 1 for Suited = 2 Mult. 35 × 2 = 70. A plain win with no combo would score only 25.");
            var table = Ui.Column(_page, 3);
            foreach (var (outcome, chips, mult) in new[]
            {
                ("Win", "+5", "1"), ("21 (3+ cards)", "+15", "2"), ("Blackjack", "+25", "3"), ("Five-card Charlie", "+30", "3"),
                ("Dealer busts", "+10 extra", "-"), ("Push (tie)", "cards only", "1"), ("Lose or bust", "0", "0"),
            })
            {
                var row = Ui.Row(table, 10);
                Ui.Text(row, outcome, 14, Ui.Cream, Weight.Bold).style.width = 170;
                Ui.Text(row, chips.Contains("only") ? chips : chips + " Chips", 14, Ui.Chips, Weight.Medium).style.width = 160;
                Ui.Text(row, mult == "-" ? "" : mult + " Mult", 14, Ui.Mult, Weight.Medium);
            }
            Para("Vices, Comps and card upgrades add more Chips and Mult. Big Mult is how scores explode.");
        }

        private static void Box(VisualElement parent, string value, string name, Color color)
        {
            var box = Ui.Column(parent);
            box.style.alignItems = Align.Center;
            box.style.minWidth = 80;
            Ui.Padding(box, 4, 12);
            Ui.Rounded(box, 10);
            box.style.backgroundColor = color;
            Ui.Text(box, value, 24, Color.white, Weight.Heavy);
            Ui.Text(box, name, 9, new Color(1, 1, 1, 0.8f), Weight.Heavy).style.letterSpacing = 2;
        }

        private void Edge()
        {
            Heading("Read the shoe, raise the bet");
            Para("THE COUNT, under the shoe, tells you how rich the cards still to come are in 10s and Aces, compared with your whole deck. HOT (+3 and up) means blackjacks and dealer busts are coming. COLD means small cards.");
            Para($"RAISE (X) before a deal to bet x2 or x3. A winning hand's Mult is multiplied by the bet. Lose and the raise costs extra Hands: 1 at x2, 2 at x3. Raise when the count is hot, but security notices: raising into a +{Balance.HotCount} count adds Heat.");
            Para($"TRICKS are one-use cards you play mid-hand from the panel on the left of the table (or keys 1 and 2). You hold {Balance.TrickSlots}, and start each run with one. Find more in shops, by winning Elite tables, at slot machines and from the Magician.");
            var tricks = Ui.Column(_page, 3);
            foreach (var kind in Tricks.All)
            {
                var row = Ui.Row(tricks, 10);
                Ui.Text(row, Tricks.Name(kind), 14, new Color(0.55f, 0.85f, 1f), Weight.Bold).style.width = 150;
                Ui.Body(row, Tricks.Describe(kind), 13, Ui.Cream).style.flexShrink = 1;
            }
            Para("FEATS are rare named hands, like The Black Jack (Ace of Spades and a black Jack) or a Six-Card Charlie. A winning Feat multiplies the whole hand's Mult and goes in your collection.");
        }

        private void Deck()
        {
            Heading("You play with your own deck");
            Para("Unlike a casino, the cards come from YOUR deck, and the dealer draws from it too. Pack it with 10s and you'll hit 20 more often, but so will the dealer.");
            Para("Shape it with Cocktails (one-use drinks) and the Card Room: remove cards, copy them, or upgrade them. Hover any card to see what it does.");
            var cards = Ui.Row(_page, 14);
            var gilded = new Card(Rank.King, Suit.Hearts) { Enhancement = Enhancement.Gilded };
            var hot = new Card(Rank.Seven, Suit.Spades) { Enhancement = Enhancement.Hot };
            var stamped = new Card(Rank.Ace, Suit.Diamonds) { Stamp = Stamp.Red };
            foreach (var card in new[] { gilded, hot, stamped })
            {
                var column = Ui.Column(cards, 6);
                column.style.alignItems = Align.Center;
                column.Add(CardView.Create(card, 0.85f));
                string note = card.Stamp != Stamp.None ? "Red Stamp" : card.Enhancement.ToString();
                Ui.Text(column, note, 12, Ui.Gold, Weight.Bold);
            }
            Para("Watch out for curses like the Dead Card: bad cards bosses and Vices can push into your deck.");
        }

        private void Vices()
        {
            Heading("Vices break the rules");
            Para("Vices are passive items you buy in the shop. You hold 5. They trigger top to bottom, so put +Mult before x Mult. Sell or reorder them in the sidebar between tables.");
            Para($"Vices LEVEL UP as they trigger: level 2 after {Balance.ViceLevel2At} triggers adds {Balance.ViceLevelBoost:P0} to what they give, level 3 after {Balance.ViceLevel3At} adds {2 * Balance.ViceLevelBoost:P0}. The stars on each Vice show its level, so the Vices you keep get stronger.");
            Para("Each Vice has a type. Hold 3 of one type for its set bonus:");
            foreach (Archetype archetype in Enum.GetValues(typeof(Archetype)))
            {
                var row = Ui.Row(_page, 10);
                var dot = new VisualElement();
                dot.style.width = dot.style.height = 10;
                Ui.Rounded(dot, 5);
                dot.style.backgroundColor = ViceCardView.ArchetypeColor(archetype);
                Ui.Add(row, dot);
                Ui.Text(row, archetype.ToString(), 14, ViceCardView.ArchetypeColor(archetype), Weight.Bold).style.width = 80;
                Ui.Body(row, ArchetypeBonuses.Describe(archetype), 14, Ui.Cream).style.flexShrink = 1;
            }
            Para("Comps (blue) level up a kind of win forever. Cocktails (purple) are drunk from the sidebar.");
        }

        private void Heat()
        {
            Heading("Don't get caught");
            Para("Cheater Vices and suspiciously huge hands add Heat. It cools by 10 after every table.");
            foreach (var (at, level, text) in new[]
            {
                ("25", "Watched", "The dealer reshuffles every 2 hands."),
                ("50", "Suspected", "Shop and room prices are up 25%."),
                ("75", "Flagged", "A random Cheater Vice is switched off each table."),
                ("100", "Busted", "Security drags you to The Eye in the Sky: a hard table you must beat."),
            })
            {
                var row = Ui.Row(_page, 10);
                Ui.Pill(row, at, Ui.HeatColor, filled: true, size: 12).style.width = 40;
                Ui.Text(row, level, 14, Ui.HeatColor, Weight.Bold).style.width = 90;
                Ui.Body(row, text, 14, Ui.Cream).style.flexShrink = 1;
            }
            Para("The Security Office room lets you pay Heat down. The Loan Shark lends gold, but if you don't repay in time, The Enforcer comes.");
        }

        // the patterns that make a winning hand big
        private void ComboList()
        {
            Heading("Combos: beating the dealer isn't enough");
            Para("A plain win scores little. The big numbers come from combos: poker patterns inside your blackjack hand. They only pay when the hand beats the dealer, and they stack, so a suited pair counts twice. Your hand shows its combos as you play, so you can decide whether to Hit and chase one.");
            foreach (var combo in Combos.All)
            {
                var (chips, mult) = Combos.Base(combo);
                var row = Ui.Row(_page, 10);
                Ui.Pill(row, Combos.Name(combo).ToUpperInvariant(), Ui.Cocktail, filled: true, size: 12).style.width = 130;
                Ui.Text(row, $"+{chips} Chips", 14, Ui.Chips, Weight.Bold).style.width = 90;
                Ui.Text(row, $"+{mult} Mult", 14, Ui.Mult, Weight.Bold).style.width = 70;
                Ui.Body(row, Combos.Describe(combo), 13, Ui.Cream).style.flexShrink = 1;
            }
            Para($"Win streak: every win in a row at a table adds +{Balance.StreakMultPerWin} Mult to the next winning hand (up to +{Balance.MaxStreakBonus}). A loss breaks it.");
            Para("Build for them: Comps level a combo up for the rest of the run, Vices like Flush Royale and Combo King multiply them, and Cocktails and the Card Room let you stack your deck with suits and ranks.");
        }

        // every boss, with their face and their one rule
        private void BossList()
        {
            Heading("Who runs this casino");
            Para("Every floor ends with a Pit Boss. Each one changes a single rule for their table. You can see the floor's boss on the map before you get there, so plan for them.");
            foreach (var boss in Bosses.PitBosses.Concat(new[] { Bosses.TheHouse, Bosses.EyeInTheSky, Bosses.Enforcer }))
            {
                var row = Ui.Row(_page, 12);
                row.style.alignItems = Align.FlexStart;
                Ui.Add(row, new BossPortrait(boss.Name, 56));
                var text = Ui.Column(row, 1);
                text.style.flexShrink = 1;
                var head = Ui.Row(text, 8);
                Ui.Text(head, boss.Name, 15, Ui.Gold, Weight.Heavy);
                Ui.Pill(head, boss.Ability.ToUpperInvariant(), Ui.Bad, size: 10);
                Ui.Body(text, boss.Twist, 13, Ui.Cream);
            }
        }

        private void Controls()
        {
            Heading("Keys");
            foreach (var (key, text) in new[]
            {
                ("Space", "Deal, leave the shop or a room, start a new run"),
                ("H / S", "Hit / Stand"),
                ("D / P", "Double down / Split"),
                ("B / R", "Burn / Surrender"),
                ("I / N", "Take / decline insurance"),
                ("W", "Swap with the dealer (Card Mechanic Vice)"),
                ("X", "Raise the bet before a deal"),
                ("1 / 2", "Play a trick card during your turn"),
                ("1 / 2 / 3", "Pick the next stop on the map"),
                ("Tab", "Look at the map"),
                ("M", "Mute"),
                ("Esc", "Menu and pause"),
            })
            {
                var row = Ui.Row(_page, 12);
                var cap = Ui.Keycap(row, key);
                cap.style.minWidth = 60;
                cap.style.height = 26;
                cap.style.fontSize = 13;
                Ui.Text(row, text, 15, Ui.Cream);
            }
            Para("Everything else is the mouse. Hover anything to see what it does.");
        }
    }
}
