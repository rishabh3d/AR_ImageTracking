<?php
/**
 * Track Controller — Receives events from the ar-analytics.js tracker
 * These endpoints are PUBLIC (authenticated via API key, not user auth)
 */

require_once __DIR__ . '/../helpers/Database.php';
require_once __DIR__ . '/../helpers/Auth.php';
require_once __DIR__ . '/../helpers/Response.php';

Response::cors();
Response::json();

$method = $_SERVER['REQUEST_METHOD'];
$action = $_GET['action'] ?? '';

if ($method !== 'POST') {
    Response::error('Method not allowed', 405);
}

// Get and validate API key
$input = json_decode(file_get_contents('php://input'), true);
if (!$input) {
    Response::error('Invalid JSON body', 400);
}

$apiKey = $input['api_key'] ?? $_SERVER['HTTP_X_API_KEY'] ?? '';
$project = Auth::validateApiKey($apiKey);
if (!$project) {
    Response::error('Invalid API key', 401);
}

$db = Database::getInstance();

switch ($action) {
    case 'session':
        handleSession($db, $project, $input);
        break;
    case 'event':
        handleEvent($db, $project, $input);
        break;
    case 'heartbeat':
        handleHeartbeat($db, $project, $input);
        break;
    default:
        Response::error('Unknown action', 400);
}

/**
 * Start or resume a session
 */
function handleSession($db, $project, $input) {
    $sessionId = $input['session_id'] ?? '';
    $visitorId = $input['visitor_id'] ?? '';
    
    if (empty($sessionId) || empty($visitorId)) {
        Response::error('session_id and visitor_id are required', 400);
    }

    // Check if session already exists
    $existing = $db->queryOne(
        "SELECT id FROM sessions WHERE project_id = ? AND session_id = ?",
        [$project['id'], $sessionId]
    );

    if ($existing) {
        // Update last active
        $db->execute(
            "UPDATE sessions SET last_active_at = NOW() WHERE id = ?",
            [$existing['id']]
        );
        Response::success(['session_id' => $sessionId, 'resumed' => true]);
    }

    // Check if this is a returning visitor
    $isNewVisitor = !$db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND visitor_id = ? AND session_id != ?",
        [$project['id'], $visitorId, $sessionId]
    );

    // Get geo data from IP
    $ip = getClientIP();
    $geo = getGeoData($ip);

    // Insert new session
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
            $input['device_type'] ?? 'unknown',
            $input['os'] ?? 'unknown',
            $input['browser'] ?? 'unknown',
            $geo['country'] ?? 'Unknown',
            $geo['city'] ?? 'Unknown',
            $input['language'] ?? 'en',
            intval($input['screen_width'] ?? 0),
            intval($input['screen_height'] ?? 0),
            substr($input['referrer'] ?? '', 0, 500),
            substr($input['utm_source'] ?? '', 0, 100),
            substr($input['utm_medium'] ?? '', 0, 100),
            substr($input['utm_campaign'] ?? '', 0, 100),
            substr($input['page_url'] ?? '', 0, 500),
            $ip,
        ]
    );

    Response::success(['session_id' => $sessionId, 'new_visitor' => $isNewVisitor]);
}

/**
 * Log an event
 */
function handleEvent($db, $project, $input) {
    $sessionId = $input['session_id'] ?? '';
    $eventType = $input['event_type'] ?? '';
    
    if (empty($sessionId) || empty($eventType)) {
        Response::error('session_id and event_type are required', 400);
    }

    // Sanitize event type (max 50 chars, alphanumeric + underscore)
    $eventType = preg_replace('/[^a-zA-Z0-9_]/', '', substr($eventType, 0, 50));

    $eventData = $input['event_data'] ?? null;
    $eventDataJson = $eventData ? json_encode($eventData) : null;

    $db->insert(
        "INSERT INTO events (project_id, session_id, event_type, event_data) VALUES (?, ?, ?, ?)",
        [$project['id'], $sessionId, $eventType, $eventDataJson]
    );

    // Update session last_active
    $db->execute(
        "UPDATE sessions SET last_active_at = NOW() WHERE project_id = ? AND session_id = ?",
        [$project['id'], $sessionId]
    );

    Response::success(null, 'Event logged');
}

/**
 * Update session duration (called periodically by tracker)
 */
function handleHeartbeat($db, $project, $input) {
    $sessionId = $input['session_id'] ?? '';
    $duration = intval($input['duration'] ?? 0);
    
    if (empty($sessionId)) {
        Response::error('session_id is required', 400);
    }

    $db->execute(
        "UPDATE sessions SET duration_seconds = ?, last_active_at = NOW() 
         WHERE project_id = ? AND session_id = ?",
        [$duration, $project['id'], $sessionId]
    );

    Response::success(null, 'Heartbeat received');
}

/**
 * Get client's real IP address
 */
function getClientIP() {
    $headers = ['HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'HTTP_X_REAL_IP', 'REMOTE_ADDR'];
    foreach ($headers as $header) {
        if (!empty($_SERVER[$header])) {
            $ip = explode(',', $_SERVER[$header])[0];
            return trim($ip);
        }
    }
    return '0.0.0.0';
}

/**
 * Get geo data from IP using free API
 */
function getGeoData($ip) {
    // Skip for localhost/private IPs
    if (in_array($ip, ['127.0.0.1', '::1', '0.0.0.0']) || 
        preg_match('/^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.)/', $ip)) {
        return ['country' => 'Local', 'city' => 'Local'];
    }

    try {
        $ctx = stream_context_create(['http' => ['timeout' => 2]]);
        $response = @file_get_contents(GEOIP_API . $ip . '?fields=country,city', false, $ctx);
        if ($response) {
            $data = json_decode($response, true);
            if ($data && $data['status'] !== 'fail') {
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
