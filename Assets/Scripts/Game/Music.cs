using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseEdge.Game
{
    public enum Track { None, Lounge, Table, Boss }

    // background music: three recorded tracks by Kevin MacLeod (incompetech.com, CC BY 4.0) crossfaded by
    // situation, with a heartbeat on your last Hand. if a track is missing, the synthesized loops stand in
    public sealed class Music : MonoBehaviour
    {
        private const float Volume = 0.34f;
        private const float FadeSeconds = 1.2f;

        private AudioSource _a, _b, _heart;
        private AudioClip _lounge, _table, _boss;
        private float _pitch = 1, _heartTarget;
        private Track _track = Track.None;
        private bool _enabled = true;

        public static Music Create(Transform parent)
        {
            var music = new GameObject("Music").AddComponent<Music>();
            music.transform.SetParent(parent, false);
            music._a = music.Source();
            music._b = music.Source();
            music._heart = music.Source();
            music._enabled = PlayerPrefs.GetInt("music", 1) == 1;
            return music;
        }

        private AudioSource Source()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0;
            return source;
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PlayerPrefs.SetInt("music", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // muted with M as well as the music switch
        public bool Muted { get; set; }

        public void Play(Track track)
        {
            if (track == _track) return;
            _track = track;
            if (track == Track.None) return;
            var clip = track switch
            {
                Track.Boss => _boss ??= Load("Hitman") ?? MusicSynth.Boss(),
                Track.Table => _table ??= Load("Bossa_Antigua") ?? MusicSynth.Table(),
                _ => _lounge ??= Load("Lobby_Time") ?? MusicSynth.Lounge(),
            };
            // the quiet source takes the new track and fades up while the other fades out
            var (incoming, outgoing) = _a.volume <= _b.volume ? (_a, _b) : (_b, _a);
            // setting the clip rewinds it. reading or setting time before a WebGL clip has loaded only warns
            incoming.clip = clip;
            incoming.Play();
            _incoming = incoming;
            _outgoing = outgoing;
        }

        private AudioSource _incoming, _outgoing;

        private static AudioClip Load(string name) => Resources.Load<AudioClip>("Music/" + name);

        // who made the music, for the menu
        public const string Credit = "Music: \"Lobby Time\", \"Bossa Antigua\", \"Hitman\" by Kevin MacLeod (incompetech.com), CC BY 4.0  ·  Portraits and photos: public domain and Wikimedia Commons, see docs/CREDITS.md";

        // tension: 0 is relaxed, 1 is your last Hand. the band speeds up and a heartbeat comes in
        public void SetTension(float tension)
        {
            // recorded music only leans in a little before it sounds sped up
            _pitch = 1 + 0.03f * tension;
            _heartTarget = tension >= 1 ? 0.55f : 0;
            if (_heartTarget > 0 && !_heart.isPlaying)
            {
                _heart.clip = MusicSynth.Heartbeat();
                _heart.Play();
            }
        }

        private void Update()
        {
            float target = _enabled && !Muted && _track != Track.None ? Volume : 0;
            float step = Time.unscaledDeltaTime / FadeSeconds * Volume;
            if (_incoming != null)
            {
                _incoming.volume = Mathf.MoveTowards(_incoming.volume, target, step);
                _incoming.pitch = Mathf.MoveTowards(_incoming.pitch, _pitch, Time.unscaledDeltaTime * 0.05f);
            }
            float heart = _enabled && !Muted && _track != Track.None ? _heartTarget : 0;
            _heart.volume = Mathf.MoveTowards(_heart.volume, heart, Time.unscaledDeltaTime * 0.6f);
            if (_heart.volume <= 0 && _heart.isPlaying && heart <= 0) _heart.Stop();
            if (_outgoing != null)
            {
                _outgoing.volume = Mathf.MoveTowards(_outgoing.volume, 0, step);
                if (_outgoing.volume <= 0 && _outgoing.isPlaying) _outgoing.Stop();
            }
        }
    }

    // a tiny jazz band in code: walking bass, electric piano, vibraphone and ride cymbal
    public static class MusicSynth
    {
        private const int Rate = 22050;

        private sealed class Song
        {
            public float Tempo;
            public bool Swing;
            // one chord per bar: root (MIDI) and chord tones above it in semitones
            public (int root, int[] tones)[] Bars;
            public int Seed;
            public bool Driving;
            // busier comping and a brushed snare on 2 and 4
            public bool Busy;
        }

        private static readonly int[] Min7 = { 0, 3, 7, 10, 14 };
        private static readonly int[] Dom7 = { 0, 4, 7, 10, 14 };
        private static readonly int[] Maj7 = { 0, 4, 7, 11, 14 };
        private static readonly int[] Alt7 = { 0, 4, 7, 10, 13 };
        private static readonly int[] MinAdd9 = { 0, 3, 7, 14, 15 };

        // a slow ii-V-I in C with a turnaround: smoky lounge
        public static AudioClip Lounge() => Render("lounge", new Song
        {
            Tempo = 92,
            Swing = true,
            Seed = 5,
            Bars = new[]
            {
                (38, Min7), (43, Dom7), (36, Maj7), (45, Alt7),
                (38, Min7), (43, Dom7), (40, Min7), (45, Alt7),
                (38, Min7), (43, Dom7), (36, Maj7), (41, Maj7),
                (40, Min7), (45, Alt7), (38, Min7), (43, Alt7),
            },
        });

        // a minor blues at a brisk swing: chips on the table
        public static AudioClip Table() => Render("table", new Song
        {
            Tempo = 118,
            Swing = true,
            Seed = 21,
            Busy = true,
            Bars = new[]
            {
                (36, Min7), (36, Min7), (41, Min7), (41, Min7),
                (36, Min7), (44, Dom7), (43, Alt7), (43, Alt7),
                (36, Min7), (36, Min7), (41, Min7), (41, Min7),
                (44, Dom7), (43, Alt7), (36, Min7), (43, Alt7),
            },
        });

        // C minor, faster and straight: the Pit Boss is watching
        public static AudioClip Boss() => Render("boss", new Song
        {
            Tempo = 132,
            Swing = false,
            Seed = 9,
            Driving = true,
            Busy = true,
            Bars = new[]
            {
                (36, MinAdd9), (36, MinAdd9), (44, Dom7), (43, Alt7),
                (36, MinAdd9), (36, MinAdd9), (41, Min7), (43, Alt7),
            },
        });

        private static AudioClip Render(string name, Song song)
        {
            float beat = 60f / song.Tempo;
            int bars = song.Bars.Length;
            int length = (int)(bars * 4 * beat * Rate);
            var mix = new float[length];
            var wet = new float[length];
            var random = new System.Random(song.Seed);

            float At(int bar, float beatInBar)
            {
                // swung eighths: the "and" lands two thirds of the way through the beat
                float whole = Mathf.Floor(beatInBar);
                float frac = beatInBar - whole;
                if (song.Swing && Mathf.Abs(frac - 0.5f) < 0.01f) frac = 0.66f;
                return (bar * 4 + whole + frac) * beat;
            }

            for (int bar = 0; bar < bars; bar++)
            {
                var (root, tones) = song.Bars[bar];
                int nextRoot = song.Bars[(bar + 1) % bars].root;

                // bass: walking quarters, or a driving eighth-note pulse for the boss
                if (song.Driving)
                {
                    for (int e = 0; e < 8; e++)
                    {
                        int note = root - 12 + (e == 3 || e == 7 ? 12 : 0);
                        Bass(mix, At(bar, e * 0.5f), beat * 0.45f, Midi(note), 0.24f);
                    }
                }
                else
                {
                    int[] walk = { root, root + tones[1], root + tones[2], nextRoot + (random.Next(2) == 0 ? -1 : 1) };
                    for (int b = 0; b < 4; b++) Bass(mix, At(bar, b), beat * 0.95f, Midi(walk[b]), 0.22f);
                }

                // piano: rootless voicings, comped on a couple of syncopated hits
                var voicing = new List<int>();
                foreach (int t in new[] { tones[1], tones[3], tones[4] })
                {
                    int note = root + t;
                    while (note < 58) note += 12;
                    while (note > 72) note -= 12;
                    voicing.Add(note);
                }
                float[][] comps = song.Busy && !song.Driving
                    ? new[] { new[] { 0f, 0.6f }, new[] { 1.5f, 0.4f }, new[] { 2.5f, 0.6f }, new[] { 3.5f, 0.4f } }
                    : song.Driving
                    ? new[] { new[] { 0.5f, 0.4f }, new[] { 1.5f, 0.4f }, new[] { 2.5f, 0.4f }, new[] { 3.5f, 0.4f } }
                    : bar % 2 == 0
                        ? new[] { new[] { 0f, 1.4f }, new[] { 2.5f, 1.2f } }
                        : new[] { new[] { 1.5f, 1.6f }, new[] { 3.5f, 0.5f } };
                foreach (var comp in comps)
                    foreach (int note in voicing)
                        Piano(mix, wet, At(bar, comp[0]) + (float)random.NextDouble() * 0.012f, beat * comp[1], Midi(note), song.Driving ? 0.05f : 0.06f);

                // vibraphone: a loose melody from the chord tones, phrased in two-bar sentences with space
                if (bar % 2 == 0 || random.Next(3) > 0)
                {
                    int count = 2 + random.Next(4);
                    float position = random.Next(2) * 0.5f;
                    for (int n = 0; n < count && position < 4; n++)
                    {
                        int tone = tones[random.Next(tones.Length)];
                        int note = root + tone + 24;
                        while (note < 67) note += 12;
                        while (note > 84) note -= 12;
                        float length8 = 0.5f * (1 + random.Next(3));
                        Vibes(mix, wet, At(bar, position), beat * length8 * 1.6f, Midi(note), 0.075f);
                        position += length8;
                    }
                }

                // drums: ride pattern, hi-hat on 2 and 4, a feathered kick
                float[] ride = song.Driving ? new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f } : new[] { 0f, 1f, 1.5f, 2f, 3f, 3.5f };
                foreach (float r in ride) Ride(mix, At(bar, r), song.Driving ? 0.03f : 0.035f, random);
                Hat(mix, At(bar, 1), random);
                Hat(mix, At(bar, 3), random);
                if (song.Busy)
                {
                    Snare(mix, At(bar, 1), song.Driving ? 0.09f : 0.05f, random);
                    Snare(mix, At(bar, 3), song.Driving ? 0.09f : 0.05f, random);
                }
                // a tom fill into every fourth bar of the boss track
                if (song.Driving && bar % 4 == 3)
                    for (int f = 0; f < 4; f++) Tom(mix, At(bar, 3 + f * 0.25f), 150 - f * 22);
                for (int b = 0; b < 4; b++)
                    if (song.Driving || b % 2 == 0) Kick(mix, At(bar, b), song.Driving ? 0.12f : 0.06f);
            }

            // a little room: two feedback delays on the piano and vibes, wrapped so the loop is seamless
            Echo(wet, 0.113f, 0.35f);
            Echo(wet, 0.171f, 0.3f);
            for (int i = 0; i < length; i++) mix[i] += wet[i] * 0.5f;

            float peak = 0.0001f;
            foreach (float s in mix) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = 0.9f / peak;
            for (int i = 0; i < length; i++) mix[i] = (float)Math.Tanh(mix[i] * gain);

            var clip = AudioClip.Create(name, length, 1, Rate, false);
            clip.SetData(mix, 0);
            return clip;
        }

        private static float Midi(int note) => 440f * Mathf.Pow(2, (note - 69) / 12f);

        // adds a note into the buffer, wrapping past the end so tails ring into the loop's start
        private static void Note(float[] buffer, float start, float seconds, Func<float, float> sample)
        {
            int from = (int)(start * Rate);
            int count = (int)(seconds * Rate);
            for (int i = 0; i < count; i++)
            {
                int index = (from + i) % buffer.Length;
                if (index < 0) index += buffer.Length;
                buffer[index] += sample(i / (float)Rate);
            }
        }

        private static float Release(float t, float length, float fade = 0.04f) => t > length - fade ? Mathf.Max(0, (length - t) / fade) : 1;

        private static void Bass(float[] mix, float start, float length, float f, float amp) =>
            Note(mix, start, length, t =>
            {
                float env = Mathf.Min(1, t / 0.006f) * Mathf.Exp(-t / 0.45f) * Release(t, length);
                float w = 2 * Mathf.PI * f * t;
                return (Mathf.Sin(w) + 0.35f * Mathf.Sin(2 * w) + 0.12f * Mathf.Sin(3 * w)) * env * amp;
            });

        // an electric piano: two-operator FM with a bright attack that mellows
        private static void Piano(float[] mix, float[] wet, float start, float length, float f, float amp)
        {
            float total = length + 0.4f;
            Note(mix, start, total, t =>
            {
                float env = Mathf.Min(1, t / 0.004f) * Mathf.Exp(-t / 1.1f) * Release(t, total, 0.35f);
                float index = 1.4f * Mathf.Exp(-t / 0.18f) + 0.25f;
                float w = 2 * Mathf.PI * f * t;
                float tremolo = 1 + 0.08f * Mathf.Sin(2 * Mathf.PI * 4.5f * t);
                return Mathf.Sin(w + index * Mathf.Sin(w)) * env * amp * tremolo;
            });
            Note(wet, start, total, t => Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / 0.9f) * amp * 0.5f * Release(t, total, 0.35f));
        }

        private static void Vibes(float[] mix, float[] wet, float start, float length, float f, float amp)
        {
            Func<float, float> voice = t =>
            {
                float env = Mathf.Min(1, t / 0.003f) * Mathf.Exp(-t / 0.9f) * Release(t, length, 0.2f);
                float w = 2 * Mathf.PI * f * t;
                float tremolo = 1 + 0.22f * Mathf.Sin(2 * Mathf.PI * 5.5f * t);
                return (Mathf.Sin(w) + 0.25f * Mathf.Sin(4 * w) * Mathf.Exp(-t / 0.08f)) * env * amp * tremolo;
            };
            Note(mix, start, length, voice);
            Note(wet, start, length, t => voice(t) * 0.8f);
        }

        // inharmonic partials plus noise: a ride cymbal
        private static void Ride(float[] mix, float start, float amp, System.Random random)
        {
            float last = 0;
            Note(mix, start, 0.6f, t =>
            {
                float n = (float)(random.NextDouble() * 2 - 1);
                float hiss = n - last;
                last = n;
                float ping = Mathf.Sin(2 * Mathf.PI * 3150 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 4410 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 5730 * t);
                return (hiss * 0.5f + ping * 0.2f) * Mathf.Exp(-t / 0.22f) * amp;
            });
        }

        private static void Hat(float[] mix, float start, System.Random random)
        {
            float last = 0;
            Note(mix, start, 0.08f, t =>
            {
                float n = (float)(random.NextDouble() * 2 - 1);
                float hiss = n - last;
                last = n;
                return hiss * Mathf.Exp(-t / 0.025f) * 0.05f;
            });
        }

        private static void Snare(float[] mix, float start, float amp, System.Random random) =>
            Note(mix, start, 0.18f, t =>
            {
                float n = (float)(random.NextDouble() * 2 - 1);
                return (n * 0.8f + Mathf.Sin(2 * Mathf.PI * 190 * t) * 0.4f) * Mathf.Exp(-t / 0.06f) * amp;
            });

        private static void Tom(float[] mix, float start, float pitch) =>
            Note(mix, start, 0.3f, t => Mathf.Sin(2 * Mathf.PI * pitch * (1 + 0.5f * Mathf.Exp(-t / 0.04f)) * t) * Mathf.Exp(-t / 0.14f) * 0.16f);

        // lub-dub, about 64 beats a minute
        private static AudioClip _heartbeat;

        public static AudioClip Heartbeat()
        {
            if (_heartbeat != null) return _heartbeat;
            int length = (int)(0.94f * Rate);
            var data = new float[length];
            Note(data, 0, 0.25f, t => Mathf.Sin(2 * Mathf.PI * 52 * t) * Mathf.Exp(-t / 0.07f) * 0.9f);
            Note(data, 0.27f, 0.25f, t => Mathf.Sin(2 * Mathf.PI * 46 * t) * Mathf.Exp(-t / 0.06f) * 0.6f);
            _heartbeat = AudioClip.Create("heartbeat", length, 1, Rate, false);
            _heartbeat.SetData(data, 0);
            return _heartbeat;
        }

        private static void Kick(float[] mix, float start, float amp) =>
            Note(mix, start, 0.25f, t =>
            {
                float f = 48 + 60 * Mathf.Exp(-t / 0.03f);
                return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / 0.1f) * amp;
            });

        // a feedback delay on a looping buffer: the second lap settles the echoes that wrap past the loop point
        private static void Echo(float[] buffer, float seconds, float feedback)
        {
            int delay = (int)(seconds * Rate);
            var dry = (float[])buffer.Clone();
            for (int lap = 0; lap < 2; lap++)
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = dry[i] + feedback * buffer[(i - delay + buffer.Length) % buffer.Length];
        }
    }
}
