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
        public static string GetVote(UnturnedPlayer player, Plugin.VoteRewardsConfig.Service service, string url)
        {
            WebClient wc = new WebClient();
            string result = null;

            if (service.APIKey == null || service.APIKey.Length == 0)
            {
                Logger.LogError("\nVoteRewards >> API key(s) not found\n");

                return null;
            }

            try
            {
                result = wc.DownloadString(string.Format(url, service.APIKey, player.CSteamID.m_SteamID));
            }
            catch (WebException)
            {
                Logger.LogError(string.Format("\nVoteRewards >> Could not connect to {0}'s API\n", service.Name));
            
                return null;
            }

            
            if (result.Length != 1)
            {
                if (result == "Error: invalid server key")
                {
                    Logger.LogError("\nVoteRewards >> API key is invalid\n");
                }
                else if (result == "Error: no server key")
                {
                    Logger.LogError("\nVoteRewards >> API key not found\n");
                }
                else
                {
                    Logger.LogError(string.Format("\nVoteRewards >> {0}'s API cannot be used with this plugin\n", service.Name));
                }

                return null;
            }

            return result;
        }

        public static bool SetVote(UnturnedPlayer player, Plugin.VoteRewardsConfig.Service service)
        {
            WebClient wc = new WebClient();
            string result = null;
            string url = null;

            if (service.Name == "unturned-servers")
            {
                url = "http://unturned-servers.net/api/?action=post&object=votes&element=claim&key={0}&steamid={1}";
            }
            else if (service.Name == "unturnedsl")
            {
                url = "http://unturnedsl.com/api/dedicated/post/{0}/{1}";
            }
            else if (service.Name == "obs.erve.me" || service.Name == "observatory")
            {
                url = "http://api.observatory.rocketmod.net/?server={0}&steamid={1}&claim";
            }

            if (service.APIKey == null || service.APIKey.Length == 0 || url == null)
            {
                return false;
            }

            try
            {
                result = wc.DownloadString(string.Format(url, service.APIKey, player.CSteamID.m_SteamID));
            }
            catch (WebException)
            {
                Logger.LogError(string.Format("\nVoteRewards >> Could not connect to {0}'s API\n", service.Name));

                return false;
            }

            if (result.Length != 1)
            {
                Logger.LogError(string.Format("\nVoteRewards >> {0}'s API cannot be used with this plugin\n", service.Name));

                return false;
            }

            if (result == "0") // Not claimed
            {
                return false;
            }
            else if (result == "1") // Claimed
            {
                return true;
            }

            return false;
        }

        public static void HandleVote(UnturnedPlayer player, bool giveReward)
        {
            string voteResult = null;
            string serviceName = null;
            var s = new Plugin.VoteRewardsConfig.Service("","");
            
            foreach (var service in Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Services)
            {
                if (service.Name == "unturned-servers")
                {
                    if (service.APIKey == null || service.APIKey.Length == 0)
                    {
                        continue;
                    }

                    s = new Plugin.VoteRewardsConfig.Service(service.Name, service.APIKey);
                    voteResult = GetVote(player, s, "http://unturned-servers.net/api/?object=votes&element=claim&key={0}&steamid={1}");
                    serviceName = service.Name;

                    if (voteResult == "2")
                    {
                        continue;
                    }
                    break;
                }
                else if (service.Name == "unturnedsl")
                {
                    if (service.APIKey == null || service.APIKey.Length == 0)
                    {
                        continue;
                    }

                    s = new Plugin.VoteRewardsConfig.Service(service.Name, service.APIKey);
                    voteResult = GetVote(player, s, "http://unturnedsl.com/api/dedicated/{0}/{1}");
                    serviceName = service.Name;

                    if (voteResult == "2")
                    {
                        continue;
                    }
                    break;
                }
                else if (service.Name == "obs.erve.me" || service.Name == "observatory")
                {

                    if (service.APIKey == null || service.APIKey.Length == 0)
                    {
                        continue;
                    }

                    s = new Plugin.VoteRewardsConfig.Service(service.Name, service.APIKey);
                    voteResult = GetVote(player, s, "http://api.observatory.rocketmod.net/?server={0}&steamid={1}");
                    serviceName = service.Name;

                    if (voteResult == "2")
                    {
                        continue;
                    }
                    break;
                }
            }

            if (voteResult == null && giveReward == true)
            {
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("failed_to_connect"), Color.red));
                //UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("failed_to_connect"), Color.red);
            }
            else
            {
                if (voteResult == "0") // Has not voted
                {
                    Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("not_yet_voted", serviceName), Color.red));
                }
                else if (voteResult == "1") // Has voted & not claimed
                {
                    if (giveReward)
                    {
                        if (SetVote(player, s))
                        {
                            GiveReward(player, serviceName);
                        }
                        else
                        {
                            Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("failed_to_connect"), Color.red));
                        }
                    }
                    else
                    {
                        Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("pending_reward")));
                    }
                }
                else if (voteResult == "2") // Has voted & claimed
                {
                    if (giveReward)
                    {
                        Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("already_voted"), Color.red));
                    }
                }
            }
        }

        public static void GiveReward(UnturnedPlayer player, string serviceName)
        {
            int sum = Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Rewards.Sum(p => p.Chance);
            string selectedElement = null;
            string value = null;

            System.Random r = new System.Random();

            int i = 0, diceRoll = r.Next(0, sum);

            foreach (var reward in Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Rewards)
            {
                if (diceRoll > i && diceRoll <= i + reward.Chance)
                {
                    selectedElement = reward.Type;
                    value = reward.Value;
                    break;
                }
                i = i + reward.Chance;
            }

            if (selectedElement == null || value == null)
            {
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, "The admin hasn't setup rewards yet.", Color.red));
                return;
            }

            // Rewards
            if (selectedElement == "item" || selectedElement == "i")
            {
                List<string> items = value.Split(',').ToList();
                foreach (string item in items)
                {
                    ushort itemID = ushort.Parse(item);
                    Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => player.Inventory.tryAddItem(new Item(itemID, true), true));
                    //player.Inventory.tryAddItem(new Item(itemID, true), true);
                }
                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(()=>UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", "some items")));
            }
            else if (selectedElement == "xp" || selectedElement == "exp")
            {
                player.Experience += uint.Parse(value);

                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", value + " xp")));
            }
            else if (selectedElement == "group" || selectedElement == "permission")
            {
                R.Permissions.AddPlayerToGroup(value, player);
                R.Permissions.Reload();

                Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", value + " Permission Group")));
            }
            else if (selectedElement == "uconomy" || selectedElement == "money")
            {
                if (Plugin.VoteRewardsPlugin.Uconomy)
                {
                    RocketPlugin.ExecuteDependencyCode("Uconomy", (IRocketPlugin plugin) =>
                    {
                        Uconomy.Instance.Database.IncreaseBalance(player.CSteamID.ToString(), decimal.Parse(value));

                        Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", value + " Uconomy " + Uconomy.Instance.Configuration.Instance.MoneyName + "s")));
                    });
                }
            }
            else if (selectedElement == "slot" || selectedElement.Contains("customkit"))
            {
                if (Plugin.VoteRewardsPlugin.CustomKits)
                {
                    RocketPlugin.ExecuteDependencyCode("CustomKits", (IRocketPlugin plugin) =>
                    {
                        SlotManager.AddSlot(player, 1, int.Parse(value));

                        Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(() => UnturnedChat.Say(player, Plugin.VoteRewardsPlugin.Instance.Translate("reward", "a CustomKits slot with item limit of " + value)));
                    });
                }
            }

            // Optional global announcement
            if (Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.GlobalAnnouncement)
            {
                foreach (SteamPlayer sP in Provider.clients)
                {
                    var p = sP.playerID.steamID;
                    if (p != player.CSteamID)
                    {
                        Rocket.Core.Utils.TaskDispatcher.QueueOnMainThread(()=> UnturnedChat.Say(UnturnedPlayer.FromSteamPlayer(sP), Plugin.VoteRewardsPlugin.Instance.Translate("reward_announcement", player.CharacterName, serviceName), Color.green));
                    }
                }
            }
        }
    }
}