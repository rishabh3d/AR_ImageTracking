using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class CDNARVideoController : MonoBehaviour
{
    [Tooltip("Optional: URL of the CDN-hosted video (e.g., .mp4 or .webm). Can also be assigned directly in the VideoPlayer component.")]
    public string cdnVideoUrl;

    [Tooltip("If true, the video resumes from exactly where it got paused. If false, it simply restarts from 0.0s every time the image is scanned.")]
    public bool resumeVideoOnRetrack = true;

    [Tooltip("If true, the video will continuously loop. If false, it stops and waits for a re-track.")]
    public bool loopVideo = true;

    [Tooltip("Optional: A GameObject (like a loading spinner or UI text) to display while the video is buffering.")]
    public GameObject loadingIndicator;

    [Tooltip("Optional: A UI Panel or GameObject (buttons, descriptions) that should appear only when the video is visible.")]
    public GameObject overlayUI;

    [Header("Performance Settings")]
    [Tooltip("If true, the video shows immediately when tracked. This is faster but might cause a momentary white flash.")]
    public bool fastLoadMode = false;

    [Tooltip("How much video progress to wait for before showing. Higher = safer from white flash, Lower = faster appearance.")]
    public float antiFlickerThreshold = 2.0f;

    private VideoPlayer videoPlayer;
    private bool hasFinished = false;

    private Renderer videoRenderer;
    private double lastRecordedTimeForCheck = -1;
    private int movingFrames = 0;

    private void Awake()
    {
        VideoPlayer originalVP = GetComponent<VideoPlayer>();
        videoRenderer = GetComponent<Renderer>();
        if (videoRenderer == null)
        {
            videoRenderer = GetComponentInChildren<Renderer>();
        }
        
        // --- PERSISTENT PLAYER TRICK ---
        // By detaching the video player onto a permanent object, we keep the WebGL buffer fully alive
        // even when the AR image tracker disables this GameObject.
        GameObject persistentObj = new GameObject("PersistentVP_" + gameObject.name);
        DontDestroyOnLoad(persistentObj);
        
        videoPlayer = persistentObj.AddComponent<VideoPlayer>();
        
        // Clone essential properties
        if (!string.IsNullOrEmpty(cdnVideoUrl))
            videoPlayer.url = cdnVideoUrl;
        else
            videoPlayer.url = originalVP.url;
            
        videoPlayer.source = originalVP.source;
        videoPlayer.clip = originalVP.clip;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loopVideo;
        videoPlayer.renderMode = originalVP.renderMode;
        
        videoPlayer.audioOutputMode = originalVP.audioOutputMode;
        if (originalVP.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            videoPlayer.EnableAudioTrack(0, originalVP.IsAudioTrackEnabled(0));
            videoPlayer.SetTargetAudioSource(0, originalVP.GetTargetAudioSource(0));
        }

        if (originalVP.renderMode == VideoRenderMode.MaterialOverride)
        {
            // Re-point the new video player to our AR plane renderer
            videoPlayer.targetMaterialRenderer = originalVP.targetMaterialRenderer != null ? originalVP.targetMaterialRenderer : videoRenderer;
            videoPlayer.targetMaterialProperty = originalVP.targetMaterialProperty;
        }
        else if (originalVP.renderMode == VideoRenderMode.RenderTexture)
        {
            videoPlayer.targetTexture = originalVP.targetTexture;
        }
        
        // Shut down the original one so it doesn't conflict
        originalVP.playOnAwake = false;
        originalVP.enabled = false;
        
        videoPlayer.loopPointReached += OnVideoEndReached;
        videoPlayer.prepareCompleted += OnPrepareCompleted;
        videoPlayer.errorReceived += OnVideoError; // Very important for WebGL debugging

        // Auto-prepare in the background IMMEDIATELY when the scene loads!
        videoPlayer.Prepare();
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
        // If Fast Load is on, we don't wait for frame counting
        if (fastLoadMode)
        {
            if (videoPlayer.isPrepared && !videoRenderer.enabled)
            {
                videoRenderer.enabled = true;
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
                if (overlayUI != null) overlayUI.SetActive(true);
            }
            return;
        }

        // Prevent white plane flash by hiding the renderer until the video starts pushing frames
        if (videoRenderer != null && !videoRenderer.enabled)
        {
            if (videoPlayer.isPrepared && videoPlayer.isPlaying)
            {
                if (lastRecordedTimeForCheck < 0)
                {
                    lastRecordedTimeForCheck = videoPlayer.time;
                }
                else
                {
                    // Check if time actually changed from the last frame. 
                    if (System.Math.Abs(videoPlayer.time - lastRecordedTimeForCheck) > 0.001)
                    {
                        movingFrames++;
                        lastRecordedTimeForCheck = videoPlayer.time;
                    }
                    
                    // Use the custom threshold from the inspector
                    if (movingFrames >= antiFlickerThreshold)
                    {
                        videoRenderer.enabled = true;
                        if (loadingIndicator != null) loadingIndicator.SetActive(false);
                        if (overlayUI != null) overlayUI.SetActive(true);
                    }
                }
            }
            else
            {
                movingFrames = 0;
                lastRecordedTimeForCheck = -1;
                if (loadingIndicator != null) loadingIndicator.SetActive(true);
            }
        }
    }

    private void OnEnable()
    {
        if (videoPlayer == null) return; // Happens momentarily before Awake completes

        // Hide renderer instantly upon retrack to hide the white gap (only in stable mode)
        if (videoRenderer != null)
        {
            videoRenderer.enabled = fastLoadMode; // If fast load is on, leave it enabled
        }
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true); // Show loading spinner
        }

        if (overlayUI != null)
        {
            overlayUI.SetActive(false); // Hide overlay until video ready
        }

        movingFrames = 0;
        lastRecordedTimeForCheck = -1;

        if (hasFinished || !resumeVideoOnRetrack)
        {
            hasFinished = false;
            videoPlayer.time = 0;
        }

        // Because the VideoPlayer is persistent in the background, its internal memory remembers exactly where it paused!
        // Calling Play() resumes INSTANTLY with ZERO network buffering required after the first time.
        videoPlayer.Play();
    }

    private void OnPrepareCompleted(VideoPlayer vp)
    {
        // If it finally finished background preparation and the AR image is currently being tracked, play!
        if (gameObject.activeInHierarchy)
        {
            videoPlayer.Play();
        }
    }

    private void OnDisable()
    {
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        if (overlayUI != null)
        {
            overlayUI.SetActive(false);
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
    }

    private void OnVideoEndReached(VideoPlayer vp)
    {
        if (loopVideo) return; // Allow native looping to continue

        // 4. When the video reaches the end -> Reset properly.
        hasFinished = true;
        vp.Pause();
        vp.time = 0;
    }
}
