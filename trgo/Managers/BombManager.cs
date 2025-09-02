using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TShockAPI;
using Trgo.Config;

namespace Trgo.Managers
{
    /// <summary>
    /// ը��������
    /// </summary>
    public class BombManager
    {
        private readonly ConfigManager configManager;
        
        // ը��״̬
        private bool bombPlanted = false;
        private string bombPlanter = "";
        private BombSiteConfig activeBombSite;
        private int bombTimer = 0;
        
        // �°�/�������
        private readonly Dictionary<string, BombAction> playerActions = new Dictionary<string, BombAction>();
        private readonly Dictionary<string, int> actionProgress = new Dictionary<string, int>();

        public BombManager(ConfigManager configManager)
        {
            this.configManager = configManager;
        }

        /// <summary>
        /// ը����������
        /// </summary>
        private enum BombAction
        {
            None,
            Planting,
            Defusing
        }

        /// <summary>
        /// ը���Ƿ��ѱ�����
        /// </summary>
        public bool IsBombPlanted => bombPlanted;

        /// <summary>
        /// ը��ʣ��ʱ��
        /// </summary>
        public int BombTimer => bombTimer;

        /// <summary>
        /// ��Ծ�ı��Ƶ�
        /// </summary>
        public BombSiteConfig ActiveBombSite => activeBombSite;

        /// <summary>
        /// ����ը��״̬
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
        /// ��ʼ�°�
        /// </summary>
        public bool StartPlanting(TSPlayer player)
        {
            if (bombPlanted)
            {
                player.SendInfoMessage("ը���Ѿ�������");
                return false;
            }

            var map = configManager.GetCurrentMap();
            var playerPos = new Vector2(player.TileX, player.TileY);
            
            // ����������ڵı��Ƶ�
            var bombSite = map.BombSites.FirstOrDefault(site => site.IsPlayerInRange(playerPos));
            if (bombSite == null)
            {
                player.SendInfoMessage("����Ҫ�ڱ��Ƶ㷶Χ�ڲ����°�");
                return false;
            }

            if (playerActions.ContainsKey(player.Name))
            {
                player.SendInfoMessage("������ִ����������");
                return false;
            }

            playerActions[player.Name] = BombAction.Planting;
            actionProgress[player.Name] = 0;
            activeBombSite = bombSite;

            player.SendInfoMessage($"��ʼ�� {bombSite.Name} �°�...");
            TShock.Utils.Broadcast($"{player.Name} ��ʼ�� {bombSite.Name} �°���", Color.Orange);

            return true;
        }

        /// <summary>
        /// ��ʼ���
        /// </summary>
        public bool StartDefusing(TSPlayer player)
        {
            if (!bombPlanted)
            {
                player.SendInfoMessage("û��ը����Ҫ���");
                return false;
            }

            var playerPos = new Vector2(player.TileX, player.TileY);
            if (!activeBombSite.IsPlayerInRange(playerPos))
            {
                player.SendInfoMessage($"����Ҫ�� {activeBombSite.Name} ��Χ�ڲ��ܲ��");
                return false;
            }

            if (playerActions.ContainsKey(player.Name))
            {
                player.SendInfoMessage("������ִ����������");
                return false;
            }

            playerActions[player.Name] = BombAction.Defusing;
            actionProgress[player.Name] = 0;

            player.SendInfoMessage($"��ʼ�� {activeBombSite.Name} ���...");
            TShock.Utils.Broadcast($"{player.Name} ��ʼ�����", Color.Cyan);

            return true;
        }

        /// <summary>
        /// ȡ����Ҳ���
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
                    var actionText = action == BombAction.Planting ? "�°�" : "���";
                    player.SendInfoMessage($"{actionText}���ж�");
                    TShock.Utils.Broadcast($"{playerName} ��{actionText}���ж�", Color.Yellow);
                }
            }
        }

        /// <summary>
        /// ����ը��״̬��ÿ����ã�
        /// </summary>
        public BombUpdateResult Update()
        {
            var result = new BombUpdateResult();
            var config = configManager.GetBombDefusalConfig();

            // ����ը������ʱ
            if (bombPlanted)
            {
                bombTimer--;
                if (bombTimer <= 0)
                {
                    result.BombExploded = true;
                    result.Message = "ը����ը���ֲ����ӻ�ʤ���֣�";
                    Reset();
                    return result;
                }

                if (bombTimer % config.BombWarningInterval == 0)
                {
                    result.WarningMessage = $"ը������ {bombTimer} ���ը��";
                }
            }

            // ������Ҷ�������
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

                // �������Ƿ�����ȷλ��
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

                // ���½���
                actionProgress[playerName]++;
                var maxTime = action == BombAction.Planting ? config.PlantTime : config.DefuseTime;
                var progress = actionProgress[playerName];

                // ��ʾ����
                var percentage = (int)((double)progress / maxTime * 100);
                var actionText = action == BombAction.Planting ? "�°�" : "���";
                player.SendMessage($"{actionText}����: {percentage}% ({progress}/{maxTime})", Color.Yellow);

                // ����Ƿ����
                if (progress >= maxTime)
                {
                    completedActions.Add(playerName);

                    if (action == BombAction.Planting)
                    {
                        bombPlanted = true;
                        bombPlanter = playerName;
                        bombTimer = config.BombTimer;
                        result.BombPlanted = true;
                        result.Message = $"{playerName} �� {activeBombSite.Name} �ɹ��°���������Ҫ�� {bombTimer} ���ڲ��ը����";
                    }
                    else if (action == BombAction.Defusing)
                    {
                        bombPlanted = false;
                        result.BombDefused = true;
                        result.Message = $"{playerName} �ɹ����ը�������ӻ�ʤ���֣�";
                        Reset();
                    }
                }
            }

            // �Ƴ�����ɵĶ���
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
        /// �������Ƿ���ִ��ը������
        /// </summary>
        public bool IsPlayerBusy(string playerName)
        {
            return playerActions.ContainsKey(playerName);
        }

        /// <summary>
        /// ��ȡ��ҵ�ǰ����
        /// </summary>
        public string GetPlayerActionStatus(string playerName)
        {
            if (!playerActions.ContainsKey(playerName))
                return "";

            var action = playerActions[playerName];
            var progress = actionProgress[playerName];
            var config = configManager.GetBombDefusalConfig();
            var maxTime = action == BombAction.Planting ? config.PlantTime : config.DefuseTime;
            var actionText = action == BombAction.Planting ? "�°�" : "���";

            return $"����{actionText}: {progress}/{maxTime}��";
        }
    }

    /// <summary>
    /// ը�����½��
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