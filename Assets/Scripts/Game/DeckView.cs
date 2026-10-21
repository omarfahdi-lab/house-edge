using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // your whole deck. also picks the cards a Cocktail works on
    public sealed class DeckView
    {
        private const float Scale = 0.52f;

        private readonly VisualElement _root, _grid;
        private readonly Label _title, _hint;
        private readonly Button _confirm;
        private readonly List<int> _picked = new List<int>();
        private Run _run;
        private Cocktail _cocktail;
        private int _slot;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public DeckView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0.02f, 0.08f, 0.05f, 0.96f);
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 18, 24);
            parent.Add(_root);

            _title = Ui.Text(_root, "", 26, Ui.Gold, bold: true);
            _hint = Ui.Text(_root, "", 15, Ui.Dim);
            _hint.style.marginTop = 4;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.alignSelf = Align.Stretch;
            scroll.style.marginTop = 10;
            _root.Add(scroll);
            _grid = Ui.Row(null);
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.justifyContent = Justify.Center;
            scroll.Add(_grid);

            var buttons = Ui.Row(_root, 12);
            buttons.style.marginTop = 10;
            _confirm = Ui.Button(buttons, "Use", Confirm, accent: true);
            Ui.Button(buttons, "Close  <size=12><color=#FFFFFF88>Esc</color></size>", Close).enableRichText = true;
            Ui.Show(_root, false);
        }

        public void ShowDeck(Run run)
        {
            _run = run;
            _cocktail = null;
            _picked.Clear();
            _title.text = $"YOUR DECK  ({run.Gambler.Deck.Count} cards)";
            _hint.text = "The dealer draws from this deck too.";
            Ui.Show(_confirm, false);
            Rebuild();
            Ui.Show(_root, true);
        }

        public void PickFor(Run run, int slot)
        {
            _run = run;
            _slot = slot;
            _cocktail = run.Gambler.Cocktails[slot];
            _picked.Clear();
            _title.text = _cocktail.Name.ToUpperInvariant();
            var (min, max) = _cocktail.Targets;
            _hint.text = $"{_cocktail.Description}  Pick {(min == max ? $"{max}" : $"{min} to {max}")} card{(max > 1 ? "s" : "")}.";
            Ui.Show(_confirm, true);
            Rebuild();
            Ui.Show(_root, true);
        }

        public void Close() => Ui.Show(_root, false);

        private void Rebuild()
        {
            _grid.Clear();
            var deck = _run.Gambler.Deck;
            for (int i = 0; i < deck.Count; i++)
            {
                int index = i;
                var card = CardView.Create(deck[i], Scale);
                card.style.marginLeft = card.style.marginRight = card.style.marginTop = card.style.marginBottom = 3;
                bool eligible = _cocktail == null || Cocktails.CanTarget(_cocktail.Kind, deck[i]);
                if (_cocktail != null)
                {
                    card.style.opacity = eligible ? 1f : 0.3f;
                    if (_picked.Contains(i))
                    {
                        card.style.translate = new Translate(0, -8);
                        Ui.Border(card, 4, Ui.Gold);
                    }
                    if (eligible) card.RegisterCallback<ClickEvent>(_ => Toggle(index));
                }
                _grid.Add(card);
            }
            if (_cocktail != null) Ui.SetEnabled(_confirm, _run.IsLegal(Input()));
        }

        private void Toggle(int index)
        {
            if (_picked.Contains(index)) _picked.Remove(index);
            else
            {
                if (_picked.Count >= _cocktail.Targets.max) _picked.RemoveAt(0);
                _picked.Add(index);
            }
            Rebuild();
        }

        private RunInput Input() => RunInput.UseCocktail(_slot,
            _picked.Count > 0 ? _picked[0] : -1,
            _picked.Count > 1 ? _picked[1] : -1);

        private void Confirm()
        {
            var input = Input();
            Close();
            InputChosen?.Invoke(input);
        }
    }
}
