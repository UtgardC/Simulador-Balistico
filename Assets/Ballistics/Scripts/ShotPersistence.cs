using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ballistics
{
    [RequireComponent(typeof(BallisticSession))]
    public class ShotPersistence : MonoBehaviour
    {
        private readonly IShotRepository repository = new UgsShotRepository();
        private readonly List<SavedShot> pendingShots = new List<SavedShot>();
        private BallisticSession session;
        private Task currentSave;

        public string Status { get; private set; } = "";
        public event Action<string> StatusChanged;

        private void OnEnable()
        {
            session = GetComponent<BallisticSession>();
            session.ShotCompleted += OnShotCompleted;
        }

        private void OnDisable()
        {
            if (session != null)
                session.ShotCompleted -= OnShotCompleted;
        }

        private async void OnShotCompleted(ShotRecord shot)
        {
            pendingShots.Add(SavedShot.FromShot(shot));
            await SavePendingAsync();
        }

        public async Task<List<SavedShot>> LoadSavedShotsAsync()
        {
            await SavePendingAsync();
            return await repository.LoadAllAsync();
        }

        private Task SavePendingAsync()
        {
            if (currentSave == null || currentSave.IsCompleted)
                currentSave = SavePendingCoreAsync();
            return currentSave;
        }

        private async Task SavePendingCoreAsync()
        {
            while (pendingShots.Count > 0)
            {
                SetStatus("Guardando tiro en UGS...");
                var shot = pendingShots[0];
                try
                {
                    await repository.SaveAsync(shot);
                    pendingShots.RemoveAt(0);
                    SetStatus("Tiro guardado en UGS");
                }
                catch (Exception exception)
                {
                    SetStatus("No se pudo guardar en UGS. Revisá Services y la conexión.");
                    Debug.LogException(exception);
                    return;
                }
            }
        }

        private void SetStatus(string message)
        {
            Status = message;
            StatusChanged?.Invoke(message);
        }
    }
}
