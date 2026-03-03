using System.Collections.Generic;
using System.Numerics;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Voxel;

namespace PixlPunkt.Core.Voxel.Tools
{
    /// <summary>
    /// Built-in voxel lighting utility settings exposed through the shared tool options UI.
    /// </summary>
    public sealed class VoxelLightingToolSettings : ToolSettingsBase
    {
        private bool _enabled;
        private float _positionX = 32f;
        private float _positionY = 48f;
        private float _positionZ = 32f;
        private uint _lightColorBgra = 0xFFFFFFFF;
        private uint _shadowColorBgra = 0xC0000000;
        private float _shadowStrength = 1f;
        private float _intensity = 1f;
        private float _falloff = 0.05f;
        private bool _castShadows;

        public override string DisplayName => "Lighting";

        public override string Description => "Preview point-light shading in the voxel viewport.";

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                RaiseChanged();
            }
        }

        public float PositionX
        {
            get => _positionX;
            set
            {
                float clamped = Clamp(value, -512f, 512f);
                if (NearlyEqual(_positionX, clamped)) return;
                _positionX = clamped;
                RaiseChanged();
            }
        }

        public float PositionY
        {
            get => _positionY;
            set
            {
                float clamped = Clamp(value, -512f, 512f);
                if (NearlyEqual(_positionY, clamped)) return;
                _positionY = clamped;
                RaiseChanged();
            }
        }

        public float PositionZ
        {
            get => _positionZ;
            set
            {
                float clamped = Clamp(value, -512f, 512f);
                if (NearlyEqual(_positionZ, clamped)) return;
                _positionZ = clamped;
                RaiseChanged();
            }
        }

        public uint LightColorBgra
        {
            get => _lightColorBgra;
            set
            {
                if (_lightColorBgra == value) return;
                _lightColorBgra = value;
                RaiseChanged();
            }
        }

        public uint ShadowColorBgra
        {
            get => _shadowColorBgra;
            set
            {
                if (_shadowColorBgra == value) return;
                _shadowColorBgra = value;
                RaiseChanged();
            }
        }

        public float Intensity
        {
            get => _intensity;
            set
            {
                float clamped = Clamp(value, 0f, 8f);
                if (NearlyEqual(_intensity, clamped)) return;
                _intensity = clamped;
                RaiseChanged();
            }
        }

        public float ShadowStrength
        {
            get => _shadowStrength;
            set
            {
                float clamped = Clamp(value, 0f, 1f);
                if (NearlyEqual(_shadowStrength, clamped)) return;
                _shadowStrength = clamped;
                RaiseChanged();
            }
        }

        public float Falloff
        {
            get => _falloff;
            set
            {
                float clamped = Clamp(value, 0f, 2f);
                if (NearlyEqual(_falloff, clamped)) return;
                _falloff = clamped;
                RaiseChanged();
            }
        }

        public bool CastShadows
        {
            get => _castShadows;
            set
            {
                if (_castShadows == value) return;
                _castShadows = value;
                RaiseChanged();
            }
        }

        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "enabled",
                "Enable lighting",
                Enabled,
                v => Enabled = v,
                Order: 0,
                Tooltip: "Enable point-light preview shading. Disabled = exact flat face colors.");

            yield return new ColorOption(
                "lightColor",
                "Light color",
                LightColorBgra,
                v => LightColorBgra = v,
                null,
                Order: 1,
                Tooltip: "Tint applied to the diffuse lighting contribution.",
                ShowAlpha: false);

            yield return new ColorOption(
                "shadowColor",
                "Shadow color",
                ShadowColorBgra,
                v => ShadowColorBgra = v,
                null,
                Order: 2,
                Tooltip: "Tint in lower-light regions. Alpha controls shadow tint strength.",
                ShowAlpha: true);

            yield return new SliderOption(
                "shadowStrength",
                "Shadow strength",
                0,
                1,
                ShadowStrength,
                v => ShadowStrength = (float)v,
                Order: 3,
                Step: 0.01,
                Tooltip: "Overall strength of shadow tint and cast-shadow darkening.");

            yield return new SliderOption(
                "intensity",
                "Intensity",
                0,
                4,
                Intensity,
                v => Intensity = (float)v,
                Order: 4,
                Step: 0.05,
                Tooltip: "Diffuse light intensity.");

            yield return new SliderOption(
                "falloff",
                "Falloff",
                0,
                1,
                Falloff,
                v => Falloff = (float)v,
                Order: 5,
                Step: 0.01,
                Tooltip: "Distance attenuation factor.");

            yield return new NumberBoxOption(
                "positionX",
                "Pos X",
                -512,
                512,
                PositionX,
                v => PositionX = (float)v,
                Order: 6,
                Step: 1,
                Width: 88);

            yield return new NumberBoxOption(
                "positionY",
                "Pos Y",
                -512,
                512,
                PositionY,
                v => PositionY = (float)v,
                Order: 7,
                Step: 1,
                Width: 88);

            yield return new NumberBoxOption(
                "positionZ",
                "Pos Z",
                -512,
                512,
                PositionZ,
                v => PositionZ = (float)v,
                Order: 8,
                Step: 1,
                Width: 88);

            yield return new ToggleOption(
                "castShadows",
                "Cast shadows",
                CastShadows,
                v => CastShadows = v,
                Order: 9,
                Tooltip: "Enable hard voxel cast shadows from the point light.");
        }

        public void SetFromSnapshot(VoxelLightingSettings snapshot, bool raiseChanged = false)
        {
            if (snapshot == null) return;

            bool changed =
                _enabled != snapshot.Enabled ||
                !NearlyEqual(_positionX, snapshot.Position.X) ||
                !NearlyEqual(_positionY, snapshot.Position.Y) ||
                !NearlyEqual(_positionZ, snapshot.Position.Z) ||
                _lightColorBgra != snapshot.LightColorBgra ||
                _shadowColorBgra != snapshot.ShadowColorBgra ||
                !NearlyEqual(_shadowStrength, snapshot.ShadowStrength) ||
                !NearlyEqual(_intensity, snapshot.Intensity) ||
                !NearlyEqual(_falloff, snapshot.Falloff) ||
                _castShadows != snapshot.CastShadows;

            _enabled = snapshot.Enabled;
            _positionX = Clamp(snapshot.Position.X, -512f, 512f);
            _positionY = Clamp(snapshot.Position.Y, -512f, 512f);
            _positionZ = Clamp(snapshot.Position.Z, -512f, 512f);
            _lightColorBgra = snapshot.LightColorBgra;
            _shadowColorBgra = snapshot.ShadowColorBgra;
            _shadowStrength = Clamp(snapshot.ShadowStrength, 0f, 1f);
            _intensity = Clamp(snapshot.Intensity, 0f, 8f);
            _falloff = Clamp(snapshot.Falloff, 0f, 2f);
            _castShadows = snapshot.CastShadows;

            if (raiseChanged && changed)
            {
                RaiseChanged();
            }
        }

        public VoxelLightingSettings ToSnapshot()
            => new()
            {
                Enabled = Enabled,
                Position = new Vector3(PositionX, PositionY, PositionZ),
                LightColorBgra = LightColorBgra,
                ShadowColorBgra = ShadowColorBgra,
                ShadowStrength = ShadowStrength,
                Intensity = Intensity,
                Falloff = Falloff,
                CastShadows = CastShadows,
            };

        private static float Clamp(float value, float min, float max)
            => MathF.Min(max, MathF.Max(min, value));

        private static bool NearlyEqual(float a, float b)
            => MathF.Abs(a - b) <= 0.0001f;
    }
}
