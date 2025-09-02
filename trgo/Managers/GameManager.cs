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
    /// ��Ϸ������
    /// </summary>
    public class GameManager
    {
        private readonly ConfigManager configManager;
        private readonly BombManager bombManager;

        // ��Ϸ״̬
        public enum GameMode { None, TeamDeathmatch, BombDefusal }
        public enum GameState { Waiting, Countdown, InProgress }

        public GameMode CurrentGameMode { get; private set; } = GameMode.None;
        public GameState CurrentGameState { get; private set; } = GameState.Waiting;

        // �������
        public Dictionary<string, GameMode> PlayersInGame { get; private set; } = new Dictionary<string, GameMode>();
        public Dictionary<string, string> PlayerTeams { get; private set; } = new Dictionary<string, string>();
        public Dictionary<string, int> PlayerKills { get; private set; } = new Dictionary<string, int>();
        public Dictionary<string, int> PlayerDeaths { get; private set; } = new Dictionary<string, int>();

        // ��Ϸ��ʱ
        private int countdown = -1;
        private int gameTimer = 0;

        // ����ģʽ���� - ������ʱ���ͻ��߻���
        private int redRounds = 0;
        private int blueRounds = 0;
        private int currentRound = 1;
        private int roundWaitTimer = -1; // �ִμ�ȴ���ʱ��
        private bool isOvertime = false; // �Ƿ��ڼ�ʱ��
        private int overtimeRounds = 0; // ��ʱ������
        private bool teamsSwapped = false; // �Ƿ��ѻ���
        
        // ԭʼ�������׷�� - ������ȷ�Ʒ�
        private readonly Dictionary<string, string> originalTeamIdentity = new Dictionary<string, string>();
        
        // ��ʱ��ʤ����ʷ׷�� - ��������ʤ���ж�
        private readonly List<string> overtimeWinHistory = new List<string>();

        // ����ϵͳ
        private readonly Dictionary<string, bool> playersInInvisRange = new Dictionary<string, bool>();

        public GameManager(ConfigManager configManager, BombManager bombManager)
        {
            this.configManager = configManager;
            this.bombManager = bombManager;
        }

        /// <summary>
        /// ��ȡ��ǰ�ִ�
        /// </summary>
        public int CurrentRound => currentRound;

        /// <summary>
        /// ��ȡ����ִ�
        /// </summary>
        public int RedRounds => redRounds;

        /// <summary>
        /// ��ȡ�����ִ�
        /// </summary>
        public int BlueRounds => blueRounds;

        /// <summary>
        /// ÿ�������Ϸ�߼�
        /// </summary>
        public void Update()
        {
            HandleCountdown();
            HandleGameLogic();
            HandleInvisibilityLogic();
        }

        /// <summary>
        /// ������ʱ
        /// </summary>
        private void HandleCountdown()
        {
            if (countdown > 0)
            {
                countdown--;
                TShock.Utils.Broadcast($"��Ϸ���� {countdown} ���ʼ", Color.Green);
                if (countdown == 0)
                {
                    StartGame();
                }
            }
            else if (countdown == -1 && ShouldStartCountdown())
            {
                var config = configManager.GetGeneralConfig();
                countdown = config.CountdownTime;
                TShock.Utils.Broadcast($"׼�������Ѵ�꣬��Ϸ���� {countdown} ���ʼ", Color.Green);
                CurrentGameState = GameState.Countdown;
            }
        }

        /// <summary>
        /// ����Ƿ�Ӧ�ÿ�ʼ����ʱ
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
        /// ������Ϸ�߼�
        /// </summary>
        private void HandleGameLogic()
        {
            if (CurrentGameState != GameState.InProgress) return;

            gameTimer++;

            // �����ִμ�ȴ�
            if (roundWaitTimer > 0)
            {
                roundWaitTimer--;
                if (roundWaitTimer > 0)
                {
                    TShock.Utils.Broadcast($"��һ�ֽ��� {roundWaitTimer} ���ʼ", Color.Yellow);
                    return;
                }
                else
                {
                    // �ȴ���������ʼ��һ��
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
        /// ������ģʽ�߼�
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

            // ����Ŷ�����ʤ������
            CheckTeamEliminationWin();
        }

        /// <summary>
        /// ����Ŷ�����ʤ������
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
                    // ���ը�����°���������Ҫ������ܻ�ʤ
                    return;
                }
                EndRound("blue", "����ȫ�ߺ�ӻ�ʤ���֣�");
            }
            else if (aliveBlue.Count == 0 && aliveRed.Count > 0)
            {
                EndRound("red", "���ȫ�����ӻ�ʤ���֣�");
            }
        }

        /// <summary>
        /// ���������߼�
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

                // ����Ƿ��е����ڷ�Χ��
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

                // Ӧ�û��Ƴ�����
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
        /// ����Ŷ�����ʤ������
        /// </summary>
        private void CheckTeamDeathmatchWin()
        {
            var config = configManager.GetTeamDeathmatchConfig();
            var redKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "red").Sum(p => p.Value);
            var blueKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "blue").Sum(p => p.Value);

            if (redKills >= config.KillsToWin)
            {
                EndGame("red", $"��ӻ�ʤ����ɱ��: {redKills}");
            }
            else if (blueKills >= config.KillsToWin)
            {
                EndGame("blue", $"���ӻ�ʤ����ɱ��: {blueKills}");
            }
        }

        /// <summary>
        /// ��Ҽ�����Ϸ
        /// </summary>
        public bool TogglePlayerInGame(string playerName, GameMode mode)
        {
            if (CurrentGameState == GameState.InProgress)
            {
                return false; // ��Ϸ�������޷�����
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
        /// ������Ҷ���
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
        /// ��ʼ��Ϸ
        /// </summary>
        private void StartGame()
        {
            AssignTeams();
            InitializePlayerStats();
            TeleportPlayersAndSetEquipment();

            CurrentGameState = GameState.InProgress;
            gameTimer = 0;

            var modeText = CurrentGameMode == GameMode.TeamDeathmatch ? "�Ŷ�����" : "����ģʽ";
            TShock.Utils.Broadcast($"=== {modeText}��Ϸ��ʼ�� ===", Color.Green);

            if (CurrentGameMode == GameMode.BombDefusal)
            {
                var config = configManager.GetBombDefusalConfig();
                TShock.Utils.Broadcast("��ӣ���������Ҫ�ڱ��Ƶ��°������������ط�", Color.Yellow);
                TShock.Utils.Broadcast("���ӣ��ط�����Ҫ��ֹ�°������ը�����������й���", Color.Yellow);
                TShock.Utils.Broadcast($"����ģʽ����: ��Ӯ�� {config.RoundsToWin} �ֵĶ����ʤ", Color.Cyan);
            }
            else
            {
                var config = configManager.GetTeamDeathmatchConfig();
                TShock.Utils.Broadcast($"�Ŷ���������: �ȴﵽ {config.KillsToWin} ��ɱ�Ķ����ʤ", Color.Cyan);
            }
        }

        /// <summary>
        /// �������
        /// </summary>
        private void AssignTeams()
        {
            var allPlayers = PlayersInGame.Keys.ToList();
            var redTeam = PlayerTeams.Where(p => p.Value == "red").Select(p => p.Key).ToList();
            var blueTeam = PlayerTeams.Where(p => p.Value == "blue").Select(p => p.Key).ToList();

            int totalPlayers = allPlayers.Count;
            int maxTeamSize = totalPlayers % 2 == 0 ? totalPlayers / 2 : (totalPlayers / 2) + 1;

            // ƽ�����
            var unassignedPlayers = allPlayers.Except(redTeam).Except(blueTeam).ToList();

            // ���ĳ���������࣬�ƶ����
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

            // ����δ��������
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

            TShock.Utils.Broadcast($"��� ({redTeam.Count}��): {string.Join(", ", redTeam)}", Color.Red);
            TShock.Utils.Broadcast($"���� ({blueTeam.Count}��): {string.Join(", ", blueTeam)}", Color.Blue);
        }

        /// <summary>
        /// ��ʼ�����ͳ��
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
                roundWaitTimer = -1; // �����ִεȴ���ʱ��
                isOvertime = false; // ���ü�ʱ��״̬
                overtimeRounds = 0;
                teamsSwapped = false;
                
                // ��ʼ��ԭʼ�������
                originalTeamIdentity.Clear();
                foreach (var kvp in PlayerTeams)
                {
                    originalTeamIdentity[kvp.Key] = kvp.Value; // ��¼ÿ����ҵ�ԭʼ����
                }
                
                bombManager.Reset();
                playersInInvisRange.Clear();
            }
        }

        /// <summary>
        /// ������Ҳ�����װ��
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
                            // �����ұ�������ֹװ���ѻ���- ��ȫ�ط��ʱ���
                            int maxInventorySize = Math.Min(player.TPlayer.inventory.Length, 58);
                            for (int i = 0; i < maxInventorySize; i++)
                            {
                                if (i < player.TPlayer.inventory.Length)
                                {
                                    player.TPlayer.inventory[i].SetDefaults();
                                }
                            }

                            // ���ݶ������ö�����ɫ�ʹ��͵���Ӧ������
                            if (team == "red")
                            {
                                player.SetTeam(1); // ���
                                player.Teleport((int)map.RedSpawn.X * 16, (int)map.RedSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] ���ͺ����� {playerName} ������ ({map.RedSpawn.X}, {map.RedSpawn.Y})");
                            }
                            else if (team == "blue")
                            {
                                player.SetTeam(3); // ����
                                player.Teleport((int)map.BlueSpawn.X * 16, (int)map.BlueSpawn.Y * 16);
                                TShock.Log.ConsoleDebug($"[Trgo] ����������� {playerName} ������ ({map.BlueSpawn.X}, {map.BlueSpawn.Y})");
                            }

                            // ���ų�ʼװ��
                            foreach (var item in config.InitialItems)
                            {
                                player.GiveItem(item.Id, item.Stack, item.Prefix);
                            }

                            // ȷ�������Ѫ��ħ
                            player.Heal(player.TPlayer.statLifeMax);
                            player.TPlayer.statMana = player.TPlayer.statManaMax2;
                            player.SendData(PacketTypes.PlayerMana, "", player.Index);
                            
                            // ���ͱ������� - ��ȫ�ط�����������
                            int inventorySize = Math.Min(player.TPlayer.inventory.Length, NetItem.MaxInventory);
                            for (int i = 0; i < inventorySize; i++)
                            {
                                if (i < player.TPlayer.inventory.Length)
                                {
                                    NetMessage.SendData(5, -1, -1, null, player.Index, i, player.TPlayer.inventory[i].prefix);
                                }
                            }

                            player.SendInfoMessage($"�Ѵ��͵�{(team == "red" ? "���" : "����")}�����㲢�ַ�װ��");
                        }
                        catch (Exception ex)
                        {
                            TShock.Log.Error($"[Trgo] ������� {playerName} ʱ����: {ex.Message}");
                            player.SendErrorMessage("���͹����г��ִ�������ϵ����Ա");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] TeleportPlayersAndSetEquipment �����쳣: {ex.Message}");
                TShock.Log.Error($"[Trgo] ��ջ����: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// �����ִ� - ʵ��CS:GOʽ���������޸����߼Ʒ����⣩
        /// </summary>
        private void EndRound(string winningTeam, string message)
        {
            TShock.Utils.Broadcast($"=== {message} ===", winningTeam == "red" ? Color.Red : Color.Blue);

            // ���ݻ�ʤ�����ԭʼ��ݽ��мƷ�
            var originalWinningTeam = GetOriginalTeamForCurrentWinner(winningTeam);
            
            // ��¼��ʱ��ʤ����ʷ
            if (isOvertime)
            {
                overtimeWinHistory.Add(originalWinningTeam);
                TShock.Log.ConsoleDebug($"[Trgo] ��ʱ��ʤ����¼: {originalWinningTeam}, ��ʷ: [{string.Join(", ", overtimeWinHistory)}]");
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
            var targetWinRounds = config.RoundsToWin; // ����13�ֻ�ʤ
            var maxRounds = (targetWinRounds - 1) * 2; // ����24����(12+12)

            // ����Ƿ���Ҫ����
            CheckTeamSwap(targetWinRounds);

            // ����ʤ����
            if (CheckWinCondition(targetWinRounds, maxRounds))
            {
                return; // ��Ϸ�ѽ���
            }

            // ������һ��
            ContinueToNextRound(config);
        }

        /// <summary>
        /// ��ȡ��ǰ��ʤ�����ԭʼ�������
        /// </summary>
        private string GetOriginalTeamForCurrentWinner(string currentWinningTeam)
        {
            // �ҵ���ǰ��ʤ�����е�����һ����ң��鿴��ԭʼ�������
            var winningPlayer = PlayerTeams.FirstOrDefault(p => p.Value == currentWinningTeam);
            
            if (winningPlayer.Key != null && originalTeamIdentity.ContainsKey(winningPlayer.Key))
            {
                return originalTeamIdentity[winningPlayer.Key];
            }
            
            // ����Ҳ��������ص�ǰ���飨�����ݣ�
            return currentWinningTeam;
        }

        /// <summary>
        /// ����Ƿ���Ҫ����
        /// </summary>
        private void CheckTeamSwap(int targetWinRounds)
        {
            var totalRounds = redRounds + blueRounds;
            var halfRounds = targetWinRounds - 1; // ����12��

            // ����������ߣ���12�ֺ�
            if (!isOvertime && totalRounds == halfRounds && !teamsSwapped)
            {
                SwapTeams();
                teamsSwapped = true;
                TShock.Utils.Broadcast($"=== �ϰ볡��������ǰ�ȷ� {redRounds}:{blueRounds} ===", Color.Orange);
                TShock.Utils.Broadcast("���ӻ��ߣ�", Color.Orange);
                return;
            }

            // ��ʱ�����ߣ�ÿ2�ֻ�һ�Σ�
            if (isOvertime && overtimeRounds > 0 && overtimeRounds % 2 == 0)
            {
                SwapTeams();
                TShock.Utils.Broadcast("��ʱ�����ߣ�", Color.Orange);
                
                // ��ʱ�����ߺ�����ʤ����ʷ����Ϊ����ʤ�������ã�
                overtimeWinHistory.Clear();
                TShock.Log.ConsoleDebug("[Trgo] ��ʱ�����ߣ�����ʤ����ʷ");
            }
        }

        /// <summary>
        /// ����ʤ����
        /// </summary>
        private bool CheckWinCondition(int targetWinRounds, int maxRounds)
        {
            // �����ʤ�������ﵽĿ������
            if (!isOvertime)
            {
                if (redRounds >= targetWinRounds)
                {
                    EndGame("red", $"��ӻ�ʤ�����ձȷ�: {redRounds}:{blueRounds}");
                    return true;
                }
                if (blueRounds >= targetWinRounds)
                {
                    EndGame("blue", $"���ӻ�ʤ�����ձȷ�: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            var totalRounds = redRounds + blueRounds;

            // ����Ƿ�����ʱ��
            if (!isOvertime && totalRounds >= maxRounds)
            {
                // �ȷ���Ƚ����ʱ��
                if (redRounds == blueRounds)
                {
                    StartOvertime();
                }
                // ����1�ֵ��Ѵ��������ִ�
                else if (Math.Abs(redRounds - blueRounds) == 1)
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "���" : "����";
                    EndGame(winner, $"{winnerName}��ʤ�����ձȷ�: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            // ��ʱ����ʤ����������Ӯ��2�ֻ�����������
            if (isOvertime)
            {
                // ����1���������2�ֻ�ʤ
                var consecutiveWins = CheckConsecutiveWins();
                if (consecutiveWins != null)
                {
                    var winnerName = consecutiveWins == "red" ? "���" : "����";
                    EndGame(consecutiveWins, $"{winnerName}��ʱ����ʤ�����ձȷ�: {redRounds}:{blueRounds} (OT)");
                    return true;
                }

                // ����2����ֹ���޼�ʱ�� - ���ĳ���������ȣ���������4�֣�
                if (Math.Abs(redRounds - blueRounds) >= 4)
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "���" : "����";
                    EndGame(winner, $"{winnerName}��ʱ����ȷֻ�ʤ�����ձȷ�: {redRounds}:{blueRounds} (OT)");
                    TShock.Log.Info($"[Trgo] ��ʱ�����������Ƚ���: {redRounds}:{blueRounds}");
                    return true;
                }

                // ����3��������ʱ������ - ��ֹ���޽��У������ܹ�50�ֺ�ǿ�ƽ�����
                if (totalRounds >= maxRounds + 20) // ����24�� + ���20�ּ�ʱ
                {
                    var winner = redRounds > blueRounds ? "red" : "blue";
                    var winnerName = winner == "red" ? "���" : "����";
                    EndGame(winner, $"{winnerName}������ʱ����ʤ�����ձȷ�: {redRounds}:{blueRounds} (OT)");
                    TShock.Log.Info($"[Trgo] ��ʱ����ﵽ����������ƽ���: {redRounds}:{blueRounds}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// ���������ʤ���
        /// </summary>
        private string CheckConsecutiveWins()
        {
            if (overtimeWinHistory.Count < 2)
            {
                TShock.Log.ConsoleDebug($"[Trgo] ��ʤ���: ��ʷ����2�� ({overtimeWinHistory.Count}��)");
                return null;
            }

            // �����������Ƿ���ͬһ����������ʤ
            var lastTwo = overtimeWinHistory.TakeLast(2).ToList();
            
            if (lastTwo.Count == 2 && lastTwo[0] == lastTwo[1])
            {
                TShock.Log.ConsoleDebug($"[Trgo] ��⵽����ʤ��: {lastTwo[0]}����ʤ2��");
                TShock.Log.Info($"[Trgo] ��ʱ����ʤ����: {lastTwo[0]}����ʤ����ʷ: [{string.Join(", ", overtimeWinHistory)}]");
                return lastTwo[0];
            }

            TShock.Log.ConsoleDebug($"[Trgo] ��ʤ���: ������� [{string.Join(", ", lastTwo)}]������ʤ");
            return null;
        }

        /// <summary>
        /// ���߲���
        /// </summary>
        private void SwapTeams()
        {
            var tempTeams = new Dictionary<string, string>();
            
            // ����������ҵĶ���
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
                    tempTeams[playerName] = currentTeam; // ����ԭ����
                }
            }

            // Ӧ�ö�����
            PlayerTeams.Clear();
            foreach (var kvp in tempTeams)
            {
                PlayerTeams[kvp.Key] = kvp.Value;
            }

            TShock.Log.Info($"[Trgo] �����ѻ��� - �ִ� {currentRound}");
        }

        /// <summary>
        /// ��ʼ��ʱ��
        /// </summary>
        private void StartOvertime()
        {
            isOvertime = true;
            overtimeRounds = 0;
            overtimeWinHistory.Clear(); // ���ʤ����ʷ
            
            TShock.Utils.Broadcast("=== �����ʱ����===", Color.Gold);
            TShock.Utils.Broadcast($"��ǰ�ȷ�: {redRounds}:{blueRounds}", Color.Gold);
            TShock.Utils.Broadcast("��ʱ����������Ӯ��2�ֵĶ����ʤ", Color.Gold);
            TShock.Utils.Broadcast("ÿ2�ֽ���һ�λ���", Color.Gold);
            
            // ��ʱ����ʼʱ����
            SwapTeams();
            TShock.Utils.Broadcast("��ʱ����ʼ�����ӻ��ߣ�", Color.Orange);
            
            TShock.Log.Info("[Trgo] ��ʱ����ʼ��ʤ����ʷ������");
        }

        /// <summary>
        /// ��ȡ�������ʤ���������ѷ�����ʹ���µ�����ʤ������߼���
        /// </summary>
        [Obsolete("�ѷ�����ʹ���µ�����ʤ������߼�")]
        private int GetRecentWins(string team)
        {
            // ��������Ѿ����µ� CheckConsecutiveWins() �������
            // �����˷����Է�������󣬵�ʵ�ʲ���ʹ��
            return 0;
        }

        /// <summary>
        /// ��������һ��
        /// </summary>
        private void ContinueToNextRound(BombDefusalConfig config)
        {
            // ����������������Ϸ�����
            ReviveAllPlayers();

            // ��ʼ�ִμ�ȴ�
            currentRound++;
            if (isOvertime) 
            {
                overtimeRounds++;
                TShock.Log.ConsoleDebug($"[Trgo] ��ʱ���ִε���: {overtimeRounds}, ���ִ�: {currentRound}");
            }
            
            bombManager.Reset();
            playersInInvisRange.Clear();
            
            // ���õȴ���ʱ��
            roundWaitTimer = config.RoundWaitTime;
            
            // ��ʾ�ִ���Ϣ
            var roundInfo = isOvertime ? $"�� {currentRound} �� (��ʱ {overtimeRounds})" : $"�� {currentRound} ��";
            TShock.Utils.Broadcast($"=== {roundInfo} ׼���� ===", Color.Yellow);
            TShock.Utils.Broadcast($"��ǰ�ȷ� ���:{redRounds} - ����:{blueRounds}", Color.Cyan);
            
            // ��ʾ��ʱ��״̬
            if (isOvertime)
            {
                var winHistory = overtimeWinHistory.Count > 0 ? $"[{string.Join(", ", overtimeWinHistory.TakeLast(4))}]" : "[��]";
                TShock.Utils.Broadcast($"��ʱ��״̬: ������2�ֻ�ʤ | ���ʤ��: {winHistory}", Color.Gold);
            }
            
            if (roundWaitTimer > 0)
            {
                TShock.Utils.Broadcast($"��������Ѹ����һ�ֽ��� {roundWaitTimer} ���ʼ", Color.Green);
            }
            else
            {
                // ����ȴ�ʱ��Ϊ0��������ʼ��һ��
                StartNextRound();
            }
        }

        /// <summary>
        /// ��ȡ�Ƿ����ִεȴ�״̬
        /// </summary>
        public bool IsRoundWaiting => roundWaitTimer > 0;

        /// <summary>
        /// ��ȡը�������������¼�������ʹ�ã�
        /// </summary>
        public BombManager GetBombManager()
        {
            return bombManager;
        }

        /// <summary>
        /// ǿ�ƽ�����Ϸ
        /// </summary>
        public void ForceEndGame(string reason)
        {
            EndGame("", reason);
        }

        /// <summary>
        /// ǿ�ƿ�ʼ��Ϸ
        /// </summary>
        public bool ForceStartGame()
        {
            if (PlayersInGame.Count >= 2)
            {
                // �����κ����еĵ���ʱ
                countdown = 3;
                CurrentGameState = GameState.Countdown;
                TShock.Utils.Broadcast("����Աǿ�ƿ�ʼ��Ϸ����ʱ��", Color.Orange);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ����������������߼������¼����������ã�
        /// </summary>
        public void OnPlayerDeathProcessed(TSPlayer deadPlayer, TSPlayer killer = null)
        {
            // �����������ֻ��������Ϸ״̬����ɱͳ�����¼�����������
            // �ڱ���ģʽ�¼���Ƿ���Ҫ�����ִ�
            if (CurrentGameMode == GameMode.BombDefusal)
            {
                // �ӳټ��ʤ������������������һ��ʱ��
                Task.Delay(1000).ContinueWith(_ => CheckTeamEliminationWin());
            }
        }

        /// <summary>
        /// ������Ϸ
        /// </summary>
        private void EndGame(string winningTeam, string message)
        {
            TShock.Utils.Broadcast($"?? {message} ??", winningTeam == "red" ? Color.Red : Color.Blue);

            // ��ʾ����ͳ��
            if (CurrentGameMode == GameMode.TeamDeathmatch)
            {
                TShock.Utils.Broadcast("=== ����ͳ�� ===", Color.Cyan);
                var redKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "red").Sum(p => p.Value);
                var blueKills = PlayerKills.Where(p => PlayerTeams.ContainsKey(p.Key) && PlayerTeams[p.Key] == "blue").Sum(p => p.Value);
                TShock.Utils.Broadcast($"����ܻ�ɱ: {redKills}", Color.Red);
                TShock.Utils.Broadcast($"�����ܻ�ɱ: {blueKills}", Color.Blue);
            }

            // ������Ϸ״̬
            CurrentGameState = GameState.Waiting;
            CurrentGameMode = GameMode.None;
            countdown = -1;

            // �����������
            PlayersInGame.Clear();
            PlayerTeams.Clear();
            PlayerKills.Clear();
            PlayerDeaths.Clear();
            playersInInvisRange.Clear();
            
            // ���ü�ʱ���ͻ���״̬
            isOvertime = false;
            overtimeRounds = 0;
            teamsSwapped = false;
            
            // ���ԭʼ������ݼ�¼�ͼ�ʱ����ʷ
            originalTeamIdentity.Clear();
            overtimeWinHistory.Clear();
            
            // ����ը��������
            bombManager.Reset();

            // ����������Ҷ���
            foreach (var player in TShock.Players.Where(p => p?.Active == true))
            {
                player.SetTeam(0);
            }

            TShock.Utils.Broadcast("������������ã����Կ�ʼ�µ���Ϸ��", Color.Green);
        }

        /// <summary>
        /// ����������Ϸ�е����
        /// </summary>
        private void ReviveAllPlayers()
        {
            foreach (var playerName in PlayersInGame.Keys.ToList())
            {
                var player = TShock.Players.FirstOrDefault(p => p?.Name == playerName);
                if (player?.Active == true)
                {
                    // �������
                    if (player.TPlayer.statLife <= 0 || player.Dead)
                    {
                        player.Spawn(PlayerSpawnContext.ReviveFromDeath);
                        player.TPlayer.statLife = player.TPlayer.statLifeMax;
                        player.SendData(PacketTypes.PlayerHp, "", player.Index);
                    }
                    
                    // ȷ�������Ѫ
                    player.Heal(player.TPlayer.statLifeMax);
                }
            }
            
            TShock.Log.Info("[Trgo] �Ѹ���������Ϸ�е����");
        }

        /// <summary>
        /// ��ʼ��һ��
        /// </summary>
        private void StartNextRound()
        {
            var roundInfo = isOvertime ? $"�� {currentRound} �� (��ʱ {overtimeRounds})" : $"�� {currentRound} ��";
            TShock.Utils.Broadcast($"=== {roundInfo} ��ʼ�� ===", Color.Green);
            
            // ��ʾ������Ϣ����ʾԭʼ��ݺ͵�ǰ��ʶ��
            ShowTeamInfo();
            
            // ����������ҵ������㲢���·ַ�װ��
            TeleportPlayersAndSetEquipment();
            
            roundWaitTimer = -1; // ���õȴ���ʱ��
        }

        /// <summary>
        /// ��ʾ������Ϣ������ԭʼ��ݣ�
        /// </summary>
        private void ShowTeamInfo()
        {
            var currentRedPlayers = PlayerTeams.Where(p => p.Value == "red").Select(p => p.Key).ToList();
            var currentBluePlayers = PlayerTeams.Where(p => p.Value == "blue").Select(p => p.Key).ToList();

            // ͳ��ԭʼ����
            var originalRedCount = currentRedPlayers.Count(p => originalTeamIdentity.GetValueOrDefault(p) == "red");
            var originalBlueCount = currentRedPlayers.Count(p => originalTeamIdentity.GetValueOrDefault(p) == "blue");
            
            if (teamsSwapped)
            {
                TShock.Utils.Broadcast($"?? �����ѻ��ߣ�", Color.Orange);
                TShock.Utils.Broadcast($"���λ�� ({currentRedPlayers.Count}��): {string.Join(", ", currentRedPlayers)} [ԭ����]", Color.Red);
                TShock.Utils.Broadcast($"����λ�� ({currentBluePlayers.Count}��): {string.Join(", ", currentBluePlayers)} [ԭ���]", Color.Blue);
            }
            else
            {
                TShock.Utils.Broadcast($"��� ({currentRedPlayers.Count}��): {string.Join(", ", currentRedPlayers)}", Color.Red);
                TShock.Utils.Broadcast($"���� ({currentBluePlayers.Count}��): {string.Join(", ", currentBluePlayers)}", Color.Blue);
            }
        }
    }
}