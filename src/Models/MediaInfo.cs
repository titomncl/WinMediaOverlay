using System;

namespace WinMediaOverlay.Models
{
    public class MediaInfo
    {
        public bool IsPlaying { get; set; }
        public string? Artist { get; set; }
        public string? Title { get; set; }
        public string? Album { get; set; }
        public string? AlbumCoverPath { get; set; }
        public string? PlaybackStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
