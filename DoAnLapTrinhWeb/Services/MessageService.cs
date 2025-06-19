using DoAnLapTrinhWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace DoAnLapTrinhWeb.Services
{
    public class MessageService
    {
        private readonly ApplicationDbContext _context;
        public MessageService(ApplicationDbContext context)
        {
            _context = context; 
        }

        public async Task SaveMessageAsync(string fromId, string toId, string content)
        {
            var msg = new Message
            {
                FromUserId = fromId,
                ToUserId = toId,
                Content = content,
                Timestamp = DateTime.Now
            };

            _context.Messages.Add(msg);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Message>> GetMessagesAsync(string user1, string user2)
        {
            return await _context.Messages
                .Where(m =>
                    (m.FromUserId == user1 && m.ToUserId == user2) ||
                    (m.FromUserId == user2 && m.ToUserId == user1))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }
    }
}
