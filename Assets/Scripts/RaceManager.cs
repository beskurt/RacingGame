using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zentrale Rennlogik: Countdown, Checkpoints in der richtigen Reihenfolge, Runden, Zeiten, Platzierung und Zurücksetzen (R).
/// Die Anzeige übernimmt RaceUI über die Events.
///
/// Jedes Auto in der Szene (Spieler und KI) hat seinen eigenen Rundenzähler und Fortschritt.
/// Eine Runde zählt nur, wenn alle Checkpoints 1, 2, 3 ... der Reihe nach durchfahren wurden
/// und danach die Start/Ziel-Linie (Index 0) überquert wird. Hin- und Herfahren über die Ziellinie zählt nicht.
/// Das Rennen endet, sobald das erste Auto seine letzte Runde beendet.
/// </summary>
public class RaceManager : MonoBehaviour
{
    public enum RaceState { Countdown, Racing, Finished }

    /// <summary>Stand eines Autos im Rennen.</summary>
    public class Racer
    {
        public CarController Car;
        public bool IsPlayer;
        public bool Started;            // Start/Ziel-Linie zum ersten Mal überquert
        public int NextCheckpoint;
        public int PassedThisLap;
        public Checkpoint LastCheckpoint;
        public Vector3 StartPosition;
        public Quaternion StartRotation;
        public readonly List<float> LapTimes = new List<float>();
        public float LapStartTime;
        public bool Finished;
        public float FinishTime;
        public int LapsDone => LapTimes.Count;
    }

    [Header("Rennen")]
    [Tooltip("Das Spielerauto. Leer = das Auto mit 'Player Controlled'.")]
    [SerializeField] private CarController player;
    [SerializeField, Min(1)] private int totalLaps = 3;
    [Tooltip("Stehen die Autos beim Start hinter der Start/Ziel-Linie? Dann zählt das erste Überfahren nur als Start, nicht als Runde.")]
    [SerializeField] private bool startBehindFinishLine = true;

    [Header("Countdown")]
    [Tooltip("Sekunden pro Zahl (3 - 2 - 1).")]
    [SerializeField] private float countdownStep = 1f;

    [Header("Zurücksetzen")]
    [SerializeField] private KeyCode resetKey = KeyCode.R;
    [Tooltip("So hoch über der Fahrbahn wird das Auto abgesetzt (Meter).")]
    [SerializeField] private float resetHeight = 0.03f;

    // --- Für die Anzeige (alles bezogen auf den Spieler) ---
    public event Action<string> CountdownStep;          // "3", "2", "1", "GO!"
    public event Action RaceStarted;
    public event Action<int, float, bool> LapCompleted; // Rundennummer, Rundenzeit, neue Bestzeit?
    public event Action RaceFinished;

    public RaceState State { get; private set; } = RaceState.Countdown;
    public CarController Player => player;
    public int TotalLaps => totalLaps;
    public int RacerCount => racers.Count;
    public IReadOnlyList<float> LapTimes => playerRacer != null ? playerRacer.LapTimes : (IReadOnlyList<float>)Array.Empty<float>();
    public int CurrentLap => playerRacer != null ? Mathf.Min(playerRacer.LapsDone + 1, totalLaps) : 1;
    /// <summary>Beste Rundenzeit des Spielers in Sekunden, -1 solange noch keine Runde fertig ist.</summary>
    public float BestLap { get; private set; } = -1f;
    /// <summary>Aktuelle Platzierung des Spielers (1 = vorne).</summary>
    public int PlayerPosition { get; private set; } = 1;
    /// <summary>Nach dem Rennen: hat der Spieler gewonnen?</summary>
    public bool PlayerWon { get; private set; }

    public float TotalTime
    {
        get
        {
            if (State == RaceState.Countdown) return 0f;
            if (playerRacer != null && playerRacer.Finished) return playerRacer.FinishTime - raceStartTime;
            return (State == RaceState.Finished ? raceEndTime : Time.time) - raceStartTime;
        }
    }

