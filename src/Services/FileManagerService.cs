using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WinMediaOverlay.Services
{
    public class FileManagerService
    {
        private readonly ConfigurationManager _config;

        public FileManagerService(ConfigurationManager config)
        {
            _config = config;
        }        public Task CleanAlbumCovers()
        {
            try
            {
                // Check both temp folder and local folder for backward compatibility
                var foldersToCheck = new List<string>();                // Add temp folders
                var baseTempPath = Path.Combine(Path.GetTempPath(), "WinMediaOverlay");
                if (Directory.Exists(baseTempPath))
                {
                    foldersToCheck.AddRange(Directory.GetDirectories(baseTempPath));
                }

                // Add local album_covers if it exists
                var localAlbumCovers = "album_covers";
                if (Directory.Exists(localAlbumCovers))
                {
                    foldersToCheck.Add(localAlbumCovers);
                }                if (foldersToCheck.Count == 0)
                {
                    Console.WriteLine("No album covers folders found.");
                    return Task.CompletedTask;
                }

                // Count all album cover files
                var allFiles = new List<string>();
                long totalSize = 0;

                foreach (var folder in foldersToCheck)
                {
                    var albumCoverPath = Path.Combine(folder, "album_covers");
                    if (Directory.Exists(albumCoverPath))
                    {
                        var files = Directory.GetFiles(albumCoverPath, "*.jpg");
                        allFiles.AddRange(files);
                        totalSize += files.Sum(file => new FileInfo(file).Length);
                    }
                    else if (Path.GetFileName(folder) == "album_covers")
                    {
                        var files = Directory.GetFiles(folder, "*.jpg");
                        allFiles.AddRange(files);
                        totalSize += files.Sum(file => new FileInfo(file).Length);
                    }
                }

                Console.WriteLine($"Found {allFiles.Count} album covers ({totalSize / 1024.0 / 1024.0:F2} MB)");                if (allFiles.Count == 0)
                {
                    Console.WriteLine("No album covers to clean.");
                    return Task.CompletedTask;
                }

                Console.Write("Do you want to delete all album covers? (y/N): ");
                var response = Console.ReadLine();

                if (response?.ToLower() == "y" || response?.ToLower() == "yes")
                {
                    var deletedCount = 0;
                    foreach (var file in allFiles)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not delete {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Deleted {deletedCount} album covers.");

                    // Clean up empty temp directories
                    if (Directory.Exists(baseTempPath))
                    {
                        try
                        {
                            var tempDirs = Directory.GetDirectories(baseTempPath);
                            foreach (var tempDir in tempDirs)
                            {
                                if (!Directory.EnumerateFileSystemEntries(tempDir).Any())
                                {
                                    Directory.Delete(tempDir, true);
                                }
                            }

                            // Remove base temp directory if empty
                            if (!Directory.EnumerateFileSystemEntries(baseTempPath).Any())
                            {
                                Directory.Delete(baseTempPath);
                                Console.WriteLine("Cleaned up empty temp directories.");
                            }
                        }
                        catch
                        {
                            // Ignore errors when cleaning temp directories
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Cleanup cancelled.");
                }
            }            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning album covers: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        public string CleanFilename(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "Unknown";

            // First, normalize Unicode characters (decompose accented characters)
            var normalized = filename.Normalize(System.Text.NormalizationForm.FormD);

            // Remove diacritics (accents) using regex
            var withoutDiacritics = System.Text.RegularExpressions.Regex.Replace(
                normalized, @"\p{Mn}", "");

            // Replace common problematic characters with safe alternatives
            var cleaned = withoutDiacritics
                .Replace("—", "-")      // Em dash
                .Replace("–", "-")      // En dash
                .Replace("'", "'")      // Smart apostrophe  
                .Replace("'", "'")      // Smart apostrophe
                .Replace("\"", "\"")    // Smart quote
                .Replace("\"", "\"")    // Smart quote
                .Replace(":", "_")      // Colon
                .Replace("?", "")       // Question mark
                .Replace("*", "")       // Asterisk
                .Replace("|", "_")      // Pipe
                .Replace("<", "_")      // Less than
                .Replace(">", "_")      // Greater than
                .Replace("/", "_")      // Forward slash
                .Replace("\\", "_")     // Backslash
                .Replace("&", "and")    // Ampersand
                .Replace("#", "")       // Hash
                .Replace("%", "")       // Percent
                .Replace("@", "at")     // At symbol
                .Replace("!", "")       // Exclamation
                .Replace("(", "_")      // Parentheses
                .Replace(")", "_")
                .Replace("[", "_")      // Brackets
                .Replace("]", "_")
                .Replace("{", "_")      // Braces
                .Replace("}", "_");

            // Use regex to remove any remaining non-ASCII and special characters
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^\w\s\-_.]", "");

            // Replace multiple spaces/underscores with single underscore
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[\s_]+", "_");

            // Remove any remaining invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                cleaned = cleaned.Replace(c, '_');
            }

            // Trim whitespace and dots (problematic at end of filenames)
            cleaned = cleaned.Trim().Trim('.');

            // Ensure it's not empty
            return string.IsNullOrEmpty(cleaned) ? "Unknown" : cleaned;
        }
    }
}
