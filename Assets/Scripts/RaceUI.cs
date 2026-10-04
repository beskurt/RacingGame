using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Anzeige für das Rennen: HUD (Tempo, Runde, Zeiten), Countdown "3 - 2 - 1 - GO!" und Ergebnis-Canvas.
/// Die drei Bereiche werden über CanvasGroups ein- und ausgeblendet.
///
/// Am schnellsten eingerichtet über das Menü Tools -> Rennen -> Race-UI erzeugen.
/// </summary>
public class RaceUI : MonoBehaviour
{
    [SerializeField] private RaceManager race;
    [Tooltip("Dauer zum Ein- und Ausblenden (Sekunden).")]
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("HUD")]
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text lapText;
    [SerializeField] private TMP_Text positionText;
    [SerializeField] private TMP_Text totalTimeText;
    [SerializeField] private TMP_Text lapTimeText;
    [SerializeField] private TMP_Text bestLapText;
    [Tooltip("Farbe, in der die Bestzeit kurz aufleuchtet, wenn sie verbessert wird.")]
    [SerializeField] private Color bestHighlightColor = new Color(0.3f, 1f, 0.4f);
    [Tooltip("Das Auto ist sehr klein (ca. 0,2 m). 1 = echte km/h, z. B. 20 = so, als wäre es ein echtes Auto.")]
    [SerializeField] private float speedDisplayFactor = 1f;

    [Header("Countdown")]
    [SerializeField] private CanvasGroup countdownGroup;
    [SerializeField] private TMP_Text countdownText;

    [Header("Ergebnis")]
    [SerializeField] private CanvasGroup resultsGroup;
    [Tooltip("Überschrift: Sieg oder Niederlage.")]
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultPositionText;
    [SerializeField] private string victoryTitle = "SIEG!";
    [SerializeField] private Color victoryColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private string defeatTitle = "NIEDERLAGE";
    [SerializeField] private Color defeatColor = new Color(1f, 0.3f, 0.25f);
    [SerializeField] private TMP_Text resultTotalText;
    [SerializeField] private TMP_Text resultLapsText;
    [SerializeField] private TMP_Text resultBestText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button exitButton;

    // Ziffern mit fester Breite, damit der Text beim Hochzählen nicht hin und her springt
    private const string Mono = "<mspace=0.6em>";
    private const string MonoEnd = "</mspace>";

    private Color bestNormalColor;
    private Coroutine bestRoutine;
    private Coroutine countdownRoutine;

    void Awake()
    {
        if (race == null) race = FindFirstObjectByType<RaceManager>();

        SetVisible(hudGroup, false);
        SetVisible(countdownGroup, false);
        SetVisible(resultsGroup, false);
        if (bestLapText != null) bestNormalColor = bestLapText.color;

        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (exitButton != null) exitButton.onClick.AddListener(Exit);
    }

    void OnEnable()
    {
        if (race == null) return;
        race.CountdownStep += OnCountdownStep;
        race.RaceStarted += OnRaceStarted;
        race.LapCompleted += OnLapCompleted;
        race.RaceFinished += OnRaceFinished;
    }

    void OnDisable()
    {
        if (race == null) return;
        race.CountdownStep -= OnCountdownStep;
        race.RaceStarted -= OnRaceStarted;
        race.LapCompleted -= OnLapCompleted;
        race.RaceFinished -= OnRaceFinished;
    }

    void Update()
    {
        if (race == null || race.Player == null) return;

        if (speedText != null) speedText.text = Mono + Mathf.RoundToInt(race.Player.SpeedKmh * speedDisplayFactor) + MonoEnd;
        if (lapText != null) lapText.text = "Lap " + race.CurrentLap + " / " + race.TotalLaps;
        if (positionText != null) positionText.text = "P" + race.PlayerPosition + " / " + race.RacerCount;
        if (totalTimeText != null) totalTimeText.text = "Gesamt  " + Mono + FormatTime(race.TotalTime) + MonoEnd;
        if (lapTimeText != null) lapTimeText.text = "Runde   " + Mono + FormatTime(race.CurrentLapTime) + MonoEnd;
        if (bestLapText != null) bestLapText.text = "Beste   " + Mono + FormatTime(race.BestLap) + MonoEnd;
    }

    // ------------------------------------------------------------------ Events

    private void OnCountdownStep(string text)
    {
        if (countdownText == null) return;
        countdownText.text = text;
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownPulse());
    }

    private void OnRaceStarted()
    {
        StartCoroutine(Fade(hudGroup, true));
    }

    private void OnLapCompleted(int lap, float time, bool newBest)
    {
        if (!newBest || bestLapText == null) return;
        if (bestRoutine != null) StopCoroutine(bestRoutine);
        bestRoutine = StartCoroutine(HighlightBest());
    }

    private void OnRaceFinished()
    {
        if (resultTitleText != null)
        {
            resultTitleText.text = race.PlayerWon ? victoryTitle : defeatTitle;
            resultTitleText.color = race.PlayerWon ? victoryColor : defeatColor;
        }
        if (resultPositionText != null) resultPositionText.text = "Platz " + race.PlayerPosition + " / " + race.RacerCount;
        if (resultTotalText != null) resultTotalText.text = "Gesamtzeit  " + Mono + FormatTime(race.TotalTime) + MonoEnd;
        if (resultBestText != null) resultBestText.text = "Beste Runde  " + Mono + FormatTime(race.BestLap) + MonoEnd;

        if (resultLapsText != null)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < race.LapTimes.Count; i++)
            {
                float t = race.LapTimes[i];
                bool best = Mathf.Approximately(t, race.BestLap);
                if (best) sb.Append("<color=#" + ColorUtility.ToHtmlStringRGB(bestHighlightColor) + ">");
                sb.Append("Runde " + (i + 1) + "   " + Mono + FormatTime(t) + MonoEnd);
                if (best) sb.Append("</color>");
                sb.Append('\n');
            }
            resultLapsText.text = sb.ToString();
        }

        StartCoroutine(Fade(hudGroup, false));
        StartCoroutine(Fade(resultsGroup, true));
    }

    // ------------------------------------------------------------------ Buttons

    private void Restart()
    {
        // Szene neu laden: Auto zurück an den Start, Runden und Zeiten auf null, Countdown startet neu
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Exit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // im Editor beendet Application.Quit() nichts
#endif
    }

    // ------------------------------------------------------------------ Effekte

    private IEnumerator CountdownPulse()
    {
        // Einblenden mit kleinem "Pop", kurz halten, ausblenden
        Transform t = countdownText.transform;
        float inTime = 0.2f, hold = 0.4f, outTime = 0.3f;

        for (float e = 0f; e < inTime; e += Time.deltaTime)
        {
            float k = e / inTime;
            countdownGroup.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, k);
            yield return null;
        }
        countdownGroup.alpha = 1f;
        t.localScale = Vector3.one;

        yield return new WaitForSeconds(hold);

        for (float e = 0f; e < outTime; e += Time.deltaTime)
        {
            countdownGroup.alpha = 1f - e / outTime;
            yield return null;
        }
        countdownGroup.alpha = 0f;
    }

    private IEnumerator HighlightBest()
    {
        Transform t = bestLapText.transform;
        float duration = 1.5f;
        for (float e = 0f; e < duration; e += Time.deltaTime)
        {
            float k = e / duration;
            bestLapText.color = Color.Lerp(bestHighlightColor, bestNormalColor, k * k);
            t.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Mathf.Min(1f, k * 3f) * Mathf.PI));
            yield return null;
        }
        bestLapText.color = bestNormalColor;
        t.localScale = Vector3.one;
    }

    private IEnumerator Fade(CanvasGroup group, bool show)
    {
        if (group == null) yield break;
        float from = group.alpha, to = show ? 1f : 0f;
        for (float e = 0f; e < fadeDuration; e += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, e / fadeDuration);
            yield return null;
        }
        SetVisible(group, show);
    }

    private static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    /// <summary>Zeit als mm:ss.mmm, z. B. 01:23.456. Negative Werte = noch keine Zeit.</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) return "--:--.---";
        int minutes = (int)(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return minutes.ToString("00") + ":" + rest.ToString("00.000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
