using System;

public sealed class StageSession
{
    public StageLoop.GameState State { get; private set; } = StageLoop.GameState.Title;
    public PlayerProgression Progression { get; } = new PlayerProgression();
    public int Score { get; private set; }
    public float SurvivalTime { get; private set; }
    private int m_last_notified_second;

    public bool IsPlaying => State == StageLoop.GameState.Playing;
    public int Level => Progression.Level;
    public int CurrentExperience => Progression.CurrentExperience;
    public int RequiredExperience => Progression.RequiredExperience;
    public int KillCount => Progression.KillCount;
    public int PendingUpgradeCount => Progression.PendingUpgradeCount;

    public event Action<StageLoop.GameState> StateChanged;
    public event Action DataChanged;

    public void SetState(StageLoop.GameState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }

    public void ResetRun()
    {
        Score = 0;
        SurvivalTime = 0f;
        m_last_notified_second = 0;
        Progression.Reset();
        DataChanged?.Invoke();
    }

    public void TickPlaying(float deltaTime)
    {
        if (!IsPlaying || deltaTime <= 0f) return;
        SurvivalTime += deltaTime;
        int currentSecond = (int)SurvivalTime;
        if (currentSecond == m_last_notified_second) return;
        m_last_notified_second = currentSecond;
        DataChanged?.Invoke();
    }

    public int RegisterKill(int scoreReward, int experienceReward)
    {
        if (!IsPlaying) return 0;
        if (scoreReward > 0) Score = (int)Math.Min((long)Score + scoreReward, int.MaxValue);
        int levelUpCount = Progression.RegisterKill(experienceReward);
        DataChanged?.Invoke();
        return levelUpCount;
    }

    public bool TryConsumePendingUpgrade()
    {
        bool consumed = Progression.TryConsumePendingUpgrade();
        if (consumed) DataChanged?.Invoke();
        return consumed;
    }

    public void DiscardPendingUpgrades() => Progression.DiscardPendingUpgrades();
}
