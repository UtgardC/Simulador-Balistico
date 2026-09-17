using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ballistics
{
    [Serializable]
    public class ImpactRecord
    {
        public float flightTime;
        public string objectName;
        public bool target;
        public Vector3 point;
        public Vector3 relativeVelocity;
        public Vector3 impulse;
    }

    [Serializable]
    public class ShotRecord
    {
        public int attempt;
        public string timestampUtc;
        public float angleDegrees, launchImpulseNs, massKg, duration;
        public string endReason;
        public int piecesDown;
        public bool hitTarget;
        public List<ImpactRecord> impacts = new List<ImpactRecord>();
    }
}
