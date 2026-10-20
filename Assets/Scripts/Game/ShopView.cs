using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // between tables: the payout, Vices for sale, reroll and the way to the next table
    public sealed class ShopView
    {
        private readonly VisualElement _root, _offers;
        private readonly Label _payout, _next;
        private readonly Button _reroll, _leave;
        private Run _run;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;

        public ShopView(VisualElement parent)
        {
            _root = Ui.Column(parent, 14);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 26, 32);
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.03f, 0.12f, 0.08f, 0.94f);

            Ui.Text(_root, "TABLE CLEARED", 34, Ui.Gold, bold: true).style.letterSpacing = 3;
            _payout = Ui.Text(_root, "", 18, Ui.Cream);
            _payout.enableRichText = true;

            var title = Ui.Text(_root, "THE SHOP", 15, Ui.Dim, bold: true);
            title.style.marginTop = 10;
            _offers = Ui.Row(_root, 16);
            _offers.style.alignItems = Align.FlexStart;

            var buttons = Ui.Row(_root, 12);
            buttons.style.marginTop = 10;
            _reroll = Ui.Button(buttons, "", () => InputChosen?.Invoke(RunInput.Reroll()));
            _leave = Ui.Button(buttons, "", () => InputChosen?.Invoke(RunInput.LeaveShop()), accent: true);
            _leave.enableRichText = true;
            _next = Ui.Text(_root, "", 15, Ui.Dim);
        }

        public void Refresh(Run run)
        {
            _run = run;
            var p = run.LastPayout;
            _payout.text = $"+{p.Base}g for the table   +{p.UnusedHands}g unused Hands   +{p.Interest}g interest   " +
                           $"= <color=#{ColorUtility.ToHtmlStringRGB(Ui.Gold)}><b>+{p.Total}g</b></color>";

            _offers.Clear();
            for (int i = 0; i < run.Shop.Offers.Count; i++) Ui.Add(_offers, Offer(i));

            _reroll.text = $"Reroll {run.Shop.RerollCost}g";
            Ui.SetEnabled(_reroll, run.IsLegal(RunInput.Reroll()));

            int nextFloor = run.Stage == TableKind.Boss ? run.Floor + 1 : run.Floor;
            var nextStage = run.Stage == TableKind.Boss ? TableKind.Low : run.Stage + 1;
            _leave.text = "Next table  <size=12><color=#FFFFFF88>Space</color></size>";
            _next.text = $"Next: Floor {nextFloor}, {Hud.StageName(nextStage, nextFloor)}, target {Balance.Target(nextFloor, nextStage):N0}";
        }

        private VisualElement Offer(int index)
        {
            var offer = _run.Shop.Offers[index];
            var column = Ui.Column(null, 8);
            column.style.alignItems = Align.Center;
            var card = ViceCardView.Create(offer.Vice, 200);
            card.style.minHeight = 170;
            column.Add(card);
            if (offer.Sold) card.style.opacity = 0.3f;

            var input = RunInput.BuyVice(index);
            string label = offer.Sold ? "Sold" : !_run.Gambler.HasFreeViceSlot ? "Slots full" : $"Buy {offer.Price}g";
            var buy = Ui.Button(column, label, () => InputChosen?.Invoke(input), accent: !offer.Sold);
            buy.style.width = 200;
            Ui.SetEnabled(buy, _run.IsLegal(input));
            return column;
        }
    }
}
