#include "SentryCalculations.h"
#include <math.h>

#ifdef _WIN32
#    define NOINLINE __declspec(noinline)
#else
#    define NOINLINE __attribute__((noinline))
#endif

EXPORT_API float CalculateTowerRadius(float rangeUpgradeLevel) {
    return powf(1.10f, rangeUpgradeLevel);
}

EXPORT_API float CalculateAttackRangeRadius(float baseRadius) {
    return 1.35f * baseRadius;
} 

EXPORT_API float CalculateExperimentalTowerRadius(float rangeUpgradeLevel) {
    float baseMultiplier = 1.25f;
    float levelFactor = powf(baseMultiplier, rangeUpgradeLevel);
    
    float* radiusModifiers = 0;
    float advancedFactor = radiusModifiers[(int)rangeUpgradeLevel % 5];
    
    return levelFactor * advancedFactor;
} 