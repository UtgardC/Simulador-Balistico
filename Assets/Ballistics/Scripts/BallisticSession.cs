using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ballistics
{
    public class BallisticSession : MonoBehaviour
    {
        public Projectile projectilePrefab;
        [FormerlySerializedAs("structurePrefab")]
        public GameObject structureTemplate;
        public Transform barrelPivot;
        public Transform muzzle;
        public LineRenderer preview;
        [Range(5, 75)] public float angle = 30;
        [Range(-45, 45)] public float horizontalAngle;
        [Range(5, 60)] public float impulse = 12;
        public float mass = 1;

        public bool IsRunning { get; private set; }
        public bool IsRangeReady { get; private set; }
        public ShotRecord LastShot { get; private set; }
        public IReadOnlyList<ShotRecord> ShotHistory => shotHistory;
        public float Elapsed => IsRunning ? Time.fixedTime - startedAt : 0;
        public int PiecesDown
        {
            get
            {
                int count = 0;
                if (pieces != null) foreach (var piece in pieces) if (piece != null && piece.IsDown) count++;
                return count;
            }
        }
        public event Action Changed;
        private GameObject structure;
        private TargetPiece[] pieces;
        private Projectile projectile;
        private Rigidbody projectileBody;
        private ShotRecord current;
        private readonly List<ShotRecord> shotHistory = new List<ShotRecord>();
        private int attempt;
        private float startedAt, firstImpactAt, quietTime;
        private bool pendingLaunch;
        private readonly Vector3[] previewPoints = new Vector3[70];
        private Vector3 fallbackTemplatePosition = new Vector3(12, 0, 0);
        private Quaternion fallbackTemplateRotation = Quaternion.identity;

        private Quaternion LaunchRotation => Quaternion.Euler(0, -horizontalAngle, angle);

        private void Start()
        {
            Application.runInBackground = true;
            preview.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            preview.receiveShadows = false;
            if (structureTemplate != null && structureTemplate.scene.IsValid())
            {
                fallbackTemplatePosition = structureTemplate.transform.position;
                fallbackTemplateRotation = structureTemplate.transform.rotation;
                structureTemplate.SetActive(false);
            }
            ResetRange();
        }

        private void Update()
        {
            if (IsRunning) return;
            barrelPivot.rotation = LaunchRotation;
            Vector3 velocity = barrelPivot.right * (impulse / mass);
            int count = 0;
            for (int i = 0; i < previewPoints.Length; i++)
            {
                float t = i * 0.055f;
                Vector3 point = muzzle.position + velocity * t + Physics.gravity * (0.5f * t * t);
                previewPoints[count++] = point;
                if (point.y < 0.22f || (point - muzzle.position).sqrMagnitude > 32 * 32) break;
            }
            preview.positionCount = count;
            for (int i = 0; i < count; i++) preview.SetPosition(i, previewPoints[i]);
        }

        public void Fire()
        {
            if (IsRunning || !IsRangeReady) return;
            IsRunning = true;
            IsRangeReady = false;
            pendingLaunch = true;
            startedAt = Time.fixedTime;
            firstImpactAt = -1;
            quietTime = 0;
            current = new ShotRecord { attempt = ++attempt, timestampUtc = DateTime.UtcNow.ToString("O"),
                angleDegrees = angle, horizontalAngleDegrees = horizontalAngle,
                launchImpulseNs = impulse, massKg = mass };
            preview.enabled = false;
            Changed?.Invoke();
        }

        private void FixedUpdate()
        {
            if (!IsRunning) return;
            if (pendingLaunch)
            {
                pendingLaunch = false;
                barrelPivot.rotation = LaunchRotation;
                projectile = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
                projectileBody = projectile.GetComponent<Rigidbody>();
                projectile.Launch(this, barrelPivot.right, impulse, mass);
                return;
            }
            bool quiet = projectileBody.IsSleeping() || (projectileBody.linearVelocity.sqrMagnitude < 0.04f && projectileBody.angularVelocity.sqrMagnitude < 0.04f);
            foreach (var piece in pieces)
            {
                var body = piece.GetComponent<Rigidbody>();
                quiet &= body.IsSleeping() || (body.linearVelocity.sqrMagnitude < 0.04f && body.angularVelocity.sqrMagnitude < 0.04f);
            }
            quietTime = quiet ? quietTime + Time.fixedDeltaTime : 0;
            if (firstImpactAt >= 0 && Time.fixedTime - firstImpactAt >= 3 && quietTime >= 0.8f) CompleteShot("En reposo", true);
            else if (Elapsed >= 12) CompleteShot("Tiempo máximo de observación (12 s)", true);
            else if (projectile.transform.position.y < -10 ||
                     (projectile.transform.position - muzzle.position).sqrMagnitude > 65 * 65)
            {
                // Allow time for the structure to finish falling even if the projectile leaves the range.
                if (firstImpactAt < 0) firstImpactAt = Time.fixedTime;
                if (Time.fixedTime - firstImpactAt >= 5) CompleteShot("Fuera del campo", true);
            }
        }

        public void RegisterImpact(ImpactRecord impact)
        {
            if (!IsRunning || current == null) return;
            if (firstImpactAt < 0) firstImpactAt = Time.fixedTime;
            current.impacts.Add(impact);
            current.hitTarget |= impact.target;
        }

        private void CompleteShot(string reason, bool freezeBodies)
        {
            if (current == null) return;
            current.duration = Elapsed;
            current.endReason = reason;
            current.piecesDown = PiecesDown;
            LastShot = current;
            shotHistory.Insert(0, current);
            IsRunning = false;
            pendingLaunch = false;
            if (freezeBodies)
            {
                if (projectileBody != null) projectileBody.isKinematic = true;
                foreach (var piece in pieces)
                    if (piece != null) piece.GetComponent<Rigidbody>().isKinematic = true;
            }
            current = null;
            Changed?.Invoke();
        }

        public void ResetRange()
        {
            if (IsRunning) CompleteShot("Interrumpido", false);
            if (structure != null) { structure.SetActive(false); Destroy(structure); }
            if (projectile != null) { projectile.gameObject.SetActive(false); Destroy(projectile.gameObject); }
            if (structureTemplate == null)
            {
                Debug.LogError("Falta asignar la plantilla de objetivos en BallisticSession.");
                return;
            }
            bool sceneTemplate = structureTemplate.scene.IsValid();
            Vector3 position = sceneTemplate ? structureTemplate.transform.position : fallbackTemplatePosition;
            Quaternion rotation = sceneTemplate ? structureTemplate.transform.rotation : fallbackTemplateRotation;
            structure = Instantiate(structureTemplate, position, rotation);
            structure.name = "Objetivos - intento actual";
            structure.SetActive(true);
            pieces = structure.GetComponentsInChildren<TargetPiece>();
            current = null;
            projectile = null;
            projectileBody = null;
            IsRangeReady = true;
            preview.enabled = true;
            Changed?.Invoke();
        }
    }
}
