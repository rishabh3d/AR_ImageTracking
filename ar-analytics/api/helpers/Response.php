<?php
/**
 * Response Helper — JSON response utilities
 */

class Response {

    /**
     * Send a JSON success response
     */
    public static function success($data = null, $message = 'OK', $code = 200) {
        http_response_code($code);
        echo json_encode([
            'success' => true,
            'message' => $message,
            'data' => $data
        ], JSON_UNESCAPED_UNICODE);
        exit;
    }

    /**
     * Send a JSON error response
     */
    public static function error($message = 'Error', $code = 400, $details = null) {
        http_response_code($code);
        $response = [
            'success' => false,
            'message' => $message,
        ];
        if ($details !== null) {
            $response['details'] = $details;
        }
        echo json_encode($response, JSON_UNESCAPED_UNICODE);
        exit;
    }

    /**
     * Set CORS headers for cross-origin tracker requests
     */
    public static function cors() {
        header('Access-Control-Allow-Origin: *');
        header('Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS');
        header('Access-Control-Allow-Headers: Content-Type, Authorization, X-API-Key');
        header('Access-Control-Max-Age: 86400');
        
        // Handle preflight
        if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
            http_response_code(204);
            exit;
        }
    }

    /**
     * Set JSON content type
     */
    public static function json() {
        header('Content-Type: application/json; charset=utf-8');
    }
}
