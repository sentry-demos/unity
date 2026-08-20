using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    private Slider _slider;

    // Start is called before the first frame update
    private void Start()
    {
        _slider = GetComponent<Slider>();
    }

    // 0.0f = 0% health, 1.0f = 100% health
    public void SetHealth(float health)
    {
        GameLog.Trace("HealthBar.SetHealth: Setting health to " + health);
        _slider.value = health;
    }
}
