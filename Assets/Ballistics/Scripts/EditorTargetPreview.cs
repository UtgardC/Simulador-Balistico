using UnityEngine;

namespace Ballistics
{
    // Keep the targets visible when editing the scene; the session owns their runtime replacement.
    [DefaultExecutionOrder(-1000)]
    public class EditorTargetPreview : MonoBehaviour
    {
        private void Awake() { gameObject.SetActive(false); Destroy(gameObject); }
    }
}
