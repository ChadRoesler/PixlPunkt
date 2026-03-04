using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Voxel.Tools.Builders
{
    /// <summary>Shared fluent builder logic for voxel tools.</summary>
    public abstract class VoxelToolBuilderBase<TBuilder> where TBuilder : VoxelToolBuilderBase<TBuilder>
    {
        private readonly VoxelToolCategory _category;
        private string? _displayName;
        private ToolSettingsBase? _settings;
        private IVoxelToolBehavior? _behavior;
        private Func<IVoxelToolContext, IVoxelToolHandler>? _handlerFactory;

        protected VoxelToolBuilderBase(string id, VoxelToolCategory category)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Tool id is required.", nameof(id));

            Id = id;
            _category = category;
        }

        protected string Id { get; }
        protected string EffectiveDisplayName => _displayName ?? Id;

        protected TBuilder This => (TBuilder)this;

        public TBuilder WithDisplayName(string displayName)
        {
            _displayName = displayName;
            return This;
        }

        public TBuilder WithSettings(ToolSettingsBase? settings)
        {
            _settings = settings;
            return This;
        }

        public TBuilder WithBehavior(IVoxelToolBehavior behavior)
        {
            _behavior = behavior;
            return This;
        }

        public TBuilder WithHandler(Func<IVoxelToolContext, IVoxelToolHandler> handlerFactory)
        {
            _handlerFactory = handlerFactory;
            return This;
        }

        protected VoxelToolRegistration BuildCore(VoxelToolBehavior defaultBehavior)
        {
            if (_handlerFactory == null)
                throw new InvalidOperationException("Voxel tool requires a handler. Call WithHandler() before Build().");

            IVoxelToolBehavior behavior = _behavior ?? defaultBehavior;
            return new VoxelToolRegistration(
                Id,
                EffectiveDisplayName,
                _category,
                _settings,
                behavior,
                _handlerFactory);
        }

        public abstract VoxelToolRegistration Build();
    }
}
