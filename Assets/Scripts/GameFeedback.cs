using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameFeedback : MonoBehaviour
{
    private const string AudioRoot = "Task5/Audio/";
    private AudioSource m_audio_source;
    private AudioSource m_music_source;
    private Camera m_camera;
    private Transform m_effect_root;
    private Vector3 m_camera_home;
    private Coroutine m_camera_shake;
    private Coroutine m_defense_flash;
    private Image m_edge_flash;
    private Text m_defense_text;
    private Transform m_pool_root;
    private ComponentPool<PooledSpriteEffect> m_hit_effect_pool;
    private ComponentPool<PooledSpriteEffect> m_death_effect_pool;
    private AudioClip m_shoot, m_hit, m_enemy_death, m_breach, m_level_up, m_upgrade_select, m_game_over, m_bgm;
    private static Sprite s_white_sprite;
    private static Font s_runtime_font;
    private static Sprite[] s_tree_sprites;
    private static Sprite[] s_decor_sprites;

    public Vector3 CameraShakeWorldOffset
    {
        get
        {
            if (!m_camera) return Vector3.zero;
            Vector3 localOffset = m_camera.transform.localPosition - m_camera_home;
            Transform parent = m_camera.transform.parent;
            return parent ? parent.TransformVector(localOffset) : localOffset;
        }
    }

    public void Initialize(Camera gameCamera, Transform stageRoot, Transform uiRoot, Text defenseText, float defenseLineY)
    {
        ClearTransient();
        m_camera = gameCamera;
        m_camera_home = m_camera ? m_camera.transform.localPosition : Vector3.zero;
        m_defense_text = defenseText;
        if (!m_audio_source)
        {
            m_audio_source = gameObject.AddComponent<AudioSource>();
            m_audio_source.playOnAwake = false;
            m_audio_source.spatialBlend = 0f;
        }
        if (!m_music_source)
        {
            m_music_source = gameObject.AddComponent<AudioSource>();
            m_music_source.playOnAwake = false;
            m_music_source.spatialBlend = 0f;
            m_music_source.loop = true;
            m_music_source.volume = 0.12f;
        }
        m_shoot = Resources.Load<AudioClip>(AudioRoot + "shoot");
        m_hit = Resources.Load<AudioClip>(AudioRoot + "hit");
        m_enemy_death = Resources.Load<AudioClip>(AudioRoot + "enemy_death");
        m_breach = Resources.Load<AudioClip>(AudioRoot + "breach");
        m_level_up = Resources.Load<AudioClip>(AudioRoot + "level_up");
        m_upgrade_select = Resources.Load<AudioClip>(AudioRoot + "upgrade_select");
        m_game_over = Resources.Load<AudioClip>(AudioRoot + "game_over");
        m_bgm = Resources.Load<AudioClip>(AudioRoot + "bgm");

        if (!m_effect_root)
        {
            GameObject root = new GameObject("Task5FeedbackObjects");
            root.transform.SetParent(transform, false);
            m_effect_root = root.transform;
        }
        EnsureEffectPools();
        if (!m_edge_flash && uiRoot)
        {
            GameObject edge = new GameObject("BreachFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.SetParent(uiRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_edge_flash = edge.GetComponent<Image>();
            m_edge_flash.color = Color.clear;
            m_edge_flash.raycastTarget = false;
            edge.transform.SetAsFirstSibling();
        }
        BuildArena(stageRoot, defenseLineY);
    }

    public void PlayShoot(Vector3 position, Vector3 direction)
    {
        Play(m_shoot, 0.25f);
        StartCoroutine(FlashRoutine(position, direction, new Color(1f, 0.86f, 0.25f), 0.055f, 0.25f));
    }
    public void PlayHit(Vector3 position)
    {
        Play(m_hit, 0.20f);
        SpawnBurst(position, new Color(1f, 0.82f, 0.28f), 4, 0.10f, 0.13f, m_hit_effect_pool);
    }
    public void PlayShieldBlock(Vector3 position, int remainingBlocks)
    {
        Play(m_hit, 0.24f);
        SpawnBurst(position, new Color(0.22f, 0.88f, 1f), 7, 0.12f, 0.18f, m_hit_effect_pool);
        StartCoroutine(ShieldBlockTextRoutine(position, remainingBlocks));
    }
    public void PlayEnemyDeath(Vector3 position)
    {
        Play(m_enemy_death, 0.35f);
        SpawnBurst(position, new Color(0.45f, 1f, 0.38f), 7, 0.22f, 0.20f, m_death_effect_pool);
        Shake(0.07f, 0.045f);
    }
    public void PlayBreach(Vector3 position)
    {
        Play(m_breach, 0.50f);
        SpawnBurst(position, new Color(1f, 0.18f, 0.12f), 8, 0.18f, 0.20f);
        Shake(0.13f, 0.10f);
        if (m_defense_flash != null) StopCoroutine(m_defense_flash);
        m_defense_flash = StartCoroutine(DefenseFlashRoutine());
    }
    public void PlayLevelUp() => Play(m_level_up, 0.50f);
    public void PlayUpgradeSelect() => Play(m_upgrade_select, 0.40f);
    public void PlayGameOver()
    {
        if (m_music_source) m_music_source.Stop();
        Play(m_game_over, 0.55f);
        Shake(0.18f, 0.13f);
    }

    public void ShowDamage(Vector3 position, float damage, bool critical)
    {
        StartCoroutine(DamageNumberRoutine(position, damage, critical));
    }

    public void PlayLightning(Vector3 targetPosition)
    {
        StartCoroutine(LightningRoutine(targetPosition));
        SpawnBurst(targetPosition, new Color(0.45f, 0.85f, 1f), 8, 0.18f, 0.25f);
    }

    public void PlayBombDetonation(float centerX, float laneWidth, float triggerY)
    {
        Play(m_breach, 0.42f);
        StartCoroutine(BombLaneRoutine(centerX, laneWidth));
        SpawnBurst(new Vector3(centerX, triggerY, -0.1f), new Color(1f, 0.58f, 0.12f), 12, 0.28f, 0.45f);
        Shake(0.15f, 0.11f);
    }

    public void PlayGameplayMusic()
    {
        if (!m_music_source || !m_bgm) return;
        if (m_music_source.clip != m_bgm) m_music_source.clip = m_bgm;
        if (!m_music_source.isPlaying) m_music_source.Play();
    }

    public void ClearTransient()
    {
        StopAllCoroutines();
        m_camera_shake = null;
        m_defense_flash = null;
        if (m_camera) m_camera.transform.localPosition = m_camera_home;
        if (m_edge_flash) m_edge_flash.color = Color.clear;
        if (m_defense_text) m_defense_text.color = Color.white;
        m_hit_effect_pool?.ReleaseAllActive();
        m_death_effect_pool?.ReleaseAllActive();
        if (m_effect_root)
            for (int index = m_effect_root.childCount - 1; index >= 0; index--)
                Destroy(m_effect_root.GetChild(index).gameObject);
    }
    public void StopAudio()
    {
        if (m_audio_source) m_audio_source.Stop();
        if (m_music_source) m_music_source.Stop();
    }
    private void Play(AudioClip clip, float volume)
    {
        if (clip && m_audio_source) m_audio_source.PlayOneShot(clip, volume);
    }

    private void Shake(float duration, float amplitude)
    {
        if (!m_camera) return;
        if (m_camera_shake != null) StopCoroutine(m_camera_shake);
        m_camera.transform.localPosition = m_camera_home;
        m_camera_shake = StartCoroutine(ShakeRoutine(duration, amplitude));
    }
    private IEnumerator ShakeRoutine(float duration, float amplitude)
    {
        float elapsed = 0f;
        while (elapsed < duration && m_camera)
        {
            elapsed += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * amplitude * falloff;
            m_camera.transform.localPosition = m_camera_home + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
        if (m_camera) m_camera.transform.localPosition = m_camera_home;
        m_camera_shake = null;
    }
    private IEnumerator DefenseFlashRoutine()
    {
        Color original = m_defense_text ? m_defense_text.color : Color.white;
        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            if (m_edge_flash) m_edge_flash.color = new Color(1f, 0.04f, 0.02f, pulse * 0.23f);
            if (m_defense_text) m_defense_text.color = Color.Lerp(original, new Color(1f, 0.22f, 0.16f), pulse);
            yield return null;
        }
        if (m_edge_flash) m_edge_flash.color = Color.clear;
        if (m_defense_text) m_defense_text.color = original;
        m_defense_flash = null;
    }
    private IEnumerator FlashRoutine(Vector3 position, Vector3 direction, Color color, float duration, float size)
    {
        GameObject flash = CreateSpriteObject("MuzzleFlash", position, color, 5);
        flash.transform.up = direction;
        flash.transform.localScale = new Vector3(size, size * 1.8f, 1f);
        float elapsed = 0f;
        while (elapsed < duration && flash)
        {
            elapsed += Time.deltaTime;
            flash.transform.localScale *= 0.72f;
            yield return null;
        }
        if (flash) Destroy(flash);
    }
    private void SpawnBurst(Vector3 position, Color color, int count, float duration, float radius,
        ComponentPool<PooledSpriteEffect> pool = null)
    {
        for (int index = 0; index < count; index++)
        {
            float angle = (360f / count) * index + Random.Range(-12f, 12f);
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            StartCoroutine(BurstPieceRoutine(position, direction, color, duration, radius, pool));
        }
    }
    private IEnumerator BurstPieceRoutine(Vector3 position, Vector3 direction, Color color, float duration, float radius,
        ComponentPool<PooledSpriteEffect> pool)
    {
        PooledSpriteEffect pooledEffect = pool?.Get();
        GameObject piece = pooledEffect
            ? pooledEffect.gameObject
            : CreateSpriteObject("FeedbackParticle", position, color, 4);
        piece.name = pool == m_death_effect_pool ? "DeathEffect" : pool == m_hit_effect_pool ? "HitEffect" : "FeedbackParticle";
        piece.transform.position = position;
        piece.transform.localScale = new Vector3(0.055f, 0.13f, 1f);
        piece.transform.up = direction;
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = 4;
        float elapsed = 0f;
        while (elapsed < duration && piece)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            piece.transform.position += direction * (radius / duration) * delta;
            Color faded = color;
            faded.a = 1f - Mathf.Clamp01(elapsed / duration);
            renderer.color = faded;
            yield return null;
        }
        if (pooledEffect) pool.Release(pooledEffect);
        else if (piece) Destroy(piece);
    }

    private IEnumerator DamageNumberRoutine(Vector3 position, float damage, bool critical)
    {
        if (!s_runtime_font) s_runtime_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject numberObject = new GameObject(critical ? "CriticalDamage" : "Damage", typeof(TextMesh));
        numberObject.transform.SetParent(m_effect_root, false);
        numberObject.transform.position = position + new Vector3(Random.Range(-0.08f, 0.08f), 0.32f, -0.1f);
        TextMesh text = numberObject.GetComponent<TextMesh>();
        text.font = s_runtime_font;
        text.text = Mathf.Approximately(damage, Mathf.Round(damage)) ? $"{Mathf.RoundToInt(damage)}" : $"{damage:0.0}";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = critical ? 82 : 64;
        text.characterSize = critical ? 0.052f : 0.044f;
        text.fontStyle = critical ? FontStyle.Bold : FontStyle.Normal;
        Color baseColor = critical ? new Color(1f, 0.55f, 0.08f) : Color.white;
        text.color = baseColor;
        MeshRenderer renderer = numberObject.GetComponent<MeshRenderer>();
        if (s_runtime_font) renderer.sharedMaterial = s_runtime_font.material;
        renderer.sortingOrder = critical ? 12 : 11;

        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration && numberObject)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            numberObject.transform.position += Vector3.up * 0.55f * delta;
            Color faded = baseColor;
            faded.a = 1f - Mathf.Clamp01(elapsed / duration);
            text.color = faded;
            yield return null;
        }
        if (numberObject) Destroy(numberObject);
    }

    private IEnumerator ShieldBlockTextRoutine(Vector3 position, int remainingBlocks)
    {
        if (!s_runtime_font) s_runtime_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject textObject = new GameObject("ShieldBlock", typeof(TextMesh));
        textObject.transform.SetParent(m_effect_root, false);
        textObject.transform.position = position + new Vector3(0f, 0.38f, -0.1f);
        TextMesh text = textObject.GetComponent<TextMesh>();
        text.font = s_runtime_font;
        text.text = remainingBlocks > 0 ? $"BLOCK  {remainingBlocks}" : "SHIELD BREAK";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 72;
        text.characterSize = 0.042f;
        text.fontStyle = FontStyle.Bold;
        Color baseColor = remainingBlocks > 0 ? new Color(0.25f, 0.90f, 1f) : new Color(1f, 0.72f, 0.18f);
        text.color = baseColor;
        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (s_runtime_font) renderer.sharedMaterial = s_runtime_font.material;
        renderer.sortingOrder = 13;

        float elapsed = 0f;
        const float duration = 0.42f;
        while (elapsed < duration && textObject)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            textObject.transform.position += Vector3.up * 0.42f * delta;
            Color faded = baseColor;
            faded.a = 1f - Mathf.Clamp01(elapsed / duration);
            text.color = faded;
            yield return null;
        }
        if (textObject) Destroy(textObject);
    }

    private IEnumerator LightningRoutine(Vector3 targetPosition)
    {
        float topY = m_camera
            ? m_camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(m_camera.transform.position.z))).y
            : targetPosition.y + 5f;
        float height = Mathf.Max(0.2f, topY - targetPosition.y);
        GameObject bolt = CreateSpriteObject("LightningBolt",
            new Vector3(targetPosition.x, targetPosition.y + height * 0.5f, -0.15f),
            new Color(0.55f, 0.90f, 1f), 9);
        bolt.transform.localScale = new Vector3(0.075f, height, 1f);
        SpriteRenderer renderer = bolt.GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        const float duration = 0.16f;
        while (elapsed < duration && bolt)
        {
            elapsed += Time.deltaTime;
            bolt.transform.position = new Vector3(targetPosition.x + Random.Range(-0.035f, 0.035f),
                targetPosition.y + height * 0.5f, -0.15f);
            Color color = renderer.color;
            color.a = 1f - Mathf.Clamp01(elapsed / duration);
            renderer.color = color;
            yield return null;
        }
        if (bolt) Destroy(bolt);
    }
    private IEnumerator BombLaneRoutine(float centerX, float laneWidth)
    {
        if (!m_camera) yield break;
        float distance = Mathf.Abs(m_camera.transform.position.z);
        Vector3 bottom = m_camera.ViewportToWorldPoint(new Vector3(0.5f, 0f, distance));
        Vector3 top = m_camera.ViewportToWorldPoint(new Vector3(0.5f, 1f, distance));
        float height = Mathf.Abs(top.y - bottom.y);
        GameObject flash = CreateSpriteObject("BombLaneBlast",
            new Vector3(centerX, (top.y + bottom.y) * 0.5f, -0.12f), new Color(1f, 0.62f, 0.10f, 0.72f), 8);
        flash.transform.localScale = new Vector3(laneWidth * 0.88f, height, 1f);
        SpriteRenderer renderer = flash.GetComponent<SpriteRenderer>();
        float elapsed = 0f;
        const float duration = 0.22f;
        while (elapsed < duration && flash)
        {
            elapsed += Time.deltaTime;
            Color color = renderer.color;
            color.a = (1f - Mathf.Clamp01(elapsed / duration)) * 0.72f;
            renderer.color = color;
            yield return null;
        }
        if (flash) Destroy(flash);
    }
    private GameObject CreateSpriteObject(string objectName, Vector3 position, Color color, int order)
    {
        GameObject result = new GameObject(objectName, typeof(SpriteRenderer));
        result.transform.SetParent(m_effect_root, false);
        result.transform.position = position;
        SpriteRenderer renderer = result.GetComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return result;
    }

    private void EnsureEffectPools()
    {
        if (!m_pool_root)
        {
            GameObject root = new GameObject("Task6EffectPools");
            root.transform.SetParent(transform, false);
            m_pool_root = root.transform;
        }
        if (m_hit_effect_pool == null)
            m_hit_effect_pool = new ComponentPool<PooledSpriteEffect>(
                () => CreatePooledEffect("HitEffect"), m_pool_root, 20);
        if (m_death_effect_pool == null)
            m_death_effect_pool = new ComponentPool<PooledSpriteEffect>(
                () => CreatePooledEffect("DeathEffect"), m_pool_root, 16);
    }

    private PooledSpriteEffect CreatePooledEffect(string objectName)
    {
        GameObject effect = new GameObject(objectName, typeof(SpriteRenderer), typeof(PooledSpriteEffect));
        effect.transform.SetParent(m_pool_root, false);
        effect.GetComponent<SpriteRenderer>().sprite = GetWhiteSprite();
        return effect.GetComponent<PooledSpriteEffect>();
    }

    private static Sprite GetWhiteSprite()
    {
        if (!s_white_sprite)
            s_white_sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), 4f);
        return s_white_sprite;
    }
    private void BuildArena(Transform stageRoot, float defenseLineY)
    {
        if (!stageRoot || !m_camera || !m_camera.orthographic) return;
        float height = m_camera.orthographicSize * 2f;
        float width = height * m_camera.aspect;
        Vector3 center = m_camera.transform.position;
        center.z = 1f;
        GameObject ground = CreateSpriteObject("ArenaGround", center, new Color(0.055f, 0.08f, 0.10f), -20);
        ground.transform.SetParent(stageRoot, true);
        ground.transform.localScale = new Vector3(width, height, 1f);
        float bottom = center.y - height * 0.5f;
        for (int index = 1; index < 8; index++)
        {
            float y = bottom + height * index / 8f;
            GameObject line = CreateSpriteObject("GroundGrid", new Vector3(center.x, y, 0.8f),
                new Color(0.12f, 0.18f, 0.20f, 0.45f), -19);
            line.transform.SetParent(stageRoot, true);
            line.transform.localScale = new Vector3(width, 0.018f, 1f);
        }
        BuildSideTrees(stageRoot, center.x, bottom, width, height);
        BuildEdgeDecorations(stageRoot, center.x, bottom, width, height);
        GameObject defense = CreateSpriteObject("DefenseLine", new Vector3(center.x, defenseLineY, 0.7f),
            new Color(0.90f, 0.16f, 0.12f, 0.85f), -5);
        defense.transform.SetParent(stageRoot, true);
        defense.transform.localScale = new Vector3(width, 0.045f, 1f);
    }

    private void BuildSideTrees(Transform stageRoot, float centerX, float bottom, float width, float height)
    {
        if (s_tree_sprites == null)
        {
            s_tree_sprites = new[]
            {
                Resources.Load<Sprite>("Task5/Environment/tree_pine"),
                Resources.Load<Sprite>("Task5/Environment/tree_column"),
                Resources.Load<Sprite>("Task5/Environment/tree_round"),
                Resources.Load<Sprite>("Task5/Environment/tree_broad")
            };
        }

        const int treesPerSide = 7;
        float edgeInset = 0.30f;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int index = 0; index < treesPerSide; index++)
            {
                float y = bottom + height * (index + 0.55f) / treesPerSide;
                float stagger = (index % 2 == 0 ? 0.08f : -0.10f) * side;
                float x = centerX + side * (width * 0.5f - edgeInset) + stagger;
                GameObject tree = new GameObject($"SideTree_{side}_{index}", typeof(SpriteRenderer));
                tree.transform.SetParent(stageRoot, false);
                tree.transform.position = new Vector3(x, y, 0.55f);
                float scale = 0.56f + (index % 3) * 0.08f;
                tree.transform.localScale = Vector3.one * scale;
                tree.transform.localRotation = Quaternion.Euler(0f, 0f, side < 0 ? -7f : 7f);
                SpriteRenderer renderer = tree.GetComponent<SpriteRenderer>();
                renderer.sprite = s_tree_sprites[index % s_tree_sprites.Length];
                renderer.color = new Color(0.72f, 0.78f, 0.78f, 0.82f);
                renderer.sortingOrder = -12;
            }
        }
    }

    private void BuildEdgeDecorations(Transform stageRoot, float centerX, float bottom, float width, float height)
    {
        if (s_decor_sprites == null)
        {
            s_decor_sprites = new[]
            {
                Resources.Load<Sprite>("Task5/Environment/decor_crate"),
                Resources.Load<Sprite>("Task5/Environment/decor_rubble"),
                Resources.Load<Sprite>("Task5/Environment/decor_sandbag")
            };
        }

        const int propsPerSide = 4;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int index = 0; index < propsPerSide; index++)
            {
                Sprite sprite = s_decor_sprites[(index + (side > 0 ? 1 : 0)) % s_decor_sprites.Length];
                if (!sprite) continue;
                float x = centerX + side * (width * 0.5f - 0.58f - (index % 2) * 0.16f);
                float y = bottom + height * (index + 1.15f) / (propsPerSide + 1.2f);
                GameObject prop = new GameObject($"EdgeProp_{side}_{index}", typeof(SpriteRenderer));
                prop.transform.SetParent(stageRoot, false);
                prop.transform.position = new Vector3(x, y, 0.52f);
                prop.transform.localScale = Vector3.one * (0.52f + index * 0.035f);
                prop.transform.localRotation = Quaternion.Euler(0f, 0f, side * (8f + index * 11f));
                SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(0.62f, 0.70f, 0.72f, 0.72f);
                renderer.sortingOrder = -11;
            }
        }
    }
}
