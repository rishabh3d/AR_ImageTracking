<?php
/**
 * Pixel Tracking Endpoint — GET-based tracking via image pixel
 * Bypasses CORS and InfinityFree POST restrictions
 * 
 * The tracker loads this as an image: <img src="pixel.php?d=BASE64_JSON">
 * This technique is used by Google Analytics, Facebook Pixel, etc.
 */

// Always return a 1x1 transparent GIF
function servePixel() {
    header('Content-Type: image/gif');
    header('Cache-Control: no-cache, no-store, must-revalidate');
    header('Pragma: no-cache');
    header('Expires: 0');
    header('Access-Control-Allow-Origin: *');
    // 1x1 transparent GIF (43 bytes)
    echo base64_decode('R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7');
}

// Parse data from query parameter
$encoded = $_GET['d'] ?? '';
if (empty($encoded)) {
    servePixel();
    exit;
}

// Debug logging
$debug_file = __DIR__ . '/debug.txt';
$log_data = date('[Y-m-d H:i:s] ') . "Request: " . $_SERVER['REQUEST_URI'] . " | IP: " . $_SERVER['REMOTE_ADDR'] . "\n";
file_put_contents($debug_file, $log_data, FILE_APPEND);

// Decode the base64-encoded JSON data
$json = base64_decode($encoded);
$input = json_decode($json, true);

if (!$input) {
    file_put_contents($debug_file, "ERROR: Failed to decode JSON: " . $json . "\n", FILE_APPEND);
    servePixel();
    exit;
}

file_put_contents($debug_file, "SUCCESS: Decoded data for project key: " . ($input['api_key'] ?? 'MISSING') . "\n", FILE_APPEND);

// Load dependencies
require_once __DIR__ . '/../helpers/Database.php';
require_once __DIR__ . '/../helpers/Auth.php';
require_once __DIR__ . '/../config.php';

// Validate API key
$apiKey = $input['api_key'] ?? '';
$project = Auth::validateApiKey($apiKey);
if (!$project) {
    servePixel();
    exit;
}

$db = Database::getInstance();
$action = $input['action'] ?? '';

try {
    switch ($action) {
        case 'session':
            handlePixelSession($db, $project, $input);
            break;
        case 'event':
            handlePixelEvent($db, $project, $input);
            break;
        case 'heartbeat':
            handlePixelHeartbeat($db, $project, $input);
            break;
    }
} catch (Exception $e) {
    error_log('[AR Analytics Pixel] Error: ' . $e->getMessage());
}

// Always serve the pixel regardless of success/failure
servePixel();

// ═══════════════════════════════════════════════════
// Handlers (same logic as track.php but simplified)
// ═══════════════════════════════════════════════════

