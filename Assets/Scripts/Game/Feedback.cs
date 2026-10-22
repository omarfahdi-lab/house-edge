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

    // sounds, the score popup, particles, screen shake and the security alarm. all made in code
    public sealed class Feedback : MonoBehaviour
    {
        private AudioSource _audio;
        private AudioClip _card, _chip, _win, _lose, _coin, _alarm, _bust, _blackjack, _cleared, _clink, _trombone, _fanfare, _boom;
        private VisualElement _root, _flash, _popup, _chipsBox, _multBox;
        private Label _chips, _mult, _times, _total;
        private Particles _particles;
        private Sunburst _rays;
        private bool _raysOn;
        private float _clinkCooldown;
        private float _popupTime = -1, _shake, _flashTime, _flashStrength;
        private Color _flashColor;
        private long _popupTotal;

        // where coins burst from when you're paid: the gold counter in the sidebar, in world space
        public Func<Vector2> GoldAnchor;
        // the dealer's cards and yours, in world space, for chips sliding across the table
        public Func<Vector2> DealerAnchor, PlayerAnchor;

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
            _cleared = Synth.Tones(new[] { 392f, 523f, 659f, 784f, 1047f }, 0.1f, 0.35f);
            _lose = Synth.Tones(new[] { 196f, 147f }, 0.16f, 0.3f);
            _bust = Synth.Tones(new[] { 110f }, 0.25f, 0.3f);
            _alarm = Synth.Siren(1.2f, 0.25f);
            _clink = Synth.Tones(new[] { 3400f }, 0.05f, 0.12f);
            _trombone = Synth.Brass(new[] { 196f, 185f, 175f, 165f }, new[] { 0.42f, 0.42f, 0.42f, 1.3f }, 0.3f);
            _fanfare = Synth.Brass(new[] { 392f, 523f, 659f, 784f, 659f, 1047f }, new[] { 0.14f, 0.14f, 0.14f, 0.3f, 0.14f, 0.9f }, 0.26f);
            _boom = Synth.Boom(0.7f, 0.35f);

            _flash = new VisualElement();
            Ui.Fill(_flash);
            _flash.pickingMode = PickingMode.Ignore;
            _flash.style.backgroundColor = new Color(0, 0, 0, 0);
            root.Add(_flash);

            // light rays behind a big hand
            _rays = new Sunburst(new Color(0.98f, 0.79f, 0.32f, 0.16f), 620);
            _rays.style.top = 300 - 310;
            _rays.style.opacity = 0;
            root.Add(_rays);

            // Chips x Mult = total, in the middle of the table
            _popup = Ui.Column(root, 6);
            _popup.pickingMode = PickingMode.Ignore;
            _popup.style.position = Position.Absolute;
            _popup.style.left = Hud.Width;
            _popup.style.right = 0;
            _popup.style.top = 236;
            _popup.style.alignItems = Align.Center;
            var equation = Ui.Row(_popup, 10);
            (_chipsBox, _chips) = ScoreBox(equation, Ui.Chips, "CHIPS");
            _times = Ui.Title(equation, "×", 34, Ui.Cream);
            (_multBox, _mult) = ScoreBox(equation, Ui.Mult, "MULT");
            _total = Ui.Title(_popup, "", 58, Ui.Gold);
            _total.style.textShadow = new TextShadow { offset = new Vector2(0, 4), blurRadius = 18, color = new Color(0, 0, 0, 0.9f) };
            foreach (var e in _popup.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            Ui.Show(_popup, false);

            _particles = new Particles();
            root.Add(_particles);
        }

        private static (VisualElement, Label) ScoreBox(VisualElement parent, Color color, string name)
        {
            var box = Ui.Column(parent, 0);
            box.style.alignItems = Align.Center;
            box.style.minWidth = 104;
            Ui.Padding(box, 6, 14);
            Ui.Rounded(box, 12);
            box.style.backgroundImage = Ui.Gradient(color, Color.Lerp(color, Color.black, 0.45f));
            box.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Border(box, 2, Color.Lerp(color, Color.white, 0.4f));
            box.style.borderBottomWidth = 4;
            box.style.borderBottomColor = new Color(0, 0, 0, 0.4f);
            var value = Ui.Text(box, "", 30, Color.white, Weight.Heavy);
            Ui.Text(box, name, 10, new Color(1, 1, 1, 0.75f), Weight.Heavy).style.letterSpacing = 2;
            return (box, value);
        }

        public void Reset(Run run)
        {
            _popupTime = -1;
            _raysOn = false;
            Ui.Show(_popup, false);
        }

        public void After(Snapshot before, Run run)
        {
            var table = run.ActiveTable;
            // left the table: clear any popup still floating
            if (run.Phase != RunPhase.AtTable && table == run.Table && before.Phase != RunPhase.AtTable) Reset(run);
            int cards = table.PlayerHands.Sum(h => h.Cards.Count) + table.DealerCards.Count + table.SecondDealerCards.Count;
            if (table == before.Table && cards > before.Cards || table != before.Table && cards > 0) Play(_card);

            bool scored = table.LastScores.Count > 0 && (table != before.Table || table.DealsPlayed != before.Deals || before.Scored == 0);
            if (scored && table.LastScores.Any(s => s.Outcome != Outcome.Surrender)) Celebrate(run, table);

            // the table was just beaten: the shop opens with a shower of coins
            bool cleared = before.Phase == RunPhase.AtTable && (run.Phase == RunPhase.Shop || run.Phase == RunPhase.BossReward) && before.Table == run.Table;
            if (cleared)
            {
                Play(_cleared);
                _particles.Burst(new Vector2(TableCenterX, 120), 70, 640);
                Flash(new Color(0.98f, 0.79f, 0.32f), 0.5f, 0.14f);
            }
            if (run.Phase == RunPhase.Won && before.Phase != RunPhase.Won) Fireworks();
            if (run.Phase == RunPhase.Lost && before.Phase != RunPhase.Lost)
            {
                // the house wins: cards rain down and a sad trombone plays
                _particles.CardRain(_root.layout.width, 12);
                StartCoroutine(After(0.5f, () => Play(_trombone, steady: true)));
                Flash(new Color(0.5f, 0, 0), 1.2f, 0.14f);
            }

            if (run.Gambler.Gold > before.Gold)
            {
                if (!scored && !cleared) Play(_coin);
                if (GoldAnchor != null) _particles.Coins(_root.WorldToLocal(GoldAnchor()), Mathf.Clamp(run.Gambler.Gold - before.Gold, 3, 18), 260);
            }
            if (run.Gambler.Gold < before.Gold && before.Phase != RunPhase.AtTable) Play(_chip);

            // security
            if (run.AtEyeInTheSky && !before.Eye || run.Gambler.Heat >= Balance.MaxHeat && before.Heat < Balance.MaxHeat)
            {
                Play(_alarm);
                Flash(new Color(0.9f, 0.05f, 0.05f), 1.2f, 0.3f, pulse: true);
            }
        }

        private void Celebrate(Run run, Table table)
        {
            long total = table.LastScores.Sum(s => s.Total);
            var best = table.LastScores.OrderByDescending(s => s.Total).First();
            bool big = best.Outcome == Outcome.Blackjack || best.Outcome == Outcome.Charlie;
            if (big) Play(_blackjack);
            else if (total > 0) Play(_win);
            else if (best.Outcome == Outcome.Bust) Play(_bust);
            else Play(_lose);

            if (best.Outcome == Outcome.Bust || total <= 0 && best.Outcome != Outcome.Push)
            {
                _shake = 0.12f;
                Flash(new Color(0.9f, 0.1f, 0.1f), 0.3f, 0.07f);
            }

            // the pot slides across the felt: to you when you win, to the dealer when you don't
            if (DealerAnchor != null && PlayerAnchor != null && run.ActiveTable == table)
            {
                var dealer = _root.WorldToLocal(DealerAnchor());
                var player = _root.WorldToLocal(PlayerAnchor());
                var chipColor = new[] { new Color(0.8f, 0.14f, 0.16f), new Color(0.13f, 0.5f, 0.3f), new Color(0.13f, 0.13f, 0.15f) };
                var color = chipColor[table.DealsPlayed % chipColor.Length];
                bool won = table.LastScores.Any(s => s.Outcome.IsWin());
                bool lost = table.LastScores.All(s => s.Outcome == Outcome.Lose || s.Outcome == Outcome.Bust);
                float pot = total / (float)Math.Max(1, table.Target);
                if (won) _particles.Chips(dealer, player, Mathf.Clamp(4 + (int)(pot * 12), 4, 14), color, Clink, 0.75f);
                else if (lost) _particles.Chips(player, dealer, 4, color, Clink, 0.75f);
            }

            if (total <= 0 || run.ActiveTable != table) return;
            _popupTotal = total;
            _popupTime = 0;
            bool split = table.LastScores.Count > 1;
            _chips.text = split ? table.LastScores.Sum(s => s.Chips).ToString() : best.Chips.ToString();
            _mult.text = split ? "split" : best.Mult.ToString("0.##");
            _total.text = "";
            Ui.Show(_popup, true);
            _popup.style.opacity = 1;
            _popup.style.translate = new Translate(0, 0);
            Ui.Pop(_chipsBox, 1.5f, 0);
            Ui.Pop(_times, 1.6f, 120);
            Ui.Pop(_multBox, 1.5f, 220);
            _raysOn = big || total / (float)Math.Max(1, table.Target) >= 0.5f;

            var center = new Vector2(TableCenterX, 300);
            float share = total / (float)Math.Max(1, table.Target);
            _particles.Coins(center, Mathf.Clamp((int)(share * 40), 6, 40));
            if (big || share >= 0.5f) _particles.Burst(center, big ? 60 : 36);
            if (big) Flash(new Color(0.98f, 0.79f, 0.32f), 0.4f, 0.12f);
            // a huge hand shakes the table
            if (share >= 0.5f) _shake = Mathf.Min(0.5f, 0.15f + share * 0.1f);
        }

        private void Clink()
        {
            if (_clinkCooldown > 0) return;
            _clinkCooldown = 0.05f;
            Play(_clink);
        }

        // bursts all over the screen for a few seconds
        private void Fireworks()
        {
            Play(_fanfare, steady: true);
            var random = new System.Random();
            for (int i = 0; i < 9; i++)
            {
                float at = i * 0.45f;
                var spot = new Vector2((float)(0.15 + random.NextDouble() * 0.7) * _root.layout.width, (float)(0.15 + random.NextDouble() * 0.4) * _root.layout.height);
                StartCoroutine(After(at, () =>
                {
                    _particles.Burst(spot, 45, 560);
                    _particles.Coins(spot, 10, 380);
                    Play(_boom);
                }));
            }
        }

        private static System.Collections.IEnumerator After(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        private float TableCenterX => Hud.Width + (_root.layout.width - Hud.Width) / 2f;

        private void Flash(Color color, float seconds, float strength, bool pulse = false)
        {
            _flashColor = color;
            _flashTime = seconds;
            _flashStrength = strength;
            _flashPulse = pulse;
        }

        private bool _flashPulse;

        public bool Muted
        {
            get => PlayerPrefs.GetInt("muted", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("muted", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // steady keeps the pitch, for melodies
        private void Play(AudioClip clip, bool steady = false)
        {
            if (clip == null || Muted) return;
            _audio.pitch = steady ? 1 : UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(clip);
        }

        private void Update()
        {
            _clinkCooldown -= Time.deltaTime;
            float raysTarget = _raysOn && _popupTime >= 0 ? 1 : 0;
            float raysAlpha = Mathf.MoveTowards(_rays.resolvedStyle.opacity, raysTarget * (_popupTime > 1.7f ? 0 : 1), Time.deltaTime * 3);
            _rays.style.opacity = raysAlpha;
            if (raysAlpha > 0)
            {
                _rays.style.left = TableCenterX - 310;
                _rays.style.rotate = new Rotate(Time.time * 25);
            }

            if (_popupTime >= 0)
            {
                _popupTime += Time.deltaTime;
                // the boxes pop in, the total counts up, holds, then floats away
                float ramp = Mathf.Clamp01((_popupTime - 0.3f) / 0.6f);
                if (_popupTime >= 0.3f)
                    _total.text = "+" + ((long)(_popupTotal * (1 - Mathf.Pow(1 - ramp, 3)))).ToString("N0");
                float fade = Mathf.Clamp01((_popupTime - 1.7f) / 0.4f);
                _popup.style.opacity = 1 - fade;
                _popup.style.translate = new Translate(0, -40 * fade);
                if (_popupTime > 2.1f)
                {
                    _popupTime = -1;
                    Ui.Show(_popup, false);
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
                // the security alarm pulses, everything else is a soft glow that fades
                float pulse = _flashPulse && _flashTime > 0.5f ? Mathf.Abs(Mathf.Sin(_flashTime * 10)) : 1;
                float alpha = _flashStrength * pulse * Mathf.Clamp01(_flashTime / 0.3f);
                _flash.style.backgroundColor = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);
                if (_flashTime <= 0) _flash.style.backgroundColor = new Color(0, 0, 0, 0);
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

        // a buzzy brass tone with vibrato: trombones and fanfares
        public static AudioClip Brass(float[] notes, float[] lengths, float volume)
        {
            int total = 0;
            foreach (float l in lengths) total += (int)(Rate * l);
            var data = new float[total];
            int offset = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                int count = (int)(Rate * lengths[n]);
                float phase = 0;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)Rate;
                    // vibrato grows on long notes
                    float vibrato = 1 + 0.012f * Mathf.Sin(2 * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t / 0.3f);
                    phase += 2 * Mathf.PI * notes[n] * vibrato / Rate;
                    float tone = 0;
                    for (int h = 1; h <= 6; h++) tone += Mathf.Sin(phase * h) / h;
                    float env = Mathf.Min(1, t / 0.03f) * Mathf.Min(1, (lengths[n] - t) / 0.06f);
                    data[offset + i] = tone * env * volume * 0.6f;
                }
                offset += count;
            }
            return Clip("brass", data);
        }

        // a low thump with a tail of noise, for fireworks
        public static AudioClip Boom(float length, float volume)
        {
            int count = (int)(Rate * length);
            var data = new float[count];
            var random = new System.Random(3);
            float smooth = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                smooth += ((float)(random.NextDouble() * 2 - 1) - smooth) * 0.08f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * (60 + 80 * Mathf.Exp(-t / 0.05f)) * t) * Mathf.Exp(-t / 0.15f) + smooth * 2 * Mathf.Exp(-t / 0.3f)) * volume;
            }
            return Clip("boom", data);
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
