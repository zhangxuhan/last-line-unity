using System.Collections;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PortraitCameraViewport : MonoBehaviour
{
    private const float TargetAspect = 3f / 4f;
    private Camera m_camera;
    private int m_last_width;
    private int m_last_height;

    private void Awake() { m_camera = GetComponent<Camera>(); ApplyViewport(); }
    private void Update()
    {
        if (Screen.width != m_last_width || Screen.height != m_last_height) ApplyViewport();
    }
    private void ApplyViewport()
    {
        if (!m_camera || Screen.width <= 0 || Screen.height <= 0) return;
        m_last_width = Screen.width;
        m_last_height = Screen.height;
        float screenAspect = (float)Screen.width / Screen.height;
        if (screenAspect > TargetAspect)
        {
            float width = TargetAspect / screenAspect;
            m_camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
        else
        {
            float height = screenAspect / TargetAspect;
            m_camera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }
    }
}

public sealed class DefenseMinePulse : MonoBehaviour
{
    private SpriteRenderer m_core;
    private SpriteRenderer m_halo;
    private float m_phase;

    public void Initialize(SpriteRenderer core, SpriteRenderer halo, float phase)
    {
        m_core = core;
        m_halo = halo;
        m_phase = phase;
    }

    private void Update()
    {
        if (!m_halo || !StageLoop.Instance || !StageLoop.Instance.IsPlaying) return;
        float pulse = 0.5f + Mathf.Sin(Time.time * 4.2f + m_phase) * 0.5f;
        m_halo.transform.localScale = Vector3.one * Mathf.Lerp(1.90f, 2.70f, pulse);
        m_halo.color = new Color(1f, 0.025f, 0.01f, Mathf.Lerp(0.42f, 0.82f, pulse));
        if (m_core) m_core.color = Color.Lerp(Color.white, new Color(1f, 0.38f, 0.30f), 0.10f + pulse * 0.30f);
    }
}

public class StageLoop : MonoBehaviour
{
    public enum GameState
    {
        Title,
        Playing,
        LevelUp,
        Skills,
        GameOver
    }

    public static StageLoop Instance { get; private set; }

    [SerializeField] private TitleLoop m_title_loop;

    [Header("Layout")]
    [SerializeField] private Transform m_stage_transform;
    [SerializeField] private Text m_stage_score_text;
    [SerializeField] private Camera m_game_camera;
    [SerializeField, Min(0f), Tooltip("Distance above the camera bottom used for the player.")]
    private float m_player_bottom_offset = 0.8f;
    [SerializeField, Min(0f), Tooltip("Distance above the camera bottom used for the defense line.")]
    private float m_defense_bottom_offset = 1.6f;

    [Header("Prefab")]
    [SerializeField] private Player m_prefab_player;
    [SerializeField] private EnemySpawner m_prefab_enemy_spawner;

    [Header("Defense")]
    [SerializeField, Min(1)] private int m_max_breaches = 3;
    [SerializeField, Range(1, 8)] private int m_defense_bomb_count = 5;

    [Header("Difficulty")]
    [SerializeField, Min(0.1f)] private float m_base_spawn_interval = 1.4f;
    [SerializeField, Min(0.1f)] private float m_min_spawn_interval = 0.5f;
    [SerializeField, Min(1)] private int m_base_enemy_hp = 30;
    [SerializeField, Min(0f)] private float m_base_enemy_speed = 0.9f;
    [SerializeField, Min(1f)] private float m_difficulty_stage_seconds = 30f;
    [SerializeField, Min(1f)] private float m_hp_multiplier_per_stage = 1.2f;
    [SerializeField, Min(1f)] private float m_speed_multiplier_per_stage = 1.08f;
    [SerializeField, Range(0.01f, 1f)] private float m_spawn_interval_multiplier_per_stage = 0.9f;

    private Coroutine m_stage_coroutine;
    private Coroutine m_upgrade_panel_animation;
    private GameFeedback m_feedback;
    private GameObject m_stage_ui_root;
    private Text m_defense_text;
    private Text m_time_text;
    private Text m_level_text;
    private Text m_experience_text;
    private Text m_game_over_text;
    private Toggle m_auto_fire_toggle;
    private bool m_auto_fire_enabled;
    private RectTransform m_experience_fill_rect;
    private Text m_experience_percent_text;
    private GameObject m_upgrade_panel;
    private Text m_upgrade_header_text;
    private GameObject m_skills_panel;
    private Text m_skills_content_text;
    private readonly Button[] m_upgrade_buttons = new Button[3];
    private readonly Text[] m_upgrade_button_texts = new Text[3];
    private readonly Image[] m_upgrade_button_icons = new Image[3];
    private readonly Outline[] m_upgrade_button_outlines = new Outline[3];
    private readonly WeaponUpgradeChoice[] m_current_upgrade_choices = new WeaponUpgradeChoice[3];
    private PlayerProgression m_progression;
    private Player m_player;
    private System.Random m_upgrade_random;
    private int m_current_upgrade_count;
    private float m_accept_upgrade_input_time;
    private bool m_upgrade_selection_locked;
    private int m_game_score;
    private int m_breach_count;
    private float m_survival_time;
    private bool[] m_defense_bombs_active;
    private GameObject[] m_defense_bomb_visuals;
    private float m_bomb_lane_min_x;
    private float m_bomb_lane_width;
    private float m_bomb_trigger_y;
    private bool m_resolving_area_attack;
    private Sprite[] m_upgrade_icons;
    private static Sprite s_tick_sprite;
    private static Material s_upgrade_icon_material;
    private static Sprite s_defense_mine_sprite;
    private static Sprite s_defense_mine_glow_sprite;

    public GameState State { get; private set; } = GameState.Title;
    public bool IsPlaying => State == GameState.Playing;
    public GameFeedback Feedback => m_feedback;
    public float DefenseLineY => GetCameraBottom() + m_defense_bottom_offset;
    public float SurvivalTime => m_survival_time;
    public int Level => m_progression != null ? m_progression.Level : PlayerProgression.InitialLevel;
    public int CurrentExperience => m_progression != null ? m_progression.CurrentExperience : 0;
    public int RequiredExperience => m_progression != null
        ? m_progression.RequiredExperience
        : PlayerProgression.InitialExperienceRequirement;
    public int KillCount => m_progression != null ? m_progression.KillCount : 0;
    public int PendingUpgradeCount => m_progression != null ? m_progression.PendingUpgradeCount : 0;
    public event Action<int> OnLevelUp;
    public float CurrentSpawnInterval
    {
        get
        {
            float interval = m_base_spawn_interval
                * Mathf.Pow(m_spawn_interval_multiplier_per_stage, GetDifficultyProgress());
            return Mathf.Max(m_min_spawn_interval, interval);
        }
    }

    private void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        m_progression = new PlayerProgression();
        m_upgrade_random = new System.Random();
        if (!m_game_camera) m_game_camera = Camera.main;
        if (m_game_camera && !m_game_camera.GetComponent<PortraitCameraViewport>())
            m_game_camera.gameObject.AddComponent<PortraitCameraViewport>();
        CreateRuntimeUi();
        m_feedback = GetComponent<GameFeedback>();
        if (!m_feedback) m_feedback = gameObject.AddComponent<GameFeedback>();
        SetState(GameState.Title);
    }

    public void StartStageLoop()
    {
        Time.timeScale = 1f;
        StopStageCoroutine();
        SetupStage();
        m_stage_coroutine = StartCoroutine(StageCoroutine());
    }

    private IEnumerator StageCoroutine()
    {
        while (State != GameState.Title)
        {
            if (State == GameState.Playing)
            {
                m_survival_time += Time.deltaTime;
                RefreshHud();

                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    OpenSkillsPanel();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ReturnToTitle();
                    yield break;
                }
            }
            else if (State == GameState.GameOver)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    SetupStage();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ReturnToTitle();
                    yield break;
                }
            }
            else if (State == GameState.LevelUp)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ReturnToTitle();
                    yield break;
                }

                if (Time.unscaledTime >= m_accept_upgrade_input_time)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgrade(0);
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgrade(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgrade(2);
                }
            }
            else if (State == GameState.Skills)
            {
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)) CloseSkillsPanel();
            }

            yield return null;
        }

        m_stage_coroutine = null;
    }

    private void SetupStage()
    {
        CleanupStageObjects();
        Time.timeScale = 1f;
        if (!m_game_camera) m_game_camera = Camera.main;
        m_feedback.StopAudio();
        m_feedback.Initialize(m_game_camera, m_stage_transform, m_stage_ui_root.transform, m_defense_text, DefenseLineY);
        m_feedback.PlayGameplayMusic();
        CreateDefenseBombs();

        m_game_score = 0;
        m_breach_count = 0;
        m_survival_time = 0f;
        m_progression.Reset();
        SetState(GameState.Playing);
        RefreshHud();
        RefreshProgressionUi();

        m_player = Instantiate(m_prefab_player, m_stage_transform);
        m_player.transform.position = new Vector3(0f, GetCameraBottom() + m_player_bottom_offset, 0f);
        m_player.InitializeForStage();
        m_player.SetAutoFire(m_auto_fire_enabled);

        EnemySpawner spawner = Instantiate(m_prefab_enemy_spawner, m_stage_transform);
        spawner.Initialize(this, m_game_camera, m_stage_transform);
    }

    public void GetCurrentEnemyStats(out int maxHp, out float moveSpeed)
    {
        float progress = GetDifficultyProgress();
        maxHp = Mathf.Max(1, Mathf.CeilToInt(m_base_enemy_hp * Mathf.Pow(m_hp_multiplier_per_stage, progress)));
        moveSpeed = Mathf.Max(0f, m_base_enemy_speed * Mathf.Pow(m_speed_multiplier_per_stage, progress));
    }

    public int DifficultyStage => GetDifficultyStage();

    public void RegisterEnemyKilled(int scoreReward, int experienceReward)
    {
        if (!IsPlaying || m_progression == null) return;

        if (scoreReward > 0)
            m_game_score = (int)Math.Min((long)m_game_score + scoreReward, int.MaxValue);

        int previousLevel = m_progression.Level;
        int levelUpCount = m_progression.RegisterKill(experienceReward);
        RefreshHud();
        RefreshProgressionUi();

        for (int index = 1; index <= levelUpCount; index++)
            OnLevelUp?.Invoke(previousLevel + index);

        if (levelUpCount > 0 && IsPlaying && !m_resolving_area_attack) EnterLevelUp();
    }

    public bool TryConsumePendingUpgrade()
    {
        return m_progression != null && m_progression.TryConsumePendingUpgrade();
    }

    private void EnterLevelUp()
    {
        if (!IsPlaying || PendingUpgradeCount <= 0 || !m_player || m_player.RuntimeWeapon == null) return;

        SetState(GameState.LevelUp);
        m_player.StopRunning();
        Time.timeScale = 0f;
        m_feedback.PlayLevelUp();
        PrepareUpgradeChoices();
    }

    private void PrepareUpgradeChoices()
    {
        if (State != GameState.LevelUp || !m_player || m_player.RuntimeWeapon == null) return;

        var choices = WeaponUpgradeSystem.GetRandomChoices(m_player.RuntimeWeapon, 3, m_upgrade_random);
        m_current_upgrade_count = choices.Count;
        m_upgrade_selection_locked = false;
        m_accept_upgrade_input_time = float.PositiveInfinity;
        if (m_upgrade_panel_animation != null) StopCoroutine(m_upgrade_panel_animation);
        m_upgrade_panel_animation = StartCoroutine(AnimateUpgradePanel());
        if (m_upgrade_header_text) m_upgrade_header_text.text = $"LEVEL UP\nLevel {Level}\nChoose one upgrade";

        for (int index = 0; index < m_upgrade_buttons.Length; index++)
        {
            bool visible = index < choices.Count;
            m_upgrade_buttons[index].gameObject.SetActive(visible);
            if (!visible) continue;
            m_upgrade_buttons[index].transform.localScale = Vector3.one * 0.84f;

            WeaponUpgradeChoice choice = choices[index];
            m_current_upgrade_choices[index] = choice;
            if (m_upgrade_button_icons[index]) m_upgrade_button_icons[index].sprite = GetUpgradeIcon(choice.Type);
            WeaponUpgradeOption option = WeaponUpgradeSystem.BuildOption(choice, m_player.RuntimeWeapon);
            m_upgrade_button_texts[index].text =
                $"<size=22><b><color=#E8FBFF>[{index + 1}] [{choice.Rarity}] {option.Name.ToUpperInvariant()}</color></b></size>\n" +
                $"<size=17><b><color=#FFFFFF>{option.Description}</color></b></size>\n" +
                $"<size=18><b><color=#FFE08A>{option.ValueChange}</color></b></size>";
            Color rarityColor = GetRarityColor(choice.Rarity);
            ColorBlock colors = m_upgrade_buttons[index].colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, rarityColor, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            m_upgrade_buttons[index].colors = colors;
            m_upgrade_buttons[index].image.color = rarityColor;
            if (m_upgrade_button_outlines[index])
            {
                m_upgrade_button_outlines[index].effectColor = Color.Lerp(rarityColor, Color.white,
                    choice.Rarity == UpgradeRarity.UR ? 0.28f : 0.12f);
                float outlineSize = choice.Rarity == UpgradeRarity.R ? 1f
                    : choice.Rarity == UpgradeRarity.SR ? 2f : choice.Rarity == UpgradeRarity.SSR ? 3f : 4f;
                m_upgrade_button_outlines[index].effectDistance = new Vector2(outlineSize, -outlineSize);
            }
            m_upgrade_buttons[index].interactable = false;
        }
    }

    private void SelectUpgrade(int index)
    {
        if (State != GameState.LevelUp || m_upgrade_selection_locked
            || Time.unscaledTime < m_accept_upgrade_input_time
            || index < 0 || index >= m_current_upgrade_count || !m_upgrade_buttons[index].gameObject.activeSelf) return;

        m_upgrade_selection_locked = true;
        foreach (Button button in m_upgrade_buttons) button.interactable = false;
        m_feedback.PlayUpgradeSelect();

        if (!m_player || !m_player.TryApplyUpgrade(m_current_upgrade_choices[index]))
        {
            PrepareUpgradeChoices();
            return;
        }

        if (!TryConsumePendingUpgrade())
        {
            ExitLevelUp();
            return;
        }

        if (PendingUpgradeCount > 0) PrepareUpgradeChoices();
        else ExitLevelUp();
    }

    private void ExitLevelUp()
    {
        if (State != GameState.LevelUp) return;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
        if (m_player) m_player.ResumeRunning();
    }

    public void RegisterBreach(Enemy enemy)
    {
        if (!IsPlaying || !enemy) return;

        m_breach_count = Mathf.Min(m_max_breaches, m_breach_count + 1);
        RefreshHud();
        if (m_breach_count >= m_max_breaches) EnterGameOver();
    }

    public bool TryTriggerDefenseBomb(Enemy triggeringEnemy)
    {
        if (!IsPlaying || !triggeringEnemy || m_defense_bombs_active == null
            || triggeringEnemy.transform.position.y > m_bomb_trigger_y || m_bomb_lane_width <= 0f) return false;

        int lane = Mathf.FloorToInt((triggeringEnemy.transform.position.x - m_bomb_lane_min_x) / m_bomb_lane_width);
        lane = Mathf.Clamp(lane, 0, m_defense_bombs_active.Length - 1);
        if (!m_defense_bombs_active[lane]) return false;

        m_defense_bombs_active[lane] = false;
        if (m_defense_bomb_visuals != null && m_defense_bomb_visuals[lane])
            Destroy(m_defense_bomb_visuals[lane]);

        float laneMin = m_bomb_lane_min_x + lane * m_bomb_lane_width;
        float laneMax = laneMin + m_bomb_lane_width;
        float centerX = (laneMin + laneMax) * 0.5f;
        m_feedback.PlayBombDetonation(centerX, m_bomb_lane_width, m_bomb_trigger_y);
        m_resolving_area_attack = true;
        Enemy.ClearVerticalLane(this, laneMin, laneMax);
        m_resolving_area_attack = false;
        if (IsPlaying && PendingUpgradeCount > 0) EnterLevelUp();
        return true;
    }

    private void CreateDefenseBombs()
    {
        if (!m_game_camera || !m_stage_transform) return;
        float distance = Mathf.Abs(m_game_camera.transform.position.z);
        Vector3 left = m_game_camera.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
        Vector3 right = m_game_camera.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));
        m_bomb_lane_min_x = Mathf.Min(left.x, right.x);
        m_bomb_lane_width = Mathf.Abs(right.x - left.x) / m_defense_bomb_count;
        m_bomb_trigger_y = DefenseLineY + 0.34f;
        m_defense_bombs_active = new bool[m_defense_bomb_count];
        m_defense_bomb_visuals = new GameObject[m_defense_bomb_count];

        for (int lane = 0; lane < m_defense_bomb_count; lane++)
        {
            m_defense_bombs_active[lane] = true;
            float x = m_bomb_lane_min_x + (lane + 0.5f) * m_bomb_lane_width;
            GameObject bomb = new GameObject($"DefenseBomb{lane + 1}");
            bomb.transform.SetParent(m_stage_transform, false);
            bomb.transform.position = new Vector3(x, m_bomb_trigger_y, -0.05f);
            SpriteRenderer renderer = bomb.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDefenseMineSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 3;

            GameObject haloObject = new GameObject("RedWarningGlow", typeof(SpriteRenderer));
            haloObject.transform.SetParent(bomb.transform, false);
            SpriteRenderer halo = haloObject.GetComponent<SpriteRenderer>();
            halo.sprite = GetDefenseMineGlowSprite();
            halo.color = new Color(1f, 0.025f, 0.01f, 0.52f);
            halo.sortingOrder = 2;
            haloObject.transform.localScale = Vector3.one * 2.1f;

            DefenseMinePulse pulse = bomb.AddComponent<DefenseMinePulse>();
            pulse.Initialize(renderer, halo, lane * 1.17f);
            bomb.transform.localScale = Vector3.one * 0.30f;
            m_defense_bomb_visuals[lane] = bomb;
        }
    }

    private static Sprite GetDefenseMineSprite()
    {
        if (s_defense_mine_sprite) return s_defense_mine_sprite;
        const int size = 64;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeDefenseMine";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float radius = offset.magnitude;
                float angle = Mathf.Atan2(offset.y, offset.x);
                float spoke = Mathf.Abs(Mathf.Sin(angle * 4f));
                Color color = Color.clear;
                if (radius <= 25f) color = radius > 21f ? new Color(0.10f, 0.22f, 0.29f, 1f) : new Color(0.035f, 0.09f, 0.14f, 1f);
                if (radius > 24f && radius <= 30f && spoke < 0.30f) color = new Color(0.08f, 0.17f, 0.23f, 1f);
                if (radius >= 10f && radius <= 12f) color = new Color(0.12f, 0.72f, 0.82f, 1f);
                if (radius <= 5f) color = new Color(0.16f, 0.92f, 1f, 1f);
                if (radius >= 17f && radius <= 19f && spoke < 0.18f) color = new Color(0.95f, 0.42f, 0.07f, 1f);
                pixels[y * size + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        s_defense_mine_sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return s_defense_mine_sprite;
    }

    private static Sprite GetDefenseMineGlowSprite()
    {
        if (s_defense_mine_glow_sprite) return s_defense_mine_glow_sprite;
        const int size = 96;
        float center = (size - 1) * 0.5f;
        float maximumRadius = center;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeDefenseMineGlow";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maximumRadius;
                float alpha = radius >= 1f ? 0f : Mathf.Pow(1f - radius, 2.2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        s_defense_mine_glow_sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), size);
        return s_defense_mine_glow_sprite;
    }

    private void EnterGameOver()
    {
        if (!IsPlaying) return;

        Time.timeScale = 1f;
        LocalLeaderboard.Record(m_game_score, m_survival_time, Level);
        m_progression.DiscardPendingUpgrades();
        SetState(GameState.GameOver);
        m_feedback.PlayGameOver();
        CleanupStageObjects();
        RefreshHud();
        RefreshGameOverText();
    }

    private void ReturnToTitle()
    {
        Time.timeScale = 1f;
        CleanupStageObjects();
        m_feedback.ClearTransient();
        m_feedback.StopAudio();
        SetState(GameState.Title);
        m_game_score = 0;
        m_breach_count = 0;
        m_survival_time = 0f;
        m_progression?.Reset();
        m_player = null;
        m_stage_coroutine = null;
        m_title_loop.StartTitleLoop();
    }

    private void SetState(GameState state)
    {
        State = state;
        if (m_stage_ui_root) m_stage_ui_root.SetActive(state != GameState.Title);
        if (m_game_over_text) m_game_over_text.gameObject.SetActive(state == GameState.GameOver);
        if (m_upgrade_panel) m_upgrade_panel.SetActive(state == GameState.LevelUp);
        if (m_skills_panel) m_skills_panel.SetActive(state == GameState.Skills);
    }

    private IEnumerator AnimateUpgradePanel()
    {
        if (!m_upgrade_panel) yield break;
        RectTransform panel = m_upgrade_panel.GetComponent<RectTransform>();
        CanvasGroup group = m_upgrade_panel.GetComponent<CanvasGroup>();
        if (!group) group = m_upgrade_panel.AddComponent<CanvasGroup>();
        float elapsed = 0f;
        const float duration = 0.14f;
        while (elapsed < duration && State == GameState.LevelUp)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = progress;
            panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, progress);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one;

        for (int index = 0; index < m_current_upgrade_count; index++)
        {
            RectTransform card = m_upgrade_buttons[index].GetComponent<RectTransform>();
            UpgradeRarity rarity = m_current_upgrade_choices[index].Rarity;
            float cardElapsed = 0f;
            float cardDuration = rarity == UpgradeRarity.R ? 0.075f
                : rarity == UpgradeRarity.SR ? 0.11f : rarity == UpgradeRarity.SSR ? 0.16f : 0.22f;
            while (cardElapsed < cardDuration && State == GameState.LevelUp)
            {
                cardElapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(cardElapsed / cardDuration);
                float progress = Mathf.SmoothStep(0f, 1f, normalized);
                float overshoot = rarity == UpgradeRarity.R ? 0f : Mathf.Sin(normalized * Mathf.PI) *
                    (rarity == UpgradeRarity.SR ? 0.025f : rarity == UpgradeRarity.SSR ? 0.045f : 0.065f);
                card.localScale = Vector3.one * (Mathf.Lerp(0.84f, 1f, progress) + overshoot);
                if (m_upgrade_button_outlines[index] && rarity != UpgradeRarity.R)
                {
                    int pulses = rarity == UpgradeRarity.SR ? 1 : rarity == UpgradeRarity.SSR ? 2 : 3;
                    Color glow = GetRarityColor(rarity);
                    glow.a = Mathf.Lerp(0.45f, 1f, Mathf.Abs(Mathf.Sin(normalized * Mathf.PI * pulses)));
                    m_upgrade_button_outlines[index].effectColor = glow;
                }
                yield return null;
            }
            card.localScale = Vector3.one;
            if (m_upgrade_button_outlines[index])
                m_upgrade_button_outlines[index].effectColor = GetRarityColor(rarity);
        }

        if (State == GameState.LevelUp)
        {
            m_accept_upgrade_input_time = Time.unscaledTime + 0.05f;
            for (int index = 0; index < m_current_upgrade_count; index++)
                m_upgrade_buttons[index].interactable = true;
        }
        m_upgrade_panel_animation = null;
    }

    private int GetDifficultyStage()
    {
        return Mathf.Max(0, Mathf.FloorToInt(m_survival_time / m_difficulty_stage_seconds));
    }

    private float GetDifficultyProgress()
    {
        return Mathf.Max(0f, m_survival_time / m_difficulty_stage_seconds);
    }

    private float GetCameraBottom()
    {
        if (!m_game_camera) return -5f;
        float distance = Mathf.Abs(m_game_camera.transform.position.z);
        return m_game_camera.ViewportToWorldPoint(new Vector3(0.5f, 0f, distance)).y;
    }

    private void StopStageCoroutine()
    {
        if (m_stage_coroutine == null) return;
        StopCoroutine(m_stage_coroutine);
        m_stage_coroutine = null;
    }

    private void CleanupStageObjects()
    {
        if (!m_stage_transform) return;

        foreach (Player player in m_stage_transform.GetComponentsInChildren<Player>(true)) player.StopRunning();
        foreach (EnemySpawner spawner in m_stage_transform.GetComponentsInChildren<EnemySpawner>(true)) spawner.StopRunning();
        foreach (Enemy enemy in m_stage_transform.GetComponentsInChildren<Enemy>(true)) enemy.StopRunning();
        foreach (PlayerBullet bullet in m_stage_transform.GetComponentsInChildren<PlayerBullet>(true)) bullet.StopRunning();

        for (int index = m_stage_transform.childCount - 1; index >= 0; index--)
            Destroy(m_stage_transform.GetChild(index).gameObject);
        m_player = null;
    }

    private void CreateRuntimeUi()
    {
        if (!m_stage_score_text) return;

        m_stage_ui_root = m_stage_score_text.transform.parent.gameObject;
        Canvas stageCanvas = m_stage_score_text.GetComponentInParent<Canvas>();
        if (stageCanvas)
        {
            stageCanvas.overrideSorting = true;
            stageCanvas.sortingOrder = 100;
            CanvasScaler scaler = stageCanvas.GetComponent<CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(768f, 1024f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }
        m_defense_text = CreateText("Defense", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -10f), new Vector2(0f, 1f), TextAnchor.UpperLeft);
        m_time_text = CreateText("Time", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-10f, -10f), new Vector2(1f, 1f), TextAnchor.UpperRight);
        m_level_text = CreateText("Level", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 10f), new Vector2(1f, 0f), TextAnchor.LowerRight);
        m_stage_score_text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        m_stage_score_text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        m_stage_score_text.rectTransform.pivot = new Vector2(0.5f, 1f);
        m_stage_score_text.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        m_stage_score_text.rectTransform.sizeDelta = new Vector2(220f, 40f);
        m_stage_score_text.alignment = TextAnchor.UpperCenter;
        m_defense_text.rectTransform.sizeDelta = new Vector2(270f, 40f);
        m_time_text.rectTransform.sizeDelta = new Vector2(230f, 40f);
        m_level_text.rectTransform.sizeDelta = new Vector2(180f, 40f);
        CreateAutoFireToggle();
        CreateSkillsUi();
        m_experience_text = CreateText("Experience", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 44f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter);
        m_experience_text.rectTransform.sizeDelta = new Vector2(340f, 34f);
        m_experience_text.fontSize = 18;
        CreateExperienceBar();
        CreateUpgradeUi();
        m_game_over_text = CreateText("GameOver", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
        m_game_over_text.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
        m_game_over_text.rectTransform.anchorMax = new Vector2(0.95f, 0.5f);
        m_game_over_text.rectTransform.sizeDelta = new Vector2(0f, 360f);
        m_game_over_text.fontSize = 28;
        m_game_over_text.resizeTextForBestFit = true;
        m_game_over_text.resizeTextMinSize = 18;
        m_game_over_text.resizeTextMaxSize = 28;
        m_game_over_text.horizontalOverflow = HorizontalWrapMode.Wrap;
        m_game_over_text.verticalOverflow = VerticalWrapMode.Truncate;
        m_game_over_text.color = Color.white;
        StyleText(m_stage_score_text, 26, new Color(0.82f, 0.95f, 1f));
    }

    private Text CreateText(string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 pivot, TextAnchor alignment)
    {
        Text text = Instantiate(m_stage_score_text, m_stage_score_text.transform.parent);
        text.name = name;
        text.rectTransform.anchorMin = anchorMin;
        text.rectTransform.anchorMax = anchorMax;
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.pivot = pivot;
        text.rectTransform.sizeDelta = new Vector2(400f, 40f);
        text.alignment = alignment;
        StyleText(text, 22, Color.white);
        return text;
    }

    private void CreateAutoFireToggle()
    {
        GameObject root = new GameObject("AutoFire", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.SetParent(m_stage_score_text.transform.parent, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(12f, 62f);
        rect.sizeDelta = new Vector2(184f, 50f);
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.08f, 0.58f, 0.72f, 1f);
        Outline rootOutline = root.AddComponent<Outline>();
        rootOutline.effectColor = new Color(0.3f, 0.95f, 1f, 0.95f);
        rootOutline.effectDistance = new Vector2(2f, -2f);

        GameObject innerObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform innerRect = innerObject.GetComponent<RectTransform>();
        innerRect.SetParent(rect, false);
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3f, 3f);
        innerRect.offsetMax = new Vector2(-3f, -3f);
        Image innerImage = innerObject.GetComponent<Image>();
        innerImage.color = new Color(0.025f, 0.09f, 0.14f, 0.98f);
        innerImage.raycastTarget = false;

        GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform checkRect = check.GetComponent<RectTransform>();
        checkRect.SetParent(rect, false);
        checkRect.anchorMin = new Vector2(0f, 0.5f);
        checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(12f, 0f);
        checkRect.sizeDelta = new Vector2(30f, 30f);
        Image checkBox = check.GetComponent<Image>();
        checkBox.color = new Color(0.12f, 0.72f, 0.86f, 1f);

        GameObject tickObject = new GameObject("Tick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform tickRect = tickObject.GetComponent<RectTransform>();
        tickRect.SetParent(checkRect, false);
        tickRect.anchorMin = Vector2.zero;
        tickRect.anchorMax = Vector2.one;
        tickRect.offsetMin = new Vector2(3f, 3f);
        tickRect.offsetMax = new Vector2(-3f, -3f);
        Image tickImage = tickObject.GetComponent<Image>();
        tickImage.sprite = GetTickSprite();
        tickImage.color = new Color(0.18f, 0.92f, 1f);
        tickImage.raycastTarget = false;

        Text label = Instantiate(m_stage_score_text, rect);
        label.name = "Label";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(51f, 0f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        label.alignment = TextAnchor.MiddleLeft;
        label.text = "AUTO FIRE";
        label.raycastTarget = false;
        StyleText(label, 19, new Color(0.78f, 0.97f, 1f));

        m_auto_fire_toggle = root.GetComponent<Toggle>();
        m_auto_fire_toggle.targetGraphic = background;
        m_auto_fire_toggle.graphic = tickImage;
        m_auto_fire_toggle.isOn = m_auto_fire_enabled;
        m_auto_fire_toggle.onValueChanged.AddListener(SetAutoFire);
    }

    private void SetAutoFire(bool enabled)
    {
        m_auto_fire_enabled = enabled;
        if (m_player) m_player.SetAutoFire(enabled);
    }

    private void CreateSkillsUi()
    {
        Transform uiParent = m_stage_score_text.transform.parent;
        GameObject buttonObject = new GameObject("SkillsButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(uiParent, false);
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-10f, -58f);
        buttonRect.sizeDelta = new Vector2(150f, 42f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.08f, 0.30f, 0.40f, 0.96f);
        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.18f, 0.82f, 0.95f, 0.9f);
        buttonOutline.effectDistance = new Vector2(2f, -2f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(OpenSkillsPanel);
        Text label = Instantiate(m_stage_score_text, buttonRect);
        label.name = "Label";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "SKILLS";
        label.raycastTarget = false;
        StyleText(label, 18, Color.white);

        m_skills_panel = new GameObject("SkillsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = m_skills_panel.GetComponent<RectTransform>();
        panelRect.SetParent(uiParent, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = m_skills_panel.GetComponent<Image>();
        panelImage.color = new Color(0.018f, 0.045f, 0.075f, 0.97f);
        panelImage.raycastTarget = true;

        Text header = CreateText("SkillsHeader", new Vector2(0.08f, 1f), new Vector2(0.92f, 1f),
            new Vector2(0f, -42f), new Vector2(0.5f, 1f), TextAnchor.UpperCenter);
        header.transform.SetParent(panelRect, false);
        header.rectTransform.sizeDelta = new Vector2(0f, 80f);
        header.text = "CURRENT BUILD";
        StyleText(header, 32, new Color(0.35f, 0.94f, 1f));

        m_skills_content_text = Instantiate(m_stage_score_text, panelRect);
        m_skills_content_text.name = "SkillsContent";
        m_skills_content_text.rectTransform.anchorMin = new Vector2(0.08f, 0.14f);
        m_skills_content_text.rectTransform.anchorMax = new Vector2(0.92f, 0.86f);
        m_skills_content_text.rectTransform.offsetMin = Vector2.zero;
        m_skills_content_text.rectTransform.offsetMax = Vector2.zero;
        m_skills_content_text.alignment = TextAnchor.UpperLeft;
        m_skills_content_text.fontSize = 19;
        m_skills_content_text.fontStyle = FontStyle.Normal;
        m_skills_content_text.supportRichText = true;
        m_skills_content_text.lineSpacing = 1.08f;
        m_skills_content_text.raycastTarget = false;
        m_skills_content_text.horizontalOverflow = HorizontalWrapMode.Wrap;
        m_skills_content_text.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject closeObject = new GameObject("CloseSkills",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.SetParent(panelRect, false);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 36f);
        closeRect.sizeDelta = new Vector2(240f, 54f);
        closeObject.GetComponent<Image>().color = new Color(0.08f, 0.34f, 0.44f, 1f);
        closeObject.GetComponent<Button>().onClick.AddListener(CloseSkillsPanel);
        Text closeLabel = Instantiate(label, closeRect);
        closeLabel.name = "Label";
        closeLabel.text = "CLOSE  [TAB]";
        closeLabel.rectTransform.anchorMin = Vector2.zero;
        closeLabel.rectTransform.anchorMax = Vector2.one;
        closeLabel.rectTransform.offsetMin = Vector2.zero;
        closeLabel.rectTransform.offsetMax = Vector2.zero;
        m_skills_panel.SetActive(false);
    }

    private void OpenSkillsPanel()
    {
        if (State != GameState.Playing || !m_player || m_player.RuntimeWeapon == null) return;
        m_player.StopRunning();
        Time.timeScale = 0f;
        SetState(GameState.Skills);
        m_skills_panel.transform.SetAsLastSibling();
        RefreshSkillsPanel();
    }

    private void CloseSkillsPanel()
    {
        if (State != GameState.Skills) return;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
        if (m_player) m_player.ResumeRunning();
    }

    private void RefreshSkillsPanel()
    {
        if (!m_skills_content_text || !m_player || m_player.RuntimeWeapon == null) return;
        WeaponRuntimeState weapon = m_player.RuntimeWeapon;
        var builder = new StringBuilder(768);
        builder.Append("<size=18><color=#7EDDEC><b>WEAPON STATUS</b></color></size>\n");
        builder.Append($"Damage {weapon.Damage:0.0}    Interval {weapon.FireInterval:0.00}s    Crit {weapon.CriticalChance * 100f:0}%\n");
        builder.Append($"Projectiles {weapon.ProjectileCount}    Penetration {weapon.PenetrationCount}    Burst {weapon.BurstCount}\n\n");
        builder.Append("<size=20><color=#FFFFFF><b>ACQUIRED SKILLS</b></color></size>\n");
        bool hasSkills = false;
        foreach (WeaponUpgradeType type in Enum.GetValues(typeof(WeaponUpgradeType)))
        {
            int level = weapon.GetUpgradeLevel(type);
            if (level <= 0) continue;
            hasSkills = true;
            builder.Append($"<color=#FFE08A><b>LV {level}  {GetSkillDisplayName(type)}</b></color>\n");
            builder.Append($"<size=16><color=#E8F5FA>{GetSkillCurrentValue(type, weapon)}</color></size>\n");
        }
        if (!hasSkills) builder.Append("<color=#B8CBD3>No upgrades acquired yet.</color>");
        m_skills_content_text.text = builder.ToString();
    }

    private static string GetSkillDisplayName(WeaponUpgradeType type)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return "REINFORCED ROUNDS";
            case WeaponUpgradeType.FireInterval: return "RAPID FIRE";
            case WeaponUpgradeType.ProjectileSpeed: return "HIGH-VELOCITY ROUNDS";
            case WeaponUpgradeType.ProjectileCount: return "MULTISHOT";
            case WeaponUpgradeType.Penetration: return "PIERCING ROUNDS";
            case WeaponUpgradeType.CriticalChance: return "CRITICAL ROUNDS";
            case WeaponUpgradeType.BurstFire: return "BURST MODULE";
            case WeaponUpgradeType.Lightning: return "AUTO LIGHTNING";
            default: return type.ToString().ToUpperInvariant();
        }
    }

    private static string GetSkillCurrentValue(WeaponUpgradeType type, WeaponRuntimeState weapon)
    {
        switch (type)
        {
            case WeaponUpgradeType.Damage: return $"Current damage: {weapon.Damage:0.0}";
            case WeaponUpgradeType.FireInterval: return $"Current fire interval: {weapon.FireInterval:0.00}s";
            case WeaponUpgradeType.ProjectileSpeed: return $"Current projectile speed: {weapon.ProjectileSpeed:0.0}";
            case WeaponUpgradeType.ProjectileCount: return $"Current projectiles per volley: {weapon.ProjectileCount}";
            case WeaponUpgradeType.Penetration: return $"Current extra penetration: {weapon.PenetrationCount}";
            case WeaponUpgradeType.CriticalChance: return $"Current critical chance: {weapon.CriticalChance * 100f:0}%";
            case WeaponUpgradeType.BurstFire: return $"Current volleys per attack: {weapon.BurstCount}";
            case WeaponUpgradeType.Lightning: return $"Lightning level {weapon.LightningLevel}, every {weapon.LightningInterval:0.00}s";
            default: return string.Empty;
        }
    }

    private static Sprite GetTickSprite()
    {
        if (s_tick_sprite) return s_tick_sprite;
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeTick";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        texture.SetPixels(pixels);
        DrawTickSegment(texture, new Vector2Int(5, 16), new Vector2Int(13, 8), 3);
        DrawTickSegment(texture, new Vector2Int(13, 8), new Vector2Int(27, 25), 3);
        texture.Apply(false, true);
        s_tick_sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return s_tick_sprite;
    }

    private static void DrawTickSegment(Texture2D texture, Vector2Int start, Vector2Int end, int radius)
    {
        int steps = Mathf.Max(Mathf.Abs(end.x - start.x), Mathf.Abs(end.y - start.y));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps > 0 ? (float)step / steps : 0f;
            int x = Mathf.RoundToInt(Mathf.Lerp(start.x, end.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(start.y, end.y, t));
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                    if (offsetX * offsetX + offsetY * offsetY <= radius * radius)
                        texture.SetPixel(Mathf.Clamp(x + offsetX, 0, texture.width - 1),
                            Mathf.Clamp(y + offsetY, 0, texture.height - 1), Color.white);
        }
    }

    private static void StyleText(Text text, int size, Color color)
    {
        if (!text) return;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        Outline outline = text.GetComponent<Outline>();
        if (!outline) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.04f, 0.07f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void RefreshHud()
    {
        if (m_stage_score_text) m_stage_score_text.text = $"Score {m_game_score:00000}";
        if (m_defense_text) m_defense_text.text = $"Defense: {m_max_breaches - m_breach_count} / {m_max_breaches}";
        if (m_time_text) m_time_text.text = $"Time {FormatTime(m_survival_time)}";
    }

    private void CreateExperienceBar()
    {
        GameObject backgroundObject = new GameObject("ExperienceBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backgroundTransform = backgroundObject.GetComponent<RectTransform>();
        backgroundTransform.SetParent(m_stage_score_text.transform.parent, false);
        backgroundTransform.anchorMin = new Vector2(0.5f, 0f);
        backgroundTransform.anchorMax = new Vector2(0.5f, 0f);
        backgroundTransform.pivot = new Vector2(0.5f, 0f);
        backgroundTransform.anchoredPosition = new Vector2(0f, 14f);
        backgroundTransform.sizeDelta = new Vector2(360f, 22f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        m_experience_fill_rect = fillObject.GetComponent<RectTransform>();
        m_experience_fill_rect.SetParent(backgroundTransform, false);
        m_experience_fill_rect.anchorMin = new Vector2(0f, 0.5f);
        m_experience_fill_rect.anchorMax = new Vector2(0f, 0.5f);
        m_experience_fill_rect.pivot = new Vector2(0f, 0.5f);
        m_experience_fill_rect.anchoredPosition = new Vector2(2f, 0f);
        m_experience_fill_rect.sizeDelta = new Vector2(0f, 18f);
        Image experienceFill = fillObject.GetComponent<Image>();
        experienceFill.type = Image.Type.Simple;
        experienceFill.color = new Color(0.2f, 0.85f, 1f, 1f);

        m_experience_percent_text = Instantiate(m_stage_score_text, backgroundTransform);
        m_experience_percent_text.name = "Percentage";
        m_experience_percent_text.rectTransform.anchorMin = Vector2.zero;
        m_experience_percent_text.rectTransform.anchorMax = Vector2.one;
        m_experience_percent_text.rectTransform.offsetMin = Vector2.zero;
        m_experience_percent_text.rectTransform.offsetMax = Vector2.zero;
        m_experience_percent_text.alignment = TextAnchor.MiddleCenter;
        m_experience_percent_text.fontSize = 14;
        m_experience_percent_text.color = Color.white;
        m_experience_percent_text.raycastTarget = false;
        m_experience_percent_text.text = "0%";
    }

    private void CreateUpgradeUi()
    {
        Canvas canvas = m_stage_score_text.GetComponentInParent<Canvas>();
        if (canvas && !canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (!EventSystem.current)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        m_upgrade_panel = new GameObject("UpgradePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelTransform = m_upgrade_panel.GetComponent<RectTransform>();
        panelTransform.SetParent(m_stage_score_text.transform.parent, false);
        panelTransform.anchorMin = Vector2.zero;
        panelTransform.anchorMax = Vector2.one;
        panelTransform.offsetMin = Vector2.zero;
        panelTransform.offsetMax = Vector2.zero;
        Image panelImage = m_upgrade_panel.GetComponent<Image>();
        panelImage.color = new Color(0.03f, 0.05f, 0.09f, 0.94f);
        panelImage.raycastTarget = true;

        m_upgrade_header_text = CreateText("UpgradeHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -38f), new Vector2(0.5f, 1f), TextAnchor.UpperCenter);
        m_upgrade_header_text.transform.SetParent(panelTransform, false);
        m_upgrade_header_text.rectTransform.anchorMin = new Vector2(0.05f, 1f);
        m_upgrade_header_text.rectTransform.anchorMax = new Vector2(0.95f, 1f);
        m_upgrade_header_text.rectTransform.anchoredPosition = new Vector2(0f, -38f);
        m_upgrade_header_text.rectTransform.sizeDelta = new Vector2(0f, 115f);
        m_upgrade_header_text.fontSize = 27;
        m_upgrade_header_text.color = Color.white;
        m_upgrade_header_text.resizeTextForBestFit = true;
        m_upgrade_header_text.resizeTextMinSize = 20;
        m_upgrade_header_text.resizeTextMaxSize = 27;

        for (int index = 0; index < m_upgrade_buttons.Length; index++)
        {
            int buttonIndex = index;
            GameObject buttonObject = new GameObject($"UpgradeOption{index + 1}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
            buttonTransform.SetParent(panelTransform, false);
            buttonTransform.anchorMin = new Vector2(0.06f, 0.5f);
            buttonTransform.anchorMax = new Vector2(0.94f, 0.5f);
            buttonTransform.pivot = new Vector2(0.5f, 0.5f);
            buttonTransform.anchoredPosition = new Vector2(0f, 115f - index * 145f);
            buttonTransform.sizeDelta = new Vector2(0f, 120f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.12f, 0.24f, 0.38f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            Outline cardOutline = buttonObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.2f, 0.55f, 0.75f, 0.85f);
            cardOutline.effectDistance = new Vector2(1f, -1f);
            m_upgrade_button_outlines[index] = cardOutline;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.2f, 0.42f, 0.62f, 1f);
            colors.pressedColor = new Color(0.08f, 0.18f, 0.28f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => SelectUpgrade(buttonIndex));
            m_upgrade_buttons[index] = button;

            Text optionText = Instantiate(m_stage_score_text, buttonTransform);
            optionText.name = "Text";
            optionText.rectTransform.anchorMin = Vector2.zero;
            optionText.rectTransform.anchorMax = Vector2.one;
            optionText.rectTransform.offsetMin = new Vector2(112f, 8f);
            optionText.rectTransform.offsetMax = new Vector2(-18f, -8f);
            optionText.alignment = TextAnchor.MiddleCenter;
            optionText.fontSize = 23;
            optionText.color = Color.white;
            optionText.raycastTarget = false;
            StyleText(optionText, 19, Color.white);
            optionText.supportRichText = true;
            optionText.fontStyle = FontStyle.Normal;
            optionText.resizeTextForBestFit = true;
            optionText.resizeTextMinSize = 13;
            optionText.resizeTextMaxSize = 19;
            optionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            optionText.verticalOverflow = VerticalWrapMode.Truncate;
            m_upgrade_button_texts[index] = optionText;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconTransform = iconObject.GetComponent<RectTransform>();
            iconTransform.SetParent(buttonTransform, false);
            iconTransform.anchorMin = new Vector2(0f, 0.5f);
            iconTransform.anchorMax = new Vector2(0f, 0.5f);
            iconTransform.pivot = new Vector2(0.5f, 0.5f);
            iconTransform.anchoredPosition = new Vector2(61f, 0f);
            iconTransform.sizeDelta = new Vector2(82f, 82f);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.material = GetUpgradeIconMaterial();
            m_upgrade_button_icons[index] = icon;
        }

        m_upgrade_panel.SetActive(false);
    }

    private Sprite GetUpgradeIcon(WeaponUpgradeType type)
    {
        if (m_upgrade_icons == null)
        {
            Texture2D atlas = Resources.Load<Texture2D>("Task5/UI/upgrade_icons");
            if (!atlas) return null;
            m_upgrade_icons = new Sprite[8];
            float cellWidth = atlas.width / 4f;
            float cellHeight = atlas.height / 2f;
            for (int index = 0; index < m_upgrade_icons.Length; index++)
            {
                int column = index % 4;
                int rowFromTop = index / 4;
                float y = atlas.height - (rowFromTop + 1) * cellHeight;
                m_upgrade_icons[index] = Sprite.Create(atlas,
                    new Rect(column * cellWidth, y, cellWidth, cellHeight), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        int iconIndex = (int)type;
        return iconIndex >= 0 && iconIndex < m_upgrade_icons.Length ? m_upgrade_icons[iconIndex] : null;
    }

    private static Material GetUpgradeIconMaterial()
    {
        if (s_upgrade_icon_material) return s_upgrade_icon_material;
        Shader shader = Resources.Load<Shader>("Task5/UI/IconChromaKey");
        if (shader) s_upgrade_icon_material = new Material(shader) { name = "RuntimeUpgradeIconMaterial" };
        return s_upgrade_icon_material;
    }

    private static Color GetRarityColor(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.R: return new Color(0.12f, 0.32f, 0.68f, 1f);
            case UpgradeRarity.SR: return new Color(0.46f, 0.18f, 0.68f, 1f);
            case UpgradeRarity.SSR: return new Color(0.92f, 0.42f, 0.08f, 1f);
            case UpgradeRarity.UR: return new Color(0.86f, 0.055f, 0.075f, 1f);
            default: return Color.gray;
        }
    }

    private void RefreshProgressionUi()
    {
        if (m_progression == null) return;
        float progress = m_progression.RequiredExperience > 0
            ? Mathf.Clamp01((float)m_progression.CurrentExperience / m_progression.RequiredExperience)
            : 0f;
        int percentage = Mathf.RoundToInt(progress * 100f);

        if (m_level_text) m_level_text.text = $"Level {m_progression.Level}";
        if (m_experience_text)
            m_experience_text.text = $"EXP {m_progression.CurrentExperience} / {m_progression.RequiredExperience} ({percentage}%)";
        if (m_experience_fill_rect) m_experience_fill_rect.sizeDelta = new Vector2(356f * progress, 18f);
        if (m_experience_percent_text) m_experience_percent_text.text = $"{percentage}%";
    }

    private void RefreshGameOverText()
    {
        if (!m_game_over_text) return;
        m_game_over_text.text =
            $"GAME OVER\n\nFinal Score: {m_game_score}\nSurvival Time: {FormatTime(m_survival_time)}\n" +
            $"Breaches: {m_breach_count} / {m_max_breaches}\nFinal Level: {Level}\nTotal Kills: {KillCount}\n\n" +
            "Press Space to Restart\nPress Esc to Return to Title";
    }

    private static string FormatTime(float time)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(time));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void OnDrawGizmos()
    {
        Camera camera = m_game_camera ? m_game_camera : Camera.main;
        if (!camera || !camera.orthographic) return;

        float distance = Mathf.Abs(camera.transform.position.z);
        Vector3 left = camera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 right = camera.ViewportToWorldPoint(new Vector3(1f, 0f, distance));
        float y = left.y + m_defense_bottom_offset;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(left.x, y, 0f), new Vector3(right.x, y, 0f));
    }

    private void OnValidate()
    {
        m_player_bottom_offset = Mathf.Max(0f, m_player_bottom_offset);
        m_defense_bottom_offset = Mathf.Max(0f, m_defense_bottom_offset);
        m_defense_bomb_count = Mathf.Clamp(m_defense_bomb_count, 1, 8);
        m_max_breaches = Mathf.Max(1, m_max_breaches);
        m_base_spawn_interval = Mathf.Max(0.1f, m_base_spawn_interval);
        m_min_spawn_interval = Mathf.Clamp(m_min_spawn_interval, 0.1f, m_base_spawn_interval);
        m_base_enemy_hp = Mathf.Max(1, m_base_enemy_hp);
        m_base_enemy_speed = Mathf.Max(0f, m_base_enemy_speed);
        m_difficulty_stage_seconds = Mathf.Max(1f, m_difficulty_stage_seconds);
        m_hp_multiplier_per_stage = Mathf.Max(1f, m_hp_multiplier_per_stage);
        m_speed_multiplier_per_stage = Mathf.Max(1f, m_speed_multiplier_per_stage);
        m_spawn_interval_multiplier_per_stage = Mathf.Clamp(m_spawn_interval_multiplier_per_stage, 0.01f, 1f);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        StopStageCoroutine();
        CleanupStageObjects();
        m_feedback?.ClearTransient();
        m_feedback?.StopAudio();
        State = GameState.Title;
        if (m_upgrade_panel) m_upgrade_panel.SetActive(false);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        OnLevelUp = null;
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit() => Time.timeScale = 1f;
}
