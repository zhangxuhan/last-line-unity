using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private Enemy m_prefab_enemy;

    [Header("Spawn Area")]
    [SerializeField, Min(0f), Tooltip("World-space margin from the visible left and right edges.")]
    private float m_horizontal_margin = 0.5f;
    [SerializeField, Min(0f), Tooltip("World-space margin below the visible top edge.")]
    private float m_top_margin = 0.5f;
    [SerializeField, Min(0f), Tooltip("Minimum horizontal separation from the previous spawn.")]
    private float m_min_horizontal_separation = 0.8f;

    private StageLoop m_stage_loop;
    private Camera m_camera;
    private Transform m_spawn_parent;
    private Coroutine m_spawn_coroutine;
    private float m_last_spawn_x;
    private bool m_has_last_spawn;

    public void Initialize(StageLoop stageLoop, Camera gameCamera, Transform spawnParent)
    {
        StopRunning();
        m_stage_loop = stageLoop;
        m_camera = gameCamera ? gameCamera : Camera.main;
        m_spawn_parent = spawnParent;
        m_has_last_spawn = false;
        m_spawn_coroutine = StartCoroutine(MainCoroutine());
    }

    public void StopRunning()
    {
        if (m_spawn_coroutine != null)
        {
            StopCoroutine(m_spawn_coroutine);
            m_spawn_coroutine = null;
        }
    }

    private IEnumerator MainCoroutine()
    {
        while (m_stage_loop && m_stage_loop.IsPlaying)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(m_stage_loop.CurrentSpawnInterval);
        }

        m_spawn_coroutine = null;
    }

    private void SpawnEnemy()
    {
        if (!m_prefab_enemy || !m_camera || !m_stage_loop || !m_stage_loop.IsPlaying) return;

        Vector3 position = GetSpawnPosition();
        Enemy enemy = Instantiate(m_prefab_enemy, m_spawn_parent);
        enemy.transform.position = position;
        m_stage_loop.GetCurrentEnemyStats(out int maxHp, out float moveSpeed);
        enemy.Initialize(m_stage_loop, maxHp, moveSpeed, m_stage_loop.DefenseLineY,
            RollArchetype(m_stage_loop.DifficultyStage));
    }

    private static Enemy.Archetype RollArchetype(int difficultyStage)
    {
        if (difficultyStage < 1) return Enemy.Archetype.Normal;
        float roll = Random.value;
        float bruteChance = difficultyStage >= 2 ? 0.35f : 0.25f;
        float runnerChance = difficultyStage >= 2 ? 0.30f : 0.25f;
        if (roll < bruteChance) return Enemy.Archetype.Brute;
        if (roll < bruteChance + runnerChance) return Enemy.Archetype.Runner;
        return Enemy.Archetype.Normal;
    }

    private Vector3 GetSpawnPosition()
    {
        float distance = Mathf.Abs(m_camera.transform.position.z - transform.position.z);
        Vector3 bottomLeft = m_camera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = m_camera.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        float minX = Mathf.Min(bottomLeft.x, topRight.x) + m_horizontal_margin;
        float maxX = Mathf.Max(bottomLeft.x, topRight.x) - m_horizontal_margin;
        float spawnX = Random.Range(minX, maxX);

        if (m_has_last_spawn && maxX > minX)
        {
            for (int attempt = 0; attempt < 4 && Mathf.Abs(spawnX - m_last_spawn_x) < m_min_horizontal_separation; attempt++)
                spawnX = Random.Range(minX, maxX);
        }

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
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
