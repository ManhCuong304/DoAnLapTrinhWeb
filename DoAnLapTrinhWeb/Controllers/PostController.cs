using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DoAnLapTrinhWeb.Controllers
{
    [Authorize]
    public class PostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public PostController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }
        public async Task<IActionResult> GetFriends()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            var currentUserId = currentUser.Id;

            // Lấy danh sách người mình follow
            var followingIds = await _context.Follow
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            // Lấy danh sách người follow mình
            var followerIds = await _context.Follow
                .Where(f => f.FollowingId == currentUserId)
                .Select(f => f.FollowerId)
                .ToListAsync();

            // Tìm danh sách mutual friends (bạn bè)
            var friendIds = followingIds.Intersect(followerIds).ToList();

            // Lấy thông tin người dùng từ danh sách friendIds
            var friends = await _context.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();
            return Json(friends);
        }
        // Hiển thị trang đăng bài và danh sách bài viết
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Posts = posts;
            var currentUserId = _userManager.GetUserId(User);
            var suggestedUsers = await _context.Users
                .Where(u => u.Id != currentUserId &&
                            !_context.Follow.Any(f => f.FollowerId == currentUserId && f.FollowingId == u.Id))
                .Take(5)
                .ToListAsync();

            ViewBag.SuggestedUsers = suggestedUsers;

            return View(new Post());
            

        }
        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrWhiteSpace(content))
                return BadRequest();

            var comment = new Comment
            {
                Content = content,
                PostId = postId,
                UserId = user.Id,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index"); // hoặc trở lại trang hiện tại
        }
        // Xử lý khi người dùng đăng bài
        [HttpPost]
        public async Task<IActionResult> Index(Post post, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Posts = await _context.Posts
                    .Include(p => p.User)
                    .Include(p => p.Comments)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return View(post);
            }

            // Xử lý upload ảnh nếu có
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder); // Tạo thư mục nếu chưa có

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                post.ImagePath = "/uploads/" + uniqueFileName;
            }

            // Lưu thông tin người dùng
            post.UserId = _userManager.GetUserId(User);
            post.CreatedAt = DateTime.Now;

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
