using DRX.Framework.Common.Engine;
using DRX.Library.Kax.Configs;

namespace DRX.Library.Kax;

public class DrxServer(DrxServerConfig config) : ServerEngine
{
    private readonly DrxServerConfig _config = config;

    protected override string? GetIp()
    {
        return _config.ServerEndIp;
    }

    protected override int GetPort()
    {
        return int.Parse(_config.ServerEndPort);
    }
}