-- ============================================
-- AR Analytics Platform — Database Schema
-- Run this once on your Hostinger MySQL database
-- ============================================

CREATE DATABASE IF NOT EXISTS ar_analytics
    CHARACTER SET utf8mb4 
    COLLATE utf8mb4_unicode_ci;

USE ar_analytics;

-- ─── Admin Users (you) ───
CREATE TABLE IF NOT EXISTS admins (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- ─── Client Accounts ───
CREATE TABLE IF NOT EXISTS clients (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    password_plain VARCHAR(100), -- Admin can view original password
    company VARCHAR(150),
    phone VARCHAR(20),
    plan_tier ENUM('free', 'pro', 'business') DEFAULT 'free',
    max_views_per_month INT DEFAULT 1000,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP NULL,
    INDEX idx_email (email),
    INDEX idx_active (is_active)
) ENGINE=InnoDB;

-- ─── AR Projects (each client can have many) ───
CREATE TABLE IF NOT EXISTS projects (
    id INT AUTO_INCREMENT PRIMARY KEY,
    client_id INT NOT NULL,
    name VARCHAR(100) NOT NULL,
    api_key VARCHAR(64) UNIQUE NOT NULL,
    domain VARCHAR(255) DEFAULT NULL,
    description TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE,
    INDEX idx_api_key (api_key),
    INDEX idx_client (client_id)
) ENGINE=InnoDB;

-- ─── Visitor Sessions ───
CREATE TABLE IF NOT EXISTS sessions (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    project_id INT NOT NULL,
    session_id VARCHAR(64) NOT NULL,
    visitor_id VARCHAR(64) NOT NULL,
    is_new_visitor BOOLEAN DEFAULT TRUE,
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_active_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    duration_seconds INT DEFAULT 0,
    device_type VARCHAR(20),
    os VARCHAR(50),
    browser VARCHAR(50),
    country VARCHAR(100),
    city VARCHAR(100),
    language VARCHAR(20),
    screen_width INT,
    screen_height INT,
    referrer VARCHAR(500),
    utm_source VARCHAR(100),
    utm_medium VARCHAR(100),
    utm_campaign VARCHAR(100),
    page_url VARCHAR(500),
    ip_address VARCHAR(45),
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_project_session (project_id, session_id),
    INDEX idx_project_started (project_id, started_at),
    INDEX idx_visitor (project_id, visitor_id),
    INDEX idx_date (started_at)
) ENGINE=InnoDB;

-- ─── Analytics Events ───
CREATE TABLE IF NOT EXISTS events (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    project_id INT NOT NULL,
    session_id VARCHAR(64) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    event_data JSON,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_project_event (project_id, event_type),
    INDEX idx_project_created (project_id, created_at),
    INDEX idx_session (session_id),
    INDEX idx_type_date (event_type, created_at)
) ENGINE=InnoDB;

-- ─── Daily Aggregates (for fast dashboard queries) ───
CREATE TABLE IF NOT EXISTS daily_stats (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    project_id INT NOT NULL,
    stat_date DATE NOT NULL,
    total_sessions INT DEFAULT 0,
    unique_visitors INT DEFAULT 0,
    new_visitors INT DEFAULT 0,
    returning_visitors INT DEFAULT 0,
    total_events INT DEFAULT 0,
    ar_activations INT DEFAULT 0,
    ar_scans INT DEFAULT 0,
    avg_session_duration INT DEFAULT 0,
    mobile_count INT DEFAULT 0,
    desktop_count INT DEFAULT 0,
    tablet_count INT DEFAULT 0,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    UNIQUE KEY uk_project_date (project_id, stat_date),
    INDEX idx_date (stat_date)
) ENGINE=InnoDB;

-- ─── Insert default admin ───
-- Password: admin123 (CHANGE THIS after first login!)
INSERT INTO admins (username, password_hash, email) VALUES
('admin', '$2y$10$YourHashWillBeGeneratedByInstallScript', 'admin@yoursite.com');
