using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Effects;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Serialization;

namespace PixlPunkt.Core.Compositing.Serialization
{
    /// <summary>
    /// Provides serialization and deserialization of layer effects for .pxp file storage.
    /// </summary>
    /// <remarks>
    /// Uses source-generated JSON serialization for .NET trimming/AOT compatibility.
    /// </remarks>
    public static class EffectSerializer
    {
        /// <summary>
        /// Version marker for effect serialization format.
        /// </summary>
        private const byte FormatVersion = 1;

        /// <summary>
        /// Built-in effect vendor prefix.
        /// </summary>
        private const string BuiltInVendor = "pixlpunkt";

        //////////////////////////////////////////////////////////////////
        // SERIALIZATION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Serializes a list of effects to a binary writer.
        /// </summary>
        public static void SerializeEffects(BinaryWriter writer, IReadOnlyList<LayerEffectBase> effects)
        {
            writer.Write(FormatVersion);
            writer.Write(effects.Count);

            LoggingService.Debug("Serializing {Count} effects", effects.Count);

            foreach (var effect in effects)
            {
                SerializeEffect(writer, effect);
            }
        }

        /// <summary>
        /// Serializes a single effect to a binary writer.
        /// </summary>
        private static void SerializeEffect(BinaryWriter writer, LayerEffectBase effect)
        {
            // Handle orphaned effects specially - preserve their original data
            if (effect is OrphanedEffect orphaned)
            {
                writer.Write(orphaned.OriginalEffectId);
                writer.Write(orphaned.OriginalPluginId ?? string.Empty);
                writer.Write(orphaned.OriginalDisplayName);
                writer.Write(orphaned.WasOriginallyEnabled);
                writer.Write(orphaned.PreservedData.Length);
                writer.Write(orphaned.PreservedData);
                LoggingService.Debug("Serialized orphaned effect {EffectId}", orphaned.OriginalEffectId);
                return;
            }

            string effectId = effect.EffectId ?? throw new InvalidOperationException(
                $"Effect '{effect.DisplayName}' has no EffectId set.");

            string pluginId = IsBuiltInEffect(effectId) ? string.Empty : GetPluginIdFromEffectId(effectId);

            // Serialize effect properties to JSON using source-generated context
            byte[] data = SerializeEffectData(effect);

            writer.Write(effectId);
            writer.Write(pluginId);
            writer.Write(effect.DisplayName);
            writer.Write(effect.IsEnabled);
            writer.Write(data.Length);
            writer.Write(data);

            LoggingService.Debug("Serialized effect {EffectId} size={Size}", effectId, data.Length);
        }

        /// <summary>
        /// Serializes effect-specific properties to a byte array using source-generated JSON.
        /// </summary>
        private static byte[] SerializeEffectData(LayerEffectBase effect)
        {
            // Use source-generated JSON serialization for built-in effects
            var typeInfo = GetTypeInfo(effect.GetType());
            if (typeInfo != null)
            {
                string json = JsonSerializer.Serialize(effect, typeInfo);
                return Encoding.UTF8.GetBytes(json);
            }

            // Fallback for plugin effects - they must handle their own serialization
            // or use a serializer that supports their types
            LoggingService.Warning("No source-generated serializer for effect type {Type}, using empty data", effect.GetType().Name);
            return [];
        }

        //////////////////////////////////////////////////////////////////
        // DESERIALIZATION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deserializes effects from a binary reader.
        /// </summary>
        public static List<LayerEffectBase> DeserializeEffects(BinaryReader reader, ICollection<EffectLoadWarning>? warnings = null)
        {
            var effects = new List<LayerEffectBase>();

            byte version = reader.ReadByte();
            if (version != FormatVersion)
            {
                throw new InvalidDataException($"Unknown effect serialization format version: {version}");
            }

            int count = reader.ReadInt32();
            LoggingService.Info("Deserializing {Count} effects", count);

            for (int i = 0; i < count; i++)
            {
                var effect = DeserializeEffect(reader, warnings);
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }

            return effects;
        }

