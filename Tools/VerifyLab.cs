using System;
using System.Threading.Tasks;
using Ballistics;
using UnityEngine;

public static class VerifyLab
{
    public static async Task<string> Main()
    {
        var deadline = DateTime.UtcNow.AddSeconds(50);
        var session = UnityEngine.Object.FindAnyObjectByType<BallisticSession>();
        if (!Application.isPlaying || session == null) throw new Exception("Abrir BallisticLab y entrar a Play.");
        int expectedPieces = session.structureTemplate.GetComponentsInChildren<TargetPiece>(true).Length;
        session.ResetRange();
        float start = Time.time;
        while (Time.time - start < 6) { CheckDeadline(deadline); await Task.Delay(20); }
        Require(session.PiecesDown == 0, "La estructura se cae sin disparar.");
        var pieces = UnityEngine.Object.FindObjectsByType<TargetPiece>();
        Require(pieces.Length == expectedPieces, $"Se esperaban {expectedPieces} piezas.");
        foreach (var piece in pieces) Require(piece.GetComponent<FixedJoint>() != null, "Joint roto en reposo.");
        string result = $"Estabilidad: {expectedPieces} piezas y joints intactos tras 6 segundos.\n";
        float[] impulses = { 7f, 12f, 24f };
        float[] masses = { 1f, 1f, 2f };
        for (int i = 0; i < impulses.Length; i++)
        {
            session.ResetRange();
            await Task.Yield();
            session.angle = 30; session.impulse = impulses[i]; session.mass = masses[i];
            session.Fire();
            while (session.IsRunning) { CheckDeadline(deadline); await Task.Delay(20); }
            var shot = session.LastShot;
            Require(shot != null, "No se registró el tiro.");
            Require(session.ShotHistory.Count >= i + 1 && session.ShotHistory[0] == shot, "Historial incompleto o mal ordenado.");
            result += $"Tiro {i + 1}: {shot.launchImpulseNs} N·s, {shot.massKg} kg, impactos={shot.impacts.Count}, piezas={shot.piecesDown}/{shot.totalPieces}.\n";
        }
        return result;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void CheckDeadline(DateTime deadline)
    {
        if (DateTime.UtcNow > deadline) throw new Exception("La prueba excedió 50 s; comprobar que Play no esté pausado.");
    }
}
