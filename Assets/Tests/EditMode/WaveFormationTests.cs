using NUnit.Framework;
using UnityEngine;

public class WaveFormationTests
{
    // The values the tuning asset ships with, so the expectations below stay concrete.
    private WaveFormation _formation;

    [SetUp]
    public void SetUp()
    {
        _formation = new WaveFormation(linearWaveDistance: 10f, fanSpacingX: 0.75f, fanRangeY: 2f);
    }

    [Test]
    public void OppositeReversesEveryDirection()
    {
        Assert.AreEqual(
            LinearEnemy.Direction.Down,
            WaveFormation.Opposite(LinearEnemy.Direction.Up)
        );
        Assert.AreEqual(
            LinearEnemy.Direction.Up,
            WaveFormation.Opposite(LinearEnemy.Direction.Down)
        );
        Assert.AreEqual(
            LinearEnemy.Direction.Right,
            WaveFormation.Opposite(LinearEnemy.Direction.Left)
        );
        Assert.AreEqual(
            LinearEnemy.Direction.Left,
            WaveFormation.Opposite(LinearEnemy.Direction.Right)
        );
    }

    [Test]
    public void OppositeIsItsOwnInverse()
    {
        foreach (LinearEnemy.Direction direction in System.Enum.GetValues(typeof(LinearEnemy.Direction)))
        {
            Assert.AreEqual(direction, WaveFormation.Opposite(WaveFormation.Opposite(direction)));
        }
    }

    [Test]
    public void WavesStartOffScreenBehindTheirDirectionOfTravel()
    {
        // A wave moving Up begins below the player, and vice versa.
        Assert.AreEqual(-10f, _formation.LinearWaveStartOffset(4, LinearEnemy.Direction.Up).y);
        Assert.AreEqual(10f, _formation.LinearWaveStartOffset(4, LinearEnemy.Direction.Down).y);
        Assert.AreEqual(10f, _formation.LinearWaveStartOffset(4, LinearEnemy.Direction.Left).x);
        Assert.AreEqual(
            -10f,
            _formation.LinearWaveStartOffset(4, LinearEnemy.Direction.Right).x
        );
    }

    [Test]
    public void TheWaveLineIsCentredOnThePlayer()
    {
        // The start offset backs off half the wave width, so stepping count-1 times leaves
        // the line straddling the player rather than starting at them.
        const int count = 6;
        var start = _formation.LinearWaveStartOffset(count, LinearEnemy.Direction.Up);
        var step = WaveFormation.LinearWaveStep(LinearEnemy.Direction.Up);

        var first = start;
        var last = start + (count - 1) * step;

        Assert.AreEqual(-3f, first.x, 0.0001f);
        Assert.AreEqual(2f, last.x, 0.0001f);
    }

    [Test]
    public void EachStepIsPerpendicularToTheDirectionOfTravel()
    {
        // Vertical waves spread horizontally and vice versa; a step parallel to travel would
        // stack the whole wave into a single column.
        Assert.AreEqual(new Vector3(1, 0, 0), WaveFormation.LinearWaveStep(LinearEnemy.Direction.Up));
        Assert.AreEqual(
            new Vector3(-1, 0, 0),
            WaveFormation.LinearWaveStep(LinearEnemy.Direction.Down)
        );
        Assert.AreEqual(
            new Vector3(0, 1, 0),
            WaveFormation.LinearWaveStep(LinearEnemy.Direction.Left)
        );
        Assert.AreEqual(
            new Vector3(0, -1, 0),
            WaveFormation.LinearWaveStep(LinearEnemy.Direction.Right)
        );
    }

    [Test]
    public void TheFanAlternatesSidesAndWidensEverySecondEnemy()
    {
        Assert.AreEqual(0f, _formation.FanOffsetX(0), 0.0001f);
        Assert.AreEqual(-0.75f, _formation.FanOffsetX(1), 0.0001f);
        Assert.AreEqual(0.75f, _formation.FanOffsetX(2), 0.0001f);
        Assert.AreEqual(-1.5f, _formation.FanOffsetX(3), 0.0001f);
        Assert.AreEqual(1.5f, _formation.FanOffsetX(4), 0.0001f);
    }

    [Test]
    public void TheFanStaysBalancedAroundItsAnchor()
    {
        // Index 0 sits on the anchor and the rest straddle it in pairs -- (1,2), (3,4), and
        // so on -- so the group is only balanced at odd counts. An even count leaves the
        // last enemy unpaired, which is why a 6-wide fan leans one step to the left.
        var sum = 0f;
        for (var i = 0; i < 5; i++)
        {
            sum += _formation.FanOffsetX(i);
        }

        Assert.AreEqual(0f, sum, 0.0001f);
    }

    [Test]
    public void AnEvenSizedFanLeansByOneStep()
    {
        var sum = 0f;
        for (var i = 0; i < 6; i++)
        {
            sum += _formation.FanOffsetX(i);
        }

        // the unpaired sixth enemy, three steps out on the left
        Assert.AreEqual(-2.25f, sum, 0.0001f);
    }
}
