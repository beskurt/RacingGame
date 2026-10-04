using UnityEngine;

/// <summary>
/// KI-Fahrer: fährt die aufgezeichnete Ideallinie mit demselben CarController wie der Spieler ab.
///
/// Lenken: Ziel-Punkt ein Stück voraus auf der Linie anpeilen ("Pure Pursuit") und genau so viel
/// einlenken, wie der CarController bei diesem Tempo für diese Kurve braucht.
/// Tempo: das aufgezeichnete Tempo der nächsten Meter (also schon vor der Kurve bremsen) mal 'Speed Factor'.
/// Bleibt die KI hängen, setzt sie kurz zurück; hilft das nicht, wird sie wie mit R zum letzten Checkpoint gesetzt.
///
/// Benutzung: Spielerauto duplizieren, beim Duplikat am CarController 'Player Controlled' ausschalten,
/// AIDriver hinzufügen und 'Racing Line' zuweisen.
/// </summary>
[RequireComponent(typeof(CarController))]
[DefaultExecutionOrder(-10)]   // Eingabe setzen, bevor der CarController sie liest
public class AIDriver : MonoBehaviour
{
    [SerializeField] private RacingLine racingLine;

    [Header("Tempo")]
    [Tooltip("Tempo relativ zur aufgezeichneten Runde. 1 = so schnell wie aufgezeichnet. Hiermit wird die KI ausbalanciert.")]
    [SerializeField, Range(0.5f, 1.2f)] private float speedFactor = 0.95f;
    [Tooltip("So weit voraus (Meter) wird das Tempo geplant, damit vor Kurven rechtzeitig gebremst wird.")]
    [SerializeField] private float brakeLookAhead = 0.8f;
    [Tooltip("Zusätzliche Planung pro m/s Tempo (Meter).")]
    [SerializeField] private float brakeLookAheadPerSpeed = 0.3f;
    [Tooltip("Mindesttempo (m/s), damit die KI nie einfach stehen bleibt.")]
    [SerializeField] private float minSpeed = 0.6f;
    [Tooltip("Wie stark Gas gegeben wird, wenn die KI zu langsam ist.")]
    [SerializeField] private float throttleGain = 2f;

    [Header("Lenkung")]
    [Tooltip("Abstand des Ziel-Punkts (Meter) im Stand.")]
    [SerializeField] private float lookAhead = 0.35f;
    [Tooltip("Zusätzlicher Abstand pro m/s Tempo (Meter). Größer = ruhiger, aber schneidet Kurven mehr.")]
    [SerializeField] private float lookAheadPerSpeed = 0.12f;

    [Header("Festgefahren")]
    [Tooltip("So lange (Sekunden) fast kein Tempo, dann setzt die KI zurück.")]
    [SerializeField] private float stuckTime = 1.2f;
    [SerializeField] private float reverseTime = 0.8f;
    [Tooltip("Nach so vielen erfolglosen Versuchen wird die KI zum letzten Checkpoint gesetzt.")]
    [SerializeField] private int maxReverseAttempts = 2;

    [SerializeField] private bool drawLine = true;

