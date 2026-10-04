using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Unsichtbarer Trigger auf der Strecke. Meldet dem RaceManager, wenn ein Auto durchfährt.
///
/// Index 0 ist die Start/Ziel-Linie, danach 1, 2, 3 ... in Fahrtrichtung.
/// Die blaue Z-Achse (Pfeil in der Scene-Ansicht) muss in Fahrtrichtung zeigen,
/// denn mit dieser Drehung wird das Auto beim Zurücksetzen (R) hingestellt.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Reihenfolge auf der Strecke. 0 = Start/Ziel.")]
    [SerializeField, Min(0)] private int index;

    public int Index => index;

    private RaceManager manager;

    /// <summary>Wird vom RaceManager beim Start aufgerufen.</summary>
    public void Init(RaceManager raceManager)
    {
        manager = raceManager;
    }

    void Reset()
    {
        // Wie der Respawn-Trigger: Collider als Trigger, Renderer aus
        GetComponent<Collider>().isTrigger = true;
        Renderer r = GetComponent<Renderer>();
        if (r != null) r.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (manager == null) return;

        // Nur Autos zählen, keine Barrieren, Deko usw.
        Rigidbody body = other.attachedRigidbody;
        CarController car = body != null ? body.GetComponent<CarController>() : null;
        if (car != null) manager.OnCheckpointPassed(this, car);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        Color c = index == 0 ? new Color(1f, 0.85f, 0f) : new Color(0f, 0.8f, 1f);

        if (box != null)
        {
            Gizmos.color = new Color(c.r, c.g, c.b, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = c;
            Gizmos.DrawWireCube(box.center, box.size);
        }

        // Pfeil in Fahrtrichtung
        Gizmos.matrix = Matrix4x4.identity;
        Vector3 p = transform.position;
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 0.4f;
        Gizmos.DrawLine(p, p + f);
        Gizmos.DrawLine(p + f, p + f * 0.7f + Vector3.Cross(Vector3.up, f) * 0.3f);
        Gizmos.DrawLine(p + f, p + f * 0.7f - Vector3.Cross(Vector3.up, f) * 0.3f);

        Handles.Label(p + Vector3.up * 0.3f, index == 0 ? "Start/Ziel (0)" : "Checkpoint " + index);
    }
#endif
}
