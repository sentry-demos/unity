using UnityEngine;

/// <summary>
/// Where the enemies in a wave are placed. Pure geometry -- it takes an anchor point and
/// returns offsets, so the formation shapes can be tested without spawning anything.
/// </summary>
/// <remarks>
/// An instance rather than a static class because the spacing values are tunable. Direction
/// mapping stays static: those are fixed relationships, not numbers anyone would tune.
/// </remarks>
public class WaveFormation
{
    private readonly float _linearWaveDistance;
    private readonly float _fanSpacingX;

    public WaveFormation(float linearWaveDistance, float fanSpacingX, float fanRangeY)
    {
        _linearWaveDistance = linearWaveDistance;
        _fanSpacingX = fanSpacingX;
        FanRangeY = fanRangeY;
    }

    public WaveFormation(BattleTuning tuning)
        : this(tuning.LinearWaveDistance, tuning.FanSpacingX, tuning.FanRangeY) { }

    /// <summary>Height of the random band a fanned-out spawn scatters within.</summary>
    public float FanRangeY { get; }

    /// <summary>The direction a doubled wave comes from, so the two pincer the player.</summary>
    public static LinearEnemy.Direction Opposite(LinearEnemy.Direction direction)
    {
        switch (direction)
        {
            case LinearEnemy.Direction.Up:
                return LinearEnemy.Direction.Down;
            case LinearEnemy.Direction.Down:
                return LinearEnemy.Direction.Up;
            case LinearEnemy.Direction.Left:
                return LinearEnemy.Direction.Right;
            case LinearEnemy.Direction.Right:
                return LinearEnemy.Direction.Left;
            default:
                throw new System.Exception("WaveFormation.Opposite: Invalid wave direction");
        }
    }

    /// <summary>
    /// Offset from the player to the first enemy of a linear wave: back along the direction
    /// of travel, and half the wave's width to the side so the line is centred on the player.
    /// </summary>
    public Vector3 LinearWaveStartOffset(int count, LinearEnemy.Direction direction)
    {
        switch (direction)
        {
            case LinearEnemy.Direction.Up:
                return new Vector3(-count / 2f, -_linearWaveDistance, 0);
            case LinearEnemy.Direction.Down:
                return new Vector3(count / 2f, _linearWaveDistance, 0);
            case LinearEnemy.Direction.Left:
                return new Vector3(_linearWaveDistance, -count / 2f, 0);
            case LinearEnemy.Direction.Right:
                return new Vector3(-_linearWaveDistance, count / 2f, 0);
            default:
                throw new System.Exception(
                    "WaveFormation.LinearWaveStartOffset: Invalid wave direction"
                );
        }
    }

    /// <summary>Step from one enemy in a linear wave to the next, perpendicular to travel.</summary>
    public static Vector3 LinearWaveStep(LinearEnemy.Direction direction)
    {
        switch (direction)
        {
            case LinearEnemy.Direction.Up:
                return new Vector3(1, 0, 0);
            case LinearEnemy.Direction.Down:
                return new Vector3(-1, 0, 0);
            case LinearEnemy.Direction.Left:
                return new Vector3(0, 1, 0);
            case LinearEnemy.Direction.Right:
                return new Vector3(0, -1, 0);
            default:
                throw new System.Exception("WaveFormation.LinearWaveStep: Invalid wave direction");
        }
    }

    /// <summary>
    /// Horizontal offset of the nth enemy in a fanned-out spawn: alternating sides, stepping
    /// further out every second enemy, so the group grows outward from its anchor.
    /// </summary>
    public float FanOffsetX(int index)
    {
        var flipX = index % 2 == 0 ? 1 : -1;
        return _fanSpacingX * ((1 + index) / 2) * flipX;
    }
}
