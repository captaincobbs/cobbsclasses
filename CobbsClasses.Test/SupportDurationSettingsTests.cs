using CobbsClasses.Source.Configuration;

using Vintagestory.API.Datastructures;

namespace CobbsClasses.Test;

public sealed class SupportDurationSettingsTests
{
    [Test]
    public void DefaultsPreserveExistingDurations()
    {
        SupportDurationSettings settings = SupportDurationSettings.FromSettings(null, null);
        Assert.Multiple(() =>
        {
            Assert.That(settings.ReviveProtectionSeconds, Is.EqualTo(120f));
            Assert.That(settings.ProtectorRetaliationSeconds, Is.EqualTo(120f));
            Assert.That(settings.ProtectorSprintSeconds, Is.EqualTo(5f));
            Assert.That(settings.TargetedRegenSeconds, Is.EqualTo(10f));
        });
    }

    [Test]
    public void ConfigLibAndImmValuesAreRespected()
    {
        JsonObject configLib = JsonObject.FromJson("""{"ReviveProtectionSeconds":60,"ProtectorSprintSeconds":15}""");
        JsonObject imm = JsonObject.FromJson("""{"ProtectorSprintSeconds":8,"ProtectorRetaliationSeconds":0}""");
        SupportDurationSettings settings = SupportDurationSettings.FromSettings(configLib, imm);
        Assert.Multiple(() =>
        {
            Assert.That(settings.ReviveProtectionSeconds, Is.EqualTo(60f));
            Assert.That(settings.ProtectorSprintSeconds, Is.EqualTo(8f));
            Assert.That(settings.ProtectorRetaliationSeconds, Is.Zero);
        });
    }

    [TestCase(-1f, 0d)]
    [TestCase(0f, 0d)]
    [TestCase(5f, 5000d)]
    [TestCase(4000f, 3600000d)]
    [TestCase(float.NaN, 0d)]
    public void MillisecondsAreBoundedAndFinite(float seconds, double expected)
    {
        Assert.That(SupportDurationSettings.Milliseconds(seconds), Is.EqualTo(expected));
    }
}