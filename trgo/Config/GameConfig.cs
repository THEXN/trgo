using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.IO;
using TShockAPI;

namespace Trgo.Config
{
    /// <summary>
    /// ��Ϸ����������
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// �Ŷ�����ģʽ����
        /// </summary>
        [JsonProperty("�Ŷ�����ģʽ")]
        public TeamDeathmatchConfig TeamDeathmatch { get; set; } = new TeamDeathmatchConfig();

        /// <summary>
        /// ����ģʽ����
        /// </summary>
        [JsonProperty("����ģʽ")]
        public BombDefusalConfig BombDefusal { get; set; } = new BombDefusalConfig();

        /// <summary>
        /// ͨ����Ϸ����
        /// </summary>
        [JsonProperty("ͨ������")]
        public GeneralConfig General { get; set; } = new GeneralConfig();

        /// <summary>
        /// ��ͼ�����б�
        /// </summary>
        [JsonProperty("��ͼ�б�")]
        public Dictionary<string, MapConfig> Maps { get; set; } = new Dictionary<string, MapConfig>();

        /// <summary>
        /// ��ǰʹ�õĵ�ͼ����
        /// </summary>
        [JsonProperty("��ǰ��ͼ")]
        public string CurrentMap { get; set; } = "default";
    }

    /// <summary>
    /// ͨ����Ϸ����
    /// </summary>
    public class GeneralConfig
    {
        /// <summary>
        /// ��ʼ��Ϸ����ʱʱ�䣨�룩
        /// </summary>
        [JsonProperty("��ʼ����ʱ")]
        public int CountdownTime { get; set; } = 10;

        /// <summary>
        /// ���ٿ�ʼ��Ϸ����
        /// </summary>
        [JsonProperty("��������")]
        public int MinPlayersToStart { get; set; } = 2;

        /// <summary>
        /// ��Ҫ׼������ұ�����0.0-1.0��
        /// </summary>
        [JsonProperty("׼����ұ���")]
        public double ReadyPlayersRatio { get; set; } = 0.5;

        /// <summary>
        /// ����Χ����
        /// </summary>
        [JsonProperty("����Χ")]
        public int InvisibilityRange { get; set; } = 30;

        /// <summary>
        /// ����buff����ʱ�䣨tick��
        /// </summary>
        [JsonProperty("�������ʱ��")]
        public int InvisibilityDuration { get; set; } = 120;

        /// <summary>
        /// ��ʼװ������
        /// </summary>
        [JsonProperty("��ʼװ��")]
        public List<ItemConfig> InitialItems { get; set; } = new List<ItemConfig>
        {
            new ItemConfig { Id = 1, Stack = 1 },  // Iron Sword
            new ItemConfig { Id = 2, Stack = 1 },  // Iron Pickaxe
            new ItemConfig { Id = 28, Stack = 50 } // Lesser Healing Potion
        };
    }

    /// <summary>
    /// �Ŷ�����ģʽ����
    /// </summary>
    public class TeamDeathmatchConfig
    {
        /// <summary>
        /// ��ʤ�����ɱ��
        /// </summary>
        [JsonProperty("��ʤ��ɱ��")]
        public int KillsToWin { get; set; } = 30;
    }

    /// <summary>
    /// ����ģʽ����
    /// </summary>
    public class BombDefusalConfig
    {
        /// <summary>
        /// ��ʤ��������
        /// </summary>
        [JsonProperty("��ʤ����")]
        public int RoundsToWin { get; set; } = 3;

        /// <summary>
        /// ը����ըʱ�䣨�룩
        /// </summary>
        [JsonProperty("ը����ըʱ��")]
        public int BombTimer { get; set; } = 45;

        /// <summary>
        /// �°�����ʱ�䣨�룩
        /// </summary>
        [JsonProperty("�°�ʱ��")]
        public int PlantTime { get; set; } = 3;

        /// <summary>
        /// �������ʱ�䣨�룩
        /// </summary>
        [JsonProperty("���ʱ��")]
        public int DefuseTime { get; set; } = 5;

        /// <summary>
        /// ը�����Ѽ�����룩
        /// </summary>
        [JsonProperty("ը�����Ѽ��")]
        public int BombWarningInterval { get; set; } = 10;

        /// <summary>
        /// �ִμ�ȴ�ʱ�䣨�룩- ����Ҹ����׼����ʱ��
        /// </summary>
        [JsonProperty("�ִμ�ȴ�ʱ��")]
        public int RoundWaitTime { get; set; } = 5;
    }

    /// <summary>
    /// ��ͼ����
    /// </summary>
    public class MapConfig
    {
        /// <summary>
        /// ��ͼ����
        /// </summary>
        [JsonProperty("��ͼ����")]
        public string Name { get; set; } = "";

        /// <summary>
        /// ��ͼ����
        /// </summary>
        [JsonProperty("��ͼ����")]
        public string Description { get; set; } = "";

        /// <summary>
        /// ��ӳ�����
        /// </summary>
        [JsonProperty("��ӳ�����")]
        public Vector2 RedSpawn { get; set; } = new Vector2(200, 200);

        /// <summary>
        /// ���ӳ�����
        /// </summary>
        [JsonProperty("���ӳ�����")]
        public Vector2 BlueSpawn { get; set; } = new Vector2(600, 200);

        /// <summary>
        /// ���Ƶ������б�
        /// </summary>
        [JsonProperty("���Ƶ��б�")]
        public List<BombSiteConfig> BombSites { get; set; } = new List<BombSiteConfig>
        {
            new BombSiteConfig 
            { 
                Name = "A��",
                BoundingBox = new BoundingBoxConfig
                {
                    TopLeft = new Vector2(390, 290),
                    BottomRight = new Vector2(410, 310)
                }
            }
        };

        /// <summary>
        /// ��ͼ�߽�
        /// </summary>
        [JsonProperty("��ͼ�߽�")]
        public BoundingBoxConfig MapBounds { get; set; } = new BoundingBoxConfig
        {
            TopLeft = new Vector2(0, 0),
            BottomRight = new Vector2(1000, 1000)
        };
    }

    /// <summary>
    /// ���Ƶ�����
    /// </summary>
    public class BombSiteConfig
    {
        /// <summary>
        /// ���Ƶ�����
        /// </summary>
        [JsonProperty("����")]
        public string Name { get; set; } = "";

        /// <summary>
        /// ���Ƶ�����߽�
        /// </summary>
        [JsonProperty("����߽�")]
        public BoundingBoxConfig BoundingBox { get; set; } = new BoundingBoxConfig();

        /// <summary>
        /// �������Ƿ��ڱ��Ƶ㷶Χ��
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
    /// �߽������
    /// </summary>
    public class BoundingBoxConfig
    {
        /// <summary>
        /// ���Ͻ�����
        /// </summary>
        [JsonProperty("���Ͻ�")]
        public Vector2 TopLeft { get; set; }

        /// <summary>
        /// ���½�����
        /// </summary>
        [JsonProperty("���½�")]
        public Vector2 BottomRight { get; set; }
    }

    /// <summary>
    /// ��Ʒ����
    /// </summary>
    public class ItemConfig
    {
        /// <summary>
        /// ��ƷID
        /// </summary>
        [JsonProperty("��ƷID")]
        public int Id { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        [JsonProperty("����")]
        public int Stack { get; set; } = 1;

        /// <summary>
        /// ǰ׺����ѡ��
        /// </summary>
        [JsonProperty("ǰ׺")]
        public byte Prefix { get; set; } = 0;
    }
}