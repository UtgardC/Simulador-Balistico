using System;
using System.IO;
using UnityEngine;

namespace Ballistics
{
    public class BallisticSession : MonoBehaviour
    {
        public Projectile projectilePrefab;
        public GameObject structurePrefab;
        public Transform barrelPivot;
        public Transform muzzle;
        public LineRenderer preview;
        public Vector3 targetPosition = new Vector3(12, 0, 0);
        [Range(5, 75)] public float angle = 30;
        [Range(5, 60)] public float impulse = 12;
        public float mass = 1;

        public bool IsRunning { get; private set; }
        public ShotRecord LastShot { get; private set; }
        public string SaveStatus { get; private set; } = "Los resultados se guardan al terminar cada tiro.";
        public string RecordsDirectory => Path.Combine(Application.persistentDataPath, "Shots");
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
        private int attempt;
        private float startedAt, firstImpactAt, quietTime;
        private bool pendingLaunch;
        private readonly Vector3[] previewPoints = new Vector3[70];

        private void Start() { Application.runInBackground = true; ResetRange(); }

        private void Update()
        {
            if (IsRunning) return;
            barrelPivot.rotation = Quaternion.Euler(0, 0, angle);
            Vector3 velocity = barrelPivot.right * (impulse / mass);
            int count = 0;
            for (int i = 0; i < previewPoints.Length; i++)
            {
                float t = i * 0.055f;
                Vector3 point = muzzle.position + velocity * t + Physics.gravity * (0.5f * t * t);
                previewPoints[count++] = point;
                if (point.y < 0.22f || point.x > 32) break;
            }
            preview.positionCount = count;
            for (int i = 0; i < count; i++) preview.SetPosition(i, previewPoints[i]);
        }

        public void Fire()
        {
            if (IsRunning || LastShot != null) return;
            IsRunning = true;
            pendingLaunch = true;
            preview.enabled = false;
            Changed?.Invoke();
        }

        private void FixedUpdate()
        {
            if (!IsRunning) return;
            if (pendingLaunch)
            {
                pendingLaunch = false;
                startedAt = Time.fixedTime;
                firstImpactAt = -1;
                quietTime = 0;
                current = new ShotRecord { attempt = ++attempt, timestampUtc = DateTime.UtcNow.ToString("O"),
                    angleDegrees = angle, launchImpulseNs = impulse, massKg = mass };
                barrelPivot.rotation = Quaternion.Euler(0, 0, angle);
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
            if (firstImpactAt >= 0 && Time.fixedTime - firstImpactAt >= 3 && quietTime >= 0.8f) Finish("En reposo");
            else if (Elapsed >= 12) Finish("Tiempo máximo de observación (12 s)");
            else if (projectile.transform.position.y < -10 || projectile.transform.position.x > 65)
            {
                // Allow time for the structure to finish falling even if the projectile leaves the range.
                if (firstImpactAt < 0) firstImpactAt = Time.fixedTime;
                if (Time.fixedTime - firstImpactAt >= 5) Finish("Fuera del campo");
            }
        }

        public void RegisterImpact(ImpactRecord impact)
        {
            if (!IsRunning || current == null) return;
            if (firstImpactAt < 0) firstImpactAt = Time.fixedTime;
            current.impacts.Add(impact);
            current.hitTarget |= impact.target;
        }

        private void Finish(string reason)
        {
            current.duration = Elapsed;
            current.endReason = reason;
            current.piecesDown = PiecesDown;
            current.score = current.piecesDown * 100 + (current.hitTarget ? 50 : 0);
            LastShot = current;
            IsRunning = false;
            // Freeze only after measuring, so the displayed report describes the final visible state.
            projectileBody.isKinematic = true;
            foreach (var piece in pieces) piece.GetComponent<Rigidbody>().isKinematic = true;
            try
            {
                Directory.CreateDirectory(RecordsDirectory);
                string filename = $"shot-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{current.attempt}-{Guid.NewGuid():N}.json";
                File.WriteAllText(Path.Combine(RecordsDirectory, filename), JsonUtility.ToJson(current, true));
                SaveStatus = "JSON guardado en: " + RecordsDirectory;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                SaveStatus = "No se pudo guardar el JSON: " + exception.Message;
                Debug.LogWarning(SaveStatus);
            }
            Changed?.Invoke();
        }

        public void ResetRange()
        {
            if (IsRunning) return;
            if (structure != null) { structure.SetActive(false); Destroy(structure); }
            if (projectile != null) { projectile.gameObject.SetActive(false); Destroy(projectile.gameObject); }
            structure = Instantiate(structurePrefab, targetPosition, Quaternion.identity);
            pieces = structure.GetComponentsInChildren<TargetPiece>();
            LastShot = null;
            current = null;
            preview.enabled = true;
            Changed?.Invoke();
        }
    }
}
