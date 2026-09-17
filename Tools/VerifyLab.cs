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
        session.ResetRange();
        float start = Time.time;
        while (Time.time - start < 6) { CheckDeadline(deadline); await Task.Delay(20); }
        Require(session.PiecesDown == 0, "La estructura se cae sin disparar.");
        var pieces = UnityEngine.Object.FindObjectsByType<TargetPiece>();
        Require(pieces.Length == 9, "Se esperaban 9 piezas.");
        foreach (var piece in pieces) Require(piece.GetComponent<FixedJoint>() != null, "Joint roto en reposo.");
        string result = "Estabilidad: 9 piezas y 9 joints intactos tras 6 segundos.\n";
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
            Require(shot != null && shot.impacts.Count > 0, "No se registraron impactos.");
            Require(shot.impacts[0].flightTime > 0 && shot.impacts[0].impulse.magnitude > 0, "Telemetría vacía.");
            Require(session.ShotHistory.Count >= i + 1 && session.ShotHistory[0] == shot, "Historial incompleto o mal ordenado.");
            Require(i == 0 ? !shot.impacts[0].target && shot.piecesDown == 0 : shot.hitTarget && shot.piecesDown > 0, "Resultado físico inesperado.");
            result += $"Tiro {i + 1}: {shot.launchImpulseNs} N·s, {shot.massKg} kg, objetivo={shot.hitTarget}, piezas={shot.piecesDown}, vuelo={shot.impacts[0].flightTime:F2}s.\n";
        }
        return result;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void CheckDeadline(DateTime deadline)
    {
        if (DateTime.UtcNow > deadline) throw new Exception("La prueba excedió 50 s; comprobar que Play no esté pausado.");
    }
}
