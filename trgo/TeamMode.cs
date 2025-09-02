using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TShockAPI;

namespace trgo
{
    public class TeamMode
    {
        public enum GameMode
        {
            None,
            TeamDeathmatch,
            BombDefusal
        }

        public enum GameState
        {
            Waiting,
            Countdown,
            InProgress,
            Finished
        }

        public class PlayerStats
        {
            public string Name { get; set; }
            public string Team { get; set; }
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public bool IsInvisible { get; set; }
            
            public PlayerStats(string name, string team = "")
            {
                Name = name;
                Team = team;
                Kills = 0;
                Deaths = 0;
                IsInvisible = false;
            }
        }

        public class GameSession
        {
            public GameMode Mode { get; set; }
            public GameState State { get; set; }
            public Dictionary<string, PlayerStats> Players { get; set; }
            public int RedTeamScore { get; set; }
            public int BlueTeamScore { get; set; }
            public int CurrentRound { get; set; }
            public int RoundsToWin { get; set; }
            public bool BombPlanted { get; set; }
            public string BombPlanter { get; set; }
            public Vector2 BombSite { get; set; }
            public int BombTimer { get; set; }
            public int GameTimer { get; set; }
            public int CountdownTimer { get; set; }

            public GameSession()
            {
                Mode = GameMode.None;
                State = GameState.Waiting;
                Players = new Dictionary<string, PlayerStats>();
                RedTeamScore = 0;
                BlueTeamScore = 0;
                CurrentRound = 1;
                RoundsToWin = 3;
                BombPlanted = false;
                BombPlanter = "";
                BombSite = new Vector2(400, 300);
                BombTimer = 45;
                GameTimer = 0;
                CountdownTimer = -1;
            }

            public void Reset()
            {
                State = GameState.Waiting;
                Players.Clear();
                RedTeamScore = 0;
                BlueTeamScore = 0;
                CurrentRound = 1;
                BombPlanted = false;
                BombPlanter = "";
                BombTimer = 45;
                GameTimer = 0;
                CountdownTimer = -1;
            }

            public bool CanJoin(string playerName)
            {
                return State == GameState.Waiting || (State == GameState.Countdown && !Players.ContainsKey(playerName));
            }

            public bool CanChangeTeam(string playerName)
            {
                return State == GameState.Waiting || State == GameState.Countdown;
            }

            public void AddPlayer(string playerName, GameMode preferredMode)
            {
                if (!Players.ContainsKey(playerName))
                {
                    Players[playerName] = new PlayerStats(playerName);
                    if (Mode == GameMode.None)
                    {
                        Mode = preferredMode;
                    }
                }
            }

            public void RemovePlayer(string playerName)
            {
                if (Players.ContainsKey(playerName))
                {
                    Players.Remove(playerName);
                    if (Players.Count == 0)
                    {
                        Mode = GameMode.None;
                    }
                }
            }

            public void SetPlayerTeam(string playerName, string team)
            {
                if (Players.ContainsKey(playerName))
                {
                    Players[playerName].Team = team;
                }
            }

            public bool ShouldStartCountdown()
            {
                if (State != GameState.Waiting) return false;
                
                var playersWithTeams = Players.Values.Count(p => !string.IsNullOrEmpty(p.Team));
                return playersWithTeams >= Players.Count / 2 && Players.Count >= 2;
            }

            public void BalanceTeams()
            {
                var allPlayers = Players.Values.ToList();
                var redTeam = allPlayers.Where(p => p.Team == "red").ToList();
                var blueTeam = allPlayers.Where(p => p.Team == "blue").ToList();
                
                int totalPlayers = allPlayers.Count;
                int maxTeamSize = totalPlayers % 2 == 0 ? totalPlayers / 2 : (totalPlayers / 2) + 1;
                
                // Move excess players from one team to another
                while (redTeam.Count > maxTeamSize)
                {
                    var playerToMove = redTeam.Last();
                    playerToMove.Team = "blue";
                    redTeam.Remove(playerToMove);
                    blueTeam.Add(playerToMove);
                }
                
                while (blueTeam.Count > maxTeamSize)
                {
                    var playerToMove = blueTeam.Last();
                    playerToMove.Team = "red";
                    blueTeam.Remove(playerToMove);
                    redTeam.Add(playerToMove);
                }
                
                // Assign unassigned players randomly
                var unassignedPlayers = allPlayers.Where(p => string.IsNullOrEmpty(p.Team) || (p.Team != "red" && p.Team != "blue")).ToList();
                
                foreach (var player in unassignedPlayers)
                {
                    if (redTeam.Count < maxTeamSize)
                    {
                        player.Team = "red";
                        redTeam.Add(player);
                    }
                    else
                    {
                        player.Team = "blue";
                        blueTeam.Add(player);
                    }
                }
            }

            public List<PlayerStats> GetRedTeam()
            {
                return Players.Values.Where(p => p.Team == "red").ToList();
            }

            public List<PlayerStats> GetBlueTeam()
            {
                return Players.Values.Where(p => p.Team == "blue").ToList();
            }

            public int GetRedTeamKills()
            {
                return Players.Values.Where(p => p.Team == "red").Sum(p => p.Kills);
            }

