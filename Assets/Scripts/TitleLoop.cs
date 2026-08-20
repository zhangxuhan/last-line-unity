using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleLoop : MonoBehaviour
{
    [SerializeField] private StageLoop m_stage_loop;
    [Header("Layout")]
    [SerializeField] private Transform m_ui_title;
    private Coroutine m_title_coroutine;
    private GameObject m_leaderboard_panel;
    private Text m_leaderboard_text;
    private bool m_leaderboard_open;

    private void Start()
    {
        CreateLeaderboardUi();
        StartTitleLoop();
    }

    public void StartTitleLoop()
    {
        if (m_title_coroutine != null) StopCoroutine(m_title_coroutine);
        m_title_coroutine = StartCoroutine(TitleCoroutine());
    }

    private IEnumerator TitleCoroutine()
    {
        m_ui_title.gameObject.SetActive(true);
        CloseLeaderboard();
        while (true)
        {
            if (m_leaderboard_open && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseLeaderboard();
            }
            else if (!m_leaderboard_open && Input.GetKeyDown(KeyCode.Space))
            {
                m_ui_title.gameObject.SetActive(false);
                m_title_coroutine = null;
                m_stage_loop.StartStageLoop();
                yield break;
            }
            yield return null;
        }
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
        buttonRect.anchoredPosition = new Vector2(0f, -105f);
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
