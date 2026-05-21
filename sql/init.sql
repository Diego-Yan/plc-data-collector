-- ============================================================================
-- TAG: one-click-deploy — 2026-05-20
-- Database initialization: creates the plc_data database, TimescaleDB extension,
-- and configuration metadata tables.
-- ============================================================================

-- Connect to the target database first, then run this script:
--   psql -U postgres -d plc_data -f init.sql

-- Enable TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- ============================================================================
-- Configuration tables (metadata, shared across all devices)
-- ============================================================================

CREATE TABLE IF NOT EXISTS sys_config (
    key   VARCHAR(128) PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS logs (
    id      BIGSERIAL PRIMARY KEY,
    time    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    level   VARCHAR(16) NOT NULL,
    message TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_logs_time ON logs (time DESC);
CREATE INDEX IF NOT EXISTS idx_logs_level ON logs (level);

-- ============================================================================
-- Done
-- ============================================================================
-- Note: t_data_{deviceId} (TimescaleDB hypertables) are created automatically
--       by TimeSeriesService.EnsureTableAsync() at runtime.
--       r_data_{deviceId} (relational wide tables) are created automatically
--       by ForwardService.EnsureWideTable() at runtime.
-- ============================================================================
