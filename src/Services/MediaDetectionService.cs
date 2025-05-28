using System;
using System.IO;
using System.Threading.Tasks;
using WinMediaOverlay.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WinMediaOverlay.Services
{
    public class MediaDetectionService
    {
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly ConfigurationManager _config;
        private readonly FileManagerService _fileManager;
        private readonly MediaOutputService _mediaOutput;

        public event Func<MediaInfo?, Task>? MediaInfoChanged;

        public MediaDetectionService(ConfigurationManager config, FileManagerService fileManager, MediaOutputService mediaOutput)
        {
            _config = config;
            _fileManager = fileManager;
            _mediaOutput = mediaOutput;
        }        public async Task InitializeAsync()
        {
            try
            {
                var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                sessionManager.SessionsChanged += OnSessionsChanged;

                // Set up initial session and its event handlers
                _currentSession = sessionManager.GetCurrentSession();
                SetupSessionEventHandlers(_currentSession);
                await UpdateSession(_currentSession);
                
                Console.WriteLine("Media detection service initialized successfully");
            }            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Media detection service could not be initialized: {ex.Message}");
                Console.WriteLine("The application will continue running without media detection capabilities.");
                
                // Continue operation without media detection - the HTTP server will still work
                // Set up a default "no media" state
                await _mediaOutput.OutputMediaInfo(null);
            }
        }

        private void SetupSessionEventHandlers(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session != null)
            {
                session.MediaPropertiesChanged += async (s, e) => await UpdateSession(session);
                session.PlaybackInfoChanged += async (s, e) => await UpdateSession(session);
            }
        }

        private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args)
        {
            var session = sender.GetCurrentSession();

            // Remove old event handlers if we had a previous session
            if (_currentSession != null && _currentSession != session)
            {
                // Note: Can't easily remove event handlers due to async lambda, but this is acceptable
                // as the old session will become inactive
            }

            _currentSession = session;
            SetupSessionEventHandlers(session);
            await UpdateSession(session);
        }

        private async Task UpdateSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null)
            {
                // Output empty state
                await _mediaOutput.OutputMediaInfo(null);
                MediaInfoChanged?.Invoke(null);
                return;
            }

            try
            {
                // Check if the current session is playing something
                var playbackInfo = session.GetPlaybackInfo();
                var mediaProperties = await session.TryGetMediaPropertiesAsync();

                // Parse artist and album information intelligently
                var (parsedArtist, parsedAlbum) = ParseArtistAndAlbum(mediaProperties);
                var title = string.IsNullOrWhiteSpace(mediaProperties.Title) ? "Unknown Track" : mediaProperties.Title;

                // Download album cover immediately if available (regardless of playback status)
                // This ensures covers are ready when the song starts playing
                string? albumCoverPath = null;
                string? albumCoverRelativePath = null;
                if (mediaProperties.Thumbnail != null &&
                    !string.IsNullOrWhiteSpace(parsedArtist) &&
                    !string.IsNullOrWhiteSpace(title))
                {
                    // Create expected filename first to check if it exists
                    var safeArtist = _fileManager.CleanFilename(parsedArtist);
                    var safeAlbum = _fileManager.CleanFilename(parsedAlbum);
                    if (safeArtist.Length > 50) safeArtist = safeArtist.Substring(0, 50);
                    if (safeAlbum.Length > 50) safeAlbum = safeAlbum.Substring(0, 50);
                    var expectedFileName = $"{safeArtist}_{safeAlbum}.jpg";
                    var expectedFilePath = Path.Combine(_config.AlbumCoversFolder, expectedFileName);

                    // Check if we already have this cover
                    if (File.Exists(expectedFilePath))
                    {
                        Console.WriteLine($"Album cover already exists: {expectedFileName}");
                        albumCoverRelativePath = $"temp/{Path.GetFileName(_config.TempSessionFolder)}/album_covers/{expectedFileName}";
                    }
                    else
                    {
                        // Add a small delay to ensure we get the current track's thumbnail data
                        await Task.Delay(500);

                        // Re-fetch media properties to ensure we have the latest thumbnail
                        var freshMediaProperties = await session.TryGetMediaPropertiesAsync();
                        if (freshMediaProperties.Thumbnail != null)
                        {
                            albumCoverPath = await DownloadAlbumCover(freshMediaProperties, parsedArtist, parsedAlbum);

                            if (albumCoverPath != null)
                            {
                                // Convert to relative path for HTTP server
                                var fileName = Path.GetFileName(albumCoverPath);
                                albumCoverRelativePath = $"temp/{Path.GetFileName(_config.TempSessionFolder)}/album_covers/{fileName}";
                                Console.WriteLine($"Album cover ready: {albumCoverRelativePath}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No thumbnail available for current track");
                        }
                    }
                }

                var mediaInfo = new MediaInfo
                {
                    IsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    Artist = parsedArtist,
                    Title = title,
                    Album = parsedAlbum,
                    AlbumCoverPath = albumCoverRelativePath, // Use relative path for HTTP
                    PlaybackStatus = playbackInfo.PlaybackStatus.ToString(),
                    Timestamp = DateTime.Now
                };

                await _mediaOutput.OutputMediaInfo(mediaInfo);
                MediaInfoChanged?.Invoke(mediaInfo);

                // Also output simple text file for basic OBS text sources
                if (mediaInfo.IsPlaying)
                {
                    var simpleOutput = $"{mediaInfo.Artist} - {mediaInfo.Title}";
                    await File.WriteAllTextAsync("nowplaying.txt", simpleOutput);
                    Console.WriteLine($"Now Playing: {simpleOutput}");
                }
                else
                {
                    await File.WriteAllTextAsync("nowplaying.txt", "No music playing");
                    Console.WriteLine("Music stopped/paused");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating session: {ex.Message}");
                await _mediaOutput.OutputMediaInfo(null);
            }
        }

        private async Task<string?> DownloadAlbumCover(GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties, string artist, string album)
        {
            try
            {
                if (mediaProperties.Thumbnail == null) return null;

                // Create a safe filename from artist and album using normalized names
                var safeArtist = _fileManager.CleanFilename(artist);
                var safeAlbum = _fileManager.CleanFilename(album);

                // Ensure filenames aren't too long (Windows has a 260 character path limit)
                if (safeArtist.Length > 50) safeArtist = safeArtist.Substring(0, 50);
                if (safeAlbum.Length > 50) safeAlbum = safeAlbum.Substring(0, 50);

                var fileName = $"{safeArtist}_{safeAlbum}.jpg";
                var filePath = Path.Combine(_config.AlbumCoversFolder, fileName);

                // Check if we already have this cover
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"Album cover already exists: {fileName}");
                    return Path.GetFullPath(filePath);
                }

                Console.WriteLine($"Downloading album cover: {fileName}");

                // Download the thumbnail
                using var stream = await mediaProperties.Thumbnail.OpenReadAsync();
                using var fileStream = File.Create(filePath);
                await stream.AsStreamForRead().CopyToAsync(fileStream);

                Console.WriteLine($"Album cover downloaded: {fileName}");
                return Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading album cover: {ex.Message}");
                return null;
            }
        }

        private (string artist, string album) ParseArtistAndAlbum(GlobalSystemMediaTransportControlsSessionMediaProperties mediaProperties)
        {
            var artist = mediaProperties.Artist ?? "";
            var album = mediaProperties.AlbumTitle ?? "";
            var title = mediaProperties.Title ?? "";

            // If album is empty but artist contains dashes, try to parse it
            if (string.IsNullOrWhiteSpace(album) && artist.Contains(" - "))
            {
                var parts = artist.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 3)
                {
                    // Format: "Artist - Album - Single/EP" or "Artist - Album - Additional Info"
                    var extractedArtist = parts[0].Trim();
                    var extractedAlbum = parts[1].Trim();

                    // Check if the last part indicates it's a single/EP
                    var lastPart = parts[parts.Length - 1].Trim().ToLower();
                    if (lastPart == "single" || lastPart == "ep" || lastPart.Contains("single"))
                    {
                        album = extractedAlbum;
                        artist = extractedArtist;
                    }
                }
                else if (parts.Length == 2)
                {
                    // Format: "Artist - Album" 
                    artist = parts[0].Trim();
                    album = parts[1].Trim();
                }
            }

            // If album is still empty, try to use the title as album (common for singles)
            if (string.IsNullOrWhiteSpace(album) && !string.IsNullOrWhiteSpace(title))
            {
                album = title;
            }

            // Final fallback
            if (string.IsNullOrWhiteSpace(album))
            {
                album = "Single";
            }

            if (string.IsNullOrWhiteSpace(artist))
            {
                artist = "Unknown Artist";
            }

            Console.WriteLine($"Parsed - Artist: '{artist}', Album: '{album}'");
            return (artist, album);
        }
    }
}
