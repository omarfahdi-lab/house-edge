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

        public static void AttachIfRequested(GameBootstrap game)
        {
            string path = Arg("-capture");
            if (string.IsNullOrEmpty(path)) return;
            // keep updating when the window isn't focused
            Application.runInBackground = true;
            var capture = game.gameObject.AddComponent<DevCapture>();
            capture._path = path;
            string play = Arg("-play");
            if (!string.IsNullOrEmpty(play)) capture._steps = play.Split(',');
        }

        private IEnumerator Start()
        {
            var game = GetComponent<GameBootstrap>();
            yield return null;
            foreach (var raw in _steps)
            {
                Step(game, raw.Trim());
                yield return null;
            }
            // let UI Toolkit lay out and draw
            for (int i = 0; i < 10; i++) yield return null;
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
                case "Buy": game.TryApply(RunInput.BuyVice(index)); break;
                case "Sell": game.TryApply(RunInput.SellVice(index)); break;
                case "Reroll": game.TryApply(RunInput.Reroll()); break;
                case "Leave": game.TryApply(RunInput.LeaveShop()); break;
                case "Auto": AutoPlayTable(game); break;
                default:
                    if (Enum.TryParse(step, out PlayerAction action)) game.TryApply(RunInput.Table(action));
                    else Debug.LogWarning($"DevCapture: unknown step {step}");
                    break;
            }
        }

        // hit below 17, burn busts, decline insurance, until the table ends
        private static void AutoPlayTable(GameBootstrap game)
        {
            for (int guard = 0; guard < 500 && game.Run.Phase == RunPhase.AtTable; guard++)
            {
                var table = game.Run.Table;
                var legal = table.LegalActions();
                PlayerAction action;
                if (legal.Contains(PlayerAction.Deal)) action = PlayerAction.Deal;
                else if (legal.Contains(PlayerAction.DeclineInsurance)) action = PlayerAction.DeclineInsurance;
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
