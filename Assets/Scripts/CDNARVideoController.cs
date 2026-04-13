using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class CDNARVideoController : MonoBehaviour
{
    [Tooltip("Optional: URL of the CDN-hosted video (e.g., .mp4 or .webm). Can also be assigned directly in the VideoPlayer component.")]
    public string cdnVideoUrl;

    [Tooltip("If true, the video resumes from exactly where it got paused. If false, it simply restarts from 0.0s every time the image is scanned.")]
    public bool resumeVideoOnRetrack = true;

    private VideoPlayer videoPlayer;
    private bool hasFinished = false;

    private double savedTime = 0;
    private bool isResuming = false;
    
    private Renderer videoRenderer;
    private int framesSincePlay = 0;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoRenderer = GetComponent<Renderer>();
        if (videoRenderer == null)
        {
            videoRenderer = GetComponentInChildren<Renderer>();
        }
        
        // Ensure streaming config is optimized for CDN use.
        videoPlayer.source = VideoSource.Url;
        if (!string.IsNullOrEmpty(cdnVideoUrl))
        {
            videoPlayer.url = cdnVideoUrl;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        
        videoPlayer.loopPointReached += OnVideoEndReached;
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.errorReceived += OnVideoError; // Very important for WebGL debugging
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEndReached;
            videoPlayer.prepareCompleted -= OnPrepareCompleted;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError($"[CDNARVideoController] WebGL Video Error: {message}. Please check if the CDN allows CORS and if the video format is supported by the browser.");
    }

    private void Update()
    {
        // Prevent white plane flash by hiding the renderer until the video starts pushing frames
        if (videoRenderer != null && !videoRenderer.enabled)
        {
            if (videoPlayer.isPrepared && videoPlayer.isPlaying)
            {
                framesSincePlay++;
                // 5 frames delay guarantees the WebGL video texture is flushed to the GPU
                if (framesSincePlay > 5)
                {
                    videoRenderer.enabled = true;
                }
            }
            else
            {
                framesSincePlay = 0;
            }
        }

        // VITAL: Native VideoPlayers can lose their 'time' memory immediately upon being disabled.
        // Reading videoPlayer.time in OnDisable is often too late (reads as 0). 
        // We continuously cache it safely here while it is playing.
        if (videoPlayer != null && videoPlayer.isPlaying && !hasFinished)
        {
            savedTime = videoPlayer.time;
        }
    }

    private void OnEnable()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();

        // Hide renderer instantly upon retrack to hide the white gap
        if (videoRenderer != null)
        {
            videoRenderer.enabled = false;
        }
        framesSincePlay = 0;

        // If the user disabled 'resume', or the video was finished previously, reset time to 0
        if (hasFinished || !resumeVideoOnRetrack)
        {
            savedTime = 0;
            hasFinished = false;
        }

        if (!videoPlayer.isPrepared)
        {
            // Unity loses the video state when disabled. Prepare it first!
            isResuming = true;
            videoPlayer.Prepare();
        }
        else
        {
            // Set playback active FIRST, then forcibly restore time (safest for WebGL)
            videoPlayer.Play();
            videoPlayer.time = savedTime;
        }
    }

    private void OnPrepareCompleted(VideoPlayer vp)
    {
        if (isResuming)
        {
            videoPlayer.Play();
            videoPlayer.time = savedTime;
            isResuming = false;
        }
    }

    private void OnDisable()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
    }

    private void OnVideoEndReached(VideoPlayer vp)
    {
        // 4. When the video reaches the end -> Reset properly.
        hasFinished = true;
        savedTime = 0; // Ensures it starts at 0 next time
        vp.Pause();
        vp.time = 0;
    }
}
