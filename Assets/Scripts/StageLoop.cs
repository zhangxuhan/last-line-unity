using System.Collections;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageLoop : MonoBehaviour
{
    public enum GameState
    {
        Title,
        Playing,
        LevelUp,
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
    private RectTransform m_experience_fill_rect;
    private Text m_experience_percent_text;
    private GameObject m_upgrade_panel;
    private Text m_upgrade_header_text;
    private readonly Button[] m_upgrade_buttons = new Button[3];
    private readonly Text[] m_upgrade_button_texts = new Text[3];
    private readonly WeaponUpgradeChoice[] m_current_upgrade_choices = new WeaponUpgradeChoice[3];
    private PlayerProgression m_progression;
    private Player m_player;
    private System.Random m_upgrade_random;
    private int m_current_upgrade_count;
    private int m_accept_upgrade_input_frame;
    private bool m_upgrade_selection_locked;
    private int m_game_score;
    private int m_breach_count;
    private float m_survival_time;

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
                * Mathf.Pow(m_spawn_interval_multiplier_per_stage, GetDifficultyStage());
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

                if (Input.GetKeyDown(KeyCode.Escape))
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

                if (Time.frameCount >= m_accept_upgrade_input_frame)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) SelectUpgrade(0);
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectUpgrade(1);
                    else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectUpgrade(2);
                }
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

        EnemySpawner spawner = Instantiate(m_prefab_enemy_spawner, m_stage_transform);
        spawner.Initialize(this, m_game_camera, m_stage_transform);
    }

    public void GetCurrentEnemyStats(out int maxHp, out float moveSpeed)
    {
        int stage = GetDifficultyStage();
        maxHp = Mathf.Max(1, Mathf.CeilToInt(m_base_enemy_hp * Mathf.Pow(m_hp_multiplier_per_stage, stage)));
        moveSpeed = Mathf.Max(0f, m_base_enemy_speed * Mathf.Pow(m_speed_multiplier_per_stage, stage));
    }

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

        if (levelUpCount > 0 && IsPlaying) EnterLevelUp();
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
        m_accept_upgrade_input_frame = Time.frameCount + 1;
        if (m_upgrade_panel_animation != null) StopCoroutine(m_upgrade_panel_animation);
        m_upgrade_panel_animation = StartCoroutine(AnimateUpgradePanel());
        if (m_upgrade_header_text) m_upgrade_header_text.text = $"LEVEL UP\nLevel {Level}\nChoose one upgrade";

        for (int index = 0; index < m_upgrade_buttons.Length; index++)
        {
            bool visible = index < choices.Count;
            m_upgrade_buttons[index].gameObject.SetActive(visible);
            if (!visible) continue;

            WeaponUpgradeChoice choice = choices[index];
            m_current_upgrade_choices[index] = choice;
            WeaponUpgradeOption option = WeaponUpgradeSystem.BuildOption(choice, m_player.RuntimeWeapon);
            m_upgrade_button_texts[index].text = $"[{index + 1}] [{choice.Rarity}] {option.Name}\n{option.Description}\n{option.ValueChange}";
            Color rarityColor = GetRarityColor(choice.Rarity);
            ColorBlock colors = m_upgrade_buttons[index].colors;
            colors.normalColor = rarityColor;
            colors.highlightedColor = Color.Lerp(rarityColor, Color.white, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(rarityColor, Color.black, 0.22f);
            colors.disabledColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            m_upgrade_buttons[index].colors = colors;
            m_upgrade_buttons[index].image.color = rarityColor;
            m_upgrade_buttons[index].interactable = true;
        }
    }

    private void SelectUpgrade(int index)
    {
        if (State != GameState.LevelUp || m_upgrade_selection_locked
            || Time.frameCount < m_accept_upgrade_input_frame
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

    private void EnterGameOver()
    {
        if (!IsPlaying) return;

        Time.timeScale = 1f;
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
        m_upgrade_panel_animation = null;
    }

    private int GetDifficultyStage()
    {
        return Mathf.Max(0, Mathf.FloorToInt(m_survival_time / m_difficulty_stage_seconds));
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
        }
        m_defense_text = CreateText("Defense", new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -10f), new Vector2(0f, 1f), TextAnchor.UpperLeft);
        m_time_text = CreateText("Time", new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-10f, -10f), new Vector2(1f, 1f), TextAnchor.UpperRight);
        m_level_text = CreateText("Level", new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 10f), new Vector2(1f, 0f), TextAnchor.LowerRight);
        m_experience_text = CreateText("Experience", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 42f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter);
        CreateExperienceBar();
        CreateUpgradeUi();
        m_game_over_text = CreateText("GameOver", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
        m_game_over_text.rectTransform.sizeDelta = new Vector2(620f, 360f);
        m_game_over_text.fontSize = 32;
        m_game_over_text.color = Color.white;
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
        return text;
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
        m_upgrade_header_text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        m_upgrade_header_text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        m_upgrade_header_text.rectTransform.anchoredPosition = new Vector2(0f, -38f);
        m_upgrade_header_text.rectTransform.sizeDelta = new Vector2(700f, 115f);
        m_upgrade_header_text.fontSize = 30;
        m_upgrade_header_text.color = Color.white;

        for (int index = 0; index < m_upgrade_buttons.Length; index++)
        {
            int buttonIndex = index;
            GameObject buttonObject = new GameObject($"UpgradeOption{index + 1}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
            buttonTransform.SetParent(panelTransform, false);
            buttonTransform.anchorMin = new Vector2(0.5f, 0.5f);
            buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
            buttonTransform.pivot = new Vector2(0.5f, 0.5f);
            buttonTransform.anchoredPosition = new Vector2(0f, 115f - index * 145f);
            buttonTransform.sizeDelta = new Vector2(700f, 120f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.12f, 0.24f, 0.38f, 1f);
            Button button = buttonObject.GetComponent<Button>();
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
            optionText.rectTransform.offsetMin = new Vector2(18f, 8f);
            optionText.rectTransform.offsetMax = new Vector2(-18f, -8f);
            optionText.alignment = TextAnchor.MiddleCenter;
            optionText.fontSize = 23;
            optionText.color = Color.white;
            optionText.raycastTarget = false;
            m_upgrade_button_texts[index] = optionText;
        }

        m_upgrade_panel.SetActive(false);
    }

    private static Color GetRarityColor(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.R: return new Color(0.12f, 0.32f, 0.68f, 1f);
            case UpgradeRarity.SR: return new Color(0.46f, 0.18f, 0.68f, 1f);
            case UpgradeRarity.SSR: return new Color(0.92f, 0.42f, 0.08f, 1f);
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
