using System;

public sealed class PlayerProgression
{
    public const int InitialLevel = 1;
    public const int InitialExperienceRequirement = 5;
    private const int MaxLevelUpsPerAward = 1024;

    public int Level { get; private set; }
    public int CurrentExperience { get; private set; }
    public int RequiredExperience { get; private set; }
    public int KillCount { get; private set; }
    public int PendingUpgradeCount { get; private set; }

    public PlayerProgression()
    {
        Reset();
    }

    public void Reset()
    {
        Level = InitialLevel;
        CurrentExperience = 0;
        RequiredExperience = InitialExperienceRequirement;
        KillCount = 0;
        PendingUpgradeCount = 0;
    }

    public int RegisterKill(int experienceReward)
    {
        if (KillCount < int.MaxValue) KillCount++;
        return AddExperience(experienceReward);
    }

    public int AddExperience(int gainedExperience)
    {
        if (gainedExperience <= 0) return 0;

        long availableExperience = (long)CurrentExperience + gainedExperience;
        int levelUps = 0;

        while (availableExperience >= RequiredExperience && levelUps < MaxLevelUpsPerAward)
        {
            availableExperience -= RequiredExperience;
            if (Level < int.MaxValue) Level++;
            RequiredExperience = CalculateNextRequirement(Level, RequiredExperience);
            if (PendingUpgradeCount < int.MaxValue) PendingUpgradeCount++;
            levelUps++;

            if (Level == int.MaxValue && RequiredExperience == int.MaxValue) break;
        }

        CurrentExperience = (int)Math.Min(availableExperience, int.MaxValue);
        return levelUps;
    }

    public bool TryConsumePendingUpgrade()
    {
        if (PendingUpgradeCount <= 0) return false;
        PendingUpgradeCount--;
        return true;
    }

    public void DiscardPendingUpgrades()
    {
        PendingUpgradeCount = 0;
    }

    public static int CalculateNextRequirement(int newLevel, int currentRequirement)
    {
        switch (newLevel)
        {
            case 2: return 8;
            case 3: return 12;
            case 4: return 17;
            default:
                long scaled = (long)Math.Ceiling(Math.Max(1, currentRequirement) * 1.3d);
                return (int)Math.Min(scaled, int.MaxValue);
        }
    }
}
