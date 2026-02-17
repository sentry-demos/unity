using UnityEngine;
using UnityEngine.InputSystem;

public class Arrow : MonoBehaviour
{
    [SerializeField] private bool _forceEnable;
    
    private void Awake()
    {
        if (_forceEnable)
        {
            return;
        }
        
        // Show arrow on mobile platforms or when a gamepad is connected
        bool shouldShowArrow = Application.platform == RuntimePlatform.Android || 
                               Application.platform == RuntimePlatform.IPhonePlayer ||
                               Gamepad.current != null;
        
        gameObject.SetActive(shouldShowArrow);
    }
}
