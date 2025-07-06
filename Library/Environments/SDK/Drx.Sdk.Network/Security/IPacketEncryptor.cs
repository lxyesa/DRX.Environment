namespace Drx.Sdk.Network.Security
{
    public interface IPacketEncryptor
    {
        byte[] Encrypt(byte[] data);
        byte[] Decrypt(byte[] data);
    }
} 