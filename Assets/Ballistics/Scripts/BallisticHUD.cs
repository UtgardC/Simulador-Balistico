using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ballistics
{
    [RequireComponent(typeof(UIDocument))]
    public class BallisticHUD : MonoBehaviour
    {
        private static readonly float[] MassSteps =
        {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f,
            2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f,
            12f, 14f, 16f, 18f, 20f,
            25f, 30f, 35f, 40f, 50f
        };

        private sealed class MarkerButtonBinding
        {
            public ShotRecord shot;
            public int impactIndex;
            public bool allImpacts;
            public Button button;
        }

        public BallisticSession session;
        private Slider angle, horizontalAngle, impulse, velocity, massSlider;
        private FloatField massInput;
        private Toggle preserveVelocity;
        private Button fire, reset;
        private Label liveTelemetry;
        private ScrollView history;
        private VisualElement controls;
        private bool updatingLinkedControls;
        private readonly List<MarkerButtonBinding> markerButtons = new List<MarkerButtonBinding>();

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            angle = root.Q<Slider>("angle");
            horizontalAngle = root.Q<Slider>("horizontal-angle");
            impulse = root.Q<Slider>("impulse");
            velocity = root.Q<Slider>("velocity");
            massSlider = root.Q<Slider>("mass-slider");
            massInput = root.Q<FloatField>("mass-input");
            preserveVelocity = root.Q<Toggle>("preserve-velocity");
            fire = root.Q<Button>("fire");
            reset = root.Q<Button>("reset");
            liveTelemetry = root.Q<Label>("live-telemetry");
            history = root.Q<ScrollView>("history");
            controls = root.Q("controls");

            massSlider.lowValue = 0;
            massSlider.highValue = MassSteps.Length - 1;
            SyncControlsFromSession();

            angle.RegisterValueChangedCallback(OnAngleChanged);
            horizontalAngle.RegisterValueChangedCallback(OnHorizontalAngleChanged);
            impulse.RegisterValueChangedCallback(OnImpulseChanged);
            velocity.RegisterValueChangedCallback(OnVelocityChanged);
            massSlider.RegisterValueChangedCallback(OnMassSliderChanged);
            massInput.RegisterValueChangedCallback(OnMassInputChanged);
            preserveVelocity.RegisterValueChangedCallback(OnPreserveVelocityChanged);
            fire.clicked += session.Fire;
            reset.clicked += session.ResetRange;
            session.Changed += Refresh;
            session.ImpactMarkersChanged += RefreshMarkerButtons;
            Refresh();
        }

        private void OnDisable()
        {
            angle.UnregisterValueChangedCallback(OnAngleChanged);
            horizontalAngle.UnregisterValueChangedCallback(OnHorizontalAngleChanged);
            impulse.UnregisterValueChangedCallback(OnImpulseChanged);
            velocity.UnregisterValueChangedCallback(OnVelocityChanged);
            massSlider.UnregisterValueChangedCallback(OnMassSliderChanged);
            massInput.UnregisterValueChangedCallback(OnMassInputChanged);
            preserveVelocity.UnregisterValueChangedCallback(OnPreserveVelocityChanged);
            session.Changed -= Refresh;
            session.ImpactMarkersChanged -= RefreshMarkerButtons;
            fire.clicked -= session.Fire;
            reset.clicked -= session.ResetRange;
        }

        private void Update()
        {
            float time = session.IsRunning ? session.Elapsed :
                !session.IsRangeReady && session.LastShot != null ? session.LastShot.duration : 0;
            int pieces = session.IsRunning || session.IsRangeReady || session.LastShot == null
                ? session.PiecesDown
                : session.LastShot.piecesDown;
            liveTelemetry.text = $"Tiempo  {time:F1} s     Piezas  {pieces}/{session.TotalPieces}";
        }

        private void OnAngleChanged(ChangeEvent<float> evt) => session.angle = evt.newValue;
        private void OnHorizontalAngleChanged(ChangeEvent<float> evt) => session.horizontalAngle = evt.newValue;

        private void OnImpulseChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            session.impulse = Mathf.Max(0.01f, evt.newValue);
            SyncImpulseAndVelocity();
        }

        private void OnVelocityChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            session.impulse = Mathf.Max(0.01f, evt.newValue) * Mathf.Max(0.1f, session.mass);
            SyncImpulseAndVelocity();
        }

        private void OnMassSliderChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            int index = Mathf.Clamp(Mathf.RoundToInt(evt.newValue), 0, MassSteps.Length - 1);
            ApplyMass(MassSteps[index]);
        }

        private void OnMassInputChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            ApplyMass(Mathf.Clamp(evt.newValue, 0.1f, 50f));
        }

        private void OnPreserveVelocityChanged(ChangeEvent<bool> evt)
        {
            if (updatingLinkedControls) return;
            session.preserveVelocityOnMassChange = evt.newValue;
            UpdatePreserveVelocityText();
        }

        private void ApplyMass(float newMass)
        {
            float oldMass = Mathf.Max(0.1f, session.mass);
            float oldVelocity = session.impulse / oldMass;
            session.mass = Mathf.Clamp(newMass, 0.1f, 50f);
            if (session.preserveVelocityOnMassChange)
                session.impulse = oldVelocity * session.mass;

            SyncMassControls();
            SyncImpulseAndVelocity();
        }

        private void SyncControlsFromSession()
        {
            updatingLinkedControls = true;
            angle.SetValueWithoutNotify(session.angle);
            horizontalAngle.SetValueWithoutNotify(session.horizontalAngle);
            preserveVelocity.SetValueWithoutNotify(session.preserveVelocityOnMassChange);
            updatingLinkedControls = false;
            UpdatePreserveVelocityText();
            SyncMassControls();
            SyncImpulseAndVelocity();
        }

        private void SyncMassControls()
        {
            updatingLinkedControls = true;
            massInput.SetValueWithoutNotify(session.mass);
            massSlider.SetValueWithoutNotify(FindNearestMassStep(session.mass));
            updatingLinkedControls = false;
        }

        private void SyncImpulseAndVelocity()
        {
            float initialVelocity = session.impulse / Mathf.Max(0.1f, session.mass);
            updatingLinkedControls = true;
            ExpandSliderToFit(impulse, session.impulse, 60f);
            ExpandSliderToFit(velocity, initialVelocity, 100f);
            impulse.SetValueWithoutNotify(session.impulse);
            velocity.SetValueWithoutNotify(initialVelocity);
            updatingLinkedControls = false;
        }

        private void UpdatePreserveVelocityText()
        {
            preserveVelocity.text = session.preserveVelocityOnMassChange
                ? "Al cambiar masa: conservar velocidad"
                : "Al cambiar masa: conservar impulso";
        }

        private static void ExpandSliderToFit(Slider slider, float value, float defaultMaximum)
        {
            slider.highValue = Mathf.Max(defaultMaximum, Mathf.Ceil(value * 1.1f / 10f) * 10f);
        }

        private static int FindNearestMassStep(float value)
        {
            int nearest = 0;
            float distance = Mathf.Abs(value - MassSteps[0]);
            for (int i = 1; i < MassSteps.Length; i++)
            {
                float candidateDistance = Mathf.Abs(value - MassSteps[i]);
                if (candidateDistance >= distance) continue;
                distance = candidateDistance;
                nearest = i;
            }
            return nearest;
        }

        private void Refresh()
        {
            controls.SetEnabled(!session.IsRunning && session.IsRangeReady);
            fire.SetEnabled(!session.IsRunning && session.IsRangeReady);
            reset.SetEnabled(true);
            markerButtons.Clear();
            history.Clear();

            foreach (var shot in session.ShotHistory)
            {
                var item = new Foldout { text = $"Tiro {shot.attempt}", value = false };
                item.AddToClassList("shot-item");
                item.Add(new Label($"Duración: {shot.duration:F2} s") { name = "shot-time" });
                item.Add(new Label($"Piezas derribadas: {shot.piecesDown}/{shot.totalPieces}") { name = "shot-pieces" });

                var impactHeader = new VisualElement();
                impactHeader.AddToClassList("impact-header");
                impactHeader.Add(new Label($"Impactos registrados: {shot.impacts.Count}") { name = "impact-count" });
                var showAll = new Button(() => session.ToggleAllImpactMarkers(shot));
                showAll.AddToClassList("impact-marker-button");
                impactHeader.Add(showAll);
                item.Add(impactHeader);
                markerButtons.Add(new MarkerButtonBinding
                {
                    shot = shot,
                    impactIndex = -1,
                    allImpacts = true,
                    button = showAll
                });

                for (int i = 0; i < shot.impacts.Count; i++)
                {
                    int impactIndex = i;
                    var impact = shot.impacts[impactIndex];
                    var impactDetails = new VisualElement();
                    impactDetails.AddToClassList("impact-details");
                    impactDetails.Add(new Label($"Impacto {impactIndex + 1} · {impact.objectName}") { name = "impact-title" });
                    impactDetails.Add(new Label($"Tiempo de vuelo: {impact.flightTime:F2} s"));

                    var pointRow = new VisualElement();
                    pointRow.AddToClassList("impact-point-row");
                    pointRow.Add(new Label($"Punto: {impact.point.ToString("F2")} m"));
                    var showImpact = new Button(() => session.ToggleImpactMarker(shot, impactIndex));
                    showImpact.AddToClassList("impact-marker-button");
                    pointRow.Add(showImpact);
                    impactDetails.Add(pointRow);

                    impactDetails.Add(new Label($"Velocidad relativa: {impact.relativeVelocity.magnitude:F2} m/s"));
                    impactDetails.Add(new Label($"Impulso: {impact.impulse.magnitude:F2} N·s"));
                    item.Add(impactDetails);
                    markerButtons.Add(new MarkerButtonBinding
                    {
                        shot = shot,
                        impactIndex = impactIndex,
                        allImpacts = false,
                        button = showImpact
                    });
                }
                history.Add(item);
            }

            RefreshMarkerButtons();
        }

        private void RefreshMarkerButtons()
        {
            foreach (var binding in markerButtons)
            {
                bool visible = binding.allImpacts
                    ? session.AreAllImpactMarkersVisible(binding.shot)
                    : session.IsImpactMarkerVisible(binding.shot, binding.impactIndex);
                binding.button.text = visible ? "Ocultar" : "Mostrar";
                binding.button.SetEnabled(!binding.allImpacts || binding.shot.impacts.Count > 0);
            }
        }
    }
}
