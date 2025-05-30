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
                Console.WriteLine("Requesting media session manager...");
                var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                Console.WriteLine("Session manager obtained successfully");
                
                sessionManager.SessionsChanged += OnSessionsChanged;

                // Set up initial session and its event handlers
                Console.WriteLine("Getting current session...");
                _currentSession = sessionManager.GetCurrentSession();
                Console.WriteLine($"Current session: {(_currentSession != null ? "Found" : "None")}");
                
                SetupSessionEventHandlers(_currentSession);
                await UpdateSession(_currentSession);
                
                Console.WriteLine("Media detection service initialized successfully");
            }            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Windows Media Control API unavailable: {ex.Message}");
                Console.WriteLine("Attempting alternative media detection methods...");
                
                // Try alternative detection method
                await InitializeAlternativeDetection();
            }
        }

        private async Task InitializeAlternativeDetection()
        {
            try
            {
                Console.WriteLine("Initializing alternative media detection (file-based)...");
                
                // Create a timer to periodically check for media info from alternative sources
                var timer = new System.Timers.Timer(2000); // Check every 2 seconds
                timer.Elapsed += async (s, e) => await CheckAlternativeMediaSources();
                timer.Start();
                
                // Set up initial state - no media detected
                await _mediaOutput.OutputMediaInfo(null);
                Console.WriteLine("Alternative media detection initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Alternative media detection failed: {ex.Message}");
                await _mediaOutput.OutputMediaInfo(null);
            }
        }        private async Task CheckAlternativeMediaSources()
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName("AppleMusic");
                if (processes.Any())
                {
                    // Try to get track info from Apple Music window title
                    var appleMusicProcess = processes.First();
                    string windowTitle = appleMusicProcess.MainWindowTitle ?? "";
                    
                    Console.WriteLine($"Apple Music window title: '{windowTitle}'");
                    
                    if (!string.IsNullOrEmpty(windowTitle) && windowTitle != "Apple Music")
                    {
                        // Parse window title to extract track information
                        var mediaInfo = ParseAppleMusicWindowTitle(windowTitle);
                        await _mediaOutput.OutputMediaInfo(mediaInfo);
                        MediaInfoChanged?.Invoke(mediaInfo);
                    }
                    else
                    {
                        // Apple Music is running but no specific track in title
                        var mediaInfo = new MediaInfo
                        {
                            Artist = "Apple Music",
                            Title = "Ready to play music",
                            Album = "No track currently playing",
                            IsPlaying = false,
                            PlaybackStatus = "Ready",
                            Timestamp = DateTime.Now,
                            AlbumCoverPath = ""
                        };
                        
                        await _mediaOutput.OutputMediaInfo(mediaInfo);
                        MediaInfoChanged?.Invoke(mediaInfo);
                    }
                }
                else
                {
                    await _mediaOutput.OutputMediaInfo(null);
                    MediaInfoChanged?.Invoke(null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Alternative media check error: {ex.Message}");
            }
        }

        private MediaInfo ParseAppleMusicWindowTitle(string windowTitle)
        {
            try
            {
                // Apple Music window title formats can be:
                // "Song Title - Artist - Apple Music"
                // "Artist - Song Title - Apple Music" 
                // "Song Title - Apple Music"
                // "Apple Music"
                
                Console.WriteLine($"Parsing window title: '{windowTitle}'");
                
                // Remove " - Apple Music" suffix if present
                string trackInfo = windowTitle.Replace(" - Apple Music", "").Trim();
                
                if (string.IsNullOrEmpty(trackInfo) || trackInfo == "Apple Music")
                {
                    return new MediaInfo
                    {
                        Artist = "Apple Music",
                        Title = "Music Player Active",
                        Album = "",
                        IsPlaying = true,
                        PlaybackStatus = "Running",
                        Timestamp = DateTime.Now,
                        AlbumCoverPath = ""
                    };
                }
                
                // Try to split by " - " to get artist and title
                var parts = trackInfo.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                
                string artist = "";
                string title = "";
                
                if (parts.Length >= 2)
                {
                    // Format: "Title - Artist" or "Artist - Title"
                    // We'll assume first part is title, second is artist for Apple Music
                    title = parts[0].Trim();
                    artist = parts[1].Trim();
                }
                else if (parts.Length == 1)
                {
                    // Only one part, treat as title
                    title = parts[0].Trim();
                    artist = "Unknown Artist";
                }
                else
                {
                    title = trackInfo;
                    artist = "Unknown Artist";
                }
                
                Console.WriteLine($"Parsed - Artist: '{artist}', Title: '{title}'");
                
                return new MediaInfo
                {
                    Artist = artist,
                    Title = title,
                    Album = "Apple Music", // We can't easily get album from window title
                    IsPlaying = true,
                    PlaybackStatus = "Playing",
                    Timestamp = DateTime.Now,
                    AlbumCoverPath = ""
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing window title: {ex.Message}");
                return new MediaInfo
                {
                    Artist = "Apple Music",
                    Title = "Error parsing track info",
                    Album = "",
                    IsPlaying = true,
                    PlaybackStatus = "Running",
                    Timestamp = DateTime.Now,
                    AlbumCoverPath = ""
                };
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
        }        private async Task UpdateSession(GlobalSystemMediaTransportControlsSession? session)
        {
            if (session == null)
            {
                Console.WriteLine("No media session available");
                // Output empty state
                await _mediaOutput.OutputMediaInfo(null);
                MediaInfoChanged?.Invoke(null);
                return;
            }

            try
            {
                Console.WriteLine("Processing media session...");
                // Check if the current session is playing something
                var playbackInfo = session.GetPlaybackInfo();
                Console.WriteLine($"Playback status: {playbackInfo.PlaybackStatus}");
                
                var mediaProperties = await session.TryGetMediaPropertiesAsync();
                Console.WriteLine($"Media properties - Title: '{mediaProperties.Title}', Artist: '{mediaProperties.Artist}', Album: '{mediaProperties.AlbumTitle}'");

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