    private CarController car;
    private RaceManager race;
    private int index = -1;
    private float stuckTimer;
    private float reverseTimer;
    private int reverseAttempts;
    private float flippedTimer;

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
        if (car.IsPlayerControlled)
            Debug.LogWarning("[AIDriver] Am CarController ist 'Player Controlled' noch an: die KI kann nicht lenken.", this);
    }

    void Update()
    {
        if (racingLine == null || racingLine.Count < 10)
        {
            car.SetInput(0f, 0f, false);
            return;
        }

        bool racing = race == null || race.State == RaceManager.RaceState.Racing;
        float speed = car.ForwardSpeed;
        index = FindNearest(transform.position, index);

        // ---- Lenken: Pure Pursuit ----
        Vector3 target = PointAhead(index, lookAhead + lookAheadPerSpeed * Mathf.Abs(speed));
        Vector3 to = target - transform.position;
        float x = Vector3.Dot(to, transform.right);
        float z = Vector3.Dot(to, transform.forward);
        float steer;
        if (z <= 0.01f)
        {
            steer = Mathf.Sign(x);   // Ziel liegt hinter uns: voll einschlagen
        }
        else
        {
            float curvature = 2f * x / (x * x + z * z);   // Kreisbogen durch das Ziel
            steer = Mathf.Clamp(curvature * car.TurnRadiusAt(Mathf.Max(Mathf.Abs(speed), 0.3f)), -1f, 1f);
        }

        // ---- Tempo: das langsamste aufgezeichnete Tempo der nächsten Meter ----
        float plan = brakeLookAhead + brakeLookAheadPerSpeed * Mathf.Abs(speed);
        float targetSpeed = Mathf.Max(minSpeed, MinSpeedAhead(index, plan) * speedFactor);
        float diff = targetSpeed - speed;
        float throttle;
        if (diff > 0f) throttle = Mathf.Clamp01(0.3f + diff * throttleGain);
        else if (diff < -0.3f) throttle = -1f;   // deutlich zu schnell: bremsen
        else throttle = 0f;                       // etwas zu schnell: ausrollen

        // ---- Festgefahren / umgekippt ----
        if (racing && HandleStuck(speed, ref throttle, ref steer)) return;

        car.SetInput(throttle, steer, false);
    }

    /// <summary>Gibt true zurück, wenn die KI gerade zurückgesetzt wurde.</summary>
    private bool HandleStuck(float speed, ref float throttle, ref float steer)
    {
        float dt = Time.deltaTime;

        flippedTimer = transform.up.y < 0.3f ? flippedTimer + dt : 0f;
        if (flippedTimer > 1.5f)
        {
            ResetToCheckpoint();
            return true;
        }

        if (reverseTimer > 0f)
        {
            // Zurücksetzen mit Gegenlenken, um von der Wand wegzukommen
            reverseTimer -= dt;
            throttle = -1f;
            steer = -steer;
            return false;
        }

        stuckTimer = Mathf.Abs(speed) < 0.15f ? stuckTimer + dt : 0f;
        if (stuckTimer > stuckTime)
        {
            stuckTimer = 0f;
            if (++reverseAttempts > maxReverseAttempts)
            {
                ResetToCheckpoint();
                return true;
            }
            reverseTimer = reverseTime;
        }
        else if (Mathf.Abs(speed) > 1f)
        {
            reverseAttempts = 0;
        }
        return false;
    }

    private void ResetToCheckpoint()
    {
        if (race != null) race.ResetToLastCheckpoint(car);
        index = -1;   // danach Linie neu suchen
        stuckTimer = reverseTimer = flippedTimer = 0f;
        reverseAttempts = 0;
    }

    // ------------------------------------------------------------------ Linie

    /// <summary>Nächster Punkt der Linie. Sucht nur ein Stück vor/hinter dem letzten Punkt, damit die KI nicht auf eine Nachbarstrecke springt.</summary>
    private int FindNearest(Vector3 p, int last)
    {
        int best = -1;
        float bestDist = float.MaxValue;
        if (last >= 0)
        {
            for (int i = last - 10; i <= last + 60; i++)
            {
                float d = FlatSqr(racingLine.Point(i) - p);
                if (d < bestDist) { bestDist = d; best = racingLine.Wrap(i); }
            }
            if (bestDist < 1f) return best;   // weiter als 1 m weg: neu suchen
        }

        for (int i = 0; i < racingLine.Count; i++)
        {
            float d = FlatSqr(racingLine.Point(i) - p);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private Vector3 PointAhead(int from, float distance)
    {
        Vector3 prev = racingLine.Point(from);
        for (int i = from + 1; i < from + racingLine.Count; i++)
        {
            Vector3 p = racingLine.Point(i);
            float seg = Vector3.Distance(prev, p);
            if (seg >= distance) return Vector3.Lerp(prev, p, distance / seg);
            distance -= seg;
            prev = p;
        }
        return prev;
    }

    private float MinSpeedAhead(int from, float distance)
    {
        float min = racingLine.Speed(from);
        Vector3 prev = racingLine.Point(from);
        for (int i = from + 1; i < from + racingLine.Count && distance > 0f; i++)
        {
            Vector3 p = racingLine.Point(i);
            distance -= Vector3.Distance(prev, p);
            min = Mathf.Min(min, racingLine.Speed(i));
            prev = p;
        }
        return min;
    }

    private static float FlatSqr(Vector3 v) { return v.x * v.x + v.z * v.z; }

    void OnDrawGizmosSelected()
    {
        if (!drawLine || racingLine == null || racingLine.Count < 2) return;
        Gizmos.color = Color.magenta;
        for (int i = 0; i < racingLine.Count; i++)
            Gizmos.DrawLine(racingLine.Point(i), racingLine.Point(i + 1));
    }
}
