using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private Enemy m_prefab_enemy;

    [Header("Spawn Area")]
    [SerializeField, Min(0f)] private float m_horizontal_margin = 0.5f;
    [SerializeField, Min(0f)] private float m_top_margin = 0.5f;
    [SerializeField, Min(0f)] private float m_min_horizontal_separation = 0.8f;

    [Header("Wave Rules")]
    [SerializeField, Min(1)] private int m_initial_wave_budget = 7;
    [SerializeField, Min(1)] private int m_budget_growth_per_wave = 3;
    [SerializeField, Min(1)] private int m_max_active_enemies = 16;
    [SerializeField, Min(0f)] private float m_wave_rest_seconds = 3f;
    [SerializeField, Tooltip("Fixed gameplay seed for repeatable debug runs.")]
    private int m_debug_seed = 1337;

    private StageLoop m_stage_loop;
    private Camera m_camera;
    private Transform m_spawn_parent;
    private Coroutine m_spawn_coroutine;
    private System.Random m_random;
    private float m_last_spawn_x;
    private bool m_has_last_spawn;

    public void Initialize(StageLoop stageLoop, Camera gameCamera, Transform spawnParent)
    {
        StopRunning();
        m_stage_loop = stageLoop;
        m_camera = gameCamera ? gameCamera : Camera.main;
        m_spawn_parent = spawnParent;
        m_random = new System.Random(m_debug_seed);
        m_has_last_spawn = false;
        m_spawn_coroutine = StartCoroutine(MainCoroutine());
    }

    public void StopRunning()
    {
        if (m_spawn_coroutine == null) return;
        StopCoroutine(m_spawn_coroutine);
        m_spawn_coroutine = null;
    }

    private IEnumerator MainCoroutine()
    {
        int wave = 0;
        while (IsSessionActive())
        {
            while (IsSessionActive() && !m_stage_loop.IsPlaying) yield return null;
            if (!IsSessionActive()) break;

            wave++;
            int budget = m_initial_wave_budget + (wave - 1) * m_budget_growth_per_wave;
            int remainingBudget = budget;
            m_stage_loop.SetWaveStatus(wave, budget, "CLEAR THE WAVE");

            while (remainingBudget > 0 && IsSessionActive())
            {
                if (!m_stage_loop.IsPlaying || Enemy.CountActive(m_stage_loop) >= m_max_active_enemies)
                {
                    yield return null;
                    continue;
                }

                Enemy.Archetype archetype = RollAffordableArchetype(wave, remainingBudget);
                SpawnEnemy(archetype);
                remainingBudget -= GetBudgetCost(archetype);
                yield return WaitForPlayingSeconds(m_stage_loop.CurrentSpawnInterval);
            }

            while (IsSessionActive() && Enemy.CountActive(m_stage_loop) > 0) yield return null;
            if (!IsSessionActive()) break;

            float restRemaining = m_wave_rest_seconds;
            int lastShownSecond = -1;
            while (restRemaining > 0f && IsSessionActive())
            {
                if (m_stage_loop.IsPlaying)
                {
                    int shownSecond = Mathf.Max(1, Mathf.CeilToInt(restRemaining));
                    if (shownSecond != lastShownSecond)
                    {
                        lastShownSecond = shownSecond;
                        int nextBudget = m_initial_wave_budget + wave * m_budget_growth_per_wave;
                        m_stage_loop.SetWaveStatus(wave, budget,
                            $"NEXT WAVE {wave + 1}  •  {shownSecond}s  •  BUDGET {nextBudget}");
                    }
                    restRemaining -= Time.deltaTime;
                }
                yield return null;
            }
        }
        m_spawn_coroutine = null;
    }

    private IEnumerator WaitForPlayingSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && IsSessionActive())
        {
            if (m_stage_loop.IsPlaying) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool IsSessionActive()
    {
        return m_stage_loop && m_stage_loop.State != StageLoop.GameState.Title
            && m_stage_loop.State != StageLoop.GameState.GameOver;
    }

    private void SpawnEnemy(Enemy.Archetype archetype)
    {
        if (!m_prefab_enemy || !m_camera || !m_stage_loop || !m_stage_loop.IsPlaying) return;
        Enemy enemy = Instantiate(m_prefab_enemy, m_spawn_parent);
        enemy.transform.position = GetSpawnPosition();
        m_stage_loop.GetCurrentEnemyStats(out int maxHp, out float moveSpeed);
        float movementPhase = (float)(m_random.NextDouble() * Math.PI * 2d);
        enemy.Initialize(m_stage_loop, maxHp, moveSpeed, m_stage_loop.DefenseLineY, archetype, movementPhase);
    }

    private Enemy.Archetype RollAffordableArchetype(int wave, int remainingBudget)
    {
        float normalWeight = Mathf.Max(20f, 55f - wave * 2f);
        float runnerWeight = wave >= 2 ? 14f + Mathf.Min(12f, wave) : 0f;
        float weaverWeight = wave >= 2 && remainingBudget >= GetBudgetCost(Enemy.Archetype.Weaver)
            ? 12f + Mathf.Min(10f, wave) : 0f;
        float bruteWeight = wave >= 3 && remainingBudget >= GetBudgetCost(Enemy.Archetype.Brute)
            ? 10f + Mathf.Min(12f, wave) : 0f;
        float eliteWeight = wave >= 5 && remainingBudget >= GetBudgetCost(Enemy.Archetype.Elite)
            ? 4f + Mathf.Min(10f, (wave - 5) * 2f) : 0f;
        float total = normalWeight + runnerWeight + weaverWeight + bruteWeight + eliteWeight;
        double roll = m_random.NextDouble() * total;
        if ((roll -= eliteWeight) < 0d) return Enemy.Archetype.Elite;
        if ((roll -= bruteWeight) < 0d) return Enemy.Archetype.Brute;
        if ((roll -= weaverWeight) < 0d) return Enemy.Archetype.Weaver;
        if ((roll -= runnerWeight) < 0d) return Enemy.Archetype.Runner;
        return Enemy.Archetype.Normal;
    }

    private static int GetBudgetCost(Enemy.Archetype archetype)
    {
        switch (archetype)
        {
            case Enemy.Archetype.Brute:
            case Enemy.Archetype.Weaver: return 2;
            case Enemy.Archetype.Elite: return 4;
            default: return 1;
        }
    }

    private Vector3 GetSpawnPosition()
    {
        float distance = Mathf.Abs(m_camera.transform.position.z - transform.position.z);
        Vector3 bottomLeft = m_camera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = m_camera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        float minX = Mathf.Min(bottomLeft.x, topRight.x) + m_horizontal_margin;
        float maxX = Mathf.Max(bottomLeft.x, topRight.x) - m_horizontal_margin;
        float spawnX = Mathf.Lerp(minX, maxX, (float)m_random.NextDouble());
        if (m_has_last_spawn && maxX > minX)
            for (int attempt = 0; attempt < 4 && Mathf.Abs(spawnX - m_last_spawn_x) < m_min_horizontal_separation; attempt++)
                spawnX = Mathf.Lerp(minX, maxX, (float)m_random.NextDouble());

        m_last_spawn_x = spawnX;
        m_has_last_spawn = true;
        return new Vector3(spawnX, Mathf.Max(bottomLeft.y, topRight.y) - m_top_margin, 0f);
    }

    private void OnDisable() => StopRunning();

    private void OnValidate()
    {
        m_horizontal_margin = Mathf.Max(0f, m_horizontal_margin);
        m_top_margin = Mathf.Max(0f, m_top_margin);
        m_min_horizontal_separation = Mathf.Max(0f, m_min_horizontal_separation);
        m_initial_wave_budget = Mathf.Max(1, m_initial_wave_budget);
        m_budget_growth_per_wave = Mathf.Max(1, m_budget_growth_per_wave);
        m_max_active_enemies = Mathf.Max(1, m_max_active_enemies);
        m_wave_rest_seconds = Mathf.Max(0f, m_wave_rest_seconds);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
