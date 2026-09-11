using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;

namespace KiwiDX;

internal sealed class CommunityChatClient : IDisposable
{
    private readonly Uri origin;
    private readonly ClientWebSocket socket = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim sends = new(1);
    internal bool Connected => socket.State == WebSocketState.Open;
    internal event Action<JsonElement>? Message;
    internal event Action? Disconnected;
    internal CommunityChatClient(Uri origin) => this.origin = origin;

    internal async Task ConnectAsync(string token, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        linked.CancelAfter(TimeSpan.FromSeconds(20));
        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
        using var http = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "/session"));
        request.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
        request.Content = JsonContent.Create(new { token });
        using var response = await http.SendAsync(request, linked.Token);
        response.EnsureSuccessStatusCode();
        socket.Options.Cookies = cookies;
        socket.Options.SetRequestHeader("Origin", origin.GetLeftPart(UriPartial.Authority));
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        var endpoint = new UriBuilder(new Uri(origin, "/ws")) { Scheme = origin.Scheme == "https" ? "wss" : "ws" };
        await socket.ConnectAsync(endpoint.Uri, linked.Token);
    }

    internal async Task ListenAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (Connected && !lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token);
                    if (part.MessageType == WebSocketMessageType.Close) return;
                    if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > 1_048_576)
                        throw new InvalidDataException("Invalid chat frame.");
                    message.Write(buffer, 0, part.Count);
                } while (!part.EndOfMessage);
                using var json = JsonDocument.Parse(message.ToArray());
                Message?.Invoke(json.RootElement.Clone());
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException or JsonException or InvalidDataException) { }
        finally { socket.Abort(); Disconnected?.Invoke(); }
    }

    internal async Task SendAsync(string channel, string text)
    {
        if (channel is not ("#hamradio" or "#shortwave") || string.IsNullOrWhiteSpace(text) || text.Length > 1500)
            throw new ArgumentException("Invalid chat message.");
        await sends.WaitAsync(lifetime.Token);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { channel, text });
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
        }
        finally { sends.Release(); }
    }

    public void Dispose() { lifetime.Cancel(); socket.Abort(); socket.Dispose(); }
}
