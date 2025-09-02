using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TShockAPI;
using Trgo.Config;
using Terraria;
using Terraria.DataStructures;

namespace Trgo.Managers
{
    /// <summary>
    /// 游戏管理器
    /// </summary>
    public class GameManager
    {
        private readonly ConfigManager configManager;
        private readonly BombManager bombManager;

        // 游戏状态
        public enum GameMode { None, TeamDeathmatch, BombDefusal }
        public enum GameState { Waiting, Countdown, InProgress }

        public GameMode CurrentGameMode { get; private set; } = GameMode.None;
        public GameState CurrentGameState { get; private set; } = GameState.Waiting;

        // 玩家数据
        public Dictionary<string, GameMode> PlayersInGame { get; private set; } = new Dictionary<string, GameMode>();
        public Dictionary<string, string> PlayerTeams { get; private set; } = new Dictionary<string, string>();
        public Dictionary<string, int> PlayerKills { get; private set; } = new Dictionary<string, int>();
        public Dictionary<string, int> PlayerDeaths { get; private set; } = new Dictionary<string, int>();

        // 游戏计时
        private int countdown = -1;
        private int gameTimer = 0;

        // 爆破模式数据 - 新增加时赛和换边机制
        private int redRounds = 0;
        private int blueRounds = 0;
        private int currentRound = 1;
        private int roundWaitTimer = -1; // 轮次间等待计时器
        private bool isOvertime = false; // 是否处于加时赛
        private int overtimeRounds = 0; // 加时赛轮数
        private bool teamsSwapped = false; // 是否已换边
        
        // 原始队伍身份追踪 - 用于正确计分
        private readonly Dictionary<string, string> originalTeamIdentity = new Dictionary<string, string>();
        
        // 加时赛胜利历史追踪 - 用于连续胜利判断
        private readonly List<string> overtimeWinHistory = new List<string>();

        // 隐身系统
        private readonly Dictionary<string, bool> playersInInvisRange = new Dictionary<string, bool>();

        public GameManager(ConfigManager configManager, BombManager bombManager)
        {
            this.configManager = configManager;
            this.bombManager = bombManager;
        }

        /// <summary>
        /// 获取当前轮次
        /// </summary>
        public int CurrentRound => currentRound;

        /// <summary>
        /// 获取红队轮次
        /// </summary>
        public int RedRounds => redRounds;

        /// <summary>
        /// 获取蓝队轮次
        /// </summary>
        public int BlueRounds => blueRounds;

        /// <summary>
        /// 每秒更新游戏逻辑
        /// </summary>
        public void Update()
        {
            HandleCountdown();
            HandleGameLogic();
            HandleInvisibilityLogic();
        }

        /// <summary>
        /// 处理倒计时
        /// </summary>
        private void HandleCountdown()
        {
            if (countdown > 0)
            {
                countdown--;
                TShock.Utils.Broadcast($"游戏将在 {countdown} 秒后开始", Color.Green);
                if (countdown == 0)
                {
                    StartGame();
                }
            }
            else if (countdown == -1 && ShouldStartCountdown())
            {
                var config = configManager.GetGeneralConfig();
                countdown = config.CountdownTime;
                TShock.Utils.Broadcast($"准备人数已达标，游戏将在 {countdown} 秒后开始", Color.Green);
                CurrentGameState = GameState.Countdown;
            }
        }

        /// <summary>
        /// 检查是否应该开始倒计时
        /// </summary>
        private bool ShouldStartCountdown()
        {
            if (CurrentGameState != GameState.Waiting) return false;

            var config = configManager.GetGeneralConfig();
            var readyPlayers = PlayersInGame.Where(p => PlayerTeams.ContainsKey(p.Key)).Count();
            var totalPlayers = PlayersInGame.Count;

            return readyPlayers >= (int)(totalPlayers * config.ReadyPlayersRatio) && 
                   totalPlayers >= config.MinPlayersToStart;
        }