function handlePixelSession($db, $project, $input) {
    $sessionId = $input['sid'] ?? '';
    $visitorId = $input['vid'] ?? '';
    
    if (empty($sessionId) || empty($visitorId)) return;

    // Support ultra-short or standard keys
    $ua = $input['ua'] ?? $_SERVER['HTTP_USER_AGENT'] ?? 'Unknown';
    $deviceType = $input['dt'] ?? (strpos(strtolower($ua), 'mobile') !== false ? 'mobile' : 'desktop');
    $os = $input['os'] ?? 'Unknown';
    $browser = $input['br'] ?? 'Unknown';

    // Simple UA parsing if keys are missing
    if ($os === 'Unknown' || $browser === 'Unknown') {
        if (strpos($ua, 'iPhone') !== false || strpos($ua, 'iPad') !== false) $os = 'iOS';
        else if (strpos($ua, 'Android') !== false) $os = 'Android';
        else if (strpos($ua, 'Windows') !== false) $os = 'Windows';
        else if (strpos($ua, 'Macintosh') !== false) $os = 'macOS';

        if (strpos($ua, 'Chrome') !== false) $browser = 'Chrome';
        else if (strpos($ua, 'Safari') !== false) $browser = 'Safari';
        else if (strpos($ua, 'Firefox') !== false) $browser = 'Firefox';
        else if (strpos($ua, 'Edg') !== false) $browser = 'Edge';
    }

    // Check if session already exists
    $existing = $db->queryOne(
        "SELECT id FROM sessions WHERE project_id = ? AND session_id = ?",
        [$project['id'], $sessionId]
    );

    if ($existing) {
        $db->execute(
            "UPDATE sessions SET last_active_at = NOW() WHERE id = ?",
            [$existing['id']]
        );
        return;
    }

    // Check if returning visitor
    $isNewVisitor = !$db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND visitor_id = ? AND session_id != ?",
        [$project['id'], $visitorId, $sessionId]
    );

    // Get geo data from IP
    $ip = getPixelClientIP();
    $geo = getPixelGeoData($ip);

    $db->insert(
        "INSERT INTO sessions (project_id, session_id, visitor_id, is_new_visitor, 
         device_type, os, browser, country, city, language, screen_width, screen_height,
         referrer, utm_source, utm_medium, utm_campaign, page_url, ip_address)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        [
            $project['id'],
            $sessionId,
            $visitorId,
            $isNewVisitor ? 1 : 0,
            $deviceType,
            $os,
            $browser,
            $geo['country'] ?? 'Unknown',
            $geo['city'] ?? 'Unknown',
            $input['lang'] ?? 'en',
            intval($input['sw'] ?? 0),
            intval($input['sh'] ?? 0),
            substr($input['ref'] ?? '', 0, 500),
            substr($input['us'] ?? '', 0, 100),
            substr($input['um'] ?? '', 0, 100),
            substr($input['uc'] ?? '', 0, 100),
            substr($input['url'] ?? '', 0, 500),
            $ip,
        ]
    );
}

function handlePixelEvent($db, $project, $input) {
    $sessionId = $input['sid'] ?? '';
    $eventType = $input['et'] ?? '';
    
    if (empty($sessionId) || empty($eventType)) return;

    $eventType = preg_replace('/[^a-zA-Z0-9_]/', '', substr($eventType, 0, 50));
    $eventData = isset($input['ed']) ? json_encode($input['ed']) : null;

    $db->insert(
        "INSERT INTO events (project_id, session_id, event_type, event_data) VALUES (?, ?, ?, ?)",
        [$project['id'], $sessionId, $eventType, $eventData]
    );

    $db->execute(
        "UPDATE sessions SET last_active_at = NOW() WHERE project_id = ? AND session_id = ?",
        [$project['id'], $sessionId]
    );
}

function handlePixelHeartbeat($db, $project, $input) {
    $sessionId = $input['sid'] ?? '';
    // Support both 'dur' and 'duration'
    $duration = intval($input['dur'] ?? $input['duration'] ?? 0);
    
    if (empty($sessionId)) return;

    $db->execute(
        "UPDATE sessions SET duration_seconds = ?, last_active_at = NOW() 
         WHERE project_id = ? AND session_id = ?",
        [$duration, $project['id'], $sessionId]
    );
}

function getPixelClientIP() {
    $headers = ['HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_REAL_IP', 'REMOTE_ADDR'];
    foreach ($headers as $header) {
        if (!empty($_SERVER[$header])) {
            $ip = explode(',', $_SERVER[$header])[0];
            return trim($ip);
        }
    }
    return '0.0.0.0';
}

function getPixelGeoData($ip) {
    if (in_array($ip, ['127.0.0.1', '::1', '0.0.0.0']) || 
        preg_match('/^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.)/', $ip)) {
        return ['country' => 'Local', 'city' => 'Local'];
    }

    try {
        $ctx = stream_context_create(['http' => ['timeout' => 2]]);
        $response = @file_get_contents(GEOIP_API . $ip . '?fields=country,city', false, $ctx);
        if ($response) {
            $data = json_decode($response, true);
            if ($data && ($data['status'] ?? '') !== 'fail') {
                return [
                    'country' => $data['country'] ?? 'Unknown',
                    'city' => $data['city'] ?? 'Unknown',
                ];
            }
        }
    } catch (Exception $e) {
        error_log("GeoIP lookup failed: " . $e->getMessage());
    }

    return ['country' => 'Unknown', 'city' => 'Unknown'];
}
