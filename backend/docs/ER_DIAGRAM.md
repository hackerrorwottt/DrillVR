# DrillVR Entity-Relationship Diagram (3NF)

```mermaid
erDiagram
    USERS ||--o{ SESSIONS : completes
    SCENARIOS ||--o{ SCENARIO_STEPS : defines
    SCENARIOS ||--o{ SESSIONS : runs
    SESSIONS ||--o{ SESSION_STEP_LOGS : records
    SCENARIO_STEPS ||--o{ SESSION_STEP_LOGS : tracks

    USERS {
        bigint id PK
        varchar name
        varchar email UK
        varchar password_hash
        enum role "trainee | instructor | admin"
        timestamp created_at
    }

    SCENARIOS {
        bigint id PK
        varchar code UK
        varchar title
        text description
        decimal base_smoke_growth_rate
        int time_limit_seconds
        tinyint max_casualties_lost
        boolean is_active
        timestamp created_at
    }

    SCENARIO_STEPS {
        bigint id PK
        bigint scenario_id FK
        tinyint step_number
        varchar step_key
        varchar title
        text instruction
        text default_hint
        text mistake_hint
        int expected_time_seconds
        decimal weight_percent
    }

    SESSIONS {
        bigint id PK
        bigint user_id FK
        bigint scenario_id FK
        decimal difficulty_multiplier
        decimal total_score
        decimal steps_score
        decimal evacuation_score
        decimal time_bonus_score
        decimal mistake_penalty
        decimal total_time_seconds
        smallint total_mistakes
        tinyint casualties_evacuated
        tinyint casualties_total
        boolean passed
        varchar weakest_step_key
        timestamp completed_at
    }

    SESSION_STEP_LOGS {
        bigint id PK
        bigint session_id FK
        bigint step_id FK
        decimal time_spent_seconds
        smallint mistakes_in_step
        boolean completed
    }
```

## 3NF Design Notes
- **1NF:** All columns are atomic; step logs live in `session_step_logs` rather than comma-separated lists on `sessions`.
- **2NF:** Every non-key column depends on the full primary key (`id`) of its respective table; composite uniqueness constraints `(scenario_id, step_number)` and `(session_id, step_id)` prevent duplicate entries.
- **3NF:** Scenario metadata (`title`, `base_smoke_growth_rate`) and step definitions (`title`, `expected_time_seconds`, `weight_percent`) are isolated in `scenarios` and `scenario_steps`, eliminating transitive dependencies inside `sessions` and `session_step_logs`.
- **Indexes:**
  - `idx_sessions_user_completed (user_id, completed_at DESC)` accelerates the last-5-sessions adaptive difficulty query and trainee history.
  - `idx_sessions_scenario_score (scenario_id, total_score DESC)` accelerates leaderboard queries.
  - `idx_logs_step_mistakes (step_id, mistakes_in_step)` accelerates weakest-step analytics for instructors.
