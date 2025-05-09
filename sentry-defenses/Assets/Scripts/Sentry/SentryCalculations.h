#ifndef SENTRY_CALCULATIONS_H
#define SENTRY_CALCULATIONS_H

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32) || defined(_WIN64)
    #define EXPORT_API __declspec(dllexport)
#else
    #define EXPORT_API
#endif

EXPORT_API float CalculateTowerRadius(float rangeUpgradeLevel);
EXPORT_API float CalculateAttackRangeRadius(float baseRadius);
EXPORT_API float CalculateExperimentalTowerRadius(float rangeUpgradeLevel);

#ifdef __cplusplus
}
#endif

#endif // SENTRY_CALCULATIONS_H 