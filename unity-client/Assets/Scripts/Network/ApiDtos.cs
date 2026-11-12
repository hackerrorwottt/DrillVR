using System;
using System.Collections.Generic;

namespace DrillVR.Network
{
    [Serializable]
    public class ApiErrorEnvelope
    {
        public ApiErrorDetail error;
    }

    [Serializable]
    public class ApiErrorDetail
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class RegisterRequest
    {
        public string name;
        public string email;
        public string password;
    }

    [Serializable]
    public class LoginRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class UserDto
    {
        public int id;
        public string name;
        public string email;
        public string role;
    }

    [Serializable]
    public class AuthResponse
    {
        public string token;
        public UserDto user;
    }

    [Serializable]
    public class ScenarioDto
    {
        public int id;
        public string code;
        public string title;
        public string description;
        public float base_smoke_growth_rate;
        public int time_limit_seconds;
        public int max_casualties_lost;
        public int is_active;
    }

    [Serializable]
    public class ScenariosListResponse
    {
        public List<ScenarioDto> scenarios = new List<ScenarioDto>();
    }

    [Serializable]
    public class ScenarioStepDto
    {
        public int id;
        public int scenario_id;
        public int step_number;
        public string step_key;
        public string title;
        public string instruction;
        public string default_hint;
        public string mistake_hint;
        public int expected_time_seconds;
        public float weight_percent;
    }

    [Serializable]
    public class ScenarioWithStepsResponse
    {
        public ScenarioDto scenario;
        public List<ScenarioStepDto> steps = new List<ScenarioStepDto>();
    }

    [Serializable]
    public class DifficultyResponse
    {
        public int userId;
        public string tier;
        public float difficultyMultiplier;
        public bool extraHints;
        public float avgMistakes;
        public int sampleSize;
    }

    [Serializable]
    public class StepLogUploadDto
    {
        public string stepKey;
        public float timeSpentSeconds;
        public int mistakesInStep;
        public bool completed;
    }

    [Serializable]
    public class SaveSessionRequest
    {
        public string scenarioCode;
        public float difficultyMultiplier;
        public float totalScore;
        public float stepsScore;
        public float evacuationScore;
        public float timeBonusScore;
        public float mistakePenalty;
        public float totalTimeSeconds;
        public int totalMistakes;
        public int casualtiesEvacuated;
        public int casualtiesTotal;
        public bool passed;
        public string weakestStepKey;
        public List<StepLogUploadDto> stepLogs = new List<StepLogUploadDto>();
    }

    [Serializable]
    public class SaveSessionResponse
    {
        public int sessionId;
        public int userId;
        public int scenarioId;
        public float totalScore;
        public bool passed;
    }

    [Serializable]
    public class PendingSessionQueueWrapper
    {
        public List<SaveSessionRequest> items = new List<SaveSessionRequest>();
    }
}
