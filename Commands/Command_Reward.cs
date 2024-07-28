using System;
using System.Linq;
using System.Collections.Generic;
using Rocket.Unturned.Player;
using Rocket.Unturned.Chat;
using Rocket.API;

namespace Teyhota.VoteRewards.Commands
{
    public class Command_Reward : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "reward";

        public string Help => "Redeem reward after successfully voting";

        public string Syntax => "";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string>() { "voterewards.reward" };

        public async void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                if (caller is ConsolePlayer)
                {
                    Plugin.VoteRewardsPlugin.Write("<player>", ConsoleColor.Red);
                    return;
                }
                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        VoteRewards.HandleVote((UnturnedPlayer)caller, true);
                    });
                }
                catch (Exception)
                {
                    VoteRewards.HandleVote((UnturnedPlayer)caller, true);
                }
            }
            else
            {
                if (caller.HasPermission("voterewards.givereward") || caller is ConsolePlayer)
                {
                    UnturnedPlayer toPlayer = UnturnedPlayer.FromName(command[0]);

                    if (toPlayer != null)
                    {
                        VoteRewards.GiveReward(toPlayer, Plugin.VoteRewardsPlugin.Instance.Configuration.Instance.Services.FirstOrDefault().Name);

                        if (caller is ConsolePlayer)
                        {
                            Plugin.VoteRewardsPlugin.Write(Plugin.VoteRewardsPlugin.Instance.Translate("free_reward", toPlayer.CharacterName));
                        }
                        else
                        {
                            UnturnedChat.Say(caller, Plugin.VoteRewardsPlugin.Instance.Translate("free_reward", toPlayer.CharacterName));
                        }
                    }
                }
            }
        }
    }
}
