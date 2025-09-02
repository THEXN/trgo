using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using TShockAPI;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;
using Trgo.Managers;

namespace Trgo
{
    /// <summary>
    /// Trgo游戏事件监听器 - 负责处理玩家伤害、死亡事件和配置重载
    /// </summary>
    /// <remarks>
    /// 主要功能：
    /// ? 基于伤害追踪的准确击杀检测系统
    /// ? 处理玩家死亡事件和击杀统计
    /// ? 自动配置重载支持
    /// ? 团队友伤防护
    /// ? 击杀奖励系统（爆破模式生命恢复）
    /// </remarks>
    public class TrgoEventListener : IDisposable
    {
        #region 私有字段
        private readonly GameManager gameManager;
        private readonly Dictionary<int, PlayerCombatData> playerCombatData;
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化事件监听器
        /// </summary>
        /// <param name="gameManager">游戏管理器实例</param>
        public TrgoEventListener(GameManager gameManager)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.playerCombatData = new Dictionary<int, PlayerCombatData>();

            // 注册游戏事件处理器
            RegisterGameEvents();
            
            TShock.Log.Info("[Trgo] 事件监听器已初始化 - 支持伤害追踪、击杀检测和配置热重载");
        }
        #endregion

        #region 事件注册与注销
        /// <summary>
        /// 注册游戏事件处理器
        /// </summary>
        private void RegisterGameEvents()
        {
            // 玩家战斗相关事件
            GetDataHandlers.PlayerDamage += OnPlayerDamage;
            GetDataHandlers.KillMe += OnKillMe;
            
            // 重生控制事件
            GetDataHandlers.PlayerSpawn += OnPlayerSpawn;
            
            // 系统配置重载事件
            GeneralHooks.ReloadEvent += OnConfigReload;
            
            TShock.Log.ConsoleDebug("[Trgo] 已注册伤害检测、死亡处理、重生控制和配置重载事件");
        }

        /// <summary>
        /// 注销游戏事件处理器
        /// </summary>
        private void UnregisterGameEvents()
        {
            // 取消注册游戏事件
            GetDataHandlers.PlayerDamage -= OnPlayerDamage;
            GetDataHandlers.KillMe -= OnKillMe;
            GetDataHandlers.PlayerSpawn -= OnPlayerSpawn;
            
            // 取消注册系统事件
            GeneralHooks.ReloadEvent -= OnConfigReload;
            
            TShock.Log.ConsoleDebug("[Trgo] 已注销所有事件处理器");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 注销事件处理器
            UnregisterGameEvents();
            
            // 清理战斗数据
            playerCombatData?.Clear();
            
            TShock.Log.Info("[Trgo] 事件监听器已安全释放");
        }
        #endregion

        #region 配置重载事件处理
        /// <summary>
        /// 处理系统配置重载事件 - 自动同步插件配置
        /// </summary>
        /// <param name="args">重载事件参数</param>
        /// <remarks>
        /// 当管理员使用 TShock 的 /reload 命令时，自动重载 Trgo 插件配置
        /// 这确保了配置的一致性和实时生效
        /// </remarks>
        private void OnConfigReload(ReloadEventArgs args)
        {
            try
            {
                // 清理当前战斗数据，避免配置更改后的数据不一致
                Reset();
                
                TShock.Log.Info("[Trgo] 事件监听器已响应系统重载，战斗数据已重置");
                args.Player?.SendInfoMessage("[Trgo] 事件监听器配置已同步更新");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 事件监听器配置重载时出错: {ex.Message}");
                args.Player?.SendErrorMessage($"[Trgo] 事件监听器重载失败: {ex.Message}");
            }
        }
        #endregion

        #region 玩家伤害事件处理
        /// <summary>
        /// 处理玩家伤害事件 - 高级伤害追踪系统
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">伤害事件参数</param>
        /// <remarks>
        /// 伤害追踪功能：
        /// ? 记录玩家间的攻击关系和时间戳
        /// ? 计算真实伤害值（考虑防御力）
        /// ? 防止同队友伤记录
        /// ? 统计伤害数据用于击杀判定
        /// </remarks>
        private void OnPlayerDamage(object sender, GetDataHandlers.PlayerDamageEventArgs args)
        {
            // 基础参数验证
            if (!IsValidDamageEvent(args))
                return;

            // 游戏状态验证
            if (!IsGameInProgress())
                return;

            var victimIndex = args.ID;
            var attackerIndex = args.Player.Index;

            // 获取参与者信息
            var victim = TShock.Players[victimIndex];
            if (!IsValidGameParticipant(victim) || !IsValidGameParticipant(args.Player))
                return;

            // 防止自伤和同队伤害
            if (attackerIndex == victimIndex || IsFriendlyFire(args.Player.Name, victim.Name))
                return;

            // 处理伤害记录
            ProcessDamageRecord(victimIndex, attackerIndex, args.Damage);
        }

