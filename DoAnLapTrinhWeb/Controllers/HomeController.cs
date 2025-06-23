using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace DoAnLapTrinhWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
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

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserId = currentUser?.Id;
            ViewBag.userId = currentUserId;

            return View();
        }

        public async Task<IActionResult> GetPosts()
        {
            var user = await _userManager.GetUserAsync(User);
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostLikes)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new {
                    p.Id,
                    p.Content,
                    p.ImagePath,
                    p.CreatedAt,
                    UserName = p.User.UserName,            
                    Avatar = p.User.Image,
                    likeCount = p.PostLikes.Count,
                    likedByCurrentUser = user != null && p.PostLikes.Any(l => l.UserId == user.Id)
                })
                .ToListAsync();
            return Json(posts);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Profile()
        {
            // Dữ liệu mẫu, bạn có thể thay bằng dữ liệu lấy từ database
            var model = new ProfileViewModel
            {
                FullName = "Nguyễn Văn A",
                Nickname = "NVA",
                PhoneNumber = "0123456789",
                BirthDate = new DateTime(2000, 1, 1)
            };

            return View(model);
        }

        [HttpPost]
        public IActionResult Profile(ProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                // TODO: Lưu dữ liệu vào database (nếu có)
                ViewBag.Message = "Cập nhật thông tin thành công!";
            }

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public async Task<IActionResult> AddPost(Post post, IFormFile? imageFile)
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
                var fileName = Path.GetFileName(imageFile.FileName);
                var uploadsFolder = Path.Combine("wwwroot", "uploads");
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

            return Redirect("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string content, int? parentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrWhiteSpace(content))
                return BadRequest(new { message = "Invalid request" });

            var comment = new Comment
            {
                Content = content,
                PostId = postId,
                ParentId = parentId,
                UserId = user.Id,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                comment = new
                {
                    content = comment.Content,
                    userName = user.UserName,
                    createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetComments(int postId)
        {
            var comments = await _context.Comments
                .Where(c => c.PostId == postId && c.ParentId == null)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    userName = c.User.UserName,
                    content = c.Content,
                    createdAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    replies = c.Replies.Select(r => new {
                        userName = r.User.UserName,
                        content = r.Content,
                        createdAt = r.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }).ToList()
                })
                .ToListAsync();

            return Json(comments);
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var like = await _context.PostLikes
                .FirstOrDefaultAsync(x => x.PostId == postId && x.UserId == user.Id);

            if (like != null)
            {
                _context.PostLikes.Remove(like);
                await _context.SaveChangesAsync();
                return Json(new { liked = false });
            }
            else
            {
                var newLike = new PostLike
                {
                    PostId = postId,
                    UserId = user.Id
                };
                _context.PostLikes.Add(newLike);
                await _context.SaveChangesAsync();
                return Json(new { liked = true });
            }
        }

    }
}
