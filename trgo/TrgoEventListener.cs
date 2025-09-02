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
    /// Trgo��Ϸ�¼������� - ����������˺��������¼�����������
    /// </summary>
    /// <remarks>
    /// ��Ҫ���ܣ�
    /// ? �����˺�׷�ٵ�׼ȷ��ɱ���ϵͳ
    /// ? ������������¼��ͻ�ɱͳ��
    /// ? �Զ���������֧��
    /// ? �Ŷ����˷���
    /// ? ��ɱ����ϵͳ������ģʽ�����ָ���
    /// </remarks>
    public class TrgoEventListener : IDisposable
    {
        #region ˽���ֶ�
        private readonly GameManager gameManager;
        private readonly Dictionary<int, PlayerCombatData> playerCombatData;
        #endregion

        #region ���캯��
        /// <summary>
        /// ��ʼ���¼�������
        /// </summary>
        /// <param name="gameManager">��Ϸ������ʵ��</param>
        public TrgoEventListener(GameManager gameManager)
        {
            this.gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
            this.playerCombatData = new Dictionary<int, PlayerCombatData>();

            // ע����Ϸ�¼�������
            RegisterGameEvents();
            
            TShock.Log.Info("[Trgo] �¼��������ѳ�ʼ�� - ֧���˺�׷�١���ɱ��������������");
        }
        #endregion

        #region �¼�ע����ע��
        /// <summary>
        /// ע����Ϸ�¼�������
        /// </summary>
        private void RegisterGameEvents()
        {
            // ���ս������¼�
            GetDataHandlers.PlayerDamage += OnPlayerDamage;
            GetDataHandlers.KillMe += OnKillMe;
            
            // ���������¼�
            GetDataHandlers.PlayerSpawn += OnPlayerSpawn;
            
            // ϵͳ���������¼�
            GeneralHooks.ReloadEvent += OnConfigReload;
            
            TShock.Log.ConsoleDebug("[Trgo] ��ע���˺���⡢���������������ƺ����������¼�");
        }

        /// <summary>
        /// ע����Ϸ�¼�������
        /// </summary>
        private void UnregisterGameEvents()
        {
            // ȡ��ע����Ϸ�¼�
            GetDataHandlers.PlayerDamage -= OnPlayerDamage;
            GetDataHandlers.KillMe -= OnKillMe;
            GetDataHandlers.PlayerSpawn -= OnPlayerSpawn;
            
            // ȡ��ע��ϵͳ�¼�
            GeneralHooks.ReloadEvent -= OnConfigReload;
            
            TShock.Log.ConsoleDebug("[Trgo] ��ע�������¼�������");
        }

        /// <summary>
        /// �ͷ���Դ
        /// </summary>
        public void Dispose()
        {
            // ע���¼�������
            UnregisterGameEvents();
            
            // ����ս������
            playerCombatData?.Clear();
            
            TShock.Log.Info("[Trgo] �¼��������Ѱ�ȫ�ͷ�");
        }
        #endregion

        #region ���������¼�����
        /// <summary>
        /// ����ϵͳ���������¼� - �Զ�ͬ���������
        /// </summary>
        /// <param name="args">�����¼�����</param>
        /// <remarks>
        /// ������Աʹ�� TShock �� /reload ����ʱ���Զ����� Trgo �������
        /// ��ȷ�������õ�һ���Ժ�ʵʱ��Ч
        /// </remarks>
        private void OnConfigReload(ReloadEventArgs args)
        {
            try
            {
                // ����ǰս�����ݣ��������ø��ĺ�����ݲ�һ��
                Reset();
                
                TShock.Log.Info("[Trgo] �¼�����������Ӧϵͳ���أ�ս������������");
                args.Player?.SendInfoMessage("[Trgo] �¼�������������ͬ������");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] �¼���������������ʱ����: {ex.Message}");
                args.Player?.SendErrorMessage($"[Trgo] �¼�����������ʧ��: {ex.Message}");
            }
        }
        #endregion

        #region ����˺��¼�����
        /// <summary>
        /// ��������˺��¼� - �߼��˺�׷��ϵͳ
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="args">�˺��¼�����</param>
        /// <remarks>
        /// �˺�׷�ٹ��ܣ�
        /// ? ��¼��Ҽ�Ĺ�����ϵ��ʱ���
        /// ? ������ʵ�˺�ֵ�����Ƿ�������
        /// ? ��ֹͬ�����˼�¼
        /// ? ͳ���˺��������ڻ�ɱ�ж�
        /// </remarks>
        private void OnPlayerDamage(object sender, GetDataHandlers.PlayerDamageEventArgs args)
        {
            // ����������֤
            if (!IsValidDamageEvent(args))
                return;

            // ��Ϸ״̬��֤
            if (!IsGameInProgress())
                return;

            var victimIndex = args.ID;
            var attackerIndex = args.Player.Index;

            // ��ȡ��������Ϣ
            var victim = TShock.Players[victimIndex];
            if (!IsValidGameParticipant(victim) || !IsValidGameParticipant(args.Player))
                return;

            // ��ֹ���˺�ͬ���˺�
            if (attackerIndex == victimIndex || IsFriendlyFire(args.Player.Name, victim.Name))
                return;

            // �����˺���¼
            ProcessDamageRecord(victimIndex, attackerIndex, args.Damage);
        }

        /// <summary>
        /// ��֤�˺��¼�����Ч��
        /// </summary>
        private bool IsValidDamageEvent(GetDataHandlers.PlayerDamageEventArgs args)
        {
            return args?.Player != null && 
                   args.ID >= 0 && 
                   args.ID < Main.player.Length &&
                   args.Damage > 0;
        }

        /// <summary>
        /// �����Ϸ�Ƿ��ڽ�����
        /// </summary>
        private bool IsGameInProgress()
        {
            return gameManager.CurrentGameState == GameManager.GameState.InProgress;
        }

        /// <summary>
        /// ��֤����Ƿ�Ϊ��Ч����Ϸ������
        /// </summary>
        private bool IsValidGameParticipant(TSPlayer player)
        {
            return player?.Active == true && 
                   !string.IsNullOrEmpty(player.Name) && 
                   gameManager.PlayersInGame.ContainsKey(player.Name);
        }

        /// <summary>
        /// ����Ƿ�Ϊ�Ѿ��˺�
        /// </summary>
        private bool IsFriendlyFire(string attackerName, string victimName)
        {
            return gameManager.PlayerTeams.ContainsKey(attackerName) &&
                   gameManager.PlayerTeams.ContainsKey(victimName) &&
                   gameManager.PlayerTeams[attackerName] == gameManager.PlayerTeams[victimName];
        }

        /// <summary>
        /// �����˺���¼��ͳ��
        /// </summary>
        private void ProcessDamageRecord(int victimIndex, int attackerIndex, int rawDamage)
        {
            // ȷ��ս�����ݴ���
            EnsureCombatDataExists(victimIndex);
            EnsureCombatDataExists(attackerIndex);

            var victimData = playerCombatData[victimIndex];
            var attackerData = playerCombatData[attackerIndex];

            // ���¹�����ϵ��ʱ���
            victimData.LastAttacker = attackerIndex;
            victimData.LastAttackTime = DateTime.Now;
            attackerData.LastTarget = victimIndex;
            attackerData.LastAttackTime = DateTime.Now;

            // ����ʵ���˺�ֵ�����Ƿ�������
            var actualDamage = (int)Main.CalculateDamagePlayersTakeInPVP(rawDamage, Main.player[victimIndex].statDefense);
            
            // �����˺�ͳ��
            attackerData.TotalDamageDealt += actualDamage;
            victimData.TotalDamageReceived += actualDamage;

            TShock.Log.ConsoleDebug($"[Trgo] �˺���¼: {TShock.Players[attackerIndex]?.Name} -> {TShock.Players[victimIndex]?.Name} ({actualDamage}���˺�)");
        }

        /// <summary>
        /// ȷ�����ս�����ݴ���
        /// </summary>
        private void EnsureCombatDataExists(int playerIndex)
        {
            if (!playerCombatData.ContainsKey(playerIndex))
            {
                playerCombatData[playerIndex] = new PlayerCombatData();
            }
        }
        #endregion

        #region ��������¼�����
        /// <summary>
        /// ������������¼� - ���ܻ�ɱ�����ͳ��ϵͳ
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="args">�����¼�����</param>
        /// <remarks>
        /// ���������ܣ�
        /// ? ���ػ�ɱ�߼���㷨��ֱ��+��ӣ�
        /// ? �Զ���ɱͳ�ƺͽ���ϵͳ
        /// ? �Զ���������Ϣ����
        /// ? ����ģʽ���⴦��ȡ��ը��������
        /// ? ��Ϸʤ��������鴥��
        /// </remarks>
        private void OnKillMe(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            // ������֤
            if (!IsValidDeathEvent(args))
                return;

            var victimIndex = args.Player.Index;
            var victimName = args.Player.Name;

            // ��������ͳ��
            UpdateDeathStatistics(victimName);

            // ������ģʽ�����߼�
            HandleBombModeDeathLogic(victimName);

            // ����ɱ��
            var killer = DetectKiller(args, victimIndex);

            // �����ɱͳ�ƺͽ���
            var deathMessage = ProcessKillStatistics(killer, victimName, args);

            // �����Զ�������ԭ��
            args.PlayerDeathReason._sourceCustomReason = deathMessage;

            // ����ս������
            CleanupCombatData(victimIndex);

            // �����������ݰ�
            SendDeathPacket(args);

            // ����¼��Ѵ���
            args.Handled = true;

            // ֪ͨ��Ϸ���������ʤ������
            gameManager.OnPlayerDeathProcessed(args.Player, killer);
        }

        /// <summary>
        /// ��֤�����¼�����Ч��
        /// </summary>
        private bool IsValidDeathEvent(GetDataHandlers.KillMeEventArgs args)
        {
            return IsGameInProgress() &&
                   args?.Player?.Name != null &&
                   gameManager.PlayersInGame.ContainsKey(args.Player.Name);
        }

        /// <summary>
        /// �����������ͳ��
        /// </summary>
        private void UpdateDeathStatistics(string victimName)
        {
            if (gameManager.PlayerDeaths.ContainsKey(victimName))
            {
                gameManager.PlayerDeaths[victimName]++;
            }
        }

        /// <summary>
        /// ������ģʽ�����������߼�
        /// </summary>
        private void HandleBombModeDeathLogic(string victimName)
        {
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                var bombManager = gameManager.GetBombManager();
                if (bombManager?.IsPlayerBusy(victimName) == true)
                {
                    bombManager.CancelPlayerAction(victimName);
                    TShock.Utils.Broadcast($"?? {victimName} ��ը���������������ж�", Color.Orange);
                }
            }
        }

        /// <summary>
        /// ���ܻ�ɱ�߼���㷨
        /// </summary>
        /// <param name="args">�����¼�����</param>
        /// <param name="victimIndex">�ܺ�������</param>
        /// <returns>��ɱ�ߣ����û���򷵻�null</returns>
        private TSPlayer DetectKiller(GetDataHandlers.KillMeEventArgs args, int victimIndex)
        {
            // ����1��ֱ�Ӵ�����ԭ���ȡ������
            var directKiller = GetDirectKiller(args);
            if (directKiller != null)
            {
                TShock.Log.ConsoleDebug($"[Trgo] ֱ�ӻ�ɱ���: {directKiller.Name}");
                return directKiller;
            }

            // ����2�������˺�׷�ٵļ�ӻ�ɱ���
            var indirectKiller = GetIndirectKiller(victimIndex);
            if (indirectKiller != null)
            {
                TShock.Log.ConsoleDebug($"[Trgo] ��ӻ�ɱ���: {indirectKiller.Name}");
                return indirectKiller;
            }

            TShock.Log.ConsoleDebug("[Trgo] δ��⵽��Ч��ɱ�ߣ��ж�Ϊ��������");
            return null;
        }

        /// <summary>
        /// ��ȡֱ�ӻ�ɱ�ߣ�������ԭ��
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
        /// ��ȡ��ӻ�ɱ�ߣ������˺�׷�٣�
        /// </summary>
        private TSPlayer GetIndirectKiller(int victimIndex)
        {
            if (!playerCombatData.ContainsKey(victimIndex))
                return null;

            var victimData = playerCombatData[victimIndex];
            var timeSinceLastAttack = DateTime.Now - victimData.LastAttackTime;

            // 5���ڵĹ�����Ϊ����Ч��ɱ
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
        /// �����ɱͳ�ƺͽ���ϵͳ
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
        /// ����Ƿ�Ϊ��Ч��ɱ���жԶ��飩
        /// </summary>
        private bool IsValidKill(string killerName, string victimName)
        {
            return gameManager.PlayerTeams.ContainsKey(killerName) &&
                   gameManager.PlayerTeams.ContainsKey(victimName) &&
                   gameManager.PlayerTeams[killerName] != gameManager.PlayerTeams[victimName];
        }

        /// <summary>
        /// ������Ч��ɱ
        /// </summary>
        private string ProcessValidKill(TSPlayer killer, string victimName, GetDataHandlers.KillMeEventArgs args)
        {
            // ���»�ɱͳ��
            if (gameManager.PlayerKills.ContainsKey(killer.Name))
            {
                gameManager.PlayerKills[killer.Name]++;
            }

            // Ӧ�û�ɱ����
            ApplyKillReward(killer);

            // ���ɻ�ɱ��Ϣ
            var deathMessage = GenerateKillMessage(killer, victimName, args);
            
            // �㲥��ɱ��Ϣ
            TShock.Utils.Broadcast(deathMessage, Color.Yellow);

            TShock.Log.Info($"[Trgo] ��ɱͳ��: {killer.Name} ��ɱ�� {victimName}");
            
            return deathMessage;
        }

        /// <summary>
        /// Ӧ�û�ɱ����ϵͳ
        /// </summary>
        private void ApplyKillReward(TSPlayer killer)
        {
            // ����ģʽ��ɱ�������ָ�����ֵ
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                var healAmount = killer.TPlayer.statLifeMax2 / 2; // �ָ�50%����ֵ
                killer.Heal(healAmount);
                killer.SendInfoMessage($"��ɱ�������ָ��� {healAmount} ������ֵ");
                
                TShock.Log.ConsoleDebug($"[Trgo] ����ģʽ��ɱ����: {killer.Name} �ָ� {healAmount} ����ֵ");
            }
        }

        /// <summary>
        /// ���ɻ�ɱ��Ϣ
        /// </summary>
        private string GenerateKillMessage(TSPlayer killer, string victimName, GetDataHandlers.KillMeEventArgs args)
        {
            var killerTeam = gameManager.PlayerTeams[killer.Name] == "red" ? "���" : "����";
            var victimTeam = gameManager.PlayerTeams[victimName] == "red" ? "���" : "����";

            // ��ȡ������Ϣ
            var weaponInfo = GetWeaponInfo(args.PlayerDeathReason._sourceItemType);
            var projectileInfo = GetProjectileInfo(args.PlayerDeathReason.SourceProjectileType);

            // ������������
            var weaponText = GenerateWeaponDescription(weaponInfo, projectileInfo);

            return $"[{killerTeam}] {killer.Name} {weaponText}��ɱ�� [{victimTeam}] {victimName}";
        }

        /// <summary>
        /// ��ȡ������Ϣ
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
        /// ��ȡ��Ļ��Ϣ
        /// </summary>
        private string GetProjectileInfo(int? sourceProjectileType)
        {
            return sourceProjectileType.HasValue 
                ? Lang.GetProjectileName(sourceProjectileType.Value).Value 
                : "";
        }

        /// <summary>
        /// �������������ı�
        /// </summary>
        private string GenerateWeaponDescription(string weaponInfo, string projectileInfo)
        {
            if (!string.IsNullOrEmpty(weaponInfo) || !string.IsNullOrEmpty(projectileInfo))
            {
                return $"��{weaponInfo}{projectileInfo}";
            }
            return "";
        }

        /// <summary>
        /// ������������
        /// </summary>
        private string ProcessAccidentalDeath(string victimName)
        {
            var victimTeam = gameManager.PlayerTeams.ContainsKey(victimName) 
                ? (gameManager.PlayerTeams[victimName] == "red" ? "���" : "����") 
                : "";

            var message = $"[{victimTeam}] {victimName} ��������";
            TShock.Log.Info($"[Trgo] ��������: {message}");
            
            return message;
        }

        /// <summary>
        /// ����ս������
        /// </summary>
        private void CleanupCombatData(int victimIndex)
        {
            if (playerCombatData.ContainsKey(victimIndex))
            {
                playerCombatData[victimIndex].Reset();
            }
        }

        /// <summary>
        /// �����������ݰ�
        /// </summary>
        private void SendDeathPacket(GetDataHandlers.KillMeEventArgs args)
        {
            Main.player[args.PlayerId].KillMe(args.PlayerDeathReason, args.Damage, args.Direction, true);
            NetMessage.SendPlayerDeath(args.PlayerId, args.PlayerDeathReason, args.Damage, args.Direction, true, -1, args.Player.Index);
        }
        #endregion

        #region ��������¼�����
        /// <summary>
        /// ������������¼� - ��Ϸ��������ϵͳ
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="args">�����¼�����</param>
        /// <remarks>
        /// �������ƹ��ܣ�
        /// ? ����ģʽ����ֹ�ִ�������
        /// ? ��������λ�õ���ȷ�Ķ��������
        /// ? �Ŷ�����ģʽ������������
        /// ? ��Ϸ����������
        /// </remarks>
        private void OnPlayerSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
        {
            if (args?.Player?.Name == null)
                return;

            // ֻ����Ϸ�����߲���Ҫ���⴦��
            if (!gameManager.PlayersInGame.ContainsKey(args.Player.Name))
                return;

            // ��Ϸ�����е���������
            if (gameManager.CurrentGameState == GameManager.GameState.InProgress)
            {
                HandleInGameRespawn(args);
            }
            else
            {
                // ��Ϸ�����������������͵����������
                RedirectToTeamSpawn(args.Player);
            }
        }

        /// <summary>
        /// ������Ϸ�е�����
        /// </summary>
        private void HandleInGameRespawn(GetDataHandlers.SpawnEventArgs args)
        {
            var player = args.Player;
            
            if (gameManager.CurrentGameMode == GameManager.GameMode.BombDefusal)
            {
                // ����ģʽ����ֹ�ִ�������
                HandleBombDefusalRespawn(player, args);
            }
            else if (gameManager.CurrentGameMode == GameManager.GameMode.TeamDeathmatch)
            {
                // �Ŷ�����ģʽ���������������͵����������
                HandleTeamDeathmatchRespawn(player, args);
            }
        }

        /// <summary>
        /// ������ģʽ����
        /// </summary>
        private void HandleBombDefusalRespawn(TSPlayer player, GetDataHandlers.SpawnEventArgs args)
        {
            // ����Ƿ����ִμ�ȴ���
            if (IsRoundWaiting())
            {
                // �ִμ�ȴ�����������
                RedirectToTeamSpawn(player);
                player.SendInfoMessage("���Ѹ����һ�ּ�����ʼ");
                return;
            }

            // �ִν�������ֹ����
            args.Handled = true; // ��ֹ����
            
            // �ӳ���Ϣ�������������߼���ͻ
            Task.Delay(1000).ContinueWith(_ =>
            {
                if (player?.Active == true)
                {
                    player.SendErrorMessage("����ģʽ���޷����ִν���ʱ����");
                    player.SendInfoMessage("��ȴ���ǰ�ִν������Զ�����");
                }
            });
            
            TShock.Log.ConsoleDebug($"[Trgo] ��ֹ�� {player.Name} �ڱ���ģʽ�ִ��е�����");
        }

        /// <summary>
        /// �����Ŷ�����ģʽ����
        /// </summary>
        private void HandleTeamDeathmatchRespawn(TSPlayer player, GetDataHandlers.SpawnEventArgs args)
        {
            // �Ŷ�����ģʽ����������������Ҫ���͵����������
            Task.Delay(500).ContinueWith(_ => RedirectToTeamSpawn(player));
            
            player.SendInfoMessage("��������������ս����");
            TShock.Log.ConsoleDebug($"[Trgo] {player.Name} ���Ŷ�����ģʽ������");
        }

        /// <summary>
        /// ����Ƿ����ִμ�ȴ���
        /// </summary>
        private bool IsRoundWaiting()
        {
            // ������Ҫ����GameManager��˽���ֶΣ�������Ҫ��ӹ�������
            // ��ʱ����false��ʵ��ʵ����ҪGameManager�ṩ�ӿ�
            return false;
        }

        /// <summary>
        /// ������ض��򵽶��������
        /// </summary>
        private void RedirectToTeamSpawn(TSPlayer player)
        {
            if (!gameManager.PlayerTeams.ContainsKey(player.Name))
                return;

            try
            {
                // ��ȡ���ú͵�ͼ��Ϣ
                var gameManagerType = gameManager.GetType();
                var configManagerField = gameManagerType.GetField("configManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (configManagerField?.GetValue(gameManager) is ConfigManager configManager)
                {
                    var currentMap = configManager.GetCurrentMap();
                    var team = gameManager.PlayerTeams[player.Name];
                    
                    // �ӳٴ��ͣ�ȷ���������
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        if (player?.Active == true)
                        {
                            if (team == "red")
                            {
                                player.Teleport((int)currentMap.RedSpawn.X * 16, (int)currentMap.RedSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] ��������: {player.Name} -> ��ӳ�����");
                            }
                            else if (team == "blue")
                            {
                                player.Teleport((int)currentMap.BlueSpawn.X * 16, (int)currentMap.BlueSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] ��������: {player.Name} -> ���ӳ�����");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] ��������ʧ��: {ex.Message}");
            }
        }
        #endregion

        #region ����������
        /// <summary>
        /// ����ָ����ҵ�ս������
        /// </summary>
        /// <param name="playerIndex">�������</param>
        public void ClearPlayerData(int playerIndex)
        {
            if (playerCombatData.ContainsKey(playerIndex))
            {
                playerCombatData.Remove(playerIndex);
                TShock.Log.ConsoleDebug($"[Trgo] ��������� {playerIndex} ��ս������");
            }
        }

        /// <summary>
        /// ��������ս������
        /// </summary>
        public void Reset()
        {
            var dataCount = playerCombatData.Count;
            playerCombatData.Clear();
            TShock.Log.Info($"[Trgo] ����������ս������ (��{dataCount}����¼)");
        }

        /// <summary>
        /// ��ȡ���ս��ͳ����Ϣ
        /// </summary>
        /// <param name="playerIndex">�������</param>
        /// <returns>ս�����ݣ�����������򷵻�null</returns>
        public PlayerCombatData GetPlayerCombatData(int playerIndex)
        {
            return playerCombatData.ContainsKey(playerIndex) ? playerCombatData[playerIndex] : null;
        }

        /// <summary>
        /// ��ȡ��ǰ��Ծ��ս����������
        /// </summary>
        public int GetActiveCombatDataCount()
        {
            return playerCombatData.Count;
        }
        #endregion
    }

    /// <summary>
    /// ���ս������ģ��
    /// </summary>
    /// <remarks>
    /// �洢�������Ϸ�е�ս��������ݣ����ڻ�ɱ����ͳ��
    /// </remarks>
    public class PlayerCombatData
    {
        #region ��������
        /// <summary>
        /// ��󹥻��ߵ�����
        /// </summary>
        public int LastAttacker { get; set; } = -1;

        /// <summary>
        /// ��󹥻�Ŀ�������
        /// </summary>
        public int LastTarget { get; set; } = -1;

        /// <summary>
        /// ���һ�ι�����ʱ��
        /// </summary>
        public DateTime LastAttackTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// ���˺����
        /// </summary>
        public int TotalDamageDealt { get; set; } = 0;

        /// <summary>
        /// �ܳ����˺�
        /// </summary>
        public int TotalDamageReceived { get; set; } = 0;
        #endregion

        #region ��������
        /// <summary>
        /// ��������ս������
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
        /// ����Ƿ�����Ч����󹥻���¼
        /// </summary>
        /// <param name="timeoutSeconds">��ʱʱ�䣨�룩</param>
        /// <returns>�����ָ��ʱ�����й�����¼�򷵻�true</returns>
        public bool HasRecentAttack(double timeoutSeconds = 5.0)
        {
            return LastAttacker >= 0 && 
                   (DateTime.Now - LastAttackTime).TotalSeconds <= timeoutSeconds;
        }

        /// <summary>
        /// ��ȡս�����ݵ��ַ�����ʾ
        /// </summary>
        public override string ToString()
        {
            return $"LastAttacker: {LastAttacker}, DamageDealt: {TotalDamageDealt}, DamageReceived: {TotalDamageReceived}";
        }
        #endregion
    }
}