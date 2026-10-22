using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // your whole deck, a row per suit. also picks the cards a Cocktail or room works on
    public sealed class DeckView
    {
        private const float Scale = 0.56f;

        private readonly VisualElement _root, _grid;
        private readonly Label _title, _hint, _counts, _picked;
        private readonly Button _confirm;
        private readonly List<int> _selection = new List<int>();
        private Run _run;
        // what's being picked for: a Cocktail or a room service. null when just looking
        private Func<Card, bool> _canPick;
        private Func<List<int>, RunInput> _makeInput;
        private int _max;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public DeckView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = Ui.FeltDark;
            Ui.Stretch(_root, Ui.FeltTexture());
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 18, 28);
            parent.Add(_root);

            _title = Ui.Title(_root, "", 30, Ui.Gold);
            _hint = Ui.Body(_root, "", 15, Ui.Cream, center: true);
            _hint.style.marginTop = 4;
            _hint.style.maxWidth = 760;
            _counts = Ui.Text(_root, "", 12, Ui.Dim, Weight.Medium);
            _counts.style.marginTop = 4;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.alignSelf = Align.Stretch;
            scroll.style.marginTop = 10;
            _root.Add(scroll);
            _grid = Ui.Column(null, 6);
            _grid.style.alignItems = Align.Center;
            scroll.Add(_grid);

            var buttons = Ui.Row(_root, 12);
            buttons.style.marginTop = 10;
            _picked = Ui.Text(buttons, "", 14, Ui.Gold, Weight.Bold);
            _confirm = Ui.Button(buttons, "Use", Confirm, ButtonKind.Gold);
            Ui.Button(buttons, "Close", Close, ButtonKind.Normal, "Esc");
            Ui.Show(_root, false);
        }

        public void ShowDeck(Run run)
        {
            _run = run;
            _canPick = null;
            _selection.Clear();
            _title.text = "YOUR DECK";
            _hint.text = Ui.Rich("You AND the dealer draw from this deck. Hover a card to see what it does.");
            Ui.Show(_confirm, false);
            Ui.Show(_picked, false);
            Open();
        }

        public void PickFor(Run run, int slot)
        {
            var cocktail = run.Gambler.Cocktails[slot];
            var (min, max) = cocktail.Targets;
            Pick(run, cocktail.Name, $"{cocktail.Description} Click {(min == max ? $"{max}" : $"{min} to {max}")} card{(max > 1 ? "s" : "")}, then Use.",
                max, card => Cocktails.CanTarget(cocktail.Kind, card),
                picked => RunInput.UseCocktail(slot, picked.Count > 0 ? picked[0] : -1, picked.Count > 1 ? picked[1] : -1));
        }

        public void Pick(Run run, string title, string hint, int max, Func<Card, bool> canPick, Func<List<int>, RunInput> makeInput)
        {
            _run = run;
            _canPick = canPick;
            _makeInput = makeInput;
            _max = max;
            _selection.Clear();
            _title.text = title.ToUpperInvariant();
            _hint.text = Ui.Rich(hint + " Greyed-out cards can't be picked.");
            Ui.Show(_confirm, true);
            Ui.Show(_picked, true);
            Open();
        }

        private void Open()
        {
            Rebuild();
            Ui.Show(_root, true);
            Ui.Enter(_root, 16, 0, 0.25f);
        }

        public void Close()
        {
            Tooltip.HideAll();
            Ui.Show(_root, false);
        }

        private void Rebuild()
        {
            _grid.Clear();
            var deck = _run.Gambler.Deck;
            int tens = deck.Count(c => !c.IsCursed && c.Rank >= Rank.Ten && c.Rank <= Rank.King);
            int aces = deck.Count(c => !c.IsCursed && c.Rank == Rank.Ace);
            int upgraded = deck.Count(c => c.Enhancement != Enhancement.None || c.Stamp != Stamp.None);
            int cursed = deck.Count(c => c.IsCursed);
            _counts.text = $"{deck.Count} CARDS   ·   {tens} WORTH 10   ·   {aces} ACES   ·   {upgraded} UPGRADED" + (cursed > 0 ? $"   ·   {cursed} CURSED" : "");

            // one row per suit, high cards first, curses last
            var rows = new List<(string name, List<int> cards)>();
            foreach (Suit suit in new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds })
                rows.Add((suit.ToString(), Enumerable.Range(0, deck.Count).Where(i => !deck[i].IsCursed && deck[i].Suit == suit)
                    .OrderByDescending(i => deck[i].Rank).ToList()));
            rows.Add(("Cursed", Enumerable.Range(0, deck.Count).Where(i => deck[i].IsCursed).ToList()));

            foreach (var (name, cards) in rows)
            {
                if (cards.Count == 0) continue;
                var row = Ui.Row(_grid);
                var label = Ui.Caption(row, $"{name.ToUpperInvariant()}  {cards.Count}");
                label.style.width = 90;
                foreach (int i in cards)
                {
                    int index = i;
                    var card = CardView.Create(deck[i], Scale);
                    card.style.marginRight = -12;
                    bool eligible = _canPick == null || _canPick(deck[i]);
                    if (_canPick != null)
                    {
                        card.style.opacity = eligible ? 1f : 0.28f;
                        CardView.SetSelected(card, _selection.Contains(i));
                        if (eligible)
                        {
                            card.RegisterCallback<ClickEvent>(_ => Toggle(index));
                            card.RegisterCallback<PointerEnterEvent>(_ => { if (!_selection.Contains(index)) card.style.translate = new Translate(0, -6); });
                            card.RegisterCallback<PointerLeaveEvent>(_ => { if (!_selection.Contains(index)) card.style.translate = new Translate(0, 0); });
                        }
                    }
                    row.Add(card);
                }
            }
            if (_canPick == null) return;
            _picked.text = $"Picked {_selection.Count} of {_max}";
            Ui.SetEnabled(_confirm, _run.IsLegal(_makeInput(_selection)));
        }

        private void Toggle(int index)
        {
            if (_selection.Contains(index)) _selection.Remove(index);
            else
            {
                if (_selection.Count >= _max) _selection.RemoveAt(0);
                _selection.Add(index);
            }
            Rebuild();
        }

        private void Confirm()
        {
            var input = _makeInput(_selection);
            Close();
            InputChosen?.Invoke(input);
        }
    }
}