        /// <summary>
        /// 验证伤害事件的有效性
        /// </summary>
        private bool IsValidDamageEvent(GetDataHandlers.PlayerDamageEventArgs args)
        {
            return args?.Player != null && 
                   args.ID >= 0 && 
                   args.ID < Main.player.Length &&
                   args.Damage > 0;
        }

        /// <summary>
        /// 检查游戏是否在进行中
        /// </summary>
        private bool IsGameInProgress()
        {
            return gameManager.CurrentGameState == GameManager.GameState.InProgress;
        }

        /// <summary>
        /// 验证玩家是否为有效的游戏参与者
        /// </summary>
        private bool IsValidGameParticipant(TSPlayer player)
        {
            return player?.Active == true && 
                   !string.IsNullOrEmpty(player.Name) && 
                   gameManager.PlayersInGame.ContainsKey(player.Name);
        }

        /// <summary>
        /// 检查是否为友军伤害
        /// </summary>
        private bool IsFriendlyFire(string attackerName, string victimName)
        {
            return gameManager.PlayerTeams.ContainsKey(attackerName) &&
                   gameManager.PlayerTeams.ContainsKey(victimName) &&
                   gameManager.PlayerTeams[attackerName] == gameManager.PlayerTeams[victimName];
        }

        /// <summary>
        /// 处理伤害记录和统计
        /// </summary>
        private void ProcessDamageRecord(int victimIndex, int attackerIndex, int rawDamage)
        {
            // 确保战斗数据存在
            EnsureCombatDataExists(victimIndex);
            EnsureCombatDataExists(attackerIndex);

            var victimData = playerCombatData[victimIndex];
            var attackerData = playerCombatData[attackerIndex];

            // 更新攻击关系和时间戳
            victimData.LastAttacker = attackerIndex;
            victimData.LastAttackTime = DateTime.Now;
            attackerData.LastTarget = victimIndex;
            attackerData.LastAttackTime = DateTime.Now;

            // 计算实际伤害值（考虑防御力）
            var actualDamage = (int)Main.CalculateDamagePlayersTakeInPVP(rawDamage, Main.player[victimIndex].statDefense);
            
            // 更新伤害统计
            attackerData.TotalDamageDealt += actualDamage;
            victimData.TotalDamageReceived += actualDamage;

            TShock.Log.ConsoleDebug($"[Trgo] 伤害记录: {TShock.Players[attackerIndex]?.Name} -> {TShock.Players[victimIndex]?.Name} ({actualDamage}点伤害)");
        }

        /// <summary>
        /// 确保玩家战斗数据存在
        /// </summary>
        private void EnsureCombatDataExists(int playerIndex)
        {
            if (!playerCombatData.ContainsKey(playerIndex))
            {
                playerCombatData[playerIndex] = new PlayerCombatData();
            }
        }
        #endregion

        #region 玩家死亡事件处理
        /// <summary>
        /// 处理玩家死亡事件 - 智能击杀检测与统计系统
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">死亡事件参数</param>
        /// <remarks>
        /// 死亡处理功能：
        /// ? 多重击杀者检测算法（直接+间接）
        /// ? 自动击杀统计和奖励系统
        /// ? 自定义死亡消息生成
        /// ? 爆破模式特殊处理（取消炸弹操作）
        /// ? 游戏胜负条件检查触发
        /// </remarks>
        private void OnKillMe(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            // 基础验证
            if (!IsValidDeathEvent(args))
                return;

            var victimIndex = args.Player.Index;
            var victimName = args.Player.Name;

            // 更新死亡统计
            UpdateDeathStatistics(victimName);

            // 处理爆破模式特殊逻辑
            HandleBombModeDeathLogic(victimName);

            // 检测击杀者
            var killer = DetectKiller(args, victimIndex);

            // 处理击杀统计和奖励
            var deathMessage = ProcessKillStatistics(killer, victimName, args);

            // 设置自定义死亡原因
            args.PlayerDeathReason._sourceCustomReason = deathMessage;

            // 清理战斗数据
            CleanupCombatData(victimIndex);

            // 发送死亡数据包
            SendDeathPacket(args);

            // 标记事件已处理
            args.Handled = true;

            // 通知游戏管理器检查胜负条件
            gameManager.OnPlayerDeathProcessed(args.Player, killer);
        }

