const { calculateDifficultyFromMistakes } = require('../src/services/difficultyService');

describe('Adaptive Difficulty Service (Last 5 Sessions)', () => {
  test('returns normal default when trainee has no previous sessions', () => {
    const result = calculateDifficultyFromMistakes([]);
    expect(result.tier).toBe('normal');
    expect(result.difficultyMultiplier).toBe(1.0);
    expect(result.extraHints).toBe(false);
    expect(result.sampleSize).toBe(0);
  });

  test('returns harder when avg mistakes <= 2', () => {
    const result = calculateDifficultyFromMistakes([1, 2, 1, 0, 2]);
    expect(result.avgMistakes).toBe(1.2);
    expect(result.tier).toBe('harder');
    expect(result.difficultyMultiplier).toBeGreaterThan(1.0);
    expect(result.extraHints).toBe(false);
  });

  test('returns same (normal) when avg mistakes is between 3 and 5', () => {
    const result = calculateDifficultyFromMistakes([4, 5, 3, 4, 3]);
    expect(result.avgMistakes).toBe(3.8);
    expect(result.tier).toBe('normal');
    expect(result.difficultyMultiplier).toBe(1.0);
    expect(result.extraHints).toBe(false);
  });

  test('returns easier with more hints when avg mistakes > 5', () => {
    const result = calculateDifficultyFromMistakes([6, 8, 7, 7, 6]);
    expect(result.avgMistakes).toBe(6.8);
    expect(result.tier).toBe('easier');
    expect(result.difficultyMultiplier).toBeLessThan(1.0);
    expect(result.extraHints).toBe(true);
  });

  test('evaluates only the latest 5 mistake counts when more than 5 are provided', () => {
    const result = calculateDifficultyFromMistakes([0, 1, 1, 2, 1, 99, 99]);
    expect(result.sampleSize).toBe(5);
    expect(result.tier).toBe('harder');
  });
});
