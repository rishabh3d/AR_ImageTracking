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
});
