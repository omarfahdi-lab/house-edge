using System;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // what changed between two moments of a run, so Feedback knows what to celebrate
    public readonly struct Snapshot
    {
        public readonly Table Table;
        public readonly int Deals;
        public readonly int Scored;
        public readonly int Gold;
        public readonly int Heat;
        public readonly int Cards;
        public readonly RunPhase Phase;
        public readonly bool Eye;

        private Snapshot(Run run)
        {
            Table = run.ActiveTable;
            Deals = Table.DealsPlayed;
            Scored = Table.LastScores.Count;
            Gold = run.Gambler.Gold;
            Heat = run.Gambler.Heat;
            Cards = Table.PlayerHands.Sum(h => h.Cards.Count) + Table.DealerCards.Count + Table.SecondDealerCards.Count;
            Phase = run.Phase;
            Eye = run.AtEyeInTheSky;
        }

        public static Snapshot Of(Run run) => new Snapshot(run);
    }

    // sounds, the score popup, screen shake and the security alarm. all made in code
    public sealed class Feedback : MonoBehaviour
    {
        private AudioSource _audio;
        private AudioClip _card, _chip, _win, _lose, _coin, _alarm, _bust, _blackjack;
        private VisualElement _root, _flash;
        private Label _popup, _popupDetail;
        private float _popupTime = -1, _shake, _flashTime;
        private long _popupTotal;

        public static Feedback Create(Transform parent, VisualElement root)
        {
            var feedback = new GameObject("Feedback").AddComponent<Feedback>();
            feedback.transform.SetParent(parent, false);
            feedback.Build(root);
            return feedback;
        }

        private void Build(VisualElement root)
        {
            _root = root;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _card = Synth.Noise(0.05f, 0.25f);
            _chip = Synth.Tones(new[] { 2200f, 3100f }, 0.07f, 0.25f);
            _coin = Synth.Tones(new[] { 1320f, 1760f }, 0.06f, 0.2f);
            _win = Synth.Tones(new[] { 523f, 659f, 784f }, 0.09f, 0.3f);
            _blackjack = Synth.Tones(new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.35f);
            _lose = Synth.Tones(new[] { 196f, 147f }, 0.16f, 0.3f);
            _bust = Synth.Tones(new[] { 110f }, 0.25f, 0.3f);
            _alarm = Synth.Siren(1.2f, 0.25f);

            _flash = new VisualElement();
            Ui.Fill(_flash);
            _flash.pickingMode = PickingMode.Ignore;
            _flash.style.backgroundColor = new Color(0.9f, 0.05f, 0.05f, 0);
            root.Add(_flash);

            var popup = Ui.Column(root);
            popup.pickingMode = PickingMode.Ignore;
            popup.style.position = Position.Absolute;
            popup.style.left = Hud.Width;
            popup.style.right = 0;
            popup.style.top = 250;
            popup.style.alignItems = Align.Center;
            _popup = Ui.Text(popup, "", 54, Ui.Gold, bold: true);
            _popupDetail = Ui.Text(popup, "", 18, Ui.Cream);
            _popup.pickingMode = _popupDetail.pickingMode = PickingMode.Ignore;
            Ui.Show(_popup.parent, false);
        }

        public void Reset(Run run)
        {
            _popupTime = -1;
            Ui.Show(_popup.parent, false);
        }

        public void After(Snapshot before, Run run)
        {
            var table = run.ActiveTable;
            int cards = table.PlayerHands.Sum(h => h.Cards.Count) + table.DealerCards.Count + table.SecondDealerCards.Count;
            if (table == before.Table && cards > before.Cards || table != before.Table && cards > 0) Play(_card);

            bool scored = table.LastScores.Count > 0 && (table != before.Table || table.DealsPlayed != before.Deals || before.Scored == 0);
            if (scored && table.LastScores.Any(s => s.Outcome != Outcome.Surrender)) Celebrate(run, table);

            if (run.Gambler.Gold > before.Gold && !scored) Play(_coin);
            if (run.Gambler.Gold < before.Gold && before.Phase != RunPhase.AtTable) Play(_chip);

            // security
            if (run.AtEyeInTheSky && !before.Eye || run.Gambler.Heat >= Balance.MaxHeat && before.Heat < Balance.MaxHeat)
            {
                Play(_alarm);
                _flashTime = 1.2f;
            }
        }

        private void Celebrate(Run run, Table table)
        {
            long total = table.LastScores.Sum(s => s.Total);
            var best = table.LastScores.OrderByDescending(s => s.Total).First();
            if (best.Outcome == Outcome.Blackjack || best.Outcome == Outcome.Charlie) Play(_blackjack);
            else if (total > 0) Play(_win);
            else if (best.Outcome == Outcome.Bust) Play(_bust);
            else Play(_lose);

            if (total <= 0) return;
            _popupTotal = total;
            _popupTime = 0;
            _popupDetail.text = table.LastScores.Count == 1 ? $"{best.Chips} Chips x {best.Mult:0.##} Mult" : "split hands";
            Ui.Show(_popup.parent, true);
            // a huge hand shakes the table
            if (total >= table.Target / 2) _shake = Mathf.Min(0.5f, 0.15f + total / (float)table.Target * 0.1f);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            _audio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(clip);
        }

        private void Update()
        {
            if (_popupTime >= 0)
            {
                _popupTime += Time.deltaTime;
                // count up, hold, then float away
                float ramp = Mathf.Clamp01(_popupTime / 0.5f);
                _popup.text = "+" + ((long)(_popupTotal * ramp)).ToString("N0");
                float fade = Mathf.Clamp01((_popupTime - 1.2f) / 0.4f);
                _popup.parent.style.opacity = 1 - fade;
                _popup.parent.style.translate = new Translate(0, -30 * fade);
                if (_popupTime > 1.6f)
                {
                    _popupTime = -1;
                    Ui.Show(_popup.parent, false);
                }
            }

            if (_shake > 0)
            {
                _shake -= Time.deltaTime;
                float strength = Mathf.Max(0, _shake) * 30;
                _root.style.translate = new Translate(UnityEngine.Random.Range(-strength, strength), UnityEngine.Random.Range(-strength, strength));
                if (_shake <= 0) _root.style.translate = new Translate(0, 0);
            }

            if (_flashTime > 0)
            {
                _flashTime -= Time.deltaTime;
                float pulse = Mathf.Abs(Mathf.Sin(_flashTime * 10)) * 0.35f * Mathf.Clamp01(_flashTime);
                _flash.style.backgroundColor = new Color(0.9f, 0.05f, 0.05f, pulse);
            }
        }
    }

    // tiny procedural sounds, so the game ships without audio files
    public static class Synth
    {
        private const int Rate = 22050;

        public static AudioClip Tones(float[] notes, float noteLength, float volume)
        {
            int perNote = (int)(Rate * noteLength);
            var data = new float[perNote * notes.Length];
            for (int n = 0; n < notes.Length; n++)
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)Rate;
                    float envelope = Mathf.Exp(-6f * i / perNote);
                    data[n * perNote + i] = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * envelope * volume;
                }
            return Clip("tones", data);
        }

        public static AudioClip Noise(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            var random = new System.Random(7);
            for (int i = 0; i < count; i++)
                data[i] = (float)(random.NextDouble() * 2 - 1) * volume * (1 - i / (float)count);
            return Clip("noise", data);
        }

        public static AudioClip Siren(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float frequency = 600 + 300 * Mathf.Sin(2 * Mathf.PI * 2.5f * t);
                phase += 2 * Mathf.PI * frequency / Rate;
                data[i] = Mathf.Sign(Mathf.Sin(phase)) * volume * 0.5f * (1 - t / length);
            }
            return Clip("siren", data);
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
