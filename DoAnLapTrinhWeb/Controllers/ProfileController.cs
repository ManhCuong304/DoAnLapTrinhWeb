using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser>userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var user = await _context.Users
            .Include(u => u.Followings)
            .Include(u => u.Followers) 
            .FirstOrDefaultAsync(u => u.Id == _userManager.GetUserId(User));

            ViewBag.Following = user.Followings?.Count ?? 0;
            ViewBag.Follower = user.Followers?.Count ?? 0;
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


    }
}
