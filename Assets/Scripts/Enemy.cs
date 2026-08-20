using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField, Min(1)] private int m_max_hp = 30;
    [SerializeField, Min(0)] private int m_score = 100;
    [SerializeField, Min(0)] private int m_experience = 1;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float m_move_speed = 0.9f;
    [SerializeField] private float m_rotation_speed = 200f;

    private StageLoop m_stage_loop;
    private int m_current_hp;
    private float m_defense_line_y;
    private bool m_is_settled;
    private bool m_initialized;

    public int ExperienceReward => m_experience;

    private void Awake()
    {
        m_current_hp = m_max_hp;
        m_is_settled = false;
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
        transform.rotation *= Quaternion.AngleAxis(m_rotation_speed * Time.deltaTime, new Vector3(1f, 1f, 0f));

        if (transform.position.y <= m_defense_line_y) BreachDefense();
    }

    public void TakeDamage(int damage)
    {
        if (!m_initialized || m_is_settled || !m_stage_loop || !m_stage_loop.IsPlaying || damage <= 0) return;

        m_current_hp -= damage;
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
        m_stage_loop.AddScore(m_score);
        Destroy(gameObject);
    }

    private void BreachDefense()
    {
        if (m_is_settled) return;
        m_is_settled = true;
        DisableCollision();
        m_stage_loop.RegisterBreach(this);
        Destroy(gameObject);
    }

    private void DisableCollision()
    {
        foreach (Collider colliderComponent in GetComponentsInChildren<Collider>())
            colliderComponent.enabled = false;
    }

    private void OnValidate()
    {
        m_max_hp = Mathf.Max(1, m_max_hp);
        m_score = Mathf.Max(0, m_score);
        m_experience = Mathf.Max(0, m_experience);
        m_move_speed = Mathf.Max(0f, m_move_speed);
    }
}
