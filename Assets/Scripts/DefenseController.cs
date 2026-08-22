using UnityEngine;

public sealed class DefenseController
{
    private readonly StageLoop m_stage;
    private int m_max_breaches;
    private int m_bomb_count;
    private int m_breach_count;
    private bool[] m_bombs_active;
    private GameObject[] m_bomb_visuals;
    private float m_lane_min_x;
    private float m_lane_width;
    private float m_trigger_y;
    private static Sprite s_mine_sprite;
    private static Sprite s_glow_sprite;

    public int BreachCount => m_breach_count;
    public int MaximumBreaches => m_max_breaches;
    public int RemainingBreaches => Mathf.Max(0, m_max_breaches - m_breach_count);
    public bool IsResolvingAreaAttack { get; private set; }

    public DefenseController(StageLoop stage) => m_stage = stage;

    public void Reset(Camera camera, Transform stageRoot, float defenseLineY, int maximumBreaches, int bombCount)
    {
        m_max_breaches = Mathf.Max(1, maximumBreaches);
        m_bomb_count = Mathf.Clamp(bombCount, 1, 8);
        m_breach_count = 0;
        IsResolvingAreaAttack = false;
        CreateBombs(camera, stageRoot, defenseLineY);
    }

    public bool RegisterBreach(Enemy enemy)
    {
        if (!m_stage.IsPlaying || !enemy) return false;
        m_breach_count = Mathf.Min(m_max_breaches, m_breach_count + 1);
        return m_breach_count >= m_max_breaches;
    }

    public bool TryTriggerBomb(Enemy triggeringEnemy, GameFeedback feedback)
    {
        if (!m_stage.IsPlaying || !triggeringEnemy || m_bombs_active == null
            || triggeringEnemy.transform.position.y > m_trigger_y || m_lane_width <= 0f) return false;

        int lane = Mathf.FloorToInt((triggeringEnemy.transform.position.x - m_lane_min_x) / m_lane_width);
        lane = Mathf.Clamp(lane, 0, m_bombs_active.Length - 1);
        if (!m_bombs_active[lane]) return false;

        m_bombs_active[lane] = false;
        if (m_bomb_visuals != null && m_bomb_visuals[lane]) Object.Destroy(m_bomb_visuals[lane]);

        float laneMin = m_lane_min_x + lane * m_lane_width;
        float laneMax = laneMin + m_lane_width;
        float centerX = (laneMin + laneMax) * 0.5f;
        feedback?.PlayBombDetonation(centerX, m_lane_width, m_trigger_y);
        IsResolvingAreaAttack = true;
        Enemy.ClearVerticalLane(m_stage, laneMin, laneMax);
        IsResolvingAreaAttack = false;
        return true;
    }

    private void CreateBombs(Camera camera, Transform stageRoot, float defenseLineY)
    {
        if (!camera || !stageRoot) return;
        float distance = Mathf.Abs(camera.transform.position.z);
        Vector3 left = camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
        Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));
        m_lane_min_x = Mathf.Min(left.x, right.x);
        m_lane_width = Mathf.Abs(right.x - left.x) / m_bomb_count;
        m_trigger_y = defenseLineY + 0.34f;
        m_bombs_active = new bool[m_bomb_count];
        m_bomb_visuals = new GameObject[m_bomb_count];

        for (int lane = 0; lane < m_bomb_count; lane++)
        {
            m_bombs_active[lane] = true;
            float x = m_lane_min_x + (lane + 0.5f) * m_lane_width;
            GameObject bomb = new GameObject($"DefenseBomb{lane + 1}");
            bomb.transform.SetParent(stageRoot, false);
            bomb.transform.position = new Vector3(x, m_trigger_y, -0.05f);
            SpriteRenderer renderer = bomb.AddComponent<SpriteRenderer>();
            renderer.sprite = GetMineSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 3;

            GameObject haloObject = new GameObject("RedWarningGlow", typeof(SpriteRenderer));
            haloObject.transform.SetParent(bomb.transform, false);
            SpriteRenderer halo = haloObject.GetComponent<SpriteRenderer>();
            halo.sprite = GetGlowSprite();
            halo.color = new Color(1f, 0.025f, 0.01f, 0.52f);
            halo.sortingOrder = 2;
            haloObject.transform.localScale = Vector3.one * 2.1f;

            DefenseMinePulse pulse = bomb.AddComponent<DefenseMinePulse>();
            pulse.Initialize(renderer, halo, lane * 1.17f);
            bomb.transform.localScale = Vector3.one * 0.30f;
            m_bomb_visuals[lane] = bomb;
        }
    }

    private static Sprite GetMineSprite()
    {
        if (s_mine_sprite) return s_mine_sprite;
        const int size = 64;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "RuntimeDefenseMine", filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 offset = new Vector2(x, y) - center;
            float radius = offset.magnitude;
            float spoke = Mathf.Abs(Mathf.Sin(Mathf.Atan2(offset.y, offset.x) * 4f));
            Color color = Color.clear;
            if (radius <= 25f) color = radius > 21f ? new Color(0.10f, 0.22f, 0.29f, 1f) : new Color(0.035f, 0.09f, 0.14f, 1f);
            if (radius > 24f && radius <= 30f && spoke < 0.30f) color = new Color(0.08f, 0.17f, 0.23f, 1f);
            if (radius >= 10f && radius <= 12f) color = new Color(0.12f, 0.72f, 0.82f, 1f);
            if (radius <= 5f) color = new Color(0.16f, 0.92f, 1f, 1f);
            if (radius >= 17f && radius <= 19f && spoke < 0.18f) color = new Color(0.95f, 0.42f, 0.07f, 1f);
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        s_mine_sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return s_mine_sprite;
    }

    private static Sprite GetGlowSprite()
    {
        if (s_glow_sprite) return s_glow_sprite;
        const int size = 96;
        float center = (size - 1) * 0.5f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "RuntimeDefenseMineGlow", filterMode = FilterMode.Bilinear };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
            float alpha = radius >= 1f ? 0f : Mathf.Pow(1f - radius, 2.2f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        s_glow_sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return s_glow_sprite;
    }
}
