using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpenseSplitterAPI.Services
{
    public class WebSocketManager
    {
        private readonly List<WebSocket> _sockets = new List<WebSocket>();

        public async Task HandleWebSocketAsync(WebSocket webSocket)
        {
            _sockets.Add(webSocket);
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.CloseStatus.HasValue)
                    {
                        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                        _sockets.Remove(webSocket);
                        return;
                    }

                    // ✅ Convert received bytes to string
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // ✅ Handle invitation messages
                    if (message.StartsWith("new_invitation:"))
                    {
                        var userId = message.Split(":")[1];
                        await BroadcastAsync($"new_invitation:{userId}"); // ✅ Notify recipient
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                _sockets.Remove(webSocket);
            }
        }

        // ✅ Broadcast message to all connected clients
        public async Task BroadcastAsync(string message)
        {
            if (_sockets.Count == 0) return;

            List<Task> tasks = new List<Task>();
            foreach (var socket in _sockets)
            {
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    var arraySegment = new ArraySegment<byte>(buffer);
                    tasks.Add(socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);
        }
    }
}
