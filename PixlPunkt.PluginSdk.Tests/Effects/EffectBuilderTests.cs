using PixlPunkt.PluginSdk.Compositing;
using PixlPunkt.PluginSdk.Effects;
using PixlPunkt.PluginSdk.Effects.Builders;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Tests.Effects;

/// <summary>
/// Tests for <see cref="EffectBuilder"/> and effect registration.
/// </summary>
public class EffectBuilderTests
{
    // ========================================================================
    // TEST DOUBLES
    // ========================================================================

    private class TestEffect : LayerEffectBase
    {
        public int Intensity { get; set; } = 50;

        public override string DisplayName => "Test Effect";

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            // No-op for testing
        }

        public IEnumerable<IToolOption> GetOptions()
        {
            yield return new TestSliderOption("intensity", "Intensity", () => Intensity, v => Intensity = (int)v, 0, 100);
        }
    }

    private class NoOptionsEffect : LayerEffectBase
    {
        public override string DisplayName => "No Options";
        public override void Apply(Span<uint> pixels, int width, int height) { }
    }

    // Simple option implementation for testing
    private class TestSliderOption : IToolOption
    {
        public string Id { get; }
        public string Label { get; }
        public int Order { get; } = 0;
        public string Group { get; } = "";
        public string? Tooltip { get; } = null;

        public Func<double> Getter { get; }
        public Action<double> Setter { get; }
        public double Min { get; }
        public double Max { get; }

        public TestSliderOption(string id, string label, Func<double> getter, Action<double> setter, double min, double max)
        {
            Id = id;
            Label = label;
            Getter = getter;
            Setter = setter;
            Min = min;
            Max = max;
        }
    }

    // ========================================================================
    // BUILDER CREATION
    // ========================================================================

    [Fact]
    public void Effect_CreatesBuilder()
    {
        var builder = EffectBuilders.Effect("myplugin.effect.test");
        Assert.NotNull(builder);
    }

    [Fact]
    public void Effect_NullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => EffectBuilders.Effect(null!));
    }

    // ========================================================================
    // FLUENT API
    // ========================================================================

    [Fact]
    public void WithDisplayName_SetsDisplayName()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithDisplayName("My Custom Effect")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal("My Custom Effect", reg.DisplayName);
    }

    [Fact]
    public void WithDescription_SetsDescription()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithDescription("This effect does amazing things")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal("This effect does amazing things", reg.Description);
    }

    [Fact]
    public void WithCategory_SetsCategory()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithCategory(EffectCategory.Stylize)
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal(EffectCategory.Stylize, reg.Category);
    }

    [Fact]
    public void DefaultCategory_IsFilter()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal(EffectCategory.Filter, reg.Category);
    }

    // ========================================================================
    // FACTORY CONFIGURATION
    // ========================================================================

    [Fact]
    public void WithFactory_Lambda_CreatesEffect()
    {
        var testEffect = new TestEffect { Intensity = 75 };

        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory(() => testEffect)
            .WithNoOptions()
            .Build();

        var created = reg.EffectFactory();
        Assert.Same(testEffect, created);
    }

    [Fact]
    public void WithFactory_Generic_CreatesEffect()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        var created = reg.EffectFactory();
        Assert.IsType<TestEffect>(created);
    }

    [Fact]
    public void WithFactory_Generic_CreatesNewInstancesEachCall()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        var effect1 = reg.EffectFactory();
        var effect2 = reg.EffectFactory();

        Assert.NotSame(effect1, effect2);
    }

    // ========================================================================
    // OPTIONS CONFIGURATION
    // ========================================================================

    [Fact]
    public void WithOptions_Lambda_ReturnsOptions()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithOptions(effect => ((TestEffect)effect).GetOptions())
            .Build();

        var effect = new TestEffect();
        var options = reg.OptionsFactory(effect).ToList();

        Assert.Single(options);
    }

    [Fact]
    public void WithOptions_Generic_ReturnsOptions()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithOptions<TestEffect>(e => e.GetOptions())
            .Build();

        var effect = new TestEffect();
        var options = reg.OptionsFactory(effect).ToList();

        Assert.Single(options);
    }

    [Fact]
    public void WithNoOptions_ReturnsEmptyEnumerable()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<NoOptionsEffect>()
            .WithNoOptions()
            .Build();

        var effect = new NoOptionsEffect();
        var options = reg.OptionsFactory(effect).ToList();

        Assert.Empty(options);
    }

    // ========================================================================
    // BUILD VALIDATION
    // ========================================================================

    [Fact]
    public void Build_WithoutFactory_ThrowsInvalidOperation()
    {
        var builder = EffectBuilders.Effect("test.effect")
            .WithNoOptions();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithoutOptions_ThrowsInvalidOperation()
    {
        var builder = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_WithAllRequired_Succeeds()
    {
        var reg = EffectBuilders.Effect("test.effect")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.NotNull(reg);
        Assert.Equal("test.effect", reg.Id);
    }

    // ========================================================================
    // DISPLAY NAME EXTRACTION
    // ========================================================================

    [Fact]
    public void DisplayName_DefaultsToIdSuffix()
    {
        var reg = EffectBuilders.Effect("myplugin.effect.halftone")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal("Halftone", reg.DisplayName);
    }

    [Fact]
    public void DisplayName_CapitalizesFirstLetter()
    {
        var reg = EffectBuilders.Effect("myplugin.effect.blur")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal("Blur", reg.DisplayName);
    }

    [Fact]
    public void DisplayName_ExplicitOverridesDefault()
    {
        var reg = EffectBuilders.Effect("myplugin.effect.blur")
            .WithDisplayName("Gaussian Blur")
            .WithFactory<TestEffect>()
            .WithNoOptions()
            .Build();

        Assert.Equal("Gaussian Blur", reg.DisplayName);
    }

    // ========================================================================
    // FLUENT CHAINING
    // ========================================================================

    [Fact]
    public void FluentChaining_AllMethodsReturnBuilder()
    {
        // Verify the entire fluent chain works
        var reg = EffectBuilders.Effect("myplugin.effect.complete")
            .WithDisplayName("Complete Effect")
            .WithDescription("A fully configured effect")
            .WithCategory(EffectCategory.Stylize)
            .WithFactory<TestEffect>()
            .WithOptions<TestEffect>(e => e.GetOptions())
            .Build();

        Assert.Equal("myplugin.effect.complete", reg.Id);
        Assert.Equal("Complete Effect", reg.DisplayName);
        Assert.Equal("A fully configured effect", reg.Description);
        Assert.Equal(EffectCategory.Stylize, reg.Category);
    }
}
