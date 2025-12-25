using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Stores the state of a single layer effect at a keyframe.
    /// Captures IsEnabled and all animatable property values.
    /// </summary>
    public sealed class EffectKeyframeData
    {
        /// <summary>
        /// Gets or sets the effect ID (matches LayerEffectBase.EffectId).
        /// </summary>
        public string EffectId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the effect is enabled at this keyframe.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the property values for this effect.
        /// Key is property name, value is the property value (boxed).
        /// </summary>
        public Dictionary<string, object?> PropertyValues { get; set; } = [];

        /// <summary>
        /// Creates an empty effect keyframe data.
        /// </summary>
        public EffectKeyframeData()
        {
        }

        /// <summary>
        /// Creates an effect keyframe data from an effect instance.
        /// </summary>
        public EffectKeyframeData(LayerEffectBase effect)
        {
            EffectId = effect.EffectId;
            IsEnabled = effect.IsEnabled;
            CaptureProperties(effect);
        }

        /// <summary>
        /// Captures all animatable properties from the effect.
        /// </summary>
        private void CaptureProperties(LayerEffectBase effect)
        {
            PropertyValues.Clear();
            var type = effect.GetType();

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                // Skip non-animatable properties
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.GetIndexParameters().Length != 0) continue;

                // Skip base properties that are handled separately
                if (prop.Name == nameof(LayerEffectBase.IsEnabled)) continue;
                if (prop.Name == nameof(LayerEffectBase.EffectId)) continue;

                // Only capture value types, strings, and enums (animatable)
                var propType = prop.PropertyType;
                if (propType.IsValueType || propType == typeof(string) || propType.IsEnum)
                {
                    try
                    {
                        var value = prop.GetValue(effect);
                        PropertyValues[prop.Name] = value;
                    }
                    catch
                    {
                        // Skip properties that throw on access
                    }
                }
            }
        }

        /// <summary>
        /// Applies this keyframe data to an effect instance.
        /// </summary>
        public void ApplyTo(LayerEffectBase effect)
        {
            if (effect.EffectId != EffectId) return;

            effect.IsEnabled = IsEnabled;

            var type = effect.GetType();
            foreach (var kvp in PropertyValues)
            {
                var prop = type.GetProperty(kvp.Key, BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || !prop.CanWrite) continue;

                try
                {
                    var value = kvp.Value;

                    // Handle enum conversion from string (for serialization compatibility)
                    if (prop.PropertyType.IsEnum && value is string strValue)
                    {
                        value = Enum.Parse(prop.PropertyType, strValue);
                    }
                    // Handle numeric conversions
                    else if (value != null && prop.PropertyType != value.GetType())
                    {
                        value = Convert.ChangeType(value, prop.PropertyType);
                    }

                    prop.SetValue(effect, value);
                }
                catch
                {
                    // Skip properties that fail to set
                }
            }
        }

        /// <summary>
        /// Creates a deep copy of this effect keyframe data.
        /// </summary>
        public EffectKeyframeData Clone()
        {
            var clone = new EffectKeyframeData
            {
                EffectId = EffectId,
                IsEnabled = IsEnabled
            };

            foreach (var kvp in PropertyValues)
            {
                // Clone value types and strings directly
                clone.PropertyValues[kvp.Key] = kvp.Value;
            }

            return clone;
        }

        /// <summary>
        /// Checks if this effect keyframe has the same values as another.
        /// </summary>
        public bool HasSameValues(EffectKeyframeData other)
        {
            if (other == null) return false;
            if (EffectId != other.EffectId) return false;
            if (IsEnabled != other.IsEnabled) return false;
            if (PropertyValues.Count != other.PropertyValues.Count) return false;

            foreach (var kvp in PropertyValues)
            {
                if (!other.PropertyValues.TryGetValue(kvp.Key, out var otherValue))
                    return false;
                if (!Equals(kvp.Value, otherValue))
                    return false;
            }

            return true;
        }
    }
}
