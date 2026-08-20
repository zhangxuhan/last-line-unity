using System;
using System.Collections.Generic;
using System.Globalization;

public enum WeaponUpgradeType
{
    Damage, FireInterval, ProjectileSpeed, ProjectileCount, Penetration,
    CriticalChance, BurstFire, Lightning
}
public enum UpgradeRarity { R, SR, SSR, UR }

public readonly struct WeaponUpgradeChoice
{
    public WeaponUpgradeType Type { get; }
    public UpgradeRarity Rarity { get; }
    public WeaponUpgradeChoice(WeaponUpgradeType type, UpgradeRarity rarity) { Type = type; Rarity = rarity; }
}

public readonly struct WeaponUpgradeOption
{
    public WeaponUpgradeChoice Choice { get; }
    public string Name { get; }
    public string Description { get; }
    public string ValueChange { get; }
    public WeaponUpgradeOption(WeaponUpgradeChoice choice, string name, string description, string valueChange)
    { Choice = choice; Name = name; Description = description; ValueChange = valueChange; }
}

public sealed class WeaponRuntimeState
{
    public const float MinimumFireInterval = 0.12f;
    public const int MaximumProjectileCount = 5;
    public const int MaximumPenetrationCount = 3;
    public const float MaximumCriticalChance = 0.75f;
    private const float LimitEpsilon = 0.0001f;
    private readonly int[] m_upgrade_levels = new int[Enum.GetValues(typeof(WeaponUpgradeType)).Length];

    public float Damage { get; private set; }
    public float FireInterval { get; private set; }
    public float ProjectileSpeed { get; private set; }
    public int ProjectileCount { get; private set; }
    public int PenetrationCount { get; private set; }
    public float CriticalChance { get; private set; }
    public float CriticalMultiplier => 2f;
    public int BurstCount { get; private set; } = 1;
    public int LightningLevel { get; private set; }
    public float LightningInterval => LightningLevel <= 0 ? float.PositiveInfinity : Math.Max(2.5f, 6f - (LightningLevel - 1) * 1.25f);
    public float SpreadAngleStep { get; }

    public WeaponRuntimeState(float damage, float fireInterval, float projectileSpeed,
        int projectileCount, int penetrationCount, float spreadAngleStep)
    {
        Damage = Math.Max(0f, damage);
        FireInterval = Math.Max(MinimumFireInterval, fireInterval);
        ProjectileSpeed = Math.Max(0.01f, projectileSpeed);
        ProjectileCount = Math.Max(1, Math.Min(MaximumProjectileCount, projectileCount));
        PenetrationCount = Math.Max(0, Math.Min(MaximumPenetrationCount, penetrationCount));
        SpreadAngleStep = Math.Max(0f, spreadAngleStep);
    }

