using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace DoAnLapTrinhWeb.Helpers
{
    // Lớp đại diện cho payload tin nhắn nhận từ client
    public class ChatMessagePayload
    {
        public string ToId { get; set; }    // ID người nhận
        public string Message { get; set; } // Nội dung tin nhắn
    }

    public static class WebSocketHandler
    {
        // Danh sách người dùng đang kết nối (userId => WebSocket)
        private static readonly Dictionary<string, WebSocket> _userSockets = new();

        public static async Task Handle(HttpContext context, WebSocket socket)
        {
            // Lấy userId từ HttpContext
            var userId = context.Request.Query["userId"].ToString();
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("userId bị thiếu, không thể kết nối WebSocket.");
                return;
            }
            // Gắn socket vào user
            if (_userSockets.ContainsKey(userId))
                _userSockets[userId] = socket;
            else
                _userSockets.Add(userId, socket);

            var buffer = new byte[1024 * 4];
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    // Giải mã JSON thành đối tượng
                    var messageData = JsonSerializer.Deserialize<ChatMessagePayload>(messageJson);

                    if (messageData != null && !string.IsNullOrEmpty(messageData.ToId))
                    {
                        var formattedMessage = JsonSerializer.Serialize(new
                        {
                            fromId = userId,
                            message = messageData.Message
                        });

                        if (_userSockets.TryGetValue(messageData.ToId, out var receiverSocket)
                            && receiverSocket.State == WebSocketState.Open)
                        {
                            var sendBuffer = Encoding.UTF8.GetBytes(formattedMessage);
                            await receiverSocket.SendAsync(
                                new ArraySegment<byte>(sendBuffer),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi khi xử lý tin nhắn JSON: " + ex.Message);
                }

                // Tiếp tục lắng nghe tin nhắn
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            // Khi đóng kết nối
            _userSockets.Remove(userId);
            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }
}
