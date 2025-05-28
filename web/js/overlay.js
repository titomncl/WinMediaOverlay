class WinMediaOverlay {
    constructor() {
        this.overlay = document.getElementById('musicOverlay');
        this.albumCover = document.getElementById('albumCover');
        this.albumImage = document.getElementById('albumImage');
        this.trackTitle = document.getElementById('trackTitle');
        this.artistName = document.getElementById('artistName');
        this.albumName = document.getElementById('albumName');
        this.statusIndicator = document.getElementById('statusIndicator');
        
        this.lastMediaInfo = null;
        this.isVisible = false;
        this.updateInterval = null;
        
        this.init();
    }
    
    init() {
        // Start polling for media info
        this.startPolling();
        
        // Configuration options - you can customize these
        this.config = {
            updateInterval: 1000,         // Check for updates every second
            hideWhenNotPlaying: true,     // Hide overlay when music is paused/stopped
            autoHideDelay: 5000,         // Auto-hide after 5 seconds of no music
            position: 'bottom-left',     // bottom-left, bottom-right, top-left, top-right, center
            compact: false,              // Use compact mode
            showAlbum: true,             // Show album name
            theme: 'default'             // default, dark, light, accent-blue, accent-purple, accent-green
        };
        
        this.applyConfiguration();
    }
    
    applyConfiguration() {
        // Apply position
        this.overlay.classList.remove('position-top-left', 'position-top-right', 'position-bottom-right', 'position-center');
        if (this.config.position !== 'bottom-left') {
            this.overlay.classList.add(`position-${this.config.position}`);
        }
        
        // Apply compact mode
        if (this.config.compact) {
            this.overlay.classList.add('compact');
        }
        
        // Apply theme
        if (this.config.theme !== 'default') {
            this.overlay.classList.add(`theme-${this.config.theme}`);
        }
        
        // Hide album name if configured
        if (!this.config.showAlbum) {
            this.albumName.style.display = 'none';
        }
    }

    async startPolling() {
        const updateMediaInfo = async () => {
            try {
                console.log('Fetching media info...');
                const response = await fetch('media_info.json?t=' + Date.now());
                console.log('Response status:', response.status);
                if (response.ok) {
                    const mediaInfo = await response.json();
                    console.log('Media info received:', mediaInfo);
                    this.updateDisplay(mediaInfo);
                } else {
                    console.log('Response not OK:', response.statusText);
                    this.showNoMusic();
                }
            } catch (error) {
                console.log('Could not fetch media info:', error.message);
                this.showNoMusic();
            }
        };
        
        // Initial update
        await updateMediaInfo();
        
        // Set up interval
        this.updateInterval = setInterval(updateMediaInfo, this.config.updateInterval);
    }

    updateDisplay(mediaInfo) {
        console.log('Updating display with:', mediaInfo);
        
        if (!mediaInfo || this.isSameTrack(mediaInfo)) {
            console.log('Same track or no media info, skipping update');
            return;
        }
        
        this.lastMediaInfo = mediaInfo;
        
        if (mediaInfo.isPlaying) {
            console.log('Music is playing, showing info');
            this.showMusicInfo(mediaInfo);
        } else if (this.config.hideWhenNotPlaying) {
            console.log('Music paused and hideWhenNotPlaying is true, hiding overlay');
            this.hideOverlay();
        } else {
            console.log('Music paused, showing paused state');
            this.showPausedState(mediaInfo);
        }
    }
    
    isSameTrack(mediaInfo) {
        if (!this.lastMediaInfo) return false;
        
        return this.lastMediaInfo.artist === mediaInfo.artist &&
               this.lastMediaInfo.title === mediaInfo.title &&
               this.lastMediaInfo.isPlaying === mediaInfo.isPlaying;
    }

    showMusicInfo(mediaInfo) {
        // Update text content
        this.trackTitle.textContent = mediaInfo.title || 'Unknown Track';
        this.artistName.textContent = mediaInfo.artist || 'Unknown Artist';
        this.albumName.textContent = mediaInfo.album || '';

        // Update album cover
        if (mediaInfo.albumCoverPath) {
            // Use the relative path directly as it's already correct
            const imageUrl = mediaInfo.albumCoverPath + '?t=' + Date.now();
            console.log('Setting album cover src to:', imageUrl);
            this.albumImage.src = imageUrl;
            this.albumImage.style.display = 'block';
            this.albumCover.style.background = 'none';
            
            // Handle image load errors
            this.albumImage.onerror = () => {
                console.log('Failed to load album cover:', imageUrl);
                this.albumImage.style.display = 'none';
                this.albumCover.classList.remove('has-image');
                this.albumCover.style.background = 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)';
            };
            
            // Handle successful image load
            this.albumImage.onload = () => {
                console.log('Successfully loaded album cover:', imageUrl);
                this.albumCover.classList.add('has-image');
            };
        } else {
            console.log('No album cover path provided');
            this.albumImage.style.display = 'none';
            this.albumCover.classList.remove('has-image');
            this.albumCover.style.background = 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)';
        }
        
        // Update status indicator
        this.statusIndicator.className = 'status-indicator playing';
        
        // Show overlay with animation
        this.showOverlay();
    }
    
    showPausedState(mediaInfo) {
        this.trackTitle.textContent = mediaInfo.title || 'Unknown Track';
        this.artistName.textContent = mediaInfo.artist || 'Unknown Artist';
        this.albumName.textContent = mediaInfo.album || '';
        
        // Update status indicator for paused state
        this.statusIndicator.className = 'status-indicator paused';
        
        this.showOverlay();
    }

    showNoMusic() {
        this.trackTitle.textContent = 'No music playing';
        this.artistName.textContent = 'Apple Music Monitor';
        this.albumName.textContent = '';
        this.albumImage.style.display = 'none';
        this.albumCover.classList.remove('has-image');
        this.albumCover.style.background = 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)';
        this.statusIndicator.className = 'status-indicator';
        
        if (!this.config.hideWhenNotPlaying) {
            this.showOverlay();
        } else {
            this.hideOverlay();
        }
    }
    
    showOverlay() {
        if (!this.isVisible) {
            this.overlay.classList.add('visible');
            this.overlay.classList.remove('fade-out');
            this.isVisible = true;
        }
    }
    
    hideOverlay() {
        if (this.isVisible) {
            this.overlay.classList.add('fade-out');
            this.overlay.classList.remove('visible');
            this.isVisible = false;
        }
    }
    
    destroy() {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
        }
    }
}

// Initialize the overlay
const overlay = new WinMediaOverlay();

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    overlay.destroy();
});

// Console window fix - Force console allocation for better visibility when running as .exe
if (typeof console !== 'undefined') {
    console.log('Apple Music Overlay loaded successfully!');
    console.log('Configuration options available in overlay.config object');
}
