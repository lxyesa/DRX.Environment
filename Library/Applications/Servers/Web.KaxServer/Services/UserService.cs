using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Models;
using Microsoft.Extensions.Logging;
using Drx.Sdk.Network.DataBase;
using System;
using System.Xml.Linq;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;

        public UserService(ILogger<UserService> logger)
        {
            _logger = logger;
        }

        public void SaveUser(UserData user)
        {
            // Assign a new unique ID if it's a new user
            if (user.UserId == 0)
            {
                var allUsers = UserRepository.GetAllUsers();
                int maxId = allUsers.Any() ? allUsers.Max(u => u.UserId) : 0;
                user.UserId = maxId + 1;
            }
            UserRepository.SaveUser(user);
        }

        public IEnumerable<UserData> GetAllUsers()
        {
            return UserRepository.GetAllUsers();
        }

        public UserData? GetUserDataById(int userId)
        {
            return UserRepository.GetUser(userId);
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
            return UserRepository.GetAllUsers().Count;
        }

        public UserSession? AuthenticateUser(string username, string password)
        {
            try
            {
                var userData = UserRepository.GetAllUsers().FirstOrDefault(u =>
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

        public UserData? GetUserDataByUserId(int userId)
        {
            return UserRepository.GetUser(userId);
        }

        public void UpdateUserData(UserData userData)
        {
            UserRepository.SaveUser(userData);
        }
    }
} 