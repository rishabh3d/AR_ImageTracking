using UnityEngine;
using UnityEngine.Video;
using System.Runtime.InteropServices;

[RequireComponent(typeof(VideoPlayer))]
public class CDNARVideoController : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int GetWebGLSoundUnlockSerial();
    [DllImport("__Internal")] private static extern bool RequiresWebGLSoundUnlock();
    [DllImport("__Internal")] private static extern void SetWebGLCurrentTargetKey(string targetKey);
#endif

    [Tooltip("Optional: URL of the CDN-hosted video (e.g., .mp4 or .webm). Can also be assigned directly in the VideoPlayer component.")]
    public string cdnVideoUrl;

    [Tooltip("Optional override for the sound prompt key used on WebGL/iOS. Leave empty to derive it from the parent target object.")]
    public string webGLSoundTargetKey;

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
    private AudioSource targetAudioSource;
    private Coroutine pendingAudioRestoreCoroutine;
    private Coroutine pendingVisibilityRecoveryCoroutine;
    private VideoRenderMode configuredRenderMode;
    private Renderer configuredTargetRenderer;
    private string configuredTargetMaterialProperty;
    private RenderTexture configuredTargetTexture;
    private bool pendingSeekAfterPrepare;
    private double pendingSeekTime;
    private int visibilityRecoveryAttempts;

    private const float VisibilityRecoveryDelaySeconds = 0.8f;
    private const int MaxVisibilityRecoveryAttemptsPerTrack = 1;

    private void Awake()
    {
        VideoPlayer originalVP = GetComponent<VideoPlayer>();
        if (string.IsNullOrEmpty(webGLSoundTargetKey))
        {
            webGLSoundTargetKey = ResolveWebGLSoundTargetKey();
        }

        videoRenderer = GetComponent<Renderer>();
        if (videoRenderer == null)
        {
            videoRenderer = GetComponentInChildren<Renderer>();
        }

        configuredRenderMode = originalVP.renderMode;
        configuredTargetRenderer = originalVP.targetMaterialRenderer != null ? originalVP.targetMaterialRenderer : videoRenderer;
        configuredTargetMaterialProperty = originalVP.targetMaterialProperty;
        configuredTargetTexture = originalVP.targetTexture;
        if (configuredTargetRenderer != null)
        {
            videoRenderer = configuredTargetRenderer;
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
        videoPlayer.renderMode = configuredRenderMode;
        
        videoPlayer.audioOutputMode = originalVP.audioOutputMode;
        if (originalVP.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            targetAudioSource = originalVP.GetTargetAudioSource(0);
            videoPlayer.EnableAudioTrack(0, originalVP.IsAudioTrackEnabled(0));
            videoPlayer.SetTargetAudioSource(0, targetAudioSource);
        }

        RebindVideoOutputTargets();
        
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

        if (pendingAudioRestoreCoroutine != null)
        {
            StopCoroutine(pendingAudioRestoreCoroutine);
            pendingAudioRestoreCoroutine = null;
        }

        if (pendingVisibilityRecoveryCoroutine != null)
        {
            StopCoroutine(pendingVisibilityRecoveryCoroutine);
            pendingVisibilityRecoveryCoroutine = null;
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
            if (videoPlayer.isPrepared && !videoRenderer.enabled && IsVideoTextureReady())
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
                    // ALSO verify the video texture is actually on the material,
                    // otherwise the chroma key shader sees white and won't clip anything.
                    if (movingFrames >= antiFlickerThreshold && IsVideoTextureReady())
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

    /// <summary>
    /// Checks if the video texture has been written to the material.
    /// Without this, the chroma key shader may see the default white texture
    /// and fail to make the green screen transparent.
    /// </summary>
    private bool IsVideoTextureReady()
    {
        if (videoRenderer == null) return false;

        // For MaterialOverride mode, the VideoPlayer writes directly to _MainTex
        Material mat = videoRenderer.material;
        if (mat == null) return false;

        Texture tex = mat.mainTexture;
        // A valid video texture will be non-null and larger than the default placeholder (usually 4x4 or 8x8)
        if (tex != null && tex.width > 16 && tex.height > 16)
        {
            return true;
        }

        // For RenderTexture mode, check the videoPlayer's texture output directly
        if (videoPlayer.texture != null && videoPlayer.texture.width > 16)
        {
            return true;
        }

        return false;
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
        visibilityRecoveryAttempts = 0;
        pendingSeekAfterPrepare = false;
        RebindVideoOutputTargets();

        if (hasFinished || !resumeVideoOnRetrack)
        {
            hasFinished = false;
            videoPlayer.time = 0;
        }

        // Because the VideoPlayer is persistent in the background, its internal memory remembers exactly where it paused!
        // Calling Play() resumes INSTANTLY with ZERO network buffering required after the first time.
        PlayVideoWithWebGLAutoplayFallback();
    }

    private void OnPrepareCompleted(VideoPlayer vp)
    {
        RebindVideoOutputTargets();

        if (pendingSeekAfterPrepare)
        {
            vp.time = pendingSeekTime;
            pendingSeekAfterPrepare = false;
        }

        // If it finally finished background preparation and the AR image is currently being tracked, play!
        if (gameObject.activeInHierarchy)
        {
            PlayVideoWithWebGLAutoplayFallback();
        }
    }

    private void OnDisable()
    {
        if (pendingAudioRestoreCoroutine != null)
        {
            StopCoroutine(pendingAudioRestoreCoroutine);
            pendingAudioRestoreCoroutine = null;
        }

        if (pendingVisibilityRecoveryCoroutine != null)
        {
            StopCoroutine(pendingVisibilityRecoveryCoroutine);
            pendingVisibilityRecoveryCoroutine = null;
        }

        ApplyWebGLAutoplayMute(false);

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

    private void PlayVideoWithWebGLAutoplayFallback()
    {
        RebindVideoOutputTargets();

#if UNITY_WEBGL && !UNITY_EDITOR
        SetWebGLCurrentTargetKey(webGLSoundTargetKey);

        if (RequiresWebGLSoundUnlock())
        {
            int unlockSerialAtPlay = GetWebGLSoundUnlockSerial();
            ApplyWebGLAutoplayMute(true);

            if (pendingAudioRestoreCoroutine != null)
            {
                StopCoroutine(pendingAudioRestoreCoroutine);
            }

            videoPlayer.Play();
            pendingAudioRestoreCoroutine = StartCoroutine(RestoreAudioAfterPlaybackStarts(unlockSerialAtPlay));
            StartVisibilityRecoveryWatch();
            return;
        }
#endif

        videoPlayer.Play();
        StartVisibilityRecoveryWatch();
    }

    private string ResolveWebGLSoundTargetKey()
    {
        if (transform.parent != null)
        {
            return transform.parent.name;
        }

        return gameObject.name;
    }

    private void OnWebGLTargetTrackingStateChanged(string targetId)
    {
        if (!string.IsNullOrEmpty(targetId))
        {
            webGLSoundTargetKey = targetId;
        }
    }

    private void RebindVideoOutputTargets()
    {
        if (videoPlayer == null)
        {
            return;
        }

        if (configuredRenderMode == VideoRenderMode.MaterialOverride)
        {
            if (configuredTargetRenderer == null)
            {
                configuredTargetRenderer = videoRenderer != null ? videoRenderer : GetComponent<Renderer>();
                if (configuredTargetRenderer == null)
                {
                    configuredTargetRenderer = GetComponentInChildren<Renderer>();
                }
            }

            if (configuredTargetRenderer != null)
            {
                videoPlayer.targetMaterialRenderer = configuredTargetRenderer;
                videoRenderer = configuredTargetRenderer;
            }

            videoPlayer.targetMaterialProperty = configuredTargetMaterialProperty;
            return;
        }

        if (configuredRenderMode == VideoRenderMode.RenderTexture)
        {
            videoPlayer.targetTexture = configuredTargetTexture;
        }
    }

    private void StartVisibilityRecoveryWatch()
    {
        if (pendingVisibilityRecoveryCoroutine != null)
        {
            StopCoroutine(pendingVisibilityRecoveryCoroutine);
        }

        pendingVisibilityRecoveryCoroutine = StartCoroutine(RecoverInvisiblePlaybackIfNeeded());
    }

    private System.Collections.IEnumerator RecoverInvisiblePlaybackIfNeeded()
    {
        while (videoPlayer != null && !videoPlayer.isPlaying)
        {
            yield return null;
        }

        float elapsed = 0f;
        while (videoPlayer != null && videoPlayer.isPlaying && elapsed < VisibilityRecoveryDelaySeconds)
        {
            if (videoRenderer == null || videoRenderer.enabled || IsVideoTextureReady())
            {
                pendingVisibilityRecoveryCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (videoPlayer == null || !videoPlayer.isPlaying || videoRenderer == null || videoRenderer.enabled || IsVideoTextureReady())
        {
            pendingVisibilityRecoveryCoroutine = null;
            yield break;
        }

        if (visibilityRecoveryAttempts >= MaxVisibilityRecoveryAttemptsPerTrack)
        {
            Debug.LogWarning($"[CDNARVideoController] Playback stayed audio-only for '{name}' after retrack. Recovery limit reached.");
            pendingVisibilityRecoveryCoroutine = null;
            yield break;
        }

        visibilityRecoveryAttempts++;
        pendingSeekTime = resumeVideoOnRetrack ? videoPlayer.time : 0d;
        pendingSeekAfterPrepare = resumeVideoOnRetrack && pendingSeekTime > 0.05d;

        Debug.LogWarning($"[CDNARVideoController] Recovering invisible playback for '{name}' at {pendingSeekTime:0.00}s.");

        RebindVideoOutputTargets();
        videoPlayer.Pause();
        videoPlayer.Prepare();
        pendingVisibilityRecoveryCoroutine = null;
    }

    private System.Collections.IEnumerator RestoreAudioAfterPlaybackStarts(int unlockSerialAtPlay)
    {
        while (videoPlayer != null && !videoPlayer.isPlaying)
        {
            yield return null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        while (videoPlayer != null && videoPlayer.isPlaying && GetWebGLSoundUnlockSerial() == unlockSerialAtPlay)
        {
            yield return null;
        }
#endif

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            ApplyWebGLAutoplayMute(false);
        }

        pendingAudioRestoreCoroutine = null;
    }

    private void ApplyWebGLAutoplayMute(bool muted)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (videoPlayer == null)
        {
            return;
        }

        if (videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            if (targetAudioSource != null)
            {
                targetAudioSource.mute = muted;
            }

            return;
        }

        if (videoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
        {
            videoPlayer.SetDirectAudioMute(0, muted);
        }
#endif
    }
}
