using System;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;
using Trgo.Config;

namespace Trgo.Managers
{
    /// <summary>
    /// ���ù�����
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
        /// ��ȡ��ǰ��Ϸ����
        /// </summary>
        public GameConfig GetConfig()
        {
            return gameConfig;
        }

        /// <summary>
        /// ��ȡ��ǰ��ͼ����
        /// </summary>
        public MapConfig GetCurrentMap()
        {
            if (gameConfig.Maps.TryGetValue(gameConfig.CurrentMap, out var map))
                return map;

            // ����Ҳ�����ǰ��ͼ������Ĭ�ϵ�ͼ
            return gameConfig.Maps.TryGetValue("default", out var defaultMap) 
                ? defaultMap 
                : CreateDefaultMap();
        }

        /// <summary>
        /// ���������ļ�
        /// </summary>
        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    gameConfig = CreateDefaultConfig();
                    SaveConfig();
                    TShock.Log.Info("[Trgo] �Ѵ���Ĭ�������ļ�");
                }
                else
                {
                    var json = File.ReadAllText(configPath);
                    gameConfig = JsonConvert.DeserializeObject<GameConfig>(json) ?? CreateDefaultConfig();
                    TShock.Log.Info("[Trgo] �����ļ����سɹ�");
                }

                // ȷ��������һ��Ĭ�ϵ�ͼ
                if (!gameConfig.Maps.ContainsKey("default"))
                {
                    gameConfig.Maps["default"] = CreateDefaultMap();
                    SaveConfig();
                }

                LoadMapConfigs();
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] ���������ļ�ʧ��: {ex.Message}");
                gameConfig = CreateDefaultConfig();
                gameConfig.Maps["default"] = CreateDefaultMap();
            }
        }

        /// <summary>
        /// ���������ļ�
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var json = JsonConvert.SerializeObject(gameConfig, Formatting.Indented);
                File.WriteAllText(configPath, json);
                TShock.Log.Info("[Trgo] �����ļ�����ɹ�");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] ���������ļ�ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// ���ص�ͼ�����ļ�
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
                        TShock.Log.Warn($"[Trgo] ���ص�ͼ���� {mapFile} ʧ��: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] ���ص�ͼ����Ŀ¼ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// �����ͼ����
        /// </summary>
        public void SaveMapConfig(string mapName, MapConfig mapConfig)
        {
            try
            {
                var mapFile = Path.Combine(mapsPath, $"{mapName}.json");
                var json = JsonConvert.SerializeObject(mapConfig, Formatting.Indented);
                File.WriteAllText(mapFile, json);
                gameConfig.Maps[mapName] = mapConfig;
                TShock.Log.Info($"[Trgo] ��ͼ���� {mapName} ����ɹ�");
            }
            catch (Exception ex)
            {
                TShock.Log.Error($"[Trgo] �����ͼ���� {mapName} ʧ��: {ex.Message}");
            }
        }

        /// <summary>
        /// �л���ǰ��ͼ
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
        /// ����Ĭ������
        /// </summary>
        private GameConfig CreateDefaultConfig()
        {
            var config = new GameConfig();
            config.Maps["default"] = CreateDefaultMap();
            return config;
        }

        /// <summary>
        /// ����Ĭ�ϵ�ͼ����
        /// </summary>
        private MapConfig CreateDefaultMap()
        {
            return new MapConfig
            {
                Name = "Ĭ�ϵ�ͼ",
                Description = "Trgo��Ϸ��Ĭ�ϵ�ͼ����",
                RedSpawn = new Microsoft.Xna.Framework.Vector2(200, 200),
                BlueSpawn = new Microsoft.Xna.Framework.Vector2(600, 200),
                BombSites = new System.Collections.Generic.List<BombSiteConfig>
                {
                    new BombSiteConfig
                    {
                        Name = "A��",
                        BoundingBox = new BoundingBoxConfig
                        {
                            TopLeft = new Microsoft.Xna.Framework.Vector2(390, 290),
                            BottomRight = new Microsoft.Xna.Framework.Vector2(410, 310)
                        }
                    },
                    new BombSiteConfig
                    {
                        Name = "B��",
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
        /// ���¼�������
        /// </summary>
        public void ReloadConfig()
        {
            LoadConfig();
        }

        /// <summary>
        /// ��ȡ�Ŷ���������
        /// </summary>
        public TeamDeathmatchConfig GetTeamDeathmatchConfig()
        {
            return gameConfig.TeamDeathmatch;
        }

        /// <summary>
        /// ��ȡ����ģʽ����
        /// </summary>
        public BombDefusalConfig GetBombDefusalConfig()
        {
            return gameConfig.BombDefusal;
        }

        /// <summary>
        /// ��ȡͨ������
        /// </summary>
        public GeneralConfig GetGeneralConfig()
        {
            return gameConfig.General;
        }
    }
}