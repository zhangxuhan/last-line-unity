using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Last Line/Game Balance Config")]
public sealed class GameBalanceConfig : ScriptableObject
{
    [Serializable]
    public sealed class DifficultyTable
    {
        public float stageSeconds = 30f;
        public float baseSpawnInterval = 1.05f;
        public float minimumSpawnInterval = 0.32f;
        public int baseEnemyHp = 30;
        public float baseEnemySpeed = 0.9f;
        public float hpMultiplierPerStage = 1.17f;
        public float speedMultiplierPerStage = 1.06f;
        public float spawnIntervalMultiplierPerStage = 0.90f;
    }

    [Serializable]
    public sealed class WaveTable
    {
        public int initialBudget = 12;
        public int budgetGrowthPerWave = 5;
        public int maximumActiveEnemies = 24;
        public float restSeconds = 3f;
        [Tooltip("Use Debug Seed for repeatable test runs. Leave disabled for normal play.")]
        public bool useFixedDebugSeed;
        public int debugSeed = 1337;
        [Min(1)] public int exponentialStartWave = 5;
        [Min(1f)] public float budgetMultiplierPerWave = 1.65f;
        [Min(1f)] public float hpMultiplierPerWave = 1.60f;
        [Min(1f)] public float speedMultiplierPerWave = 1.10f;
        [Range(0.1f, 1f)] public float spawnIntervalMultiplierPerWave = 0.78f;
        [Min(0)] public int activeEnemyGrowthPerWave = 7;
        [Min(1)] public int absoluteMaximumActiveEnemies = 60;
    }

    [Serializable]
    public sealed class BaseWeaponTable
    {
        public float damage = 10f;
        public float shotInterval = 0.35f;
        public float bulletSpeed = 10f;
        public int projectileCount = 1;
        public int penetration;
        public float spreadAngleStep = 8f;
    }

    [Serializable]
    public sealed class ProgressionTable
    {
        public int initialExperience = 5;
        public int level2Experience = 8;
        public int level3Experience = 12;
        public int level4Experience = 17;
        public float laterLevelMultiplier = 1.3f;
    }

    [Serializable]
    public sealed class UpgradeRulesTable
    {
        public float minimumFireInterval = 0.12f;
        public int maximumProjectileCount = 5;
        public int maximumPenetrationCount = 3;
        public float criticalDamageMultiplier = 2f;
        public int maximumBurstCount = 3;
        public float burstShotDelay = 0.065f;
        public int maximumLightningLevel = 3;
        public float lightningBaseInterval = 6f;
        public float lightningIntervalReductionPerLevel = 1.25f;
        public float lightningMinimumInterval = 2.5f;
    }

    [Serializable]
    public sealed class EnemyRow
    {
        public Enemy.Archetype archetype;
        public int unlockWave = 1;
        public int budgetCost = 1;
        public float baseWeight = 20f;
        public float weightPerWave = 0f;
        public float minimumWeight = 0f;
        public float maximumWeight = 100f;
        public float hpMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float rootScale = 1f;
        public Vector2 visualScale = new Vector2(1.55f, 1.55f);
        public Color color = Color.white;
        public float scoreMultiplier = 1f;
        public int experienceMultiplier = 1;
        public float lateralAmplitude;
        public float lateralFrequency = 2.15f;
        public int shieldBlockHits;
        [Min(0)] public int growthTierWaveInterval;
        [Min(0)] public int maximumGrowthTiers;
        [Min(1f)] public float hpMultiplierPerGrowthTier = 1f;
        [Min(0f)] public float rootScalePerGrowthTier;
    }

    [Serializable]
    public sealed class RarityRow
    {
        public UpgradeRarity rarity;
        public int rollWeight;
        public float powerMultiplier = 1f;
        public float fireIntervalMultiplier = 1f;
        public int discreteIncrease = 1;
        public int abilityIncrease = 1;
        public float criticalChanceIncrease;
    }

