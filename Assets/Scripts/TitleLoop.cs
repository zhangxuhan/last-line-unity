using System.Collections;
using UnityEngine;

public class TitleLoop : MonoBehaviour
{
    [SerializeField] private StageLoop m_stage_loop;
    [Header("Layout")]
    [SerializeField] private Transform m_ui_title;
    private Coroutine m_title_coroutine;

    private void Start() => StartTitleLoop();

    public void StartTitleLoop()
    {
        if (m_title_coroutine != null) StopCoroutine(m_title_coroutine);
        m_title_coroutine = StartCoroutine(TitleCoroutine());
    }

    private IEnumerator TitleCoroutine()
    {
        m_ui_title.gameObject.SetActive(true);
        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                m_ui_title.gameObject.SetActive(false);
                m_title_coroutine = null;
                m_stage_loop.StartStageLoop();
                yield break;
            }
            yield return null;
        }
    }

    private void OnDisable()
    {
        if (m_title_coroutine == null) return;
        StopCoroutine(m_title_coroutine);
        m_title_coroutine = null;
    }
}
