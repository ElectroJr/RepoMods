using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityModManagerNet;

namespace ShowUpgrades
{
    [HarmonyPatch]
    static class Main
    {
        private static UnityModManager.ModEntry _entry;
        private static bool _enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            _entry = modEntry;
            _entry.OnToggle = OnToggle;
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry arg1, bool value)
        {
            _enabled = value;
            StatsUI.instance.Fetch();
            return true;
        }

        public static void Log(string str) => _entry.Logger.Log(str);

        [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
        static void Postfix()
        {
            if (!_enabled)
                return;

            // Ensure that the UI gets updated when any player gets an upgrade, not just the local player
            StatsUI.instance.Fetch();
        }

        [HarmonyPatch(typeof(StatsUI), nameof(StatsUI.Fetch))]
        static bool Prefix(StatsUI __instance)
        {
            if (!_enabled)
                return true;

            var upgradesHeader = Traverse.Create(__instance).Field("upgradesHeader").GetValue<TextMeshProUGUI>();
            var upgradeNames = Traverse.Create(__instance).Field("Text").GetValue<TextMeshProUGUI>();
            var upgradeNumbers = Traverse.Create(__instance).Field("textNumbers").GetValue<TextMeshProUGUI>();

            upgradesHeader.enabled = true;
            upgradeNumbers.enabled = false;
            upgradeNumbers.text = "";
            __instance.scanlineObject.SetActive(true);
            upgradeNames.enableWordWrapping = false;

            // Look ma! it's a bug in the game!
            // fetched never gets set to true
            Traverse.Create(__instance).Field("fetched").SetValue(true);

            // List of player steam IDs, sorted by the player's name.
            var players = GameDirector.instance.PlayerList
                .OrderBy(x => Traverse.Create(x).Field("playerName").GetValue<string>(),
                    StringComparer.InvariantCulture)
                .Select(x => Traverse.Create(x).Field("steamID").GetValue<string>())
                .ToArray();

            var localPlayer = Traverse.Create(PlayerController.instance).Field("playerSteamID").GetValue<string>();

            // Some players (Looking at you Vyncis) choose really dark colours.
            // This is a relatively brain-dead solution, but lets just put a lower bound on the brightness of the colour.
            const float minBrightness = 0.25f;
            Color Clamp(Color colour)
            {
                Color.RGBToHSV(colour, out var h, out var s, out var v);
                return Color.HSVToRGB(h, s, Mathf.Max(v, minBrightness));
            }

            var defaultColour = upgradeNumbers.color;
            var colours = players
                .Select(StatsManager.instance.GetPlayerColor)
                .Select(i => AssetManager.instance.playerColors[i])
                .Select(Clamp)
                .ToArray();

            var upgrades = players.Select(StatsManager.instance.FetchPlayerUpgrades).ToArray();
            var localPlayerIndex = Array.IndexOf(players, localPlayer);

            // Take the per-player dictionaries and combine them into a single dictionary. i.e., convert
            // List<Dictionary<string,int> into Dictionary<string,int[]>
            var combinedUpgrades = new Dictionary<string, int[]>();
            for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                var playerUpgrades = upgrades[playerIndex];
                foreach (var upgrade in playerUpgrades)
                {
                    if (upgrade.Value <= 0)
                        continue;

                    if (!combinedUpgrades.TryGetValue(upgrade.Key, out var arr))
                    {
                        arr = new int[players.Length];
                        combinedUpgrades.Add(upgrade.Key, arr);
                    }

                    arr[playerIndex] = upgrade.Value;
                }
            }

            // Sort combined results by steam id to ensure consistent ordering.
            var combined = combinedUpgrades
                .OrderBy(x => x.Key, StringComparer.InvariantCulture)
                .ToArray();

            // Compute total upgrade counts
            var total = new int[players.Length];
            foreach (var upgrade in combined)
            {
                for (var playerIndex = 0; playerIndex < total.Length; playerIndex++)
                {
                    total[playerIndex] += upgrade.Value[playerIndex];
                }
            }

            // Is any single entry for a given player greater than 9? If yes, we
            // may need to add leading zeros. We can be lazy and just check the totals.
            var needLeadingZero = total.Select(x => x > 9).ToArray();

            var text = new StringBuilder();

            // Give text a slight negative indent.
            // This is both because we aren't using the numbers text box anymore, and to make a bit more room
            // in case there are many players.
            text.Append("<indent=-1.5em>");

            AddRow(text, "<u>TOTAL</u>", total, colours, localPlayerIndex, needLeadingZero, defaultColour);
            text.AppendLine();

            foreach (var upgrade in combined)
            {
                text.AppendLine();
                AddRow(text, upgrade.Key, upgrade.Value, colours, localPlayerIndex, needLeadingZero, defaultColour);
            }

            upgradeNames.text = text.ToString();
            return false;
        }

        private static void AddRow(StringBuilder text,
            string id,
            int[] upgrades,
            Color[] colors,
            int localPlayer,
            bool[] needLeadingZero,
            Color defaultColour)
        {
            text.Append("<mspace=0.35em>");

            for (var i = 0; i < upgrades.Length; i++)
            {
                if (i == localPlayer)
                    continue;

                var color = ColorUtility.ToHtmlStringRGBA(colors[i]);
                text.Append(!needLeadingZero[i]
                    ? $"<color=#{color}><b>{upgrades[i]}</b></color>/"
                    : $"<color=#{color}><b>{upgrades[i]:D2}</b></color>/");
            }

            var def = ColorUtility.ToHtmlStringRGBA(defaultColour);
            text.Append($"<color=#{def}><u><b>{upgrades[localPlayer]}</b></u></color> </mspace>{id}");
        }
    }
}
