using System;
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
        public Transform cameraRotatingPivot;
        private Slider angle, horizontalAngle, impulse, velocity, massSlider;
        private Slider cameraAngle;
        private FloatField massInput;
        private Toggle preserveVelocity;
        private Button fire, reset;
        private Button savedShotsButton, localShotsButton;
        private Label liveTelemetry;
        private Label cloudStatus;
        private ScrollView history;
        private ScrollView savedHistory;
        private VisualElement controls;
        private ShotPersistence persistence;
        private bool viewingSavedShots;
        private bool loadingSavedShots;
        private bool updatingLinkedControls;
        private bool cameraAngleInitialized;
        private float linkedVelocity;
        private readonly List<MarkerButtonBinding> markerButtons = new List<MarkerButtonBinding>();

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            angle = root.Q<Slider>("angle");
            horizontalAngle = root.Q<Slider>("horizontal-angle");
            impulse = root.Q<Slider>("impulse");
            velocity = root.Q<Slider>("velocity");
            massSlider = root.Q<Slider>("mass-slider");
            cameraAngle = root.Q<Slider>("camera-angle");
            massInput = root.Q<FloatField>("mass-input");
            preserveVelocity = root.Q<Toggle>("preserve-velocity");
            fire = root.Q<Button>("fire");
            reset = root.Q<Button>("reset");
            savedShotsButton = root.Q<Button>("saved-shots");
            localShotsButton = root.Q<Button>("local-shots");
            liveTelemetry = root.Q<Label>("live-telemetry");
            cloudStatus = root.Q<Label>("cloud-status");
            history = root.Q<ScrollView>("history");
            savedHistory = root.Q<ScrollView>("saved-history");
            controls = root.Q("controls");
            persistence = session.GetComponent<ShotPersistence>();
            if (persistence == null)
                persistence = session.gameObject.AddComponent<ShotPersistence>();
            savedHistory.style.display = DisplayStyle.None;
            UpdateResultsTabs();
            SetCloudStatus(persistence.Status);

            EnsureCameraPivot();
            if (cameraRotatingPivot != null)
            {
                if (!cameraAngleInitialized)
                {
                    SetCameraAngle(67f);
                    cameraAngleInitialized = true;
                }
                cameraAngle.SetValueWithoutNotify(cameraRotatingPivot.localEulerAngles.y);
            }
            else
                cameraAngle.SetEnabled(false);

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
            cameraAngle.RegisterValueChangedCallback(OnCameraAngleChanged);
            fire.clicked += session.Fire;
            reset.clicked += session.ResetRange;
            savedShotsButton.clicked += ShowSavedShots;
            localShotsButton.clicked += ShowLocalShots;
            session.Changed += Refresh;
            session.ImpactMarkersChanged += RefreshMarkerButtons;
            persistence.StatusChanged += SetCloudStatus;
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
            cameraAngle.UnregisterValueChangedCallback(OnCameraAngleChanged);
            session.Changed -= Refresh;
            session.ImpactMarkersChanged -= RefreshMarkerButtons;
            persistence.StatusChanged -= SetCloudStatus;
            fire.clicked -= session.Fire;
            reset.clicked -= session.ResetRange;
            savedShotsButton.clicked -= ShowSavedShots;
            localShotsButton.clicked -= ShowLocalShots;
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
        private void OnCameraAngleChanged(ChangeEvent<float> evt) => SetCameraAngle(evt.newValue);

        private void EnsureCameraPivot()
        {
            if (cameraRotatingPivot != null) return;
            var existingPivot = GameObject.Find("CameraRotatingPivot");
            if (existingPivot != null)
            {
                cameraRotatingPivot = existingPivot.transform;
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("No se encontró Main Camera para el control de ángulo.");
                return;
            }

            cameraRotatingPivot = new GameObject("CameraRotatingPivot").transform;
            cameraRotatingPivot.position = Vector3.zero;
            cameraRotatingPivot.rotation = Quaternion.Euler(0f, 67f, 0f);
            mainCamera.transform.SetParent(cameraRotatingPivot, true);
        }

        private void SetCameraAngle(float degrees)
        {
            if (cameraRotatingPivot == null) return;
            var rotation = cameraRotatingPivot.localEulerAngles;
            rotation.y = Mathf.Clamp(degrees, 0f, 180f);
            cameraRotatingPivot.localEulerAngles = rotation;
        }

        private void OnImpulseChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            session.impulse = Mathf.Max(0f, evt.newValue);
            linkedVelocity = session.impulse / Mathf.Max(0.1f, session.mass);
            SyncImpulseAndVelocity();
        }

        private void OnVelocityChanged(ChangeEvent<float> evt)
        {
            if (updatingLinkedControls) return;
            linkedVelocity = Mathf.Max(0f, evt.newValue);
            session.impulse = linkedVelocity * Mathf.Max(0.1f, session.mass);
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
            linkedVelocity = session.impulse / Mathf.Max(0.1f, session.mass);
            UpdatePreserveVelocityText();
        }

        private void ApplyMass(float newMass)
        {
            session.mass = Mathf.Clamp(newMass, 0.1f, 50f);
            if (session.preserveVelocityOnMassChange)
                session.impulse = linkedVelocity * session.mass;
            else
                linkedVelocity = session.impulse / session.mass;

            SyncMassControls();
            SyncImpulseAndVelocity();
        }

        private void SyncControlsFromSession()
        {
            linkedVelocity = session.impulse / Mathf.Max(0.1f, session.mass);
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
            updatingLinkedControls = true;
            ExpandSliderToFit(impulse, session.impulse, 60f);
            ExpandSliderToFit(velocity, linkedVelocity, 100f);
            impulse.SetValueWithoutNotify(session.impulse);
            velocity.SetValueWithoutNotify(linkedVelocity);
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

        private void ShowLocalShots()
        {
            viewingSavedShots = false;
            UpdateResultsTabs();
        }

        private void UpdateResultsTabs()
        {
            history.style.display = viewingSavedShots ? DisplayStyle.None : DisplayStyle.Flex;
            savedHistory.style.display = viewingSavedShots ? DisplayStyle.Flex : DisplayStyle.None;
            savedShotsButton.EnableInClassList("results-tab--active", viewingSavedShots);
            localShotsButton.EnableInClassList("results-tab--active", !viewingSavedShots);
        }

        private async void ShowSavedShots()
        {
            viewingSavedShots = true;
            UpdateResultsTabs();
            if (loadingSavedShots) return;

            loadingSavedShots = true;
            savedHistory.Clear();
            SetCloudStatus("Cargando resultados de UGS...");
            try
            {
                var shots = await persistence.LoadSavedShotsAsync();
                foreach (var shot in shots)
                {
                    var item = new Foldout { text = SavedShotTitle(shot), value = false };
                    item.AddToClassList("shot-item");
                    item.Add(new Label($"Acierto: {(shot.hitTarget ? "Sí" : "No")}"));
                    item.Add(new Label($"Elevación: {shot.angleDegrees:F1}° · Horizontal: {shot.horizontalAngleDegrees:F1}°"));
                    item.Add(new Label($"Impulso: {shot.impulseNs:F2} N·s · Masa: {shot.massKg:F2} kg"));
                    item.Add(new Label($"Distancia horizontal: {shot.distanceMeters:F2} m"));
                    item.Add(new Label($"Piezas derribadas: {shot.piecesDown}"));
                    item.Add(new Label($"Cierre: {shot.endReason}"));
                    savedHistory.Add(item);
                }
                SetCloudStatus(shots.Count == 0
                    ? "Todavía no hay tiros guardados en UGS."
                    : $"{shots.Count} tiro(s) recuperado(s) de UGS.");
            }
            catch (Exception exception)
            {
                SetCloudStatus("No se pudieron cargar los resultados. Revisá Services y la conexión.");
                Debug.LogException(exception);
            }
            finally
            {
                loadingSavedShots = false;
            }
        }

        private static string SavedShotTitle(SavedShot shot)
        {
            if (DateTime.TryParse(shot.timestampUtc, out var date))
                return $"{date.ToLocalTime():dd/MM HH:mm} · {(shot.hitTarget ? "Acierto" : "Fallo")}";
            return shot.hitTarget ? "Tiro guardado · Acierto" : "Tiro guardado · Fallo";
        }

        private void SetCloudStatus(string message)
        {
            cloudStatus.text = message;
            cloudStatus.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
