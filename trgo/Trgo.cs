using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;
using Trgo.Managers;

namespace Trgo
{
    [ApiVersion(2, 1)]
    public class Trgo : TerrariaPlugin
    {
        #region Plugin Properties
        public override string Name => "Trgo";
        public override string Author => "肝帝熙恩";
        public override string Description => "Trgo小游戏";
        public override Version Version => new Version(2, 2, 0);
        #endregion

        #region Private Fields
        private long _timerCount;
        private ConfigManager configManager;
        private BombManager bombManager;
        private GameManager gameManager;
        private TrgoEventListener eventListener;
        #endregion

        #region Constructor
        public Trgo(Main game) : base(game)
        {
        }
        #endregion

        #region Plugin Lifecycle
        public override void Initialize()
        {
            try
            {
                // 初始化管理器
                InitializeManagers();
                
                // 注册指令
                RegisterCommands();
                
                // 注册事件
                RegisterEvents();
                
                TShock.Log.Info("[Trgo] 插件初始化完成 - 支持自动下包和配置热重载");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 插件初始化失败: {ex.Message}");
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // 注销事件
                    UnregisterEvents();
                    
                    // 清理资源
                    eventListener?.Dispose();
                    
                    TShock.Log.Info("[Trgo] 插件已安全卸载");
                }
                catch (Exception ex)
                {
                    TShock.Log.Error($"[Trgo] 插件卸载时出错: {ex.Message}");
                }
            }
            
            base.Dispose(disposing);
        }
        #endregion

        #region Initialization Methods
        private void InitializeManagers()
        {
            configManager = new ConfigManager();
            bombManager = new BombManager(configManager);
            gameManager = new GameManager(configManager, bombManager);
            eventListener = new TrgoEventListener(gameManager);
        }

        private void RegisterCommands()
        {
            Commands.ChatCommands.Add(new Command("trgo.use", TrgoUse, "trgo"));
            Commands.ChatCommands.Add(new Command("trgo.admin", TrgoAdmin, "trgoadmin"));
        }