        /// <summary>
        /// 处理游戏逻辑
        /// </summary>
        private void HandleGameLogic()
        {
            if (CurrentGameState != GameState.InProgress) return;

            gameTimer++;

            // 处理轮次间等待
            if (roundWaitTimer > 0)
            {
                roundWaitTimer--;
                if (roundWaitTimer > 0)
                {
                    TShock.Utils.Broadcast($"下一轮将在 {roundWaitTimer} 秒后开始", Color.Yellow);
                    return;
                }
                else
                {
                    // 等待结束，开始下一轮
                    StartNextRound();
                    return;
                }
            }

            if (CurrentGameMode == GameMode.TeamDeathmatch)
            {
                CheckTeamDeathmatchWin();
            }
            else if (CurrentGameMode == GameMode.BombDefusal)
            {
                HandleBombDefusalLogic();
            }
        }

        /// <summary>
        /// 处理爆破模式逻辑
        /// </summary>
        private void HandleBombDefusalLogic()
        {
            var result = bombManager.Update();

            if (!string.IsNullOrEmpty(result.WarningMessage))
            {
                TShock.Utils.Broadcast(result.WarningMessage, Color.Red);
            }

            if (result.BombPlanted)
            {
                TShock.Utils.Broadcast(result.Message, Color.Red);
            }
            else if (result.BombDefused)
            {
                EndRound("blue", result.Message);
            }
            else if (result.BombExploded)
            {
                EndRound("red", result.Message);
            }

            // 检查团队消灭胜利条件
            CheckTeamEliminationWin();
        }

        /// <summary>
        /// 检查团队消灭胜利条件
        /// </summary>
        private void CheckTeamEliminationWin()
        {
            if (CurrentGameMode != GameMode.BombDefusal) return;

            var alivePlayers = TShock.Players.Where(p => 
                p?.Active == true && 
                PlayersInGame.ContainsKey(p.Name) && 
                p.TPlayer.statLife > 0).ToList();

            var aliveRed = alivePlayers.Where(p => PlayerTeams.GetValueOrDefault(p.Name) == "red").ToList();
            var aliveBlue = alivePlayers.Where(p => PlayerTeams.GetValueOrDefault(p.Name) == "blue").ToList();

            if (aliveRed.Count == 0 && aliveBlue.Count > 0)
            {
                if (bombManager.IsBombPlanted)
                {
                    // 如果炸弹已下包，蓝队需要拆包才能获胜
                    return;
                }
                EndRound("blue", "蓝队全歼红队获胜本轮！");
            }
            else if (aliveBlue.Count == 0 && aliveRed.Count > 0)
            {
                EndRound("red", "红队全歼蓝队获胜本轮！");
            }
        }

        /// <summary>
        /// 处理隐身逻辑
        /// </summary>
        private void HandleInvisibilityLogic()
        {
            if (CurrentGameMode != GameMode.BombDefusal || CurrentGameState != GameState.InProgress) return;

            var config = configManager.GetGeneralConfig();
            var players = TShock.Players.Where(p => p?.Active == true && PlayersInGame.ContainsKey(p.Name)).ToList();

            foreach (var player in players)
            {
                bool shouldBeInvisible = true;
                var playerPos = new Vector2(player.TileX, player.TileY);

                // 检查是否有敌人在范围内
                foreach (var otherPlayer in players)
                {
                    if (player == otherPlayer) continue;
                    if (!PlayerTeams.ContainsKey(player.Name) || !PlayerTeams.ContainsKey(otherPlayer.Name)) continue;
                    if (PlayerTeams[player.Name] == PlayerTeams[otherPlayer.Name]) continue;

                    var otherPos = new Vector2(otherPlayer.TileX, otherPlayer.TileY);
                    var distance = Vector2.Distance(playerPos, otherPos);

                    if (distance <= config.InvisibilityRange)
                    {
                        shouldBeInvisible = false;
                        break;
                    }
                }

                // 应用或移除隐身
                if (shouldBeInvisible && !playersInInvisRange.GetValueOrDefault(player.Name))
                {
                    player.SetBuff(14, config.InvisibilityDuration);
                    playersInInvisRange[player.Name] = true;
                }
                else if (!shouldBeInvisible && playersInInvisRange.GetValueOrDefault(player.Name))
                {
                    player.SetBuff(14, 0);
                    playersInInvisRange[player.Name] = false;
                }
            }
        }

