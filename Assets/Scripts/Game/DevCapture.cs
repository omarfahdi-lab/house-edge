using System;
using System.Collections;
using HouseEdge.Core;
using UnityEngine;

namespace HouseEdge.Game
{
    // command-line hook for screenshots and smoke tests of a built player:
    //   -seed 7 -play Deal,Hit,Stand -capture out.png
    // steps are PlayerActions, or Buy:0, Sell:0, Reroll, Leave, or Auto (play the table out).
    // plays them, saves a screenshot and quits. does nothing without -capture
    public sealed class DevCapture : MonoBehaviour
    {
        private string _path;
        private string[] _steps = Array.Empty<string>();

        public static int? SeedOverride => int.TryParse(Arg("-seed"), out int seed) ? seed : (int?)null;
        public static bool Requested => !string.IsNullOrEmpty(Arg("-capture"));

        // -wav <dir>: save both music loops as WAV files, to check them outside the game
        public static void SaveMusicIfRequested()
        {
            string dir = Arg("-wav");
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            WriteWav(System.IO.Path.Combine(dir, "lounge.wav"), MusicSynth.Lounge());
            WriteWav(System.IO.Path.Combine(dir, "boss.wav"), MusicSynth.Boss());
        }

        private static void WriteWav(string path, AudioClip clip)
        {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            using var writer = new System.IO.BinaryWriter(System.IO.File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);
            foreach (float s in samples) writer.Write((short)(Mathf.Clamp(s, -1, 1) * 32767));
        }

        public static void AttachIfRequested(GameBootstrap game)
        {
            SaveMusicIfRequested();
            string path = Arg("-capture");
            if (string.IsNullOrEmpty(path)) return;
            // keep updating when the window isn't focused
            Application.runInBackground = true;
            var capture = game.gameObject.AddComponent<DevCapture>();
            capture._path = path;
            string play = Arg("-play");
            if (!string.IsNullOrEmpty(play)) capture._steps = play.Split(',');
        }

        // -record <dir>: save a frame every 0.1 s while the steps play, for the README GIF
        private IEnumerator Record(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            int frame = 0;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"frame{frame++:0000}.png"), shot.EncodeToPNG());
                Destroy(shot);
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        private IEnumerator Start()
        {
            var game = GetComponent<GameBootstrap>();
            yield return null;
            string record = Arg("-record");
            if (!string.IsNullOrEmpty(record)) StartCoroutine(Record(record));
            foreach (var raw in _steps)
            {
                // Bot:N lets the balance bot play N whole runs through the real UI, as a smoke test
                if (raw == "Bot" || raw.StartsWith("Bot:"))
                {
                    int runs = raw.Contains(":") ? int.Parse(raw.Split(':')[1]) : 1;
                    var bot = new BalanceBot();
                    for (int r = 0; r < runs; r++)
                    {
                        if (r > 0) game.StartRun(1000 + r, new RunOptions { Deck = (StartingDeck)(r % Decks.All.Count), Stake = (Stake)(r % Stakes.All.Count) });
                        for (int i = 0; i < 6000 && !game.Run.IsOver; i++)
                        {
                            game.TryApply(bot.Choose(game.Run));
                            if (i % 25 == 0) yield return null;
                        }
                        Debug.Log($"DevCapture: bot run {r + 1} ended in {game.Run.Phase} on floor {game.Run.Floor}, score {game.Run.TotalScore}");
                    }
                    continue;
                }
                // Watch:N plays N bot inputs slowly enough to see (and record)
                if (raw.StartsWith("Watch:"))
                {
                    var bot = new BalanceBot();
                    int count = int.Parse(raw.Split(':')[1]);
                    for (int i = 0; i < count && !game.Run.IsOver; i++)
                    {
                        var phase = game.Run.Phase;
                        game.TryApply(bot.Choose(game.Run));
                        yield return new WaitForSecondsRealtime(phase == RunPhase.AtTable ? 0.55f : 1.1f);
                    }
                    continue;
                }
                Step(game, raw.Trim());
                yield return null;
            }
            // let UI Toolkit lay out, and the card animations finish
            for (int i = 0; i < 10; i++) yield return null;
            yield return new WaitForSecondsRealtime(float.TryParse(Arg("-wait"), out float wait) ? wait : 1.2f);
            ScreenCapture.CaptureScreenshot(_path);
            yield return null;
            yield return null;
            Application.Quit();
        }

