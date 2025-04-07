using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
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

            public Player(PlayerAvatar avatar)
            {
                Avatar = avatar;
                Name = Traverse.Create(avatar).Field("playerName").GetValue<string>();
                var t = Traverse.Create(avatar.playerHealth);
                MaxHealth = t.Field("maxHealth").GetValue<int>();
                Health = t.Field("health").GetValue<int>();
            }
        }

        private static Player[] GetPlayers()
        {
            // List of players, sorted by max health.
            // We want to always process the players with the least max health first.
            return GameDirector.instance.PlayerList
                .Select(x => new Player(x))
                .OrderBy(x => x.MaxHealth)
                .ThenBy(x => x.Name, StringComparer.InvariantCulture)
                .ToArray();
        }

        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.OnSceneSwitch))]
        public static void Prefix(bool _gameOver, bool _leaveGame)
        {
            if (!_enabled || _gameOver || _leaveGame)
                return;

            if (!SemiFunc.IsMasterClient())
                return;

            if (GameDirector.instance.PlayerList.Count <= 1)
                return;

            // Only run when switcihing to a normal level, not the shop or "Lobby" (post-shop truck downtime).
            if (!SemiFunc.RunIsLevel())
                return;

            // We assume that at the start of the round:
            // - Every player has at least one health, no one is dead.
            // - No player has an active invincibleTimer
            // - No godmode or anything else that would prevent playerHealth.HurtOther() from working.

            Log("Equalizing health");

            var players = GetPlayers();
            var remainingHealth = players.Sum(x => x.Health);
            var remainingPlayers = players.Length;

            var initial = string.Join(", ", players.Select(p => $"{p.Health}/{p.MaxHealth}"));
            Log($"Initial health: {initial}");

            foreach (var player in players)
            {
                var targetHealth = remainingHealth/remainingPlayers;
                targetHealth = Math.Min(targetHealth, player.MaxHealth);

                // Pretty sure this shouldn't be possible as every player should have at-least one health, but might
                // as well ensure we don't somehow set a players health to 0
                targetHealth = Math.Max(1, targetHealth);

                remainingHealth -= targetHealth;
                remainingPlayers--;

                if (player.Health == targetHealth)
                    continue;

                var delta = targetHealth - player.Health;
                if (delta > 0)
                {
                    Log($"Healing {player.Name} by {delta}");
                    player.Avatar.playerHealth.HealOther(delta, false);
                }
                else
                {
                    Log($"Damaging {player.Name} by {-delta}");
                    // No effects:false option?
                    // Ah well.... its probably fine....
                    player.Avatar.playerHealth.HurtOther(-delta, Vector3.zero, false);
                }
            }

            players = GetPlayers();
            var final = string.Join(", ", players.Select(p => $"{p.Health}/{p.MaxHealth}"));
            Log($"Final health: {final}");
        }
    }
}
