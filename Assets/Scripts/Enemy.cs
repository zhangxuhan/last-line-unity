using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField, Min(1)] private int m_max_hp = 30;
    [SerializeField, Min(0)] private int m_score = 100;
    [SerializeField, Min(0)] private int m_experience = 1;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float m_move_speed = 0.9f;
    [SerializeField, Min(0f)] private float m_walk_bob = 0.045f;
    [SerializeField, Min(0f)] private float m_walk_tilt = 3f;

    private StageLoop m_stage_loop;
    private float m_current_hp;
    private float m_defense_line_y;
    private bool m_is_settled;
    private bool m_initialized;
    private Transform m_visual;
    private SpriteRenderer m_sprite_renderer;
    private Vector3 m_visual_home;
    private Vector3 m_visual_base_scale;
    private float m_walk_phase;
    private Coroutine m_hit_feedback;

    public int ExperienceReward => m_experience;

    private void Awake()
    {
        m_current_hp = m_max_hp;
        m_is_settled = false;
        SetupVisual();
    }

    public void Initialize(StageLoop stageLoop, int maxHp, float moveSpeed, float defenseLineY)
    {
        m_stage_loop = stageLoop;
        m_max_hp = Mathf.Max(1, maxHp);
        m_current_hp = m_max_hp;
        m_move_speed = Mathf.Max(0f, moveSpeed);
        m_defense_line_y = defenseLineY;
        m_is_settled = false;
        m_initialized = true;
    }

    private void Update()
    {
        if (!m_initialized || m_is_settled || !m_stage_loop || !m_stage_loop.IsPlaying) return;

        transform.position += Vector3.down * m_move_speed * Time.deltaTime;
        UpdateWalkVisual();

        if (transform.position.y <= m_defense_line_y) BreachDefense();
    }

    public void TakeDamage(float damage)
    {
        if (!m_initialized || m_is_settled || !m_stage_loop || !m_stage_loop.IsPlaying || damage <= 0) return;

        m_current_hp -= damage;
        m_stage_loop.Feedback?.PlayHit(transform.position);
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        if (m_sprite_renderer) m_sprite_renderer.color = Color.white;
        if (m_visual) m_visual.localScale = m_visual_base_scale;
        m_hit_feedback = StartCoroutine(HitFeedbackRoutine());
        if (m_current_hp <= 0) Die();
    }

    public void StopRunning()
    {
        m_is_settled = true;
        DisableCollision();
    }

    private void Die()
    {
        if (m_is_settled) return;
        m_is_settled = true;
        DisableCollision();
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        m_stage_loop.Feedback?.PlayEnemyDeath(transform.position);
        m_stage_loop.RegisterEnemyKilled(m_score, m_experience);
        Destroy(gameObject);
    }

    private void BreachDefense()
    {
        if (m_is_settled) return;
        m_is_settled = true;
        DisableCollision();
        m_stage_loop.Feedback?.PlayBreach(transform.position);
        m_stage_loop.RegisterBreach(this);
        Destroy(gameObject);
    }

    private void DisableCollision()
    {
        foreach (Collider colliderComponent in GetComponentsInChildren<Collider>())
            colliderComponent.enabled = false;
    }

    private void SetupVisual()
    {
        foreach (MeshRenderer mesh in GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
        GameObject visualObject = new GameObject("Visual", typeof(SpriteRenderer));
        m_visual = visualObject.transform;
        m_visual.SetParent(transform, false);
        m_sprite_renderer = visualObject.GetComponent<SpriteRenderer>();
        m_sprite_renderer.sprite = Resources.Load<Sprite>("Task5/Art/zombie");
        m_sprite_renderer.sortingOrder = 1;
        m_visual.localScale = Vector3.one * 1.55f;
        m_visual_base_scale = m_visual.localScale;
        m_visual_home = Vector3.zero;
        m_walk_phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void UpdateWalkVisual()
    {
        if (!m_visual) return;
        float frequency = Mathf.Lerp(5f, 8f, Mathf.Clamp01(m_move_speed / 2f));
        m_walk_phase += Time.deltaTime * frequency;
        float step = Mathf.Sin(m_walk_phase);
        m_visual.localPosition = m_visual_home + Vector3.up * (Mathf.Abs(step) * m_walk_bob);
        m_visual.localRotation = Quaternion.Euler(0f, 0f, 270f + step * m_walk_tilt);
    }

    private IEnumerator HitFeedbackRoutine()
    {
        if (!m_sprite_renderer) yield break;
        Color original = Color.white;
        m_sprite_renderer.color = new Color(1f, 0.42f, 0.30f);
        m_visual.localScale = m_visual_base_scale * 1.06f;
        float elapsed = 0f;
        const float duration = 0.085f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (m_sprite_renderer) m_sprite_renderer.color = original;
        if (m_visual) m_visual.localScale = m_visual_base_scale;
        m_hit_feedback = null;
    }

    private void OnDisable()
    {
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        if (m_sprite_renderer) m_sprite_renderer.color = Color.white;
    }

    private void OnValidate()
    {
        m_max_hp = Mathf.Max(1, m_max_hp);
        m_score = Mathf.Max(0, m_score);
        m_experience = Mathf.Max(0, m_experience);
        m_move_speed = Mathf.Max(0f, m_move_speed);
        m_walk_bob = Mathf.Max(0f, m_walk_bob);
        m_walk_tilt = Mathf.Max(0f, m_walk_tilt);
    }
}
