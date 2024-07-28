using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Teyhota.VoteRewards
{
    public class MsgObj
    {
        public UnturnedPlayer player;
        public string msg;
        public Color color;
        public MsgObj(){}
        public MsgObj(UnturnedPlayer p, string m)
        {
            player = p;
            msg = m;
            color = Color.white;
        }
        public MsgObj(UnturnedPlayer p, string m, Color c)
        {
            player = p;
            msg = m;
            color = c;
        }
    }
}
