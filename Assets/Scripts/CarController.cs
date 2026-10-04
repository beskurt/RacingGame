using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arcade-Auto für Unity 6 (linearVelocity / linearDamping).
///
/// Das Auto berührt die Fahrbahn nicht mit Collidern, sondern "schwebt" auf vier Federn.
/// Jede Feder tastet den Boden unter ihrem Rad mit einem SphereCast ab. Dadurch können
/// Kachelnähte, kleine Stufen und Lücken das Auto nicht mehr abbremsen: Die Feder fährt
/// einfach weicher oder härter ein. Die Karosserie-Box berührt nur noch Barrieren.
///
/// Alle Standardwerte passen zum Maßstab deiner Szene (Auto ca. 0,2 m lang, Kachel 1 m).
///
/// Rigidbody-Einstellungen (werden NICHT vom Skript gesetzt, sondern direkt am Rigidbody):
/// Interpolate, Collision Detection = Continuous, Constraints = Freeze Rotation X und Z (das Auto kann nicht umkippen).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class CarController : MonoBehaviour
{
    [Header("Antrieb (m/s und m/s²)")]
    [SerializeField] private float topSpeed = 4f;
    [SerializeField] private float reverseTopSpeed = 2f;
    [SerializeField] private float forwardAcceleration = 1.75f;
    [SerializeField] private float backwardAcceleration = 2.5f;
    [Tooltip("Wie schnell das Gas aufgebaut wird (pro Sekunde, 1 = voll in 1 s).")]
    [SerializeField] private float throttleResponse = 3f;

    [Header("Bremsen und Ausrollen (m/s²)")]
    [Tooltip("Bremsen mit S, solange das Auto noch vorwärts rollt.")]
    [SerializeField] private float brakeDeceleration = 8f;
    [Tooltip("Ausrollen ohne Tastendruck.")]
    [SerializeField] private float coastDeceleration = 1.5f;
    [Tooltip("Handbremse (Leertaste). 4 m/s² = ca. 1,25 s von 5 m/s bis zum Stillstand.")]
    [SerializeField] private float handbrakeDeceleration = 4f;

    [Header("Lenkung")]
    [Tooltip("Kleinster Kurvenradius (Meter) bei niedrigem Tempo.")]
    [SerializeField] private float minTurnRadius = 0.35f;
    [Tooltip("Kurvenradius (Meter) bei Höchstgeschwindigkeit. Größer = weniger 'Einkaufswagen'.")]
    [SerializeField] private float maxTurnRadius = 2f;
    [Tooltip("Obergrenze der Drehrate in Grad pro Sekunde.")]
    [SerializeField] private float maxYawRate = 160f;
    [Tooltip("Maximale Querbeschleunigung (m/s²). Begrenzt die Lenkung bei hohem Tempo: Kurvenradius = Tempo² / Wert. Kleiner = weniger Lenkung bei hohem Tempo.")]
    [SerializeField] private float maxLateralAcceleration = 5f;
    [Tooltip("Wie schnell das Lenkrad bei niedrigem Tempo ein- und ausschlägt (pro Sekunde).")]
    [SerializeField] private float steerResponse = 6f;
    [Tooltip("Wie schnell das Lenkrad bei Höchstgeschwindigkeit einschlägt (pro Sekunde). Kleiner = sanfteres Einlenken.")]
    [SerializeField] private float steerResponseHighSpeed = 2.5f;
    [Tooltip("Zurück zur Mitte geht das Lenkrad um diesen Faktor schneller.")]
    [SerializeField] private float steerCenteringFactor = 1.5f;
    [Tooltip("Wie schnell sich die Drehrate aufbauen darf (Grad/s²). Kleiner = weicheres, trägeres Einlenken.")]
    [SerializeField] private float yawAcceleration = 500f;
    [Tooltip("Seitengrip: wie schnell die Fahrtrichtung der Fahrzeugnase folgt. Hoch = auf Schienen, niedrig = Rutschen.")]
    [SerializeField] private float grip = 12f;
    [Tooltip("Grip bei gezogener Handbremse (Drift).")]
    [SerializeField] private float handbrakeGrip = 1.5f;

    [Header("Federung")]
    [Tooltip("Wie weit die Federn im Stand einsinken (Meter). Größer = weicher und stabiler, kleiner = straffer.")]
    [SerializeField] private float suspensionSag = 0.01f;
    [Tooltip("Dämpfung relativ zur kritischen Dämpfung. 0,5–0,8 ist sinnvoll, 1 = kein Nachwippen.")]
    [SerializeField, Range(0.1f, 1.5f)] private float suspensionDamping = 0.6f;
    [Tooltip("Wie weit ein Rad nach unten ausfedern darf (Meter), bevor es den Bodenkontakt verliert.")]
    [SerializeField] private float suspensionDroop = 0.01f;
    [Tooltip("Breite des Abtast-Strahls relativ zum Reifenradius. Breiter überbrückt größere Lücken zwischen Kacheln.")]
    [SerializeField, Range(0.1f, 1f)] private float probeWidth = 0.6f;
    [Tooltip("Welche Layer als Boden gelten. 'Everything' ist ok, Trigger werden ignoriert.")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("Mindestabstand (Meter) zwischen Karosserie-Box und Boden, wenn die Räder aufliegen. Muss größer sein als die Randstreifen der Straße (Kenney: 1 cm), sonst bleibt die Box daran hängen.")]
    [SerializeField] private float bodyClearance = 0.02f;
    [Tooltip("So lange (Sekunden) gilt das Auto ohne Radkontakt noch als 'am Boden'.")]
    [SerializeField] private float groundGraceTime = 0.12f;

    [Header("Physik")]
    [Tooltip("Reibungsfreies Material auf der Karosserie: Das Auto rutscht an Barrieren entlang, statt hängen zu bleiben.")]
    [SerializeField] private bool frictionlessBody = true;

    [Header("Steuerung")]
    [Tooltip("An = Tastatur (Spieler). Aus = ein anderes Skript (z. B. AIDriver) steuert über SetInput().")]
    [SerializeField] private bool playerControlled = true;

    [Header("Debug")]
    [Tooltip("Zeichnet die Federn: grün = Bodenkontakt, rot = in der Luft (Scene-Ansicht).")]
    [SerializeField] private bool debugSuspension = false;

    [Header("Visuals")]
    [SerializeField] private Transform[] frontWheels;
    [SerializeField] private Transform[] rearWheels;
    [SerializeField] private float maxWheelTurnAngle = 30f;
    [Tooltip("Reifenradius automatisch aus dem Mesh bestimmen.")]
    [SerializeField] private bool autoWheelRadius = true;
    [Tooltip("Reifenradius in Metern (Weltmaßstab), nur wenn Auto-Radius aus ist oder kein Renderer gefunden wird.")]
    [SerializeField] private float wheelRadius = 0.018f;
    [Tooltip("Lokale Achse, um die sich die Reifen drehen. Wenn ein Rad falsch dreht: (0,0,1) oder (0,1,0) probieren.")]
    [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;
    [Tooltip("Höchste sichtbare Drehgeschwindigkeit der Reifen (Grad/s). Echte Drehzahl wäre bei diesem kleinen Auto ca. 30 Umdrehungen/s und flimmert. 720 = 2 Umdrehungen/s.")]
    [SerializeField] private float maxWheelSpinSpeed = 720f;
    [Tooltip("Feinkorrektur der Radhöhe (Meter, Weltmaßstab). Negativ = Räder tiefer.")]
    [SerializeField] private float wheelVisualOffset = 0f;

    // --- Für andere Skripte (HUD, Motorsound, RaceManager) ---
    /// <summary>Auf false setzen für Countdown/Ziel: keine Eingabe, das Auto rollt aus.</summary>
    public bool ControlsEnabled { get; set; } = true;
    public float SpeedMS => rb != null ? rb.linearVelocity.magnitude : 0f;
    public float SpeedKmh => SpeedMS * 3.6f;
    public float NormalizedSpeed => Mathf.Clamp01(SpeedMS / topSpeed);
    /// <summary>Geschwindigkeit in Fahrtrichtung (m/s), rückwärts negativ.</summary>
    public float ForwardSpeed => rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
    public bool IsGrounded { get; private set; }
    public bool IsPlayerControlled => playerControlled;

    /// <summary>Eingabe von außen (KI). move: -1..1 (Gas/Bremse), turn: -1..1 (Lenkung). Wirkt nur ohne Spielersteuerung.</summary>
    public void SetInput(float move, float turn, bool handbrake)
    {
        externalMove = Mathf.Clamp(move, -1f, 1f);
        externalTurn = Mathf.Clamp(turn, -1f, 1f);
        externalHandbrake = handbrake;
    }

    /// <summary>Kurvenradius (Meter) bei vollem Lenkeinschlag und diesem Tempo.</summary>
    public float TurnRadiusAt(float speed)
    {
        float s = Mathf.Abs(speed);
        float radius = Mathf.Lerp(minTurnRadius, maxTurnRadius, Mathf.Clamp01(s / topSpeed));
        // Bei hohem Tempo begrenzt der Grip: Radius mindestens v² / a_quer
        return Mathf.Max(radius, s * s / Mathf.Max(0.1f, maxLateralAcceleration));
    }

    private class Wheel
    {
        public Transform visual;
        public bool isFront;
        public Vector3 anchorLocal;     // Radmitte im lokalen Raum des Autos
        public float radius;            // Weltmaßstab
        public Quaternion baseRot;
        public Vector3 centerParent;    // echte Radmitte im Raum des Eltern-Objekts
        public Vector3 centerLocal;     // echte Radmitte im eigenen Raum des Rad-Objekts (unskaliert)
        public float distance;          // Radmitte -> Boden
    }

    private readonly List<Wheel> wheels = new List<Wheel>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private Rigidbody rb;

    private float moveInput;
    private float smoothedThrottle;
    private float steerInput;
    private float currentYawRate;   // Grad pro Sekunde
    private bool isHandbrake;
    private float externalMove;
    private float externalTurn;
    private bool externalHandbrake;

    private Vector3 groundNormal = Vector3.up;
    private float groundTimer;
    private float spinRadius = 0.018f;
    private float frontSpin;
    private float rearSpin;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        BuildWheels();
        RaiseBodyBox();
        if (frictionlessBody) ApplyFrictionlessMaterial();
    }

    /// <summary>Setzt das Auto an eine Stelle zurück (z. B. letzter Checkpoint): Position, Drehung und Geschwindigkeit.</summary>
    public void ResetTo(Vector3 position, Quaternion rotation)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = position;
        rb.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);

        smoothedThrottle = 0f;
        steerInput = 0f;
        currentYawRate = 0f;
    }

    // ------------------------------------------------------------------ Setup

    private void BuildWheels()
    {
        wheels.Clear();
        AddWheels(frontWheels, true);
        AddWheels(rearWheels, false);

        if (wheels.Count == 0)
        {
            Debug.LogWarning("[CarController] Keine Räder zugewiesen. Die Federung braucht Front- und Hinterräder.", this);
            return;
        }

        float sum = 0f;
        foreach (Wheel w in wheels) sum += w.radius;
        spinRadius = sum / wheels.Count;
    }

    private void AddWheels(Transform[] list, bool front)
    {
        if (list == null) return;
        foreach (Transform t in list)
        {
            if (t == null) continue;

            Vector3 center = t.position;
            float radius = wheelRadius;
            Renderer r = t.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                center = r.bounds.center;
                if (autoWheelRadius && r.bounds.extents.y > 0.001f) radius = r.bounds.extents.y;
            }

            // Der Drehpunkt (Pivot) liegt oft nicht genau in der Radmitte. Damit das Rad nicht eiert,
            // drehen wir es rechnerisch um seine echte Mitte aus dem Mesh.
            Transform parent = t.parent;
            Vector3 centerParent = parent != null ? parent.InverseTransformPoint(center) : center;
            Vector3 rel = Quaternion.Inverse(t.localRotation) * (centerParent - t.localPosition);
            Vector3 sc = t.localScale;
            Vector3 centerLocal = new Vector3(rel.x / sc.x, rel.y / sc.y, rel.z / sc.z);

            wheels.Add(new Wheel
            {
                visual = t,
                isFront = front,
                anchorLocal = transform.InverseTransformPoint(center),
                radius = radius,
                baseRot = t.localRotation,
                centerParent = centerParent,
                centerLocal = centerLocal,
                distance = radius
            });
        }
    }

    /// <summary>Hebt den Boden der Karosserie-Box über die Räder, damit sie die Fahrbahn nie berührt.</summary>
    private void RaiseBodyBox()
    {
        BoxCollider box = GetComponent<Collider>() as BoxCollider;
        if (box == null || wheels.Count == 0) return;

        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float lowest = float.MaxValue;
        foreach (Wheel w in wheels)
            lowest = Mathf.Min(lowest, w.anchorLocal.y - w.radius / scale);

        float bottom = box.center.y - box.size.y * 0.5f;
        float top = box.center.y + box.size.y * 0.5f;
        float minBottom = lowest + bodyClearance / scale;

        if (bottom < minBottom && top - minBottom > 0.02f)
        {
            box.size = new Vector3(box.size.x, top - minBottom, box.size.z);
            box.center = new Vector3(box.center.x, (top + minBottom) * 0.5f, box.center.z);
        }
    }

    private void ApplyFrictionlessMaterial()
    {
        PhysicsMaterial mat = new PhysicsMaterial("CarNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.material = mat;
    }

    // ------------------------------------------------------------------ Eingabe

    void Update()
    {
        float rawMove = 0f;
        float rawTurn = 0f;
        isHandbrake = false;

        if (ControlsEnabled && playerControlled)
        {
            // Achsen aus dem Input Manager: W/S bzw. Pfeiltasten (Vertical), A/D (Horizontal), auch Gamepad.
            // Raw = ohne Unitys eigene Glättung, geglättet wird unten selbst.
            rawMove = Input.GetAxisRaw("Vertical");
            rawTurn = Input.GetAxisRaw("Horizontal");
            isHandbrake = Input.GetKey(KeyCode.Space);
        }
        else if (ControlsEnabled)
        {
            rawMove = externalMove;
            rawTurn = externalTurn;
            isHandbrake = externalHandbrake;
        }

        moveInput = rawMove;
        smoothedThrottle = Mathf.MoveTowards(smoothedThrottle, rawMove, throttleResponse * Time.deltaTime);
        // Lenkrad: bei hohem Tempo langsamer einschlagen, zur Mitte schneller zurück
        float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / topSpeed);
        float response = Mathf.Lerp(steerResponse, steerResponseHighSpeed, speedRatio);
        bool centering = Mathf.Abs(rawTurn) < Mathf.Abs(steerInput) || Mathf.Sign(rawTurn) != Mathf.Sign(steerInput);
        if (centering) response *= steerCenteringFactor;
        steerInput = Mathf.MoveTowards(steerInput, rawTurn, response * Time.deltaTime);

        UpdateWheelVisuals();
    }

    // ------------------------------------------------------------------ Physik

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        int groundedWheels = UpdateSuspension(dt, out Vector3 normalSum);

        if (groundedWheels > 0)
        {
            groundNormal = normalSum.normalized;
            groundTimer = groundGraceTime;
        }
        else
        {
            groundTimer -= dt;
        }

        IsGrounded = groundTimer > 0f;
        if (!IsGrounded) return;   // in der Luft: nur normale Physik

        Drive(dt);
    }

    /// <summary>Federkräfte an allen Rädern. Gibt die Zahl der Räder mit Bodenkontakt zurück.</summary>
    private int UpdateSuspension(float dt, out Vector3 normalSum)
    {
        normalSum = Vector3.zero;
        int n = wheels.Count;
        if (n == 0) return 0;

        Vector3 up = transform.up;

        // Federhärte so, dass das Auto im Stand genau um 'suspensionSag' einsinkt.
        // Begrenzungen halten die Feder bei jedem Fixed Timestep stabil.
        float kTotal = Physics.gravity.magnitude / Mathf.Max(0.001f, suspensionSag);
        kTotal = Mathf.Min(kTotal, (1.2f / dt) * (1.2f / dt));
        float cTotal = Mathf.Min(2f * Mathf.Sqrt(kTotal) * suspensionDamping, 0.9f / dt);

        int grounded = 0;
        foreach (Wheel w in wheels)
        {
            Vector3 anchor = transform.TransformPoint(w.anchorLocal);
            float rest = w.radius + suspensionSag;
            float maxLen = rest + suspensionDroop;

            bool hit = ProbeGround(anchor, up, w.radius, maxLen, out float dist, out Vector3 nrm);
            w.distance = dist;   // ohne Bodenkontakt liefert ProbeGround maxLen

            if (debugSuspension)
                Debug.DrawLine(anchor, anchor - up * w.distance, hit ? Color.green : Color.red);

            if (!hit) continue;

            grounded++;
            normalSum += nrm;

            float compression = rest - dist;
            float pointVel = Vector3.Dot(rb.GetPointVelocity(anchor), up);
            float accel = (kTotal * compression - cTotal * pointVel) / n;
            if (accel > 0f)
                rb.AddForceAtPosition(up * (accel * rb.mass), anchor, ForceMode.Force);
        }
        return grounded;
    }

    /// <summary>Tastet den Boden unter einem Rad ab. dist = Abstand Radmitte -> Fahrbahn.</summary>
    private bool ProbeGround(Vector3 anchor, Vector3 up, float radius, float maxLen, out float dist, out Vector3 normal)
    {
        float r = radius * probeWidth;
        float startOffset = radius;                 // etwas oberhalb der Radmitte starten
        Vector3 origin = anchor + up * startOffset;
        float castLen = startOffset + maxLen - r;

        int count = Physics.SphereCastNonAlloc(origin, r, -up, hitBuffer, castLen, groundLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        normal = up;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider.attachedRigidbody != null) continue;   // nur feste Fahrbahn: eigenes und andere Autos ignorieren
            if (h.distance <= 0f) continue;                     // schon beim Start überlappend
            if (h.distance < best)
            {
                best = h.distance;
                normal = h.normal;
            }
        }

        if (best == float.MaxValue)
        {
            dist = maxLen;
            return false;
        }

        dist = best + r - startOffset;
        return true;
    }

    private void Drive(float dt)
    {
        Vector3 normal = groundNormal;
        Vector3 vel = rb.linearVelocity;

        Vector3 fwd = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, normal).normalized;
        if (fwd.sqrMagnitude < 0.0001f) return;
        float fwdSpeed = Vector3.Dot(vel, fwd);

        // ---- Lenkung über Kurvenradius: Drehrate = Geschwindigkeit / Radius ----
        float radius = TurnRadiusAt(fwdSpeed);
        float targetYaw = (fwdSpeed / radius) * Mathf.Rad2Deg * steerInput;   // rückwärts automatisch invertiert
        targetYaw = Mathf.Clamp(targetYaw, -maxYawRate, maxYawRate);

        // Drehrate weich aufbauen statt sofort umzuspringen
        currentYawRate = Mathf.MoveTowards(currentYawRate, targetYaw, yawAcceleration * dt);

        // Die Physik dämpft die Drehung nach diesem Schritt noch (Angular Damping am Rigidbody): hier vorab ausgleichen
        float damp = Mathf.Max(0.2f, 1f - rb.angularDamping * dt);
        Vector3 av = rb.angularVelocity;
        av -= normal * Vector3.Dot(av, normal);                 // Neigen/Wippen bleibt der Federung
        av += normal * (currentYawRate * Mathf.Deg2Rad / damp);
        rb.angularVelocity = av;

        // ---- Längsbewegung auf der Fahrbahnebene ----
        // fwd liegt in der Fahrbahnebene, daher ist fwdSpeed auch der Vorwärtsanteil von 'planar'
        Vector3 planar = Vector3.ProjectOnPlane(vel, normal);
        Vector3 vertical = vel - planar;

        if (isHandbrake)
        {
            planar = Decelerate(planar, handbrakeDeceleration, dt);
        }
        else if (moveInput * fwdSpeed < -0.3f)   // Taste gegen die Fahrtrichtung: erst bremsen
        {
            planar = Decelerate(planar, brakeDeceleration, dt);
        }
        else if (moveInput > 0.1f)
        {
            float ratio = Mathf.Clamp01(fwdSpeed / topSpeed);
            planar += fwd * (forwardAcceleration * Mathf.Max(0f, smoothedThrottle) * (1f - ratio * ratio) * dt);
        }
        else if (moveInput < -0.1f)
        {
            float ratio = Mathf.Clamp01(-fwdSpeed / reverseTopSpeed);
            planar -= fwd * (backwardAcceleration * Mathf.Max(0f, -smoothedThrottle) * (1f - ratio * ratio) * dt);
        }
        else
        {
            planar = Decelerate(planar, coastDeceleration, dt);
        }

        if (planar.magnitude > topSpeed) planar = planar.normalized * topSpeed;

        // ---- Grip: Fahrtrichtung folgt der Fahrzeugnase ----
        float speed = planar.magnitude;
        if (speed > 0.01f)
        {
            float g = isHandbrake ? handbrakeGrip : grip;
            float along = Vector3.Dot(planar, fwd);
            Vector3 desired = fwd * (along >= 0f ? speed : -speed);
            planar = Vector3.Lerp(planar, desired, 1f - Mathf.Exp(-g * dt));
        }

        rb.linearVelocity = planar + vertical;
    }

    private static Vector3 Decelerate(Vector3 v, float decel, float dt)
    {
        float s = v.magnitude;
        if (s < 0.0001f) return Vector3.zero;
        return v * (Mathf.Max(0f, s - decel * dt) / s);
    }

    // ------------------------------------------------------------------ Reifen

    private void UpdateWheelVisuals()
    {
        float spinRate = ForwardSpeed / Mathf.Max(spinRadius, 0.001f) * Mathf.Rad2Deg;      // Grad pro Sekunde
        spinRate = Mathf.Clamp(spinRate, -maxWheelSpinSpeed, maxWheelSpinSpeed);       // gegen Flimmern begrenzen
        float spinDelta = spinRate * Time.deltaTime;

        frontSpin = Mathf.Repeat(frontSpin + spinDelta, 360f);
        if (!isHandbrake)                                   // Handbremse blockiert die Hinterräder
            rearSpin = Mathf.Repeat(rearSpin + spinDelta, 360f);

        Quaternion steer = Quaternion.AngleAxis(steerInput * maxWheelTurnAngle, Vector3.up);
        Vector3 axis = wheelSpinAxis.normalized;
        Quaternion frontRoll = Quaternion.AngleAxis(frontSpin, axis);
        Quaternion rearRoll = Quaternion.AngleAxis(rearSpin, axis);
        Vector3 up = transform.up;

        foreach (Wheel w in wheels)
        {
            Quaternion rot = w.isFront
                ? steer * w.baseRot * frontRoll
                : w.baseRot * rearRoll;

            // Rad optisch mit der Feder ein- und ausfedern lassen
            float travel = Mathf.Clamp(w.distance - w.radius, -suspensionSag, suspensionSag + suspensionDroop);
            Vector3 worldOffset = -up * (travel - wheelVisualOffset);
            Transform parent = w.visual.parent;
            Vector3 offsetParent = parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;

            // Position so wählen, dass die echte Radmitte fest bleibt, egal wie das Rad gedreht ist
            Vector3 scaledCenter = Vector3.Scale(w.centerLocal, w.visual.localScale);
            w.visual.localPosition = w.centerParent - rot * scaledCenter + offsetParent;
            w.visual.localRotation = rot;
        }
    }
}