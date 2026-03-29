<?php
/**
 * AR Analytics — One-Time Install Script
 * 
 * 1. Upload the entire ar-analytics/ folder to your Hostinger server
 * 2. Update api/config.php with your database credentials
 * 3. Visit: https://yoursite.com/ar-analytics/install.php
 * 4. DELETE this file after installation!
 */

require_once __DIR__ . '/api/config.php';

$step = $_GET['step'] ?? 'form';
$message = '';
$error = '';

// Handle form submission
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $adminUser = trim($_POST['admin_username'] ?? 'admin');
    $adminPass = $_POST['admin_password'] ?? '';
    $adminEmail = trim($_POST['admin_email'] ?? '');
    
    if (empty($adminPass) || strlen($adminPass) < 8) {
        $error = 'Password must be at least 8 characters';
    } else {
        try {
            // Connect to database
            $pdo = new PDO(
                "mysql:host=" . DB_HOST . ";charset=" . DB_CHARSET,
                DB_USER, DB_PASS,
                [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
            );

            // Create database
            $pdo->exec("CREATE DATABASE IF NOT EXISTS `" . DB_NAME . "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            $pdo->exec("USE `" . DB_NAME . "`");

            // Read and execute schema
            $schema = file_get_contents(__DIR__ . '/sql/schema.sql');
            // Remove the CREATE DATABASE and USE lines (we already did that)
            $schema = preg_replace('/CREATE DATABASE.*?;/s', '', $schema);
            $schema = preg_replace('/USE.*?;/s', '', $schema);
            // Remove the default INSERT (we'll add our own)
            $schema = preg_replace('/INSERT INTO admins.*?;/s', '', $schema);
            
            // Execute each statement
            $statements = array_filter(array_map('trim', explode(';', $schema)));
            foreach ($statements as $stmt) {
                if (!empty($stmt) && $stmt !== '') {
                    $pdo->exec($stmt);
                }
            }

            // Insert admin user
            $hash = password_hash($adminPass, PASSWORD_BCRYPT, ['cost' => 10]);
            $stmt = $pdo->prepare("INSERT INTO admins (username, password_hash, email) VALUES (?, ?, ?) 
                                   ON DUPLICATE KEY UPDATE password_hash = ?, email = ?");
            $stmt->execute([$adminUser, $hash, $adminEmail, $hash, $adminEmail]);

            $step = 'success';

        } catch (PDOException $e) {
            $error = 'Database error: ' . $e->getMessage();
        }
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Install — AR Analytics</title>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700;800&display=swap" rel="stylesheet">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Inter', sans-serif;
            background: #0a0b0f;
            color: #f0f2f5;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .container {
            background: #161920;
            border: 1px solid #1e2230;
            border-radius: 16px;
            padding: 48px 40px;
            width: 100%;
            max-width: 520px;
        }
        h1 { font-size: 28px; font-weight: 800; text-align: center; margin-bottom: 8px; }
        h1 span { color: #00e68a; }
        .sub { text-align: center; color: #8b92a5; margin-bottom: 32px; font-size: 14px; }
        .form-group { margin-bottom: 20px; }
        .form-group label { display: block; font-size: 13px; font-weight: 500; color: #8b92a5; margin-bottom: 8px; }
        .form-group input {
            width: 100%; padding: 12px 16px; background: #111318; border: 1px solid #1e2230;
            border-radius: 10px; color: #f0f2f5; font-size: 14px; font-family: inherit; outline: none;
        }
        .form-group input:focus { border-color: #00e68a; }
        .btn {
            width: 100%; padding: 14px; background: linear-gradient(135deg, #00e68a, #00c07a);
            color: #000; border: none; border-radius: 10px; font-size: 15px; font-weight: 600;
            font-family: inherit; cursor: pointer;
        }
        .btn:hover { transform: translateY(-1px); box-shadow: 0 8px 24px rgba(0,230,138,0.3); }
        .error { color: #ef4444; font-size: 13px; text-align: center; margin-bottom: 16px; padding: 12px; background: rgba(239,68,68,0.1); border-radius: 10px; }
        .success-box { text-align: center; }
        .success-box .check { font-size: 64px; margin-bottom: 16px; }
        .success-box h2 { font-size: 24px; margin-bottom: 16px; color: #00e68a; }
        .success-box p { color: #8b92a5; margin-bottom: 12px; font-size: 14px; }
        .success-box a { color: #00d4ff; text-decoration: none; }
        .success-box a:hover { text-decoration: underline; }
        .warning { background: rgba(245,158,11,0.1); border: 1px solid rgba(245,158,11,0.3); border-radius: 10px; padding: 16px; margin-top: 20px; }
        .warning strong { color: #f59e0b; }
        .warning p { color: #8b92a5; font-size: 13px; margin-top: 4px; }
        .steps { margin: 20px 0; }
        .step { display: flex; gap: 12px; align-items: flex-start; margin-bottom: 12px; }
        .step-num { background: #00e68a; color: #000; width: 24px; height: 24px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; flex-shrink: 0; }
        .step-text { font-size: 14px; color: #8b92a5; }
        .step-text code { background: #111318; padding: 2px 6px; border-radius: 4px; color: #00d4ff; font-size: 12px; }
    </style>
</head>
<body>
<div class="container">
<?php if ($step === 'form'): ?>
    <h1>Install <span>AR Analytics</span></h1>
    <p class="sub">Set up your analytics platform</p>

    <?php if ($error): ?>
        <div class="error"><?= htmlspecialchars($error) ?></div>
    <?php endif; ?>

    <p class="sub" style="margin-bottom:16px;font-size:13px">Make sure you've updated <code style="background:#111318;padding:2px 6px;border-radius:4px;color:#00d4ff">api/config.php</code> with your Hostinger database credentials first.</p>

    <form method="POST">
        <div class="form-group">
            <label>Admin Username</label>
            <input type="text" name="admin_username" value="admin" required>
        </div>
        <div class="form-group">
            <label>Admin Password (min 8 chars)</label>
            <input type="password" name="admin_password" required minlength="8" placeholder="Choose a strong password">
        </div>
        <div class="form-group">
            <label>Admin Email</label>
            <input type="email" name="admin_email" required placeholder="your@email.com">
        </div>
        <button type="submit" class="btn">🚀 Install Now</button>
    </form>

<?php elseif ($step === 'success'): ?>
    <div class="success-box">
        <div class="check">✅</div>
        <h2>Installation Complete!</h2>
        <p>Your AR Analytics platform is ready to use.</p>

        <div class="steps">
            <div class="step">
                <div class="step-num">1</div>
                <div class="step-text">Open <a href="admin/">Admin Panel</a> to create clients & projects</div>
            </div>
            <div class="step">
                <div class="step-num">2</div>
                <div class="step-text">Copy the tracker <code>&lt;script&gt;</code> tag for each project</div>
            </div>
            <div class="step">
                <div class="step-num">3</div>
                <div class="step-text">Add it to your AR build's <code>index.html</code></div>
            </div>
            <div class="step">
                <div class="step-num">4</div>
                <div class="step-text">Share <a href="dashboard/">Dashboard</a> login with clients</div>
            </div>
        </div>

        <div class="warning">
            <strong>⚠️ Security: Delete this file!</strong>
            <p>Delete <code>install.php</code> from your server now to prevent unauthorized re-installation.</p>
        </div>
    </div>
<?php endif; ?>
</div>
</body>
</html>
