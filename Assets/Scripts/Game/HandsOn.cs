using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the things you do with your hands on the felt, not with buttons: grab a lucky chip as it rolls past,
    // drag a card up your sleeve while the security camera looks away, and point a trick at a card
    public sealed class HandsOn
    {
        private readonly VisualElement _root, _camera, _lens, _sleeve;
        private readonly Label _cameraLabel;
        private float _time;
        private VisualElement _chip;
        private string _chipFor;

        // a trick waiting for you to pick its card
        private int _targetSlot = -1;
        private readonly List<(VisualElement view, EventCallback<ClickEvent> click, VisualElement tag)> _targets =
            new List<(VisualElement, EventCallback<ClickEvent>, VisualElement)>();

        public event Action<PlayerAction> ActionChosen;
        public event Action<int, int> TrickPlayed;

        public bool Targeting => _targetSlot >= 0;

        // the camera is looking at your table right now: palming would be seen
        public bool CameraLooking => Mathf.Repeat(_time, CameraCycle) < CameraWatch;
        private const float CameraCycle = 4.6f, CameraWatch = 2.6f;

        public HandsOn(VisualElement root)
        {
            _root = root;

            // a ceiling camera that sweeps the table, red while it's watching you
            _camera = new VisualElement { pickingMode = PickingMode.Position };
            _camera.style.position = Position.Absolute;
            _camera.style.right = 110;
            _camera.style.top = 14;
            _camera.style.alignItems = Align.Center;
            var mount = new VisualElement();
            mount.style.width = 6;
            mount.style.height = 14;
            mount.style.backgroundColor = new Color(0.3f, 0.3f, 0.32f);
            _camera.Add(mount);
            _lens = new VisualElement();
            _lens.style.width = 46;
            _lens.style.height = 26;
            Ui.Rounded(_lens, 8);
            _lens.style.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
            Ui.Border(_lens, 2, new Color(0.45f, 0.46f, 0.5f));
            _lens.style.alignItems = Align.Center;
            _lens.style.justifyContent = Justify.Center;
            _lens.style.transformOrigin = new TransformOrigin(Length.Percent(50), 0);
            var eye = new VisualElement();
            eye.style.width = eye.style.height = 12;
            Ui.Rounded(eye, 6);
            eye.name = "eye";
            _lens.Add(eye);
            _camera.Add(_lens);
            _cameraLabel = Ui.Caption(_camera, "");
            _cameraLabel.style.marginTop = 3;
            Ui.Tip(_camera, "Security camera",
                $"Once a table, right after you Hit, you can drag that card into your sleeve to take it back. " +
                $"Do it while the camera looks away (green) for +{Balance.PalmCleanHeat} Heat. Get seen (red) and it's +{Balance.PalmSeenHeat} Heat.");
            root.Add(_camera);

            // where a palmed card goes
            _sleeve = Ui.Column(root, 2);
            _sleeve.style.position = Position.Absolute;
            _sleeve.style.left = 22;
            _sleeve.style.bottom = 236;
            _sleeve.style.width = 150;
            _sleeve.style.height = 96;
            _sleeve.style.alignItems = Align.Center;
            _sleeve.style.justifyContent = Justify.Center;
            Ui.Rounded(_sleeve, 14);
            Ui.Border(_sleeve, 2, new Color(0.98f, 0.79f, 0.32f, 0.7f));
            _sleeve.style.backgroundColor = new Color(0, 0, 0, 0.45f);
            Ui.Add(_sleeve, new SuitIcon(SuitIcon.Shape.Cards, Ui.Gold, 26));
            Ui.Text(_sleeve, "YOUR SLEEVE", 12, Ui.Gold, Weight.Heavy);
            Ui.Text(_sleeve, "drop the card here", 10, Ui.Dim);
            _sleeve.pickingMode = PickingMode.Ignore;
            Ui.Show(_sleeve, false);

            root.schedule.Execute(Tick).Every(33);
        }

        private void Tick()
        {
            _time += 0.033f;
            bool looking = CameraLooking;
            // it pans across while watching, and swings off to the side while it isn't
            float angle = looking ? 12 * Mathf.Sin(_time * 2.2f) : 48;
            _lens.style.rotate = new Rotate(Settings.ReducedMotion ? 0 : angle);
            var eye = _lens.Q("eye");
            eye.style.backgroundColor = looking ? new Color(1f, 0.25f, 0.2f) : new Color(0.35f, 0.95f, 0.45f);
            _cameraLabel.text = looking ? "WATCHING" : "LOOKING AWAY";
            _cameraLabel.style.color = looking ? Ui.Bad : Ui.Good;
        }

        public void Refresh(Run run, Table table, List<VisualElement> activeCards, bool live)
        {
            EndTarget();
            var legal = live ? table.LegalActions() : new List<PlayerAction>();
            bool canPalm = legal.Contains(PlayerAction.PalmClean);
            Ui.Show(_camera, live && table.Phase == TablePhase.PlayerTurn && !table.PalmUsed);
            if (canPalm && activeCards.Count > 0) Draggable(activeCards[activeCards.Count - 1]);
            RefreshChip(table, legal.Contains(PlayerAction.CatchChip));
        }

        // the lucky chip rolls across the felt once; grab it before it's gone
        private void RefreshChip(Table table, bool rolling)
        {
            string key = rolling ? $"{table.GetHashCode()}:{table.DealsPlayed}" : null;
            if (!rolling)
            {
                _chip?.RemoveFromHierarchy();
                _chip = null;
                _chipFor = null;
                return;
            }
            if (key == _chipFor) return;
            _chipFor = key;
            _chip?.RemoveFromHierarchy();
            var chip = new PokerChip(new Color(0.95f, 0.72f, 0.16f), new Color(0.6f, 0.08f, 0.1f), 52) { pickingMode = PickingMode.Position };
            chip.style.position = Position.Absolute;
            chip.style.top = 352;
            _root.Add(chip);
            _chip = chip;
            Ui.Tip(chip, "Lucky chip!", $"Click it before it rolls away: +{Balance.LuckyChipGold} gold, and +{Balance.LuckyChipMult} Mult if this hand wins.");
            float width = Mathf.Max(600, _root.layout.width);
            float t = 0;
            const float seconds = 3.6f;
            chip.schedule.Execute(() =>
            {
                if (chip.parent == null) return;
                t += 0.016f;
                float k = t / seconds;
                chip.style.left = Mathf.Lerp(-60, width + 10, k);
                chip.style.translate = new Translate(0, -Mathf.Abs(Mathf.Sin(k * Mathf.PI * 5)) * 22 * (1 - k * 0.5f));
                chip.style.rotate = new Rotate(k * 900);
                if (k >= 1) chip.RemoveFromHierarchy();
            }).Every(16);
            chip.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                chip.RemoveFromHierarchy();
                ActionChosen?.Invoke(PlayerAction.CatchChip);
            });
        }

        // your last card can be dragged into your sleeve
        private void Draggable(VisualElement card)
        {
            bool dragging = false;
            Vector2 start = default;
            card.RegisterCallback<PointerDownEvent>(e =>
            {
                dragging = true;
                start = e.position;
                card.CapturePointer(e.pointerId);
                Ui.Show(_sleeve, true);
                Ui.Pop(_sleeve, 1.1f);
            });
            card.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging) return;
                var delta = (Vector2)e.position - start;
                Ui.Transition(card, 0, "translate", "rotate");
                card.style.translate = new Translate(delta.x, delta.y);
                card.style.rotate = new Rotate(Mathf.Clamp(delta.x * 0.05f, -14, 14));
                bool over = _sleeve.worldBound.Contains(e.position);
                _sleeve.style.backgroundColor = over ? new Color(0.98f, 0.79f, 0.32f, 0.3f) : new Color(0, 0, 0, 0.45f);
            });
            card.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging) return;
                dragging = false;
                card.ReleasePointer(e.pointerId);
                Ui.Show(_sleeve, false);
                if (_sleeve.worldBound.Contains(e.position))
                {
                    ActionChosen?.Invoke(CameraLooking ? PlayerAction.PalmSeen : PlayerAction.PalmClean);
                    return;
                }
                Ui.Transition(card, 0.25f, EasingMode.EaseOutBack, "translate", "rotate");
                card.style.translate = new Translate(0, 0);
                card.style.rotate = new Rotate(0);
            });
        }

        // a trick that needs a card: light up the cards it can go on and wait for a click
        public void BeginTarget(int slot, TrickKind kind, Table table, List<VisualElement> activeCards)
        {
            EndTarget();
            _targetSlot = slot;
            for (int i = 0; i < activeCards.Count; i++)
            {
                if (kind == TrickKind.Nudge && !table.CanNudge(i)) continue;
                int index = i;
                var view = activeCards[i];
                CardView.SetSelected(view, true);
                var tag = Ui.Pill(view, kind == TrickKind.Steal ? "SWAP" : "-1", Ui.Gold, filled: true, size: 11);
                tag.style.position = Position.Absolute;
                tag.style.top = -26;
                tag.style.alignSelf = Align.Center;
                tag.pickingMode = PickingMode.Ignore;
                EventCallback<ClickEvent> click = e =>
                {
                    e.StopPropagation();
                    int chosen = _targetSlot;
                    EndTarget();
                    TrickPlayed?.Invoke(chosen, index);
                };
                view.RegisterCallback(click);
                _targets.Add((view, click, tag));
            }
        }

        public void EndTarget()
        {
            foreach (var (view, click, tag) in _targets)
            {
                view.UnregisterCallback(click);
                tag.RemoveFromHierarchy();
                CardView.SetSelected(view, false);
            }
            _targets.Clear();
            _targetSlot = -1;
        }
    }
}