        private void RegisterEvents()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            GeneralHooks.ReloadEvent += OnReload;
        }

        private void UnregisterEvents()
        {
            ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            GeneralHooks.ReloadEvent -= OnReload;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 游戏更新事件 - 每秒执行游戏逻辑
        /// </summary>
        private void OnUpdate(EventArgs args)
        {
            _timerCount++;
            if (_timerCount % 60 == 0) // 每秒执行一次
            {
                gameManager.Update();
                
                // 检查自动下包/拆包
                CheckAutoBombActions();
            }
        }

        /// <summary>
        /// TShock重载事件 - 自动重载插件配置
        /// </summary>
        private void OnReload(ReloadEventArgs args)
        {
            try
            {
                configManager.ReloadConfig();
                TShock.Utils.Broadcast("[Trgo] 配置文件已通过系统重载自动更新", Color.Green);
                TShock.Log.Info("[Trgo] 配置已通过系统重载事件自动更新");
                args.Player?.SendSuccessMessage("[Trgo] 插件配置已重新加载");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 重载配置时出错: {ex.Message}");
                args.Player?.SendErrorMessage($"[Trgo] 配置重载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查自动下包/拆包 - 玩家站在爆破点自动触发
        /// </summary>
        private void CheckAutoBombActions()
        {
            try
            {
                // 只在爆破模式游戏进行中检查，且不在轮次等待期间
                if (gameManager.CurrentGameMode != GameManager.GameMode.BombDefusal ||
                    gameManager.CurrentGameState != GameManager.GameState.InProgress ||
                    IsInRoundWait())
                {
                    return;
                }

                var currentMap = configManager.GetCurrentMap();
                if (currentMap?.BombSites == null || !currentMap.BombSites.Any())
                {
                    TShock.Log.ConsoleDebug("[Trgo] 当前地图没有配置爆破点");
                    return;
                }

                var activePlayers = TShock.Players.Where(p => 
                    p?.Active == true && 
                    gameManager.PlayersInGame.ContainsKey(p.Name) &&
                    gameManager.PlayerTeams.ContainsKey(p.Name)).ToList();

                if (!activePlayers.Any())
                {
                    return;
                }

                TShock.Log.ConsoleDebug($"[Trgo] 检查自动下包: 活跃玩家数量 {activePlayers.Count}");

                foreach (var player in activePlayers)
                {
                    var playerPos = new Vector2(player.TileX, player.TileY);
                    var playerTeam = gameManager.PlayerTeams[player.Name];

                    TShock.Log.ConsoleDebug($"[Trgo] 玩家 {player.Name} ({playerTeam}队) 位置: ({player.TileX}, {player.TileY})");

                    // 检查每个爆破点
                    foreach (var bombSite in currentMap.BombSites)
                    {
                        if (bombSite.IsPlayerInRange(playerPos))
                        {
                            TShock.Log.ConsoleDebug($"[Trgo] 玩家 {player.Name} 在爆破点 {bombSite.Name} 范围内");

                            // 红队自动下包
                            if (playerTeam == "red" && !bombManager.IsBombPlanted)
                            {
                                if (!bombManager.IsPlayerBusy(player.Name))
                                {
                                    TShock.Log.ConsoleDebug($"[Trgo] 尝试为红队玩家 {player.Name} 开始下包");
                                    if (bombManager.StartPlanting(player))
                                    {
                                        player.SendInfoMessage($"你进入了 {bombSite.Name}，正在自动下包...");
                                        TShock.Utils.Broadcast($"警告：{player.Name} 正在 {bombSite.Name} 下包！", Color.Red);
                                        TShock.Log.ConsoleDebug($"[Trgo] 成功开始下包: {player.Name} 在 {bombSite.Name}");
                                    }
                                    else
                                    {
                                        TShock.Log.ConsoleDebug($"[Trgo] StartPlanting 返回 false: {player.Name}");
                                    }
                                }
                                else
                                {
                                    TShock.Log.ConsoleDebug($"[Trgo] 玩家 {player.Name} 正忙于其他操作");
                                }
                            }
                            // 蓝队自动拆包
                            else if (playerTeam == "blue" && bombManager.IsBombPlanted && 
                                    bombManager.ActiveBombSite?.Name == bombSite.Name)
                            {
                                if (!bombManager.IsPlayerBusy(player.Name))
                                {
                                    TShock.Log.ConsoleDebug($"[Trgo] 尝试为蓝队玩家 {player.Name} 开始拆包");
                                    if (bombManager.StartDefusing(player))
                                    {
                                        player.SendInfoMessage($"你正在 {bombSite.Name} 拆包...");
                                        TShock.Utils.Broadcast($"警告：{player.Name} 正在拆除炸弹！", Color.Blue);
                                        TShock.Log.ConsoleDebug($"[Trgo] 成功开始拆包: {player.Name} 在 {bombSite.Name}");
                                    }
                                    else
                                    {
                                        TShock.Log.ConsoleDebug($"[Trgo] StartDefusing 返回 false: {player.Name}");
                                    }
                                }
                                else
                                {
                                    TShock.Log.ConsoleDebug($"[Trgo] 玩家 {player.Name} 正忙于其他操作");
                                }
                            }
                            
                            break; // 找到一个爆破点就停止检查其他的
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] CheckAutoBombActions 出错: {ex.Message}");
                TShock.Log.Error($"[Trgo] 堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 检查是否处于轮次间等待状态
        /// </summary>
        private bool IsInRoundWait()
        {
            return gameManager.IsRoundWaiting;
        }
        #endregion

        #region Command Handlers
        /// <summary>
        /// 主要玩家指令处理
        /// </summary>
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
                    ShowHelp(player);
                    break;
                case "team":
                    TogglePlayerInGame(player, GameManager.GameMode.TeamDeathmatch);
                    break;
                case "boom":
                    TogglePlayerInGame(player, GameManager.GameMode.BombDefusal);
                    break;
                case "red":
                    SetPlayerTeam(player, "red");
                    break;
                case "blue":
                    SetPlayerTeam(player, "blue");
                    break;
                case "cancel":
                    CancelBombAction(player);
                    break;
                case "kill":
                    HandleManualKill(player, args);
                    break;
                case "status":
                    ShowGameStatus(player);
                    break;
                case "maps":
                    ShowMaps(player);
                    break;
                default:
                    player.SendInfoMessage("请输入 /trgo help 查看帮助");
                    break;
            }
        }

        /// <summary>
        /// 管理员指令处理
        /// </summary>
        private void TrgoAdmin(CommandArgs args)
        {
            var player = args.Player;

            if (args.Parameters.Count < 1)
            {
                ShowAdminHelp(player);
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "help":
                    ShowAdminHelp(player);
                    break;
                case "stop":
                    gameManager.ForceEndGame("管理员强制结束了游戏");
                    player.SendSuccessMessage("游戏已强制结束");
                    break;
                case "forcestart":
                    HandleForceStart(player);
                    break;
                case "map":
                    HandleMapCommand(player, args.Parameters.Skip(1).ToArray());
                    break;
                case "reload":
                    HandleConfigReload(player);
                    break;
                case "config":
                    HandleConfigCommand(player, args.Parameters.Skip(1).ToArray());
                    break;
                case "status":
                    ShowAdminStatus(player);
                    break;
                case "debug":
                    HandleDebugCommand(player);
                    break;
                case "endot":
                    HandleEndOvertimeCommand(player);
                    break;
                default:
                    player.SendInfoMessage("请输入 /trgoadmin help 查看帮助");
                    break;
            }
        }
        #endregion

        #region Help Methods
        private void ShowHelp(TSPlayer player)
        {
            player.SendInfoMessage("=== Trgo 游戏系统帮助 ===");
            player.SendInfoMessage("/trgo help - 查看帮助");
            player.SendInfoMessage("/trgo team - 加入或离开团队死斗模式");
            player.SendInfoMessage("/trgo boom - 加入或离开爆破模式");
            player.SendInfoMessage("/trgo red - 准备加入红队");
            player.SendInfoMessage("/trgo blue - 准备加入蓝队");
            player.SendInfoMessage("/trgo cancel - 取消当前炸弹操作");
            player.SendInfoMessage("/trgo status - 查看游戏状态");
            player.SendInfoMessage("/trgo maps - 查看地图列表");
            
            // 新功能提示
            player.SendInfoMessage("新功能：");
            player.SendInfoMessage("• 爆破模式：站在爆破点自动下包/拆包，无需输入指令！");
            player.SendInfoMessage("• 击杀检测：基于伤害追踪的准确击杀统计");
            player.SendInfoMessage("• 配置热重载：支持 /reload 命令自动更新配置");
            
            if (player.HasPermission("trgo.admin"))
            {
                player.SendInfoMessage("/trgo kill <玩家名> - 手动击杀玩家（测试用）");
                player.SendInfoMessage("管理员请使用 /trgoadmin help 查看管理指令");
            }
        }

        private void ShowAdminHelp(TSPlayer player)
        {
            player.SendInfoMessage("=== Trgo 管理员指令 ===");
            player.SendInfoMessage("/trgoadmin help - 查看管理员指令");
            player.SendInfoMessage("/trgoadmin stop - 强制结束游戏");
            player.SendInfoMessage("/trgoadmin forcestart - 强制开始游戏");
            player.SendInfoMessage("/trgoadmin map [list|set <名称>] - 地图管理");
            player.SendInfoMessage("/trgoadmin reload - 手动重新加载配置文件");
            player.SendInfoMessage("/trgoadmin config [show|set <配置项> <值>] - 配置管理");
            player.SendInfoMessage("/trgoadmin status - 查看详细游戏状态");
            player.SendInfoMessage("/trgoadmin debug - 查看调试信息");
            player.SendInfoMessage("/trgoadmin endot - 强制结束加时赛（紧急用）");
            player.SendInfoMessage("");
            player.SendInfoMessage("提示：配置文件使用中文配置项，更易理解！");
            player.SendInfoMessage("提示：系统 /reload 命令会自动重载 Trgo 配置！");
        }
        #endregion

        #region Game Action Methods
        private void TogglePlayerInGame(TSPlayer player, GameManager.GameMode mode)
        {
            if (!gameManager.TogglePlayerInGame(player.Name, mode))
            {
                player.SendErrorMessage("游戏进行中无法取消加入");
                return;
            }

            if (gameManager.PlayersInGame.ContainsKey(player.Name))
            {
                var modeText = mode == GameManager.GameMode.TeamDeathmatch ? "团队死斗模式" : "爆破模式";
                player.SendSuccessMessage($"你已加入{modeText}");
                TShock.Utils.Broadcast($"{player.Name} 加入了{modeText}", Color.Yellow);
                
                if (mode == GameManager.GameMode.BombDefusal)
                {
                    player.SendInfoMessage("提示：爆破模式中，站在爆破点即可自动下包/拆包！");
                }
            }
            else
            {
                player.SendInfoMessage("你已离开游戏模式");
                TShock.Utils.Broadcast($"{player.Name} 离开了游戏", Color.Gray);
            }
        }

        private void SetPlayerTeam(TSPlayer player, string team)
        {
            if (!gameManager.SetPlayerTeam(player.Name, team))
            {
                if (!gameManager.PlayersInGame.ContainsKey(player.Name))
                {
                    player.SendErrorMessage("你需要先加入游戏模式");
                }
                else
                {
                    player.SendErrorMessage("游戏进行中无法更换队伍");
                }
                return;
            }

            var teamText = team == "red" ? "红队" : "蓝队";
            var teamColor = team == "red" ? Color.Red : Color.Blue;
            
            player.SendSuccessMessage($"你已准备加入{teamText}");
            TShock.Utils.Broadcast($"{player.Name} 准备加入{teamText}", teamColor);
        }

        private void CancelBombAction(TSPlayer player)
        {
            if (bombManager.IsPlayerBusy(player.Name))
            {
                bombManager.CancelPlayerAction(player.Name);
                player.SendInfoMessage("已取消当前炸弹操作");
            }
            else
            {
                player.SendInfoMessage("你当前没有进行任何炸弹操作");
            }
        }

        private void HandleManualKill(TSPlayer killer, CommandArgs args)
        {
            if (!killer.HasPermission("trgo.admin"))
            {
                killer.SendErrorMessage("你没有权限使用此指令");
                return;
            }

            if (args.Parameters.Count < 2)
            {
                killer.SendErrorMessage("用法: /trgo kill <玩家名>");
                return;
            }

            var victimName = args.Parameters[1];
            var victim = TShock.Players.FirstOrDefault(p => p?.Name?.ToLower() == victimName.ToLower());
            
            if (victim == null)
            {
                killer.SendErrorMessage($"找不到玩家: {victimName}");
                return;
            }

            if (gameManager.CurrentGameState != GameManager.GameState.InProgress)
            {
                killer.SendErrorMessage("当前没有进行中的游戏");
                return;
            }

            if (!gameManager.PlayersInGame.ContainsKey(victim.Name))
            {
                killer.SendErrorMessage("只能击杀游戏中的玩家");
                return;
            }

            // 杀死玩家，让游戏的事件系统自动处理击杀统计
            victim.KillPlayer();
            killer.SendSuccessMessage($"已击杀玩家 {victimName}，击杀将由游戏系统自动统计");
        }
        #endregion

        #region Config and Map Handling
        private void HandleForceStart(TSPlayer player)
        {
            if (gameManager.ForceStartGame())
            {
                player.SendSuccessMessage("强制开始游戏倒计时");
                TShock.Utils.Broadcast("管理员强制开始游戏倒计时", Color.Orange);
            }
            else
            {
                player.SendErrorMessage("至少需要2名玩家才能开始游戏");
            }
        }

        private void HandleConfigReload(TSPlayer player)
        {
            try
            {
                configManager.ReloadConfig();
                player.SendSuccessMessage("配置文件已重新加载");
                TShock.Utils.Broadcast("管理员手动重新加载了游戏配置", Color.Cyan);
            }
            catch (Exception ex)
            {
                player.SendErrorMessage($"配置重载失败: {ex.Message}");
                TShock.Log.Error($"[Trgo] 手动配置重载失败: {ex.Message}");
            }
        }

        private void HandleMapCommand(TSPlayer player, string[] parameters)
        {
            if (parameters.Length == 0 || parameters[0].ToLower() == "list")
            {
                ShowMaps(player);
                return;
            }

            if (parameters[0].ToLower() == "set" && parameters.Length >= 2)
            {
                var mapName = parameters[1];
                if (configManager.SetCurrentMap(mapName))
                {
                    player.SendSuccessMessage($"当前地图已切换为: {mapName}");
                    TShock.Utils.Broadcast($"管理员将地图切换为: {mapName}", Color.Cyan);
                }
                else
                {
                    player.SendErrorMessage($"找不到地图: {mapName}");
                }
                return;
            }

            player.SendErrorMessage("用法: /trgoadmin map [list|set <名称>]");
        }

        private void HandleConfigCommand(TSPlayer player, string[] parameters)
        {
            if (parameters.Length == 0 || parameters[0].ToLower() == "show")
            {
                ShowConfigInfo(player);
                return;
            }

            if (parameters[0].ToLower() == "set" && parameters.Length >= 3)
            {
                var configKey = parameters[1].ToLower();
                var configValue = parameters[2];
                
                if (SetConfigValue(configKey, configValue, player))
                {
                    configManager.SaveConfig();
                    player.SendSuccessMessage($"配置 {configKey} 已设置为 {configValue}");
                    TShock.Utils.Broadcast($"管理员修改了游戏配置", Color.Cyan);
                }
                return;
            }

            player.SendErrorMessage("用法: /trgoadmin config [show|set <配置项> <值>]");
        }

        private void ShowConfigInfo(TSPlayer player)
        {
            var config = configManager.GetConfig();
            player.SendInfoMessage("=== 当前配置 ===");
            player.SendInfoMessage($"团队死斗获胜击杀数: {config.TeamDeathmatch.KillsToWin}");
            player.SendInfoMessage($"爆破模式获胜轮数: {config.BombDefusal.RoundsToWin} (CS:GO式规则)");
            player.SendInfoMessage($"  - 总回合数: {(config.BombDefusal.RoundsToWin - 1) * 2} 回合制");
            player.SendInfoMessage($"  - 半场轮数: {config.BombDefusal.RoundsToWin - 1} 轮后换边");
            player.SendInfoMessage($"  - 加时规则: 连续2轮获胜，每2轮换边");
            player.SendInfoMessage($"炸弹爆炸时间: {config.BombDefusal.BombTimer}秒");
            player.SendInfoMessage($"下包时间: {config.BombDefusal.PlantTime}秒");
            player.SendInfoMessage($"拆包时间: {config.BombDefusal.DefuseTime}秒");
            player.SendInfoMessage($"轮次间等待时间: {config.BombDefusal.RoundWaitTime}秒");
            player.SendInfoMessage($"开始倒计时: {config.General.CountdownTime}秒");
            player.SendInfoMessage($"最少开始人数: {config.General.MinPlayersToStart}");
            player.SendInfoMessage($"隐身范围: {config.General.InvisibilityRange}格");
            player.SendInfoMessage("");
            player.SendInfoMessage("提示: 配置文件支持中文配置项，直接编辑更直观!");
            player.SendInfoMessage("例如：获胜轮数=13 → 24回合制，12轮后换边，平局进入加时");
        }

        private void ShowAdminStatus(TSPlayer player)
        {
            ShowGameStatus(player);
            
            var config = configManager.GetConfig();
            var currentMap = configManager.GetCurrentMap();
            
            player.SendInfoMessage("=== 管理员状态 ===");
            player.SendInfoMessage($"配置文件: {(config != null ? "已加载" : "未加载")}");
            player.SendInfoMessage($"当前地图文件: {config.CurrentMap}");
            player.SendInfoMessage($"地图爆破点数量: {currentMap.BombSites.Count}");
            player.SendInfoMessage($"红队出生点: ({currentMap.RedSpawn.X}, {currentMap.RedSpawn.Y})");
            player.SendInfoMessage($"蓝队出生点: ({currentMap.BlueSpawn.X}, {currentMap.BlueSpawn.Y})");
            
            foreach (var site in currentMap.BombSites)
            {
                player.SendInfoMessage($"{site.Name}: ({site.BoundingBox.TopLeft.X}, {site.BoundingBox.TopLeft.Y}) - ({site.BoundingBox.BottomRight.X}, {site.BoundingBox.BottomRight.Y})");
            }
            
            player.SendInfoMessage($"事件监听器: {(eventListener != null ? "正常" : "异常")}");
            player.SendInfoMessage($"游戏更新计数: {_timerCount}");
        }
        #endregion

        #region Configuration Methods
        private bool SetConfigValue(string key, string value, TSPlayer player)
        {
            var config = configManager.GetConfig();
            
            try
            {
                switch (key)
                {
                    case "killstowin":
                    case "获胜击杀数":
                        config.TeamDeathmatch.KillsToWin = int.Parse(value);
                        break;
                    case "roundstowin":
                    case "获胜轮数":
                        config.BombDefusal.RoundsToWin = int.Parse(value);
                        break;
                    case "bombtimer":
                    case "炸弹爆炸时间":
                        config.BombDefusal.BombTimer = int.Parse(value);
                        break;
                    case "planttime":
                    case "下包时间":
                        config.BombDefusal.PlantTime = int.Parse(value);
                        break;
                    case "defusetime":
                    case "拆包时间":
                        config.BombDefusal.DefuseTime = int.Parse(value);
                        break;
                    case "roundwaittime":
                    case "轮次间等待时间":
                        config.BombDefusal.RoundWaitTime = int.Parse(value);
                        break;
                    case "countdown":
                    case "开始倒计时":
                        config.General.CountdownTime = int.Parse(value);
                        break;
                    case "minplayers":
                    case "最少人数":
                        config.General.MinPlayersToStart = int.Parse(value);
                        break;
                    case "invisrange":
                    case "隐身范围":
                        config.General.InvisibilityRange = int.Parse(value);
                        break;
                    default:
                        player.SendErrorMessage($"未知的配置项: {key}");
                        player.SendInfoMessage("支持的配置项: killstowin, roundstowin, bombtimer, planttime, defusetime, roundwaittime, countdown, minplayers, invisrange");
                        player.SendInfoMessage("或使用中文: 获胜击杀数, 获胜轮数, 炸弹爆炸时间, 下包时间, 拆包时间, 轮次间等待时间, 开始倒计时, 最少人数, 隐身范围");
                        return false;
                }
                return true;
            }
            catch (FormatException)
            {
                player.SendErrorMessage($"配置值格式错误: {value}");
                return false;
            }
            catch (Exception ex)
            {
                player.SendErrorMessage($"设置配置时出错: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// 获取游戏状态的中文描述
        /// </summary>
        private string GetGameStateText(GameManager.GameState state)
        {
            return state switch
            {
                GameManager.GameState.Waiting => "等待玩家",
                GameManager.GameState.Countdown => "准备开始",
                GameManager.GameState.InProgress => "游戏中",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取游戏模式的中文描述
        /// </summary>
        private string GetGameModeText(GameManager.GameMode mode)
        {
            return mode switch
            {
                GameManager.GameMode.TeamDeathmatch => "团队死斗",
                GameManager.GameMode.BombDefusal => "爆破模式",
                GameManager.GameMode.None => "无",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取队伍标签（包含换边信息）
        /// </summary>
        private string GetTeamLabel(string currentTeam)
        {
            // 这里需要访问gameManager的私有字段来判断是否换边
            // 简化处理：检查是否处于游戏中且可能已换边
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal && 
                gameManager.CurrentGameState == GameManager.GameState.InProgress)
            {
                var totalRounds = gameManager.RedRounds + gameManager.BlueRounds;
                var config = configManager.GetBombDefusalConfig();
                var halfRounds = config.RoundsToWin - 1;
                
                // 简单判断：如果总轮数超过半场，可能已换边
                if (totalRounds > halfRounds)
                {
                    return currentTeam == "red" ? "红队位置 [可能是原蓝队]" : "蓝队位置 [可能是原红队]";
                }
            }
            
            return currentTeam == "red" ? "红队" : "蓝队";
        }

        /// <summary>
        /// 显示游戏状态信息
        /// </summary>
        private void ShowGameStatus(TSPlayer player)
        {
            var gameState = GetGameStateText(gameManager.CurrentGameState);
            var gameMode = GetGameModeText(gameManager.CurrentGameMode);
            
            player.SendInfoMessage("=== 游戏状态 ===");
            player.SendInfoMessage($"当前模式: {gameMode}");
            player.SendInfoMessage($"游戏状态: {gameState}");
            player.SendInfoMessage($"参与玩家: {gameManager.PlayersInGame.Count}");
            
            if (gameManager.PlayersInGame.Any())
            {
                player.SendInfoMessage("--- 参与玩家 ---");
                var redPlayers = gameManager.PlayerTeams.Where(p => p.Value == "red").ToList();
                var bluePlayers = gameManager.PlayerTeams.Where(p => p.Value == "blue").ToList();
                
                if (redPlayers.Any())
                {
                    var redNames = string.Join(", ", redPlayers.Select(p => p.Key));
                    var redTeamLabel = GetTeamLabel("red");
                    player.SendInfoMessage($"{redTeamLabel} ({redPlayers.Count}): {redNames}");
                }
                
                if (bluePlayers.Any())
                {
                    var blueNames = string.Join(", ", bluePlayers.Select(p => p.Key));
                    var blueTeamLabel = GetTeamLabel("blue");
                    player.SendInfoMessage($"{blueTeamLabel} ({bluePlayers.Count}): {blueNames}");
                }
                
                // 显示比分统计
                if (gameManager.CurrentGameState == GameManager.GameState.InProgress)
                {
                    player.SendInfoMessage("--- 比分统计 ---");
                    
                    if (gameManager.CurrentGameMode == GameManager.GameMode.TeamDeathmatch)
                    {
                        var redKills = redPlayers.Sum(p => gameManager.PlayerKills.GetValueOrDefault(p.Key, 0));
                        var blueKills = bluePlayers.Sum(p => gameManager.PlayerKills.GetValueOrDefault(p.Key, 0));
                        player.SendInfoMessage($"红队击杀: {redKills} | 蓝队击杀: {blueKills}");
                    }
                    else if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
                    {
                        player.SendInfoMessage($"第 {gameManager.CurrentRound} 轮");
                        player.SendInfoMessage($"原红队胜局: {gameManager.RedRounds} | 原蓝队胜局: {gameManager.BlueRounds}");
                        
                        var bombManager = gameManager.GetBombManager();
                        if (bombManager.IsBombPlanted)
                        {
                            player.SendInfoMessage($"炸弹已下包 - 剩余时间: {bombManager.BombTimer}秒");
                        }
                    }
                }
            }
            
            // 显示当前地图信息
            var config = configManager.GetConfig();
            var currentMap = configManager.GetCurrentMap();
            player.SendInfoMessage($"当前地图: {config.CurrentMap} ({currentMap.Name})");
        }

        /// <summary>
        /// 显示可用地图列表
        /// </summary>
        private void ShowMaps(TSPlayer player)
        {
            var config = configManager.GetConfig();
            
            player.SendInfoMessage("=== 可用地图 ===");
            
            if (config.Maps.Any())
            {
                foreach (var map in config.Maps)
                {
                    var marker = map.Key == config.CurrentMap ? "[当前]" : "[可用]";
                    player.SendInfoMessage($"{marker} {map.Key} - {map.Value.Name}");
                    if (!string.IsNullOrEmpty(map.Value.Description))
                    {
                        player.SendInfoMessage($"   描述: {map.Value.Description}");
                    }
                    player.SendInfoMessage($"   爆破点数量: {map.Value.BombSites.Count}");
                }
            }
            else
            {
                player.SendErrorMessage("未找到任何地图配置");
                player.SendInfoMessage("请管理员检查地图配置文件");
            }
            
            player.SendInfoMessage("管理员可使用 /trgoadmin map set <地图名> 切换地图");
        }
        #endregion

        /// <summary>
        /// 处理调试命令
        /// </summary>
        private void HandleDebugCommand(TSPlayer player)
        {
            if (!player.HasPermission("trgo.admin"))
            {
                player.SendErrorMessage("你没有权限使用此指令");
                return;
            }

            player.SendInfoMessage("=== Trgo 调试信息 ===");
            player.SendInfoMessage($"当前游戏模式: {gameManager.CurrentGameMode}");
            player.SendInfoMessage($"当前游戏状态: {gameManager.CurrentGameState}");
            player.SendInfoMessage($"游戏中玩家数量: {gameManager.PlayersInGame.Count}");
            player.SendInfoMessage($"已分配队伍玩家数量: {gameManager.PlayerTeams.Count}");
            player.SendInfoMessage($"游戏更新计数: {_timerCount}");

            var currentMap = configManager.GetCurrentMap();
            player.SendInfoMessage($"当前地图: {configManager.GetConfig().CurrentMap}");
            player.SendInfoMessage($"地图爆破点数量: {currentMap?.BombSites?.Count ?? 0}");

            if (currentMap?.BombSites?.Any() == true)
            {
                foreach (var site in currentMap.BombSites)
                {
                    player.SendInfoMessage($"  爆破点 {site.Name}: ({site.BoundingBox.TopLeft.X}-{site.BoundingBox.BottomRight.X}, {site.BoundingBox.TopLeft.Y}-{site.BoundingBox.BottomRight.Y})");
                }
            }

            player.SendInfoMessage($"炸弹是否已下包: {bombManager.IsBombPlanted}");
            
            // 添加队伍换边调试信息
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                var totalRounds = gameManager.RedRounds + gameManager.BlueRounds;
                var bombConfig = configManager.GetBombDefusalConfig();
                var halfRounds = bombConfig.RoundsToWin - 1;
                
                player.SendInfoMessage($"--- 队伍换边调试信息 ---");
                player.SendInfoMessage($"总轮数: {totalRounds}, 半场轮数: {halfRounds}");
                player.SendInfoMessage($"是否应该换边: {totalRounds >= halfRounds}");
                player.SendInfoMessage($"当前比分计入: 红队(原始) {gameManager.RedRounds}轮, 蓝队(原始) {gameManager.BlueRounds}轮");
                
                // 添加加时赛调试信息
                if (totalRounds >= halfRounds * 2) // 可能在加时赛
                {
                    player.SendInfoMessage($"--- 加时赛调试信息 ---");
                    player.SendInfoMessage($"是否加时赛: 可能");
                    player.SendInfoMessage($"常规比赛最大轮数: {halfRounds * 2}");
                    
                    // 这里无法直接访问私有字段，但可以通过总轮数推测
                    if (totalRounds > halfRounds * 2)
                    {
                        var overtimeRounds = totalRounds - (halfRounds * 2);
                        player.SendInfoMessage($"可能的加时赛轮数: {overtimeRounds}");
                        player.SendInfoMessage($"注意: 加时赛每2轮换边，连续2轮获胜则结束");
                    }
                }
            }
            
            // 添加倒计时调试信息
            var config = configManager.GetGeneralConfig();
            player.SendInfoMessage($"配置 - 最少开始人数: {config.MinPlayersToStart}");
            player.SendInfoMessage($"配置 - 准备玩家比例: {config.ReadyPlayersRatio}");
            player.SendInfoMessage($"配置 - 开始倒计时时间: {config.CountdownTime}");
            
            if (gameManager.PlayersInGame.Any())
            {
                var readyPlayersCount = gameManager.PlayerTeams.Count;
                var totalPlayersCount = gameManager.PlayersInGame.Count;
                var requiredReadyPlayers = (int)(totalPlayersCount * config.ReadyPlayersRatio);
                
                player.SendInfoMessage($"已准备玩家: {readyPlayersCount}/{totalPlayersCount}");
                player.SendInfoMessage($"需要准备玩家数: {requiredReadyPlayers}");
                player.SendInfoMessage($"满足开始条件: {readyPlayersCount >= requiredReadyPlayers && totalPlayersCount >= config.MinPlayersToStart}");
                
                player.SendInfoMessage("--- 游戏中玩家详情 ---");
                foreach (var kvp in gameManager.PlayersInGame)
                {
                    var playerName = kvp.Key;
                    var tsPlayer = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                    var team = gameManager.PlayerTeams.ContainsKey(playerName) ? gameManager.PlayerTeams[playerName] : "未分配";
                    var isActive = tsPlayer?.Active == true;
                    var position = isActive ? $"({tsPlayer.TileX}, {tsPlayer.TileY})" : "离线";
                    
                    player.SendInfoMessage($"  {playerName}: {team}队, 状态: {(isActive ? "在线" : "离线")}, 位置: {position}");
                }
            }

            player.SendInfoMessage("调试信息已显示，请查看控制台日志获取更多详细信息");
            player.SendInfoMessage("如果游戏卡在倒计时状态，请尝试使用 /trgoadmin stop 然后重新开始");
        }

        /// <summary>
        /// 处理强制结束加时赛命令
        /// </summary>
        private void HandleEndOvertimeCommand(TSPlayer player)
        {
            if (!player.HasPermission("trgo.admin"))
            {
                player.SendErrorMessage("你没有权限使用此指令");
                return;
            }

            if (gameManager.CurrentGameMode != GameManager.GameMode.BombDefusal ||
                gameManager.CurrentGameState != GameManager.GameState.InProgress)
            {
                player.SendErrorMessage("当前没有进行爆破模式游戏");
                return;
            }

            // 检查是否在加时赛
            var totalRounds = gameManager.RedRounds + gameManager.BlueRounds;
            var config = configManager.GetBombDefusalConfig();
            var maxRounds = (config.RoundsToWin - 1) * 2;

            if (totalRounds <= maxRounds)
            {
                player.SendErrorMessage("当前不在加时赛中");
                return;
            }

            // 强制结束加时赛，比分高的队伍获胜
            if (gameManager.RedRounds > gameManager.BlueRounds)
            {
                gameManager.ForceEndGame($"管理员强制结束加时赛！红队获胜 {gameManager.RedRounds}:{gameManager.BlueRounds}");
                player.SendSuccessMessage("已强制结束加时赛，红队获胜");
            }
            else if (gameManager.BlueRounds > gameManager.RedRounds)
            {
                gameManager.ForceEndGame($"管理员强制结束加时赛！蓝队获胜 {gameManager.RedRounds}:{gameManager.BlueRounds}");
                player.SendSuccessMessage("已强制结束加时赛，蓝队获胜");
            }
            else
            {
                // 平局的情况
                gameManager.ForceEndGame($"管理员强制结束加时赛！平局 {gameManager.RedRounds}:{gameManager.BlueRounds}");
                player.SendSuccessMessage("已强制结束加时赛，比赛平局");
            }

            TShock.Utils.Broadcast("管理员强制结束了加时赛", Color.Orange);
        }
    }
}
