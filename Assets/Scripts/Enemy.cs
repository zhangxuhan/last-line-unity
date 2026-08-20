using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public enum Archetype { Normal, Brute, Runner, Elite, Weaver, Shield, Giant }

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
    private Vector3 m_base_root_scale;
    private Color m_visual_color = Color.white;
    private Archetype m_archetype;
    private float m_lateral_origin_x;
    private float m_lateral_phase;
    private float m_lateral_elapsed;
    private float m_lateral_amplitude;
    private float m_lateral_frequency;
    private int m_shield_hits_remaining;
    private int m_shield_max_hits;
    private SpriteRenderer m_shield_renderer;
    private static Sprite s_shield_sprite;
    private static readonly List<Enemy> s_active_enemies = new List<Enemy>();

    public int ExperienceReward => m_experience;

    private void Awake()
    {
        m_current_hp = m_max_hp;
        m_is_settled = false;
        m_base_root_scale = transform.localScale;
        SetupVisual();
    }

    public void Initialize(StageLoop stageLoop, int maxHp, float moveSpeed, float defenseLineY,
        Archetype archetype = Archetype.Normal, float movementPhase = 0f, int waveNumber = 1)
    {
        m_stage_loop = stageLoop;
        m_archetype = archetype;
        m_lateral_origin_x = transform.position.x;
        m_lateral_phase = movementPhase;
        m_lateral_elapsed = 0f;
        ApplyArchetype(archetype, waveNumber, ref maxHp, ref moveSpeed);
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

        Vector3 position = transform.position;
        position.y -= m_move_speed * Time.deltaTime;
        if (m_archetype == Archetype.Weaver)
        {
            m_lateral_elapsed += Time.deltaTime;
            position.x = m_lateral_origin_x
                + Mathf.Sin(m_lateral_elapsed * m_lateral_frequency + m_lateral_phase) * m_lateral_amplitude;
        }
        transform.position = position;
        UpdateWalkVisual();

        if (m_stage_loop.TryTriggerDefenseBomb(this)) return;
        if (transform.position.y <= m_defense_line_y) BreachDefense();
    }

    private void OnEnable()
    {
        if (!s_active_enemies.Contains(this)) s_active_enemies.Add(this);
    }

    public void TakeDamage(float damage, bool isCritical = false)
    {
        if (!m_initialized || m_is_settled || !m_stage_loop || !m_stage_loop.IsPlaying || damage <= 0) return;

        if (m_shield_hits_remaining > 0)
        {
            m_shield_hits_remaining--;
            UpdateShieldVisual();
            m_stage_loop.Feedback?.PlayShieldBlock(transform.position, m_shield_hits_remaining);
            return;
        }

        m_current_hp -= damage;
        m_stage_loop.Feedback?.PlayHit(transform.position);
        m_stage_loop.Feedback?.ShowDamage(transform.position, damage, isCritical);
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        if (m_sprite_renderer) m_sprite_renderer.color = m_visual_color;
        if (m_visual) m_visual.localScale = m_visual_base_scale;
        m_hit_feedback = StartCoroutine(HitFeedbackRoutine());
        if (m_current_hp <= 0) Die();
    }

    public void StopRunning()
    {
        m_is_settled = true;
        DisableCollision();
    }

    private void Die(bool playFeedback = true)
    {
        if (m_is_settled) return;
        m_is_settled = true;
        DisableCollision();
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        if (playFeedback) m_stage_loop.Feedback?.PlayEnemyDeath(transform.position);
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

        GameObject outlineObject = new GameObject("Silhouette", typeof(SpriteRenderer));
        outlineObject.transform.SetParent(m_visual, false);
        outlineObject.transform.localScale = Vector3.one * 1.10f;
        SpriteRenderer outline = outlineObject.GetComponent<SpriteRenderer>();
        outline.sprite = m_sprite_renderer.sprite;
        outline.color = new Color(0.015f, 0.035f, 0.045f, 0.88f);
        outline.sortingOrder = 0;
        m_visual_base_scale = m_visual.localScale;
        m_visual_home = Vector3.zero;
        m_walk_phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void ApplyArchetype(Archetype archetype, int waveNumber, ref int maxHp, ref float moveSpeed)
    {
        GameBalanceConfig.EnemyRow balance = GameBalanceConfig.Current.GetEnemy(archetype);
        int growthTier = balance.growthTierWaveInterval > 0
            ? Mathf.Clamp((Mathf.Max(1, waveNumber) - balance.unlockWave) / balance.growthTierWaveInterval,
                0, balance.maximumGrowthTiers)
            : 0;
        float tierHpMultiplier = Mathf.Pow(Mathf.Max(1f, balance.hpMultiplierPerGrowthTier), growthTier);
        maxHp = Mathf.Max(1, Mathf.CeilToInt(maxHp * balance.hpMultiplier * tierHpMultiplier));
        moveSpeed *= balance.speedMultiplier;
        m_score = Mathf.CeilToInt(m_score * balance.scoreMultiplier);
        m_experience = Mathf.Max(1, m_experience * balance.experienceMultiplier);
        transform.localScale = m_base_root_scale
            * (balance.rootScale + growthTier * balance.rootScalePerGrowthTier);
        m_visual.localScale = new Vector3(balance.visualScale.x, balance.visualScale.y, 1f);
        m_visual_color = balance.color;
        m_lateral_amplitude = Mathf.Max(0f, balance.lateralAmplitude);
        m_lateral_frequency = Mathf.Max(0.01f, balance.lateralFrequency);
        m_shield_hits_remaining = Mathf.Max(0, balance.shieldBlockHits);
        m_shield_max_hits = m_shield_hits_remaining;

        switch (archetype)
        {
            case Archetype.Brute:
                m_walk_bob *= 0.75f;
                break;
            case Archetype.Runner:
                m_walk_bob *= 1.15f;
                break;
            case Archetype.Elite:
                m_walk_bob *= 0.65f;
                m_walk_tilt *= 0.75f;
                break;
            case Archetype.Weaver:
                m_walk_bob *= 0.90f;
                m_walk_tilt *= 1.25f;
                break;
            case Archetype.Shield:
                m_walk_bob *= 0.70f;
                m_walk_tilt *= 0.55f;
                break;
            case Archetype.Giant:
                m_walk_bob *= 0.45f;
                m_walk_tilt *= 0.50f;
                break;
        }
        BuildArchetypeAccessories(archetype);
        m_visual_base_scale = m_visual.localScale;
        if (m_sprite_renderer) m_sprite_renderer.color = m_visual_color;
    }

    private void BuildArchetypeAccessories(Archetype archetype)
    {
        if (archetype != Archetype.Shield) return;
        if (!s_shield_sprite) s_shield_sprite = CreateShieldSprite();
        GameObject shield = new GameObject("Shield", typeof(SpriteRenderer));
        shield.transform.SetParent(m_visual, false);
        // The Kenney zombie source faces left and its visual is rotated 270 degrees.
        // Counter-rotate the shield so its heraldic silhouette remains upright on screen.
        shield.transform.localPosition = new Vector3(0.34f, 0f, 0f);
        shield.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        shield.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
        m_shield_renderer = shield.GetComponent<SpriteRenderer>();
        m_shield_renderer.sprite = s_shield_sprite;
        m_shield_renderer.sortingOrder = 4;
        UpdateShieldVisual();
    }

    private static Sprite CreateShieldSprite()
    {
        const int width = 64;
        const int height = 80;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "RuntimeShieldIcon";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color clear = Color.clear;
        Color rim = new Color(0.035f, 0.10f, 0.14f, 1f);
        Color metal = new Color(0.18f, 0.68f, 0.82f, 1f);
        Color highlight = new Color(0.58f, 0.94f, 1f, 1f);
        Color emblem = new Color(0.94f, 0.78f, 0.24f, 1f);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = Mathf.Abs((x + 0.5f) / width - 0.5f) * 2f;
            float ny = (y + 0.5f) / height;
            float halfWidth = ny >= 0.48f ? 0.84f : Mathf.Lerp(0.05f, 0.84f, ny / 0.48f);
            bool inside = nx <= halfWidth && ny >= 0.04f && ny <= 0.94f;
            if (!inside) { texture.SetPixel(x, y, clear); continue; }

            float innerHalfWidth = Mathf.Max(0f, halfWidth - 0.13f);
            bool border = nx > innerHalfWidth || ny < 0.10f || ny > 0.86f;
            bool centerRidge = nx < 0.08f && ny > 0.18f && ny < 0.80f;
            bool crossBar = Mathf.Abs(ny - 0.55f) < 0.055f && nx < 0.48f;
            Color pixel = border ? rim : centerRidge || crossBar ? emblem : metal;
            if (!border && x < width / 2 - 4) pixel = Color.Lerp(pixel, highlight, 0.22f);
            texture.SetPixel(x, y, pixel);
        }
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.48f), 80f);
    }

    private void UpdateShieldVisual()
    {
        if (!m_shield_renderer) return;
        float strength = m_shield_max_hits > 0
            ? Mathf.Clamp01((float)m_shield_hits_remaining / m_shield_max_hits) : 0f;
        m_shield_renderer.color = Color.Lerp(new Color(1f, 0.48f, 0.30f), Color.white, strength);
        float scale = 0.72f * Mathf.Lerp(0.88f, 1f, strength);
        m_shield_renderer.transform.localScale = new Vector3(scale, scale, 1f);
        m_shield_renderer.gameObject.SetActive(m_shield_hits_remaining > 0);
    }

    public static int CountActive(StageLoop stageLoop)
    {
        int count = 0;
        for (int index = s_active_enemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = s_active_enemies[index];
            if (!enemy)
            {
                s_active_enemies.RemoveAt(index);
                continue;
            }
            if (enemy.m_initialized && !enemy.m_is_settled && enemy.m_stage_loop == stageLoop) count++;
        }
        return count;
    }

    public static void StrikeFrontmost(StageLoop stageLoop)
    {
        Enemy target = null;
        float lowestY = float.PositiveInfinity;
        for (int index = s_active_enemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = s_active_enemies[index];
            if (!enemy)
            {
                s_active_enemies.RemoveAt(index);
                continue;
            }
            if (!enemy.m_initialized || enemy.m_is_settled || enemy.m_stage_loop != stageLoop) continue;
            if (enemy.transform.position.y < lowestY)
            {
                lowestY = enemy.transform.position.y;
                target = enemy;
            }
        }
        if (!target) return;
        target.m_stage_loop.Feedback?.PlayLightning(target.transform.position);
        target.Die();
    }

    public static int ClearVerticalLane(StageLoop stageLoop, float minX, float maxX)
    {
        var targets = new List<Enemy>();
        foreach (Enemy enemy in s_active_enemies)
            if (enemy && enemy.m_initialized && !enemy.m_is_settled && enemy.m_stage_loop == stageLoop
                && enemy.transform.position.x >= minX && enemy.transform.position.x <= maxX)
                targets.Add(enemy);
        foreach (Enemy enemy in targets) if (enemy) enemy.Die(false);
        return targets.Count;
    }

    private void UpdateWalkVisual()
    {
        if (!m_visual) return;
        float frequency = Mathf.Lerp(5f, 8f, Mathf.Clamp01(m_move_speed / 2f));
        m_walk_phase += Time.deltaTime * frequency;
        float step = Mathf.Sin(m_walk_phase);
        m_visual.localPosition = m_visual_home + Vector3.up * (Mathf.Abs(step) * m_walk_bob);
        float weaveTilt = m_archetype == Archetype.Weaver
            ? Mathf.Cos(m_lateral_elapsed * m_lateral_frequency + m_lateral_phase) * 5f : 0f;
        m_visual.localRotation = Quaternion.Euler(0f, 0f, 270f + step * m_walk_tilt + weaveTilt);
    }

    private IEnumerator HitFeedbackRoutine()
    {
        if (!m_sprite_renderer) yield break;
        m_sprite_renderer.color = new Color(1f, 0.42f, 0.30f);
        m_visual.localScale = m_visual_base_scale * 1.06f;
        float elapsed = 0f;
        const float duration = 0.085f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (m_sprite_renderer) m_sprite_renderer.color = m_visual_color;
        if (m_visual) m_visual.localScale = m_visual_base_scale;
        m_hit_feedback = null;
    }

    private void OnDisable()
    {
        s_active_enemies.Remove(this);
        if (m_hit_feedback != null) StopCoroutine(m_hit_feedback);
        if (m_sprite_renderer) m_sprite_renderer.color = m_visual_color;
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
