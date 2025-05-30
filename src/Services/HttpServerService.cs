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
        public bool IsRunning { get; private set; }

        public HttpServerService(ConfigurationManager config)
        {
            _config = config;
        }        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"{_config.BaseUrl}/");
                Console.WriteLine($"Attempting to start HTTP server on {_config.BaseUrl}");
                _httpListener.Start();
                IsRunning = true;

                // Display both local and network access information
                Console.WriteLine($"HTTP server started successfully on {_config.BaseUrl}");
                if (_config.BaseUrl.Contains("*"))
                {
                    Console.WriteLine("Network access enabled:");
                    Console.WriteLine("  Local access: http://localhost:8080");
                    Console.WriteLine("  Network access: http://192.168.1.11:8080");
                    Console.WriteLine("  (Replace 192.168.1.11 with your actual IP address)");
                }
                else
                {
                    Console.WriteLine("Local access only (localhost binding)");
                }

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
            catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
            {
                Console.WriteLine($"HTTP Server error: Access denied when binding to {_config.BaseUrl}");
                Console.WriteLine("To enable network access, you need to:");
                Console.WriteLine("1. Run as Administrator, OR");
                Console.WriteLine("2. Run this command as Admin: netsh http add urlacl url=http://*:8080/ user=Everyone");
                Console.WriteLine();
                Console.WriteLine("Falling back to localhost-only mode...");
                
                // Fallback to localhost
                await StartLocalhostFallback(cancellationToken);
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
                IsRunning = false;
                _httpListener?.Stop();
                _httpListener?.Close();
                _httpListener = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping HTTP server: {ex.Message}");
            }        }

        private async Task HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                  // Remove query parameters for file path and URL decode
                var requestPath = request.Url?.AbsolutePath ?? "/";
                if (requestPath == "/") requestPath = "/index.html";

                Console.WriteLine($"HTTP Request: {requestPath}");

                // URL decode the path to handle special characters
                requestPath = Uri.UnescapeDataString(requestPath);

                // Remove leading slash
                var fileName = requestPath.TrimStart('/');
                
                Console.WriteLine($"Serving file: {fileName}");

                string filePath;
                // Handle temp folder requests for album covers
                if (fileName.StartsWith("temp/"))
                {
                    // Convert temp/sessionId/album_covers/filename.jpg to actual temp path
                    var tempPath = fileName.Substring(5); // Remove "temp/"
                    var parts = tempPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                      if (parts.Length >= 3 && parts[1] == "album_covers")
                    {
                        var sessionId = parts[0];
                        var imgFileName = string.Join('/', parts.Skip(2));
                        filePath = Path.Combine(Path.GetTempPath(), "WinMediaOverlay", sessionId, "album_covers", imgFileName);
                    }
                    else
                    {
                        filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                    }
                }
                else if (fileName == "index.html" || fileName == "obs_overlay.html")
                {
                    // Serve main overlay file
                    filePath = Path.Combine(Environment.CurrentDirectory, "web", "index.html");
                }
                else if (fileName == "media_info.json")
                {
                    // Serve the JSON API endpoint
                    filePath = Path.Combine(Environment.CurrentDirectory, "media_info.json");
                }                else if (fileName.StartsWith("css/") || fileName.StartsWith("js/"))
                {
                    // Serve CSS and JS files from web folder
                    filePath = Path.Combine(Environment.CurrentDirectory, "web", fileName);
                }
                else
                {
                    filePath = Path.Combine(Environment.CurrentDirectory, fileName);
                }

                Console.WriteLine($"Resolved file path: {filePath}");
                Console.WriteLine($"File exists: {File.Exists(filePath)}");

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

        private async Task StartLocalhostFallback(CancellationToken cancellationToken)
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://localhost:8080/");
                _httpListener.Start();
                IsRunning = true;

                Console.WriteLine("HTTP server started on http://localhost:8080 (localhost only)");
                Console.WriteLine("Note: Not accessible from other devices on the network");

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
                Console.WriteLine($"Fallback HTTP Server error: {ex.Message}");
            }
        }
    }
}
