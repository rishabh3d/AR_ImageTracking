<?php
/**
 * Dashboard Controller — Provides analytics data for client dashboards
 */

require_once __DIR__ . '/../helpers/Database.php';
require_once __DIR__ . '/../helpers/Auth.php';
require_once __DIR__ . '/../helpers/Response.php';

Response::cors();
Response::json();

$user = Auth::requireAuth();
$db = Database::getInstance();

$action = $_GET['action'] ?? '';
$projectId = intval($_GET['project_id'] ?? 0);

// Verify project ownership (clients can only see their own)
if ($user['role'] === 'client') {
    if ($projectId > 0) {
        $project = $db->queryOne(
            "SELECT id FROM projects WHERE id = ? AND client_id = ?",
            [$projectId, $user['user_id']]
        );
        if (!$project) Response::error('Project not found', 404);
    } else {
        // Get first project if not specified
        $project = $db->queryOne(
            "SELECT id FROM projects WHERE client_id = ? AND is_active = 1 LIMIT 1",
            [$user['user_id']]
        );
        if ($project) $projectId = $project['id'];
        else Response::error('No projects found', 404);
    }
} elseif ($user['role'] === 'admin') {
    // Admin can view any project — verify it exists
    if ($projectId > 0) {
        $project = $db->queryOne("SELECT id FROM projects WHERE id = ?", [$projectId]);
        if (!$project) Response::error('Project not found', 404);
    }
}

// Date range
$range = $_GET['range'] ?? '7d';
$dateFrom = getDateFrom($range);

switch ($action) {
    case 'overview':
        handleOverview($db, $projectId, $dateFrom);
        break;
    case 'views-over-time':
        handleViewsOverTime($db, $projectId, $dateFrom, $range);
        break;
    case 'devices':
        handleDevices($db, $projectId, $dateFrom);
        break;
    case 'browsers':
        handleBrowsers($db, $projectId, $dateFrom);
        break;
    case 'locations':
        handleLocations($db, $projectId, $dateFrom);
        break;
    case 'traffic':
        handleTraffic($db, $projectId, $dateFrom);
        break;
    case 'ar-stats':
        handleARStats($db, $projectId, $dateFrom);
        break;
    case 'sessions-list':
        handleSessionsList($db, $projectId, $dateFrom);
        break;
    case 'peak-hours':
        handlePeakHours($db, $projectId, $dateFrom);
        break;
    case 'languages':
        handleLanguages($db, $projectId, $dateFrom);
        break;
    case 'screen-sizes':
        handleScreenSizes($db, $projectId, $dateFrom);
        break;
    default:
        Response::error('Unknown action', 400);
}

// ═══════════════════════════════════════════════════
// Handler functions
// ═══════════════════════════════════════════════════

function handleOverview($db, $projectId, $dateFrom) {
    // Total views (sessions)
    $totalViews = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND started_at >= ?",
        [$projectId, $dateFrom]
    );

    // Unique visitors
    $uniqueVisitors = $db->scalar(
        "SELECT COUNT(DISTINCT visitor_id) FROM sessions WHERE project_id = ? AND started_at >= ?",
        [$projectId, $dateFrom]
    );

    // New vs returning
    $newVisitors = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND started_at >= ? AND is_new_visitor = 1",
        [$projectId, $dateFrom]
    );

    // Average session duration
    $avgDuration = $db->scalar(
        "SELECT COALESCE(AVG(duration_seconds), 0) FROM sessions WHERE project_id = ? AND started_at >= ? AND duration_seconds > 0",
        [$projectId, $dateFrom]
    );

    // AR activations
    $arActivations = $db->scalar(
        "SELECT COUNT(*) FROM events WHERE project_id = ? AND event_type = 'ar_activation' AND created_at >= ?",
        [$projectId, $dateFrom]
    );

    // AR scans (image found events) - unique sessions
    $arScans = $db->scalar(
        "SELECT COUNT(DISTINCT session_id) FROM events WHERE project_id = ? AND event_type = 'ar_image_found' AND created_at >= ?",
        [$projectId, $dateFrom]
    );

    // Mobile percentage
    $mobileCount = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND started_at >= ? AND device_type = 'mobile'",
        [$projectId, $dateFrom]
    );

    // Return rate
    $returningVisitors = $totalViews > 0 ? $totalViews - $newVisitors : 0;
    $returnRate = $totalViews > 0 ? round(($returningVisitors / $totalViews) * 100, 1) : 0;

    Response::success([
        'total_views' => intval($totalViews),
        'unique_visitors' => intval($uniqueVisitors),
        'new_visitors' => intval($newVisitors),
        'returning_visitors' => intval($returningVisitors),
        'avg_session_duration' => round($avgDuration),
        'ar_activations' => intval($arActivations),
        'ar_scans' => intval($arScans),
        'mobile_percentage' => $totalViews > 0 ? round(($mobileCount / $totalViews) * 100, 1) : 0,
        'return_rate' => $returnRate,
    ]);
}

