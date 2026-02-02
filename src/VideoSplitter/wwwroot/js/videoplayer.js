// Video Player JavaScript Functions for VideoSplitter

// Improved streaming with chunked loading
let dotNetHelper = null;
const videoCache = new Map();

window.initializeVideoPlayer = (dotNetObjectRef) => {
    dotNetHelper = dotNetObjectRef;
    
    const video = document.getElementById('videoPlayer');
    const currentTimeDisplay = document.getElementById('currentTime');
    const loadingSpinner = document.getElementById('videoLoadingSpinner');
    
    if (video) {
        // Handle video source initialization
        initializeVideoSource(video);
        
        // Show loading spinner when video starts loading
        video.addEventListener('loadstart', () => {
            console.log('Video loading started');
            if (loadingSpinner) {
                loadingSpinner.style.display = 'block';
            }
        });

        // Show spinner during waiting/buffering
        video.addEventListener('waiting', () => {
            console.log('Video buffering...');
            if (loadingSpinner) {
                loadingSpinner.style.display = 'block';
            }
        });

        // Hide spinner when video can play
        video.addEventListener('canplay', () => {
            console.log('Video can start playing');
            if (loadingSpinner) {
                loadingSpinner.style.display = 'none';
            }
        });

        // Hide spinner when video starts playing
        video.addEventListener('playing', () => {
            console.log('Video is playing');
            if (loadingSpinner) {
                loadingSpinner.style.display = 'none';
            }
        });

        // Update current time display
        video.addEventListener('timeupdate', () => {
            if (currentTimeDisplay) {
                const currentTime = video.currentTime;
                const hours = Math.floor(currentTime / 3600);
                const minutes = Math.floor((currentTime % 3600) / 60);
                const seconds = Math.floor(currentTime % 60);
                
                if (hours > 0) {
                    currentTimeDisplay.textContent = 
                        `${hours}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
                } else {
                    currentTimeDisplay.textContent = 
                        `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
                }
            }
        });

        // Handle video load errors
        video.addEventListener('error', (e) => {
            console.error('Video load error:', e);
            console.error('Video error details:', {
                error: video.error,
                networkState: video.networkState,
                readyState: video.readyState,
                currentSrc: video.currentSrc
            });
            
            // Hide loading spinner on error
            if (loadingSpinner) {
                loadingSpinner.style.display = 'none';
            }
            
            const errorMsg = 'Unable to load video. The file may be corrupted or in an unsupported format.';
            
            // Create error message element
            const errorDiv = document.createElement('div');
            errorDiv.className = 'alert alert-danger m-3';
            errorDiv.innerHTML = `
                <strong>Video Error:</strong> ${errorMsg}
                <br><small>Supported formats: MP4, WebM, OGG</small>
                <br><small>Error code: ${video.error?.code || 'Unknown'}</small>
            `;
            
            // Replace video element with error message
            video.parentNode.replaceChild(errorDiv, video);
        });

        // Handle successful video load
        video.addEventListener('loadedmetadata', () => {
            console.log('Video metadata loaded successfully');
            console.log('Video duration:', video.duration, 'seconds');
            
            // Hide loading spinner when metadata is loaded
            if (loadingSpinner) {
                loadingSpinner.style.display = 'none';
            }
        });

        // Monitor buffering progress
        video.addEventListener('progress', () => {
            if (video.buffered.length > 0) {
                const bufferedEnd = video.buffered.end(video.buffered.length - 1);
                const duration = video.duration;
                if (duration > 0) {
                    const percentBuffered = (bufferedEnd / duration) * 100;
                    console.log(`Video buffered: ${percentBuffered.toFixed(1)}%`);
                }
            }
        });
    }
};

async function initializeVideoSource(video) {
    const sources = video.querySelectorAll('source');
    const loadingSpinner = document.getElementById('videoLoadingSpinner');
    
    for (const source of sources) {
        const src = source.src;
        
        // Check if this is our custom app:// URL
        if (src.startsWith('app://video/')) {
            try {
                const encodedPath = src.replace('app://video/', '');
                const filePath = atob(encodedPath);
                
                console.log('Loading video from path:', filePath);
                
                // Show loading spinner
                if (loadingSpinner) {
                    loadingSpinner.style.display = 'block';
                }
                
                // Check cache first
                if (videoCache.has(filePath)) {
                    console.log('Using cached video URL');
                    source.src = videoCache.get(filePath);
                    video.load();
                    continue;
                }
                
                // Create blob URL using optimized chunked loading
                const blobUrl = await createOptimizedVideoBlobUrl(filePath);
                if (blobUrl) {
                    source.src = blobUrl;
                    videoCache.set(filePath, blobUrl);
                    console.log('Video blob URL created successfully');
                    video.load();
                } else {
                    console.error('Failed to create blob URL for video');
                    if (loadingSpinner) {
                        loadingSpinner.style.display = 'none';
                    }
                }
            } catch (error) {
                console.error('Error processing video source:', error);
                if (loadingSpinner) {
                    loadingSpinner.style.display = 'none';
                }
            }
        }
    }
}