        private static void Step(GameBootstrap game, string step)
        {
            var parts = step.Split(':');
            int index = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            switch (parts[0])
            {
                case "Buy": game.TryApply(RunInput.Buy(index)); break;
                case "Sell": game.TryApply(RunInput.SellVice(index)); break;
                case "Reroll": game.TryApply(RunInput.Reroll()); break;
                case "Leave": game.TryApply(RunInput.LeaveShop()); break;
                case "Auto": AutoPlayTable(game); break;
                case "Skip": game.TryApply(RunInput.SkipReward()); break;
                case "Menu": game.OpenMenu(); break;
                case "Title": game.OpenMenuAfterAbandon(); break;
                case "Deck": game.ShowDeck(); break;
                case "HowTo": game.ShowHowTo(index); break;
                case "Map": game.ShowMap(); break;
                case "Collection": game.ShowCollection(); break;
                case "Intro": game.PreviewBoss(index); break;
                case "BotTo":
                    // the bot plays until it reaches floor N (or the run ends), then stops at the next shop
                    var bot = new BalanceBot();
                    for (int i = 0; i < 6000 && !game.Run.IsOver && !(game.Run.Floor >= index && game.Run.Phase == RunPhase.Shop); i++)
                        game.TryApply(bot.Choose(game.Run));
                    break;
                case "ToBoss":
                    // the bot plays until the next Pit Boss table is about to deal
                    var bossBot = new BalanceBot();
                    for (int i = 0; i < 6000 && !game.Run.IsOver && !(game.Run.Stage == TableKind.Boss && game.Run.Phase == RunPhase.AtTable && game.Run.Table.DealsPlayed == 0); i++)
                        game.TryApply(bossBot.Choose(game.Run));
                    break;
                case "Door": game.TryApply(RunInput.ChooseRoom(index)); break;
                case "Room": game.TryApply(RunInput.RoomOption(index)); break;
                case "Out": game.TryApply(RunInput.LeaveRoom()); break;
                case "Drink": game.TryApply(RunInput.UseCocktail(index)); break;
                case "Gold": game.Run.Gambler.GainGold(index); break;
                case "Heat": game.Run.Gambler.AddHeat(index); break;
                case "Refresh": game.TryApply(RunInput.Reroll()); break;
                default:
                    if (Enum.TryParse(step, out PlayerAction action)) game.TryApply(RunInput.Table(action));
                    else Debug.LogWarning($"DevCapture: unknown step {step}");
                    break;
            }
        }

        // hit below 17, burn busts, decline insurance, until the table ends
        private static void AutoPlayTable(GameBootstrap game)
        {
            var start = game.Run.ActiveTable;
            for (int guard = 0; guard < 500 && game.Run.ActiveTable == start && !start.IsOver
                                && (game.Run.Phase == RunPhase.AtTable || game.Run.Phase == RunPhase.InRoom && !game.Run.Room.Used); guard++)
            {
                var table = start;
                var legal = table.LegalActions();
                PlayerAction action;
                if (legal.Contains(PlayerAction.Deal)) action = PlayerAction.Deal;
                else if (legal.Contains(PlayerAction.DeclineInsurance)) action = PlayerAction.DeclineInsurance;
                else if (legal.Contains(PlayerAction.LetDealerDraw)) action = PlayerAction.LetDealerDraw;
                else if (legal.Contains(PlayerAction.Burn) && table.ActiveHand.Value.IsBust) action = PlayerAction.Burn;
                else if (table.ActiveHand.Value.Total < 17 && legal.Contains(PlayerAction.Hit)) action = PlayerAction.Hit;
                else action = PlayerAction.Stand;
                game.TryApply(RunInput.Table(action));
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
