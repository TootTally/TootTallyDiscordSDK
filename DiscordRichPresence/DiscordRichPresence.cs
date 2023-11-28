using System;
using System.Collections.Generic;
using System.Linq;
using BaboonAPI.Hooks.Entrypoints;
using HarmonyLib;
using UnityEngine;

namespace TootTallyDiscordSDK.DiscordRichPresence
{
    public class DiscordRPCManager : MonoBehaviour
    {
        public static string[] Statuses = { "Main Menu", "Choosing a song", "Tooting up a storm", "Watching a replay", "Celebrating a successful play" };
        private const long clientId = 1067808791330029589;
        private static ActivityManager _actMan;
        private static Discord _discord;
        private static Activity _act;
        internal static string _username;
        internal static int _ranking;

        private void Awake() => InitRPC();

        private static void InitRPC()
        {
            try
            {
                _discord = new Discord(clientId, (ulong)CreateFlags.NoRequireDiscord);
                _discord.SetLogHook(LogLevel.Error, (level, message) => Plugin.LogError($"[{level}] {message}"));
                _actMan = _discord.GetActivityManager();
            }
            catch (Exception e)
            {
                Plugin.LogError(e.ToString());
            }

        }

        public static void SetActivity(GameStatus status)
        {
            string rankingText = "";
            if (_username != null && _ranking > 0)
            {
                rankingText = $"#{_ranking}";
            }
            else
            {
                rankingText = "Unrated";
            }
            _act = new Activity
            {
                Details = Statuses[(int)status],
                Assets = { LargeImage = "toottallylogo", LargeText = $"{_username} ({rankingText})" },
            };
            return;
        }

        public static void SetActivity(GameStatus status, string message)
        {
            string rankingText = "";
            if (_username != null && _ranking > 0)
            {
                rankingText = $"#{_ranking}";
            }
            else
            {
                rankingText = "Unrated";
            }
            _act = new Activity
            {
                State = message,
                Details = Statuses[(int)status],
                Assets = { LargeImage = "toottallylogo", LargeText = $"{_username} ({rankingText})" },
            };
        }

        public static void SetActivity(GameStatus status, long startTime, string songName, string artist)
        {
            string rankingText;
            if (_username != null && _ranking > 0)
            {
                rankingText = $"#{_ranking}";
            }
            else
            {
                rankingText = "Unrated";
            }
            _act = new Activity
            {
                State = $"{artist} - {songName}",
                Details = Statuses[(int)status],
                Timestamps = { Start = startTime },
                Assets = { LargeImage = "toottallylogo", LargeText = $"{_username} ({rankingText})" },
            };
        }

        public static void SetStatus(GameStatus status) => _act.Details = Statuses[(int)status];

        public static void SetAccount(string username, int ranking)
        {
            _username = username;
            _ranking = ranking;
        }

        [HarmonyPatch(typeof(SaveSlotController), nameof(SaveSlotController.Start))]
        [HarmonyPostfix]
        public static void InitializeOnStartup()
        {
            if (_discord == null)
            {
                InitRPC();
                _username = "Picking a save...";
            }

            SetActivity(GameStatus.MainMenu);
        }

        private static List<IDiscordSDKEntryPoint> _entryPointsInstances;

        [HarmonyPatch(typeof(HomeController), nameof(HomeController.Start))]
        [HarmonyPostfix]
        public static void SetHomeScreenRP()
        {
            _entryPointsInstances ??= Entrypoints.get<IDiscordSDKEntryPoint>().ToList();
            if (_discord == null) InitRPC();
            SetActivity(GameStatus.MainMenu);
        }

        [HarmonyPatch(typeof(CharSelectController), nameof(CharSelectController.Start))]
        [HarmonyPostfix]
        public static void SetCharScreenRP()
        {
            if (_discord == null) InitRPC();
            SetActivity(GameStatus.MainMenu);
        }

        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
        [HarmonyPostfix]
        public static void SetLevelSelectRP()
        {
            if (_discord == null) InitRPC();
            SetActivity(GameStatus.LevelSelect);
            _entryPointsInstances.ForEach(entry => entry.OnLevelSelectStart());
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.startSong))]
        [HarmonyPostfix]
        public static void SetPlayingRP()
        {
            if (_discord == null) InitRPC();
            GameStatus status = GameStatus.InGame;
            SetActivity(status, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), GlobalVariables.chosen_track_data.trackname_long, GlobalVariables.chosen_track_data.artist);
            _entryPointsInstances.ForEach(entry => entry.OnGameControllerStart());
        }

        [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
        [HarmonyPostfix]
        public static void SetPointScreenRP()
        {
            if (_discord == null) InitRPC();
            SetActivity(GameStatus.PointScreen);
        }

        private void Update()
        {
            if (_discord != null)
            {
                _actMan.UpdateActivity(_act, (result) =>
                {
                    if (result != Result.Ok)
                        Plugin.LogInfo("Discord: Something went wrong: " + result.ToString());
                });
                try
                {
                    _discord.RunCallbacks();
                }
                catch (Exception e)
                {
                    Plugin.LogError(e.ToString());
                    _discord.Dispose();
                    _discord = null;
                }
            }
        }

        public interface IDiscordSDKEntryPoint
        {
            void OnGameControllerStart();
            void OnLevelSelectStart();
        }
    }
}