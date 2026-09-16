using System;
using System.Threading.Tasks;
using Ballistics;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyControls
{
    public static async Task<string> Main()
    {
        var session = UnityEngine.Object.FindAnyObjectByType<BallisticSession>();
        var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
        session.ResetRange();
        var angle = root.Q<Slider>("angle");
        var impulse = root.Q<Slider>("impulse");
        var mass = root.Q<DropdownField>("mass");
        angle.value = 45; impulse.value = 15; mass.index = 0;
        Check(session.angle == 45 && session.impulse == 15 && session.mass == 0.5f, "Enlace de controles incorrecto.");
        mass.index = 2; Check(session.mass == 2, "Masa pesada incorrecta.");
        angle.value = 30; impulse.value = 12; mass.index = 1;
        Submit(root.Q<Button>("fire"));
        Check(session.IsRunning && !root.Q<Button>("fire").enabledInHierarchy && !angle.enabledInHierarchy, "Disparo o bloqueo de controles incorrecto.");
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (session.IsRunning)
        {
            Check(DateTime.UtcNow < deadline, "La prueba no terminó.");
            await Task.Delay(20);
        }
        Check(root.Q<Label>("summary").text.Contains(session.LastShot.score.ToString()), "Reporte desactualizado.");
        Submit(root.Q<Button>("reset"));
        Check(session.LastShot == null && root.Q<Button>("fire").enabledInHierarchy && angle.enabledInHierarchy, "Nuevo intento incorrecto.");
        return "Sliders, tres masas, botón de disparo, bloqueo, reporte y botón de reconstrucción: OK.";
    }
    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
