using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Eine aufgezeichnete Ideallinie: Punkte entlang einer Runde und das Tempo an jedem Punkt.
/// Wird vom RacingLineRecorder (Spielerauto) gefüllt und vom AIDriver abgefahren.
/// Die Linie ist geschlossen: nach dem letzten Punkt geht es beim ersten weiter.
/// </summary>
[CreateAssetMenu(menuName = "Racing/Racing Line", fileName = "RacingLine")]
public class RacingLine : ScriptableObject
{
    public const string DefaultPath = "Assets/RacingLine.asset";

    public List<Vector3> points = new List<Vector3>();
    public List<float> speeds = new List<float>();
    [Tooltip("Rundenzeit der aufgezeichneten Runde (Sekunden), -1 = noch leer.")]
    public float lapTime = -1f;

    public int Count => points.Count;
    public int Wrap(int i) { return ((i % Count) + Count) % Count; }
    public Vector3 Point(int i) { return points[Wrap(i)]; }
    public float Speed(int i) { return speeds[Wrap(i)]; }

#if UNITY_EDITOR
    /// <summary>Lädt Assets/RacingLine.asset oder legt es an (nur im Editor).</summary>
    public static RacingLine LoadOrCreateDefault()
    {
        RacingLine line = AssetDatabase.LoadAssetAtPath<RacingLine>(DefaultPath);
        if (line != null) return line;
        line = CreateInstance<RacingLine>();
        AssetDatabase.CreateAsset(line, DefaultPath);
        AssetDatabase.SaveAssets();
        return line;
    }
#endif
}
