using System;
using Drx.Sdk.Shared.Serialization;

namespace Drx.Sdk.Network.V2.Socket.Packet;

/// <summary>
/// 数据包构建器，将数据包构建为 DSD 格式。
/// </summary>
public class PacketBuilder2DSD
{
    private readonly DrxSerializationData _dsd = new();
    public PacketBuilder2DSD()
    {
    }

    public byte[] Build()
    {
        return _dsd.Serialize();
    }

    public PacketBuilder2DSD Add(string key, string? value)
    {
        _dsd.SetString(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, int value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, long value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, bool value)
    {
        _dsd.SetBool(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, byte[]? value)
    {
        _dsd.SetBytes(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, float value)
    {
        _dsd.SetFloat(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, double value)
    {
        _dsd.SetDouble(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, DrxSerializationData? value)
    {
        _dsd.SetObject(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, ulong value)
    {
        _dsd.SetInt(key, (long)value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, uint value)
    {
        _dsd.SetInt(key, (long)value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, short value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, ushort value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, byte value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, sbyte value)
    {
        _dsd.SetInt(key, value);
        return this;
    }

    public PacketBuilder2DSD Add(string key, IntPtr value)
    {
        _dsd.SetInt(key, value.ToInt64());
        return this;
    }
}
