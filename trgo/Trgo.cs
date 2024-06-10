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
        public Trgo(Main game) : base(game)
        {

        }
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("trgo.use", trgouse, "trgo"));//注册加入游戏指令
            Commands.ChatCommands.Add(new Command("trgo.admin", trgoadmin, "trgoadmin"));//注册离开游戏指令
        }
        //两个字典，用来存储玩家是否加入游戏
        public static Dictionary<string, bool> playersintrgoteam = new Dictionary<string, bool>();//团队死斗模式
        public static Dictionary<string, bool> playersintrgobomb = new Dictionary<string, bool>();//爆破模式
        private void trgouse(CommandArgs args)//进入游戏和离开游戏
        {
            var plr = args.Player;
            if (args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("请输入/trgo help 查看帮助");
                return;
            }

            switch (args.Parameters[0])
            {
                case "help":
                    args.Player.SendInfoMessage("/trgo help 查看帮助");
                    args.Player.SendInfoMessage("/trgo tean 团队死斗模式");
                    args.Player.SendInfoMessage("/trgo bomb 爆破模式");
                    break;
                case "team":
                    if (!playersintrgoteam.ContainsKey(plr.Name))
                    {
                        playersintrgoteam.Add(plr.Name, true);
                    }
                    else
                    {
                        playersintrgoteam.Remove(plr.Name);
                    }
                    if(args.Parameters.Count > 1)
                    {
                        switch (args.Parameters[1])
                        {
                            case "ready":
                                playersintrgoteam[plr.Name] = playersintrgoteam[plr.Name] ? false : true;//切换准备状态
                                trgotean();
                                break;
                            default:
                                args.Player.SendInfoMessage("输入/trgo team ready 切换准备状态");
                                break;
                        }
                    }
                    break;
                case "bomb":
                    if (!playersintrgobomb.ContainsKey(plr.Name))
                    {
                        playersintrgobomb.Add(plr.Name, true);
                    }
                    else
                    {
                        playersintrgobomb.Remove(plr.Name);
                    }
                    if (args.Parameters.Count > 1)
                    {
                        switch (args.Parameters[1])
                        {
                            case "ready":
                                playersintrgobomb[plr.Name] = playersintrgobomb[plr.Name] ? false : true;//切换
                                break;
                            default:
                                args.Player.SendInfoMessage("输入/trgo bomb ready 切换准备状态");
                                break;
                        }
                    }
                    break;
                default:
                    args.Player.SendInfoMessage("请输入/trgo help 查看帮助");
                    break;
            }
        }

        private void trgotean()
        {
            // 获取加入游戏的玩家总数
            int joinedCount = playersintrgoteam.Count;

            // 获取已准备的玩家数量，直接根据字典中值为true来计数
            int readyCount = playersintrgoteam.Count(pair => pair.Value);

            if (joinedCount == 0)
            {
                return;
            }

            // 检查准备的玩家数量是否达到可开始游戏的条件（至少一半的玩家已准备）
            if (readyCount < joinedCount / 2)
            {
                return;
            }
        }
        private void trgobomb(CommandArgs args)
        {
        }




        private void trgoadmin(CommandArgs args)
        {

        }



        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }
    }
}