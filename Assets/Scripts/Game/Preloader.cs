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
        // every character's painting
        public static readonly string[] Portraits =
        {
            "thetwins", "theiceman", "thecheat", "themime", "thetaxman", "thepurist", "theceiling", "thesweeper", "thesilencer",
            "thecardmechanic", "thehoarder", "thepitboss", "theeyeinthesky", "theenforcer", "thehouse",
            "rookieray", "silentsofia", "chattychen", "oldpete", "vegasval", "luckylou",
            "sal", "vinnie", "morty", "finn", "dot", "gus", "officerreyes", "madamerose", "cherry", "mrbig", "slickeddie",
            "jules", "thecoat", "hank", "flash", "security", "theusher", "zappo", "you",
        };

        public static IEnumerator Run()
        {
            yield return null;
            foreach (var id in Portraits)
            {
                var request = Resources.LoadAsync<Texture2D>("Portraits/" + id);
                yield return request;
                Puppet.Keep(id, request.asset as Texture2D);
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
