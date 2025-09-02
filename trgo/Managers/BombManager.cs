using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TShockAPI;
using Trgo.Config;

namespace Trgo.Managers
{
    /// <summary>
    /// 炸弹管理器
    /// </summary>
    public class BombManager
    {
        private readonly ConfigManager configManager;
        
        // 炸弹状态
        private bool bombPlanted = false;
        private string bombPlanter = "";
        private BombSiteConfig activeBombSite;
        private int bombTimer = 0;
        
        // 下包/拆包进度
        private readonly Dictionary<string, BombAction> playerActions = new Dictionary<string, BombAction>();
        private readonly Dictionary<string, int> actionProgress = new Dictionary<string, int>();

        public BombManager(ConfigManager configManager)
        {
            this.configManager = configManager;
        }

        /// <summary>
        /// 炸弹动作类型
        /// </summary>
        private enum BombAction
        {
            None,
            Planting,
            Defusing
        }

        /// <summary>
        /// 炸弹是否已被放置
        /// </summary>
        public bool IsBombPlanted => bombPlanted;

        /// <summary>
        /// 炸弹剩余时间
        /// </summary>
        public int BombTimer => bombTimer;

        /// <summary>
        /// 活跃的爆破点
        /// </summary>
        public BombSiteConfig ActiveBombSite => activeBombSite;

        /// <summary>
        /// 重置炸弹状态
        /// </summary>
        public void Reset()
        {
            bombPlanted = false;
            bombPlanter = "";
            activeBombSite = null;
            bombTimer = 0;
            playerActions.Clear();
            actionProgress.Clear();
        }

        /// <summary>
        /// 开始下包
        /// </summary>
        public bool StartPlanting(TSPlayer player)
        {
            if (bombPlanted)
            {
                player.SendInfoMessage("炸弹已经被放置");
                return false;
            }

            var map = configManager.GetCurrentMap();
            var playerPos = new Vector2(player.TileX, player.TileY);
            
            // 查找玩家所在的爆破点
            var bombSite = map.BombSites.FirstOrDefault(site => site.IsPlayerInRange(playerPos));
            if (bombSite == null)
            {
                player.SendInfoMessage("你需要在爆破点范围内才能下包");
                return false;
            }

            if (playerActions.ContainsKey(player.Name))
            {
                player.SendInfoMessage("你正在执行其他操作");
                return false;
            }

            playerActions[player.Name] = BombAction.Planting;
            actionProgress[player.Name] = 0;
            activeBombSite = bombSite;

            player.SendInfoMessage($"开始在 {bombSite.Name} 下包...");
            TShock.Utils.Broadcast($"{player.Name} 开始在 {bombSite.Name} 下包！", Color.Orange);

            return true;
        }

        /// <summary>
        /// 开始拆包
        /// </summary>
        public bool StartDefusing(TSPlayer player)
        {
            if (!bombPlanted)
            {
                player.SendInfoMessage("没有炸弹需要拆除");
                return false;
            }

            var playerPos = new Vector2(player.TileX, player.TileY);
            if (!activeBombSite.IsPlayerInRange(playerPos))
            {
                player.SendInfoMessage($"你需要在 {activeBombSite.Name} 范围内才能拆包");
                return false;
            }

            if (playerActions.ContainsKey(player.Name))
            {
                player.SendInfoMessage("你正在执行其他操作");
                return false;
            }

            playerActions[player.Name] = BombAction.Defusing;
            actionProgress[player.Name] = 0;

            player.SendInfoMessage($"开始在 {activeBombSite.Name} 拆包...");
            TShock.Utils.Broadcast($"{player.Name} 开始拆包！", Color.Cyan);

            return true;
        }

        /// <summary>
        /// 取消玩家操作
        /// </summary>
        public void CancelPlayerAction(string playerName)
        {
            if (playerActions.ContainsKey(playerName))
            {
                var action = playerActions[playerName];
                playerActions.Remove(playerName);
                actionProgress.Remove(playerName);

                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                if (player != null)
                {
                    var actionText = action == BombAction.Planting ? "下包" : "拆包";
                    player.SendInfoMessage($"{actionText}被中断");
                    TShock.Utils.Broadcast($"{playerName} 的{actionText}被中断", Color.Yellow);
                }
            }
        }

