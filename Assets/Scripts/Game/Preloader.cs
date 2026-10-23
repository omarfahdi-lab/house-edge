using System.Collections;
using UnityEngine;

namespace HouseEdge.Game
{
    // loads the character art, room photos and music a little at a time while you sit on the menu,
    // so the first boss, room or track never stalls a frame mid-game
    public static class Preloader
    {
        private static readonly string[] Backgrounds = { "bar", "slots", "pawn", "vault", "security", "lostfound", "lounge", "backroom", "cardroom", "shop", "mystery" };
        private static readonly string[] Tracks = { "Lobby_Time", "Bossa_Antigua", "Hitman" };
        private static readonly string[] Layers = { "legL", "legR", "armL", "armR", "torso", "head" };

        public static IEnumerator Run()
        {
            yield return null;
            foreach (var meta in Resources.LoadAll<TextAsset>("Characters"))
                foreach (var layer in Layers)
                {
                    var request = Resources.LoadAsync<Texture2D>($"Characters/{meta.name}_{layer}");
                    yield return request;
                    Puppet.Keep($"{meta.name}_{layer}", request.asset as Texture2D);
                }
            foreach (var mouth in new[] { "glad", "happy", "oh", "sad", "straight", "teethLower", "teethUpper" })
            {
                var request = Resources.LoadAsync<Texture2D>("Characters/mouth_" + mouth);
                yield return request;
                Puppet.Keep("mouth_" + mouth, request.asset as Texture2D);
            }
            foreach (var name in Backgrounds)
            {
                var request = Resources.LoadAsync<Texture2D>("Backgrounds/" + name);
                yield return request;
                RoomScene.Keep(name, request.asset as Texture2D);
            }
            foreach (var track in Tracks)
            {
                var request = Resources.LoadAsync<AudioClip>("Music/" + track);
                yield return request;
                if (request.asset is AudioClip clip) clip.LoadAudioData();
            }
        }
    }
}
