using System.Net;
using System.Linq;
using System.Collections.Generic;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using Rocket.Core.Plugins;
using Rocket.Core;
using Rocket.API;
using SDG.Unturned;
using UnityEngine;
using fr34kyn01535.Uconomy;
using Teyhota.CustomKits;
using Logger = Rocket.Core.Logging.Logger;

namespace Teyhota.VoteRewards
{
    public class VoteRewards
    {
        // Reuse a single WebClient per thread — avoids repeated allocation/disposal
        // while remaining thread-safe (each thread gets its own instance).
        [System.ThreadStatic]
        private static WebClient _wc;
        private static WebClient Wc => _wc ?? (_wc = new WebClient());

        [System.ThreadStatic]
        private static System.Random _rng;
        private static System.Random Rng => _rng ?? (_rng = new System.Random());

        // ── API helpers ──────────────────────────────────────────────────────────

        public static string GetVote(UnturnedPlayer player,
                                     Plugin.VoteRewardsConfig.Service service,
                                     string url)
        {
            if (string.IsNullOrEmpty(service.APIKey))
            {
                Logger.LogError("\nVoteRewards >> API key(s) not found\n");
                return null;
            }

            string result;
            try
            {
                result = Wc.DownloadString(
                    string.Format(url, service.APIKey, player.CSteamID.m_SteamID));
            }
            catch (WebException)
            {
                Logger.LogError(
                    $"\nVoteRewards >> Could not connect to {service.Name}'s API\n");
                return null;
            }

            if (result.Length != 1)
            {
                switch (result)
                {
                    case "Error: invalid server key":
                        Logger.LogError("\nVoteRewards >> API key is invalid\n");
                        break;
                    case "Error: no server key":
                        Logger.LogError("\nVoteRewards >> API key not found\n");
                        break;
                    default:
                        Logger.LogError(
                            $"\nVoteRewards >> {service.Name}'s API cannot be used with this plugin\n");
                        break;
                }
                return null;
            }

            return result;
        }

        public static bool SetVote(UnturnedPlayer player,
                                   Plugin.VoteRewardsConfig.Service service)
        {
            string url;
            switch (service.Name)
            {
                case "unturned-servers":
                    url = "http://unturned-servers.net/api/?action=post&object=votes&element=claim&key={0}&steamid={1}";
                    break;
                case "unturnedsl":
                    url = "http://unturnedsl.com/api/dedicated/post/{0}/{1}";
                    break;
                case "obs.erve.me":
                case "observatory":
                    url = "http://api.observatory.rocketmod.net/?server={0}&steamid={1}&claim";
                    break;
                default:
                    return false;
            }

            if (string.IsNullOrEmpty(service.APIKey))
                return false;

            string result;
            try
            {
                result = Wc.DownloadString(
                    string.Format(url, service.APIKey, player.CSteamID.m_SteamID));
            }
            catch (WebException)
            {
                Logger.LogError(
                    $"\nVoteRewards >> Could not connect to {service.Name}'s API\n");
                return false;
            }

            // API must return a single character; anything else is unexpected.
            return result.Length == 1 && result == "1";
        }

        // ── Vote flow ────────────────────────────────────────────────────────────

        public static void HandleVote(UnturnedPlayer player, bool giveReward)
        {
            string voteResult = null;
            string serviceName = null;
            Plugin.VoteRewardsConfig.Service matched = null;

            var services = Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Services;

            for (int idx = 0; idx < services.Count; idx++)
            {
                var service = services[idx];

                if (string.IsNullOrEmpty(service.APIKey))
                    continue;

                string pollUrl;
                switch (service.Name)
                {
                    case "unturned-servers":
                        pollUrl = "http://unturned-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}";
                        break;
                    case "unturnedsl":
                        pollUrl = "http://unturnedsl.com/api/dedicated/{0}/{1}";
                        break;
                    case "obs.erve.me":
                    case "observatory":
                        pollUrl = "http://api.observatory.rocketmod.net/?server={0}&steamid={1}";
                        break;
                    default:
                        continue;
                }

                string result = GetVote(player, service, pollUrl);
                if (result == "2")
                    continue; // already claimed on this service — try next

                voteResult = result;
                serviceName = service.Name;
                matched = service;
                break;
            }

            if (voteResult == null)
            {
                if (giveReward)
                    QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate("failed_to_connect"), Color.red);
                return;
            }

