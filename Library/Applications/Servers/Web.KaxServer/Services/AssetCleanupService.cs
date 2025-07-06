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

            var userDataRepository = new IndexedRepository<UserData>(Path.Combine(Directory.GetCurrentDirectory(), "user_data"), "user_");
            var userDatas = userDataRepository.GetAll();

            foreach (var userData in userDatas)
            {
                foreach (var asset in userData.OwnedAssets)
                {
                    if (asset.Value <= DateTime.Now)
                    {
                        userData.OwnedAssets.Remove(asset.Key);
                        userDataRepository.Save(userData);
                        _logger.LogInformation($"Expired asset {asset.Key} removed from user {userData.Id}");
                    }
                }
            }

            _logger.LogInformation("Expired assets cleanup task completed.");
            return Task.CompletedTask;
        }
    }
} 