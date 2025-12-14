using DoAnCoSo.Models;
using DoAnCoSo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace DoAnCoSo.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ProfileService _profileService;

        public ProfileController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration,
            ProfileService profileService)
        {
            _userManager = userManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
            _profileService = profileService;
        }

        // ✅ Trang hồ sơ chính
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);

            // Lấy thông tin user cùng follower và following
            var user = await _context.Users
                .Include(u => u.Followings)
                .Include(u => u.Followers)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);

            if (user == null)
                return RedirectToAction("Login", "Account");

            ViewBag.Following = user.Followings?.Count ?? 0;
            ViewBag.Follower = user.Followers?.Count ?? 0;

            // LẤY BÀI VIẾT CỦA USER HIỆN TẠI
            var userPosts = await _context.Posts
                .Where(p => p.UserId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.UserPosts = userPosts;

            // Gợi ý follow
            var followingIds = await _context.Follow
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            var suggestedUsers = await _context.Users
                .Where(u => u.Id != currentUserId && !followingIds.Contains(u.Id))
                .Take(5)
                .ToListAsync();

            ViewBag.SuggestedUsers = suggestedUsers;

            // Bạn bè chung
            var mutualFriends = await (from f1 in _context.Follow
                                       join f2 in _context.Follow
                                       on f1.FollowingId equals f2.FollowerId
                                       where f1.FollowerId == currentUserId && f2.FollowingId == currentUserId
                                       select f1.Following)
                                      .Distinct()
                                      .ToListAsync();

            ViewBag.MutualFriends = mutualFriends ?? new List<ApplicationUser>();

            return View(user);
        }

        // ✅ Chỉnh sửa hồ sơ
        [HttpPost]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            user.FullName = model.FullName;
            user.UserName = model.Nickname;
            user.Description = model.Description;
            user.Major = model.Major;
            user.BirthDate = model.BirthDate;

            // ✅ Upload ảnh đại diện
            if (model.Avatar != null)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath ?? "wwwroot", "images", "avatars");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Avatar.CopyToAsync(stream);
                }

                user.Image = "/images/avatars/" + fileName;
            }

            // ✅ Sinh mô tả tự động nếu chưa có
            user.Describe = string.IsNullOrWhiteSpace(model.Describe)
                ? await GenerateDescriptionAsync(user.Major, user.Description)
                : model.Describe;

            // ✅ Sinh vector embedding và lưu
            await _profileService.GenerateUserEmbeddingAsync(user);

            // ✅ Lưu thay đổi
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                Console.WriteLine("❌ Không lưu được user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            return RedirectToAction("Index");
        }

        // ✅ Gợi ý bạn bè (AI-based hoặc random fallback)
        [HttpGet]
        public async Task<IActionResult> GetSuggestedFriends()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new List<object>());

            // Gợi ý theo vector (AI)
            var similarIds = await _profileService.GetSimilarUsersAsync(currentUser.Id);
            var users = await _context.Users
                .Where(u => similarIds.Contains(u.Id))
                .Select(u => new
                {
                    id = u.Id,
                    fullName = u.FullName,
                    userName = u.UserName,
                    image = string.IsNullOrEmpty(u.Image) ? "/images/default-avatar.png" : u.Image
                })
                .ToListAsync();

            // Nếu chưa có vector thì fallback random
            if (!users.Any())
            {
                users = await _context.Users
                    .Where(u => u.Id != currentUser.Id)
                    .Take(5)
                    .Select(u => new
                    {
                        id = u.Id,
                        fullName = u.FullName,
                        userName = u.UserName,
                        image = string.IsNullOrEmpty(u.Image) ? "/images/default-avatar.png" : u.Image
                    })
                    .ToListAsync();
            }

            return Json(users);
        }

        // ✅ Sinh mô tả bằng AI
        private async Task<string> GenerateDescriptionAsync(string major, string description)
        {
            try
            {
                var apiKey = _configuration["OpenRouter:ApiKey"];
                var url = "https://openrouter.ai/api/v1/chat/completions";

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://localhost:5001");
                httpClient.DefaultRequestHeaders.Add("X-Title", "Profile Description");

                var requestBody = new
                {
                    model = "openai/gpt-3.5-turbo",
                    messages = new[]
                    {
                        new {
                            role = "user",
                            content = $"Viết khoảng 20 đặc điểm băng tiếng việt , thế mạnh giỏi về công việc, cách nhau bằng dấu phẩy. Dựa trên chuyên ngành {major} và mô tả: {description}. Sử dụng động từ, tính từ, không viết thành câu."
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    return doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "Không thể sinh mô tả.";
                }

                return "Không thể tạo mô tả tự động.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi sinh mô tả: " + ex.Message);
                return "Đã xảy ra lỗi khi tạo mô tả.";
            }
        }
        [HttpGet]
        [AllowAnonymous] // hoặc bỏ nếu muốn yêu cầu đăng nhập
        public async Task<IActionResult> SearchProfile(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new object[0]);

            var q = query.Trim().ToLower();

            var users = await _context.Users
                .Where(u => (u.UserName ?? "").ToLower().Contains(q)
                         || (u.FullName ?? "").ToLower().Contains(q))
                .Select(u => new {
                    id = u.Id,
                    fullName = u.FullName,
                    userName = u.UserName,
                    image = string.IsNullOrEmpty(u.Image) ? Url.Content("~/images/default-avatar.png") : u.Image
                })
                .Take(10)
                .ToListAsync();

            return Json(users);
        }

    }
}