        /// <summary>
        /// 更新炸弹状态（每秒调用）
        /// </summary>
        public BombUpdateResult Update()
        {
            var result = new BombUpdateResult();
            var config = configManager.GetBombDefusalConfig();

            // 更新炸弹倒计时
            if (bombPlanted)
            {
                bombTimer--;
                if (bombTimer <= 0)
                {
                    result.BombExploded = true;
                    result.Message = "炸弹爆炸！恐怖分子获胜本轮！";
                    Reset();
                    return result;
                }

                if (bombTimer % config.BombWarningInterval == 0)
                {
                    result.WarningMessage = $"炸弹将在 {bombTimer} 秒后爆炸！";
                }
            }

            // 更新玩家动作进度
            var completedActions = new List<string>();
            foreach (var kvp in playerActions.ToList())
            {
                var playerName = kvp.Key;
                var action = kvp.Value;
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);

                if (player == null || !player.Active)
                {
                    completedActions.Add(playerName);
                    continue;
                }

                var playerPos = new Vector2(player.TileX, player.TileY);

                // 检查玩家是否还在正确位置
                if (action == BombAction.Planting)
                {
                    if (activeBombSite?.IsPlayerInRange(playerPos) != true)
                    {
                        CancelPlayerAction(playerName);
                        continue;
                    }
                }
                else if (action == BombAction.Defusing)
                {
                    if (!bombPlanted || activeBombSite?.IsPlayerInRange(playerPos) != true)
                    {
                        CancelPlayerAction(playerName);
                        continue;
                    }
                }

                // 更新进度
                actionProgress[playerName]++;
                var maxTime = action == BombAction.Planting ? config.PlantTime : config.DefuseTime;
                var progress = actionProgress[playerName];

                // 显示进度
                var percentage = (int)((double)progress / maxTime * 100);
                var actionText = action == BombAction.Planting ? "下包" : "拆包";
                player.SendMessage($"{actionText}进度: {percentage}% ({progress}/{maxTime})", Color.Yellow);

                // 检查是否完成
                if (progress >= maxTime)
                {
                    completedActions.Add(playerName);

                    if (action == BombAction.Planting)
                    {
                        bombPlanted = true;
                        bombPlanter = playerName;
                        bombTimer = config.BombTimer;
                        result.BombPlanted = true;
                        result.Message = $"{playerName} 在 {activeBombSite.Name} 成功下包！蓝队需要在 {bombTimer} 秒内拆除炸弹！";
                    }
                    else if (action == BombAction.Defusing)
                    {
                        bombPlanted = false;
                        result.BombDefused = true;
                        result.Message = $"{playerName} 成功拆除炸弹！蓝队获胜本轮！";
                        Reset();
                    }
                }
            }

            // 移除已完成的动作
            foreach (var playerName in completedActions)
            {
                if (playerActions.ContainsKey(playerName))
                {
                    playerActions.Remove(playerName);
                    actionProgress.Remove(playerName);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查玩家是否在执行炸弹操作
        /// </summary>
        public bool IsPlayerBusy(string playerName)
        {
            return playerActions.ContainsKey(playerName);
        }

        /// <summary>
        /// 获取玩家当前动作
        /// </summary>
        public string GetPlayerActionStatus(string playerName)
        {
            if (!playerActions.ContainsKey(playerName))
                return "";

            var action = playerActions[playerName];
            var progress = actionProgress[playerName];
            var config = configManager.GetBombDefusalConfig();
            var maxTime = action == BombAction.Planting ? config.PlantTime : config.DefuseTime;
            var actionText = action == BombAction.Planting ? "下包" : "拆包";

            return $"正在{actionText}: {progress}/{maxTime}秒";
        }
    }

    /// <summary>
    /// 炸弹更新结果
    /// </summary>
    public class BombUpdateResult
    {
        public bool BombPlanted { get; set; }
        public bool BombDefused { get; set; }
        public bool BombExploded { get; set; }
        public string Message { get; set; } = "";
        public string WarningMessage { get; set; } = "";
    }
}