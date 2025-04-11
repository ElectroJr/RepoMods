using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace HealthEqualizer
{
    [HarmonyPatch]
    public static class Main
    {
        private static UnityModManager.ModEntry _entry;
        private static bool _enabled;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            _entry = modEntry;
            _entry.OnToggle = OnToggle;
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry _, bool value)
        {
            _enabled = value;
            return true;
        }

        private static void Log(string str) => _entry.Logger.Log(str);

        private struct Player
        {
            public readonly PlayerAvatar Avatar;
            public readonly int MaxHealth;
            public readonly int Health;
            public readonly string Name;
            public readonly string SteamID;

            public Player(PlayerAvatar avatar)
            {
                Avatar = avatar;

                var t = Traverse.Create(avatar);
                Name = t.Field("playerName").GetValue<string>();
                SteamID = t.Field("steamID").GetValue<string>();

                Health = StatsManager.instance.GetPlayerHealth(SteamID);

                // Fun fact of the day: GetPlayerMaxHealth() does not fucking return the max health.
                // Instead, it just returns the additional health due to any purchased upgrades.
                // Yipeeeee! Gotta love misleading function names.
                MaxHealth = 100 + StatsManager.instance.GetPlayerMaxHealth(SteamID);
            }
        }

        /// <summary>
        /// List of players, sorted by max health, as we want to always process the players with the least max health
        /// first. We also use the GameDirector.PlayerList, instead of iterating over the StatsManager dictionaries, as
        /// I think those can contain players that are in the save file but not in-game?
        /// </summary>
        private static Player[] GetPlayers()
        {
            return GameDirector.instance.PlayerList
                .Select(x => new Player(x))
                .OrderBy(x => x.MaxHealth)
                .ThenBy(x => x.Name, StringComparer.InvariantCulture)
                .ToArray();
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.OnSceneSwitch))]
        private static void Prefix(bool _gameOver, bool _leaveGame)
        {
            if (!_enabled || _gameOver || _leaveGame)
                return;

            if (!SemiFunc.IsMasterClient())
                return;

            if (GameDirector.instance.PlayerList.Count <= 1)
                return;

            // Only run when switching to a normal level, not the shop or "Lobby" (post-shop truck downtime).
            if (!SemiFunc.RunIsLevel())
                return;

            // As to how this mod works, originally I looked at how health-sharing / neck grabbing worked tried to use
            // PlayerHealth.HurtOther() and PlayerHealth.HealOther() to modify health.
            // However, that didn't seem to work, and I suspect it was either due to the fact that:
            // - I was using RPCs just before/after loading a level, which can apparently cause issues https://doc.photonengine.com/pun/current/gameplay/rpcsandraiseevent
            // - OnSceneSwitch() calls SemiFunc.StatSyncAll(), which syncs health which is then used to initialize PlayerHealth. Notably, this
            //   would always get called before the RPC networking could finish (i.e., HurtOther gets sent to client, and then client syncs health back to master).
            //
            // Hence, I'm trying a different approach and just modifying the health stats directly, and that seems to work?
            // The networking & level restarting logic seems pretty convoluted to me. Though maybe thats just due to lack of familiarity with Unity & Photon

            Log("Equalizing health");

            var players = GetPlayers();
            var remainingHealth = players.Sum(x => x.Health);
            var remainingPlayers = players.Length;

            var initial = string.Join(", ", players.Select(p => $"{p.Health}/{p.MaxHealth}"));
            Log($"Initial health: {initial}");

            foreach (var player in players)
            {
                var targetHealth = remainingHealth / remainingPlayers;
                targetHealth = Math.Max(1, Math.Min(targetHealth, player.MaxHealth));

                remainingHealth -= targetHealth;
                remainingPlayers--;

                if (player.Health != targetHealth)
                    StatsManager.instance.SetPlayerHealth(player.SteamID, targetHealth, false);
            }

            players = GetPlayers();
            var final = string.Join(", ", players.Select(p => $"{p.Health}/{p.MaxHealth}"));
            Log($"Final health: {final}");
        }
    }
}