    public float CurrentLapTime
    {
        get
        {
            if (State == RaceState.Countdown || playerRacer == null) return 0f;
            if (playerRacer.Finished) return playerRacer.LapTimes[playerRacer.LapTimes.Count - 1];
            return (State == RaceState.Finished ? raceEndTime : Time.time) - playerRacer.LapStartTime;
        }
    }

    private readonly List<Racer> racers = new List<Racer>();
    private readonly Dictionary<CarController, Racer> byCar = new Dictionary<CarController, Racer>();
    private Racer playerRacer;
    private Checkpoint[] checkpoints = new Checkpoint[0];
    private float raceStartTime;
    private float raceEndTime;

    void Awake()
    {
        CollectCheckpoints();
        CollectRacers();
    }

    void Start()
    {
        if (playerRacer == null)
        {
            Debug.LogError("[RaceManager] Kein Spielerauto gefunden (CarController mit 'Player Controlled').", this);
            return;
        }
        StartCoroutine(Countdown());
    }

    void Update()
    {
        if (State == RaceState.Racing && Input.GetKeyDown(resetKey) && player != null)
            ResetToLastCheckpoint(player);

        if (State == RaceState.Racing) UpdatePositions();
    }

    // ------------------------------------------------------------------ Ablauf

    private IEnumerator Countdown()
    {
        State = RaceState.Countdown;
        SetAllControls(false);

        for (int i = 3; i >= 1; i--)
        {
            CountdownStep?.Invoke(i.ToString());
            yield return new WaitForSeconds(countdownStep);
        }

        State = RaceState.Racing;
        raceStartTime = Time.time;
        foreach (Racer r in racers) r.LapStartTime = raceStartTime;
        SetAllControls(true);
        CountdownStep?.Invoke("GO!");
        RaceStarted?.Invoke();
    }

    /// <summary>Wird von den Checkpoints aufgerufen.</summary>
    public void OnCheckpointPassed(Checkpoint checkpoint, CarController car)
    {
        // Auch im Countdown annehmen: Steht ein Auto beim Start schon im Ziel-Trigger, wird das hier erkannt
        if (State == RaceState.Finished || !byCar.TryGetValue(car, out Racer r) || r.Finished) return;
        if (checkpoint.Index != r.NextCheckpoint) return;   // falsche Reihenfolge oder rückwärts

        r.LastCheckpoint = checkpoint;
        if (checkpoint.Index == 0)
        {
            if (r.PassedThisLap > 0) CompleteLap(r);         // sonst: erstes Überfahren beim Start
            r.Started = true;
            r.PassedThisLap = 0;
        }
        else
        {
            r.PassedThisLap++;
        }
        r.NextCheckpoint = (checkpoint.Index + 1) % checkpoints.Length;
    }

    private void CompleteLap(Racer r)
    {
        if (State != RaceState.Racing) return;

        float lapTime = Time.time - r.LapStartTime;
        r.LapStartTime = Time.time;
        r.LapTimes.Add(lapTime);

        if (r.IsPlayer)
        {
            bool newBest = BestLap < 0f || lapTime < BestLap;
            if (newBest) BestLap = lapTime;
            LapCompleted?.Invoke(r.LapsDone, lapTime, newBest);
        }

        if (r.LapsDone >= totalLaps)
        {
            r.Finished = true;
            r.FinishTime = Time.time;
            EndRace(r);   // wer zuerst die letzte Runde beendet, gewinnt: das Rennen ist vorbei
        }
    }

    private void EndRace(Racer winner)
    {
        State = RaceState.Finished;
        raceEndTime = Time.time;
        UpdatePositions();
        PlayerWon = winner.IsPlayer;
        SetAllControls(false);   // keine Eingabe mehr, die Autos rollen aus
        RaceFinished?.Invoke();
    }

    private void SetAllControls(bool enabled)
    {
        foreach (Racer r in racers) r.Car.ControlsEnabled = enabled;
    }

    // ------------------------------------------------------------------ Platzierung

