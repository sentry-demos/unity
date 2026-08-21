using System;

/// <summary>
/// XP accumulation and the XP -> level rules. Plain C#: no Unity types, no frame loop, so
/// the milestone math can be tested without a scene.
/// </summary>
public class LevelProgression
{
    private readonly int[] _milestones;

    public LevelProgression(int[] milestones, int startingLevel = 0, float startingXp = 0f)
    {
        if (milestones == null || milestones.Length == 0)
        {
            throw new ArgumentException("At least one milestone is required", nameof(milestones));
        }

        _milestones = milestones;
        CurrentLevel = startingLevel < 0 ? 0 : startingLevel;
        Xp = startingXp;

        // Starting past level 0 is an inspector convenience for testing mid-game state, so
        // the milestone window has to be seeded to match rather than assumed to be the first.
        if (IsMaxLevel)
        {
            _prevLevelXpMilestone = _milestones[_milestones.Length - 1];
            NextLevelXpMilestone = _prevLevelXpMilestone;
        }
        else
        {
            _prevLevelXpMilestone = CurrentLevel == 0 ? 0 : _milestones[CurrentLevel - 1];
            NextLevelXpMilestone = _milestones[CurrentLevel];
        }
    }

    private int _prevLevelXpMilestone;

    public int CurrentLevel { get; private set; }
    public float Xp { get; private set; }
    public int NextLevelXpMilestone { get; private set; }

    /// <summary>The last level whose milestone exists; past it there is nothing left to reach.</summary>
    public int MaxLevel => _milestones.Length;

    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    /// <summary>Fraction of the way from the previous milestone to the next, clamped to 0..1.</summary>
    public float XpProgress
    {
        get
        {
            if (IsMaxLevel)
            {
                return 1f;
            }

            var span = NextLevelXpMilestone - _prevLevelXpMilestone;
            if (span <= 0)
            {
                return 1f;
            }

            var progress = (Xp - _prevLevelXpMilestone) / span;
            return progress < 0f ? 0f : (progress > 1f ? 1f : progress);
        }
    }

    public void AddXp(float xp)
    {
        Xp += xp;
    }

    /// <summary>
    /// Consumes one pending level-up, if the accumulated XP has reached the next milestone.
    /// Returns false when there is nothing to award, so the caller drives the UI only on a
    /// real transition. Call in a loop if a single XP grant can span several milestones.
    /// </summary>
    public bool TryLevelUp()
    {
        if (IsMaxLevel || Xp < NextLevelXpMilestone)
        {
            return false;
        }

        CurrentLevel++;

        if (!IsMaxLevel)
        {
            _prevLevelXpMilestone = _milestones[CurrentLevel - 1];
            NextLevelXpMilestone = _milestones[CurrentLevel];
        }
        else
        {
            _prevLevelXpMilestone = _milestones[_milestones.Length - 1];
            NextLevelXpMilestone = _prevLevelXpMilestone;
        }

        return true;
    }
}
