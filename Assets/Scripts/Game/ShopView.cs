using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // between tables: the payout, the shop, and boss rewards
    public sealed class ShopView
    {
        private readonly VisualElement _root, _receipt, _vices, _extras, _extrasSection;
        private readonly Label _title, _notes, _vicesTitle, _vicesHint, _next, _nextBoss;
        private readonly Button _reroll, _leave;
        private Run _run;
        private object _shownFor;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;

        public ShopView(VisualElement parent)
        {
            _root = Ui.Column(parent, 8);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 16, 28);
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.01f, 0.05f, 0.035f, 0.82f);

            _title = Ui.Title(_root, "", 38, Ui.Gold);
            _receipt = Ui.Row(_root, 6);
            _notes = Ui.Text(_root, "", 14, Ui.Cream);
            _notes.style.whiteSpace = WhiteSpace.Normal;
            _notes.style.unityTextAlign = TextAnchor.MiddleCenter;

            var vicesHead = Ui.Row(_root, 10);
            vicesHead.style.marginTop = 4;
            _vicesTitle = Ui.Caption(vicesHead, "", Ui.Gold);
            _vicesHint = Ui.Text(vicesHead, "", 12, Ui.Dim);
            _vices = Ui.Row(_root, 14);
            _vices.style.alignItems = Align.FlexStart;

            _extrasSection = Ui.Column(_root, 8);
            _extrasSection.style.alignItems = Align.Center;
            var extrasHead = Ui.Row(_extrasSection, 10);
            Ui.Caption(extrasHead, "DRINKS & COMPS", Ui.Gold);
            Ui.Text(extrasHead, "Cocktails are one-use. Comps permanently boost one kind of win.", 12, Ui.Dim);
            _extras = Ui.Row(_extrasSection, 12);
            _extras.style.alignItems = Align.FlexStart;

            var bottom = Ui.Row(_root, 12);
            bottom.style.marginTop = 6;
            _reroll = Ui.Button(bottom, "", () => InputChosen?.Invoke(RunInput.Reroll()));
            Ui.Tip(_reroll, "Reroll", "Replace everything in the shop with new offers. Costs 1 more each time in the same shop.");
            var next = Ui.Glass(bottom, 1, radius: 10);
            Ui.Padding(next, 6, 14);
            next.style.minWidth = 230;
            Ui.Caption(next, "UP NEXT");
            _next = Ui.Text(next, "", 14, Ui.Cream, Weight.Bold);
            _nextBoss = Ui.Text(next, "", 12, Ui.Bad);
            _nextBoss.style.whiteSpace = WhiteSpace.Normal;
            _nextBoss.style.maxWidth = 300;
            _leave = Ui.Button(bottom, "", Leave, ButtonKind.Primary, "Space");
        }

        private void Leave() => InputChosen?.Invoke(_run.Phase == RunPhase.BossReward ? RunInput.SkipReward() : RunInput.LeaveShop());

        public void Refresh(Run run)
        {
            _run = run;
            bool reward = run.Phase == RunPhase.BossReward;
            // the title only animates when the screen opens, not on every purchase
            object key = (run.Table, run.Phase);
            bool opened = !Equals(key, _shownFor);
            _shownFor = key;

            _title.text = reward ? $"{run.Boss?.Name?.ToUpperInvariant() ?? "THE BOSS"} FALLS" : "TABLE CLEARED";
            if (opened) Ui.Pop(_title, 1.3f);
            RefreshReceipt(run, opened);

            string notes = "";
            if (run.LastFreeVice != null) notes += $"You beat security! Heat cleared and a free {run.LastFreeVice.Name}.";
            if (run.Gambler.HeatLevel >= HeatLevel.Suspected)
                notes += (notes.Length > 0 ? "\n" : "") + Ui.Colored("Security suspects you: prices are up 25%.", Ui.Bad);
            _notes.text = notes;
            Ui.Show(_notes, notes.Length > 0);

            _vices.Clear();
            _extras.Clear();
            if (reward)
            {
                _vicesTitle.text = "PICK A REWARD";
                _vicesHint.text = "Take one of these Vices for free.";
                for (int i = 0; i < run.Rewards.Count; i++) Ui.Add(_vices, RewardCard(i, opened));
                Ui.Show(_extrasSection, false);
                Ui.Show(_reroll, false);
                Ui.SetText(_leave, "Skip reward");
                _next.text = run.Gambler.HasFreeViceSlot ? "Pick one, or skip it" : "Your Vice slots are full";
                _nextBoss.text = run.Gambler.HasFreeViceSlot ? "" : "Sell a Vice in the sidebar to make room.";
                return;
            }

            _vicesTitle.text = "VICES";
            _vicesHint.text = $"Permanent rule-breakers. You hold up to {run.Gambler.ViceSlots}.";
            Ui.Show(_extrasSection, true);
            int vice = 0, extra = 0;
            for (int i = 0; i < run.Shop.Offers.Count; i++)
            {
                var offer = run.Shop.Offers[i];
                bool isVice = offer.Kind == OfferKind.Vice;
                var card = OfferCard(i);
                Ui.Add(isVice ? _vices : _extras, card);
                if (opened) Ui.Enter(card, 30, 80 + 70 * (isVice ? vice++ : 3 + extra++));
            }
            Ui.Show(_reroll, true);
            Ui.SetText(_reroll, $"Reroll  ·  {run.RerollCost}g");
            Ui.SetEnabled(_reroll, run.IsLegal(RunInput.Reroll()));

            int nextFloor = run.Stage == TableKind.Boss ? run.Floor + 1 : run.Floor;
            var nextStage = run.Stage == TableKind.Boss ? TableKind.Low : run.Stage + 1;
            Ui.SetText(_leave, "Next table");
            _next.text = $"Floor {nextFloor}  ·  {Hud.StageName(nextStage, nextFloor)}  ·  target {Balance.Target(nextFloor, nextStage):N0}";
            _nextBoss.text = nextStage == TableKind.Boss ? $"{run.FloorBoss.Name}: {run.FloorBoss.Twist}" : "";
            Ui.Show(_nextBoss, _nextBoss.text.Length > 0);
        }

        // what the table paid, as a little receipt
        private void RefreshReceipt(Run run, bool opened)
        {
            _receipt.Clear();
            if (run.Phase == RunPhase.BossReward && run.LastPayout.Total == 0) return;
            var p = run.LastPayout;
            Item("Table", p.Base, "The payout for clearing this table.");
            Item("Unused Hands", p.UnusedHands, $"+{Balance.GoldPerUnusedHand} gold for every Hand you didn't need.");
            Item("Interest", p.Interest, $"+1 gold for every {Balance.InterestStep} gold you held, up to +{Balance.InterestCap}.");
            Ui.Text(_receipt, "=", 18, Ui.Dim, Weight.Bold);
            var total = Ui.Pill(_receipt, $"+{p.Total}g", Ui.Gold, filled: true, size: 16);
            Ui.Padding(total, 3, 12);
            if (opened) Ui.Pop(total, 1.4f, 450);

            void Item(string name, int gold, string tip)
            {
                var pill = Ui.Glass(_receipt, 6, row: true, radius: 14);
                Ui.Padding(pill, 4, 11);
                Ui.Text(pill, name, 13, Ui.Dim, Weight.Medium);
                Ui.Text(pill, $"+{gold}g", 14, gold > 0 ? Ui.Gold : Ui.Dim, Weight.Heavy);
                Ui.Tip(pill, name, tip);
            }
        }

        private VisualElement OfferCard(int index)
        {
            var offer = _run.Shop.Offers[index];
            var column = Ui.Column(null, 7);
            column.style.alignItems = Align.Center;
            VisualElement card;
            float width;
            if (offer.Kind == OfferKind.Vice)
            {
                width = 204;
                card = ViceCardView.Create(offer.Vice, width);
                card.style.minHeight = 158;
            }
            else
            {
                width = 176;
                bool cocktail = offer.Kind == OfferKind.Cocktail;
                card = SmallCard(cocktail ? "COCKTAIL" : "COMP", offer.Name, offer.Description,
                    cocktail ? Ui.Cocktail : Ui.Chips, width, cocktail ? SuitIcon.Shape.Glass : SuitIcon.Shape.Star);
                if (cocktail) Ui.Tip(card, offer.Name, offer.Description + "\nDrink it from the sidebar when you want.");
                else
                {
                    int level = _run.Gambler.CompLevel(offer.Comp);
                    Ui.Text(card, level > 0 ? $"You have level {level}. Buying makes it {level + 1}." : "You don't have this yet.", 11, Ui.Gold, Weight.Medium)
                        .style.whiteSpace = WhiteSpace.Normal;
                    Ui.Tip(card, offer.Name, "Comps level up each time you buy them and never run out.");
                }
            }
            column.Add(card);
            if (offer.Sold)
            {
                card.style.opacity = 0.35f;
                var sold = Ui.Title(column, "SOLD", 26, Ui.Bad, glow: false);
                sold.style.position = Position.Absolute;
                sold.style.top = 44;
                sold.style.rotate = new Rotate(-12);
                Ui.Border(sold, 3, Ui.Bad);
                Ui.Rounded(sold, 6);
                Ui.Padding(sold, 2, 10);
                sold.pickingMode = PickingMode.Ignore;
            }
            else Ui.Lift(card);

            var input = RunInput.Buy(index);
            int price = _run.PriceOf(offer);
            bool legal = _run.IsLegal(input);
            string label = offer.Sold ? "Sold"
                : offer.Kind == OfferKind.Vice && !_run.Gambler.HasFreeViceSlot ? "Slots full"
                : offer.Kind == OfferKind.Cocktail && !_run.Gambler.HasFreeCocktailSlot ? "Slots full"
                : !legal && _run.Gambler.Gold < price ? $"Need {price}g"
                : $"Buy  ·  {price}g";
            var buy = Ui.Button(column, label, () => InputChosen?.Invoke(input), legal ? ButtonKind.Gold : ButtonKind.Normal);
            buy.style.width = width;
            buy.style.height = 38;
            Ui.SetEnabled(buy, legal);
            if (label == "Slots full")
            {
                var wrap = Ui.Column(column);
                wrap.Add(buy);
                buy.style.marginTop = 0;
                Ui.Tip(wrap, "No room", offer.Kind == OfferKind.Vice
                    ? "All your Vice slots are full. Sell one in the sidebar first."
                    : "Both Cocktail slots are full. Drink one first.");
            }
            return column;
        }

        private VisualElement RewardCard(int index, bool opened)
        {
            var vice = _run.Rewards[index];
            var column = Ui.Column(null, 7);
            column.style.alignItems = Align.Center;
            var card = ViceCardView.Create(vice, 230);
            card.style.minHeight = 176;
            column.Add(card);
            Ui.Lift(card);
            var input = RunInput.PickReward(index);
            var take = Ui.Button(column, "Take it", () => InputChosen?.Invoke(input), ButtonKind.Gold);
            take.style.width = 230;
            Ui.SetEnabled(take, _run.IsLegal(input));
            if (opened) Ui.Enter(column, 30, 150 + 120 * index);
            return column;
        }

        private static VisualElement SmallCard(string kind, string name, string text, Color color, float width, SuitIcon.Shape icon)
        {
            var card = Ui.Column(null, 5);
            card.style.width = width;
            card.style.minHeight = 112;
            card.style.backgroundImage = Ui.Gradient(new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.3f), new Color(0.05f, 0.05f, 0.07f));
            card.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Rounded(card, 12);
            Ui.Border(card, 2, color);
            Ui.Padding(card, 9, 11);
            var head = Ui.Row(card, 5);
            Ui.Add(head, new SuitIcon(icon, color, 12));
            Ui.Caption(head, kind, color);
            Ui.Text(card, name, 16, Ui.Cream, Weight.Heavy).style.whiteSpace = WhiteSpace.Normal;
            Ui.Body(card, text, 12, new Color(0.88f, 0.9f, 0.86f));
            return card;
        }
    }
}
