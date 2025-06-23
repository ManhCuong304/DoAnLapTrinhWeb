using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class Post
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nội dung không được bỏ trống")]
        public string Content { get; set; }

        public string? ImagePath { get; set; } // Đường dẫn ảnh nếu có

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? LikeType { get; set; } // Ví dụ: "like", "love", "haha", "sad", "angry"

        // Khóa ngoại đến ApplicationUser
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // Danh sách comment
        public List<Comment>? Comments { get; set; }
    }
}
