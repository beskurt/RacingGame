using UnityEngine;

/// <summary>
/// Motorsound und Crash-Sound für das Auto.
///
/// Benutzung: Auf das Auto legen (dort, wo auch der CarController liegt) und bei 'Engine Loop'
/// einen loopbaren Motorsound zuweisen. Die AudioSource wird automatisch eingerichtet.
/// Die Tonhöhe folgt der Geschwindigkeit, damit es klingt, als würde das Auto beschleunigen.
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarSound : MonoBehaviour
{
    [Header("Motor")]
    [Tooltip("Loopbarer Motorsound (z. B. von freesound.org).")]
    [SerializeField] private AudioClip engineLoop;
    [Tooltip("Tonhöhe im Stand.")]
    [SerializeField, Range(0.1f, 3f)] private float minPitch = 0.6f;
    [Tooltip("Tonhöhe bei Höchstgeschwindigkeit.")]
    [SerializeField, Range(0.1f, 3f)] private float maxPitch = 2f;
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.8f;
    [Tooltip("Wie schnell die Tonhöhe der Geschwindigkeit folgt. Kleiner = weicher.")]
    [SerializeField] private float pitchSmoothing = 5f;
    [Tooltip("0 = 2D (gleich laut, gut für das Spielerauto), 1 = 3D (leiser mit Abstand, gut für Gegner).")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

    [Header("Crash (optional)")]
    [Tooltip("Wird beim Aufprall auf Barrieren/Rails abgespielt. Leer lassen = kein Crash-Sound.")]
    [SerializeField] private AudioClip crashClip;
    [Tooltip("Ab dieser Aufprallgeschwindigkeit (m/s) ertönt der Crash.")]
    [SerializeField] private float minCrashSpeed = 0.8f;
    [Tooltip("Bei dieser Aufprallgeschwindigkeit (m/s) ist der Crash am lautesten.")]
    [SerializeField] private float maxCrashSpeed = 4f;
    [Tooltip("Mindestabstand (Sekunden) zwischen zwei Crash-Sounds, damit es beim Entlangschrammen nicht rattert.")]
    [SerializeField] private float crashCooldown = 0.3f;

    private CarController car;
    private AudioSource engine;
    private AudioSource crash;
    private float lastCrashTime = -10f;

    void Awake()
    {
        car = GetComponent<CarController>();

        engine = gameObject.AddComponent<AudioSource>();
        engine.clip = engineLoop;
        engine.loop = true;
        engine.playOnAwake = false;
        engine.spatialBlend = spatialBlend;
        engine.pitch = minPitch;
        engine.volume = idleVolume;

        crash = gameObject.AddComponent<AudioSource>();
        crash.playOnAwake = false;
        crash.spatialBlend = spatialBlend;
    }

    void Start()
    {
        if (engineLoop != null) engine.Play();
        else Debug.LogWarning("[CarSound] Kein Motorsound zugewiesen.", this);
    }

    void Update()
    {
        // Ziel-Tonhöhe aus der Geschwindigkeit, dann weich dorthin gleiten statt springen
        float speed = car.NormalizedSpeed;
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, speed);
        float targetVolume = Mathf.Lerp(idleVolume, maxVolume, speed);
        float k = 1f - Mathf.Exp(-pitchSmoothing * Time.deltaTime);

        engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, k);
        engine.volume = Mathf.Lerp(engine.volume, targetVolume, k);
    }

    void OnCollisionEnter(Collision c)
    {
        if (crashClip == null || Time.time - lastCrashTime < crashCooldown) return;

        // Nur seitliche Treffer (Wände), nicht das Aufsetzen auf der Fahrbahn
        ContactPoint cp = c.GetContact(0);
        if (Mathf.Abs(cp.normal.y) > 0.5f) return;

        float impact = Vector3.Dot(c.relativeVelocity, cp.normal);
        impact = Mathf.Abs(impact);
        if (impact < minCrashSpeed) return;

        lastCrashTime = Time.time;
        crash.pitch = Random.Range(0.9f, 1.1f);
        crash.PlayOneShot(crashClip, Mathf.InverseLerp(minCrashSpeed, maxCrashSpeed, impact) * 0.7f + 0.3f);
    }
}
