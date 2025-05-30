using System;
using System.Threading;
using System.Threading.Tasks;
using WinMediaOverlay.Commands;
using WinMediaOverlay.Services;

namespace WinMediaOverlay
{
    class Program
    {
        private static CancellationTokenSource? _cancellationTokenSource;
        private static ConfigurationManager? _config;
        private static HttpServerService? _httpServer;
        private static MediaDetectionService? _mediaDetection;
        private static bool _isShuttingDown = false;

        static async Task Main(string[] args)
        {
            try
            {                // Initialize configuration and services
                _config = new ConfigurationManager();
                _config.InitializeTempFolder();
                
                var fileManager = new FileManagerService(_config);
                var commandHandler = new CommandLineHandler(fileManager);
                
                // Handle command line arguments
                bool commandResult = await commandHandler.HandleCommandsAsync(args);
                if (args.Length > 0) // If arguments were provided
                {
                    if (commandResult)
                    {
                        Environment.Exit(0); // Valid command executed successfully
                    }
                    else
                    {
                        Environment.Exit(1); // Invalid command
                    }
                }

                // Initialize services for headless mode
                var mediaOutput = new MediaOutputService();
                _mediaDetection = new MediaDetectionService(_config, fileManager, mediaOutput);
                _httpServer = new HttpServerService(_config);

                // Set up media detection
                await _mediaDetection.InitializeAsync();

                Console.WriteLine("Apple Music Monitor running (headless mode)...");
                Console.WriteLine("Commands available:");
                Console.WriteLine("  --clean-covers    : Clean up downloaded album covers");
                Console.WriteLine("  --kill-applemusic : Stop Apple Music process");
                Console.WriteLine("  --status          : Show Apple Music status");
                Console.WriteLine("  --help            : Show this help");
                
                // Start HTTP server for serving files to avoid CORS issues
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Start the HTTP server and wait a moment for it to initialize
                var serverTask = _httpServer.StartAsync(_cancellationTokenSource.Token);
                await Task.Delay(1000); // Give server time to start
                
                if (_httpServer.IsRunning)
                {
                    Console.WriteLine($"HTTP server started at: {_config.BaseUrl}");
                    Console.WriteLine($"OBS Browser Source URL: {_config.BaseUrl}/index.html");
                }
                else
                {
                    Console.WriteLine("HTTP server failed to start. File output mode only.");
                    Console.WriteLine("To enable web interface, run as administrator or configure URL reservation.");
                }                Console.WriteLine("\nPress Ctrl+C to exit");
                
                // Handle Ctrl+C gracefully - only cancel the token, don't call cleanup directly
                Console.CancelKeyPress += (sender, e) => {
                    e.Cancel = true;
                    if (!_isShuttingDown)
                    {
                        Console.WriteLine("\nShutdown requested...");
                        _cancellationTokenSource?.Cancel();
                    }
                };

                // Register cleanup for unexpected exits only
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => {
                    if (!_isShuttingDown)
                    {
                        CleanupAndExit();
                    }
                };// Keep the application running
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when shutting down
                }
                  // Cleanup after cancellation
                CleanupAndExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (!_isShuttingDown)
                {
                    CleanupAndExit();
                }
                Environment.Exit(1);
            }
        }        private static void CleanupAndExit()
        {
            if (_isShuttingDown) return; // Prevent multiple cleanup calls
            _isShuttingDown = true;
            
            try
            {
                Console.WriteLine("Shutting down gracefully...");

                // Stop HTTP server
                _httpServer?.Stop();

                // Clean up temp folder
                _config?.CleanupTempFolder();

                Console.WriteLine("Goodbye!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning during cleanup: {ex.Message}");
            }
            finally
            {
                // Ensure we exit
                Environment.Exit(0);
            }
        }
    }
}
