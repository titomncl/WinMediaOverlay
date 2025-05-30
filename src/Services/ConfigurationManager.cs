using System;
using System.IO;
using System.Linq;

namespace WinMediaOverlay.Services
{
    public class ConfigurationManager
    {
        public string TempSessionFolder { get; private set; }
        public string AlbumCoversFolder { get; private set; }
        public string BaseUrl { get; private set; } = "http://localhost:8080";
        public int Port { get; private set; } = 8080;

        public ConfigurationManager()
        {
            // Initialize paths
            TempSessionFolder = Path.Combine(Path.GetTempPath(), "WinMediaOverlay", Guid.NewGuid().ToString("N")[..8]);
            AlbumCoversFolder = Path.Combine(TempSessionFolder, "album_covers");
        }

        public void InitializeTempFolder()
        {
            try
            {
                // Clean up any old temp folders (from previous runs)
                var baseTempPath = Path.Combine(Path.GetTempPath(), "WinMediaOverlay");
                if (Directory.Exists(baseTempPath))
                {
                    var oldFolders = Directory.GetDirectories(baseTempPath)
                        .Where(dir => Directory.GetCreationTime(dir) < DateTime.Now.AddHours(-24)) // Older than 24 hours
                        .ToArray();

                    foreach (var oldFolder in oldFolders)
                    {
                        try
                        {
                            Directory.Delete(oldFolder, true);
                            Console.WriteLine($"Cleaned up old temp folder: {Path.GetFileName(oldFolder)}");
                        }
                        catch
                        {
                            // Ignore errors for folders that might be in use
                        }
                    }
                }

                // Create our session temp folder
                Directory.CreateDirectory(AlbumCoversFolder);
                Console.WriteLine($"Temp folder created: {TempSessionFolder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize temp folder: {ex.Message}");
                // Fall back to current directory if temp fails
                AlbumCoversFolder = "album_covers";
            }
        }

        public void CleanupTempFolder()
        {
            try
            {
                if (Directory.Exists(TempSessionFolder))
                {
                    Directory.Delete(TempSessionFolder, true);
                    Console.WriteLine("Temp files cleaned up.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning during temp folder cleanup: {ex.Message}");
            }
        }
    }
}
