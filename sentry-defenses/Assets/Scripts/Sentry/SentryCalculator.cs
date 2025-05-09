using System.Runtime.InteropServices;
using UnityEngine;

public static class SentryCalculator
{
    [DllImport("__Internal")]
    private static extern float CalculateTowerRadius(float rangeUpgradeLevel);

    [DllImport("__Internal")]
    private static extern float CalculateAttackRangeRadius(float baseRadius);

    [DllImport("__Internal")]
    private static extern float CalculateExperimentalTowerRadius(float rangeUpgradeLevel);

    public static float GetTowerRadius(float rangeUpgradeLevel)
    {
        try
        {
            if (Random.value < 0.1f)
            {
                Debug.Log("Calculating Experimental Tower Radius");
                return CalculateExperimentalTowerRadius(rangeUpgradeLevel);
            }
            else
            {
                Debug.Log("Calculating Tower Radius");
                return CalculateTowerRadius(rangeUpgradeLevel);    
            }
            
        }
        catch (System.Exception)
        {
            Debug.Log("Falling back to C# Range calculations");
            return Mathf.Pow(1.10f, rangeUpgradeLevel);
        }
    }

    public static float GetAttackRangeRadius(float towerRadius)
    {
        try
        {
            Debug.Log("Calculating Attack Radius");
            return CalculateAttackRangeRadius(towerRadius);
        }
        catch (System.Exception)
        {
            Debug.Log("Falling back to C# Attack calculations");
            return 1.35f * towerRadius;
        }
    }
} 