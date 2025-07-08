using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Data;
using Web.KaxServer.Models.Domain;
using Web.KaxServer.Models.DTOs;

namespace Web.KaxServer.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly SqliteWrapper<UserDto, int> _userDataAccess;
        
        public UserRepository()
        {
            var dbDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "userdata");
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
            }
            var dbPath = Path.Combine(dbDirectory, "user_data.db");
            _userDataAccess = new SqliteWrapper<UserDto, int>(dbPath);
        }
        
        public User GetById(int userId)
        {
            var userDto = _userDataAccess.FindSingle("UserId", userId);
            return userDto != null ? MapToEntity(userDto) : null;
        }
        
        public IEnumerable<User> GetAll()
        {
            return _userDataAccess.GetAll().Select(MapToEntity);
        }
        
        public User GetByUsername(string username)
        {
            var userDto = _userDataAccess.FindSingle("Username", username);
            return userDto != null ? MapToEntity(userDto) : null;
        }
        
        public void Save(User user)
        {
            var userDto = MapToDto(user);
            _userDataAccess.Update(userDto);
        }
        
        public void Delete(int userId)
        {
            var userDto = _userDataAccess.FindSingle("UserId", userId);
            if (userDto != null)
            {
                _userDataAccess.Delete(userDto);
            }
        }

        // 将DTO映射到领域实体
        private User MapToEntity(UserDto dto)
        {
            if (dto == null) return null;

            return new User
            {
                Id = dto.UserId,
                Username = dto.Username,
                Email = dto.Email,
                Password = dto.Password,
                UserPermission = dto.UserPermission,
                Coins = dto.Coins,
                OwnedAssets = dto.OwnedAssets?.Select(a => new UserAsset
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    AssetId = a.AssetId,
                    PurchaseDate = a.PurchaseDate,
                    IsActive = a.IsActive
                }).ToList() ?? new List<UserAsset>(),
                McaCodes = dto.McaCodes?.Select(m => new McaCode
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    AssetId = m.AssetId,
                    CodeValue = m.CodeValue,
                    CreatedAt = m.CreatedAt
                }).ToList() ?? new List<McaCode>(),
                ClientTokens = dto.ClientTokens?.Select(t => new ClientToken
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    AssetId = t.AssetId,
                    Token = t.Token,
                    CreatedAt = t.CreatedAt
                }).ToList() ?? new List<ClientToken>(),
                PublishedAssetIds = dto.PublishedAssetIds?.ToList() ?? new List<int>(),
                AvatarUrl = dto.AvatarUrl,
                SessionId = dto.SessionId,
                CreatedAt = dto.CreatedAt,
                ClientOnline = dto.ClientOnline?.Select(c => new ClientOnline
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    AssetId = c.AssetId,
                    IsOnline = c.IsOnline,
                    LastOnlineTime = c.LastOnlineTime
                }).ToList() ?? new List<ClientOnline>(),
                Banned = dto.Banned,
                BanEndTime = dto.BanEndTime
            };
        }
        
        // 将领域实体映射到DTO
        private UserDto MapToDto(User entity)
        {
            if (entity == null) return null;
            
            return new UserDto
            {
                UserId = entity.Id,
                Username = entity.Username,
                Email = entity.Email,
                Password = entity.Password,
                UserPermission = entity.UserPermission,
                Coins = entity.Coins,
                OwnedAssets = entity.OwnedAssets?.Select(a => new UserAssetDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    AssetId = a.AssetId,
                    PurchaseDate = a.PurchaseDate,
                    IsActive = a.IsActive
                }).ToList() ?? new List<UserAssetDto>(),
                McaCodes = entity.McaCodes?.Select(m => new McaCodeDto
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    AssetId = m.AssetId,
                    CodeValue = m.CodeValue,
                    CreatedAt = m.CreatedAt
                }).ToList() ?? new List<McaCodeDto>(),
                ClientTokens = entity.ClientTokens?.Select(t => new ClientTokenDto
                {
                    Id = t.Id,
                    UserId = t.UserId,
                    AssetId = t.AssetId,
                    Token = t.Token,
                    CreatedAt = t.CreatedAt
                }).ToList() ?? new List<ClientTokenDto>(),
                PublishedAssetIds = entity.PublishedAssetIds?.ToList() ?? new List<int>(),
                AvatarUrl = entity.AvatarUrl,
                SessionId = entity.SessionId,
                CreatedAt = entity.CreatedAt,
                ClientOnline = entity.ClientOnline?.Select(c => new ClientOnlineDto
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    AssetId = c.AssetId,
                    IsOnline = c.IsOnline,
                    LastOnlineTime = c.LastOnlineTime
                }).ToList() ?? new List<ClientOnlineDto>(),
                Banned = entity.Banned,
                BanEndTime = entity.BanEndTime
            };
        }
    }
} 