function handleViewsOverTime($db, $projectId, $dateFrom, $range) {
    $groupBy = in_array($range, ['30d', '90d', '365d']) ? '%Y-%m-%d' : '%Y-%m-%d';
    
    // For 7-day range, group by day with day name
    if ($range === '7d') {
        $rows = $db->query(
            "SELECT DATE(started_at) as date, 
                    DAYNAME(DATE(started_at)) as day_name,
                    COUNT(*) as views
             FROM sessions 
             WHERE project_id = ? AND started_at >= ?
             GROUP BY DATE(started_at) 
             ORDER BY date ASC",
            [$projectId, $dateFrom]
        );
    } else {
        $rows = $db->query(
            "SELECT DATE(started_at) as date, COUNT(*) as views
             FROM sessions 
             WHERE project_id = ? AND started_at >= ?
             GROUP BY DATE(started_at) 
             ORDER BY date ASC",
            [$projectId, $dateFrom]
        );
    }

    Response::success($rows);
}

function handleDevices($db, $projectId, $dateFrom) {
    // Device type split
    $devices = $db->query(
        "SELECT device_type as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY device_type ORDER BY count DESC",
        [$projectId, $dateFrom]
    );

    // OS split
    $os = $db->query(
        "SELECT os as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY os ORDER BY count DESC LIMIT 10",
        [$projectId, $dateFrom]
    );

    // AR usage rate
    $totalSessions = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND started_at >= ?",
        [$projectId, $dateFrom]
    );
    $arSessions = $db->scalar(
        "SELECT COUNT(DISTINCT session_id) FROM events 
         WHERE project_id = ? AND event_type = 'ar_activation' AND created_at >= ?",
        [$projectId, $dateFrom]
    );

    Response::success([
        'devices' => $devices,
        'os' => $os,
        'total_sessions' => intval($totalSessions),
        'ar_sessions' => intval($arSessions),
        'ar_usage_rate' => $totalSessions > 0 ? round(($arSessions / $totalSessions) * 100, 1) : 0,
    ]);
}

function handleBrowsers($db, $projectId, $dateFrom) {
    $browsers = $db->query(
        "SELECT browser as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY browser ORDER BY count DESC LIMIT 10",
        [$projectId, $dateFrom]
    );
    Response::success($browsers);
}

function handleLocations($db, $projectId, $dateFrom) {
    $countries = $db->query(
        "SELECT country as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY country ORDER BY count DESC LIMIT 20",
        [$projectId, $dateFrom]
    );

    $cities = $db->query(
        "SELECT CONCAT(city, ', ', country) as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY city, country ORDER BY count DESC LIMIT 20",
        [$projectId, $dateFrom]
    );

    Response::success(['countries' => $countries, 'cities' => $cities]);
}

function handleTraffic($db, $projectId, $dateFrom) {
    // Referrer sources
    $sources = $db->query(
        "SELECT CASE 
            WHEN referrer = '' OR referrer IS NULL THEN 'Direct'
            ELSE SUBSTRING_INDEX(SUBSTRING_INDEX(referrer, '/', 3), '//', -1)
         END as label, COUNT(*) as count
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY label ORDER BY count DESC LIMIT 15",
        [$projectId, $dateFrom]
    );

    // UTM breakdown
    $utm = $db->query(
        "SELECT utm_source as source, utm_medium as medium, utm_campaign as campaign, COUNT(*) as count
         FROM sessions WHERE project_id = ? AND started_at >= ? AND utm_source IS NOT NULL AND utm_source != ''
         GROUP BY utm_source, utm_medium, utm_campaign ORDER BY count DESC LIMIT 15",
        [$projectId, $dateFrom]
    );

    Response::success(['sources' => $sources, 'utm' => $utm]);
}

