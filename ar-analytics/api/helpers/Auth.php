<?php
/**
 * Auth Helper — Session & token management
 */

require_once __DIR__ . '/Database.php';
require_once __DIR__ . '/../config.php';

class Auth {

    /**
     * Generate a secure API key for projects
     */
    public static function generateApiKey() {
        return bin2hex(random_bytes(32));
    }

    /**
     * Generate a secure auth token
     */
    public static function generateToken($userId, $role = 'client') {
        $payload = json_encode([
            'user_id' => $userId,
            'role' => $role,
            'exp' => time() + TOKEN_LIFETIME,
            'iat' => time(),
        ]);
        $signature = hash_hmac('sha256', $payload, APP_SECRET);
        return base64_encode($payload) . '.' . $signature;
    }

    /**
     * Verify and decode an auth token
     */
    public static function verifyToken($token) {
        $parts = explode('.', $token);
        if (count($parts) !== 2) return null;

        $payload = base64_decode($parts[0]);
        $signature = $parts[1];

        $expectedSig = hash_hmac('sha256', $payload, APP_SECRET);
        if (!hash_equals($expectedSig, $signature)) return null;

        $data = json_decode($payload, true);
        if (!$data || $data['exp'] < time()) return null;

        return $data;
    }

    /**
     * Get the authenticated user from the Authorization header
     * Returns ['user_id' => ..., 'role' => ...] or null
     */
    public static function getUser() {
        // Try multiple ways to get the header because shared hosts strip it
        $header = $_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '';
        
        if (empty($header)) {
            // Also check for token in browser cookie
            $header = $_COOKIE['ar_analytics_token'] ?? '';
        }
        
        // Remove 'Bearer ' prefix if present
        $token = str_replace('Bearer ', '', $header);

        if (empty($token)) return null;

        return self::verifyToken($token);
    }

    /**
     * Require authentication — exits with 401 if not authenticated
     */
    public static function requireAuth($requiredRole = null) {
        $user = self::getUser();
        if (!$user) {
            Response::error('Unauthorized', 401);
        }
        if ($requiredRole && $user['role'] !== $requiredRole) {
            Response::error('Forbidden', 403);
        }
        return $user;
    }

    /**
     * Hash a password
     */
    public static function hashPassword($password) {
        return password_hash($password, PASSWORD_BCRYPT, ['cost' => 10]);
    }

    /**
     * Verify a password against a hash
     */
    public static function verifyPassword($password, $hash) {
        return password_verify($password, $hash);
    }

    /**
     * Validate an API key and return the project
     */
    public static function validateApiKey($apiKey) {
        $db = Database::getInstance();
        $project = $db->queryOne(
            "SELECT p.*, c.plan_tier, c.is_active as client_active 
             FROM projects p 
             JOIN clients c ON p.client_id = c.id 
             WHERE p.api_key = ? AND p.is_active = 1",
            [$apiKey]
        );
        
        if (!$project || !$project['client_active']) {
            return null;
        }
        return $project;
    }
}
