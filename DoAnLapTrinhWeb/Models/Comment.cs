using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DoAnLapTrinhWeb.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Quan hệ đến bài viết
        public int PostId { get; set; }
        public Post Post { get; set; }

        // Quan hệ đến người dùng
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public int? ParentId { get; set; }  // nullable: comment gốc thì null
        [ForeignKey("ParentId")]
        public Comment? Parent { get; set; }

        public ICollection<Comment>? Replies { get; set; } = new List<Comment>();
    }
}
