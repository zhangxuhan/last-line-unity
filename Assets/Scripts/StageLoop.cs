using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageLoop : MonoBehaviour
{
    public static StageLoop Instance { get; private set; }

    [SerializeField] private TitleLoop m_title_loop;
    [Header("Layout")]
    [SerializeField] private Transform m_stage_transform;
    [SerializeField] private Text m_stage_score_text;
    [Header("Prefab")]
    [SerializeField] private Player m_prefab_player;
    [SerializeField] private EnemySpawner m_prefab_enemy_spawner;

    private Coroutine m_stage_coroutine;
    private int m_game_score;

    public void StartStageLoop()
    {
        StopStageLoop();
        m_stage_coroutine = StartCoroutine(StageCoroutine());
    }

    private IEnumerator StageCoroutine()
    {
        SetupStage();
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CleanupStage();
                m_stage_coroutine = null;
                m_title_loop.StartTitleLoop();
                yield break;
            }
            yield return null;
        }
    }

    private void SetupStage()
    {
        CleanupStageObjects();
        Instance = this;
        m_game_score = 0;
        RefreshScore();

        Player player = Instantiate(m_prefab_player, m_stage_transform);
        player.transform.position = new Vector3(0f, -4f, 0f);
        player.StartRunning();
        CreateSpawner(new Vector3(-4f, 4f, 0f));
        CreateSpawner(new Vector3(4f, 4f, 0f));
    }

    private void CreateSpawner(Vector3 position)
    {
        EnemySpawner spawner = Instantiate(m_prefab_enemy_spawner, m_stage_transform);
        spawner.transform.position = position;
        spawner.StartRunning();
    }

    private void StopStageLoop()
    {
        if (m_stage_coroutine != null)
        {
            StopCoroutine(m_stage_coroutine);
            m_stage_coroutine = null;
        }
        CleanupStage();
    }

    private void CleanupStage()
    {
        if (Instance == this) Instance = null;
        CleanupStageObjects();
    }

    private void CleanupStageObjects()
    {
        if (!m_stage_transform) return;
        foreach (Player player in m_stage_transform.GetComponentsInChildren<Player>(true)) player.StopRunning();
        foreach (EnemySpawner spawner in m_stage_transform.GetComponentsInChildren<EnemySpawner>(true)) spawner.StopRunning();
        for (int index = m_stage_transform.childCount - 1; index >= 0; index--)
            Destroy(m_stage_transform.GetChild(index).gameObject);
    }

    public void AddScore(int value)
    {
        if (Instance != this || value <= 0) return;
        m_game_score += value;
        RefreshScore();
    }

    private void RefreshScore()
    {
        if (m_stage_score_text) m_stage_score_text.text = $"Score {m_game_score:00000}";
    }

    private void OnDisable() => StopStageLoop();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
