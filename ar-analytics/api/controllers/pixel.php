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

// Decode the base64-encoded JSON data
$json = base64_decode($encoded);
$input = json_decode($json, true);

if (!$input) {
    servePixel();
    exit;
}

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
            $input['dt'] ?? 'unknown',
            $input['os'] ?? 'unknown',
            $input['br'] ?? 'unknown',
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
    $duration = intval($input['dur'] ?? 0);
    
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
