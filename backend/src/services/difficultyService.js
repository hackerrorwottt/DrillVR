/**
 * Computes adaptive difficulty from the trainee's last up to 5 sessions:
 *   avg mistakes <= 2   -> harder (1.35x smoke spread, normal hints)
 *   2 < avg <= 5        -> same   (1.00x smoke spread, normal hints)
 *   avg > 5             -> easier (0.70x smoke spread, extra hints enabled)
 */
function calculateDifficultyFromMistakes(mistakeCounts) {
  if (!Array.isArray(mistakeCounts) || mistakeCounts.length === 0) {
    return {
      tier: 'normal',
      difficultyMultiplier: 1.0,
      extraHints: false,
      avgMistakes: 0,
      sampleSize: 0,
    };
  }

  const recent = mistakeCounts.slice(0, 5);
  const sum = recent.reduce((acc, val) => acc + Number(val || 0), 0);
  const avgMistakes = Math.round((sum / recent.length) * 100) / 100;

  if (avgMistakes <= 2) {
    return {
      tier: 'harder',
      difficultyMultiplier: 1.35,
      extraHints: false,
      avgMistakes,
      sampleSize: recent.length,
    };
  }

  if (avgMistakes <= 5) {
    return {
      tier: 'normal',
      difficultyMultiplier: 1.0,
      extraHints: false,
      avgMistakes,
      sampleSize: recent.length,
    };
  }

  return {
    tier: 'easier',
    difficultyMultiplier: 0.7,
    extraHints: true,
    avgMistakes,
    sampleSize: recent.length,
  };
}

async function getUserAdaptiveDifficulty(pool, userId) {
  const [rows] = await pool.execute(
    `SELECT total_mistakes
     FROM sessions
     WHERE user_id = ?
     ORDER BY completed_at DESC, id DESC
     LIMIT 5`,
    [userId]
  );

  const counts = rows.map((r) => r.total_mistakes);
  return calculateDifficultyFromMistakes(counts);
}

module.exports = { calculateDifficultyFromMistakes, getUserAdaptiveDifficulty };