            public int GetBlueTeamKills()
            {
                return Players.Values.Where(p => p.Team == "blue").Sum(p => p.Kills);
            }

            public void ResetRoundStats()
            {
                BombPlanted = false;
                BombPlanter = "";
                BombTimer = 45;
                
                foreach (var player in Players.Values)
                {
                    player.IsInvisible = false;
                }
            }

            public bool CheckWinCondition(out string winnerTeam, out string message)
            {
                winnerTeam = "";
                message = "";

                if (Mode == GameMode.TeamDeathmatch)
                {
                    const int killsToWin = 30;
                    var redKills = GetRedTeamKills();
                    var blueKills = GetBlueTeamKills();
                    
                    if (redKills >= killsToWin)
                    {
                        winnerTeam = "red";
                        message = $"红队获胜！击杀数: {redKills}";
                        return true;
                    }
                    else if (blueKills >= killsToWin)
                    {
                        winnerTeam = "blue";
                        message = $"蓝队获胜！击杀数: {blueKills}";
                        return true;
                    }
                }
                else if (Mode == GameMode.BombDefusal)
                {
                    if (RedTeamScore >= RoundsToWin)
                    {
                        winnerTeam = "red";
                        message = $"红队获胜！比分: {RedTeamScore}:{BlueTeamScore}";
                        return true;
                    }
                    else if (BlueTeamScore >= RoundsToWin)
                    {
                        winnerTeam = "blue";
                        message = $"蓝队获胜！比分: {RedTeamScore}:{BlueTeamScore}";
                        return true;
                    }
                }

                return false;
            }

            public void ProcessPlayerKill(string killerName, string victimName)
            {
                if (Players.ContainsKey(killerName) && Players.ContainsKey(victimName))
                {
                    var killer = Players[killerName];
                    var victim = Players[victimName];
                    
                    // Only count kills between different teams
                    if (killer.Team != victim.Team && !string.IsNullOrEmpty(killer.Team) && !string.IsNullOrEmpty(victim.Team))
                    {
                        killer.Kills++;
                        victim.Deaths++;
                        
                        TShock.Utils.Broadcast($"{killerName} 击杀了 {victimName}", Color.Yellow);
                    }
                }
            }

            public void UpdateInvisibility()
            {
                if (Mode != GameMode.BombDefusal || State != GameState.InProgress) return;

                var activePlayers = TShock.Players.Where(p => p?.Active == true && Players.ContainsKey(p.Name)).ToList();

                foreach (var player in activePlayers)
                {
                    if (!Players.ContainsKey(player.Name)) continue;
                    
                    var playerStats = Players[player.Name];
                    bool shouldBeInvisible = true;
                    var playerPos = new Vector2(player.TileX, player.TileY);

                    // Check if any enemy is within 30 tiles
                    foreach (var otherPlayer in activePlayers)
                    {
                        if (player == otherPlayer || !Players.ContainsKey(otherPlayer.Name)) continue;
                        
                        var otherStats = Players[otherPlayer.Name];
                        if (playerStats.Team == otherStats.Team) continue;

                        var otherPos = new Vector2(otherPlayer.TileX, otherPlayer.TileY);
                        var distance = Vector2.Distance(playerPos, otherPos);

                        if (distance <= 30)
                        {
                            shouldBeInvisible = false;
                            break;
                        }
                    }

                    // Apply or remove invisibility
                    if (shouldBeInvisible && !playerStats.IsInvisible)
                    {
                        player.SetBuff(14, 120); // Invisibility buff
                        playerStats.IsInvisible = true;
                    }
                    else if (!shouldBeInvisible && playerStats.IsInvisible)
                    {
                        // Clear invisibility buff by setting it to 0 duration
                        player.SetBuff(14, 0);
                        playerStats.IsInvisible = false;
                    }
                }
            }

            public bool CanPlantBomb(string playerName, Vector2 playerPosition)
            {
                if (Mode != GameMode.BombDefusal || State != GameState.InProgress) return false;
                if (!Players.ContainsKey(playerName) || Players[playerName].Team != "red") return false;
                if (BombPlanted) return false;

                var distance = Vector2.Distance(playerPosition, BombSite);
                return distance <= 5;
            }

            public bool CanDefuseBomb(string playerName, Vector2 playerPosition)
            {
                if (Mode != GameMode.BombDefusal || State != GameState.InProgress) return false;
                if (!Players.ContainsKey(playerName) || Players[playerName].Team != "blue") return false;
                if (!BombPlanted) return false;

                var distance = Vector2.Distance(playerPosition, BombSite);
                return distance <= 5;
            }

            public void PlantBomb(string playerName)
            {
                BombPlanted = true;
                BombPlanter = playerName;
                BombTimer = 45;
            }

            public void DefuseBomb()
            {
                BombPlanted = false;
                BombPlanter = "";
            }

            public bool ProcessBombTimer()
            {
                if (!BombPlanted) return false;
                
                BombTimer--;
                return BombTimer <= 0; // Returns true if bomb exploded
            }

            public void EndRound(string winningTeam)
            {
                if (winningTeam == "red")
                {
                    RedTeamScore++;
                }
                else if (winningTeam == "blue")
                {
                    BlueTeamScore++;
                }

                CurrentRound++;
                ResetRoundStats();
            }
        }
    }
}
