namespace Drx.Sdk.Network.Security
{
    /// <summary>
    /// 提供数据完整性保护，通过签名来防止数据篡改。
    /// </summary>
    public interface IPacketIntegrityProvider
    {
        /// <summary>
        /// 为原始数据添加签名，生成受保护的数据包。
        /// </summary>
        /// <param name="originalData">原始数据。</param>
        /// <returns>包含签名和原始数据的数据包。</returns>
        byte[] Protect(byte[] originalData);

        /// <summary>
        /// 验证数据包的签名，并提取原始数据。
        /// </summary>
        /// <param name="protectedData">受保护的数据包。</param>
        /// <returns>如果签名有效，则返回原始数据；否则返回null。</returns>
        byte[] Unprotect(byte[] protectedData);
    }
} 