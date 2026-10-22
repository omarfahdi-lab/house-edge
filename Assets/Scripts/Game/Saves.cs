using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;

namespace HouseEdge.Game
{
    // everything kept between sessions, in PlayerPrefs (browser storage on WebGL)
    public static class Saves
    {
        private const string ProfileKey = "profile";
        private const string DailyPrefix = "daily-";
        private const int DailyEntries = 5;

        public static Profile LoadProfile() => Profile.Parse(PlayerPrefs.GetString(ProfileKey, ""));

        public static void SaveProfile(Profile profile)
        {
            PlayerPrefs.SetString(ProfileKey, profile.Serialize());
            PlayerPrefs.Save();
        }

        // today's date as the Daily Run sees it (the device's local date)
        public static (int year, int month, int day) Today()
        {
            var now = DateTime.Now;
            return (now.Year, now.Month, now.Day);
        }

        public static int DailySeed()
        {
            var (y, m, d) = Today();
            return RunCodes.DailySeed(y, m, d);
        }

        private static string DailyKey()
        {
            var (y, m, d) = Today();
            return $"{DailyPrefix}{y:0000}{m:00}{d:00}";
        }

        // this device's best Daily Run scores for today, best first
        public static List<long> DailyScores() =>
            PlayerPrefs.GetString(DailyKey(), "")
                .Split(',')
                .Select(s => long.TryParse(s, out long v) ? v : -1)
                .Where(v => v >= 0)
                .OrderByDescending(v => v)
                .ToList();

        // returns the score's place (1 = best), or 0 if it didn't make the board
        public static int AddDailyScore(long score)
        {
            var scores = DailyScores();
            scores.Add(score);
            scores = scores.OrderByDescending(v => v).Take(DailyEntries).ToList();
            PlayerPrefs.SetString(DailyKey(), string.Join(",", scores));
            PlayerPrefs.Save();
            return scores.IndexOf(score) + 1;
        }

        public static string DailyText()
        {
            var scores = DailyScores();
            string board = scores.Count == 0 ? "no runs yet today" : string.Join("  ·  ", scores.Select((s, i) => $"{i + 1}. {s:N0}"));
            return $"Today's Daily Run (same deal for everyone): {board}";
        }
    }
}
