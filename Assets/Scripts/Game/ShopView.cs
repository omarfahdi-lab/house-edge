using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // between tables: the payout, the shop, and boss rewards
    public sealed class ShopView
    {
        private static readonly Color CocktailColor = new Color(0.75f, 0.45f, 0.95f);
        private static readonly Color CompColor = new Color(0.36f, 0.68f, 1f);

        private readonly VisualElement _root, _vices, _extras;
        private readonly Label _title, _payout, _shopTitle, _next;
        private readonly Button _reroll, _leave;
        private Run _run;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;

        public ShopView(VisualElement parent)
        {
            _root = Ui.Column(parent, 10);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 20, 28);
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.03f, 0.12f, 0.08f, 0.97f);

            _title = Ui.Text(_root, "", 32, Ui.Gold, bold: true);
            _title.style.letterSpacing = 3;
            _payout = Ui.Text(_root, "", 17, Ui.Cream);
            _payout.enableRichText = true;
            _payout.style.whiteSpace = WhiteSpace.Normal;
            _payout.style.unityTextAlign = TextAnchor.MiddleCenter;

            _shopTitle = Ui.Text(_root, "", 14, Ui.Dim, bold: true);
            _shopTitle.style.marginTop = 6;
            _vices = Ui.Row(_root, 14);
            _vices.style.alignItems = Align.FlexStart;
            _extras = Ui.Row(_root, 12);
            _extras.style.alignItems = Align.FlexStart;

            var buttons = Ui.Row(_root, 12);
            buttons.style.marginTop = 6;
            _reroll = Ui.Button(buttons, "", () => InputChosen?.Invoke(RunInput.Reroll()));
            _leave = Ui.Button(buttons, "", Leave, accent: true);
            _leave.enableRichText = true;
            _next = Ui.Text(_root, "", 14, Ui.Dim);
            _next.style.whiteSpace = WhiteSpace.Normal;
            _next.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void Leave() => InputChosen?.Invoke(_run.Phase == RunPhase.BossReward ? RunInput.SkipReward() : RunInput.LeaveShop());

        public void Refresh(Run run)
        {
            _run = run;
            bool reward = run.Phase == RunPhase.BossReward;
            var p = run.LastPayout;
            _title.text = reward ? $"{run.Boss?.Name?.ToUpperInvariant() ?? "THE BOSS"} FALLS" : "TABLE CLEARED";
            string payout = $"+{p.Base}g for the table   +{p.UnusedHands}g unused Hands   +{p.Interest}g interest   " +
                            $"= <color=#{ColorUtility.ToHtmlStringRGB(Ui.Gold)}><b>+{p.Total}g</b></color>";
            if (run.LastFreeVice != null) payout += $"\nYou beat security! Heat cleared and a free {run.LastFreeVice.Name}.";
            if (run.Gambler.HeatLevel >= HeatLevel.Suspected) payout += $"\n<color=#{ColorUtility.ToHtmlStringRGB(Ui.Bad)}>Suspected: prices are up 25%.</color>";
            _payout.text = payout;

            _vices.Clear();
            _extras.Clear();
            if (reward)
            {
                _shopTitle.text = "PICK A REWARD";
                for (int i = 0; i < run.Rewards.Count; i++) Ui.Add(_vices, RewardCard(i));
                Ui.Show(_reroll, false);
                _leave.text = "Skip reward";
                _next.text = run.Gambler.HasFreeViceSlot ? "" : "Your Vice slots are full. Sell one in the sidebar to make room.";
                return;
            }

            _shopTitle.text = "THE SHOP";
            for (int i = 0; i < run.Shop.Offers.Count; i++)
            {
                var offer = run.Shop.Offers[i];
                Ui.Add(offer.Kind == OfferKind.Vice ? _vices : _extras, OfferCard(i));
            }
            Ui.Show(_reroll, true);
            _reroll.text = $"Reroll {run.RerollCost}g";
            Ui.SetEnabled(_reroll, run.IsLegal(RunInput.Reroll()));

            int nextFloor = run.Stage == TableKind.Boss ? run.Floor + 1 : run.Floor;
            var nextStage = run.Stage == TableKind.Boss ? TableKind.Low : run.Stage + 1;
            _leave.text = "Next table  <size=12><color=#FFFFFF88>Space</color></size>";
            string boss = nextStage == TableKind.Boss ? $"\n{run.FloorBoss.Name}: {run.FloorBoss.Twist}" : "";
            _next.text = $"Next: Floor {nextFloor}, {Hud.StageName(nextStage, nextFloor)}, target {Balance.Target(nextFloor, nextStage):N0}{boss}";
        }

        private VisualElement OfferCard(int index)
        {
            var offer = _run.Shop.Offers[index];
            var column = Ui.Column(null, 6);
            column.style.alignItems = Align.Center;
            VisualElement card;
            float width;
            if (offer.Kind == OfferKind.Vice)
            {
                width = 190;
                card = ViceCardView.Create(offer.Vice, width);
                card.style.minHeight = 150;
            }
            else
            {
                width = 170;
                card = SmallCard(offer.Kind == OfferKind.Cocktail ? "COCKTAIL" : "COMP", offer.Name, offer.Description,
                    offer.Kind == OfferKind.Cocktail ? CocktailColor : CompColor, width);
                if (offer.Kind == OfferKind.Comp) Ui.Text(card, $"Level {_run.Gambler.CompLevel(offer.Comp)} now", 12, Ui.Gold);
            }
            column.Add(card);
            if (offer.Sold) card.style.opacity = 0.3f;

            var input = RunInput.Buy(index);
            string label = offer.Sold ? "Sold"
                : offer.Kind == OfferKind.Vice && !_run.Gambler.HasFreeViceSlot ? "Slots full"
                : offer.Kind == OfferKind.Cocktail && !_run.Gambler.HasFreeCocktailSlot ? "Slots full"
                : $"Buy {_run.PriceOf(offer)}g";
            var buy = Ui.Button(column, label, () => InputChosen?.Invoke(input), accent: !offer.Sold);
            buy.style.width = width;
            buy.style.height = 38;
            Ui.SetEnabled(buy, _run.IsLegal(input));
            return column;
        }

        private VisualElement RewardCard(int index)
        {
            var vice = _run.Rewards[index];
            var column = Ui.Column(null, 6);
            column.style.alignItems = Align.Center;
            var card = ViceCardView.Create(vice, 220);
            card.style.minHeight = 170;
            column.Add(card);
            var input = RunInput.PickReward(index);
            var take = Ui.Button(column, "Take it", () => InputChosen?.Invoke(input), accent: true);
            take.style.width = 220;
            Ui.SetEnabled(take, _run.IsLegal(input));
            return column;
        }

        private static VisualElement SmallCard(string kind, string name, string text, Color color, float width)
        {
            var card = Ui.Column(null, 5);
            card.style.width = width;
            card.style.minHeight = 110;
            card.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            Ui.Rounded(card, 10);
            Ui.Border(card, 2, color);
            Ui.Padding(card, 8, 10);
            Ui.Text(card, kind, 10, color, bold: true);
            Ui.Text(card, name, 16, Ui.Cream, bold: true).style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(card, text, 12, Ui.Dim).style.whiteSpace = WhiteSpace.Normal;
            return card;
        }
    }
}
