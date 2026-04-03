<?php
/**
 * Auth Controller — Login/logout for clients and admin
 */

require_once __DIR__ . '/../helpers/Database.php';
require_once __DIR__ . '/../helpers/Auth.php';
require_once __DIR__ . '/../helpers/Response.php';

Response::cors();
Response::json();

$method = $_SERVER['REQUEST_METHOD'];
$action = $_GET['action'] ?? '';

switch ($action) {
    case 'login':
        if ($method !== 'POST') Response::error('Method not allowed', 405);
        handleLogin();
        break;
    case 'admin-login':
        if ($method !== 'POST') Response::error('Method not allowed', 405);
        handleAdminLogin();
        break;
    case 'me':
        if ($method !== 'GET') Response::error('Method not allowed', 405);
        handleMe();
        break;
    case 'change-password':
        if ($method !== 'POST') Response::error('Method not allowed', 405);
        handleChangePassword();
        break;
    default:
        Response::error('Unknown action', 400);
}

/**
 * Client login
 */
function handleLogin() {
    $input = json_decode(file_get_contents('php://input'), true);
    $email = trim($input['email'] ?? '');
    $password = $input['password'] ?? '';

    if (empty($email) || empty($password)) {
        Response::error('Email and password are required', 400);
    }

    $db = Database::getInstance();
    $client = $db->queryOne(
        "SELECT id, name, email, password_hash, company, plan_tier, is_active FROM clients WHERE email = ?",
        [$email]
    );

    if (!$client || !Auth::verifyPassword($password, $client['password_hash'])) {
        Response::error('Invalid email or password', 401);
    }

    if (!$client['is_active']) {
        Response::error('Account is deactivated. Contact support.', 403);
    }

    // Update last login
    $db->execute("UPDATE clients SET last_login = NOW() WHERE id = ?", [$client['id']]);

    // Generate token
    $token = Auth::generateToken($client['id'], 'client');

    // Get client's projects
    $projects = $db->query(
        "SELECT id, name, api_key, domain, is_active FROM projects WHERE client_id = ?",
        [$client['id']]
    );

    Response::success([
        'token' => $token,
        'user' => [
            'id' => $client['id'],
            'name' => $client['name'],
            'email' => $client['email'],
            'company' => $client['company'],
            'plan' => $client['plan_tier'],
        ],
        'projects' => $projects,
    ]);
}

/**
 * Admin login
 */
function handleAdminLogin() {
    $input = json_decode(file_get_contents('php://input'), true);
    $username = trim($input['username'] ?? '');
    $password = $input['password'] ?? '';

    if (empty($username) || empty($password)) {
        Response::error('Username and password are required', 400);
    }

    $db = Database::getInstance();
    $admin = $db->queryOne(
        "SELECT id, username, password_hash FROM admins WHERE username = ?",
        [$username]
    );

    if (!$admin || !Auth::verifyPassword($password, $admin['password_hash'])) {
        Response::error('Invalid credentials', 401);
    }

    $token = Auth::generateToken($admin['id'], 'admin');

    Response::success([
        'token' => $token,
        'user' => [
            'id' => $admin['id'],
            'username' => $admin['username'],
            'role' => 'admin',
        ],
    ]);
}

/**
 * Get current user info
 */
function handleMe() {
    $user = Auth::requireAuth();
    $db = Database::getInstance();

    if ($user['role'] === 'admin') {
        $admin = $db->queryOne("SELECT id, username, email FROM admins WHERE id = ?", [$user['user_id']]);
        $admin['role'] = 'admin';
        Response::success($admin);
    } else {
        $client = $db->queryOne(
            "SELECT id, name, email, company, plan_tier FROM clients WHERE id = ?",
            [$user['user_id']]
        );
        $client['role'] = 'client';
        $client['projects'] = $db->query(
            "SELECT id, name, api_key, domain FROM projects WHERE client_id = ? AND is_active = 1",
            [$user['user_id']]
        );
        Response::success($client);
    }
}

/**
 * Change password
 */
function handleChangePassword() {
    $user = Auth::requireAuth();
    $input = json_decode(file_get_contents('php://input'), true);
    
    $currentPassword = $input['current_password'] ?? '';
    $newPassword = $input['new_password'] ?? '';

    if (empty($currentPassword) || empty($newPassword)) {
        Response::error('Current and new passwords are required', 400);
    }

    if (strlen($newPassword) < 8) {
        Response::error('New password must be at least 8 characters', 400);
    }

    $db = Database::getInstance();
    
    if ($user['role'] === 'admin') {
        $record = $db->queryOne("SELECT password_hash FROM admins WHERE id = ?", [$user['user_id']]);
        if (!Auth::verifyPassword($currentPassword, $record['password_hash'])) {
            Response::error('Current password is incorrect', 401);
        }
        $db->execute("UPDATE admins SET password_hash = ?, password_plain = ? WHERE id = ?", 
            [Auth::hashPassword($newPassword), $newPassword, $user['user_id']]);
    } else {
        $record = $db->queryOne("SELECT password_hash FROM clients WHERE id = ?", [$user['user_id']]);
        if (!Auth::verifyPassword($currentPassword, $record['password_hash'])) {
            Response::error('Current password is incorrect', 401);
        }
        $db->execute("UPDATE clients SET password_hash = ?, password_plain = ? WHERE id = ?", 
            [Auth::hashPassword($newPassword), $newPassword, $user['user_id']]);
    }

    Response::success(null, 'Password updated');
}
