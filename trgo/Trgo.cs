using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace Trgo
{
    [ApiVersion(2, 1)]
    public class Trgo : TerrariaPlugin
    {
        public override string Name => "Trgo";
        public override string Author => "肝帝熙恩";
        public override string Description => "Trgo小游戏";
        private long _timerCount;

        public static Dictionary<string, bool> PlayersInTeamMode = new Dictionary<string, bool>();//
        public static Dictionary<string, string> PlayerTeams = new Dictionary<string, string>(); // 存储玩家队伍，红队或蓝队

        private int countdown = -1; // 倒计时初始值，-1表示不在倒计时中

        public Trgo(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("trgo.use", TrgoUse, "trgo"));
            Commands.ChatCommands.Add(new Command("trgo.admin", TrgoAdmin, "trgoadmin"));
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }

        private void OnUpdate(EventArgs args)
        {
            _timerCount++;
            if (_timerCount % 60 == 0) // 每秒执行一次
            {
                if (countdown > 0)
                {
                    countdown--;
                    TShock.Utils.Broadcast($"游戏将在 {countdown} 秒后开始", Microsoft.Xna.Framework.Color.Green);
                    if (countdown == 0)
                    {
                        StartGame();
                    }
                }
                else if (countdown == -1 && PlayersInTeamMode.Values.Count(v => v) >= PlayersInTeamMode.Count / 2)// 是否没有游戏正在进行，判断准备人数是否达到总人数的一半
                {
                    countdown = 10;
                    TShock.Utils.Broadcast("准备人数已达标，游戏将在 10 秒后开始", Microsoft.Xna.Framework.Color.Green);
                }
            }
        }

        private void TrgoUse(CommandArgs args)
        {
            var player = args.Player;

            if (args.Parameters.Count < 1)
            {
                player.SendInfoMessage("请输入 /trgo help 查看帮助");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "help":
                    player.SendInfoMessage("/trgo help 查看帮助");
                    player.SendInfoMessage("/trgo team 加入或离开团队死斗模式");
                    player.SendInfoMessage("/trgo red 准备加入红队");
                    player.SendInfoMessage("/trgo blue 准备加入蓝队");
                    break;
                case "team":
                    TogglePlayerInTeamMode(player.Name);
                    break;
                case "red":
                    SetPlayerTeam(player.Name, "red");
                    player.SendInfoMessage("你已准备加入红队");
                    break;
                case "blue":
                    SetPlayerTeam(player.Name, "blue");
                    player.SendInfoMessage("你已准备加入蓝队");
                    break;
                default:
                    player.SendInfoMessage("请输入 /trgo help 查看帮助");
                    break;
            }
        }

        private void TogglePlayerInTeamMode(string playerName)//
        {
            if (!PlayersInTeamMode.ContainsKey(playerName))
            {
                PlayersInTeamMode.Add(playerName, false); // 默认未准备状态
            }
            else
            {
                PlayersInTeamMode.Remove(playerName);
                if (PlayerTeams.ContainsKey(playerName))
                {
                    PlayerTeams.Remove(playerName);
                }
            }
        }

        private void SetPlayerTeam(string playerName, string team)
        {
            if (PlayersInTeamMode.ContainsKey(playerName))
            {
                PlayerTeams[playerName] = team;
            }
            else
            {
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                player?.SendInfoMessage("你需要先加入团队死斗模式");
            }
        }

        private void StartGame()
        {
            var redTeam = new List<string>();
            var blueTeam = new List<string>();

            foreach (var player in PlayersInTeamMode.Keys)
            {
                if (PlayerTeams.TryGetValue(player, out var team))
                {
                    if (team == "red")
                    {
                        redTeam.Add(player);
                    }
                    else if (team == "blue")
                    {
                        blueTeam.Add(player);
                    }
                }
            }

            int totalPlayers = PlayersInTeamMode.Count;
            int maxTeamSize = totalPlayers % 2 == 0 ? totalPlayers / 2 : (totalPlayers / 2) + 1;

            if (redTeam.Count > maxTeamSize)
            {
                RandomlyAssignPlayers(redTeam, blueTeam, maxTeamSize, true);
            }
            else if (blueTeam.Count > maxTeamSize)
            {
                RandomlyAssignPlayers(blueTeam, redTeam, maxTeamSize, false);
            }
            else
            {
                AssignRemainingPlayers(redTeam, blueTeam, maxTeamSize);
            }

            // 传送玩家和设置初始装备
            foreach (var playerName in redTeam)
            {
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                if (player != null)
                {
                    player.SetTeam(1); // 红队
                                       // 传送到红队出生点
                                       // 设置初始装备
                }
            }

            foreach (var playerName in blueTeam)
            {
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                if (player != null)
                {
                    player.SetTeam(3); // 蓝队
                                       // 传送到蓝队出生点
                                       // 设置初始装备
                }
            }
        }

        private void RandomlyAssignPlayers(List<string> fromTeam, List<string> toTeam, int maxTeamSize, bool isRedTeam)
        {
            while (fromTeam.Count > maxTeamSize)
            {
                var player = fromTeam.Last();
                fromTeam.Remove(player);
                toTeam.Add(player);
                PlayerTeams[player] = isRedTeam ? "blue" : "red";
            }
        }

        private void AssignRemainingPlayers(List<string> redTeam, List<string> blueTeam, int maxTeamSize)
        {
            var remainingPlayers = PlayersInTeamMode.Keys.Except(redTeam).Except(blueTeam).ToList();
            Random rand = new Random();

            foreach (var player in remainingPlayers)
            {
                if (redTeam.Count < maxTeamSize)
                {
                    redTeam.Add(player);
                    PlayerTeams[player] = "red";
                }
                else
                {
                    blueTeam.Add(player);
                    PlayerTeams[player] = "blue";
                }
            }
        }


        private void TrgoAdmin(CommandArgs args)
        {
            // 管理员指令处理
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }
    }
}
