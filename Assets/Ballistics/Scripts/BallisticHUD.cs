using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ballistics
{
    [RequireComponent(typeof(UIDocument))]
    public class BallisticHUD : MonoBehaviour
    {
        public BallisticSession session;
        private Slider angle, impulse;
        private DropdownField mass;
        private Button fire, reset;
        private Label status, summary, report, storage, launch;
        private VisualElement controls;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            angle = root.Q<Slider>("angle");
            impulse = root.Q<Slider>("impulse");
            mass = root.Q<DropdownField>("mass");
            fire = root.Q<Button>("fire"); reset = root.Q<Button>("reset");
            status = root.Q<Label>("status"); summary = root.Q<Label>("summary");
            report = root.Q<Label>("report"); storage = root.Q<Label>("storage");
            launch = root.Q<Label>("launch"); controls = root.Q("controls");
            angle.value = session.angle; impulse.value = session.impulse;
            mass.choices = new List<string> { "0,5 kg", "1 kg", "2 kg" };
            mass.index = session.mass < 0.75f ? 0 : session.mass > 1.5f ? 2 : 1;
            angle.RegisterValueChangedCallback(e => session.angle = e.newValue);
            impulse.RegisterValueChangedCallback(e => session.impulse = e.newValue);
            mass.RegisterValueChangedCallback(e => session.mass = mass.index == 0 ? 0.5f : mass.index == 1 ? 1 : 2);
            fire.clicked += session.Fire; reset.clicked += session.ResetRange;
            session.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            fire.clicked -= session.Fire; reset.clicked -= session.ResetRange;
        }

        private void Update()
        {
            launch.text = $"{session.angle:F0}°   /   {session.impulse:F1} N·s   /   v₀ {session.impulse / session.mass:F1} m/s";
            if (session.IsRunning) status.text = $"OBSERVANDO   {session.Elapsed:F1} s   ·   {session.PiecesDown}/9 piezas";
        }

        private void Refresh()
        {
            bool complete = session.LastShot != null;
            controls.SetEnabled(!session.IsRunning && !complete);
            fire.SetEnabled(!session.IsRunning && !complete);
            reset.SetEnabled(!session.IsRunning);
            storage.text = session.SaveStatus;
            if (!complete)
            {
                status.text = session.IsRunning ? "DISPARO EN CURSO" : "CAMPO LISTO";
                summary.text = "Ajustá. Dispará. Compará.";
                report.text = "Derribá los 9 bloques de la estructura.\n100 puntos por pieza + 50 por acertar.\n\nLa línea muestra la trayectoria ideal\nsin colisiones ni rebotes.";
                return;
            }
            var shot = session.LastShot;
            status.text = $"INTENTO {shot.attempt:00} COMPLETADO";
            summary.text = $"{shot.score} PTS   /   {shot.piecesDown} de 9";
            string details = $"{(shot.hitTarget ? "Objetivo alcanzado" : "Sin impacto directo en objetivos")}\n{shot.endReason}\nObservación: {shot.duration:F2} s";
            if (shot.impacts.Count > 0)
            {
                var hit = shot.impacts[0];
                details += $"\n\nPRIMER IMPACTO · {hit.objectName}\nVuelo: {hit.flightTime:F2} s\nPunto: {hit.point.ToString("F2")} m\nVel. relativa: {hit.relativeVelocity.magnitude:F2} m/s\nImpulso: {hit.impulse.magnitude:F2} N·s\nContactos registrados: {shot.impacts.Count}";
            }
            else details += "\n\nSin colisiones registradas.";
            report.text = details;
        }
    }
}
