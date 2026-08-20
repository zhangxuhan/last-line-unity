using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private Enemy m_prefab_enemy;
    [Header("Parameter")]
    [SerializeField, Min(0.1f)] private float m_spawn_interval = 2f;
    private Coroutine m_spawn_coroutine;

    public void StartRunning()
    {
        StopRunning();
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
        while (true)
        {
            if (m_prefab_enemy)
            {
                Enemy enemy = Instantiate(m_prefab_enemy, transform.parent);
                enemy.transform.position = transform.position;
            }
            yield return new WaitForSeconds(m_spawn_interval);
        }
    }

    private void OnDisable() => StopRunning();

    private void OnValidate() => m_spawn_interval = Mathf.Max(0.1f, m_spawn_interval);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
