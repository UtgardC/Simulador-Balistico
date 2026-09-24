using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace Ballistics
{
    public interface IShotRepository
    {
        Task SaveAsync(SavedShot shot);
        Task<List<SavedShot>> LoadAllAsync();
    }

    public sealed class UgsShotRepository : IShotRepository
    {
        private const string KeyPrefix = "shot_";
        private Task initializationTask;

        public async Task SaveAsync(SavedShot shot)
        {
            await EnsureReadyAsync();
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object>
                {
                    { KeyPrefix + shot.id, JsonUtility.ToJson(shot) }
                });
        }

        public async Task<List<SavedShot>> LoadAllAsync()
        {
            await EnsureReadyAsync();
            var items = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
            var shots = new List<SavedShot>();

            foreach (var item in items)
            {
                if (!item.Key.StartsWith(KeyPrefix, StringComparison.Ordinal)) continue;
                try
                {
                    var shot = JsonUtility.FromJson<SavedShot>(item.Value.Value.GetAs<string>());
                    if (shot != null && !string.IsNullOrEmpty(shot.id))
                        shots.Add(shot);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"No se pudo leer el tiro guardado '{item.Key}': {exception.Message}");
                }
            }

            shots.Sort((a, b) => string.CompareOrdinal(b.timestampUtc, a.timestampUtc));
            return shots;
        }

        private async Task EnsureReadyAsync()
        {
            if (initializationTask == null)
                initializationTask = UnityServices.InitializeAsync();

            try
            {
                await initializationTask;
            }
            catch
            {
                initializationTask = null;
                throw;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
