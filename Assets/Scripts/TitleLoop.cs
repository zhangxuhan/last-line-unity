using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleLoop : MonoBehaviour
{
    private const string DisplayModeKey = "QGames.DisplayMode.V1";
    private static readonly Vector2Int[] WindowSizes =
    {
        new Vector2Int(600, 800), new Vector2Int(720, 960), new Vector2Int(768, 1024)
    };

    [SerializeField] private StageLoop m_stage_loop;
    [Header("Layout")]
    [SerializeField] private Transform m_ui_title;
    private Coroutine m_title_coroutine;
    private GameObject m_leaderboard_panel;
    private Text m_leaderboard_text;
    private bool m_leaderboard_open;
    private GameObject m_display_panel;
    private Text m_display_status_text;
    private bool m_display_open;
    private int m_selected_display_mode;
    private bool m_start_requested;

    private void Start()
    {
        ApplySavedDisplayMode();
        CreateTitlePresentation();
        CreateLeaderboardUi();
        CreateDisplaySettingsUi();
        StartTitleLoop();
    }

    public void StartTitleLoop()
    {
        if (m_title_coroutine != null) StopCoroutine(m_title_coroutine);
        m_start_requested = false;
        m_title_coroutine = StartCoroutine(TitleCoroutine());
    }

    private IEnumerator TitleCoroutine()
    {
        m_ui_title.gameObject.SetActive(true);
        CloseLeaderboard();
        CloseDisplaySettings();
        while (true)
        {
            if (m_display_open && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseDisplaySettings();
            }
            else if (m_leaderboard_open && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseLeaderboard();
            }
            else if (!m_leaderboard_open && !m_display_open
                && (m_start_requested || Input.GetKeyDown(KeyCode.Space)))
            {
                m_start_requested = false;
                m_ui_title.gameObject.SetActive(false);
                m_title_coroutine = null;
                m_stage_loop.StartStageLoop();
                yield break;
            }
            yield return null;
        }
    }

    private void CreateTitlePresentation()
    {
        if (!m_ui_title || m_ui_title.Find("TitleBackdrop")) return;
        Text template = m_ui_title.GetComponentInChildren<Text>(true);
        if (!template) return;

        Canvas canvas = m_ui_title.GetComponentInParent<Canvas>();
        if (canvas && !canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>();
        if (!EventSystem.current)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        GameObject backdrop = new GameObject("TitleBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.SetParent(m_ui_title, false);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.055f, 0.90f);
        backdropRect.SetAsFirstSibling();

        Text title = m_ui_title.Find("TitleText")?.GetComponent<Text>();
        if (title)
        {
            title.text = "LAST LINE";
            title.rectTransform.anchoredPosition = new Vector2(0f, -142f);
            title.rectTransform.sizeDelta = new Vector2(650f, 116f);
            title.fontSize = 72;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.72f, 0.96f, 1f);
            Outline titleOutline = title.GetComponent<Outline>();
            if (!titleOutline) titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.02f, 0.36f, 0.48f, 0.95f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
        }

        Text subtitle = CreateLabel(template, m_ui_title, "HOLD THE DEFENSE  •  SURVIVE  •  EVOLVE", 20,
            new Color(0.32f, 0.82f, 0.92f));
        subtitle.name = "Subtitle";
        subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -214f);
        subtitle.rectTransform.sizeDelta = new Vector2(620f, 42f);

        GameObject missionPanel = new GameObject("MissionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform missionRect = missionPanel.GetComponent<RectTransform>();
        missionRect.SetParent(m_ui_title, false);
        missionRect.anchorMin = new Vector2(0.5f, 0.5f);
        missionRect.anchorMax = new Vector2(0.5f, 0.5f);
        missionRect.anchoredPosition = new Vector2(0f, 24f);
        missionRect.sizeDelta = new Vector2(620f, 214f);
        missionPanel.GetComponent<Image>().color = new Color(0.025f, 0.10f, 0.15f, 0.94f);
        Outline missionOutline = missionPanel.AddComponent<Outline>();
        missionOutline.effectColor = new Color(0.12f, 0.48f, 0.60f, 0.85f);
        missionOutline.effectDistance = new Vector2(2f, -2f);

        Text instructions = m_ui_title.Find("PressStart")?.GetComponent<Text>();
        if (instructions)
        {
            instructions.text = "MOVE    A / D  or  ARROW KEYS\nAIM       MOUSE\nFIRE      HOLD LEFT MOUSE  or  AUTO FIRE\n\nStop every enemy before the defense line falls.";
            instructions.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            instructions.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            instructions.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            instructions.rectTransform.sizeDelta = new Vector2(560f, 178f);
            instructions.fontSize = 23;
            instructions.fontStyle = FontStyle.Bold;
            instructions.color = new Color(0.88f, 0.96f, 1f);
            instructions.raycastTarget = false;
            instructions.transform.SetAsLastSibling();
        }

        CreateMenuButton(template, m_ui_title, "StartButton", new Vector2(0f, -164f), new Vector2(320f, 64f),
            "START GAME", new Color(0.06f, 0.58f, 0.72f, 1f), RequestStart);

        Text shortcut = CreateLabel(template, m_ui_title, "or press SPACE", 17, new Color(0.55f, 0.72f, 0.78f));
        shortcut.name = "StartShortcut";
        shortcut.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        shortcut.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        shortcut.rectTransform.anchoredPosition = new Vector2(0f, -208f);
        shortcut.rectTransform.sizeDelta = new Vector2(260f, 28f);
    }

    private Button CreateMenuButton(Text template, Transform parent, string name, Vector2 position, Vector2 size,
        string label, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = Color.Lerp(color, Color.white, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.78f, 0.98f, 1f);
        colors.pressedColor = new Color(0.55f, 0.78f, 0.84f);
        button.colors = colors;
        button.onClick.AddListener(action);
        CreateLabel(template, rect, label, 25, Color.white);
        return button;
    }

    private void RequestStart()
    {
        if (!m_leaderboard_open && !m_display_open) m_start_requested = true;
    }

    private void CreateLeaderboardUi()
    {
        if (!m_ui_title || m_leaderboard_panel) return;
        Text template = m_ui_title.GetComponentInChildren<Text>(true);
        if (!template) return;

        if (!EventSystem.current)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(transform, false);
        }

        GameObject buttonObject = new GameObject("LeaderboardButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(m_ui_title, false);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-155f, -276f);
        buttonRect.sizeDelta = new Vector2(280f, 58f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.06f, 0.20f, 0.30f, 0.96f);
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.95f, 1f);
        colors.pressedColor = new Color(0.45f, 0.72f, 0.82f);
        button.colors = colors;
        button.onClick.AddListener(OpenLeaderboard);
        CreateLabel(template, buttonRect, "LEADERBOARD", 24, Color.white);

        m_leaderboard_panel = new GameObject("LeaderboardPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = m_leaderboard_panel.GetComponent<RectTransform>();
        panelRect.SetParent(m_ui_title, false);
        panelRect.anchorMin = new Vector2(0.08f, 0.18f);
        panelRect.anchorMax = new Vector2(0.92f, 0.82f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = m_leaderboard_panel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.07f, 0.11f, 0.98f);
        Outline panelOutline = m_leaderboard_panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.18f, 0.82f, 0.95f, 0.9f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        Text header = CreateLabel(template, panelRect, "LOCAL TOP 3", 36, new Color(0.30f, 0.94f, 1f));
        header.rectTransform.anchorMin = new Vector2(0.05f, 0.84f);
        header.rectTransform.anchorMax = new Vector2(0.95f, 0.98f);

        Text columns = CreateLabel(template, panelRect, "RANKING   /   SCORE   /   TIME   /   LEVEL", 17,
            new Color(0.52f, 0.78f, 0.86f));
        columns.rectTransform.anchorMin = new Vector2(0.08f, 0.77f);
        columns.rectTransform.anchorMax = new Vector2(0.92f, 0.85f);

        m_leaderboard_text = CreateLabel(template, panelRect, string.Empty, 25, new Color(0.94f, 0.98f, 1f));
        m_leaderboard_text.rectTransform.anchorMin = new Vector2(0.09f, 0.22f);
        m_leaderboard_text.rectTransform.anchorMax = new Vector2(0.91f, 0.76f);
        m_leaderboard_text.alignment = TextAnchor.MiddleLeft;
        m_leaderboard_text.lineSpacing = 1.15f;

        GameObject closeObject = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.SetParent(panelRect, false);
        closeRect.anchorMin = new Vector2(0.5f, 0.08f);
        closeRect.anchorMax = new Vector2(0.5f, 0.08f);
        closeRect.anchoredPosition = Vector2.zero;
        closeRect.sizeDelta = new Vector2(210f, 52f);
        closeObject.GetComponent<Image>().color = new Color(0.10f, 0.28f, 0.38f, 1f);
        closeObject.GetComponent<Button>().onClick.AddListener(CloseLeaderboard);
        CreateLabel(template, closeRect, "CLOSE", 22, Color.white);
        m_leaderboard_panel.SetActive(false);
    }

    private static Text CreateLabel(Text template, Transform parent, string value, int size, Color color)
    {
        Text label = Instantiate(template, parent);
        label.name = "Text";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8f, 5f);
        label.rectTransform.offsetMax = new Vector2(-8f, -5f);
        label.text = value;
        label.fontSize = size;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 14;
        label.resizeTextMaxSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        Outline outline = label.GetComponent<Outline>();
        if (!outline) outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.02f, 0.04f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return label;
    }

    private void OpenLeaderboard()
    {
        CloseDisplaySettings();
        m_leaderboard_open = true;
        if (m_leaderboard_text) m_leaderboard_text.text = LocalLeaderboard.FormatTopThree();
        if (m_leaderboard_panel)
        {
            m_leaderboard_panel.SetActive(true);
            m_leaderboard_panel.transform.SetAsLastSibling();
        }
    }

    private void CloseLeaderboard()
    {
        m_leaderboard_open = false;
        if (m_leaderboard_panel) m_leaderboard_panel.SetActive(false);
    }

    private void CreateDisplaySettingsUi()
    {
        if (!m_ui_title || m_display_panel) return;
        Text template = m_ui_title.GetComponentInChildren<Text>(true);
        if (!template) return;

        CreateMenuButton(template, m_ui_title, "DisplayButton", new Vector2(155f, -276f), new Vector2(280f, 58f),
            "DISPLAY", new Color(0.06f, 0.20f, 0.30f, 0.96f), OpenDisplaySettings);

        m_display_panel = new GameObject("DisplayPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = m_display_panel.GetComponent<RectTransform>();
        panelRect.SetParent(m_ui_title, false);
        panelRect.anchorMin = new Vector2(0.10f, 0.14f);
        panelRect.anchorMax = new Vector2(0.90f, 0.86f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = m_display_panel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.07f, 0.11f, 0.99f);
        Outline panelOutline = m_display_panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.18f, 0.82f, 0.95f, 0.9f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        Text header = CreateLabel(template, panelRect, "DISPLAY SETTINGS", 34, new Color(0.30f, 0.94f, 1f));
        header.rectTransform.anchorMin = new Vector2(0.05f, 0.84f);
        header.rectTransform.anchorMax = new Vector2(0.95f, 0.98f);

        for (int index = 0; index < WindowSizes.Length; index++)
        {
            int modeIndex = index;
            Vector2Int size = WindowSizes[index];
            CreateMenuButton(template, panelRect, $"Window{size.x}x{size.y}",
                new Vector2(0f, 155f - index * 74f), new Vector2(430f, 56f),
                $"WINDOWED   {size.x} × {size.y}", new Color(0.08f, 0.28f, 0.38f, 1f),
                () => SetDisplayMode(modeIndex));
        }

        CreateMenuButton(template, panelRect, "Fullscreen", new Vector2(0f, -67f), new Vector2(430f, 56f),
            "FULLSCREEN   NATIVE", new Color(0.20f, 0.34f, 0.46f, 1f), () => SetDisplayMode(3));

        m_display_status_text = CreateLabel(template, panelRect, string.Empty, 19, new Color(0.62f, 0.88f, 0.94f));
        m_display_status_text.rectTransform.anchorMin = new Vector2(0.08f, 0.28f);
        m_display_status_text.rectTransform.anchorMax = new Vector2(0.92f, 0.38f);

        CreateMenuButton(template, panelRect, "Close", new Vector2(0f, -247f), new Vector2(210f, 50f),
            "CLOSE", new Color(0.10f, 0.28f, 0.38f, 1f), CloseDisplaySettings);
        RefreshDisplayStatus();
        m_display_panel.SetActive(false);
    }

    private void ApplySavedDisplayMode()
    {
        m_selected_display_mode = Mathf.Clamp(PlayerPrefs.GetInt(DisplayModeKey, 0), 0, 3);
        ApplyDisplayMode(m_selected_display_mode, false);
    }

    private void SetDisplayMode(int modeIndex)
    {
        m_selected_display_mode = Mathf.Clamp(modeIndex, 0, 3);
        ApplyDisplayMode(m_selected_display_mode, true);
    }

    private void ApplyDisplayMode(int modeIndex, bool save)
    {
        if (modeIndex >= WindowSizes.Length)
        {
            Resolution native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Vector2Int size = WindowSizes[modeIndex];
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }
        if (save)
        {
            PlayerPrefs.SetInt(DisplayModeKey, modeIndex);
            PlayerPrefs.Save();
        }
        RefreshDisplayStatus();
    }

    private void RefreshDisplayStatus()
    {
        if (!m_display_status_text) return;
        m_display_status_text.text = m_selected_display_mode >= WindowSizes.Length
            ? "CURRENT: FULLSCREEN / NATIVE RESOLUTION"
            : $"CURRENT: {WindowSizes[m_selected_display_mode].x} × {WindowSizes[m_selected_display_mode].y} / WINDOWED";
    }

    private void OpenDisplaySettings()
    {
        CloseLeaderboard();
        m_display_open = true;
        RefreshDisplayStatus();
        if (m_display_panel)
        {
            m_display_panel.SetActive(true);
            m_display_panel.transform.SetAsLastSibling();
        }
    }

    private void CloseDisplaySettings()
    {
        m_display_open = false;
        if (m_display_panel) m_display_panel.SetActive(false);
    }

    private void OnDisable()
    {
        if (m_title_coroutine == null) return;
        StopCoroutine(m_title_coroutine);
        m_title_coroutine = null;
    }
}

public static class LocalLeaderboard
{
    private const string KeyPrefix = "QGames.LocalLeaderboard.V1.";
    private const int MaximumEntries = 3;

    private struct Entry
    {
        public int Score;
        public float Time;
        public int Level;
    }

    public static void Record(int score, float survivalTime, int level)
    {
        var entries = Load();
        entries.Add(new Entry
        {
            Score = Mathf.Max(0, score),
            Time = Mathf.Max(0f, survivalTime),
            Level = Mathf.Max(1, level)
        });
        entries.Sort((left, right) =>
        {
            int scoreOrder = right.Score.CompareTo(left.Score);
            if (scoreOrder != 0) return scoreOrder;
            int timeOrder = right.Time.CompareTo(left.Time);
            return timeOrder != 0 ? timeOrder : right.Level.CompareTo(left.Level);
        });
        if (entries.Count > MaximumEntries) entries.RemoveRange(MaximumEntries, entries.Count - MaximumEntries);

        PlayerPrefs.SetInt(KeyPrefix + "Count", entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            PlayerPrefs.SetInt(KeyPrefix + index + ".Score", entries[index].Score);
            PlayerPrefs.SetFloat(KeyPrefix + index + ".Time", entries[index].Time);
            PlayerPrefs.SetInt(KeyPrefix + index + ".Level", entries[index].Level);
        }
        PlayerPrefs.Save();
    }

    public static string FormatTopThree()
    {
        List<Entry> entries = Load();
        if (entries.Count == 0) return "NO RECORDS YET\n\nFinish a run to register your first score.";
        var lines = new List<string>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            int seconds = Mathf.FloorToInt(entry.Time);
            string medal = index == 0 ? "#1" : index == 1 ? "#2" : "#3";
            lines.Add($"{medal}   SCORE  {entry.Score:00000}\n      TIME  {seconds / 60:00}:{seconds % 60:00}     LEVEL  {entry.Level}");
        }
        return string.Join("\n", lines);
    }

    private static List<Entry> Load()
    {
        int count = Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + "Count", 0), 0, MaximumEntries);
        var entries = new List<Entry>(count);
        for (int index = 0; index < count; index++)
        {
            entries.Add(new Entry
            {
                Score = Mathf.Max(0, PlayerPrefs.GetInt(KeyPrefix + index + ".Score", 0)),
                Time = Mathf.Max(0f, PlayerPrefs.GetFloat(KeyPrefix + index + ".Time", 0f)),
                Level = Mathf.Max(1, PlayerPrefs.GetInt(KeyPrefix + index + ".Level", 1))
            });
        }
        return entries;
    }
}
