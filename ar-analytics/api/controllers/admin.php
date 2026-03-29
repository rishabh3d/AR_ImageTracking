<?php
/**
 * Admin Controller — Client & project management (admin only)
 */

require_once __DIR__ . '/../helpers/Database.php';
require_once __DIR__ . '/../helpers/Auth.php';
require_once __DIR__ . '/../helpers/Response.php';

Response::cors();
Response::json();

$user = Auth::requireAuth('admin');
$db = Database::getInstance();

$method = $_SERVER['REQUEST_METHOD'];
$action = $_GET['action'] ?? '';

switch ($action) {
    // ─── Client Management ───
    case 'clients':
        if ($method === 'GET') listClients($db);
        elseif ($method === 'POST') createClient($db);
        else Response::error('Method not allowed', 405);
        break;
    case 'client':
        $id = intval($_GET['id'] ?? 0);
        if ($method === 'GET') getClient($db, $id);
        elseif ($method === 'PUT') updateClient($db, $id);
        elseif ($method === 'DELETE') deleteClient($db, $id);
        else Response::error('Method not allowed', 405);
        break;

    // ─── Project Management ───
    case 'projects':
        if ($method === 'GET') listProjects($db);
        elseif ($method === 'POST') createProject($db);
        else Response::error('Method not allowed', 405);
        break;
    case 'project':
        $id = intval($_GET['id'] ?? 0);
        if ($method === 'GET') getProject($db, $id);
        elseif ($method === 'PUT') updateProject($db, $id);
        elseif ($method === 'DELETE') deleteProject($db, $id);
        else Response::error('Method not allowed', 405);
        break;

    // ─── Global Overview ───
    case 'overview':
        adminOverview($db);
        break;

    default:
        Response::error('Unknown action', 400);
}

// ═════════════════════════════════════════
// Client Functions
// ═════════════════════════════════════════

function listClients($db) {
    $clients = $db->query(
        "SELECT c.id, c.name, c.email, c.company, c.phone, c.plan_tier, c.is_active, 
                c.created_at, c.last_login,
                COUNT(DISTINCT p.id) as project_count,
                COALESCE(SUM(stats.total_views), 0) as total_views
         FROM clients c
         LEFT JOIN projects p ON c.id = p.client_id
         LEFT JOIN (
             SELECT project_id, COUNT(*) as total_views 
             FROM sessions 
             WHERE started_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
             GROUP BY project_id
         ) stats ON p.id = stats.project_id
         GROUP BY c.id
         ORDER BY c.created_at DESC"
    );
    Response::success($clients);
}

function createClient($db) {
    $input = json_decode(file_get_contents('php://input'), true);
    
    $name = trim($input['name'] ?? '');
    $email = trim($input['email'] ?? '');
    $password = $input['password'] ?? '';
    $company = trim($input['company'] ?? '');
    $phone = trim($input['phone'] ?? '');
    $plan = $input['plan_tier'] ?? 'free';

    if (empty($name) || empty($email) || empty($password)) {
        Response::error('Name, email, and password are required', 400);
    }

    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        Response::error('Invalid email address', 400);
    }

    // Check duplicate email
    $existing = $db->scalar("SELECT COUNT(*) FROM clients WHERE email = ?", [$email]);
    if ($existing > 0) {
        Response::error('A client with this email already exists', 409);
    }

    $planLimits = json_decode(PLAN_LIMITS, true);
    $maxViews = $planLimits[$plan]['max_views'] ?? 1000;

    $clientId = $db->insert(
        "INSERT INTO clients (name, email, password_hash, company, phone, plan_tier, max_views_per_month) 
         VALUES (?, ?, ?, ?, ?, ?, ?)",
        [$name, $email, Auth::hashPassword($password), $company, $phone, $plan, $maxViews]
    );

    Response::success(['id' => $clientId, 'email' => $email], 'Client created', 201);
}

function getClient($db, $id) {
    $client = $db->queryOne(
        "SELECT id, name, email, company, phone, plan_tier, max_views_per_month, is_active, 
                created_at, last_login FROM clients WHERE id = ?",
        [$id]
    );
    if (!$client) Response::error('Client not found', 404);

    $client['projects'] = $db->query(
        "SELECT p.id, p.name, p.api_key, p.domain, p.is_active, p.created_at,
                COUNT(s.id) as total_sessions
         FROM projects p
         LEFT JOIN sessions s ON p.id = s.project_id AND s.started_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
         WHERE p.client_id = ?
         GROUP BY p.id",
        [$id]
    );

    Response::success($client);
}

function updateClient($db, $id) {
    $input = json_decode(file_get_contents('php://input'), true);
    
    $fields = [];
    $params = [];

    if (isset($input['name'])) { $fields[] = "name = ?"; $params[] = trim($input['name']); }
    if (isset($input['email'])) { $fields[] = "email = ?"; $params[] = trim($input['email']); }
    if (isset($input['company'])) { $fields[] = "company = ?"; $params[] = trim($input['company']); }
    if (isset($input['phone'])) { $fields[] = "phone = ?"; $params[] = trim($input['phone']); }
    if (isset($input['plan_tier'])) { $fields[] = "plan_tier = ?"; $params[] = $input['plan_tier']; }
    if (isset($input['is_active'])) { $fields[] = "is_active = ?"; $params[] = $input['is_active'] ? 1 : 0; }
    if (isset($input['password']) && !empty($input['password'])) {
        $fields[] = "password_hash = ?";
        $params[] = Auth::hashPassword($input['password']);
    }

    if (empty($fields)) {
        Response::error('No fields to update', 400);
    }

    $params[] = $id;
    $db->execute("UPDATE clients SET " . implode(', ', $fields) . " WHERE id = ?", $params);

    Response::success(null, 'Client updated');
}