    [Serializable]
    public sealed class UpgradeTypeRow
    {
        public WeaponUpgradeType type;
        public float offerWeight = 1f;
        public UpgradeRarity minimumRarity = UpgradeRarity.R;
    }

    public DifficultyTable difficulty = new DifficultyTable();
    public WaveTable waves = new WaveTable();
    public BaseWeaponTable baseWeapon = new BaseWeaponTable();
    public ProgressionTable progression = new ProgressionTable();
    public UpgradeRulesTable upgradeRules = new UpgradeRulesTable();
    [Range(0f, 1f)] public float maximumCriticalChance = 0.75f;
    [Min(0f)] public float repeatBonusPerLevel = 0.20f;
    [Min(1f)] public float maximumRepeatBonus = 1.60f;

    public EnemyRow[] enemies = CreateDefaultEnemies();
    public RarityRow[] rarities = CreateDefaultRarities();
    public UpgradeTypeRow[] upgradeTypes = CreateDefaultUpgradeTypes();

    private static GameBalanceConfig s_current;
    public static GameBalanceConfig Current
    {
        get
        {
            if (!s_current) s_current = Resources.Load<GameBalanceConfig>("Task6/GameBalanceConfig");
            if (!s_current)
            {
                s_current = CreateInstance<GameBalanceConfig>();
                s_current.name = "Runtime Default Game Balance";
            }
            return s_current;
        }
    }

    public EnemyRow GetEnemy(Enemy.Archetype archetype)
    {
        foreach (EnemyRow row in enemies) if (row != null && row.archetype == archetype) return row;
        return CreateDefaultEnemies()[0];
    }

    public RarityRow GetRarity(UpgradeRarity rarity)
    {
        foreach (RarityRow row in rarities) if (row != null && row.rarity == rarity) return row;
        return CreateDefaultRarities()[0];
    }

    public UpgradeTypeRow GetUpgradeType(WeaponUpgradeType type)
    {
        foreach (UpgradeTypeRow row in upgradeTypes) if (row != null && row.type == type) return row;
        return CreateDefaultUpgradeTypes()[0];
    }

    private static EnemyRow[] CreateDefaultEnemies() => new[]
    {
        new EnemyRow { archetype = Enemy.Archetype.Normal, unlockWave = 1, budgetCost = 1, baseWeight = 55f,
            weightPerWave = -2f, minimumWeight = 20f, maximumWeight = 55f },
        new EnemyRow { archetype = Enemy.Archetype.Runner, unlockWave = 2, budgetCost = 1, baseWeight = 16f,
            weightPerWave = 1f, maximumWeight = 26f, hpMultiplier = 0.65f, speedMultiplier = 1.55f,
            rootScale = 0.78f, visualScale = new Vector2(1.48f, 1.12f), color = new Color(0.78f, 1f, 0.48f), scoreMultiplier = 1.5f },
        new EnemyRow { archetype = Enemy.Archetype.Weaver, unlockWave = 2, budgetCost = 2, baseWeight = 14f,
            weightPerWave = 1f, maximumWeight = 22f, hpMultiplier = 0.90f, speedMultiplier = 1.05f,
            rootScale = 0.90f, visualScale = new Vector2(1.50f, 1.18f), color = new Color(0.28f, 0.90f, 1f),
            scoreMultiplier = 1.75f, experienceMultiplier = 2, lateralAmplitude = 0.38f },
        new EnemyRow { archetype = Enemy.Archetype.Brute, unlockWave = 3, budgetCost = 2, baseWeight = 13f,
            weightPerWave = 1f, maximumWeight = 22f, hpMultiplier = 2f, speedMultiplier = 0.68f,
            rootScale = 1.45f, visualScale = new Vector2(1.74f, 1.50f), color = new Color(0.72f, 0.40f, 0.32f),
            scoreMultiplier = 2f, experienceMultiplier = 2 },
        new EnemyRow { archetype = Enemy.Archetype.Elite, unlockWave = 5, budgetCost = 4, baseWeight = 4f,
            weightPerWave = 2f, maximumWeight = 14f, hpMultiplier = 3.2f, speedMultiplier = 0.82f,
            rootScale = 1.65f, visualScale = new Vector2(1.72f, 1.72f), color = new Color(0.72f, 0.48f, 1f),
            scoreMultiplier = 3f, experienceMultiplier = 3 },
        new EnemyRow { archetype = Enemy.Archetype.Shield, unlockWave = 3, budgetCost = 3, baseWeight = 8f,
            weightPerWave = 1.2f, maximumWeight = 18f, hpMultiplier = 1.25f, speedMultiplier = 0.78f,
            rootScale = 1.12f, visualScale = new Vector2(1.58f, 1.58f), color = new Color(0.48f, 0.72f, 0.78f),
            scoreMultiplier = 2.4f, experienceMultiplier = 2, shieldBlockHits = 5 },
        new EnemyRow { archetype = Enemy.Archetype.Giant, unlockWave = 4, budgetCost = 7, baseWeight = 3f,
            weightPerWave = 0.8f, maximumWeight = 12f, hpMultiplier = 4.5f, speedMultiplier = 0.52f,
            rootScale = 1.80f, visualScale = new Vector2(1.65f, 1.65f), color = new Color(0.48f, 0.18f, 0.14f),
            scoreMultiplier = 5f, experienceMultiplier = 5, growthTierWaveInterval = 3, maximumGrowthTiers = 5,
            hpMultiplierPerGrowthTier = 1.45f, rootScalePerGrowthTier = 0.18f }
    };

