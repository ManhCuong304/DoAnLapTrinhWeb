using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;


        public ProfileController(UserManager<ApplicationUser>userManager, ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;

        }
        public async Task<IActionResult> Index()
        {
            var user = await _context.Users
                .Include(u => u.Followings)
                .Include(u => u.Followers)
                .FirstOrDefaultAsync(u => u.Id == _userManager.GetUserId(User));

            ViewBag.Following = user.Followings?.Count ?? 0;
            ViewBag.Follower = user.Followers?.Count ?? 0;

            // ✅ Thêm đoạn này vào đây:
            var currentUserId = _userManager.GetUserId(User);

            var suggestedUsers = await _context.Users
                .Where(u => u.Id != currentUserId &&
                            !_context.Follow.Any(f => f.FollowerId == currentUserId && f.FollowingId == u.Id))
                .Take(5)
                .ToListAsync();

            ViewBag.SuggestedUsers = suggestedUsers;

            return View(user);
        }


        public async Task<IActionResult> SearchProfile(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new List<object>());
            }

            var profiles = await _context.Users
                .Where(u => u.FullName.Contains(query)) 
                .Select(u => new
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Image = string.IsNullOrEmpty(u.Image) ? "/images/default-avatar.png" : u.Image
                })
                .Take(10) // giới hạn kết quả
                .ToListAsync();

            return Json(profiles);
        }
        
        public async Task<IActionResult>ProfileDetail(string Id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var profiledetail = await _context.Users
                .Include(u => u.Followers)
                .Include(u => u.Followings)
                .FirstOrDefaultAsync(p => p.Id == Id);

            if (profiledetail == null)
            {
                return NotFound();
            }
            ViewBag.Follower = profiledetail.Followers?.Count ?? 0;
            ViewBag.Following = profiledetail.Followings?.Count ?? 0;
            var isFollowing = await _context.Follow
                .AnyAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == Id);
            var isFollowedBack = await _context.Follow
                .AnyAsync(f => f.FollowerId == Id && f.FollowingId == currentUser.Id);

            ViewBag.IsFollowing = isFollowing;
            ViewBag.IsMutualFollow = isFollowing && isFollowedBack;
            return View(profiledetail);
        }

        [HttpPost]
        public async Task<IActionResult> Follow(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.Id == id)
            {
                return BadRequest();
            }

            var userToFollow = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (userToFollow == null)
            {
                return NotFound();
            }

            var existingFollow = await _context.Follow.FirstOrDefaultAsync(f => f.FollowerId == currentUser.Id && f.FollowingId == id);

            if (existingFollow != null)
            {
                _context.Follow.Remove(existingFollow);
            }
            else
            {
       
                var follow = new Follow
                {
                    FollowerId = currentUser.Id,
                    FollowingId = id,
                    FollowAt = DateTime.Now
                };
                _context.Follow.Add(follow);
            }

            await _context.SaveChangesAsync();
            return Json("Thành Công");
        }
        [HttpPost]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            user.FullName = model.FullName;
            user.UserName = model.Nickname;
            user.Description = model.Description;

            if (model.Avatar != null)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Avatar.CopyToAsync(fileStream);
                }

                user.Image = "/images/avatars/" + fileName;
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction("Index");
        }
        public async Task<IActionResult> MyFriends()
        {
            var currentUserId = _userManager.GetUserId(User);

            // Lấy danh sách người dùng mà current user đã follow
            var friends = await _context.Follow
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.Following)
                .ToListAsync();

            return View(friends);
        }

    }
}
