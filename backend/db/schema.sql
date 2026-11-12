-- DrillVR MySQL 8 Schema (3NF, Foreign Keys, Indexes)

CREATE DATABASE IF NOT EXISTS drillvr
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE drillvr;

CREATE TABLE IF NOT EXISTS users (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  name VARCHAR(100) NOT NULL,
  email VARCHAR(160) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  role ENUM('trainee', 'instructor', 'admin') NOT NULL DEFAULT 'trainee',
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uk_users_email (email),
  KEY idx_users_role (role)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS scenarios (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  code VARCHAR(80) NOT NULL,
  title VARCHAR(150) NOT NULL,
  description TEXT NOT NULL,
  base_smoke_growth_rate DECIMAL(6,3) NOT NULL DEFAULT 0.250,
  time_limit_seconds INT UNSIGNED NOT NULL DEFAULT 300,
  max_casualties_lost TINYINT UNSIGNED NOT NULL DEFAULT 1,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uk_scenarios_code (code),
  KEY idx_scenarios_active (is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS scenario_steps (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  scenario_id BIGINT UNSIGNED NOT NULL,
  step_number TINYINT UNSIGNED NOT NULL,
  step_key VARCHAR(64) NOT NULL,
  title VARCHAR(120) NOT NULL,
  instruction TEXT NOT NULL,
  default_hint TEXT NOT NULL,
  mistake_hint TEXT NOT NULL,
  expected_time_seconds INT UNSIGNED NOT NULL DEFAULT 30,
  weight_percent DECIMAL(5,2) NOT NULL DEFAULT 15.00,
  PRIMARY KEY (id),
  UNIQUE KEY uk_scenario_step_num (scenario_id, step_number),
  UNIQUE KEY uk_scenario_step_key (scenario_id, step_key),
  CONSTRAINT fk_steps_scenario
    FOREIGN KEY (scenario_id) REFERENCES scenarios (id)
    ON DELETE CASCADE
    ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS sessions (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NOT NULL,
  scenario_id BIGINT UNSIGNED NOT NULL,
  difficulty_multiplier DECIMAL(4,2) NOT NULL DEFAULT 1.00,
  total_score DECIMAL(5,2) NOT NULL,
  steps_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  evacuation_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  time_bonus_score DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  mistake_penalty DECIMAL(5,2) NOT NULL DEFAULT 0.00,
  total_time_seconds DECIMAL(7,2) NOT NULL,
  total_mistakes SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  casualties_evacuated TINYINT UNSIGNED NOT NULL DEFAULT 0,
  casualties_total TINYINT UNSIGNED NOT NULL DEFAULT 0,
  passed BOOLEAN NOT NULL DEFAULT FALSE,
  weakest_step_key VARCHAR(64) NULL,
  completed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_sessions_user_completed (user_id, completed_at DESC),
  KEY idx_sessions_scenario_score (scenario_id, total_score DESC),
  CONSTRAINT fk_sessions_user
    FOREIGN KEY (user_id) REFERENCES users (id)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT fk_sessions_scenario
    FOREIGN KEY (scenario_id) REFERENCES scenarios (id)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS session_step_logs (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id BIGINT UNSIGNED NOT NULL,
  step_id BIGINT UNSIGNED NOT NULL,
  time_spent_seconds DECIMAL(7,2) NOT NULL DEFAULT 0.00,
  mistakes_in_step SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  completed BOOLEAN NOT NULL DEFAULT FALSE,
  PRIMARY KEY (id),
  UNIQUE KEY uk_session_step (session_id, step_id),
  KEY idx_logs_step_mistakes (step_id, mistakes_in_step),
  CONSTRAINT fk_logs_session
    FOREIGN KEY (session_id) REFERENCES sessions (id)
    ON DELETE CASCADE
    ON UPDATE CASCADE,
  CONSTRAINT fk_logs_step
    FOREIGN KEY (step_id) REFERENCES scenario_steps (id)
    ON DELETE RESTRICT
    ON UPDATE CASCADE
) ENGINE=InnoDB;
