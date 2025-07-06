using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Models;
using Microsoft.Extensions.Logging;
using Drx.Sdk.Network.DataBase;
using System;
using System.Xml.Linq;

namespace Web.KaxServer.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly IndexedRepository<UserData> _userRepository;

        public UserService(ILogger<UserService> logger)
        {
            _logger = logger;
            var usersDataPath = Path.Combine(AppContext.BaseDirectory, "user_data");
            _userRepository = new IndexedRepository<UserData>(usersDataPath, "user_");
            
            // Attempt to migrate old data if the new repository is empty.
            if (!_userRepository.GetAll().Any())
            {
                MigrateFromOldXml();
            }
        }
        
        private void MigrateFromOldXml()
        {
            var oldUsersXmlPath = Path.Combine(AppContext.BaseDirectory, "user", "users.xml");
            if (!File.Exists(oldUsersXmlPath)) return;

            _logger.LogInformation("Old users.xml found. Starting data migration...");
            try
            {
                var doc = XDocument.Load(oldUsersXmlPath);
                var usersToMigrate = doc.Descendants("user")
                    .Select(u => {
                        var userData = new UserData
                        {
                            UserId = (int?)u.Attribute("userid") ?? 0,
                            Username = (string)u.Attribute("username"),
                            Email = (string)u.Attribute("email"),
                            Password = (string)u.Attribute("password"),
                            UserPermission = Enum.TryParse<UserPermissionType>((string)u.Attribute("permission"), true, out var p) ? p : UserPermissionType.Normal,
                            Coins = (decimal?)u.Attribute("coins") ?? 0m,
                            AvatarUrl = (string)u.Attribute("avatarUrl"),
                            SessionId = (string)u.Attribute("sessionId"),
                            CreatedAt = DateTime.TryParse((string)u.Attribute("createdAt"), out var dt) ? dt : DateTime.UtcNow,
                            McaCodes = new Dictionary<int, string>()
                        };
                        
                        var ownedAssetsString = (string)u.Attribute("ownedAssetIds");
                        if (!string.IsNullOrEmpty(ownedAssetsString))
                        {
                            userData.OwnedAssets = ownedAssetsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(part => part.Split(new[] { ':' }, 2))
                                .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out _))
                                .ToDictionary(parts => int.Parse(parts[0]), parts => DateTime.Parse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind));
                        }
                        else
                        {
                            userData.OwnedAssets = new Dictionary<int, DateTime>();
                        }
                        return userData;
                    })
                    .ToList();

                if (usersToMigrate.Any())
                {
                    _userRepository.SaveAll(usersToMigrate);
                    _logger.LogInformation($"Successfully migrated {usersToMigrate.Count} users to the new repository.");
                    // File.Move(oldUsersXmlPath, oldUsersXmlPath + ".migrated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during data migration from old users.xml.");
            }
        }
        
        public void SaveUser(UserData user)
        {
            // Assign a new unique ID if it's a new user
            if (user.UserId == 0)
            {
                var allUsers = _userRepository.GetAll();
                int maxId = allUsers.Any() ? allUsers.Max(u => u.UserId) : 0;
                user.UserId = maxId + 1;
            }
            _userRepository.Save(user);
        }

        public IEnumerable<UserData> GetAllUsers()
        {
            return _userRepository.GetAll();
        }

        public UserData? GetUserDataById(int userId)
        {
            return _userRepository.Get(userId.ToString());
        }

        public UserSession? GetUserById(int userId)
        {
            var userData = GetUserDataById(userId);
            return userData != null ? new UserSession(userData) : null;
        }

        public string GetUsernameById(int userId)
        {
            if (userId <= 0) return "匿名";
            var user = GetUserDataById(userId);
            return user?.Username ?? "未知用户";
        }

        public int GetTotalUsersCount()
        {
            return _userRepository.GetAll().Count;
        }

        public UserSession? AuthenticateUser(string username, string password)
        {
            try
            {
                var userData = _userRepository.GetAll().FirstOrDefault(u => 
                    u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

                if (userData != null && userData.Password == password)
                {
                    _logger.LogInformation($"User '{username}' authenticated successfully.");
                    return new UserSession(userData);
                }

                _logger.LogWarning($"Authentication failed for user '{username}'.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while authenticating user {Username}", username);
                return null;
            }
        }
    }
} 