        /// <summary>
        /// 验证死亡事件的有效性
        /// </summary>
        private bool IsValidDeathEvent(GetDataHandlers.KillMeEventArgs args)
        {
            return IsGameInProgress() &&
                   args?.Player?.Name != null &&
                   gameManager.PlayersInGame.ContainsKey(args.Player.Name);
        }

        /// <summary>
        /// 更新玩家死亡统计
        /// </summary>
        private void UpdateDeathStatistics(string victimName)
        {
            if (gameManager.PlayerDeaths.ContainsKey(victimName))
            {
                gameManager.PlayerDeaths[victimName]++;
            }
        }

        /// <summary>
        /// 处理爆破模式死亡的特殊逻辑
        /// </summary>
        private void HandleBombModeDeathLogic(string victimName)
        {
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                var bombManager = gameManager.GetBombManager();
                if (bombManager?.IsPlayerBusy(victimName) == true)
                {
                    bombManager.CancelPlayerAction(victimName);
                    TShock.Utils.Broadcast($"?? {victimName} 的炸弹操作因死亡而中断", Color.Orange);
                }
            }
        }

        /// <summary>
        /// 智能击杀者检测算法
        /// </summary>
        /// <param name="args">死亡事件参数</param>
        /// <param name="victimIndex">受害者索引</param>
        /// <returns>击杀者，如果没有则返回null</returns>
        private TSPlayer DetectKiller(GetDataHandlers.KillMeEventArgs args, int victimIndex)
        {
            // 方法1：直接从死亡原因获取攻击者
            var directKiller = GetDirectKiller(args);
            if (directKiller != null)
            {
                TShock.Log.ConsoleDebug($"[Trgo] 直接击杀检测: {directKiller.Name}");
                return directKiller;
            }

            // 方法2：基于伤害追踪的间接击杀检测
            var indirectKiller = GetIndirectKiller(victimIndex);
            if (indirectKiller != null)
            {
                TShock.Log.ConsoleDebug($"[Trgo] 间接击杀检测: {indirectKiller.Name}");
                return indirectKiller;
            }

            TShock.Log.ConsoleDebug("[Trgo] 未检测到有效击杀者，判定为意外死亡");
            return null;
        }

        /// <summary>
        /// 获取直接击杀者（从死亡原因）
        /// </summary>
        private TSPlayer GetDirectKiller(GetDataHandlers.KillMeEventArgs args)
        {
            var sourcePlayerIndex = args.PlayerDeathReason._sourcePlayerIndex;
            
            if (sourcePlayerIndex >= 0 && 
                sourcePlayerIndex < TShock.Players.Length && 
                TShock.Players[sourcePlayerIndex]?.Active == true)
            {
                var killer = TShock.Players[sourcePlayerIndex];
                if (gameManager.PlayersInGame.ContainsKey(killer.Name))
                {
                    return killer;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 获取间接击杀者（基于伤害追踪）
        /// </summary>
        private TSPlayer GetIndirectKiller(int victimIndex)
        {
            if (!playerCombatData.ContainsKey(victimIndex))
                return null;

            var victimData = playerCombatData[victimIndex];
            var timeSinceLastAttack = DateTime.Now - victimData.LastAttackTime;

            // 5秒内的攻击认为是有效击杀
            if (timeSinceLastAttack.TotalSeconds <= 5 && 
                victimData.LastAttacker >= 0 &&
                victimData.LastAttacker < TShock.Players.Length)
            {
                var killer = TShock.Players[victimData.LastAttacker];
                if (killer?.Active == true && gameManager.PlayersInGame.ContainsKey(killer.Name))
                {
                    return killer;
                }
            }

            return null;
        }

        /// <summary>
        /// 处理击杀统计和奖励系统
        /// </summary>
        private string ProcessKillStatistics(TSPlayer killer, string victimName, GetDataHandlers.KillMeEventArgs args)
        {
            if (killer != null && IsValidKill(killer.Name, victimName))
            {
                return ProcessValidKill(killer, victimName, args);
            }
            else
            {
                return ProcessAccidentalDeath(victimName);
            }
        }

        /// <summary>
        /// 检查是否为有效击杀（敌对队伍）
        /// </summary>
        private bool IsValidKill(string killerName, string victimName)
        {
            return gameManager.PlayerTeams.ContainsKey(killerName) &&
                   gameManager.PlayerTeams.ContainsKey(victimName) &&
                   gameManager.PlayerTeams[killerName] != gameManager.PlayerTeams[victimName];
        }

        /// <summary>
        /// 处理有效击杀
        /// </summary>
        private string ProcessValidKill(TSPlayer killer, string victimName, GetDataHandlers.KillMeEventArgs args)
        {
            // 更新击杀统计
            if (gameManager.PlayerKills.ContainsKey(killer.Name))
            {
                gameManager.PlayerKills[killer.Name]++;
            }

            // 应用击杀奖励
            ApplyKillReward(killer);

            // 生成击杀消息
            var deathMessage = GenerateKillMessage(killer, victimName, args);
            
            // 广播击杀消息
            TShock.Utils.Broadcast(deathMessage, Color.Yellow);

            TShock.Log.Info($"[Trgo] 击杀统计: {killer.Name} 击杀了 {victimName}");
            
            return deathMessage;
        }

        /// <summary>
        /// 应用击杀奖励系统
        /// </summary>
        private void ApplyKillReward(TSPlayer killer)
        {
            // 爆破模式击杀奖励：恢复生命值
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                var healAmount = killer.TPlayer.statLifeMax2 / 2; // 恢复50%生命值
                killer.Heal(healAmount);
                killer.SendInfoMessage($"击杀奖励：恢复了 {healAmount} 点生命值");
                
                TShock.Log.ConsoleDebug($"[Trgo] 爆破模式击杀奖励: {killer.Name} 恢复 {healAmount} 生命值");
            }
        }

        /// <summary>
        /// 生成击杀消息
        /// </summary>
        private string GenerateKillMessage(TSPlayer killer, string victimName, GetDataHandlers.KillMeEventArgs args)
        {
            var killerTeam = gameManager.PlayerTeams[killer.Name] == "red" ? "红队" : "蓝队";
            var victimTeam = gameManager.PlayerTeams[victimName] == "red" ? "红队" : "蓝队";

            // 获取武器信息
            var weaponInfo = GetWeaponInfo(args.PlayerDeathReason._sourceItemType);
            var projectileInfo = GetProjectileInfo(args.PlayerDeathReason.SourceProjectileType);

            // 构建武器描述
            var weaponText = GenerateWeaponDescription(weaponInfo, projectileInfo);

            return $"[{killerTeam}] {killer.Name} {weaponText}击杀了 [{victimTeam}] {victimName}";
        }

        /// <summary>
        /// 获取武器信息
        /// </summary>
        private string GetWeaponInfo(int sourceItemType)
        {
            if (sourceItemType > 0)
            {
                var item = new Item();
                item.SetDefaults(sourceItemType);
                if (item.damage > 0)
                {
                    return $"[i:{sourceItemType}]";
                }
            }
            return "";
        }

        /// <summary>
        /// 获取弹幕信息
        /// </summary>
        private string GetProjectileInfo(int? sourceProjectileType)
        {
            return sourceProjectileType.HasValue 
                ? Lang.GetProjectileName(sourceProjectileType.Value).Value 
                : "";
        }

        /// <summary>
        /// 生成武器描述文本
        /// </summary>
        private string GenerateWeaponDescription(string weaponInfo, string projectileInfo)
        {
            if (!string.IsNullOrEmpty(weaponInfo) || !string.IsNullOrEmpty(projectileInfo))
            {
                return $"用{weaponInfo}{projectileInfo}";
            }
            return "";
        }

        /// <summary>
        /// 处理意外死亡
        /// </summary>
        private string ProcessAccidentalDeath(string victimName)
        {
            var victimTeam = gameManager.PlayerTeams.ContainsKey(victimName) 
                ? (gameManager.PlayerTeams[victimName] == "red" ? "红队" : "蓝队") 
                : "";

            var message = $"[{victimTeam}] {victimName} 死于意外";
            TShock.Log.Info($"[Trgo] 意外死亡: {message}");
            
            return message;
        }

        /// <summary>
        /// 清理战斗数据
        /// </summary>
        private void CleanupCombatData(int victimIndex)
        {
            if (playerCombatData.ContainsKey(victimIndex))
            {
                playerCombatData[victimIndex].Reset();
            }
        }

        /// <summary>
        /// 发送死亡数据包
        /// </summary>
        private void SendDeathPacket(GetDataHandlers.KillMeEventArgs args)
        {
            Main.player[args.PlayerId].KillMe(args.PlayerDeathReason, args.Damage, args.Direction, true);
            NetMessage.SendPlayerDeath(args.PlayerId, args.PlayerDeathReason, args.Damage, args.Direction, true, -1, args.Player.Index);
        }
        #endregion

        #region 玩家重生事件处理
        /// <summary>
        /// 处理玩家重生事件 - 游戏重生控制系统
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="args">重生事件参数</param>
        /// <remarks>
        /// 重生控制功能：
        /// ? 爆破模式中阻止轮次内重生
        /// ? 控制重生位置到正确的队伍出生点
        /// ? 团队死斗模式允许立即重生
        /// ? 游戏外正常重生
        /// </remarks>
        private void OnPlayerSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
        {
            if (args?.Player?.Name == null)
                return;

            // 只有游戏参与者才需要特殊处理
            if (!gameManager.PlayersInGame.ContainsKey(args.Player.Name))
                return;

            // 游戏进行中的重生控制
            if (gameManager.CurrentGameState == GameManager.GameState.InProgress)
            {
                HandleInGameRespawn(args);
            }
            else
            {
                // 游戏外正常重生，但传送到队伍出生点
                RedirectToTeamSpawn(args.Player);
            }
        }

        /// <summary>
        /// 处理游戏中的重生
        /// </summary>
        private void HandleInGameRespawn(GetDataHandlers.SpawnEventArgs args)
        {
            var player = args.Player;
            
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                // 爆破模式：阻止轮次中重生
                HandleBombDefusalRespawn(player, args);
            }
            else if (gameManager.CurrentGameMode == GameManager.GameMode.TeamDeathmatch)
            {
                // 团队死斗模式：允许重生但传送到队伍出生点
                HandleTeamDeathmatchRespawn(player, args);
            }
        }

        /// <summary>
        /// 处理爆破模式重生
        /// </summary>
        private void HandleBombDefusalRespawn(TSPlayer player, GetDataHandlers.SpawnEventArgs args)
        {
            // 检查是否在轮次间等待期
            if (IsRoundWaiting())
            {
                // 轮次间等待期允许重生
                RedirectToTeamSpawn(player);
                player.SendInfoMessage("你已复活，下一轮即将开始");
                return;
            }

            // 轮次进行中阻止重生
            args.Handled = true; // 阻止重生
            
            // 延迟消息，避免与重生逻辑冲突
            Task.Delay(1000).ContinueWith(_ =>
            {
                if (player?.Active == true)
                {
                    player.SendErrorMessage("爆破模式中无法在轮次进行时重生");
                    player.SendInfoMessage("请等待当前轮次结束后自动复活");
                }
            });
            
            TShock.Log.ConsoleDebug($"[Trgo] 阻止了 {player.Name} 在爆破模式轮次中的重生");
        }

        /// <summary>
        /// 处理团队死斗模式重生
        /// </summary>
        private void HandleTeamDeathmatchRespawn(TSPlayer player, GetDataHandlers.SpawnEventArgs args)
        {
            // 团队死斗模式允许立即重生，但要传送到队伍出生点
            Task.Delay(500).ContinueWith(_ => RedirectToTeamSpawn(player));
            
            player.SendInfoMessage("你已重生，继续战斗！");
            TShock.Log.ConsoleDebug($"[Trgo] {player.Name} 在团队死斗模式中重生");
        }

        /// <summary>
        /// 检查是否在轮次间等待期
        /// </summary>
        private bool IsRoundWaiting()
        {
            // 这里需要访问GameManager的私有字段，可能需要添加公共属性
            // 暂时返回false，实际实现需要GameManager提供接口
            return false;
        }

        /// <summary>
        /// 将玩家重定向到队伍出生点
        /// </summary>
        private void RedirectToTeamSpawn(TSPlayer player)
        {
            if (!gameManager.PlayerTeams.ContainsKey(player.Name))
                return;

            try
            {
                // 获取配置和地图信息
                var gameManagerType = gameManager.GetType();
                var configManagerField = gameManagerType.GetField("configManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (configManagerField?.GetValue(gameManager) is ConfigManager configManager)
                {
                    var currentMap = configManager.GetCurrentMap();
                    var team = gameManager.PlayerTeams[player.Name];
                    
                    // 延迟传送，确保重生完成
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        if (player?.Active == true)
                        {
                            if (team == "red")
                            {
                                player.Teleport((int)currentMap.RedSpawn.X * 16, (int)currentMap.RedSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] 重生传送: {player.Name} -> 红队出生点");
                            }
                            else if (team == "blue")
                            {
                                player.Teleport((int)currentMap.BlueSpawn.X * 16, (int)currentMap.BlueSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] 重生传送: {player.Name} -> 蓝队出生点");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 重生传送失败: {ex.Message}");
            }
        }
        #endregion

        #region 公共管理方法
        /// <summary>
        /// 清理指定玩家的战斗数据
        /// </summary>
        /// <param name="playerIndex">玩家索引</param>
        public void ClearPlayerData(int playerIndex)
        {
            if (playerCombatData.ContainsKey(playerIndex))
            {
                playerCombatData.Remove(playerIndex);
                TShock.Log.ConsoleDebug($"[Trgo] 已清理玩家 {playerIndex} 的战斗数据");
            }
        }

        /// <summary>
        /// 重置所有战斗数据
        /// </summary>
        public void Reset()
        {
            var dataCount = playerCombatData.Count;
            playerCombatData.Clear();
            TShock.Log.Info($"[Trgo] 已重置所有战斗数据 (共{dataCount}条记录)");
        }

        /// <summary>
        /// 获取玩家战斗统计信息
        /// </summary>
        /// <param name="playerIndex">玩家索引</param>
        /// <returns>战斗数据，如果不存在则返回null</returns>
        public PlayerCombatData GetPlayerCombatData(int playerIndex)
        {
            return playerCombatData.ContainsKey(playerIndex) ? playerCombatData[playerIndex] : null;
        }

        /// <summary>
        /// 获取当前活跃的战斗数据数量
        /// </summary>
        public int GetActiveCombatDataCount()
        {
            return playerCombatData.Count;
        }
        #endregion
    }

    /// <summary>
    /// 玩家战斗数据模型
    /// </summary>
    /// <remarks>
    /// 存储玩家在游戏中的战斗相关数据，用于击杀检测和统计
    /// </remarks>
    public class PlayerCombatData
    {
        #region 公共属性
        /// <summary>
        /// 最后攻击者的索引
        /// </summary>
        public int LastAttacker { get; set; } = -1;

        /// <summary>
        /// 最后攻击目标的索引
        /// </summary>
        public int LastTarget { get; set; } = -1;

        /// <summary>
        /// 最后一次攻击的时间
        /// </summary>
        public DateTime LastAttackTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 总伤害输出
        /// </summary>
        public int TotalDamageDealt { get; set; } = 0;

        /// <summary>
        /// 总承受伤害
        /// </summary>
        public int TotalDamageReceived { get; set; } = 0;
        #endregion

        #region 公共方法
        /// <summary>
        /// 重置所有战斗数据
        /// </summary>
        public void Reset()
        {
            LastAttacker = -1;
            LastTarget = -1;
            LastAttackTime = DateTime.MinValue;
            TotalDamageDealt = 0;
            TotalDamageReceived = 0;
        }

        /// <summary>
        /// 检查是否有有效的最后攻击记录
        /// </summary>
        /// <param name="timeoutSeconds">超时时间（秒）</param>
        /// <returns>如果在指定时间内有攻击记录则返回true</returns>
        public bool HasRecentAttack(double timeoutSeconds = 5.0)
        {
            return LastAttacker >= 0 && 
                   (DateTime.Now - LastAttackTime).TotalSeconds <= timeoutSeconds;
        }

        /// <summary>
        /// 获取战斗数据的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"LastAttacker: {LastAttacker}, DamageDealt: {TotalDamageDealt}, DamageReceived: {TotalDamageReceived}";
        }
        #endregion
    }
}