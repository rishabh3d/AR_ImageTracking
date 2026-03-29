mergeInto(LibraryManager.library, {
    // Track AR image found event
    WebGLTrackImageFound: function(id) {
        var targetId = UTF8ToString(id);
        if (window.arAnalytics) {
            window.arAnalytics.arImageFound(targetId);
        }
    },
    // Track AR image lost event
    WebGLTrackImageLost: function(id) {
        var targetId = UTF8ToString(id);
        if (window.arAnalytics) {
            window.arAnalytics.arImageLost(targetId);
        }
    },
    // Track custom event
    WebGLTrackEvent: function(eventType, eventDataJson) {
        if (window.arAnalytics) {
            var type = UTF8ToString(eventType);
            var dataStr = UTF8ToString(eventDataJson);
            var data = {};
            try { data = JSON.parse(dataStr); } catch(e) {}
            window.arAnalytics.track(type, data);
        }
    },
});
