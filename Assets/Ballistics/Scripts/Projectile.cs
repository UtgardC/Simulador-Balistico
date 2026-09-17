using UnityEngine;

namespace Ballistics
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Projectile : MonoBehaviour
    {
        private BallisticSession session;
        private float launchTime;

        private void Awake()
        {
            var trail = GetComponent<TrailRenderer>();
            if (trail == null) return;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        public void Launch(BallisticSession owner, Vector3 direction, float impulse, float mass)
        {
            session = owner;
            launchTime = Time.fixedTime;
            var body = GetComponent<Rigidbody>();
            body.mass = mass;
            body.AddForce(direction * impulse, ForceMode.Impulse);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (session == null || !session.IsRunning) return;
            session.RegisterImpact(new ImpactRecord
            {
                flightTime = Time.fixedTime - launchTime,
                objectName = collision.gameObject.name,
                target = collision.gameObject.GetComponent<TargetPiece>() != null,
                point = collision.GetContact(0).point,
                relativeVelocity = collision.relativeVelocity,
                impulse = collision.impulse
            });
        }
    }
}
