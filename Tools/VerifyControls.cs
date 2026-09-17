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
        int initialHistoryCount = session.ShotHistory.Count;
        var angle = root.Q<Slider>("angle");
        var horizontalAngle = root.Q<Slider>("horizontal-angle");
        var impulse = root.Q<Slider>("impulse");
        var mass = root.Q<DropdownField>("mass");
        angle.value = 45; horizontalAngle.value = 20; impulse.value = 15; mass.index = 0;
        Check(session.angle == 45 && session.horizontalAngle == 20 && session.impulse == 15 && session.mass == 0.5f, "Enlace de controles incorrecto.");
        mass.index = 2; Check(session.mass == 2, "Masa pesada incorrecta.");
        angle.value = 30; horizontalAngle.value = 0; impulse.value = 12; mass.index = 1;
        Submit(root.Q<Button>("fire"));
        Check(session.IsRunning && !root.Q<Button>("fire").enabledInHierarchy && !angle.enabledInHierarchy, "Disparo o bloqueo de controles incorrecto.");
        Check(root.Q<Button>("reset").enabledInHierarchy, "Nuevo intento debe estar habilitado durante el vuelo.");
        await Task.Delay(250);
        Submit(root.Q<Button>("reset"));
        Check(!session.IsRunning && session.IsRangeReady && session.ShotHistory.Count == initialHistoryCount + 1, "Interrupción o reconstrucción incorrecta.");
        Check(session.ShotHistory[0].endReason == "Interrumpido", "El tiro interrumpido no se registró.");
        Check(root.Q<ScrollView>("history").contentContainer.childCount == initialHistoryCount + 1, "El historial no se actualizó.");
        Submit(root.Q<Button>("fire"));
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (session.IsRunning)
        {
            Check(DateTime.UtcNow < deadline, "La prueba no terminó.");
            await Task.Delay(20);
        }
        Check(session.ShotHistory.Count == initialHistoryCount + 2 && session.ShotHistory[0].attempt > session.ShotHistory[1].attempt, "El historial no está ordenado por más reciente.");
        Check(root.Q<ScrollView>("history").contentContainer.childCount == initialHistoryCount + 2, "La lista visual no contiene ambos tiros.");
        var newest = root.Q<ScrollView>("history").contentContainer[0] as Foldout;
        Check(newest != null && !newest.value, "El tiro debe comenzar plegado.");
        newest.value = true; Check(newest.value, "El tiro no se pudo desplegar.");
        newest.value = false; Check(!newest.value, "El tiro no se pudo volver a plegar.");
        Submit(root.Q<Button>("reset"));
        Check(session.ShotHistory.Count == initialHistoryCount + 2 && root.Q<Button>("fire").enabledInHierarchy && angle.enabledInHierarchy, "Nuevo intento o persistencia del historial incorrectos.");
        return "Controles, reinicio durante el vuelo e historial plegable más reciente primero: OK.";
    }
    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
