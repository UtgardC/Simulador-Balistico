using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ballistics
{
    [RequireComponent(typeof(UIDocument))]
    public class BallisticHUD : MonoBehaviour
    {
        public BallisticSession session;
        private Slider angle, horizontalAngle, impulse;
        private DropdownField mass;
        private Button fire, reset;
        private Label liveTelemetry;
        private ScrollView history;
        private VisualElement controls;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            angle = root.Q<Slider>("angle");
            horizontalAngle = root.Q<Slider>("horizontal-angle");
            impulse = root.Q<Slider>("impulse");
            mass = root.Q<DropdownField>("mass");
            fire = root.Q<Button>("fire"); reset = root.Q<Button>("reset");
            liveTelemetry = root.Q<Label>("live-telemetry");
            history = root.Q<ScrollView>("history");
            controls = root.Q("controls");
            angle.value = session.angle; horizontalAngle.value = session.horizontalAngle; impulse.value = session.impulse;
            mass.choices = new List<string> { "0,5 kg", "1 kg", "2 kg" };
            mass.index = session.mass < 0.75f ? 0 : session.mass > 1.5f ? 2 : 1;
            angle.RegisterValueChangedCallback(e => session.angle = e.newValue);
            horizontalAngle.RegisterValueChangedCallback(e => session.horizontalAngle = e.newValue);
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
            float time = session.IsRunning ? session.Elapsed :
                !session.IsRangeReady && session.LastShot != null ? session.LastShot.duration : 0;
            int pieces = session.IsRunning || session.IsRangeReady ? session.PiecesDown : session.LastShot.piecesDown;
            liveTelemetry.text = $"Tiempo  {time:F1} s     Piezas  {pieces}/{session.TotalPieces}";
        }

        private void Refresh()
        {
            controls.SetEnabled(!session.IsRunning && session.IsRangeReady);
            fire.SetEnabled(!session.IsRunning && session.IsRangeReady);
            reset.SetEnabled(true);
            history.Clear();
            foreach (var shot in session.ShotHistory)
            {
                var item = new Foldout { text = $"Tiro {shot.attempt}", value = false };
                item.AddToClassList("shot-item");
                item.Add(new Label($"Duración: {shot.duration:F2} s") { name = "shot-time" });
                item.Add(new Label($"Piezas derribadas: {shot.piecesDown}/{shot.totalPieces}") { name = "shot-pieces" });
                item.Add(new Label($"Impactos registrados: {shot.impacts.Count}") { name = "impact-count" });
                for (int i = 0; i < shot.impacts.Count; i++)
                {
                    var impact = shot.impacts[i];
                    var impactDetails = new VisualElement();
                    impactDetails.AddToClassList("impact-details");
                    impactDetails.Add(new Label($"Impacto {i + 1} · {impact.objectName}") { name = "impact-title" });
                    impactDetails.Add(new Label($"Tiempo de vuelo: {impact.flightTime:F2} s"));
                    impactDetails.Add(new Label($"Punto: {impact.point.ToString("F2")} m"));
                    impactDetails.Add(new Label($"Velocidad relativa: {impact.relativeVelocity.magnitude:F2} m/s"));
                    impactDetails.Add(new Label($"Impulso: {impact.impulse.magnitude:F2} N·s"));
                    item.Add(impactDetails);
                }
                history.Add(item);
            }
        }
    }
}
