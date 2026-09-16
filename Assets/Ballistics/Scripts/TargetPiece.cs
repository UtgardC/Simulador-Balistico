using UnityEngine;

namespace Ballistics
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class TargetPiece : MonoBehaviour
    {
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        public bool IsDown => Vector3.Distance(transform.position, initialPosition) > 0.65f ||
                              Quaternion.Angle(transform.rotation, initialRotation) > 35f;
        private void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }
    }
}
