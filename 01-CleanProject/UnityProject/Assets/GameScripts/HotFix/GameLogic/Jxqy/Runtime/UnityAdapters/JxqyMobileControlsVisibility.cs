using UnityEngine;

namespace Jxqy.UnityAdapters
{
    public sealed class JxqyMobileControlsVisibility : MonoBehaviour
    {
        [SerializeField] private bool _showInEditor;

        private void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            gameObject.SetActive(true);
#elif UNITY_EDITOR
            gameObject.SetActive(_showInEditor);
#else
            gameObject.SetActive(false);
#endif
        }
    }
}
