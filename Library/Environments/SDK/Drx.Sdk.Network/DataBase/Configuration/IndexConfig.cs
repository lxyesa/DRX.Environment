namespace Drx.Sdk.Network.DataBase.Configuration
{
    /// <summary>
    /// 哈希算法
    /// </summary>
    public enum HashAlgorithm
    {
        SHA256,
        MD5,
        SHA1
    }

    /// <summary>
    /// 哈希冲突解决策略
    /// </summary>
    public enum CollisionStrategy
    {
        Overwrite,
        ThrowException,
        AppendCounter
    }

    /// <summary>
    /// 基于哈希的文件命名配置
    /// </summary>
    public class HashFileNamingConfig
    {
        public HashAlgorithm HashAlgorithm { get; set; } = HashAlgorithm.SHA256;
        public bool UseShortNames { get; set; } = false;
        public bool IncludeMetadataInHash { get; set; } = false;
        public CollisionStrategy CollisionStrategy { get; set; } = CollisionStrategy.ThrowException;
    }

    /// <summary>
    /// 索引系统配置
    /// </summary>
    public class IndexSystemConfig
    {
        public string RootPath { get; set; }
        public bool UseHashForFilenames { get; set; } = false;
        public int MaxItemsPerFile { get; set; } = 1000;
        public bool AutoCreateDirectories { get; set; } = true;
        public HashFileNamingConfig HashConfig { get; set; }
    }
} 