mergeInto(LibraryManager.library, {
    ShowConfirmUrl: function(url)
    {
        window.ShowConfirmUrl(UTF8ToString(url));
    },
    ShowWebGLScreenshot: function(dataUrl)
    {
        window.ShowScreenshot(UTF8ToString(dataUrl));
    },
    // v1.8.0: Direct URL opener for business card helpers (tel:, mailto:, etc.)
    OpenUrl: function(url)
    {
        window.open(UTF8ToString(url), '_self');
    },
    GetWebGLSoundUnlockSerial: function()
    {
        return window.__AR_SOUND_UNLOCK_SERIAL || 0;
    },
    RequiresWebGLSoundUnlock: function()
    {
        if (!window.__AR_REQUIRE_SOUND_GESTURE) {
            return 0;
        }

        var targetKey = window.__AR_CURRENT_TARGET_KEY || "";
        if (!targetKey) {
            return 1;
        }

        var targetUnlocks = window.__AR_TARGET_SOUND_UNLOCKS || {};
        return targetUnlocks[targetKey] ? 0 : 1;
    },
    SetWebGLCurrentTargetKey: function(targetKey)
    {
        window.__AR_CURRENT_TARGET_KEY = UTF8ToString(targetKey);
    },
});
