using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerBullet : MonoBehaviour, IPoolable
{
    private const int PhysicsQueryBufferSize = 16;

    [Header("Lifetime")]
    [SerializeField, Min(0.1f)] private float m_max_lifetime = 3f;
    [SerializeField, Min(0.01f)] private float m_collision_radius = 0.12f;
    [SerializeField, Min(0f)] private float m_viewport_margin = 0.1f;

    private readonly HashSet<int> m_hit_enemy_ids = new HashSet<int>();
    private readonly RaycastHit[] m_hit_buffer = new RaycastHit[PhysicsQueryBufferSize];
    private readonly Collider[] m_overlap_buffer = new Collider[PhysicsQueryBufferSize];
    private Vector3 m_direction = Vector3.up;
    private float m_speed;
    private float m_damage;
    private int m_remaining_penetration;
    private float m_spawn_time;
    private Camera m_camera;
    private bool m_initialized;
    private bool m_is_critical;
    private Action<PlayerBullet> m_release_to_pool;
    private Collider[] m_colliders;
    private SpriteRenderer[] m_sprite_renderers;
    private TrailRenderer m_trail;
    private static int s_last_physics_sync_frame = -1;
    private static Sprite s_bullet_sprite;
    private static Material s_trail_material;

    public void ConfigurePool(Action<PlayerBullet> releaseToPool)
    {
        m_release_to_pool = releaseToPool;
    }

    public void Initialize(Vector3 direction, float damage, float speed, int penetration, bool isCritical = false)
    {
        m_direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        m_damage = Mathf.Max(0f, damage);
        m_speed = Mathf.Max(0f, speed);
        m_remaining_penetration = Mathf.Max(0, penetration);
        m_is_critical = isCritical;
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
            Despawn();
            return;
        }
        if (StageLoop.Instance.State == StageLoop.GameState.LevelUp
            || StageLoop.Instance.State == StageLoop.GameState.Skills) return;

        if (s_last_physics_sync_frame != Time.frameCount)
        {
            Physics.SyncTransforms();
            s_last_physics_sync_frame = Time.frameCount;
        }

        float distance = m_speed * Time.deltaTime;
        Vector3 start = transform.position;
        if (ProcessOverlaps(start)) return;

        int hitCount = Physics.SphereCastNonAlloc(start, m_collision_radius, m_direction, m_hit_buffer,
            distance, ~0, QueryTriggerInteraction.Collide);
        if (hitCount >= m_hit_buffer.Length)
        {
            // Preserve collision correctness in unusually dense casts instead of silently truncating hits.
            RaycastHit[] overflowHits = Physics.SphereCastAll(start, m_collision_radius, m_direction,
                distance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(overflowHits, CompareHitDistance);
            if (ProcessHits(overflowHits, overflowHits.Length, start)) return;
        }
        else
        {
            SortHitsByDistance(m_hit_buffer, hitCount);
            if (ProcessHits(m_hit_buffer, hitCount, start)) return;
        }

        transform.position = start + m_direction * distance;
        if (ProcessOverlaps(transform.position)) return;
        if (Time.time - m_spawn_time >= m_max_lifetime || IsOutsideCamera()) Despawn();
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
        CacheComponents();
        foreach (Collider colliderComponent in m_colliders) colliderComponent.enabled = false;
        foreach (SpriteRenderer sprite in m_sprite_renderers) sprite.enabled = false;
        if (m_trail)
        {
            m_trail.Clear();
            m_trail.enabled = false;
        }
    }

    public void OnSpawned()
    {
        m_direction = Vector3.up;
        m_speed = 0f;
        m_damage = 0f;
        m_remaining_penetration = 0;
        m_spawn_time = 0f;
        m_camera = null;
        m_initialized = false;
        m_is_critical = false;
        m_hit_enemy_ids.Clear();
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        CacheComponents();
        foreach (Collider colliderComponent in m_colliders) colliderComponent.enabled = true;
        foreach (SpriteRenderer sprite in m_sprite_renderers) sprite.enabled = true;
        if (m_trail)
        {
            m_trail.Clear();
            m_trail.enabled = true;
        }
    }

    public void OnDespawned()
    {
        StopRunning();
        m_direction = Vector3.up;
        m_speed = 0f;
        m_damage = 0f;
        m_remaining_penetration = 0;
        m_spawn_time = 0f;
        m_camera = null;
        m_is_critical = false;
        m_hit_enemy_ids.Clear();
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private bool TryHit(Enemy enemy)
    {
        if (!m_hit_enemy_ids.Add(enemy.GetInstanceID())) return false;
        enemy.TakeDamage(m_damage, m_is_critical);
        if (m_remaining_penetration <= 0)
        {
            Despawn();
            return true;
        }
        m_remaining_penetration--;
        return false;
    }

    private bool ProcessOverlaps(Vector3 position)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(position, m_collision_radius, m_overlap_buffer,
            ~0, QueryTriggerInteraction.Collide);
        if (overlapCount >= m_overlap_buffer.Length)
        {
            // Saturation is uncommon, but the allocating fallback avoids changing gameplay in crowded waves.
            Collider[] overflowOverlaps = Physics.OverlapSphere(position, m_collision_radius, ~0,
                QueryTriggerInteraction.Collide);
            return ProcessOverlapResults(overflowOverlaps, overflowOverlaps.Length);
        }
        return ProcessOverlapResults(m_overlap_buffer, overlapCount);
    }

    private bool ProcessHits(RaycastHit[] hits, int hitCount, Vector3 start)
    {
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (!enemy) continue;
            transform.position = start + m_direction * hit.distance;
            if (TryHit(enemy)) return true;
        }
        return false;
    }

    private bool ProcessOverlapResults(Collider[] overlaps, int overlapCount)
    {
        for (int i = 0; i < overlapCount; i++)
        {
            Enemy enemy = overlaps[i].GetComponentInParent<Enemy>();
            if (enemy && TryHit(enemy)) return true;
        }
        return false;
    }

    private static void SortHitsByDistance(RaycastHit[] hits, int hitCount)
    {
        // The reusable buffer is deliberately small, so insertion sort avoids comparer/delegate allocations.
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit value = hits[i];
            int index = i - 1;
            while (index >= 0 && hits[index].distance > value.distance)
            {
                hits[index + 1] = hits[index];
                index--;
            }
            hits[index + 1] = value;
        }
    }

    private static int CompareHitDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
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
        CacheComponents(true);
        foreach (Collider colliderComponent in m_colliders) colliderComponent.enabled = true;
        foreach (SpriteRenderer spriteRenderer in m_sprite_renderers) spriteRenderer.enabled = true;
        trail.Clear();
        trail.enabled = true;
    }

    private void CacheComponents(bool refresh = false)
    {
        if (refresh || m_colliders == null) m_colliders = GetComponentsInChildren<Collider>(true);
        if (refresh || m_sprite_renderers == null) m_sprite_renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (refresh || !m_trail) m_trail = GetComponent<TrailRenderer>();
    }

    private void Despawn()
    {
        if (!gameObject.activeSelf) return;
        if (m_release_to_pool != null) m_release_to_pool(this);
        else
        {
            StopRunning();
            Destroy(gameObject);
        }
    }

    private void OnValidate()
    {
        m_max_lifetime = Mathf.Max(0.1f, m_max_lifetime);
        m_collision_radius = Mathf.Max(0.01f, m_collision_radius);
        m_viewport_margin = Mathf.Max(0f, m_viewport_margin);
    }
}