async function createOptimizedVideoBlobUrl(filePath) {
    if (!dotNetHelper) {
        console.error('DotNet helper not initialized');
        return null;
    }

    const loadingSpinner = document.getElementById('videoLoadingSpinner');
    const loadingText = loadingSpinner?.querySelector('p');

    try {
        // Show progress
        if (loadingText) {
            loadingText.textContent = 'Initializing video...';
        }

        // Get file size
        const fileSize = await dotNetHelper.invokeMethodAsync('GetFileSize', filePath);
        if (fileSize === 0) {
            console.error('File not found or empty:', filePath);
            return null;
        }
        
        console.log('Video file size:', formatBytes(fileSize));
        
        // Optimized chunk size: 5MB chunks for better streaming
        const chunkSize = 5 * 1024 * 1024;
        const chunks = [];
        let loadedBytes = 0;
        
        // Load video in chunks with progress feedback
        for (let offset = 0; offset < fileSize; offset += chunkSize) {
            const remainingSize = Math.min(chunkSize, fileSize - offset);
            
            // Update progress
            const progressPercent = ((offset / fileSize) * 100).toFixed(1);
            if (loadingText) {
                loadingText.textContent = `Loading video... ${progressPercent}%`;
            }
            
            const chunk = await dotNetHelper.invokeMethodAsync('ReadFileChunk', filePath, offset, remainingSize);
            
            if (chunk && chunk.length > 0) {
                chunks.push(new Uint8Array(chunk));
                loadedBytes += chunk.length;
            } else {
                console.error('Failed to read chunk at offset:', offset);
                return null;
            }
            
            // Small delay to prevent UI freezing on large files
            if (chunks.length % 10 === 0) {
                await new Promise(resolve => setTimeout(resolve, 1));
            }
        }
        
        if (loadingText) {
            loadingText.textContent = 'Creating video stream...';
        }
        
        // Create blob from all chunks
        const blob = new Blob(chunks, { type: getVideoMimeType(filePath) });
        const blobUrl = URL.createObjectURL(blob);
        
        console.log(`Video blob created: ${formatBytes(loadedBytes)} loaded`);
        return blobUrl;
    } catch (error) {
        console.error('Error creating video blob URL:', error);
        return null;
    }
}

function getVideoMimeType(filePath) {
    const extension = filePath.split('.').pop().toLowerCase();
    
    switch (extension) {
        case 'mp4':
            return 'video/mp4';
        case 'webm':
            return 'video/webm';
        case 'ogg':
            return 'video/ogg';
        case 'avi':
            return 'video/x-msvideo';
        case 'mov':
            return 'video/quicktime';
        case 'wmv':
            return 'video/x-ms-wmv';
        case 'flv':
            return 'video/x-flv';
        case 'mkv':
            return 'video/x-matroska';
        default:
            return 'video/mp4';
    }
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

// Clean up blob URLs when page unloads
window.addEventListener('beforeunload', () => {
    for (const blobUrl of videoCache.values()) {
        if (blobUrl.startsWith('blob:')) {
            URL.revokeObjectURL(blobUrl);
        }
    }
    videoCache.clear();
});

window.seekVideoToTime = (seconds) => {
    const video = document.getElementById('videoPlayer');
    if (video) {
        video.currentTime = seconds;
        
        // Play the video if it's not already playing
        if (video.paused) {
            video.play().catch(e => {
                console.warn('Auto-play prevented by browser:', e);
            });
        }
        
        // Scroll video into view if not visible
        video.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};

window.toggleVideoFullscreen = () => {
    const video = document.getElementById('videoPlayer');
    if (video) {
        if (document.fullscreenElement) {
            document.exitFullscreen().catch(e => {
                console.error('Error exiting fullscreen:', e);
            });
        } else {
            video.requestFullscreen().catch(e => {
                console.error('Error entering fullscreen:', e);
                alert('Fullscreen not supported or permission denied.');
            });
        }
    }
};

window.setVideoPlaybackRate = (rate) => {
    const video = document.getElementById('videoPlayer');
    if (video) {
        video.playbackRate = rate;
    }
};

window.setVideoVolume = (volume) => {
    const video = document.getElementById('videoPlayer');
    if (video) {
        video.volume = Math.max(0, Math.min(1, volume));
    }
};

window.getCurrentVideoTime = () => {
    const video = document.getElementById('videoPlayer');
    if (video) {
        return video.currentTime;
    }
    return 0;
};

window.downloadFile = (fileName, base64Data) => {
    try {
        // Convert base64 to byte array
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        
        // Create blob and download link
        const blob = new Blob([byteArray], { type: 'video/mp4' });
        const url = URL.createObjectURL(blob);
        
        // Create temporary link and trigger download
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        
        // Cleanup
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        
        console.log('File download initiated:', fileName);
    } catch (error) {
        console.error('Error downloading file:', error);
        alert('Failed to download file. Please try again.');
    }
};

// Clean up on Blazor navigation
window.addEventListener('blazor:navigated', () => {
    for (const blobUrl of videoCache.values()) {
        if (blobUrl.startsWith('blob:')) {
            URL.revokeObjectURL(blobUrl);
        }
    }
    videoCache.clear();
    dotNetHelper = null;
});