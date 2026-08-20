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
    [SerializeField, Min(0.1f)] private float m_life_time = 12f;

    private int m_current_hp;
    private float m_spawn_time;
    private bool m_is_dead;

    public int ExperienceReward => m_experience;

    private void Awake()
    {
        m_current_hp = m_max_hp;
        m_spawn_time = Time.time;
        m_is_dead = false;
    }

    private void Update()
    {
        if (m_is_dead) return;
        transform.position += Vector3.down * m_move_speed * Time.deltaTime;
        transform.rotation *= Quaternion.AngleAxis(m_rotation_speed * Time.deltaTime, new Vector3(1f, 1f, 0f));
        if (Time.time - m_spawn_time >= m_life_time) Destroy(gameObject);
    }

    public void TakeDamage(int damage)
    {
        if (m_is_dead || damage <= 0) return;
        m_current_hp -= damage;
        if (m_current_hp <= 0) Die();
    }

    private void Die()
    {
        if (m_is_dead) return;
        m_is_dead = true;
        if (StageLoop.Instance) StageLoop.Instance.AddScore(m_score);
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        m_max_hp = Mathf.Max(1, m_max_hp);
        m_score = Mathf.Max(0, m_score);
        m_experience = Mathf.Max(0, m_experience);
        m_move_speed = Mathf.Max(0f, m_move_speed);
        m_life_time = Mathf.Max(0.1f, m_life_time);
    }
}
