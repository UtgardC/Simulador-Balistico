using System;
using UnityEngine;

namespace Ballistics
{
    // Small, independent snapshot stored in Cloud Save after a shot ends.
    [Serializable]
    public class SavedShot
    {
        public string id;
        public string timestampUtc;
        public float angleDegrees;
        public float horizontalAngleDegrees;
        public float impulseNs;
        public float massKg;
        public bool hitTarget;
        public float distanceMeters;
        public int piecesDown;
        public float durationSeconds;
        public bool hasDuration;
        public string endReason;

        public static SavedShot FromShot(ShotRecord shot)
        {
            return new SavedShot
            {
                id = Guid.NewGuid().ToString("N"),
                timestampUtc = shot.timestampUtc,
                angleDegrees = shot.angleDegrees,
                horizontalAngleDegrees = shot.horizontalAngleDegrees,
                impulseNs = shot.launchImpulseNs,
                massKg = shot.massKg,
                hitTarget = shot.hitTarget,
                distanceMeters = shot.distanceMeters,
                piecesDown = shot.piecesDown,
                durationSeconds = shot.duration,
                hasDuration = true,
                endReason = shot.endReason
            };
        }
    }
}
