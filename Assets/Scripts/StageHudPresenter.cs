using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class StageHudPresenter : IDisposable
{
    private readonly GameObject m_root;
    private readonly Text m_score;
    private readonly Text m_defense_text;
    private readonly Text m_time;
    private readonly Text m_level;
    private readonly Text m_wave;
    private readonly Text m_wave_hint;
    private readonly Text m_experience;
    private readonly Text m_game_over;
    private readonly GameObject m_upgrade_panel;
    private readonly GameObject m_skills_panel;
    private readonly RectTransform m_experience_fill;
    private readonly Text m_experience_percent;
    private readonly Text m_skills_status;
    private readonly Text m_skills_empty;
    private readonly GameObject[] m_skill_cells;
    private readonly Text[] m_skill_names;
    private readonly Text[] m_skill_values;
    private readonly Image[] m_skill_icons;
    private readonly Func<WeaponUpgradeType, Sprite> m_icon_resolver;
    private StageSession m_session;
    private DefenseController m_defense;
    private int m_last_displayed_second = -1;

    public StageHudPresenter(GameObject root, Text score, Text defenseText, Text time, Text level,
        Text wave, Text waveHint, Text experience, Text gameOver, GameObject upgradePanel,
        GameObject skillsPanel, RectTransform experienceFill, Text experiencePercent, Text skillsStatus,
        Text skillsEmpty, GameObject[] skillCells, Text[] skillNames, Text[] skillValues,
        Image[] skillIcons, Func<WeaponUpgradeType, Sprite> iconResolver)
    {
        m_root = root;
        m_score = score;
        m_defense_text = defenseText;
        m_time = time;
        m_level = level;
        m_wave = wave;
        m_wave_hint = waveHint;
        m_experience = experience;
        m_game_over = gameOver;
        m_upgrade_panel = upgradePanel;
        m_skills_panel = skillsPanel;
        m_experience_fill = experienceFill;
        m_experience_percent = experiencePercent;
        m_skills_status = skillsStatus;
        m_skills_empty = skillsEmpty;
        m_skill_cells = skillCells;
        m_skill_names = skillNames;
        m_skill_values = skillValues;
        m_skill_icons = skillIcons;
        m_icon_resolver = iconResolver;
    }

    public void Bind(StageSession session, DefenseController defense)
    {
        Dispose();
        m_session = session;
        m_defense = defense;
        if (m_session != null)
        {
            m_session.StateChanged += ApplyState;
            m_session.DataChanged += RefreshSession;
            ApplyState(m_session.State);
        }
        RefreshAll();
    }

    public void Dispose()
    {
        if (m_session != null)
        {
            m_session.StateChanged -= ApplyState;
            m_session.DataChanged -= RefreshSession;
        }
        m_session = null;
    }

    public void RefreshAll()
    {
        RefreshSession();
        RefreshDefense();
    }

    public void RefreshDefense()
    {
        if (m_defense_text && m_defense != null)
            m_defense_text.text = $"Defense: {m_defense.RemainingBreaches} / {m_defense.MaximumBreaches}";
    }

    public void SetWaveStatus(int wave, int budget, string hint)
    {
        if (m_wave) m_wave.text = wave > 0 ? $"WAVE {wave:00}  •  BUDGET {budget}" : "WAVE --";
        if (m_wave_hint) m_wave_hint.text = hint ?? string.Empty;
    }

    public void RefreshSkills(WeaponRuntimeState weapon)
    {
        if (!m_skills_status || weapon == null) return;
        m_skills_status.text =
            $"<color=#7EDDEC><b>WEAPON STATUS</b></color>\n" +
            $"DMG {weapon.Damage:0.0}   INTERVAL {weapon.FireInterval:0.00}s   CRIT {weapon.CriticalChance * 100f:0}%   " +
            $"PROJECTILES {weapon.ProjectileCount}   PIERCE {weapon.PenetrationCount}   BURST {weapon.BurstCount}";
        bool hasSkills = false;
        foreach (WeaponUpgradeType type in Enum.GetValues(typeof(WeaponUpgradeType)))
        {
            int index = (int)type;
            if (!HasSkillSlot(index))
            {
                Debug.LogWarning($"Skills HUD has no valid slot for {type} (enum value {index}); skipping it.");
                continue;
            }
            int upgradeLevel = weapon.GetUpgradeLevel(type);
            bool acquired = upgradeLevel > 0;
            m_skill_cells[index].SetActive(acquired);
            if (!acquired) continue;
            hasSkills = true;
            m_skill_names[index].text = $"LV {upgradeLevel}  {GetSkillDisplayName(type)}";
            m_skill_values[index].text = GetSkillCurrentValue(type, weapon);
            m_skill_icons[index].sprite = m_icon_resolver != null ? m_icon_resolver(type) : null;
        }
        if (m_skills_empty) m_skills_empty.gameObject.SetActive(!hasSkills);
    }

    private bool HasSkillSlot(int index)
    {
        return index >= 0
            && m_skill_cells != null && index < m_skill_cells.Length
            && m_skill_names != null && index < m_skill_names.Length
            && m_skill_values != null && index < m_skill_values.Length
            && m_skill_icons != null && index < m_skill_icons.Length
            && m_skill_cells[index] && m_skill_names[index] && m_skill_values[index] && m_skill_icons[index];
    }

    public void RefreshGameOver()
    {
        if (!m_game_over || m_session == null || m_defense == null) return;
        m_game_over.text =
            $"GAME OVER\n\nFinal Score: {m_session.Score}\nSurvival Time: {FormatTime(m_session.SurvivalTime)}\n" +
            $"Breaches: {m_defense.BreachCount} / {m_defense.MaximumBreaches}\nFinal Level: {m_session.Level}\n" +
            $"Total Kills: {m_session.KillCount}\n\nPress Space to Restart\nPress Esc to Return to Title";
    }

    private void ApplyState(StageLoop.GameState state)
    {
        if (m_root) m_root.SetActive(state != StageLoop.GameState.Title);
        if (m_game_over) m_game_over.gameObject.SetActive(state == StageLoop.GameState.GameOver);
        if (m_upgrade_panel) m_upgrade_panel.SetActive(state == StageLoop.GameState.LevelUp);
        if (m_skills_panel) m_skills_panel.SetActive(state == StageLoop.GameState.Skills);
    }

    private void RefreshSession()
    {
        if (m_session == null) return;
        if (m_score) m_score.text = $"Score {m_session.Score:00000}";
        int seconds = Mathf.Max(0, Mathf.FloorToInt(m_session.SurvivalTime));
        if (m_time && seconds != m_last_displayed_second)
        {
            m_last_displayed_second = seconds;
            m_time.text = $"Time {FormatTime(m_session.SurvivalTime)}";
        }
        if (m_level) m_level.text = $"Level {m_session.Level}";
        float progress = m_session.RequiredExperience > 0
            ? Mathf.Clamp01((float)m_session.CurrentExperience / m_session.RequiredExperience) : 0f;
        int percentage = Mathf.RoundToInt(progress * 100f);
        if (m_experience)
            m_experience.text = $"EXP {m_session.CurrentExperience} / {m_session.RequiredExperience} ({percentage}%)";
        if (m_experience_fill) m_experience_fill.sizeDelta = new Vector2(356f * progress, 18f);
        if (m_experience_percent) m_experience_percent.text = $"{percentage}%";
    }

    private static string GetSkillDisplayName(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return "REINFORCED ROUNDS";
            case WeaponUpgradeType.FireInterval: return "RAPID FIRE";
            case WeaponUpgradeType.ProjectileSpeed: return "HIGH-VELOCITY ROUNDS";
            case WeaponUpgradeType.ProjectileCount: return "MULTISHOT";
            case WeaponUpgradeType.Penetration: return "PIERCING ROUNDS";
            case WeaponUpgradeType.CriticalChance: return "CRITICAL ROUNDS";
            case WeaponUpgradeType.BurstFire: return "BURST MODULE";
            case WeaponUpgradeType.Lightning: return "AUTO LIGHTNING";
            default: return type.ToString().ToUpperInvariant();
        }
    }

    private static string GetSkillCurrentValue(WeaponUpgradeType type, WeaponRuntimeState weapon)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return $"Current damage: {weapon.Damage:0.0}";
            case WeaponUpgradeType.FireInterval: return $"Current fire interval: {weapon.FireInterval:0.00}s";
            case WeaponUpgradeType.ProjectileSpeed: return $"Current projectile speed: {weapon.ProjectileSpeed:0.0}";
            case WeaponUpgradeType.ProjectileCount: return $"Current projectiles per volley: {weapon.ProjectileCount}";
            case WeaponUpgradeType.Penetration: return $"Current extra penetration: {weapon.PenetrationCount}";
            case WeaponUpgradeType.CriticalChance: return $"Current critical chance: {weapon.CriticalChance * 100f:0}%";
            case WeaponUpgradeType.BurstFire: return $"Current volleys per attack: {weapon.BurstCount}";
            case WeaponUpgradeType.Lightning: return $"Lightning level {weapon.LightningLevel}, every {weapon.LightningInterval:0.00}s";
            default: return string.Empty;
        }
    }

    private static string FormatTime(float time)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(time));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }
}
