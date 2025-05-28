using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinMediaOverlay.Services;
using Windows.Media.Control;

namespace WinMediaOverlay.Commands
{
    public class CommandLineHandler
    {
        private readonly FileManagerService _fileManager;

        public CommandLineHandler(FileManagerService fileManager)
        {
            _fileManager = fileManager;
        }

        public async Task<bool> HandleCommandsAsync(string[] args)
        {
            if (args.Length == 0) return false;

            var command = args[0].ToLower();

            switch (command)
            {
                case "--clean-covers":
                case "-c":
                    await _fileManager.CleanAlbumCovers();
                    return true;

                case "--kill-applemusic":
                case "-k":
                    await KillAppleMusicProcess();
                    return true;

                case "--status":
                case "-s":
                    await ShowAppleMusicStatus();
                    return true;

                case "--help":
                case "-h":
                    ShowHelp();
                    return true;                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Use --help to see available commands.");
                    return false;
            }
        }

        private async Task KillAppleMusicProcess()
        {
            try
            {
                var appleMusicProcesses = Process.GetProcessesByName("AppleMusic");

                if (appleMusicProcesses.Length == 0)
                {
                    Console.WriteLine("Apple Music is not running.");
                    return;
                }

                Console.WriteLine($"Found {appleMusicProcesses.Length} Apple Music process(es).");
                Console.Write("Do you want to stop Apple Music? (y/N): ");
                var response = Console.ReadLine();

                if (response?.ToLower() == "y" || response?.ToLower() == "yes")
                {
                    foreach (var process in appleMusicProcesses)
                    {
                        try
                        {
                            process.Kill();
                            await Task.Run(() => process.WaitForExit(5000)); // Wait up to 5 seconds
                            Console.WriteLine($"Stopped Apple Music process (PID: {process.Id})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to stop process {process.Id}: {ex.Message}");
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Process termination cancelled.");
                    foreach (var process in appleMusicProcesses)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error managing Apple Music process: {ex.Message}");
            }
        }

        private async Task ShowAppleMusicStatus()
        {
            try
            {
                var appleMusicProcesses = Process.GetProcessesByName("AppleMusic");

                Console.WriteLine("=== Apple Music Status ===");

                if (appleMusicProcesses.Length == 0)
                {
                    Console.WriteLine("Apple Music: Not running");
                }
                else
                {
                    Console.WriteLine($"Apple Music: Running ({appleMusicProcesses.Length} process(es))");
                    foreach (var process in appleMusicProcesses)
                    {
                        try
                        {
                            Console.WriteLine($"  PID: {process.Id}");
                            Console.WriteLine($"  Memory: {process.WorkingSet64 / 1024.0 / 1024.0:F1} MB");
                            Console.WriteLine($"  Started: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  PID: {process.Id} (Details unavailable: {ex.Message})");
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }                }

                // Check album covers (both temp and local)
                var totalFiles = 0;
                long totalSize = 0;

                // Check temp folders
                var baseTempPath = Path.Combine(Path.GetTempPath(), "WinMediaOverlay");
                if (Directory.Exists(baseTempPath))
                {
                    var tempDirs = Directory.GetDirectories(baseTempPath);
                    foreach (var tempDir in tempDirs)
                    {
                        var tempAlbumPath = Path.Combine(tempDir, "album_covers");
                        if (Directory.Exists(tempAlbumPath))
                        {
                            var files = Directory.GetFiles(tempAlbumPath, "*.jpg");
                            totalFiles += files.Length;
                            totalSize += files.Sum(file => new FileInfo(file).Length);
                        }
                    }
                }

                // Check local album_covers folder  
                var localAlbumCovers = "album_covers";
                if (Directory.Exists(localAlbumCovers))
                {
                    var files = Directory.GetFiles(localAlbumCovers, "*.jpg");
                    totalFiles += files.Length;
                    totalSize += files.Sum(file => new FileInfo(file).Length);
                }

                if (totalFiles > 0)
                {
                    Console.WriteLine($"Total album covers: {totalFiles} files ({totalSize / 1024.0 / 1024.0:F2} MB)");
                }
                else
                {
                    Console.WriteLine("Album covers: None");
                }

                // Check current media session
                try
                {
                    var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    var session = sessionManager.GetCurrentSession();

                    if (session != null)
                    {
                        var playbackInfo = session.GetPlaybackInfo();
                        var mediaProperties = await session.TryGetMediaPropertiesAsync();

                        Console.WriteLine($"Current session: {playbackInfo.PlaybackStatus}");
                        if (!string.IsNullOrEmpty(mediaProperties.Artist) && !string.IsNullOrEmpty(mediaProperties.Title))
                        {
                            Console.WriteLine($"Current track: {mediaProperties.Artist} - {mediaProperties.Title}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Current session: None");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Media session: Error ({ex.Message})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status: {ex.Message}");
            }
        }        private void ShowHelp()
        {
            Console.WriteLine("Windows Media Overlay - Command Line Options");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  WinMediaOverlay.exe [command]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  --clean-covers, -c    Clean up downloaded album covers");
            Console.WriteLine("  --kill-applemusic, -k Stop Apple Music process");
            Console.WriteLine("  --status, -s          Show media and monitor status");
            Console.WriteLine("  --help, -h            Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  WinMediaOverlay.exe --status");
            Console.WriteLine("  WinMediaOverlay.exe --clean-covers");
            Console.WriteLine("  WinMediaOverlay.exe --kill-applemusic");
            Console.WriteLine();
            Console.WriteLine("Without arguments, runs in monitor mode (headless).");
        }
    }
}
