# EffectBuilders

Fluent static API for registering layer effects.

## Main Method
- `Effect(string id)` - Start an effect registration.

## Example
```csharp
yield return EffectBuilders.Effect("myplugin.effect.halftone")
    .WithDisplayName("Halftone")
    .WithFactory<HalftoneEffect>()
    .WithOptions<HalftoneEffect>(e => e.GetOptions())
    .Build();
```

See also: `EffectBuilder` for more configuration options.
