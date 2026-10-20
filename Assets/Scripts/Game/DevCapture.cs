using System;
using System.Collections;
using HouseEdge.Core;
using UnityEngine;

namespace HouseEdge.Game
{
    // command-line hook for screenshots and smoke tests of a built player:
    //   -seed 7 -play Deal,Hit,Stand -capture out.png
    // plays the actions, saves a screenshot and quits. does nothing without -capture
    public sealed class DevCapture : MonoBehaviour
    {
        private string _path;
        private string[] _actions = Array.Empty<string>();

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
            if (!string.IsNullOrEmpty(play)) capture._actions = play.Split(',');
        }

        private IEnumerator Start()
        {
            var game = GetComponent<GameBootstrap>();
            yield return null;
            foreach (var name in _actions)
            {
                if (Enum.TryParse(name.Trim(), out PlayerAction action)) game.TryApply(action);
                else Debug.LogWarning($"DevCapture: unknown action {name}");
                yield return null;
            }
            // let UI Toolkit lay out and draw
            for (int i = 0; i < 10; i++) yield return null;
            ScreenCapture.CaptureScreenshot(_path);
            yield return null;
            yield return null;
            Application.Quit();
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
