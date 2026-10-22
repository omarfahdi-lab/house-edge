using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // how to play, one short page at a time
    public sealed class HowToView
    {
        private static readonly string[] Pages = { "The goal", "Blackjack", "Scoring", "Your deck", "Vices & the shop", "Heat", "Controls" };

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
                case 3: Deck(); break;
                case 4: Vices(); break;
                case 5: Heat(); break;
                default: Controls(); break;
            }
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
            Box(equation, "30", "CHIPS", Ui.Chips);
            Ui.Title(equation, "×", 30, Ui.Cream, glow: false);
            Box(equation, "2", "MULT", Ui.Mult);
            Ui.Title(equation, "=", 30, Ui.Cream, glow: false);
            Ui.Title(equation, "60", 40, Ui.Gold);
            Para("Chips start as your card total plus a bonus for how you won. Mult starts at the outcome's Mult. Above: you win with 20, so 20 + 10 = 30 Chips, × 2 Mult = 60 points.");
            var table = Ui.Column(_page, 3);
            foreach (var (outcome, chips, mult) in new[]
            {
                ("Win", "+10", "2"), ("21 (3+ cards)", "+20", "3"), ("Blackjack", "+30", "4"), ("Five-card Charlie", "+40", "4"),
                ("Dealer busts", "+15 extra", "-"), ("Push (tie)", "cards only", "1"), ("Lose or bust", "0", "0"),
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
