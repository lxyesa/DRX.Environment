using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web.KaxServer.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        
        public int ItemId { get; set; }

        [Required]
        public string ReviewerName { get; set; } = "Anonymous";

        public UserPermissionType ReviewerPermission { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        public string Comment { get; set; } = string.Empty;

        public DateTime Date { get; set; }
        
        public List<string> LikedByUsers { get; set; } = new();

        public int LikesCount => LikedByUsers.Count;
    }
} 