        /// <summary>
        /// Deserializes a single effect from a binary reader.
        /// </summary>
        private static LayerEffectBase? DeserializeEffect(BinaryReader reader, ICollection<EffectLoadWarning>? warnings)
        {
            string effectId = reader.ReadString();
            string pluginId = reader.ReadString();
            string displayName = reader.ReadString();
            bool isEnabled = reader.ReadBoolean();
            int dataLength = reader.ReadInt32();
            byte[] data = reader.ReadBytes(dataLength);

            bool isPlugin = !string.IsNullOrEmpty(pluginId);

            if (isPlugin)
            {
                return DeserializePluginEffect(effectId, pluginId, displayName, isEnabled, data, warnings);
            }
            else
            {
                return DeserializeBuiltInEffect(effectId, displayName, isEnabled, data, warnings);
            }
        }

        private static LayerEffectBase? DeserializePluginEffect(
            string effectId, string pluginId, string displayName, bool isEnabled, byte[] data,
            ICollection<EffectLoadWarning>? warnings)
        {
            var registration = EffectRegistry.Shared.GetById(effectId);
            if (registration != null)
            {
                try
                {
                    var effect = registration.CreateInstance();
                    DeserializeEffectData(effect, data);
                    effect.EffectId = effectId;
                    effect.IsEnabled = isEnabled;
                    LoggingService.Debug("Loaded plugin effect {EffectId}", effectId);
                    return effect;
                }
                catch (Exception ex)
                {
                    warnings?.Add(new EffectLoadWarning(effectId, pluginId, displayName,
                        $"Failed to load effect data: {ex.Message}"));
                    LoggingService.Error($"Failed to deserialize plugin effect {effectId}", ex);
                    return new OrphanedEffect(effectId, pluginId, displayName, isEnabled, data);
                }
            }

            warnings?.Add(new EffectLoadWarning(effectId, pluginId, displayName,
                $"Plugin '{pluginId}' is not installed"));
            LoggingService.Warning("Plugin effect not available {EffectId}", effectId);
            return new OrphanedEffect(effectId, pluginId, displayName, isEnabled, data);
        }