        /// <summary>
        /// 检查团队死斗胜利条件
        /// </summary>
        private void CheckTeamDeathmatchWin()
        {
            var config = configManager.GetTeamDeathmatchConfig();
            var redKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "red").Sum(p => p.Value);
            var blueKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "blue").Sum(p => p.Value);

            if (redKills >= config.KillsToWin)
            {
                EndGame("red", $"红队获胜！击杀数: {redKills}");
            }
            else if (blueKills >= config.KillsToWin)
            {
                EndGame("blue", $"蓝队获胜！击杀数: {blueKills}");
            }
        }

        /// <summary>
        /// 玩家加入游戏
        /// </summary>
        public bool TogglePlayerInGame(string playerName, GameMode mode)
        {
            if (CurrentGameState == GameState.InProgress)
            {
                return false; // 游戏进行中无法加入
            }

            if (!PlayersInGame.ContainsKey(playerName))
            {
                PlayersInGame.Add(playerName, mode);
                CurrentGameMode = mode;
                return true;
            }
            else
            {
                PlayersInGame.Remove(playerName);
                if (PlayerTeams.ContainsKey(playerName))
                {
                    PlayerTeams.Remove(playerName);
                }

                if (PlayersInGame.Count == 0)
                {
                    CurrentGameMode = GameMode.None;
                }
                return true;
            }
        }

        /// <summary>
        /// 设置玩家队伍
        /// </summary>
        public bool SetPlayerTeam(string playerName, string team)
        {
            if (!PlayersInGame.ContainsKey(playerName) || CurrentGameState == GameState.InProgress)
            {
                return false;
            }

            PlayerTeams[playerName] = team;
            return true;
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        private void StartGame()
        {
            AssignTeams();
            InitializePlayerStats();
            TeleportPlayersAndSetEquipment();

            CurrentGameState = GameState.InProgress;
            gameTimer = 0;

            var modeText = CurrentGameMode == GameMode.TeamDeathmatch ? "团队死斗" : "爆破模式";
            TShock.Utils.Broadcast($"=== {modeText}游戏开始！ ===", Color.Green);

            if (CurrentGameMode == GameMode.BombDefusal)
            {
                var config = configManager.GetBombDefusalConfig();
                TShock.Utils.Broadcast("红队（攻方）需要在爆破点下包或消灭所有守方", Color.Yellow);
                TShock.Utils.Broadcast("蓝队（守方）需要阻止下包、拆除炸弹或消灭所有攻方", Color.Yellow);
                TShock.Utils.Broadcast($"爆破模式规则: 先赢得 {config.RoundsToWin} 轮的队伍获胜", Color.Cyan);
            }
            else
            {
                var config = configManager.GetTeamDeathmatchConfig();
                TShock.Utils.Broadcast($"团队死斗规则: 先达到 {config.KillsToWin} 击杀的队伍获胜", Color.Cyan);
            }
        }

        /// <summary>
        /// 分配队伍
        /// </summary>
        private void AssignTeams()
        {
            var allPlayers = PlayersInGame.Keys.ToList();
            var redTeam = PlayerTeams.Where(p => p.Value == "red").Select(p => p.Key).ToList();
            var blueTeam = PlayerTeams.Where(p => p.Value == "blue").Select(p => p.Key).ToList();

            int totalPlayers = allPlayers.Count;
            int maxTeamSize = totalPlayers % 2 == 0 ? totalPlayers / 2 : (totalPlayers / 2) + 1;

            // 平衡队伍
            var unassignedPlayers = allPlayers.Except(redTeam).Except(blueTeam).ToList();

            // 如果某队人数过多，移动玩家
            while (redTeam.Count > maxTeamSize)
            {
                var playerToMove = redTeam.Last();
                redTeam.Remove(playerToMove);
                blueTeam.Add(playerToMove);
                PlayerTeams[playerToMove] = "blue";
            }

            while (blueTeam.Count > maxTeamSize)
            {
                var playerToMove = blueTeam.Last();
                blueTeam.Remove(playerToMove);
                redTeam.Add(playerToMove);
                PlayerTeams[playerToMove] = "red";
            }

            // 分配未分配的玩家
            foreach (var player in unassignedPlayers)
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

            TShock.Utils.Broadcast($"红队 ({redTeam.Count}人): {string.Join(", ", redTeam)}", Color.Red);
            TShock.Utils.Broadcast($"蓝队 ({blueTeam.Count}人): {string.Join(", ", blueTeam)}", Color.Blue);
        }

        /// <summary>
        /// 初始化玩家统计
        /// </summary>
        private void InitializePlayerStats()
        {
            PlayerKills.Clear();
            PlayerDeaths.Clear();

            foreach (var player in PlayersInGame.Keys)
            {
                PlayerKills[player] = 0;
                PlayerDeaths[player] = 0;
            }

            if (CurrentGameMode == GameMode.BombDefusal)
            {
                redRounds = 0;
                blueRounds = 0;
                currentRound = 1;
                roundWaitTimer = -1; // 重置轮次等待计时器
                isOvertime = false; // 重置加时赛状态
                overtimeRounds = 0;
                teamsSwapped = false;
                
                // 初始化原始队伍身份
                originalTeamIdentity.Clear();
                foreach (var kvp in PlayerTeams)
                {
                    originalTeamIdentity[kvp.Key] = kvp.Value; // 记录每个玩家的原始队伍
                }
                
                bombManager.Reset();
                playersInInvisRange.Clear();
            }
        }

        /// <summary>
        /// 传送玩家并设置装备
        /// </summary>
        private void TeleportPlayersAndSetEquipment()
        {
            try
            {
                var map = configManager.GetCurrentMap();
                var config = configManager.GetGeneralConfig();

                foreach (var kvp in PlayerTeams)
                {
                    var playerName = kvp.Key;
                    var team = kvp.Value;
                    var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);

                    if (player?.Active == true)
                    {
                        try
                        {
                            // 清除玩家背包（防止装备堆积）- 安全地访问背包
                            int maxInventorySize = Math.Min(player.TPlayer.inventory.Length, 58);
                            for (int i = 0; i < maxInventorySize; i++)
                            {
                                if (i < player.TPlayer.inventory.Length)
                                {
                                    player.TPlayer.inventory[i].SetDefaults();
                                }
                            }

                            // 根据队伍设置队伍颜色和传送到对应出生点
                            if (team == "red")
                            {
                                player.SetTeam(1); // 红队
                                player.Teleport((int)map.RedSpawn.X * 16, (int)map.RedSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] 传送红队玩家 {playerName} 到坐标 ({map.RedSpawn.X}, {map.RedSpawn.Y})");
                            }
                            else if (team == "blue")
                            {
                                player.SetTeam(3); // 蓝队
                                player.Teleport((int)map.BlueSpawn.X * 16, (int)map.BlueSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] 传送蓝队玩家 {playerName} 到坐标 ({map.BlueSpawn.X}, {map.BlueSpawn.Y})");
                            }

                            // 发放初始装备
                            foreach (var item in config.InitialItems)
                            {
                                player.GiveItem(item.Id, item.Stack, item.Prefix);
                            }

                            // 确保玩家满血满魔
                            player.Heal(player.TPlayer.statLifeMax);
                            player.TPlayer.statMana = player.TPlayer.statManaMax2;
                            player.SendData(PacketTypes.PlayerMana, "", player.Index);
                            
                            // 发送背包更新 - 安全地发送网络数据
                            int inventorySize = Math.Min(player.TPlayer.inventory.Length, NetItem.MaxInventory);
                            for (int i = 0; i < inventorySize; i++)
                            {
                                if (i < player.TPlayer.inventory.Length)
                                {
                                    NetMessage.SendData(5, -1, -1, null, player.Index, i, player.TPlayer.inventory[i].prefix);
                                }
                            }

                            player.SendInfoMessage($"已传送到{(team == "red" ? "红队" : "蓝队")}出生点并分发装备");
                        }
                        catch (Exception ex)
                        {
                            TShock.Log.Error($"[Trgo] 传送玩家 {playerName} 时出错: {ex.Message}");
                            player.SendErrorMessage("传送过程中出现错误，请联系管理员");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] TeleportPlayersAndSetEquipment 总体异常: {ex.Message}");
                TShock.Log.Error($"[Trgo] 堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 结束轮次 - 实现CS:GO式比赛规则（修复换边计分问题）
        /// </summary>
        private void EndRound(string winningTeam, string message)
        {
            TShock.Utils.Broadcast($"=== {message} ===", winningTeam == "red" ? Color.Red : Color.Blue);

            // 根据获胜队伍的原始身份进行计分
            var originalWinningTeam = GetOriginalTeamForCurrentWinner(winningTeam);
            
            // 记录加时赛胜利历史
            if (isOvertime)
            {
                overtimeWinHistory.Add(originalWinningTeam);
                TShock.Log.ConsoleDebug($"[Trgo] 加时赛胜利记录: {originalWinningTeam}, 历史: [{string.Join(", ", overtimeWinHistory)}]");
            }
            
            if (originalWinningTeam == "red")
            {
                redRounds++;
            }
            else if (originalWinningTeam == "blue")
            {
                blueRounds++;
            }

            var config = configManager.GetBombDefusalConfig();
            var targetWinRounds = config.RoundsToWin; // 例如13轮获胜
            var maxRounds = (targetWinRounds - 1) * 2; // 例如24轮制(12+12)

            // 检查是否需要换边
            CheckTeamSwap(targetWinRounds);

            // 检查获胜条件
            if (CheckWinCondition(targetWinRounds, maxRounds))
            {
                return; // 游戏已结束
            }

            // 继续下一轮
            ContinueToNextRound(config);
        }

        /// <summary>
        /// 获取当前获胜队伍的原始队伍身份
        /// </summary>
        private string GetOriginalTeamForCurrentWinner(string currentWinningTeam)
        {
            // 找到当前获胜队伍中的任意一個玩家，查看其原始队伍身份
            var winningPlayer = PlayerTeams.FirstOrDefault(p => p.Value == currentWinningTeam);
            
            if (winningPlayer.Key != null && originalTeamIdentity.ContainsKey(winningPlayer.Key))
            {
                return originalTeamIdentity[winningPlayer.Key];
            }
            
            // 如果找不到，返回当前队伍（向后兼容）
            return currentWinningTeam;
        }

        /// <summary>
        /// 检查是否需要换边
        /// </summary>
        private void CheckTeamSwap(int targetWinRounds)
        {
            var totalRounds = redRounds + blueRounds;
            var halfRounds = targetWinRounds - 1; // 例如12轮

            // 常规比赛换边（第12轮后）
            if (!isOvertime && totalRounds == halfRounds && !teamsSwapped)
            {
                SwapTeams();
                teamsSwapped = true;
                TShock.Utils.Broadcast($"=== 上半场结束！当前比分 {redRounds}:{blueRounds} ===", Color.Orange);
                TShock.Utils.Broadcast("两队换边！", Color.Orange);
                return;
            }

            // 加时赛换边（每2轮换一次）
            if (isOvertime && overtimeRounds > 0 && overtimeRounds % 2 == 0)
            {
                SwapTeams();
                TShock.Utils.Broadcast("加时赛换边！", Color.Orange);
                
                // 加时赛换边后，重置胜利历史（因为连续胜利被重置）
                overtimeWinHistory.Clear();
                TShock.Log.ConsoleDebug("[Trgo] 加时赛换边，重置胜利历史");
            }
        }

        /// <summary>
        /// 检查获胜条件
        /// </summary>
        private bool CheckWinCondition(int targetWinRounds, int maxRounds)
        {
            // 常规获胜条件：达到目标轮数
            if (!isOvertime)
            {
                if (redRounds >= targetWinRounds)
                {
                    EndGame("red", $"红队获胜！最终比分: {redRounds}:{blueRounds}");
                    return true;
                }
                if (blueRounds >= targetWinRounds)
                {
                    EndGame("blue", $"蓝队获胜！最终比分: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            var totalRounds = redRounds + blueRounds;

            // 检查是否进入加时赛
            if (!isOvertime && totalRounds >= maxRounds)
            {
                // 比分相等进入加时赛
                if (redRounds == blueRounds)
                {
                    StartOvertime();
                }
                // 领先1轮但已打满常规轮次
                else if (Math.Abs(redRounds - blueRounds) == 1)
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "红队" : "蓝队";
                    EndGame(winner, $"{winnerName}获胜！最终比分: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            // 加时赛获胜条件：连续赢得2轮或者显著领先
            if (isOvertime)
            {
                // 方法1：检查连续2轮获胜
                var consecutiveWins = CheckConsecutiveWins();
                if (consecutiveWins != null)
                {
                    var winnerName = consecutiveWins == "red" ? "红队" : "蓝队";
                    EndGame(consecutiveWins, $"{winnerName}加时赛获胜！最终比分: {redRounds}:{blueRounds} (OT)");
                    return true;
                }

                // 方法2：防止无限加时赛 - 如果某队显著领先（比如领先4轮）
                if (Math.Abs(redRounds - blueRounds) >= 4)
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "红队" : "蓝队";
                    EndGame(winner, $"{winnerName}加时赛大比分获胜！最终比分: {redRounds}:{blueRounds} (OT)");
                    TShock.Log.Info($"[Trgo] 加时赛因显著领先结束: {redRounds}:{blueRounds}");
                    return true;
                }

                // 方法3：超长加时赛保护 - 防止无限进行（比如总共50轮后强制结束）
                if (totalRounds >= maxRounds + 20) // 常规24轮 + 最多20轮加时
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "红队" : "蓝队";
                    EndGame(winner, $"{winnerName}超长加时赛获胜！最终比分: {redRounds}:{blueRounds} (OT)");
                    TShock.Log.Info($"[Trgo] 加时赛因达到最大轮数限制结束: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查连续获胜情况
        /// </summary>
        private string CheckConsecutiveWins()
        {
            if (overtimeWinHistory.Count < 2)
            {
                TShock.Log.ConsoleDebug($"[Trgo] 连胜检查: 历史不足2轮 ({overtimeWinHistory.Count}轮)");
                return null;
            }

            // 检查最后两轮是否是同一队伍连续获胜
            var lastTwo = overtimeWinHistory.TakeLast(2).ToList();
            
            if (lastTwo.Count == 2 && lastTwo[0] == lastTwo[1])
            {
                TShock.Log.ConsoleDebug($"[Trgo] 检测到连续胜利: {lastTwo[0]}队连胜2轮");
                TShock.Log.Info($"[Trgo] 加时赛连胜结束: {lastTwo[0]}队连胜，历史: [{string.Join(", ", overtimeWinHistory)}]");
                return lastTwo[0];
            }

            TShock.Log.ConsoleDebug($"[Trgo] 连胜检查: 最后两轮 [{string.Join(", ", lastTwo)}]，无连胜");
            return null;
        }

        /// <summary>
        /// 换边操作
        /// </summary>
        private void SwapTeams()
        {
            var tempTeams = new Dictionary<string, string>();
            
            // 交换所有玩家的队伍
            foreach (var kvp in PlayerTeams.ToList())
            {
                var playerName = kvp.Key;
                var currentTeam = kvp.Value;
                
                if (currentTeam == "red")
                {
                    tempTeams[playerName] = "blue";
                }
                else if (currentTeam == "blue")
                {
                    tempTeams[playerName] = "red";
                }
                else
                {
                    tempTeams[playerName] = currentTeam; // 保持原队伍
                }
            }

            // 应用队伍变更
            PlayerTeams.Clear();
            foreach (var kvp in tempTeams)
            {
                PlayerTeams[kvp.Key] = kvp.Value;
            }

            TShock.Log.Info($"[Trgo] 队伍已换边 - 轮次 {currentRound}");
        }

        /// <summary>
        /// 开始加时赛
        /// </summary>
        private void StartOvertime()
        {
            isOvertime = true;
            overtimeRounds = 0;
            overtimeWinHistory.Clear(); // 清空胜利历史
            
            TShock.Utils.Broadcast("=== 进入加时赛！===", Color.Gold);
            TShock.Utils.Broadcast($"当前比分: {redRounds}:{blueRounds}", Color.Gold);
            TShock.Utils.Broadcast("加时赛规则：连续赢得2轮的队伍获胜", Color.Gold);
            TShock.Utils.Broadcast("每2轮进行一次换边", Color.Gold);
            
            // 加时赛开始时换边
            SwapTeams();
            TShock.Utils.Broadcast("加时赛开始，两队换边！", Color.Orange);
            
            TShock.Log.Info("[Trgo] 加时赛开始，胜利历史已重置");
        }

        /// <summary>
        /// 获取最近连续胜利轮数（已废弃，使用新的连续胜利检查逻辑）
        /// </summary>
        [Obsolete("已废弃，使用新的连续胜利检查逻辑")]
        private int GetRecentWins(string team)
        {
            // 这个方法已经被新的 CheckConsecutiveWins() 方法替代
            // 保留此方法以防编译错误，但实际不再使用
            return 0;
        }

        /// <summary>
        /// 继续到下一轮
        /// </summary>
        private void ContinueToNextRound(BombDefusalConfig config)
        {
            // 复活所有死亡的游戏中玩家
            ReviveAllPlayers();

            // 开始轮次间等待
            currentRound++;
            if (isOvertime) 
            {
                overtimeRounds++;
                TShock.Log.ConsoleDebug($"[Trgo] 加时赛轮次递增: {overtimeRounds}, 总轮次: {currentRound}");
            }
            
            bombManager.Reset();
            playersInInvisRange.Clear();
            
            // 设置等待计时器
            roundWaitTimer = config.RoundWaitTime;
            
            // 显示轮次信息
            var roundInfo = isOvertime ? $"第 {currentRound} 轮 (加时 {overtimeRounds})" : $"第 {currentRound} 轮";
            TShock.Utils.Broadcast($"=== {roundInfo} 准备中 ===", Color.Yellow);
            TShock.Utils.Broadcast($"当前比分 红队:{redRounds} - 蓝队:{blueRounds}", Color.Cyan);
            
            // 显示加时赛状态
            if (isOvertime)
            {
                var winHistory = overtimeWinHistory.Count > 0 ? $"[{string.Join(", ", overtimeWinHistory.TakeLast(4))}]" : "[无]";
                TShock.Utils.Broadcast($"加时赛状态: 需连续2轮获胜 | 最近胜利: {winHistory}", Color.Gold);
            }
            
            if (roundWaitTimer > 0)
            {
                TShock.Utils.Broadcast($"所有玩家已复活，下一轮将在 {roundWaitTimer} 秒后开始", Color.Green);
            }
            else
            {
                // 如果等待时间为0，立即开始下一轮
                StartNextRound();
            }
        }

        /// <summary>
        /// 获取是否处于轮次等待状态
        /// </summary>
        public bool IsRoundWaiting => roundWaitTimer > 0;

        /// <summary>
        /// 获取炸弹管理器（供事件监听器使用）
        /// </summary>
        public BombManager GetBombManager()
        {
            return bombManager;
        }

        /// <summary>
        /// 强制结束游戏
        /// </summary>
        public void ForceEndGame(string reason)
        {
            EndGame("", reason);
        }

        /// <summary>
        /// 强制开始游戏
        /// </summary>
        public bool ForceStartGame()
        {
            if (PlayersInGame.Count >= 2)
            {
                // 重置任何现有的倒计时
                countdown = 3;
                CurrentGameState = GameState.Countdown;
                TShock.Utils.Broadcast("管理员强制开始游戏倒计时！", Color.Orange);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 处理玩家死亡后续逻辑（由事件监听器调用）
        /// </summary>
        public void OnPlayerDeathProcessed(TSPlayer deadPlayer, TSPlayer killer = null)
        {
            // 这个方法现在只负责检查游戏状态，击杀统计由事件监听器处理
            // 在爆破模式下检查是否需要结束轮次
            if (CurrentGameMode == GameMode.BombDefusal)
            {
                // 延迟检查胜负条件，给死亡动画一点时间
                Task.Delay(1000).ContinueWith(_ => CheckTeamEliminationWin());
            }
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        private void EndGame(string winningTeam, string message)
        {
            TShock.Utils.Broadcast($"?? {message} ??", winningTeam == "red" ? Color.Red : Color.Blue);

            // 显示最终统计
            if (CurrentGameMode == GameMode.TeamDeathmatch)
            {
                TShock.Utils.Broadcast("=== 最终统计 ===", Color.Cyan);
                var redKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "red").Sum(p => p.Value);
                var blueKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "blue").Sum(p => p.Value);
                TShock.Utils.Broadcast($"红队总击杀: {redKills}", Color.Red);
                TShock.Utils.Broadcast($"蓝队总击杀: {blueKills}", Color.Blue);
            }

            // 重置游戏状态
            CurrentGameState = GameState.Waiting;
            CurrentGameMode = GameMode.None;
            countdown = -1;

            // 清除所有数据
            PlayersInGame.Clear();
            PlayerTeams.Clear();
            PlayerKills.Clear();
            PlayerDeaths.Clear();
            playersInInvisRange.Clear();
            
            // 重置加时赛和换边状态
            isOvertime = false;
            overtimeRounds = 0;
            teamsSwapped = false;
            
            // 清除原始队伍身份记录和加时赛历史
            originalTeamIdentity.Clear();
            overtimeWinHistory.Clear();
            
            // 重置炸弹管理器
            bombManager.Reset();

            // 重置所有玩家队伍
            foreach (var player in TShock.Players.Where(p => p?.Active == true))
            {
                player.SetTeam(0);
            }

            TShock.Utils.Broadcast("所有玩家已重置，可以开始新的游戏！", Color.Green);
        }

        /// <summary>
        /// 复活所有游戏中的玩家
        /// </summary>
        private void ReviveAllPlayers()
        {
            foreach (var playerName in PlayersInGame.Keys.ToList())
            {
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                if (player?.Active == true)
                {
                    // 复活玩家
                    if (player.TPlayer.statLife <= 0 || player.Dead)
                    {
                        player.Spawn(PlayerSpawnContext.ReviveFromDeath);
                        player.TPlayer.statLife = player.TPlayer.statLifeMax;
                        player.SendData(PacketTypes.PlayerHp, "", player.Index);
                    }
                    
                    // 确保玩家满血
                    player.Heal(player.TPlayer.statLifeMax);
                }
            }
            
            TShock.Log.Info("[Trgo] 已复活所有游戏中的玩家");
        }

        /// <summary>
        /// 开始下一轮
        /// </summary>
        private void StartNextRound()
        {
            var roundInfo = isOvertime ? $"第 {currentRound} 轮 (加时 {overtimeRounds})" : $"第 {currentRound} 轮";
            TShock.Utils.Broadcast($"=== {roundInfo} 开始！ ===", Color.Green);
            
            // 显示队伍信息（显示原始身份和当前标识）
            ShowTeamInfo();
            
            // 传送所有玩家到出生点并重新分发装备
            TeleportPlayersAndSetEquipment();
            
            roundWaitTimer = -1; // 重置等待计时器
        }

        /// <summary>
        /// 显示队伍信息（包含原始身份）
        /// </summary>
        private void ShowTeamInfo()
        {
            var currentRedPlayers = PlayerTeams.Where(p => p.Value == "red").Select(p => p.Key).ToList();
            var currentBluePlayers = PlayerTeams.Where(p => p.Value == "blue").Select(p => p.Key).ToList();

            // 统计原始队伍
            var originalRedCount = currentRedPlayers.Count(p => originalTeamIdentity.GetValueOrDefault(p) == "red");
            var originalBlueCount = currentRedPlayers.Count(p => originalTeamIdentity.GetValueOrDefault(p) == "blue");
            
            if (teamsSwapped)
            {
                TShock.Utils.Broadcast($"?? 队伍已换边！", Color.Orange);
                TShock.Utils.Broadcast($"红队位置 ({currentRedPlayers.Count}人): {string.Join(", ", currentRedPlayers)} [原蓝队]", Color.Red);
                TShock.Utils.Broadcast($"蓝队位置 ({currentBluePlayers.Count}人): {string.Join(", ", currentBluePlayers)} [原红队]", Color.Blue);
            }
            else
            {
                TShock.Utils.Broadcast($"红队 ({currentRedPlayers.Count}人): {string.Join(", ", currentRedPlayers)}", Color.Red);
                TShock.Utils.Broadcast($"蓝队 ({currentBluePlayers.Count}人): {string.Join(", ", currentBluePlayers)}", Color.Blue);
            }
        }
    }
}