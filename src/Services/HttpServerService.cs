using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace WinMediaOverlay.Services
{
    public class HttpServerService
    {
        private HttpListener? _httpListener;
        private readonly ConfigurationManager _config;

        public HttpServerService(ConfigurationManager config)
        {
            _config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"{_config.BaseUrl}/");
                _httpListener.Start();

                Console.WriteLine($"HTTP server started on {_config.BaseUrl}");

                while (!cancellationToken.IsCancellationRequested && _httpListener.IsListening)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        _ = Task.Run(() => HandleHttpRequest(context));
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected when shutting down
                        break;
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                    {
                        // Expected when shutting down
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP Server error: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
                _httpListener = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping HTTP server: {ex.Message}");
            }
        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;                // Remove query parameters for file path and URL decode
                var requestPath = request.Url?.AbsolutePath ?? "/";
                if (requestPath == "/") requestPath = "/index.html";

                // URL decode the path to handle special characters
                requestPath = Uri.UnescapeDataString(requestPath);

                // Remove leading slash
                var fileName = requestPath.TrimStart('/');

                string filePath;
                // Handle temp folder requests for album covers
                if (fileName.StartsWith("temp/"))
                {
                    // Convert temp/sessionId/album_covers/filename.jpg to actual temp path
                    var tempPath = fileName.Substring(5); // Remove "temp/"
                    var parts = tempPath.Split('/', StringSplitOptions.RemoveEmptyEntries);                    if (parts.Length >= 3 && parts[1] == "album_covers")
                    {
                        var sessionId = parts[0];
                        var imgFileName = string.Join('/', parts.Skip(2));
                        filePath = Path.Combine(Path.GetTempPath(), "WinMediaOverlay", sessionId, "album_covers", imgFileName);
                    }else
                    {
                        filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                    }
                }
                else if (fileName == "index.html" || fileName == "obs_overlay.html")
                {
                    // Serve main overlay file
                    filePath = Path.Combine(Environment.CurrentDirectory, "web", "index.html");
                }
                else if (fileName.StartsWith("css/") || fileName.StartsWith("js/"))
                {
                    // Serve CSS and JS files from web folder
                    filePath = Path.Combine(Environment.CurrentDirectory, "web", fileName);
                }                else
                {
                    filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                }

                // Set CORS headers
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                if (File.Exists(filePath))
                {
                    var fileBytes = await File.ReadAllBytesAsync(filePath);

                    // Set content type based on file extension
                    var contentType = Path.GetExtension(fileName).ToLower() switch
                    {
                        ".html" => "text/html",
                        ".json" => "application/json",
                        ".css" => "text/css",
                        ".js" => "application/javascript",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".txt" => "text/plain",
                        _ => "application/octet-stream"
                    };

                    response.ContentType = contentType;
                    response.StatusCode = 200;
                    response.ContentLength64 = fileBytes.Length;

                    await response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                }
                else
                {
                    response.StatusCode = 404;
                    var notFoundBytes = System.Text.Encoding.UTF8.GetBytes("File not found");
                    await response.OutputStream.WriteAsync(notFoundBytes, 0, notFoundBytes.Length);
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling HTTP request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }
    }
}
