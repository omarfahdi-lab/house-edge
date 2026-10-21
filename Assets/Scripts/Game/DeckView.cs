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
            _canPick = null;
            _picked.Clear();
            _title.text = $"YOUR DECK  ({run.Gambler.Deck.Count} cards)";
            _hint.text = "The dealer draws from this deck too.";
            Ui.Show(_confirm, false);
            Rebuild();
            Ui.Show(_root, true);
        }

        public void PickFor(Run run, int slot)
        {
            var cocktail = run.Gambler.Cocktails[slot];
            var (min, max) = cocktail.Targets;
            Pick(run, cocktail.Name, $"{cocktail.Description}  Pick {(min == max ? $"{max}" : $"{min} to {max}")} card{(max > 1 ? "s" : "")}.",
                max, card => Cocktails.CanTarget(cocktail.Kind, card),
                picked => RunInput.UseCocktail(slot, picked.Count > 0 ? picked[0] : -1, picked.Count > 1 ? picked[1] : -1));
        }

        public void Pick(Run run, string title, string hint, int max, Func<Card, bool> canPick, Func<List<int>, RunInput> makeInput)
        {
            _run = run;
            _canPick = canPick;
            _makeInput = makeInput;
            _max = max;
            _picked.Clear();
            _title.text = title.ToUpperInvariant();
            _hint.text = hint;
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
                bool eligible = _canPick == null || _canPick(deck[i]);
                if (_canPick != null)
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
            if (_canPick != null) Ui.SetEnabled(_confirm, _run.IsLegal(_makeInput(_picked)));
        }

        private void Toggle(int index)
        {
            if (_picked.Contains(index)) _picked.Remove(index);
            else
            {
                if (_picked.Count >= _max) _picked.RemoveAt(0);
                _picked.Add(index);
            }
            Rebuild();
        }

        private void Confirm()
        {
            var input = _makeInput(_picked);
            Close();
            InputChosen?.Invoke(input);
        }
    }
}
