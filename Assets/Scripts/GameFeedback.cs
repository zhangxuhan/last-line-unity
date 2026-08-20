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
    private AudioClip m_shoot, m_hit, m_enemy_death, m_breach, m_level_up, m_upgrade_select, m_game_over, m_bgm;
    private static Sprite s_white_sprite;
    private static Font s_runtime_font;

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
        SpawnBurst(position, new Color(1f, 0.82f, 0.28f), 4, 0.10f, 0.13f);
    }
    public void PlayEnemyDeath(Vector3 position)
    {
        Play(m_enemy_death, 0.35f);
        SpawnBurst(position, new Color(0.45f, 1f, 0.38f), 7, 0.22f, 0.20f);
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
    private void SpawnBurst(Vector3 position, Color color, int count, float duration, float radius)
    {
        for (int index = 0; index < count; index++)
        {
            float angle = (360f / count) * index + Random.Range(-12f, 12f);
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
            StartCoroutine(BurstPieceRoutine(position, direction, color, duration, radius));
        }
    }
    private IEnumerator BurstPieceRoutine(Vector3 position, Vector3 direction, Color color, float duration, float radius)
    {
        GameObject piece = CreateSpriteObject("FeedbackParticle", position, color, 4);
        piece.transform.localScale = new Vector3(0.055f, 0.13f, 1f);
        piece.transform.up = direction;
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
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
        if (piece) Destroy(piece);
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
        if (!s_white_sprite)
            s_white_sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f), 4f);
        GameObject result = new GameObject(objectName, typeof(SpriteRenderer));
        result.transform.SetParent(m_effect_root, false);
        result.transform.position = position;
        SpriteRenderer renderer = result.GetComponent<SpriteRenderer>();
        renderer.sprite = s_white_sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return result;
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
        GameObject defense = CreateSpriteObject("DefenseLine", new Vector3(center.x, defenseLineY, 0.7f),
            new Color(0.90f, 0.16f, 0.12f, 0.85f), -5);
        defense.transform.SetParent(stageRoot, true);
        defense.transform.localScale = new Vector3(width, 0.045f, 1f);
    }
}