        private static LayerEffectBase? DeserializeBuiltInEffect(
            string effectId, string displayName, bool isEnabled, byte[] data,
            ICollection<EffectLoadWarning>? warnings)
        {
            var registration = EffectRegistry.Shared.GetById(effectId);
            if (registration == null)
            {
                warnings?.Add(new EffectLoadWarning(effectId, string.Empty, displayName,
                    "Unknown built-in effect type"));
                LoggingService.Warning("Unknown built-in effect type {EffectId}", effectId);
                return null;
            }

            try
            {
                var effect = registration.CreateInstance();
                DeserializeEffectData(effect, data);
                effect.EffectId = effectId;
                effect.IsEnabled = isEnabled;
                LoggingService.Debug("Loaded built-in effect {EffectId}", effectId);
                return effect;
            }
            catch (Exception ex)
            {
                warnings?.Add(new EffectLoadWarning(effectId, string.Empty, displayName,
                    $"Failed to load effect: {ex.Message}"));
                LoggingService.Error($"Failed to load built-in effect {effectId}", ex);

                // Try to create a default instance
                try
                {
                    var effect = registration.CreateInstance();
                    effect.EffectId = effectId;
                    effect.IsEnabled = isEnabled;
                    return effect;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Deserializes effect-specific properties from a byte array.
        /// </summary>
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(AsciiEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ChromaticAberrationEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ColorAdjustEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CrtEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(DropShadowEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GlowBloomEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GrainEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OrphanedEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OutlineEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PaletteQuantizeEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PixelateEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScanLinesEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(VignetteEffect))]
        private static void DeserializeEffectData(LayerEffectBase effect, byte[] data)
        {
            if (data.Length == 0)
                return;

            string json = Encoding.UTF8.GetString(data);
            var effectType = effect.GetType();

            // Use source-generated deserializer for built-in effects
            var typeInfo = GetTypeInfo(effectType);
            if (typeInfo != null)
            {
                var tempEffect = JsonSerializer.Deserialize(json, typeInfo) as LayerEffectBase;
                if (tempEffect != null)
                {
                    CopyEffectProperties(tempEffect, effect);
                }
                return;
            }

            // Plugin effects - they need to handle their own deserialization
            LoggingService.Debug("No source-generated deserializer for effect type {Type}", effectType.Name);
        }

        /// <summary>
        /// Copies effect-specific properties from source to target.
        /// Uses reflection but with DynamicDependency to preserve properties.
        /// </summary>
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(AsciiEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ChromaticAberrationEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ColorAdjustEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CrtEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(DropShadowEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GlowBloomEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GrainEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OrphanedEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OutlineEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PaletteQuantizeEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PixelateEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScanLinesEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(VignetteEffect))]
        private static void CopyEffectProperties(LayerEffectBase source, LayerEffectBase target)
        {
            var effectType = source.GetType();
            var baseProps = new HashSet<string> { "IsEnabled", "EffectId", "DisplayName" };

            foreach (var prop in effectType.GetProperties())
            {
                if (baseProps.Contains(prop.Name))
                    continue;

                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                try
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(target, value);
                }
                catch
                {
                    // Skip properties that can't be copied
                }
            }
        }

        /// <summary>
        /// Gets the source-generated JsonTypeInfo for built-in effect types.
        /// </summary>
        private static JsonTypeInfo? GetTypeInfo(Type effectType)
        {
            // Map effect types to their source-generated type info
            if (effectType == typeof(AsciiEffect))
                return EffectJsonContext.Default.AsciiEffect;
            if (effectType == typeof(ChromaticAberrationEffect))
                return EffectJsonContext.Default.ChromaticAberrationEffect;
            if (effectType == typeof(ColorAdjustEffect))
                return EffectJsonContext.Default.ColorAdjustEffect;
            if (effectType == typeof(CrtEffect))
                return EffectJsonContext.Default.CrtEffect;
            if (effectType == typeof(DropShadowEffect))
                return EffectJsonContext.Default.DropShadowEffect;
            if (effectType == typeof(GlowBloomEffect))
                return EffectJsonContext.Default.GlowBloomEffect;
            if (effectType == typeof(GrainEffect))
                return EffectJsonContext.Default.GrainEffect;
            if (effectType == typeof(OrphanedEffect))
                return EffectJsonContext.Default.OrphanedEffect;
            if (effectType == typeof(OutlineEffect))
                return EffectJsonContext.Default.OutlineEffect;
            if (effectType == typeof(PaletteQuantizeEffect))
                return EffectJsonContext.Default.PaletteQuantizeEffect;
            if (effectType == typeof(PixelateEffect))
                return EffectJsonContext.Default.PixelateEffect;
            if (effectType == typeof(ScanLinesEffect))
                return EffectJsonContext.Default.ScanLinesEffect;
            if (effectType == typeof(VignetteEffect))
                return EffectJsonContext.Default.VignetteEffect;

            return null;
        }

        //////////////////////////////////////////////////////////////////
        // HELPERS
        //////////////////////////////////////////////////////////////////

        private static bool IsBuiltInEffect(string effectId)
        {
            return effectId?.StartsWith(BuiltInVendor + ".") == true;
        }

        private static string GetPluginIdFromEffectId(string effectId)
        {
            if (string.IsNullOrEmpty(effectId))
                return string.Empty;

            int firstDot = effectId.IndexOf('.');
            if (firstDot > 0)
            {
                return effectId[..firstDot];
            }

            return effectId;
        }
    }

    /// <summary>
    /// Represents a warning generated during effect loading.
    /// </summary>
    public sealed record EffectLoadWarning(
        string EffectId,
        string PluginId,
        string DisplayName,
        string Message)
    {
        public bool IsPluginEffect => !string.IsNullOrEmpty(PluginId);

        public string Description => IsPluginEffect
            ? $"Effect '{DisplayName}' from plugin '{PluginId}' is unavailable: {Message}"
            : $"Effect '{DisplayName}' could not be loaded: {Message}";
    }
}