    /// <summary>Fortschritt auf der Strecke: gefahrene Checkpoints plus Anteil der Strecke bis zum nächsten.</summary>
    private float Progress(Racer r)
    {
        if (r.Finished) return 1e6f - r.FinishTime;   // wer früher im Ziel war, liegt vorne

        Vector3 from = r.LastCheckpoint != null ? r.LastCheckpoint.transform.position : r.StartPosition;
        Vector3 to = checkpoints[r.NextCheckpoint].transform.position;
        float span = Vector3.Distance(from, to);
        float fraction = span > 0.01f ? Mathf.Clamp01(1f - Vector3.Distance(r.Car.transform.position, to) / span) : 0f;

        float done = r.LapsDone * checkpoints.Length + r.PassedThisLap;
        return (r.Started ? done : -1f) + fraction;
    }

    private void UpdatePositions()
    {
        if (playerRacer == null || checkpoints.Length < 2) return;
        float mine = Progress(playerRacer);
        int ahead = 0;
        foreach (Racer r in racers)
            if (r != playerRacer && Progress(r) > mine) ahead++;
        PlayerPosition = ahead + 1;
    }

    // ------------------------------------------------------------------ Zurücksetzen

    /// <summary>Setzt ein Auto zum zuletzt durchfahrenen Checkpoint (oder zum Start): Position, Drehung, Geschwindigkeit.</summary>
    public void ResetToLastCheckpoint(CarController car)
    {
        if (!byCar.TryGetValue(car, out Racer r)) return;

        Vector3 position = r.StartPosition;
        Quaternion rotation = r.StartRotation;
        if (r.LastCheckpoint != null)
        {
            Transform cp = r.LastCheckpoint.transform;
            Vector3 forward = Vector3.ProjectOnPlane(cp.forward, Vector3.up);
            rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward, Vector3.up);
            position = GroundBelow(cp.position, car) + Vector3.up * resetHeight;
        }
        car.ResetTo(position, rotation);
    }

    /// <summary>Fahrbahn unter einem Punkt finden (Trigger und das Auto selbst werden ignoriert).</summary>
    private static Vector3 GroundBelow(Vector3 point, CarController car)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        Rigidbody own = car.GetComponent<Rigidbody>();

        float best = float.MaxValue;
        Vector3 ground = point;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.attachedRigidbody == own) continue;
            if (h.distance < best)
            {
                best = h.distance;
                ground = h.point;
            }
        }
        return ground;
    }

    // ------------------------------------------------------------------ Einsammeln

    private void CollectRacers()
    {
        CarController[] cars = FindObjectsByType<CarController>(FindObjectsSortMode.None);
        if (player == null)
            foreach (CarController c in cars)
                if (c.IsPlayerControlled) { player = c; break; }

        int start = startBehindFinishLine ? 0 : 1 % Mathf.Max(1, checkpoints.Length);
        foreach (CarController c in cars)
        {
            Racer r = new Racer
            {
                Car = c,
                IsPlayer = c == player,
                Started = !startBehindFinishLine,
                NextCheckpoint = start,
                StartPosition = c.transform.position,
                StartRotation = c.transform.rotation
            };
            racers.Add(r);
            byCar[c] = r;
            if (r.IsPlayer) playerRacer = r;
        }
    }

    private void CollectCheckpoints()
    {
        checkpoints = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        Array.Sort(checkpoints, (a, b) => a.Index.CompareTo(b.Index));

        for (int i = 0; i < checkpoints.Length; i++)
        {
            checkpoints[i].Init(this);
            if (checkpoints[i].Index != i)
                Debug.LogError("[RaceManager] Checkpoint-Indizes müssen 0, 1, 2 ... ohne Lücken und doppelte sein. " +
                               "Problem bei '" + checkpoints[i].name + "' (Index " + checkpoints[i].Index + ").", checkpoints[i]);
        }

        if (checkpoints.Length < 2)
            Debug.LogError("[RaceManager] Es werden mindestens 2 Checkpoints gebraucht (0 = Start/Ziel und weitere).", this);
    }
}
