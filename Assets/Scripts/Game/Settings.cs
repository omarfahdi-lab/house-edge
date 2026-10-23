using UnityEngine;

namespace HouseEdge.Game
{
    // accessibility options, kept between sessions
    public static class Settings
    {
        // diamonds blue and clubs green, so all four suits read apart at a glance
        public static bool FourColor
        {
            get => PlayerPrefs.GetInt("fourColor", 0) == 1;
            set => Save("fourColor", value);
        }

        // no screen shake, flashes or card wobble
        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt("reducedMotion", 0) == 1;
            set => Save("reducedMotion", value);
        }

        private static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
