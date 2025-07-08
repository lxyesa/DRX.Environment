using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Drx.Sdk.Network.DataBase;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.Services
{
    public class AssetCleanupService : BackgroundService
    {
        private readonly ILogger<AssetCleanupService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(1); // Run once every hour

        public AssetCleanupService(ILogger<AssetCleanupService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Asset Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredAssets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while cleaning up expired assets.");
                }

                await Task.Delay(_period, stoppingToken);
            }

            _logger.LogInformation("Asset Cleanup Service is stopping.");
        }

        private Task CleanupExpiredAssets()
        {
            _logger.LogInformation("Running expired assets cleanup task.");

            var userDatas = UserRepository.GetAllUsers();

            foreach (var userData in userDatas)
            {
                foreach (var asset in userData.OwnedAssets)
                {
                    if (asset.Value <= DateTime.Now)
                    {
                        userData.OwnedAssets.Remove(asset.Key);
                        UserRepository.SaveUser(userData);
                        _logger.LogInformation($"Expired asset {asset.Key} removed from user {userData.UserId}");
                    }
                }
            }

            _logger.LogInformation("Expired assets cleanup task completed.");
            return Task.CompletedTask;
        }
    }
} 