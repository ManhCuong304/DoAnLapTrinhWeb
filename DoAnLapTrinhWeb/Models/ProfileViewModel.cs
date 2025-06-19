using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace DoAnLapTrinhWeb.Models
{
    public class ProfileViewModel
    {
        [Display(Name = "Họ và tên")]
        [Required]
        public string FullName { get; set; }

        [Display(Name = "Biệt danh")]
        public string Nickname { get; set; }

        [Display(Name = "Số điện thoại")]
        [Phone]
        public string PhoneNumber { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; }  // ✅ thêm dòng này

        // Hiển thị ảnh cũ
        public string Image { get; set; }

        // File ảnh mới upload
        [Display(Name = "Ảnh đại diện")]
        public IFormFile Avatar { get; set; }
    }
}
