<?php
/**
 * AR Analytics Platform — Configuration
 * Update these values with your Hostinger database credentials
 */

// ─── Database Settings ───
define('DB_HOST', 'localhost');          // Hostinger usually uses localhost
define('DB_NAME', 'ar_analytics');      // Your database name
define('DB_USER', 'your_db_username');  // From Hostinger hPanel → Databases
define('DB_PASS', 'your_db_password');  // From Hostinger hPanel → Databases
define('DB_CHARSET', 'utf8mb4');

// ─── App Settings ───
define('APP_NAME', 'AR Analytics');
define('APP_URL', 'https://yoursite.com/ar-analytics'); // Your analytics URL
define('APP_SECRET', 'CHANGE_THIS_TO_A_RANDOM_64_CHAR_STRING'); // Used for token signing

// ─── Session Settings ───
define('SESSION_LIFETIME', 86400 * 7); // 7 days
define('TOKEN_LIFETIME', 86400 * 30);  // 30 days

// ─── CORS Settings ───
// Allow tracker to send events from any domain (client AR builds)
define('CORS_ALLOWED_ORIGINS', '*');

// ─── Rate Limiting ───
define('RATE_LIMIT_TRACK', 60);        // Max events per minute per IP
define('RATE_LIMIT_API', 120);         // Max API requests per minute per user

// ─── Geo IP ───
// Free API for IP geolocation (no key needed, 45 req/min limit)
define('GEOIP_API', 'http://ip-api.com/json/');

// ─── Plan Limits ───
define('PLAN_LIMITS', json_encode([
    'free' => [
        'max_views' => 1000,
        'retention_days' => 7,
        'realtime' => false,
        'export' => false,
    ],
    'pro' => [
        'max_views' => 50000,
        'retention_days' => 90,
        'realtime' => false,
        'export' => true,
    ],
    'business' => [
        'max_views' => -1, // unlimited
        'retention_days' => 365,
        'realtime' => true,
        'export' => true,
    ],
]));

// ─── Error Reporting (set to 0 in production) ───
error_reporting(E_ALL);
ini_set('display_errors', '0');
ini_set('log_errors', '1');

// ─── Timezone ───
date_default_timezone_set('Asia/Kolkata');
