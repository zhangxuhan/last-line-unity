using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerBullet : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField, Min(0.1f)] private float m_max_lifetime = 3f;
    [SerializeField, Min(0.01f)] private float m_collision_radius = 0.12f;
    [SerializeField, Min(0f)] private float m_viewport_margin = 0.1f;

    private readonly HashSet<int> m_hit_enemy_ids = new HashSet<int>();
    private Vector3 m_direction = Vector3.up;
    private float m_speed;
    private float m_damage;
    private int m_remaining_penetration;
    private float m_spawn_time;
    private Camera m_camera;
    private bool m_initialized;
    private static int s_last_physics_sync_frame = -1;
    private static Sprite s_bullet_sprite;
    private static Material s_trail_material;

    public void Initialize(Vector3 direction, float damage, float speed, int penetration)
    {
        m_direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        m_damage = Mathf.Max(0f, damage);
        m_speed = Mathf.Max(0f, speed);
        m_remaining_penetration = Mathf.Max(0, penetration);
        m_spawn_time = Time.time;
        m_camera = Camera.main;
        m_hit_enemy_ids.Clear();
        SetupVisual();
        m_initialized = true;
    }

    private void Update()
    {
        if (!m_initialized) return;
        if (!StageLoop.Instance || StageLoop.Instance.State == StageLoop.GameState.Title
            || StageLoop.Instance.State == StageLoop.GameState.GameOver)
        {
            StopRunning();
            Destroy(gameObject);
            return;
        }
        if (StageLoop.Instance.State == StageLoop.GameState.LevelUp) return;

        if (s_last_physics_sync_frame != Time.frameCount)
        {
            Physics.SyncTransforms();
            s_last_physics_sync_frame = Time.frameCount;
        }

        float distance = m_speed * Time.deltaTime;
        Vector3 start = transform.position;
        if (ProcessOverlaps(start)) return;

        RaycastHit[] hits = Physics.SphereCastAll(start, m_collision_radius, m_direction, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (!enemy) continue;
            transform.position = start + m_direction * hit.distance;
            if (TryHit(enemy)) return;
        }

        transform.position = start + m_direction * distance;
        if (ProcessOverlaps(transform.position)) return;
        if (Time.time - m_spawn_time >= m_max_lifetime || IsOutsideCamera()) Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!m_initialized || !StageLoop.Instance || !StageLoop.Instance.IsPlaying) return;
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy) TryHit(enemy);
    }

    public void StopRunning()
    {
        m_initialized = false;
        foreach (Collider colliderComponent in GetComponentsInChildren<Collider>())
            colliderComponent.enabled = false;
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>())
            sprite.enabled = false;
        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (trail)
        {
            trail.Clear();
            trail.enabled = false;
        }
    }

    private bool TryHit(Enemy enemy)
    {
        if (!m_hit_enemy_ids.Add(enemy.GetInstanceID())) return false;
        enemy.TakeDamage(m_damage);
        if (m_remaining_penetration <= 0)
        {
            StopRunning();
            Destroy(gameObject);
            return true;
        }
        m_remaining_penetration--;
        return false;
    }

    private bool ProcessOverlaps(Vector3 position)
    {
        Collider[] overlaps = Physics.OverlapSphere(position, m_collision_radius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider overlap in overlaps)
        {
            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (enemy && TryHit(enemy)) return true;
        }
        return false;
    }

    private bool IsOutsideCamera()
    {
        if (!m_camera) return false;
        Vector3 viewport = m_camera.WorldToViewportPoint(transform.position);
        return viewport.z <= 0f
            || viewport.x < -m_viewport_margin || viewport.x > 1f + m_viewport_margin
            || viewport.y < -m_viewport_margin || viewport.y > 1f + m_viewport_margin;
    }

    private void SetupVisual()
    {
        foreach (MeshRenderer mesh in GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
        if (!s_bullet_sprite)
            s_bullet_sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), 4f);
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (!sprite)
        {
            GameObject visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(transform, false);
            sprite = visual.GetComponent<SpriteRenderer>();
        }
        sprite.sprite = s_bullet_sprite;
        sprite.color = new Color(1f, 0.83f, 0.24f);
        sprite.sortingOrder = 3;
        sprite.transform.localScale = new Vector3(0.07f, 0.24f, 1f);
        sprite.transform.up = m_direction;

        TrailRenderer trail = GetComponent<TrailRenderer>();
        if (!trail) trail = gameObject.AddComponent<TrailRenderer>();
        if (!s_trail_material)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader) s_trail_material = new Material(shader) { name = "Task5 Shared Bullet Trail" };
        }
        if (s_trail_material) trail.sharedMaterial = s_trail_material;
        trail.time = 0.075f;
        trail.startWidth = 0.055f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.startColor = new Color(1f, 0.80f, 0.20f, 0.72f);
        trail.endColor = new Color(1f, 0.45f, 0.08f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private void OnValidate()
    {
        m_max_lifetime = Mathf.Max(0.1f, m_max_lifetime);
        m_collision_radius = Mathf.Max(0.01f, m_collision_radius);
        m_viewport_margin = Mathf.Max(0f, m_viewport_margin);
    }
}