function handleARStats($db, $projectId, $dateFrom) {
    // AR activations over time
    $activations = $db->query(
        "SELECT DATE(created_at) as date, COUNT(*) as count
         FROM events WHERE project_id = ? AND event_type = 'ar_activation' AND created_at >= ?
         GROUP BY DATE(created_at) ORDER BY date ASC",
        [$projectId, $dateFrom]
    );

    // Most scanned targets (unique sessions per target)
    $targets = $db->query(
        "SELECT JSON_UNQUOTE(JSON_EXTRACT(event_data, '$.target_id')) as target_id, COUNT(DISTINCT session_id) as count
         FROM events WHERE project_id = ? AND event_type = 'ar_image_found' AND created_at >= ?
         GROUP BY target_id ORDER BY count DESC LIMIT 10",
        [$projectId, $dateFrom]
    );

    // Average scan duration
    $avgScanDuration = $db->scalar(
        "SELECT COALESCE(AVG(CAST(JSON_UNQUOTE(JSON_EXTRACT(event_data, '$.duration')) AS UNSIGNED)), 0)
         FROM events WHERE project_id = ? AND event_type = 'ar_scan_duration' AND created_at >= ?",
        [$projectId, $dateFrom]
    );

    // CTA clicks
    $ctaClicks = $db->scalar(
        "SELECT COUNT(*) FROM events WHERE project_id = ? AND event_type = 'ar_cta_click' AND created_at >= ?",
        [$projectId, $dateFrom]
    );

    Response::success([
        'activations' => $activations,
        'top_targets' => $targets,
        'avg_scan_duration' => round($avgScanDuration),
        'cta_clicks' => intval($ctaClicks),
    ]);
}

function handleSessionsList($db, $projectId, $dateFrom) {
    $page = max(1, intval($_GET['page'] ?? 1));
    $limit = 50;
    $offset = ($page - 1) * $limit;

    $sessions = $db->query(
        "SELECT session_id, visitor_id, is_new_visitor, started_at, duration_seconds,
                device_type, os, browser, country, city, referrer
         FROM sessions WHERE project_id = ? AND started_at >= ?
         ORDER BY started_at DESC LIMIT $limit OFFSET $offset",
        [$projectId, $dateFrom]
    );

    $total = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE project_id = ? AND started_at >= ?",
        [$projectId, $dateFrom]
    );

    Response::success([
        'sessions' => $sessions,
        'total' => intval($total),
        'page' => $page,
        'pages' => ceil($total / $limit),
    ]);
}

function handlePeakHours($db, $projectId, $dateFrom) {
    $hours = $db->query(
        "SELECT HOUR(started_at) as hour, COUNT(*) as count
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY HOUR(started_at) ORDER BY hour ASC",
        [$projectId, $dateFrom]
    );

    // Fill in missing hours with 0
    $hourMap = array_fill(0, 24, 0);
    foreach ($hours as $h) {
        $hourMap[intval($h['hour'])] = intval($h['count']);
    }

    $result = [];
    foreach ($hourMap as $hour => $count) {
        $result[] = [
            'hour' => $hour,
            'label' => sprintf('%02d:00', $hour),
            'count' => $count,
        ];
    }

    Response::success($result);
}

function handleLanguages($db, $projectId, $dateFrom) {
    $languages = $db->query(
        "SELECT language as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY language ORDER BY count DESC LIMIT 15",
        [$projectId, $dateFrom]
    );
    Response::success($languages);
}

function handleScreenSizes($db, $projectId, $dateFrom) {
    $sizes = $db->query(
        "SELECT CONCAT(screen_width, 'x', screen_height) as label, COUNT(*) as count 
         FROM sessions WHERE project_id = ? AND started_at >= ?
         GROUP BY screen_width, screen_height ORDER BY count DESC LIMIT 15",
        [$projectId, $dateFrom]
    );
    Response::success($sizes);
}

// ═══════════════════════════════════════════════════
// Helper
// ═══════════════════════════════════════════════════

function getDateFrom($range) {
    switch ($range) {
        case '24h': return date('Y-m-d H:i:s', strtotime('-24 hours'));
        case '7d':  return date('Y-m-d', strtotime('-7 days'));
        case '30d': return date('Y-m-d', strtotime('-30 days'));
        case '90d': return date('Y-m-d', strtotime('-90 days'));
        case '365d': return date('Y-m-d', strtotime('-365 days'));
        default:    return date('Y-m-d', strtotime('-7 days'));
    }
}
