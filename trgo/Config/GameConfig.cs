using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.IO;
using TShockAPI;

namespace Trgo.Config
{
    /// <summary>
    /// 游戏基础配置类
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// 团队死斗模式配置
        /// </summary>
        [JsonProperty("团队死斗模式")]
        public TeamDeathmatchConfig TeamDeathmatch { get; set; } = new TeamDeathmatchConfig();

        /// <summary>
        /// 爆破模式配置
        /// </summary>
        [JsonProperty("爆破模式")]
        public BombDefusalConfig BombDefusal { get; set; } = new BombDefusalConfig();

        /// <summary>
        /// 通用游戏配置
        /// </summary>
        [JsonProperty("通用配置")]
        public GeneralConfig General { get; set; } = new GeneralConfig();

        /// <summary>
        /// 地图配置列表
        /// </summary>
        [JsonProperty("地图列表")]
        public Dictionary<string, MapConfig> Maps { get; set; } = new Dictionary<string, MapConfig>();

        /// <summary>
        /// 当前使用的地图名称
        /// </summary>
        [JsonProperty("当前地图")]
        public string CurrentMap { get; set; } = "default";
    }

    /// <summary>
    /// 通用游戏配置
    /// </summary>
    public class GeneralConfig
    {
        /// <summary>
        /// 开始游戏倒计时时间（秒）
        /// </summary>
        [JsonProperty("开始倒计时")]
        public int CountdownTime { get; set; } = 10;

        /// <summary>
        /// 最少开始游戏人数
        /// </summary>
        [JsonProperty("最少人数")]
        public int MinPlayersToStart { get; set; } = 2;

        /// <summary>
        /// 需要准备的玩家比例（0.0-1.0）
        /// </summary>
        [JsonProperty("准备玩家比例")]
        public double ReadyPlayersRatio { get; set; } = 0.5;

        /// <summary>
        /// 隐身范围（格）
        /// </summary>
        [JsonProperty("隐身范围")]
        public int InvisibilityRange { get; set; } = 30;

        /// <summary>
        /// 隐身buff持续时间（tick）
        /// </summary>
        [JsonProperty("隐身持续时间")]
        public int InvisibilityDuration { get; set; } = 120;

        /// <summary>
        /// 初始装备配置
        /// </summary>
        [JsonProperty("初始装备")]
        public List<ItemConfig> InitialItems { get; set; } = new List<ItemConfig>
        {
            new ItemConfig { Id = 1, Stack = 1 },  // Iron Sword
            new ItemConfig { Id = 2, Stack = 1 },  // Iron Pickaxe
            new ItemConfig { Id = 28, Stack = 50 } // Lesser Healing Potion
        };
    }

    /// <summary>
    /// 团队死斗模式配置
    /// </summary>
    public class TeamDeathmatchConfig
    {
        /// <summary>
        /// 获胜所需击杀数
        /// </summary>
        [JsonProperty("获胜击杀数")]
        public int KillsToWin { get; set; } = 30;
    }

    /// <summary>
    /// 爆破模式配置
    /// </summary>
    public class BombDefusalConfig
    {
        /// <summary>
        /// 获胜所需轮数
        /// </summary>
        [JsonProperty("获胜轮数")]
        public int RoundsToWin { get; set; } = 3;

        /// <summary>
        /// 炸弹爆炸时间（秒）
        /// </summary>
        [JsonProperty("炸弹爆炸时间")]
        public int BombTimer { get; set; } = 45;

        /// <summary>
        /// 下包所需时间（秒）
        /// </summary>
        [JsonProperty("下包时间")]
        public int PlantTime { get; set; } = 3;

        /// <summary>
        /// 拆包所需时间（秒）
        /// </summary>
        [JsonProperty("拆包时间")]
        public int DefuseTime { get; set; } = 5;

        /// <summary>
        /// 炸弹提醒间隔（秒）
        /// </summary>
        [JsonProperty("炸弹提醒间隔")]
        public int BombWarningInterval { get; set; } = 10;

        /// <summary>
        /// 轮次间等待时间（秒）- 给玩家复活和准备的时间
        /// </summary>
        [JsonProperty("轮次间等待时间")]
        public int RoundWaitTime { get; set; } = 5;
    }

    /// <summary>
    /// 地图配置
    /// </summary>
    public class MapConfig
    {
        /// <summary>
        /// 地图名称
        /// </summary>
        [JsonProperty("地图名称")]
        public string Name { get; set; } = "";

        /// <summary>
        /// 地图描述
        /// </summary>
        [JsonProperty("地图描述")]
        public string Description { get; set; } = "";

        /// <summary>
        /// 红队出生点
        /// </summary>
        [JsonProperty("红队出生点")]
        public Vector2 RedSpawn { get; set; } = new Vector2(200, 200);

        /// <summary>
        /// 蓝队出生点
        /// </summary>
        [JsonProperty("蓝队出生点")]
        public Vector2 BlueSpawn { get; set; } = new Vector2(600, 200);

        /// <summary>
        /// 爆破点配置列表
        /// </summary>
        [JsonProperty("爆破点列表")]
        public List<BombSiteConfig> BombSites { get; set; } = new List<BombSiteConfig>
        {
            new BombSiteConfig 
            { 
                Name = "A点",
                BoundingBox = new BoundingBoxConfig
                {
                    TopLeft = new Vector2(390, 290),
                    BottomRight = new Vector2(410, 310)
                }
            }
        };

        /// <summary>
        /// 地图边界
        /// </summary>
        [JsonProperty("地图边界")]
        public BoundingBoxConfig MapBounds { get; set; } = new BoundingBoxConfig
        {
            TopLeft = new Vector2(0, 0),
            BottomRight = new Vector2(1000, 1000)
        };
    }

    /// <summary>
    /// 爆破点配置
    /// </summary>
    public class BombSiteConfig
    {
        /// <summary>
        /// 爆破点名称
        /// </summary>
        [JsonProperty("名称")]
        public string Name { get; set; } = "";

        /// <summary>
        /// 爆破点区域边界
        /// </summary>
        [JsonProperty("区域边界")]
        public BoundingBoxConfig BoundingBox { get; set; } = new BoundingBoxConfig();

        /// <summary>
        /// 检查玩家是否在爆破点范围内
        /// </summary>
        public bool IsPlayerInRange(Vector2 playerPos)
        {
            return playerPos.X >= BoundingBox.TopLeft.X &&
                   playerPos.X <= BoundingBox.BottomRight.X &&
                   playerPos.Y >= BoundingBox.TopLeft.Y &&
                   playerPos.Y <= BoundingBox.BottomRight.Y;
        }
    }

    /// <summary>
    /// 边界框配置
    /// </summary>
    public class BoundingBoxConfig
    {
        /// <summary>
        /// 左上角坐标
        /// </summary>
        [JsonProperty("左上角")]
        public Vector2 TopLeft { get; set; }

        /// <summary>
        /// 右下角坐标
        /// </summary>
        [JsonProperty("右下角")]
        public Vector2 BottomRight { get; set; }
    }

    /// <summary>
    /// 物品配置
    /// </summary>
    public class ItemConfig
    {
        /// <summary>
        /// 物品ID
        /// </summary>
        [JsonProperty("物品ID")]
        public int Id { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        [JsonProperty("数量")]
        public int Stack { get; set; } = 1;

        /// <summary>
        /// 前缀（可选）
        /// </summary>
        [JsonProperty("前缀")]
        public byte Prefix { get; set; } = 0;
    }
}