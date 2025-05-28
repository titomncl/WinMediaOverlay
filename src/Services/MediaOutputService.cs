using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WinMediaOverlay.Models;

namespace WinMediaOverlay.Services
{
    public class MediaOutputService
    {
        public async Task OutputMediaInfo(MediaInfo? mediaInfo)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(mediaInfo, jsonOptions);
                await File.WriteAllTextAsync("media_info.json", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing media info: {ex.Message}");
            }
        }
    }
}
