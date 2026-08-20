using System;
using System.Collections.Generic;
using System.Globalization;

public enum WeaponUpgradeType
{
    Damage, FireInterval, ProjectileSpeed, ProjectileCount, Penetration,
    CriticalChance, BurstFire, Lightning
}
public enum UpgradeRarity { R, SR, SSR }

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
    private const float LimitEpsilon = 0.0001f;

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
            case WeaponUpgradeType.CriticalChance: return CriticalChance < 0.5f - LimitEpsilon;
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
            case WeaponUpgradeType.Damage: Damage *= GetPowerMultiplier(choice.Rarity); break;
            case WeaponUpgradeType.FireInterval: FireInterval = Math.Max(MinimumFireInterval, FireInterval * GetIntervalMultiplier(choice.Rarity)); break;
            case WeaponUpgradeType.ProjectileSpeed: ProjectileSpeed *= GetPowerMultiplier(choice.Rarity); break;
            case WeaponUpgradeType.ProjectileCount: ProjectileCount = Math.Min(MaximumProjectileCount, ProjectileCount + GetDiscreteIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.Penetration: PenetrationCount = Math.Min(MaximumPenetrationCount, PenetrationCount + GetDiscreteIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.CriticalChance: CriticalChance = Math.Min(0.5f, CriticalChance + GetCriticalChanceIncrease(choice.Rarity)); break;
            case WeaponUpgradeType.BurstFire: BurstCount = Math.Min(3, BurstCount + 1); break;
            case WeaponUpgradeType.Lightning: LightningLevel = Math.Min(3, LightningLevel + 1); break;
            default: return false;
        }
        return true;
    }

    public float GetNextFloatValue(WeaponUpgradeChoice choice)
    {
        switch (choice.Type)
        {
            case WeaponUpgradeType.Damage: return Damage * GetPowerMultiplier(choice.Rarity);
            case WeaponUpgradeType.FireInterval: return Math.Max(MinimumFireInterval, FireInterval * GetIntervalMultiplier(choice.Rarity));
            case WeaponUpgradeType.ProjectileSpeed: return ProjectileSpeed * GetPowerMultiplier(choice.Rarity);
            case WeaponUpgradeType.CriticalChance: return Math.Min(0.5f, CriticalChance + GetCriticalChanceIncrease(choice.Rarity));
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
            case WeaponUpgradeType.BurstFire: return Math.Min(3, BurstCount + 1);
            case WeaponUpgradeType.Lightning: return Math.Min(3, LightningLevel + 1);
            default: return 0;
        }
    }

    public static float GetPowerMultiplier(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 1.15f : rarity == UpgradeRarity.SR ? 1.25f : 1.40f;
    public static float GetIntervalMultiplier(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 0.90f : rarity == UpgradeRarity.SR ? 0.85f : 0.75f;
    public static int GetDiscreteIncrease(UpgradeRarity rarity) => rarity == UpgradeRarity.SSR ? 2 : 1;
    public static float GetCriticalChanceIncrease(UpgradeRarity rarity)
        => rarity == UpgradeRarity.R ? 0.05f : rarity == UpgradeRarity.SR ? 0.10f : 0.15f;
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
        for (int index = types.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            WeaponUpgradeType value = types[index];
            types[index] = types[swapIndex];
            types[swapIndex] = value;
        }

        int count = Math.Min(maximumCount, types.Count);
        var choices = new List<WeaponUpgradeChoice>(count);
        for (int index = 0; index < count; index++) choices.Add(new WeaponUpgradeChoice(types[index], RollRarity(random)));
        return choices;
    }

    public static UpgradeRarity RollRarity(Random random)
    {
        int roll = random.Next(100);
        if (roll < 60) return UpgradeRarity.R;
        if (roll < 90) return UpgradeRarity.SR;
        return UpgradeRarity.SSR;
    }

    public static WeaponUpgradeOption BuildOption(WeaponUpgradeChoice choice, WeaponRuntimeState weapon)
    {
        switch (choice.Type)
        {
            case WeaponUpgradeType.Damage:
                return new WeaponUpgradeOption(choice, "Reinforced Rounds", PowerDescription(choice.Rarity, "bullet damage"), $"{Format(weapon.Damage)} -> {Format(weapon.GetNextFloatValue(choice))}");
            case WeaponUpgradeType.FireInterval:
                return new WeaponUpgradeOption(choice, "Rapid Fire", IntervalDescription(choice.Rarity), $"{Format(weapon.FireInterval)}s -> {Format(weapon.GetNextFloatValue(choice))}s");
            case WeaponUpgradeType.ProjectileSpeed:
                return new WeaponUpgradeOption(choice, "High-Velocity Rounds", PowerDescription(choice.Rarity, "projectile speed"), $"{Format(weapon.ProjectileSpeed)} -> {Format(weapon.GetNextFloatValue(choice))}");
            case WeaponUpgradeType.ProjectileCount:
                return new WeaponUpgradeOption(choice, "Multishot", $"Fire {WeaponRuntimeState.GetDiscreteIncrease(choice.Rarity)} additional projectile(s).", $"{weapon.ProjectileCount} -> {weapon.GetNextIntValue(choice)} projectiles");
            case WeaponUpgradeType.Penetration:
                return new WeaponUpgradeOption(choice, "Piercing Rounds", $"Penetrate {WeaponRuntimeState.GetDiscreteIncrease(choice.Rarity)} additional enemy(s).", $"{weapon.PenetrationCount} -> {weapon.GetNextIntValue(choice)} extra penetration");
            case WeaponUpgradeType.CriticalChance:
                return new WeaponUpgradeOption(choice, "Critical Rounds", "Shots can deal double damage.", $"{weapon.CriticalChance * 100f:0}% -> {weapon.GetNextFloatValue(choice) * 100f:0}% crit chance");
            case WeaponUpgradeType.BurstFire:
                return new WeaponUpgradeOption(choice, "Burst Module", "Fire an additional rapid volley per attack.", $"{weapon.BurstCount} -> {weapon.GetNextIntValue(choice)} volleys");
            case WeaponUpgradeType.Lightning:
                return new WeaponUpgradeOption(choice, "Auto Lightning", "Periodically execute the enemy closest to the defense line.", $"Level {weapon.LightningLevel} -> {weapon.GetNextIntValue(choice)}");
            default: throw new ArgumentOutOfRangeException(nameof(choice));
        }
    }

    private static string PowerDescription(UpgradeRarity rarity, string stat)
        => $"Increase {stat} by {(WeaponRuntimeState.GetPowerMultiplier(rarity) - 1f) * 100f:0}%.";
    private static string IntervalDescription(UpgradeRarity rarity)
        => $"Reduce fire interval by {(1f - WeaponRuntimeState.GetIntervalMultiplier(rarity)) * 100f:0}%.";
    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