    private static RarityRow[] CreateDefaultRarities() => new[]
    {
        new RarityRow { rarity = UpgradeRarity.R, rollWeight = 35, powerMultiplier = 1.20f,
            fireIntervalMultiplier = 0.88f, discreteIncrease = 1, abilityIncrease = 1, criticalChanceIncrease = 0.10f },
        new RarityRow { rarity = UpgradeRarity.SR, rollWeight = 38, powerMultiplier = 1.30f,
            fireIntervalMultiplier = 0.82f, discreteIncrease = 1, abilityIncrease = 1, criticalChanceIncrease = 0.15f },
        new RarityRow { rarity = UpgradeRarity.SSR, rollWeight = 20, powerMultiplier = 1.45f,
            fireIntervalMultiplier = 0.72f, discreteIncrease = 2, abilityIncrease = 1, criticalChanceIncrease = 0.25f },
        new RarityRow { rarity = UpgradeRarity.UR, rollWeight = 7, powerMultiplier = 1.65f,
            fireIntervalMultiplier = 0.62f, discreteIncrease = 3, abilityIncrease = 2, criticalChanceIncrease = 0.35f }
    };

    private static UpgradeTypeRow[] CreateDefaultUpgradeTypes() => new[]
    {
        new UpgradeTypeRow { type = WeaponUpgradeType.Damage, offerWeight = 1.35f },
        new UpgradeTypeRow { type = WeaponUpgradeType.FireInterval, offerWeight = 1.20f },
        new UpgradeTypeRow { type = WeaponUpgradeType.ProjectileSpeed, offerWeight = 0.90f },
        new UpgradeTypeRow { type = WeaponUpgradeType.ProjectileCount, offerWeight = 0.65f, minimumRarity = UpgradeRarity.SR },
        new UpgradeTypeRow { type = WeaponUpgradeType.Penetration, offerWeight = 0.90f },
        new UpgradeTypeRow { type = WeaponUpgradeType.CriticalChance, offerWeight = 1.00f },
        new UpgradeTypeRow { type = WeaponUpgradeType.BurstFire, offerWeight = 0.28f, minimumRarity = UpgradeRarity.SSR },
        new UpgradeTypeRow { type = WeaponUpgradeType.Lightning, offerWeight = 0.45f, minimumRarity = UpgradeRarity.SR }
    };
}