function deleteClient($db, $id) {
    $db->execute("DELETE FROM clients WHERE id = ?", [$id]);
    Response::success(null, 'Client deleted');
}

// ═════════════════════════════════════════
// Project Functions
// ═════════════════════════════════════════

function listProjects($db) {
    $clientId = intval($_GET['client_id'] ?? 0);
    
    $sql = "SELECT p.id, p.name, p.api_key, p.domain, p.is_active, p.created_at, p.client_id,
                   c.name as client_name, COUNT(s.id) as total_sessions
            FROM projects p
            JOIN clients c ON p.client_id = c.id
            LEFT JOIN sessions s ON p.id = s.project_id AND s.started_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)";
    
    $params = [];
    if ($clientId > 0) {
        $sql .= " WHERE p.client_id = ?";
        $params[] = $clientId;
    }
    
    $sql .= " GROUP BY p.id ORDER BY p.created_at DESC";
    
    Response::success($db->query($sql, $params));
}

function createProject($db) {
    $input = json_decode(file_get_contents('php://input'), true);
    
    $clientId = intval($input['client_id'] ?? 0);
    $name = trim($input['name'] ?? '');
    $domain = trim($input['domain'] ?? '');
    $description = trim($input['description'] ?? '');

    if ($clientId === 0 || empty($name)) {
        Response::error('client_id and name are required', 400);
    }

    // Verify client exists
    $client = $db->queryOne("SELECT id FROM clients WHERE id = ?", [$clientId]);
    if (!$client) Response::error('Client not found', 404);

    $apiKey = Auth::generateApiKey();

    $projectId = $db->insert(
        "INSERT INTO projects (client_id, name, api_key, domain, description) VALUES (?, ?, ?, ?, ?)",
        [$clientId, $name, $apiKey, $domain, $description]
    );

    Response::success([
        'id' => $projectId,
        'api_key' => $apiKey,
        'name' => $name,
    ], 'Project created', 201);
}

function getProject($db, $id) {
    $project = $db->queryOne(
        "SELECT p.*, c.name as client_name FROM projects p 
         JOIN clients c ON p.client_id = c.id WHERE p.id = ?",
        [$id]
    );
    if (!$project) Response::error('Project not found', 404);
    Response::success($project);
}

function updateProject($db, $id) {
    $input = json_decode(file_get_contents('php://input'), true);
    
    $fields = [];
    $params = [];

    if (isset($input['name'])) { $fields[] = "name = ?"; $params[] = trim($input['name']); }
    if (isset($input['domain'])) { $fields[] = "domain = ?"; $params[] = trim($input['domain']); }
    if (isset($input['description'])) { $fields[] = "description = ?"; $params[] = trim($input['description']); }
    if (isset($input['is_active'])) { $fields[] = "is_active = ?"; $params[] = $input['is_active'] ? 1 : 0; }

    if (empty($fields)) Response::error('No fields to update', 400);

    $params[] = $id;
    $db->execute("UPDATE projects SET " . implode(', ', $fields) . " WHERE id = ?", $params);
    Response::success(null, 'Project updated');
}

function deleteProject($db, $id) {
    $db->execute("DELETE FROM projects WHERE id = ?", [$id]);
    Response::success(null, 'Project deleted');
}

// ═════════════════════════════════════════
// Admin Overview
// ═════════════════════════════════════════

function adminOverview($db) {
    $totalClients = $db->scalar("SELECT COUNT(*) FROM clients");
    $activeClients = $db->scalar("SELECT COUNT(*) FROM clients WHERE is_active = 1");
    $totalProjects = $db->scalar("SELECT COUNT(*) FROM projects");
    
    $totalViews30d = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE started_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)"
    );
    $totalViewsToday = $db->scalar(
        "SELECT COUNT(*) FROM sessions WHERE started_at >= CURDATE()"
    );
    $totalEvents30d = $db->scalar(
        "SELECT COUNT(*) FROM events WHERE created_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)"
    );

    // Top clients by views (last 30 days)
    $topClients = $db->query(
        "SELECT c.id, c.name, c.company, c.plan_tier, COUNT(s.id) as views
         FROM clients c
         JOIN projects p ON c.id = p.client_id
         JOIN sessions s ON p.id = s.project_id AND s.started_at >= DATE_SUB(NOW(), INTERVAL 30 DAY)
         GROUP BY c.id ORDER BY views DESC LIMIT 10"
    );

    // Plan distribution
    $planDist = $db->query(
        "SELECT plan_tier as plan, COUNT(*) as count FROM clients GROUP BY plan_tier"
    );

    Response::success([
        'total_clients' => intval($totalClients),
        'active_clients' => intval($activeClients),
        'total_projects' => intval($totalProjects),
        'views_30d' => intval($totalViews30d),
        'views_today' => intval($totalViewsToday),
        'events_30d' => intval($totalEvents30d),
        'top_clients' => $topClients,
        'plan_distribution' => $planDist,
    ]);
}
