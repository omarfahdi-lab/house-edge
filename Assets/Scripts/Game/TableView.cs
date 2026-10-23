using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the blackjack table in the main area. reads a Table and turns button presses into PlayerActions
    public sealed class TableView
    {
        private static readonly (PlayerAction action, string label, string key, string tip)[] ActionButtons =
        {
            (PlayerAction.Deal, "Deal", "Space", "Start the next hand. Each deal uses 1 Hand."),
            (PlayerAction.Hit, "Hit", "H", "Take another card. Go over 21 and you bust."),
            (PlayerAction.Stand, "Stand", "S", "Keep your total. Then the dealer plays."),
            (PlayerAction.DoubleDown, "Double", "D", "Only on your first two cards. Take exactly one more card, and a win scores x2."),
            (PlayerAction.Split, "Split", "P", "Only with a pair. Split it into two hands that are played and scored separately. Still uses just 1 Hand."),
            (PlayerAction.Burn, "Burn", "B", "Only right after a Hit. Throw that card away and draw a new one, even if it busted you. Uses 1 Burn."),
            (PlayerAction.Surrender, "Surrender", "R", "Only on your first two cards. Give up this hand for 3 gold, and keep the Hand to play again."),
            (PlayerAction.SwapWithDealer, "Swap", "W", "Card Mechanic: swap your last card with the dealer's face-up card. +10 Heat."),
            (PlayerAction.TakeInsurance, "Insure  ·  2g", "I", "If the dealer has blackjack, your hand becomes a push instead of a loss."),
            (PlayerAction.DeclineInsurance, "No insurance", "N", "Play on without insurance."),
            (PlayerAction.LetDealerDraw, "Let him draw", "H", "The dealer takes the next card."),
            (PlayerAction.StopDealer, "Make him stand", "S", "Cold Read: the dealer stands on what he has. +8 Heat."),
        };

        private readonly VisualElement _root;
        private readonly VisualElement _plate, _plateFace, _bubble;
        private readonly Label _plateTag, _plateName, _plateText, _bubbleText;
        private string _plateFor;
        private readonly Label _tell;
        private readonly VisualElement _dealerCards, _secondDealer, _secondDealerCards, _playerHands;
        private readonly VisualElement _dealerBadgeSlot, _secondBadgeSlot;
        private readonly VisualElement _nextCard, _nextCardSlot;
        private readonly VisualElement _shoe;
        private readonly Label _shoeCount;
        private readonly Label _message, _breakdown, _hint;
        private readonly VisualElement _print;
        private readonly TableMarkings _markings;
        private readonly VisualElement _bets;
        private readonly Label _betPlaced;
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();
        private readonly Dictionary<PlayerAction, Button> _betButtons = new Dictionary<PlayerAction, Button>();
        private readonly Label _replay;
        // cards already on screen this deal, so only new ones fly in
        private readonly HashSet<(Card, int, bool)> _shown = new HashSet<(Card, int, bool)>();
        private Table _shownTable;
        private int _dealIndex;
        // ms into this refresh's dealing, so each new card waits for the one before it
        private float _cursor;
        // the result on screen hasn't been acted out yet: the scoring playback will call ActOut
        private bool _resultPending;
        private Table _resultTable;
        private string _lastMessage;
        // what's on screen, so the result can animate the cards
        private readonly List<(PlayerHand hand, VisualElement group, List<VisualElement> cards)> _handViews =
            new List<(PlayerHand, VisualElement, List<VisualElement>)>();
        private readonly List<VisualElement> _dealerViews = new List<VisualElement>();

        // world-space centers, for chips sliding between you and the dealer
        public Vector2 DealerCenter => _dealerCards.worldBound.center;
        public Vector2 PlayerCenter => _playerHands.worldBound.center;

        public event Action<PlayerAction> ActionChosen;
        // a trick card's slot was clicked
        public event Action<int> TrickChosen;

        private readonly CountMeter _count;
        private readonly VisualElement _tricks, _trickCards;
        private readonly Button _raise;
        // a card just landed on the felt, for the sound
        public event Action CardLanded;

        // 1, 2 or 4: how fast cards fly and results play out
        public static float Speed = 1;

        // when the cards dealt by the last refresh will have landed, in seconds from now
        public float SettleSeconds => (_cursor + 380) / 1000f / Speed;

        public VisualElement Root => _root;

        public TableView(VisualElement parent)
        {
            _root = Ui.Column(parent);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 12, 24);
            _root.style.alignItems = Align.Center;
            _root.pickingMode = PickingMode.Ignore;

            // printed on the felt
            _markings = new TableMarkings();
            Ui.Fill(_markings);
            _root.Add(_markings);
            // a lamp over the table and cigar smoke drifting through it
            _root.Add(new Ambience());
            _replay = Ui.Pill(_root, "REPLAY", Ui.Bad, filled: true, size: 13);
            _replay.style.position = Position.Absolute;
            _replay.style.left = 20;
            _replay.style.top = 14;

            // the shoe the cards are dealt from
            _shoe = Ui.Column(_root, 4);
            _shoe.style.position = Position.Absolute;
            _shoe.style.right = 26;
            _shoe.style.top = 18;
            _shoe.style.alignItems = Align.Center;
            var stack = new VisualElement();
            stack.style.width = CardView.Width * 0.6f + 8;
            stack.style.height = CardView.Height * 0.6f + 8;
            _shoe.Add(stack);
            FillShoe();
            _shoeCount = Ui.Caption(_shoe, "");
            Ui.Tip(stack, "The shoe", "Your deck, shuffled at the start of each table. You AND the dealer draw from it, so the cards you add change what the dealer gets too.");
            _count = new CountMeter();
            _shoe.Add(_count);
            Ui.Tip(_count, "The count",
                "How rich the cards left in the shoe are in 10s and Aces, compared with your whole deck.\n" +
                "HOT (right): more blackjacks and dealer busts coming, a good time to Raise.\n" +
                $"COLD (left): small cards coming. Raising into a count of +{Balance.HotCount} or more draws Heat.");

            // trick cards, played in the middle of a hand
            _tricks = Ui.Column(_root, 6);
            _tricks.style.position = Position.Absolute;
            _tricks.style.left = 22;
            _tricks.style.bottom = 150;
            _tricks.style.width = 150;
            var tricksHead = Ui.Caption(_tricks, "TRICKS", new Color(0.55f, 0.85f, 1f));
            Ui.Tip(tricksHead, "Trick cards", $"One-use plays for the middle of a hand: peek, steal, take a card back. You hold up to {Balance.TrickSlots}. Find them in shops, Elite wins, slot machines and the Magician.");
            _trickCards = Ui.Column(_tricks, 6);

            // who you're playing: the dealer or the Pit Boss
            // a boss's portrait sits on the left of the plate
            _plate = Ui.Glass(_root, 12, row: true, radius: 12);
            _plate.style.maxWidth = 620;
            Ui.Padding(_plate, 7, 16);
            _plateFace = new VisualElement();
            _plate.Add(_plateFace);
            var plateText = Ui.Column(_plate, 2);
            plateText.style.alignItems = Align.Center;
            plateText.style.flexShrink = 1;
            var plateTop = Ui.Row(plateText, 8);
            _plateTag = Ui.Pill(plateTop, "", Ui.Gold, size: 10);
            _plateName = Ui.Title(plateTop, "", 20, Ui.Gold);
            _plateText = Ui.Body(plateText, "", 13, Ui.Cream, center: true);

            // what the boss says when their ability goes off
            _bubble = Ui.Row(_root, 8);
            _bubble.style.position = Position.Absolute;
            _bubble.style.top = 70;
            _bubble.style.left = Length.Percent(58);
            _bubble.style.backgroundColor = new Color(0.98f, 0.95f, 0.87f);
            Ui.Rounded(_bubble, 14);
            Ui.Padding(_bubble, 8, 14);
            Ui.Border(_bubble, 2, Ui.Gold);
            _bubble.pickingMode = PickingMode.Ignore;
            _bubbleText = Ui.Text(_bubble, "", 15, Ui.Ink, Weight.Bold);
            _bubbleText.style.whiteSpace = WhiteSpace.Normal;
            _bubbleText.style.maxWidth = 340;
            Ui.Show(_bubble, false);

            // dealer, the Twins' second hand and the next-card peek
            var dealerRow = Ui.Row(_root, 30);
            dealerRow.style.alignItems = Align.FlexStart;
            dealerRow.style.marginTop = 10;
            var dealer = Ui.Column(dealerRow, 8);
            dealer.style.alignItems = Align.Center;
            _dealerCards = Ui.Row(dealer);
            _dealerCards.style.height = CardView.Height;
            _dealerBadgeSlot = Ui.Row(dealer);
            _tell = Ui.Text(dealer, "", 13, new Color(0.85f, 0.76f, 1f), Weight.Medium);
            _tell.style.unityFontStyleAndWeight = FontStyle.Italic;
            _tell.style.whiteSpace = WhiteSpace.Normal;
            _tell.style.maxWidth = 360;
            _tell.style.unityTextAlign = TextAnchor.MiddleCenter;

            _secondDealer = Ui.Column(dealerRow, 8);
            _secondDealer.style.alignItems = Align.Center;
            _secondDealerCards = Ui.Row(_secondDealer);
            _secondDealerCards.style.height = CardView.Height;
            _secondBadgeSlot = Ui.Row(_secondDealer);

            _nextCard = Ui.Column(dealerRow, 6);
            _nextCard.style.alignItems = Align.Center;
            Ui.Caption(_nextCard, "NEXT CARD", Ui.Gold);
            _nextCardSlot = Ui.Row(_nextCard);
            Ui.Tip(_nextCard, "Next card", "A Vice lets you see the top card of the shoe.");

            // the result of the last hand
            var middle = Ui.Column(_root, 4);
            middle.style.alignItems = Align.Center;
            middle.style.flexGrow = 1;
            middle.style.justifyContent = Justify.Center;
            middle.pickingMode = PickingMode.Ignore;
            // printed on the felt while nothing else is going on
            _print = Ui.Column(middle, 3);
            _print.style.alignItems = Align.Center;
            _print.pickingMode = PickingMode.Ignore;
            Ui.Title(_print, "SCORE  =  CHIPS  ×  MULT", 20, new Color(0.98f, 0.79f, 0.32f, 0.32f), glow: false).pickingMode = PickingMode.Ignore;
            var printRule = Ui.Text(_print, "DEALER STANDS ON ALL 17s", 11, new Color(0.98f, 0.79f, 0.32f, 0.28f), Weight.Bold);
            printRule.style.letterSpacing = 3;
            printRule.pickingMode = PickingMode.Ignore;
            _print.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (_print.resolvedStyle.display != DisplayStyle.None) _markings.SetBand(_root.WorldToLocal(_print.worldBound).center.y);
            });
            _message = Ui.Title(middle, "", 32, Ui.Gold);
            _breakdown = Ui.Text(middle, "", 16, Ui.Cream, Weight.Medium);
            _breakdown.style.unityTextAlign = TextAnchor.MiddleCenter;
            _breakdown.style.whiteSpace = WhiteSpace.Normal;
            _breakdown.style.maxWidth = 640;

            // you
            _playerHands = Ui.Row(_root, 40);
            _playerHands.style.minHeight = CardView.Height + 44;
            _playerHands.style.alignItems = Align.FlexStart;

            _hint = Ui.Text(_root, "", 14, Ui.Cream);
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.maxWidth = 720;
            _hint.style.marginTop = 4;

            // side bets
            _bets = Ui.Row(_root, 6);
            _bets.style.marginTop = 8;
            _bets.style.flexWrap = Wrap.Wrap;
            _bets.style.justifyContent = Justify.Center;
            var betsLabel = Ui.Caption(_bets, "SIDE BET", Ui.Gold);
            betsLabel.style.marginRight = 4;
            Ui.Tip(betsLabel, "Side bets (optional)", "Before a deal you can buy one side bet. It pays gold if its condition happens, whether you win the hand or not.");
            _betPlaced = Ui.Pill(_bets, "", Ui.Gold, filled: true, size: 11);
            foreach (var bet in SideBets.All)
            {
                var action = SideBets.ActionFor(bet);
                var button = SmallButton(_bets, "", () => ActionChosen?.Invoke(action));
                Ui.Tip(button, SideBets.Name(bet), SideBets.Description(bet));
                _betButtons[action] = button;
            }
            var ante = SmallButton(_bets, "Ante Up  x2", () => ActionChosen?.Invoke(PlayerAction.AnteUp));
            Ui.Tip(ante, "Ante Up", "This hand scores x2. If you don't at least push, you also lose 3 gold.");
            _betButtons[PlayerAction.AnteUp] = ante;
            _raise = Ui.Button(_bets, "Raise", () => ActionChosen?.Invoke(PlayerAction.Raise), ButtonKind.Gold, "X");
            _raise.style.height = 30;
            Ui.Padding(_raise, 0, 10);
            _raise.Q<Label>().style.fontSize = 13;
            Ui.Tip(_raise, "Raise your bet",
                "x2 or x3: a winning hand's Mult is multiplied by the bet. Lose the hand and the raise costs that many extra Hands (1 at x2, 2 at x3).\n" +
                "Watch the count by the shoe: raise when it's hot.");

            // actions
            var actions = Ui.Row(_root, 8);
            actions.style.marginTop = 10;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.justifyContent = Justify.Center;
            foreach (var (action, label, key, tip) in ActionButtons)
            {
                var chosen = action;
                var kind = action == PlayerAction.Deal ? ButtonKind.Gold
                    : action == PlayerAction.Hit || action == PlayerAction.Stand ? ButtonKind.Primary
                    : ButtonKind.Normal;
                var button = Ui.Button(null, label, () => ActionChosen?.Invoke(chosen), kind, key);
                button.style.height = 48;
                // disabled buttons get no hover, so the explanation sits on a wrapper
                var wrap = new VisualElement();
                Ui.Add(actions, wrap);
                wrap.Add(button);
                Ui.Tip(wrap, label.Split(' ')[0], tip);
                _buttons[action] = button;
            }
        }

        private static Button SmallButton(VisualElement parent, string text, Action onClick)
        {
            var button = Ui.Button(parent, text, onClick, ButtonKind.Ghost);
            button.style.height = 30;
            button.style.minWidth = 60;
            Ui.Padding(button, 0, 10);
            button.Q<Label>().style.fontSize = 13;
            return button;
        }

        public void SetReplay(bool replaying) => Ui.Show(_replay, replaying);

        private VisualElement _spotRing;
        private PlayerAction? _spotted;

        // a pulsing gold ring around the one button the tutorial wants pressed
        public void Spotlight(PlayerAction? action)
        {
            if (action == _spotted) return;
            _spotted = action;
            _spotRing?.RemoveFromHierarchy();
            _spotRing = null;
            if (action == null || !_buttons.TryGetValue(action.Value, out var button)) return;
            // buttons clip their children, so the ring sits beside the button and follows its layout
            _spotRing = new VisualElement { pickingMode = PickingMode.Ignore };
            _spotRing.style.position = Position.Absolute;
            Ui.Border(_spotRing, 3, Ui.Gold);
            Ui.Rounded(_spotRing, 14);
            button.parent.Add(_spotRing);
            var ring = _spotRing;
            float t = 0;
            ring.schedule.Execute(() =>
            {
                var at = button.layout;
                ring.style.left = at.x - 6;
                ring.style.top = at.y - 6;
                ring.style.width = at.width + 12;
                ring.style.height = at.height + 12;
                t += 0.05f;
                float pulse = (Mathf.Sin(t * 5) + 1) / 2;
                ring.style.opacity = 0.45f + 0.55f * pulse;
                if (!Settings.ReducedMotion) ring.style.scale = new Scale(Vector2.one * (1 + 0.04f * pulse));
            }).Every(50);
        }

        // three card backs, redone when the starting deck (and so the back) changes
        private void FillShoe()
        {
            var stack = _shoe.ElementAt(0);
            stack.Clear();
            for (int i = 0; i < 3; i++)
            {
                var back = CardView.CreateFaceDown(0.6f);
                back.style.position = Position.Absolute;
                back.style.left = 8 - i * 4;
                back.style.top = 8 - i * 4;
                back.pickingMode = PickingMode.Ignore;
                stack.Add(back);
            }
        }

        // a fresh table: the shoe's cards fan out and snap back together, with a shuffle sound
        private void Riffle()
        {
            var stack = _shoe.ElementAt(0);
            int i = 0;
            foreach (var card in stack.Children())
            {
                int n = i++;
                var c = card;
                Ui.Transition(c, 0.18f, EasingMode.EaseOutBack, "translate", "rotate");
                for (int pass = 0; pass < 3; pass++)
                {
                    long at = 150 + pass * 260;
                    c.schedule.Execute(() =>
                    {
                        c.style.translate = new Translate((n - 1) * 26, -8);
                        c.style.rotate = new Rotate((n - 1) * 10);
                        CardLanded?.Invoke();
                    }).StartingIn(at);
                    c.schedule.Execute(() =>
                    {
                        c.style.translate = new Translate(0, 0);
                        c.style.rotate = new Rotate(0);
                    }).StartingIn(at + 130);
                }
            }
        }

        public void Refresh(Run run)
        {
            var table = run.ActiveTable;
            if (run.Options.Deck != CardView.BackStyle)
            {
                CardView.BackStyle = run.Options.Deck;
                FillShoe();
            }
            if (table != _shownTable && table.DealsPlayed == 0) Riffle();
            if (table != _shownTable || table.DealsPlayed != _dealIndex)
            {
                _shown.Clear();
                _shownTable = table;
                _dealIndex = table.DealsPlayed;
            }
            _cursor = 0;
            _shoeCount.text = $"{table.CardsInShoe} LEFT";
            _count.Set(table.Count);
            RefreshTricks(run, table);
            RefreshPlate(run);
            if (run.Boss == null || table != run.Table) Ui.Show(_bubble, false);
            // your cards first, then the dealer plays
            RefreshPlayer(table);
            RefreshDealer(table);
            RefreshTell(run, table);
            RefreshResult(table);
            RefreshHint(table);
            RefreshBets(table);

            var legal = table.LegalActions();
            foreach (var pair in _buttons)
            {
                bool show = pair.Key switch
                {
                    PlayerAction.TakeInsurance or PlayerAction.DeclineInsurance => table.Phase == TablePhase.InsuranceOffer,
                    PlayerAction.LetDealerDraw or PlayerAction.StopDealer => table.Phase == TablePhase.DealerDecision,
                    PlayerAction.SwapWithDealer => legal.Contains(PlayerAction.SwapWithDealer),
                    _ => table.Phase != TablePhase.InsuranceOffer && table.Phase != TablePhase.DealerDecision,
                };
                Ui.Show(pair.Value.parent, show);
                Ui.SetEnabled(pair.Value, legal.Contains(pair.Key));
                if (pair.Key == PlayerAction.Hit)
                    Ui.SetText(pair.Value, table.Rules.HitCost > 0 ? $"Hit  ·  {table.Rules.HitCost}g" : "Hit");
                if (pair.Key == PlayerAction.Burn && table.Rules.BurnsDisabled) Ui.SetText(pair.Value, "No Burns");
                else if (pair.Key == PlayerAction.Burn) Ui.SetText(pair.Value, "Burn");
            }
        }

        private void RefreshPlate(Run run)
        {
            bool backRoom = run.ActiveTable != run.Table;
            bool boss = !backRoom && run.Boss != null;
            Ui.Show(_plate, backRoom || boss || run.Dealer != null);
            var red = new Color(0.5f, 0.05f, 0.08f, 0.85f);
            _plate.style.backgroundColor = boss || backRoom ? red : Ui.Panel;
            Ui.BorderColor(_plate, boss || backRoom ? new Color(1f, 0.45f, 0.4f, 0.6f) : Ui.Hairline);
            if (backRoom)
            {
                _plateTag.text = "BACK ROOM";
                _plateName.text = "DOUBLE OR NOTHING";
                _plateText.text = Ui.Rich($"One hand, no Vices. Win and your {run.Gambler.Gold}g doubles. Lose and it's gone.");
                return;
            }
            string face = boss ? run.Boss.Name : null;
            if (face != _plateFor)
            {
                _plateFor = face;
                _plateFace.Clear();
                if (face != null) _plateFace.Add(new BossPortrait(face, 58));
                Ui.Show(_plateFace, face != null);
            }
            if (boss)
            {
                var definition = run.Boss.Definition;
                _plateTag.text = (run.AtEyeInTheSky ? "SECURITY" : run.AtElite ? "ELITE" : run.AtEnforcer ? "COLLECTIONS" : run.Floor == Balance.Floors ? "FINAL BOSS" : "PIT BOSS")
                                 + "  ·  " + definition.Ability.ToUpperInvariant();
                _plateName.text = run.Boss.Name.ToUpperInvariant();
                string twist = definition.Twist;
                if (run.Boss is TheHouse house && house.CurrentTwist != null)
                    twist = $"Now playing as {house.CurrentTwist.Name} ({house.CurrentTwist.Definition.Ability}): {house.CurrentTwist.Definition.Twist}";
                // past half the target the boss's second phase is on too
                if (run.Table.Raged && run.Boss.RageText != null)
                {
                    _plateTag.text = "PHASE TWO  ·  " + _plateTag.text;
                    twist += "\n" + Ui.Colored("Phase two: " + run.Boss.RageText, Ui.Bad);
                }
                _plateText.text = Ui.Rich(twist);
                Ui.Tip(_plate, $"{run.Boss.Name}: {definition.Ability}", $"{twist}\nHow to beat them: {definition.Tip}");
                return;
            }
            _plateTag.text = "YOUR DEALER";
            _plateName.text = run.Dealer?.Name.ToUpperInvariant();
            _plateText.text = Ui.Rich(run.Dealer?.Style);
        }

        private void RefreshDealer(Table table)
        {
            _dealerCards.Clear();
            _dealerViews.Clear();
            // once your turn is over the dealer reveals and draws one card at a time, with a beat between each
            bool dealerPlaying = table.Phase != TablePhase.PlayerTurn && table.Phase != TablePhase.InsuranceOffer;
            float startCursor = _cursor;
            for (int i = 0; i < table.DealerCards.Count; i++)
            {
                bool hidden = i == 1 && table.HoleCardHidden;
                var view = Animate(hidden ? CardView.CreateFaceDown() : CardView.Create(table.DealerCards[i]), table.DealerCards[i], hidden,
                    dealerPlaying && (i >= 2 || i == 1) ? 520 : 110);
                if (i > 0) view.style.marginLeft = 10;
                _dealerCards.Add(view);
                _dealerViews.Add(view);
            }
            if (table.DealerCards.Count == 0) Spots(_dealerCards);
            _dealerBadgeSlot.Clear();
            if (table.DealerCards.Count > 0)
            {
                var value = table.HoleCardHidden ? table.DealerVisibleValue : table.DealerValue;
                string text = table.HoleCardHidden ? $"SHOWS {value.Total}" : value.IsBust ? $"BUST  {value.Total}" : $"DEALER  {value.Total}";
                Badge(_dealerBadgeSlot, text, value.IsBust && !table.HoleCardHidden ? Ui.Good : Ui.Cream);
                // the total only shows once the last card is down
                if (_cursor > startCursor) Ui.Enter(_dealerBadgeSlot, 6, (long)(_cursor / Speed) + 250, 0.2f);
            }
            else Badge(_dealerBadgeSlot, "DEALER", Ui.Dim);

            bool twins = table.SecondDealerCards.Count > 0;
            Ui.Show(_secondDealer, twins || table.Rules.DealerPlaysTwoHands);
            _secondDealerCards.Clear();
            for (int i = 0; i < table.SecondDealerCards.Count; i++)
            {
                var card = table.SecondDealerCards[i];
                var view = Animate(CardView.Create(card), card, false, 520);
                if (i > 0) view.style.marginLeft = 10;
                _secondDealerCards.Add(view);
            }
            _secondBadgeSlot.Clear();
            Badge(_secondBadgeSlot, twins ? (table.SecondDealerValue.IsBust ? $"BUST  {table.SecondDealerValue.Total}" : $"TWIN  {table.SecondDealerValue.Total}") : "TWIN PLAYS LAST",
                twins ? Ui.Cream : Ui.Dim);

            var next = table.NextCardPreview;
            Ui.Show(_nextCard, next != null);
            _nextCardSlot.Clear();
            if (next != null) _nextCardSlot.Add(CardView.Create(next, 0.75f));
        }

        // faint outlines where the cards will land
        private static void Spots(VisualElement row)
        {
            for (int i = 0; i < 2; i++)
            {
                var spot = new VisualElement();
                spot.style.width = CardView.Width;
                spot.style.height = CardView.Height;
                if (i > 0) spot.style.marginLeft = 10;
                Ui.Rounded(spot, 8);
                Ui.Border(spot, 2, new Color(0.98f, 0.79f, 0.32f, 0.16f));
                spot.style.backgroundColor = new Color(0, 0, 0, 0.08f);
                row.Add(spot);
            }
        }

        private static void Badge(VisualElement slot, string text, Color color)
        {
            var badge = Ui.Text(slot, text, 13, color, Weight.Heavy);
            badge.style.letterSpacing = 1;
            Ui.Padding(badge, 3, 12);
            Ui.Rounded(badge, 12);
            badge.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            Ui.Border(badge, 1, new Color(color.r, color.g, color.b, 0.4f));
        }

        // a subtle hint about the hole card, spelled out if you hold Tell Reader
        private void RefreshTell(Run run, Table table)
        {
            var dealer = table == run.Table ? run.Dealer : null;
            string tell = dealer != null && table.HoleCardHidden && table.DealerCards.Count > 1 ? dealer.Tell(table) : null;
            if (tell != null && run.Gambler.Vices.Any(v => !v.Disabled && v.RevealsTells)) tell += "  " + dealer.TellMeaning(table);
            _tell.text = tell != null ? $"“{tell}”" : "";
            Ui.Show(_tell, tell != null);
        }

        // a card that wasn't on screen yet flies in from the shoe. a card that was face down flips over
        private VisualElement Animate(VisualElement view, Card card, bool faceDown, float gapMs = 110)
        {
            if (!_shown.Add((card, _dealIndex, faceDown))) return view;
            _cursor += _cursor > 0 ? gapMs : 30;
            long delay = (long)(_cursor / Speed);
            if (!faceDown && _shown.Contains((card, _dealIndex, true)))
            {
                Ui.Transition(view, 0, "scale");
                view.style.scale = new Scale(new Vector2(0.02f, 1));
                view.schedule.Execute(() =>
                {
                    Ui.Transition(view, 0.24f / Speed, EasingMode.EaseOutBack, "scale");
                    view.style.scale = new Scale(Vector2.one);
                    CardLanded?.Invoke();
                }).StartingIn(delay);
                return view;
            }

            view.style.opacity = 0;
            EventCallback<GeometryChangedEvent> fly = null;
            fly = _ =>
            {
                view.UnregisterCallback(fly);
                var from = _shoe.worldBound.center - view.worldBound.center;
                Ui.Transition(view, 0, "translate", "rotate", "scale", "opacity");
                view.style.translate = new Translate(from.x, from.y);
                view.style.rotate = new Rotate(-35);
                view.style.scale = new Scale(new Vector2(0.6f, 0.6f));
                view.schedule.Execute(() =>
                {
                    Ui.Transition(view, 0.34f / Speed, "translate", "rotate", "scale");
                    view.style.opacity = 1;
                    view.style.translate = new Translate(0, 0);
                    view.style.rotate = new Rotate(0);
                    view.style.scale = new Scale(Vector2.one);
                    CardLanded?.Invoke();
                }).StartingIn(delay);
            };
            view.RegisterCallback(fly);
            return view;
        }

        private void RefreshPlayer(Table table)
        {
            _playerHands.Clear();
            _handViews.Clear();
            if (table.PlayerHands.Count == 0)
            {
                var empty = Ui.Column(_playerHands, 8);
                empty.style.alignItems = Align.Center;
                Ui.Padding(empty, 6, 8);
                var spots = Ui.Row(empty);
                Spots(spots);
                Badge(Ui.Row(empty), "YOU", Ui.Dim);
            }
            var active = table.ActiveHand;
            foreach (var hand in table.PlayerHands)
            {
                var group = Ui.Column(_playerHands, 8);
                group.style.alignItems = Align.Center;
                Ui.Padding(group, 6, 8);
                Ui.Rounded(group, 12);
                bool highlight = active == hand && table.PlayerHands.Count > 1;
                Ui.Border(group, 2, highlight ? Ui.Gold : new Color(0, 0, 0, 0));
                if (highlight) group.style.backgroundColor = new Color(0.98f, 0.79f, 0.32f, 0.08f);

                // the Mime keeps your second card face down until the hand is over
                bool mimed = table.Rules.SecondCardFaceDown && !hand.IsDone;
                var cards = Ui.Row(group);
                var views = new List<VisualElement>();
                for (int i = 0; i < hand.Cards.Count; i++)
                {
                    bool down = mimed && i == 1;
                    var view = Animate(down ? CardView.CreateFaceDown() : CardView.Create(hand.Cards[i]), hand.Cards[i], down);
                    // overlap a little so five-card hands fit
                    if (i > 0) view.style.marginLeft = -20;
                    cards.Add(view);
                    views.Add(view);
                }
                _handViews.Add((hand, group, views));

                var (text, color) = mimed ? ("?", Ui.Cream) : Describe(hand, table);
                var badges = Ui.Row(group, 6);
                Badge(badges, text, color);
                if (hand.IsDoubled) Ui.Pill(badges, "DOUBLED x2", Ui.Mult, filled: true, size: 10);

                // the combos these cards make right now. they only pay if the hand beats the dealer
                if (!mimed)
                {
                    var combos = Combos.Find(hand.Cards);
                    var score = table.LastScores.FirstOrDefault(sc => sc.Hand == hand);
                    bool paid = score != null && score.Outcome.IsWin();
                    bool lost = score != null && !paid;
                    if (combos.Count > 0)
                    {
                        var row = Ui.Row(group, 4);
                        foreach (var combo in combos)
                        {
                            var (chips, mult) = Combos.Base(combo);
                            var pill = Ui.Pill(row, Combos.Name(combo).ToUpperInvariant(), lost ? Ui.Dim : Ui.Cocktail, filled: paid, size: 11);
                            Ui.Tip(pill, Combos.Name(combo), $"{Combos.Describe(combo)}\n+{chips} Chips, +{mult} Mult if this hand beats the dealer." +
                                (lost ? "\nThis hand didn't win, so it doesn't pay." : ""));
                        }
                    }
                }
            }
        }

        private static (string, Color) Describe(PlayerHand hand, Table table)
        {
            var value = hand.Value;
            if (hand.IsBlackjack) return ("BLACKJACK!", Ui.Gold);
            if (hand.RescuedAsPush) return ($"BUST {value.Total} · SAVED AS A PUSH", Ui.Cream);
            if (value.IsBust) return ($"BUST  {value.Total}", Ui.Bad);
            if (value.IsCharlie) return ("FIVE-CARD CHARLIE!", Ui.Gold);
            string limit = table.Rules.PlayerBustLimit != Balance.BustLimit ? $"  (BUST OVER {table.Rules.PlayerBustLimit})" : "";
            string total = value.IsSoft ? $"SOFT {value.Total}" : $"YOU  {value.Total}";
            return (total + limit, value.Total == table.Rules.PlayerBustLimit ? Ui.Gold : Ui.Cream);
        }

        private void RefreshResult(Table table)
        {
            string message;
            Color color = Ui.Gold;
            string breakdown;
            if (table.Phase == TablePhase.InsuranceOffer)
            {
                message = "THE DEALER SHOWS AN ACE";
                breakdown = "";
            }
            else if (table.Phase == TablePhase.DealerDecision)
            {
                message = "YOUR CALL";
                breakdown = Ui.Rich($"The dealer has {table.DealerValue.Total} and wants to hit. The next card is the {CardView.Name(table.NextCardPreview)}. Let him take it?");
            }
            else if (table.LastScores.Count == 0)
            {
                message = "";
                breakdown = "";
            }
            else
            {
                long total = table.LastScores.Sum(s => s.Total);
                var best = table.LastScores.OrderByDescending(s => s.Total).First();
                message = table.LastScores.Count > 1 ? $"SPLIT HANDS: {total:N0}" : Headline(best);
                color = total > 0 ? Ui.Gold : best.Outcome == Outcome.Push ? Ui.Cream : Ui.Bad;

                var lines = table.LastScores.Select(Breakdown).Where(l => l.Length > 0).ToList();
                if (table.SideBetResult is SideBetResult bet)
                    lines.Add($"{SideBets.Name(bet.Bet)}: {bet.Label}" + (bet.Won ? "  " + Ui.Colored($"+{bet.Payout}g", Ui.Gold) : ""));
                if (table.AnteLost) lines.Add(Ui.Colored($"Ante lost: -{Balance.AnteUpLoss}g", Ui.Bad));
                if (table.RaiseHandsLost > 0) lines.Add(Ui.Colored($"Lost the raise: -{table.RaiseHandsLost} Hand{(table.RaiseHandsLost == 1 ? "" : "s")}", Ui.Bad));
                foreach (var feat in table.LastScores.SelectMany(s => s.Feats)) lines.Add(Ui.Colored($"{Feats.Name(feat)}! x{Feats.Factor(feat):0.#} Mult", Ui.Cocktail));
                breakdown = string.Join("\n", lines);
            }
            _message.text = message;
            Ui.Show(_message, message.Length > 0);
            Ui.Show(_print, message.Length == 0);
            _message.style.color = color;
            _breakdown.text = breakdown;
            string key = message + "|" + table.DealsPlayed;
            bool fresh = key != _lastMessage;
            _lastMessage = key;
            bool result = table.LastScores.Count > 0 && (table.Phase == TablePhase.ReadyToDeal || table.IsOver);
            if (result && fresh)
            {
                // the scoring playback runs first, then calls ActOut
                _resultPending = true;
                _resultTable = table;
                _message.parent.style.opacity = 0;
            }
            else if (result && !_resultPending) PlayOutcome(table, false);
            else if (!result)
            {
                _resultPending = false;
                _message.parent.style.opacity = 1;
            }
        }

        // the boss's second phase: the nameplate shakes and stays red-hot
        public void Enrage()
        {
            Shake(_plate, 0);
            Ui.Pop(_plateFace, 1.35f);
        }

        // a speech bubble from the boss, with their portrait giving a little jolt
        public void Say(string text)
        {
            _bubbleText.text = Ui.Rich(text);
            Ui.Show(_bubble, true);
            Ui.Transition(_bubble, 0, "opacity");
            _bubble.style.opacity = 1;
            Ui.Pop(_bubble, 1.25f);
            Ui.Pop(_plateFace, 1.2f);
            string said = text;
            _bubble.schedule.Execute(() =>
            {
                if (_bubbleText.text != Ui.Rich(said)) return;
                Ui.Transition(_bubble, 0.4f, "opacity");
                _bubble.style.opacity = 0;
            }).StartingIn(3200);
        }

        // the playback finished: show the verdict and let the cards act it out
        public void ActOut()
        {
            if (!_resultPending) return;
            _resultPending = false;
            Ui.Enter(_message.parent, 10, 0, 0.25f);
            Ui.Pop(_message, 1.35f);
            PlayOutcome(_resultTable, true);
        }

        // a card of yours scoring: it jumps and flashes gold
        public void PopCard(int hand, int card)
        {
            if (hand < 0 || hand >= _handViews.Count) return;
            var cards = _handViews[hand].cards;
            if (card < 0 || card >= cards.Count) return;
            var view = cards[card];
            Ui.Transition(view, 0.08f, "translate", "scale");
            view.style.translate = new Translate(0, -16);
            view.style.scale = new Scale(new Vector2(1.08f, 1.08f));
            CardView.SetSelected(view, true);
            view.style.translate = new Translate(0, -16);
            view.schedule.Execute(() =>
            {
                Ui.Transition(view, 0.25f, EasingMode.EaseOutBack, "translate", "scale");
                view.style.translate = new Translate(0, 0);
                view.style.scale = new Scale(Vector2.one);
                CardView.SetSelected(view, false);
            }).StartingIn((long)(160 / Speed));
        }

        // where a card of yours is, in world space
        public Vector2? CardCenter(int hand, int card)
        {
            if (hand < 0 || hand >= _handViews.Count) return null;
            var cards = _handViews[hand].cards;
            if (card < 0 || card >= cards.Count) return null;
            return cards[card].worldBound.center;
        }

        public int HandIndex(PlayerHand hand) => _handViews.FindIndex(h => h.hand == hand);

        // the cards act out the result: winners hop and glow, losers slump, busts tumble off the felt
        private void PlayOutcome(Table table, bool animate)
        {
            long delay = 0;
            bool anyWin = false, anyLoss = false;
            foreach (var (hand, group, cards) in _handViews)
            {
                var score = table.LastScores.FirstOrDefault(sc => sc.Hand == hand);
                if (score == null) continue;
                var outcome = score.Outcome;
                if (outcome.IsWin())
                {
                    anyWin = true;
                    bool big = outcome == Outcome.Blackjack || outcome == Outcome.Charlie || outcome == Outcome.TwentyOne;
                    Glow(group, big ? Ui.Gold : Ui.Good, animate, delay);
                    if (animate) Hop(cards, delay, big ? 34 : 22, big);
                }
                else if (outcome == Outcome.Bust)
                {
                    anyLoss = true;
                    Tumble(cards, animate, delay);
                }
                else if (outcome == Outcome.Lose || outcome == Outcome.Surrender)
                {
                    anyLoss = true;
                    Slump(cards, animate, delay);
                    if (animate) Shake(group, delay + 200);
                }
            }
            var dealer = table.DealerValue;
            if (dealer.IsBust) Tumble(_dealerViews, animate, delay);
            else if (anyWin && !anyLoss) Slump(_dealerViews, animate, delay);
            else if (anyLoss && !anyWin && animate) Hop(_dealerViews, delay, 14, false);
        }

        private static void Hop(List<VisualElement> cards, long delay, float height, bool spin)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                long at = delay + 70 * i;
                card.schedule.Execute(() =>
                {
                    Ui.Transition(card, 0.16f, EasingMode.EaseOutSine, "translate", "rotate");
                    card.style.translate = new Translate(0, -height);
                    if (spin) card.style.rotate = new Rotate(i % 2 == 0 ? -8 : 8);
                }).StartingIn(at);
                card.schedule.Execute(() =>
                {
                    Ui.Transition(card, 0.42f, EasingMode.EaseOutBounce, "translate", "rotate");
                    card.style.translate = new Translate(0, 0);
                    card.style.rotate = new Rotate(0);
                }).StartingIn(at + 170);
            }
        }

        private static void Glow(VisualElement group, Color color, bool animate, long delay)
        {
            void Apply()
            {
                Ui.Transition(group, 0.35f, "background-color", "border-top-color", "border-bottom-color", "border-left-color", "border-right-color");
                Ui.Border(group, 2, color);
                group.style.backgroundColor = new Color(color.r, color.g, color.b, 0.12f);
            }
            if (!animate) Apply();
            else group.schedule.Execute(Apply).StartingIn(delay);
        }

        private void Slump(List<VisualElement> cards, bool animate, long delay)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                float tilt = (i - (cards.Count - 1) / 2f) * 3;
                void Apply()
                {
                    if (animate) Ui.Transition(card, 0.6f, EasingMode.EaseOutCubic, "translate", "rotate", "opacity");
                    card.style.translate = new Translate(0, 10);
                    card.style.rotate = new Rotate(tilt);
                    card.style.opacity = 0.5f;
                }
                if (!animate) Apply();
                else card.schedule.Execute(Apply).StartingIn(delay + 40 * i);
            }
        }

        private void Tumble(List<VisualElement> cards, bool animate, long delay)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                // the same hand always lands the same way, even when the table redraws
                var random = new System.Random(i * 7919 + cards.Count);
                float angle = (random.Next(2) == 0 ? -1 : 1) * (14 + random.Next(22));
                var drift = new Translate(random.Next(-26, 27), 34 + random.Next(26));
                void Apply()
                {
                    if (animate) Ui.Transition(card, 0.7f, EasingMode.EaseInCubic, "translate", "rotate", "opacity");
                    card.style.translate = drift;
                    card.style.rotate = new Rotate(angle);
                    card.style.opacity = 0.35f;
                }
                if (!animate) Apply();
                else card.schedule.Execute(Apply).StartingIn(delay + 90 * i);
            }
        }

        private static void Shake(VisualElement e, long delay)
        {
            if (Settings.ReducedMotion) return;
            float[] steps = { -9, 8, -6, 5, -3, 0 };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = steps[i];
                e.schedule.Execute(() =>
                {
                    Ui.Transition(e, 0.04f, "translate");
                    e.style.translate = new Translate(x, 0);
                }).StartingIn(delay + 45 * i);
            }
        }

        // one line that says what you can do right now
        private void RefreshHint(Table table)
        {
            string hint = "";
            var hand = table.ActiveHand;
            switch (table.Phase)
            {
                case TablePhase.ReadyToDeal:
                    hint = table.DealsPlayed == 0
                        ? $"Score {table.Target:N0} points in {table.HandsLeft} Hands to clear this table. Combos like a Pair or a Flush only pay when you beat the dealer."
                        : $"{table.HandsLeft} Hands left. Deal the next one when you're ready.";
                    if (table.WinStreak > 0)
                        hint += $" Win streak {table.WinStreak}: your next win gets +{System.Math.Min(table.WinStreak * Balance.StreakMultPerWin, Balance.MaxStreakBonus)} Mult.";
                    break;
                case TablePhase.InsuranceOffer:
                    hint = $"Insurance costs {Balance.InsuranceCost}g. If the dealer has blackjack, your hand becomes a push instead of a loss.";
                    break;
                case TablePhase.PlayerTurn when hand != null:
                    var value = hand.Value;
                    if (value.IsBust)
                        hint = table.BurnsLeft > 0 && !table.Rules.BurnsDisabled
                            ? "You went bust! Burn throws that card away and draws a new one. Or Stand to take the loss."
                            : "You went bust. Stand to take the loss.";
                    else if (table.PlayerHands.Count > 1)
                        hint = $"Playing hand {table.ActiveHandIndex + 1} of {table.PlayerHands.Count}. Hit to take a card, Stand to keep {value.Total}.";
                    else
                        hint = $"You have {value.Total}. Hit to take a card, or Stand to keep it. The dealer must draw to 17.";
                    if (table.Rules.HitCost > 0) hint += $" Every Hit costs {table.Rules.HitCost} gold.";
                    break;
                case TablePhase.DealerDecision:
                    hint = "Cold Read lets you choose whether the dealer draws.";
                    break;
            }
            _hint.text = Ui.Rich(hint);
            Ui.Show(_hint, hint.Length > 0);
        }

        private void RefreshBets(Table table)
        {
            bool open = table.Phase == TablePhase.ReadyToDeal;
            Ui.Show(_bets, open);
            if (!open) return;
            var legal = table.LegalActions();
            string placed = table.SideBet != SideBet.None ? SideBets.Name(table.SideBet).ToUpperInvariant() + " PLACED" : "";
            if (table.AntedUp) placed += (placed.Length > 0 ? "  +  " : "") + "ANTED UP x2";
            _betPlaced.text = placed;
            Ui.Show(_betPlaced, placed.Length > 0);
            foreach (var bet in SideBets.All)
            {
                var button = _betButtons[SideBets.ActionFor(bet)];
                Ui.SetText(button, $"{SideBets.Name(bet)}  ·  {table.SideBetCost(bet)}g");
                Ui.Show(button, table.SideBet == SideBet.None);
                Ui.SetEnabled(button, legal.Contains(SideBets.ActionFor(bet)));
            }
            var ante = _betButtons[PlayerAction.AnteUp];
            Ui.Show(ante, !table.AntedUp);
            Ui.SetEnabled(ante, legal.Contains(PlayerAction.AnteUp));

            if (table.BetLevel > 1) placed += (placed.Length > 0 ? "  +  " : "") + $"RAISED x{table.BetLevel}";
            _betPlaced.text = placed;
            Ui.Show(_betPlaced, placed.Length > 0);
            int next = table.NextBetLevel;
            Ui.SetText(_raise, next > table.BetLevel ? $"Raise to x{next}" : "Back to x1");
            Ui.Show(_raise, legal.Contains(PlayerAction.Raise));
            Ui.Paint(_raise, next > table.BetLevel ? ButtonKind.Gold : ButtonKind.Ghost);
        }

        // the trick cards you hold, lit up when they can be played right now
        private void RefreshTricks(Run run, Table table)
        {
            var tricks = run.Gambler.Tricks;
            bool show = tricks.Count > 0 && table == run.Table && run.Phase == RunPhase.AtTable && !table.IsOver;
            Ui.Show(_tricks, show);
            if (!show) return;
            _trickCards.Clear();
            for (int i = 0; i < tricks.Count; i++)
            {
                int slot = i;
                var kind = tricks[i];
                bool usable = run.IsLegal(RunInput.PlayTrick(i));
                var card = Ui.Button(_trickCards, Tricks.Name(kind), () => TrickChosen?.Invoke(slot), ButtonKind.Normal, (i + 1).ToString());
                card.style.height = 40;
                card.style.width = 150;
                Ui.Padding(card, 0, 8);
                card.style.backgroundImage = Ui.Gradient(new Color(0.14f, 0.36f, 0.56f), new Color(0.05f, 0.14f, 0.26f));
                Ui.BorderColor(card, usable ? new Color(0.55f, 0.85f, 1f) : new Color(1, 1, 1, 0.15f));
                card.Q<Label>().style.fontSize = 13;
                card.Q<Label>().style.whiteSpace = WhiteSpace.Normal;
                Ui.SetEnabled(card, usable);
                Ui.Tip(card, Tricks.Name(kind), Tricks.Describe(kind) + (usable ? "\nClick (or press " + (i + 1) + ") to play it now." : "\nNot usable right now: play it during your turn."));
            }
        }

        private static string Headline(HandScore score) => score.Outcome switch
        {
            Outcome.Blackjack => "BLACKJACK!",
            Outcome.Charlie => score.Hand.Cards.Count switch { 4 => "FOUR-CARD CHARLIE!", 5 => "FIVE-CARD CHARLIE!", 6 => "SIX-CARD CHARLIE!", _ => "CHARLIE!" },
            Outcome.TwentyOne => "TWENTY-ONE!",
            Outcome.Win => score.DealerBusted ? "DEALER BUSTS!" : "YOU WIN",
            Outcome.Push => "PUSH",
            Outcome.Bust => "BUST",
            Outcome.Surrender => "SURRENDERED",
            _ => "DEALER WINS",
        };

        private static string Breakdown(HandScore score)
        {
            if (score.Outcome == Outcome.Surrender) return $"You paid {Balance.SurrenderCost}g and kept the Hand.";
            if (score.Total == 0 && !score.Outcome.IsWin()) return score.Outcome == Outcome.Bust ? "Over 21. This hand scores nothing." : "This hand scores nothing.";
            string chips = Ui.Colored($"{score.Chips} Chips", Ui.Chips);
            string mult = Ui.Colored($"{score.Mult:0.##} Mult", Ui.Mult);
            return $"{score.Outcome.Label()}:  {chips}  ×  {mult}  =  <b>{score.Total:N0}</b>";
        }
    }
}