    public bool CanApply(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.FireInterval: return FireInterval > MinimumFireInterval + LimitEpsilon;
            case WeaponUpgradeType.ProjectileCount: return ProjectileCount < MaximumProjectileCount;
            case WeaponUpgradeType.Penetration: return PenetrationCount < MaximumPenetrationCount;
            case WeaponUpgradeType.CriticalChance: return CriticalChance < MaximumCriticalChance - LimitEpsilon;
            case WeaponUpgradeType.BurstFire: return BurstCount < 3;
            case WeaponUpgradeType.Lightning: return LightningLevel < 3;
            default: return true;
        }
    }

    public bool TryApply(WeaponUpgradeChoice choice)
    {
        if (!CanApply(choice.Type)) return false;
        switch (choice.Type)
        {
            case WeaponUpgradeType.Damage: Damage *= GetScaledPowerMultiplier(choice); break;
            case WeaponUpgradeType.FireInterval: FireInterval = Math.Max(MinimumFireInterval, FireInterval * GetScaledIntervalMultiplier(choice)); break;
            case WeaponUpgradeType.ProjectileSpeed: ProjectileSpeed *= GetScaledPowerMultiplier(choice); break;
            case WeaponUpgradeType.ProjectileCount: ProjectileCount = Math.Min(MaximumProjectileCount, ProjectileCount + GetDiscreteIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.Penetration: PenetrationCount = Math.Min(MaximumPenetrationCount, PenetrationCount + GetDiscreteIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.CriticalChance: CriticalChance = Math.Min(MaximumCriticalChance, CriticalChance + GetScaledCriticalChanceIncrease(choice)); break;
            case WeaponUpgradeType.BurstFire: BurstCount = Math.Min(3, BurstCount + GetAbilityIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.Lightning: LightningLevel = Math.Min(3, LightningLevel + GetAbilityIncrease(choice.Rarity)); break;
            default: return false;
        }
        m_upgrade_levels[(int)choice.Type]++;
        return true;
    }

    public int GetUpgradeLevel(WeaponUpgradeType type) => m_upgrade_levels[(int)type];

    public float GetNextFloatValue(WeaponUpgradeChoice choice)
    {
        switch (choice.Type)
        {
            case WeaponUpgradeType.Damage: return Damage * GetScaledPowerMultiplier(choice);
            case WeaponUpgradeType.FireInterval: return Math.Max(MinimumFireInterval, FireInterval * GetScaledIntervalMultiplier(choice));
            case WeaponUpgradeType.ProjectileSpeed: return ProjectileSpeed * GetScaledPowerMultiplier(choice);
            case WeaponUpgradeType.CriticalChance: return Math.Min(MaximumCriticalChance, CriticalChance + GetScaledCriticalChanceIncrease(choice));
            default: return 0f;
        }
    }

    public int GetNextIntValue(WeaponUpgradeChoice choice)
    {
        int increase = GetDiscreteIncrease(choice.Rarity);
        switch (choice.Type)
        {
            case WeaponUpgradeType.ProjectileCount: return Math.Min(MaximumProjectileCount, ProjectileCount + increase);
            case WeaponUpgradeType.Penetration: return Math.Min(MaximumPenetrationCount, PenetrationCount + increase);
            case WeaponUpgradeType.BurstFire: return Math.Min(3, BurstCount + GetAbilityIncrease(choice.Rarity));
            case WeaponUpgradeType.Lightning: return Math.Min(3, LightningLevel + GetAbilityIncrease(choice.Rarity));
            default: return 0;
        }
    }

    public float GetScaledPowerMultiplier(WeaponUpgradeChoice choice)
    {
        float baseIncrease = GetPowerMultiplier(choice.Rarity) - 1f;
        return 1f + baseIncrease * GetRepeatBonus(choice.Type);
    }

    public float GetScaledIntervalMultiplier(WeaponUpgradeChoice choice)
    {
        float baseReduction = 1f - GetIntervalMultiplier(choice.Rarity);
        return Math.Max(0.50f, 1f - baseReduction * GetRepeatBonus(choice.Type));
    }

    public float GetScaledCriticalChanceIncrease(WeaponUpgradeChoice choice)
        => GetCriticalChanceIncrease(choice.Rarity) * GetRepeatBonus(choice.Type);

    private float GetRepeatBonus(WeaponUpgradeType type)
        => Math.Min(1.60f, 1f + GetUpgradeLevel(type) * 0.20f);

    public static float GetPowerMultiplier(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 1.20f : rarity == UpgradeRarity.SR ? 1.30f
            : rarity == UpgradeRarity.SSR ? 1.45f : 1.65f;
    public static float GetIntervalMultiplier(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 0.88f : rarity == UpgradeRarity.SR ? 0.82f
            : rarity == UpgradeRarity.SSR ? 0.72f : 0.62f;
    public static int GetDiscreteIncrease(UpgradeRarity rarity)
        => rarity == UpgradeRarity.UR ? 3 : rarity == UpgradeRarity.SSR ? 2 : 1;
    public static int GetAbilityIncrease(UpgradeRarity rarity) => rarity == UpgradeRarity.UR ? 2 : 1;
    public static float GetCriticalChanceIncrease(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 0.10f : rarity == UpgradeRarity.SR ? 0.15f
            : rarity == UpgradeRarity.SSR ? 0.25f : 0.35f;
}

public static class WeaponUpgradeSystem
{
    private static readonly WeaponUpgradeType[] AllTypes =
    {
        WeaponUpgradeType.Damage, WeaponUpgradeType.FireInterval, WeaponUpgradeType.ProjectileSpeed,
        WeaponUpgradeType.ProjectileCount, WeaponUpgradeType.Penetration,
        WeaponUpgradeType.CriticalChance, WeaponUpgradeType.BurstFire, WeaponUpgradeType.Lightning
    };

    public static List<WeaponUpgradeChoice> GetRandomChoices(WeaponRuntimeState weapon, int maximumCount, Random random)
    {
        var types = new List<WeaponUpgradeType>(AllTypes.Length);
        foreach (WeaponUpgradeType type in AllTypes) if (weapon.CanApply(type)) types.Add(type);

        int count = Math.Min(maximumCount, types.Count);
        var choices = new List<WeaponUpgradeChoice>(count);
        for (int index = 0; index < count; index++)
        {
            int selectedIndex = PickWeightedTypeIndex(types, random);
            WeaponUpgradeType type = types[selectedIndex];
            types.RemoveAt(selectedIndex);
            choices.Add(new WeaponUpgradeChoice(type, RollRarity(type, random)));
        }
        return choices;
    }

    private static int PickWeightedTypeIndex(List<WeaponUpgradeType> types, Random random)
    {
        float totalWeight = 0f;
        foreach (WeaponUpgradeType type in types) totalWeight += GetTypeWeight(type);
        double roll = random.NextDouble() * totalWeight;
        for (int index = 0; index < types.Count; index++)
        {
            roll -= GetTypeWeight(types[index]);
            if (roll <= 0d) return index;
        }
        return types.Count - 1;
    }

    private static float GetTypeWeight(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return 1.35f;
            case WeaponUpgradeType.FireInterval: return 1.20f;
            case WeaponUpgradeType.CriticalChance: return 1.00f;
            case WeaponUpgradeType.ProjectileSpeed:
            case WeaponUpgradeType.Penetration: return 0.90f;
            case WeaponUpgradeType.ProjectileCount: return 0.65f;
            case WeaponUpgradeType.Lightning: return 0.45f;
            case WeaponUpgradeType.BurstFire: return 0.28f;
            default: return 1f;
        }
    }

    public static UpgradeRarity RollRarity(Random random)
    {
        int roll = random.Next(100);
        if (roll < 35) return UpgradeRarity.R;
        if (roll < 73) return UpgradeRarity.SR;
        if (roll < 93) return UpgradeRarity.SSR;
        return UpgradeRarity.UR;
    }

    public static UpgradeRarity RollRarity(WeaponUpgradeType type, Random random)
    {
        UpgradeRarity rarity = RollRarity(random);
        UpgradeRarity minimum = GetMinimumRarity(type);
        return rarity < minimum ? minimum : rarity;
    }

    public static UpgradeRarity GetMinimumRarity(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.BurstFire: return UpgradeRarity.SSR;
            case WeaponUpgradeType.ProjectileCount:
            case WeaponUpgradeType.Lightning: return UpgradeRarity.SR;
            default: return UpgradeRarity.R;
        }
    }

    public static WeaponUpgradeOption BuildOption(WeaponUpgradeChoice choice, WeaponRuntimeState weapon)
    {
        switch (choice.Type)
        {
            case WeaponUpgradeType.Damage:
                return new WeaponUpgradeOption(choice, "Reinforced Rounds", PowerDescription(weapon.GetScaledPowerMultiplier(choice), "bullet damage"), $"{Format(weapon.Damage)} -> {Format(weapon.GetNextFloatValue(choice))}");
            case WeaponUpgradeType.FireInterval:
                return new WeaponUpgradeOption(choice, "Rapid Fire", IntervalDescription(weapon.GetScaledIntervalMultiplier(choice)), $"{Format(weapon.FireInterval)}s -> {Format(weapon.GetNextFloatValue(choice))}s");
            case WeaponUpgradeType.ProjectileSpeed:
                return new WeaponUpgradeOption(choice, "High-Velocity Rounds", PowerDescription(weapon.GetScaledPowerMultiplier(choice), "projectile speed"), $"{Format(weapon.ProjectileSpeed)} -> {Format(weapon.GetNextFloatValue(choice))}");
            case WeaponUpgradeType.ProjectileCount:
                return new WeaponUpgradeOption(choice, "Multishot", $"Fire {WeaponRuntimeState.GetDiscreteIncrease(choice.Rarity)} additional projectile(s).", $"{weapon.ProjectileCount} -> {weapon.GetNextIntValue(choice)} projectiles");
            case WeaponUpgradeType.Penetration:
                return new WeaponUpgradeOption(choice, "Piercing Rounds", $"Penetrate {WeaponRuntimeState.GetDiscreteIncrease(choice.Rarity)} additional enemy(s).", $"{weapon.PenetrationCount} -> {weapon.GetNextIntValue(choice)} extra penetration");
            case WeaponUpgradeType.CriticalChance:
                return new WeaponUpgradeOption(choice, "Critical Rounds", "Shots can deal double damage.", $"{weapon.CriticalChance * 100f:0}% -> {weapon.GetNextFloatValue(choice) * 100f:0}% crit chance");
            case WeaponUpgradeType.BurstFire:
                return new WeaponUpgradeOption(choice, "Burst Module", $"Add {WeaponRuntimeState.GetAbilityIncrease(choice.Rarity)} rapid volley per attack.", $"{weapon.BurstCount} -> {weapon.GetNextIntValue(choice)} volleys");
            case WeaponUpgradeType.Lightning:
                return new WeaponUpgradeOption(choice, "Auto Lightning", $"Gain {WeaponRuntimeState.GetAbilityIncrease(choice.Rarity)} lightning level.", $"Level {weapon.LightningLevel} -> {weapon.GetNextIntValue(choice)}");
            default: throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }

    private static string PowerDescription(float multiplier, string stat)
        => $"Increase {stat} by {(multiplier - 1f) * 100f:0}%.";
    private static string IntervalDescription(float multiplier)
        => $"Reduce fire interval by {(1f - multiplier) * 100f:0}%.";
    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
