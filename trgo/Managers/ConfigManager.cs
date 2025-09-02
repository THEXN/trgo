using System;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;
using Trgo.Config;

namespace Trgo.Managers
{
    /// <summary>
    /// 配置管理器
    /// </summary>
    public class ConfigManager
    {
        private readonly string configPath;
        private readonly string mapsPath;
        private GameConfig gameConfig;

        public ConfigManager()
        {
            var pluginDirectory = Path.Combine(TShock.SavePath, "Trgo");
            if (!Directory.Exists(pluginDirectory))
                Directory.CreateDirectory(pluginDirectory);

            configPath = Path.Combine(pluginDirectory, "config.json");
            mapsPath = Path.Combine(pluginDirectory, "maps");

            if (!Directory.Exists(mapsPath))
                Directory.CreateDirectory(mapsPath);

            LoadConfig();
        }

        /// <summary>
        /// 获取当前游戏配置
        /// </summary>
        public GameConfig GetConfig()
        {
            return gameConfig;
        }

        /// <summary>
        /// 获取当前地图配置
        /// </summary>
        public MapConfig GetCurrentMap()
        {
            if (gameConfig.Maps.TryGetValue(gameConfig.CurrentMap, out var map))
                return map;

            // 如果找不到当前地图，返回默认地图
            return gameConfig.Maps.TryGetValue("default", out var defaultMap) 
                ? defaultMap 
                : CreateDefaultMap();
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    gameConfig = CreateDefaultConfig();
                    SaveConfig();
                    TShock.Log.Info("[Trgo] 已创建默认配置文件");
                }
                else
                {
                    var json = File.ReadAllText(configPath);
                    gameConfig = JsonConvert.DeserializeObject<GameConfig>(json) ?? CreateDefaultConfig();
                    TShock.Log.Info("[Trgo] 配置文件加载成功");
                }

                // 确保至少有一个默认地图
                if (!gameConfig.Maps.ContainsKey("default"))
                {
                    gameConfig.Maps["default"] = CreateDefaultMap();
                    SaveConfig();
                }

                LoadMapConfigs();
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 加载配置文件失败: {ex.Message}");
                gameConfig = CreateDefaultConfig();
                gameConfig.Maps["default"] = CreateDefaultMap();
            }
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(gameConfig, Formatting.Indented);
                File.WriteAllText(configPath, json);
                TShock.Log.Info("[Trgo] 配置文件保存成功");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载地图配置文件
        /// </summary>
        private void LoadMapConfigs()
        {
            try
            {
                var mapFiles = Directory.GetFiles(mapsPath, "*.json");
                foreach (var mapFile in mapFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(mapFile);
                        var mapConfig = JsonConvert.DeserializeObject<MapConfig>(json);
                        if (mapConfig != null)
                        {
                            var mapName = Path.GetFileNameWithoutExtension(mapFile);
                            gameConfig.Maps[mapName] = mapConfig;
                        }
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.Warn($"[Trgo] 加载地图配置 {mapFile} 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 加载地图配置目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存地图配置
        /// </summary>
        public void SaveMapConfig(string mapName, MapConfig mapConfig)
        {
            try
            {
                var mapFile = Path.Combine(mapsPath, $"{mapName}.json");
                var json = JsonConvert.SerializeObject(mapConfig, Formatting.Indented);
                File.WriteAllText(mapFile, json);
                gameConfig.Maps[mapName] = mapConfig;
                TShock.Log.Info($"[Trgo] 地图配置 {mapName} 保存成功");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] 保存地图配置 {mapName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换当前地图
        /// </summary>
        public bool SetCurrentMap(string mapName)
        {
            if (gameConfig.Maps.ContainsKey(mapName))
            {
                gameConfig.CurrentMap = mapName;
                SaveConfig();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private GameConfig CreateDefaultConfig()
        {
            var config = new GameConfig();
            config.Maps["default"] = CreateDefaultMap();
            return config;
        }

        /// <summary>
        /// 创建默认地图配置
        /// </summary>
        private MapConfig CreateDefaultMap()
        {
            return new MapConfig
            {
                Name = "默认地图",
                Description = "Trgo游戏的默认地图配置",
                RedSpawn = new Microsoft.Xna.Framework.Vector2(200, 200),
                BlueSpawn = new Microsoft.Xna.Framework.Vector2(600, 200),
                BombSites = new System.Collections.Generic.List<BombSiteConfig>
                {
                    new BombSiteConfig
                    {
                        Name = "A点",
                        BoundingBox = new BoundingBoxConfig
                        {
                            TopLeft = new Microsoft.Xna.Framework.Vector2(390, 290),
                            BottomRight = new Microsoft.Xna.Framework.Vector2(410, 310)
                        }
                    },
                    new BombSiteConfig
                    {
                        Name = "B点",
                        BoundingBox = new BoundingBoxConfig
                        {
                            TopLeft = new Microsoft.Xna.Framework.Vector2(390, 490),
                            BottomRight = new Microsoft.Xna.Framework.Vector2(410, 510)
                        }
                    }
                },
                MapBounds = new BoundingBoxConfig
                {
                    TopLeft = new Microsoft.Xna.Framework.Vector2(0, 0),
                    BottomRight = new Microsoft.Xna.Framework.Vector2(1000, 1000)
                }
            };
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            LoadConfig();
        }

        /// <summary>
        /// 获取团队死斗配置
        /// </summary>
        public TeamDeathmatchConfig GetTeamDeathmatchConfig()
        {
            return gameConfig.TeamDeathmatch;
        }

        /// <summary>
        /// 获取爆破模式配置
        /// </summary>
        public BombDefusalConfig GetBombDefusalConfig()
        {
            return gameConfig.BombDefusal;
        }

        /// <summary>
        /// 获取通用配置
        /// </summary>
        public GeneralConfig GetGeneralConfig()
        {
            return gameConfig.General;
        }
    }
}