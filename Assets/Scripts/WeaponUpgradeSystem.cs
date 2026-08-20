using System;
using System.Collections.Generic;
using System.Globalization;

public enum WeaponUpgradeType { Damage, FireInterval, ProjectileSpeed, ProjectileCount, Penetration }

public readonly struct WeaponUpgradeOption
{
    public WeaponUpgradeType Type { get; }
    public string Name { get; }
    public string Description { get; }
    public string ValueChange { get; }
    public WeaponUpgradeOption(WeaponUpgradeType type, string name, string description, string valueChange)
    { Type = type; Name = name; Description = description; ValueChange = valueChange; }
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
            default: return true;
        }
    }

    public bool TryApply(WeaponUpgradeType type)
    {
        if (!CanApply(type)) return false;
        switch (type)
        {
            case WeaponUpgradeType.Damage: Damage *= 1.25f; break;
            case WeaponUpgradeType.FireInterval: FireInterval = Math.Max(MinimumFireInterval, FireInterval * 0.85f); break;
            case WeaponUpgradeType.ProjectileSpeed: ProjectileSpeed *= 1.25f; break;
            case WeaponUpgradeType.ProjectileCount: ProjectileCount++; break;
            case WeaponUpgradeType.Penetration: PenetrationCount++; break;
            default: return false;
        }
        return true;
    }

    public float GetNextFloatValue(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return Damage * 1.25f;
            case WeaponUpgradeType.FireInterval: return Math.Max(MinimumFireInterval, FireInterval * 0.85f);
            case WeaponUpgradeType.ProjectileSpeed: return ProjectileSpeed * 1.25f;
            default: return 0f;
        }
    }

    public int GetNextIntValue(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.ProjectileCount: return Math.Min(MaximumProjectileCount, ProjectileCount + 1);
            case WeaponUpgradeType.Penetration: return Math.Min(MaximumPenetrationCount, PenetrationCount + 1);
            default: return 0;
        }
    }
}

public static class WeaponUpgradeSystem
{
    private static readonly WeaponUpgradeType[] AllTypes =
    {
        WeaponUpgradeType.Damage, WeaponUpgradeType.FireInterval, WeaponUpgradeType.ProjectileSpeed,
        WeaponUpgradeType.ProjectileCount, WeaponUpgradeType.Penetration
    };

    public static List<WeaponUpgradeType> GetRandomCandidates(WeaponRuntimeState weapon, int maximumCount, Random random)
    {
        var candidates = new List<WeaponUpgradeType>(AllTypes.Length);
        foreach (WeaponUpgradeType type in AllTypes) if (weapon.CanApply(type)) candidates.Add(type);
        for (int index = candidates.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            WeaponUpgradeType value = candidates[index];
            candidates[index] = candidates[swapIndex];
            candidates[swapIndex] = value;
        }
        if (candidates.Count > maximumCount) candidates.RemoveRange(maximumCount, candidates.Count - maximumCount);
        return candidates;
    }

    public static WeaponUpgradeOption BuildOption(WeaponUpgradeType type, WeaponRuntimeState weapon)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage:
                return new WeaponUpgradeOption(type, "Reinforced Rounds", "Increase bullet damage by 25%.", $"{Format(weapon.Damage)} -> {Format(weapon.GetNextFloatValue(type))}");
            case WeaponUpgradeType.FireInterval:
                return new WeaponUpgradeOption(type, "Rapid Fire", "Reduce fire interval by 15%.", $"{Format(weapon.FireInterval)}s -> {Format(weapon.GetNextFloatValue(type))}s");
            case WeaponUpgradeType.ProjectileSpeed:
                return new WeaponUpgradeOption(type, "High-Velocity Rounds", "Increase projectile speed by 25%.", $"{Format(weapon.ProjectileSpeed)} -> {Format(weapon.GetNextFloatValue(type))}");
            case WeaponUpgradeType.ProjectileCount:
                return new WeaponUpgradeOption(type, "Multishot", "Fire one additional projectile.", $"{weapon.ProjectileCount} -> {weapon.GetNextIntValue(type)} projectiles");
            case WeaponUpgradeType.Penetration:
                return new WeaponUpgradeOption(type, "Piercing Rounds", "Penetrate one additional enemy.", $"{weapon.PenetrationCount} -> {weapon.GetNextIntValue(type)} extra penetration");
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
