using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Zeichnet die Runden des Spielers als Ideallinie für die KI auf.
///
/// Benutzung: Auf das Spielerauto legen, Play drücken und ein paar saubere Runden fahren.
/// Die schnellste vollständige Runde wird in 'Racing Line' gespeichert (bleibt nach dem Play-Modus erhalten).
/// Danach 'Record' ausschalten, damit die Linie nicht mehr überschrieben wird.
/// </summary>
[RequireComponent(typeof(CarController))]
public class RacingLineRecorder : MonoBehaviour
{
    [SerializeField] private RacingLine racingLine;
    [Tooltip("Aufzeichnen an/aus. Nach einer guten Runde ausschalten.")]
    [SerializeField] private bool record = true;
    [Tooltip("Abstand zwischen zwei Punkten (Meter).")]
    [SerializeField] private float sampleDistance = 0.1f;
    [Tooltip("Die erste Runde beginnt im Stand und ist zu langsam: nicht speichern.")]
    [SerializeField] private bool skipFirstLap = true;
    [Tooltip("Nur speichern, wenn die Runde schneller ist als die bereits gespeicherte.")]
    [SerializeField] private bool onlyIfFaster = true;

    private CarController car;
    private RaceManager race;
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<float> speeds = new List<float>();
    private bool lapValid = true;

#if UNITY_EDITOR
    void Reset()
    {
        racingLine = RacingLine.LoadOrCreateDefault();
    }
#endif

    void Awake()
    {
        car = GetComponent<CarController>();
        race = FindFirstObjectByType<RaceManager>();

        // Runden werden nur für den Spieler gemeldet: an einem KI-Auto würde sonst die falsche Linie gespeichert
        if (!car.IsPlayerControlled)
        {
            Debug.LogWarning("[Recorder] Liegt auf einem KI-Auto und wird deaktiviert. Der Rekorder gehört aufs Spielerauto.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        if (race != null) race.LapCompleted += OnLapCompleted;
    }

    void OnDisable()
    {
        if (race != null) race.LapCompleted -= OnLapCompleted;
    }

    void Update()
    {
        if (!record || racingLine == null || race == null || race.State != RaceManager.RaceState.Racing) return;

        Vector3 p = transform.position;
        if (points.Count > 0)
        {
            float d = Vector3.Distance(points[points.Count - 1], p);
            if (d > 1f) lapValid = false;   // Sprung = mit R zurückgesetzt: diese Runde taugt nicht als Linie
            if (d < sampleDistance) return;
        }
        points.Add(p);
        speeds.Add(car.SpeedMS);
    }

    private void OnLapCompleted(int lap, float lapTime, bool newBest)
    {
        bool save = record && racingLine != null && lapValid && points.Count > 10
                    && !(skipFirstLap && lap == 1)
                    && !(onlyIfFaster && racingLine.lapTime > 0f && lapTime >= racingLine.lapTime);

        if (save)
        {
            racingLine.points = new List<Vector3>(points);
            racingLine.speeds = new List<float>(speeds);
            racingLine.lapTime = lapTime;
#if UNITY_EDITOR
            EditorUtility.SetDirty(racingLine);
            AssetDatabase.SaveAssets();
#endif
            Debug.Log("[Recorder] Ideallinie gespeichert: Runde " + lap + ", " + RaceUI.FormatTime(lapTime) + ", " + points.Count + " Punkte.", racingLine);
        }
        else if (record && !lapValid)
        {
            Debug.Log("[Recorder] Runde " + lap + " nicht gespeichert (Auto wurde zurückgesetzt).");
        }

        // Neue Runde beginnt hier
        points.Clear();
        speeds.Clear();
        lapValid = true;
        points.Add(transform.position);
        speeds.Add(car.SpeedMS);
    }
}
