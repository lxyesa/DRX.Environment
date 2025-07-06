using Drx.Sdk.Network.DataBase;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Web.KaxServer.Models
{
    /// <summary>
    /// Represents the complete persisted data for a user.
    /// This class is designed to be serialized and deserialized by the XmlDatabase system.
    /// </summary>
    public class UserData : IXmlSerializable, IIndexable
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserPermissionType UserPermission { get; set; }
        public decimal Coins { get; set; }
        public Dictionary<int, DateTime> OwnedAssets { get; set; } = new Dictionary<int, DateTime>();
        public Dictionary<int, string> McaCodes { get; set; } = new Dictionary<int, string>();
        public List<int> PublishedAssetIds { get; set; } = new List<int>();
        public string AvatarUrl { get; set; }
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Implementation for IIndexable
        public string Id => UserId.ToString();

        /// <summary>
        /// Reads data from an IXmlNode and populates the object's properties.
        /// </summary>
        public void ReadFromXml(IXmlNode node)
        {
            UserId = node.GetInt("data", "UserId");
            Username = node.GetString("data", "Username") ?? string.Empty;
            Email = node.GetString("data", "Email") ?? string.Empty;
            Password = node.GetString("data", "Password") ?? string.Empty;
            UserPermission = Enum.TryParse<UserPermissionType>(node.GetString("data", "Permission"), true, out var p) ? p : UserPermissionType.Normal;
            Coins = node.GetDecimalNullable("data", "Coins") ?? 0m;
            AvatarUrl = node.GetString("data", "AvatarUrl");
            SessionId = node.GetString("data", "SessionId");
            CreatedAt = DateTime.Parse(node.GetString("data", "CreatedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            // Deserialize Dictionaries and Lists
            string ownedAssetsString = node.GetString("data", "OwnedAssets");
            if (!string.IsNullOrEmpty(ownedAssetsString))
            {
                OwnedAssets = ownedAssetsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split(new[] { ':' }, 2))
                    .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out _))
                    .ToDictionary(parts => int.Parse(parts[0]), parts => DateTime.Parse(parts[1], null, DateTimeStyles.RoundtripKind));
            }

            string mcaCodesString = node.GetString("data", "McaCodes");
            if (!string.IsNullOrEmpty(mcaCodesString))
            {
                McaCodes = mcaCodesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Split(new[] { ':' }, 2))
                    .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _))
                    .ToDictionary(parts => int.Parse(parts[0]), parts => parts[1]);
            }
            
            string publishedAssetsString = node.GetString("data", "PublishedAssetIds");
            if (!string.IsNullOrEmpty(publishedAssetsString))
            {
                PublishedAssetIds = publishedAssetsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(int.Parse)
                                           .ToList();
            }
        }

        /// <summary>
        /// Writes the object's properties to an IXmlNode.
        /// </summary>
        public void WriteToXml(IXmlNode node)
        {
            node.PushInt("data", "UserId", UserId);
            node.PushString("data", "Username", Username);
            node.PushString("data", "Email", Email);
            node.PushString("data", "Password", Password);
            node.PushString("data", "Permission", UserPermission.ToString());
            node.PushDecimal("data", "Coins", Coins);
            node.PushString("data", "AvatarUrl", AvatarUrl);
            node.PushString("data", "SessionId", SessionId);
            node.PushString("data", "CreatedAt", CreatedAt.ToString("o"));

            // Serialize Dictionaries and Lists
            string ownedAssetsString = string.Join(",", OwnedAssets.Select(kv => $"{kv.Key}:{kv.Value:o}"));
            node.PushString("data", "OwnedAssets", ownedAssetsString);

            string mcaCodesString = string.Join(",", McaCodes.Select(kv => $"{kv.Key}:{kv.Value}"));
            node.PushString("data", "McaCodes", mcaCodesString);

            string publishedAssetsString = string.Join(",", PublishedAssetIds);
            node.PushString("data", "PublishedAssetIds", publishedAssetsString);
        }
    }
} 