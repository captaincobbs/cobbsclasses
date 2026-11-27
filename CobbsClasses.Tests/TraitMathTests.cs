using CobbsClasses.Source.API;

namespace CobbsClasses.Tests;

public sealed class TraitMathTests
{
    [Test]
    public void MissingStatsUseNeutralValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TraitMath.GetMultiplier(null, "missing"), Is.EqualTo(1f));
            Assert.That(TraitMath.GetAdditive(null, "missing"), Is.Zero);
            Assert.That(TraitMath.GetBoolean(null, "missing"), Is.False);
        });
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFiniteValueUsesFallback(float value)
    {
        Assert.That(TraitMath.ClampFinite(value, 0, 2, 1), Is.EqualTo(1f));
    }

    [TestCase(10, 2d, 20)]
    [TestCase(10, 0.5d, 5)]
    [TestCase(10, 0d, 0)]
    [TestCase(4, -1d, 0)]
    [TestCase(4, double.NaN, 4)]
    [TestCase(int.MaxValue, double.MaxValue, int.MaxValue)]
    [TestCase(0, 5d, 0)]
    public void QuantityScaling(int quantity, double multiplier, int expected)
    {
        Assert.That(TraitMath.ScaleQuantity(new Random(123), quantity, multiplier), Is.EqualTo(expected));
    }

    [Test]
    public void QuantityScalingHonorsMaximum()
    {
        Assert.That(TraitMath.ScaleQuantity(new Random(123), 10, 10, 64), Is.EqualTo(64));
    }

    [Test]
    public void FractionalOutputHasExpectedAverage()
    {
        Random random = new(123);
        double average = Enumerable.Range(0, 10_000).Average(_ => TraitMath.ScaleQuantity(random, 3, 1.25));
        Assert.That(average, Is.EqualTo(3.75).Within(0.03));
    }

    [TestCase(10f, 2f, 5f)]
    [TestCase(10f, 1f, 10f)]
    [TestCase(10f, 0f, 200f)]
    [TestCase(10f, float.NaN, 10f)]
    public void ProcessingTimeUsesBoundedSpeed(float duration, float speed, float expected)
    {
        Assert.That(TraitMath.ScaleProcessingTime(duration, speed), Is.EqualTo(expected).Within(0.001));
    }

    [Test]
    public void InvalidBoundsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TraitMath.ClampFinite(1, 2, 0, 1));
    }
}