            switch (voteResult)
            {
                case "0": // Has not voted
                    QueueSay(player,
                        Plugin.VoteRewardsPlugin.Instance.Translate("not_yet_voted", serviceName),
                        Color.red);
                    break;

                case "1": // Has voted, reward not yet claimed
                    if (giveReward)
                    {
                        if (SetVote(player, matched))
                            GiveReward(player, serviceName);
                        else
                            QueueSay(player,
                                Plugin.VoteRewardsPlugin.Instance.Translate("failed_to_connect"),
                                Color.red);
                    }
                    else
                    {
                        QueueSay(player,
                            Plugin.VoteRewardsPlugin.Instance.Translate("pending_reward"));
                    }
                    break;

                case "2": // Has voted and already claimed
                    if (giveReward)
                        QueueSay(player,
                            Plugin.VoteRewardsPlugin.Instance.Translate("already_voted"),
                            Color.red);
                    break;
            }
        }

        // ── Reward dispatch ──────────────────────────────────────────────────────

        public static void GiveReward(UnturnedPlayer player, string serviceName)
        {
            var rewards = Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Rewards;

            if (rewards == null || rewards.Count == 0)
            {
                QueueSay(player, "The admin hasn't setup rewards yet.", Color.red);
                return;
            }

            // Weighted random pick — fix: use >= 0 lower bound so diceRoll == 0
            // correctly falls into the first bucket (was strict > causing misses).
            int sum = rewards.Sum(r => r.Chance);
            int diceRoll = Rng.Next(0, sum); // [0, sum)
            int cursor = 0;

            Plugin.VoteRewardsConfig.Reward selected = null;
            foreach (var reward in rewards)
            {
                cursor += reward.Chance;
                if (diceRoll < cursor) // fixed: >= lower bound, < upper bound
                {
                    selected = reward;
                    break;
                }
            }

            if (selected == null)
            {
                // Mathematically unreachable after the fix, but kept as a safety net.
                QueueSay(player, "The admin hasn't setup rewards yet.", Color.red);
                return;
            }

            string type = selected.Type;
            string value = selected.Value;

            if (type == "item" || type == "i")
            {
                // Parse item IDs once, off-thread — avoids per-item parsing on main thread.
                string[] parts = value.Split(',');
                ushort[] itemIDs = new ushort[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    itemIDs[i] = ushort.Parse(parts[i].Trim());

                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() =>
                {
                    for (int i = 0; i < itemIDs.Length; i++)
                        player.Inventory.tryAddItem(new Item(itemIDs[i], true), true);
                });
                QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", "some items"));
            }
            else if (type == "xp" || type == "exp")
            {
                uint xp = uint.Parse(value);
                // Experience write must be on main thread (engine field).
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => player.Experience += xp);
                QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", value + " xp"));
            }
            else if (type == "group" || type == "permission")
            {
                // Permission mutations are thread-safe in RocketMod's XML provider.
                R.Permissions.AddPlayerToGroup(value, player);
                // Not needed: R.Permissions.Reload();
                QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", value + " Permission Group"));
            }
            else if (type == "uconomy" || type == "money")
            {
                if (Plugin.VoteRewardsPlugin.Uconomy)
                {
                    decimal amount = decimal.Parse(value);
                    RocketPlugin.ExecuteDependencyCode("Uconomy", (IRocketPlugin plugin) =>
                    {
                        Uconomy.Instance.Database.IncreaseBalance(
                            player.CSteamID.ToString(), amount);
                        QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate(
                            "reward",
                            value + " Uconomy " + Uconomy.Instance.Configuration.Instance.MoneyName + "s"));
                    });
                }
            }
            else if (type == "slot" || type.Contains("customkit"))
            {
                if (Plugin.VoteRewardsPlugin.CustomKits)
                {
                    int slotValue = int.Parse(value);
                    RocketPlugin.ExecuteDependencyCode("CustomKits", (IRocketPlugin plugin) =>
                    {
                        SlotManager.AddSlot(player, 1, slotValue);
                        QueueSay(player, Plugin.VoteRewardsPlugin.Instance.Translate(
                            "reward",
                            "a CustomKits slot with item limit of " + value));
                    });
                }
            }

            // Optional global announcement — batch snapshot to avoid repeated Provider.clients access.
            if (Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.GlobalAnnouncement)
            {
                string charName = player.CharacterName;
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() =>
                {
                    // Snapshot inside main thread where Provider.clients is safe to read.
                    var clients = Provider.clients;
                    for (int i = 0; i < clients.Count; i++)
                    {
                        var sP = clients[i];
                        if (sP.playerID.steamID != player.CSteamID)
                        {
                            UnturnedChat.Say(
                                UnturnedPlayer.FromSteamPlayer(sP),
                                Plugin.VoteRewardsPlugin.Instance.Translate(
                                    "reward_announcement", charName, serviceName),
                                Color.green);
                        }
                    }
                });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void QueueSay(UnturnedPlayer player, string message, Color? color = null)
        {
            if (color.HasValue)
            {
                Color c = color.Value;
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(
                    () => UnturnedChat.Say(player, message, c));
            }
            else
            {
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(
                    () => UnturnedChat.Say(player, message));
            }
        }
    }
}