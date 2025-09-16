using System;
using Drx.Sdk.Shared.Serialization;

namespace Drx.Sdk.Network.V2.Socket.Packet;

/// <summary>
/// 数据包构建器，将数据包构建为 DSD 格式。
/// </summary>
public class PacketBuilder2DSD : DrxSerializationData
{
    public PacketBuilder2DSD() : base()
    {
    }

    public byte[] Build()
    {
        return Serialize();
    }
}
