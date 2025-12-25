# EffectSettingsBase

Base class for effect-specific settings with dynamic UI generation.

## Key Members
- `string DisplayName` - Effect name
- `string Description` - Effect description
- `IEnumerable<IToolOption> GetOptions()` - Returns options for the effect settings panel

## Example
```csharp
public class MyEffectSettings : EffectSettingsBase
{
    public override string DisplayName => "My Effect";
    public override IEnumerable<IToolOption> GetOptions()
    {
        yield return new SliderOption("amount", "Amount", 0, 100, _effect.Amount, v => _effect.Amount = (int)v);
    }
}
```
