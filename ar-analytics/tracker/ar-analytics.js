/**
 * AR Analytics Tracker v1.1
 * Lightweight analytics for WebAR experiences
 */
(function() {
    'use strict';

    // ─── Configuration ───
    const SCRIPT_TAG = document.currentScript || document.querySelector('script[data-project]');
    const API_KEY = SCRIPT_TAG ? SCRIPT_TAG.getAttribute('data-project') : null;
    const HEARTBEAT_INTERVAL = 30000; // 30 seconds
    const SESSION_TIMEOUT = 30 * 60 * 1000; // 30 minutes

    // Calculate API Base robustly
    let api_base_calc = '';
    if (SCRIPT_TAG) {
        const src = SCRIPT_TAG.src;
        api_base_calc = src.substring(0, src.lastIndexOf('/tracker/'));
    }
    const API_BASE = api_base_calc;

    if (!API_KEY) {
        console.error('[AR Analytics] No API Key provided (data-project attribute). Tracking disabled.');
        return;
    }

    // ─── ID Management ───
    const STORAGE_PREFIX = 'ara_';
    function generateId() { return 'ara_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now().toString(36); }
    
    function getVisitorId() {
        let vid = localStorage.getItem(STORAGE_PREFIX + 'visitor_id');
        if (!vid) { vid = generateId(); localStorage.setItem(STORAGE_PREFIX + 'visitor_id', vid); }
        return vid;
    }

    function getSessionId() {
        let sid = sessionStorage.getItem(STORAGE_PREFIX + 'session_id');
        let lastActive = parseInt(sessionStorage.getItem(STORAGE_PREFIX + 'last_active') || '0');
        if (!sid || (Date.now() - lastActive > SESSION_TIMEOUT)) {
            sid = generateId();
            sessionStorage.setItem(STORAGE_PREFIX + 'session_id', sid);
        }
        sessionStorage.setItem(STORAGE_PREFIX + 'last_active', Date.now().toString());
        return sid;
    }

    // ─── Detection Hub ───
    const detect = {
        type: () => /tablet|ipad|playbook|silk/i.test(navigator.userAgent) ? 'tablet' : (/mobile|iphone|ipod|android/i.test(navigator.userAgent) ? 'mobile' : 'desktop'),
        os: () => {
            const ua = navigator.userAgent;
            if (/iPad|iPhone|iPod/.test(ua)) return 'iOS';
            if (/Android/.test(ua)) return 'Android';
            if (/Windows/.test(ua)) return 'Windows';
            if (/Mac/.test(ua)) return 'macOS';
            return 'Other';
        },
        browser: () => {
            const ua = navigator.userAgent;
            if (/Edg/.test(ua)) return 'Edge';
            if (/Chrome/.test(ua)) return 'Chrome';
            if (/Safari/.test(ua)) return 'Safari';
            if (/Firefox/.test(ua)) return 'Firefox';
            return 'Other';
        }
    };

    // ─── Transmission Engine ───
    function send(action, data) {
        data.api_key = API_KEY;
        data.action = action;
        data.ts = Date.now();
        const payload = btoa(JSON.stringify(data));
        const url = `${API_BASE}/api/controllers/pixel.php?d=${encodeURIComponent(payload)}`;
        
        // Use Beacon API for final data if available
        if (action === 'heartbeat' && navigator.sendBeacon) {
            navigator.sendBeacon(url);
        } else {
            const img = new Image();
            img.src = url;
        }
    }

    // ─── Tracking Logic ───
    const vid = getVisitorId();
    const sid = getSessionId();
    const startTime = Date.now();
    let arActive = false;
    let targetTimers = {};

    // 1. Initial Session Ping
    send('session', {
        sid, vid,
        dt: detect.type(),
        os: detect.os(),
        br: detect.browser(),
        sw: screen.width,
        sh: screen.height,
        ref: document.referrer || '',
        url: window.location.href,
        lang: navigator.language
    });

    // 2. Heartbeat & Duration
    setInterval(() => {
        const dur = Math.round((Date.now() - startTime) / 1000);
        send('heartbeat', { sid, dur });
    }, HEARTBEAT_INTERVAL);

    // Final heartbeat on close
    window.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            const dur = Math.round((Date.now() - startTime) / 1000);
            send('heartbeat', { sid, dur });
        }
    });

    // ─── Public API ───
    window.arAnalytics = {
        track: (et, ed) => send('event', { sid, et, ed: ed || {} }),
        
        arSessionStart: function() { this.track('ar_session_start'); },
        
        arImageFound: function(tid) {
            if (!arActive) { arActive = true; this.track('ar_activation', { target_id: tid }); }
            this.track('ar_image_found', { target_id: tid });
            targetTimers[tid] = Date.now();
        },
        
        arImageLost: function(tid) {
            if (targetTimers[tid]) {
                const dur = Math.round((Date.now() - targetTimers[tid]) / 1000);
                if (dur > 0) this.track('ar_scan_duration', { target_id: tid, duration: dur });
                delete targetTimers[tid];
            }
            this.track('ar_image_lost', { target_id: tid });
        },

        arCtaClick: function(type, val) { this.track('ar_cta_click', { type, value: val }); }
    };

    // ─── Automated Hooks for Imagine WebAR (iTracker) ───
    (function installHooks() {
        const functionsToHook = {
            'ShowError': 'ar_error',
            'ShowScreenshot': 'ar_screenshot',
            'ShowConfirmUrl': 'ar_cta_click',
            'FlipCam': 'ar_camera_flip'
        };

        const checkAndHook = () => {
            Object.keys(functionsToHook).forEach(fn => {
                if (window[fn] && !window['__ara_hooked_' + fn]) {
                    const original = window[fn];
                    window[fn] = function() {
                        const args = Array.from(arguments);
                        window.arAnalytics.track(functionsToHook[fn], { args });
                        return original.apply(this, args);
                    };
                    window['__ara_hooked_' + fn] = true;
                }
            });
        };

        // Poll for 10 seconds to catch delayed definitions
        let hooksFound = 0;
        const poll = setInterval(() => {
            checkAndHook();
            hooksFound++;
            if (hooksFound > 100) clearInterval(poll);
        }, 100);
    })();

    console.log('[AR Analytics] Tracker Active · Project: ' + API_KEY.substring(0, 8));
})();
