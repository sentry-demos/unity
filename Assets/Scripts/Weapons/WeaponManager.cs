using System.Collections.Generic;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    /// <summary>
    /// The player's global weapon modifiers. Runtime state, deliberately not serialised: it
    /// accumulates over a run and must start fresh each time.
    /// </summary>
    public WeaponStats Stats { get; } = new WeaponStats();

    private List<WeaponBase> _weapons = new List<WeaponBase>();

    private void Awake()
    {
        _weapons.AddRange(GetComponentsInChildren<WeaponBase>());
    }

    public void Add(WeaponBase weapon)
    {
        weapon.transform.parent = transform;

        _weapons.Add(weapon);
    }

    private void Update()
    {
        foreach (var weapon in _weapons)
        {
            if (weapon.CanFire())
            {
                weapon.Fire();
            }
        }
    }
}
