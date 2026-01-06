// Global using aliases to redirect Core types to SDK types for dogfooding
// This allows Core code to continue using Core namespace paths while actually using SDK types

// Enums shared between SDK and Core
global using BrushShape = PixlPunkt.PluginSdk.Imaging.BrushShape;
global using ButtonOption = PixlPunkt.PluginSdk.Settings.Options.ButtonOption;
global using ColorOption = PixlPunkt.PluginSdk.Settings.Options.ColorOption;
global using ColorPickerWindowOption = PixlPunkt.PluginSdk.Settings.Options.ColorPickerWindowOption;
global using CustomBrushOption = PixlPunkt.PluginSdk.Settings.Options.CustomBrushOption;
global using DropdownOption = PixlPunkt.PluginSdk.Settings.Options.DropdownOption;
global using DynamicLabelOption = PixlPunkt.PluginSdk.Settings.Options.DynamicLabelOption;
global using EffectCategory = PixlPunkt.PluginSdk.Effects.EffectCategory;
global using ExportBuilders = PixlPunkt.PluginSdk.IO.Builders.ExportBuilders;
global using ExportCategory = PixlPunkt.PluginSdk.IO.ExportCategory;
global using GradientPickerWindowOption = PixlPunkt.PluginSdk.Settings.Options.GradientPickerWindowOption;
global using HueSliderOption = PixlPunkt.PluginSdk.Settings.Options.HueSliderOption;
global using IBrushLikeSettings = PixlPunkt.PluginSdk.Settings.IBrushLikeSettings;
global using IconButtonOption = PixlPunkt.PluginSdk.Settings.Options.IconButtonOption;
global using IconOption = PixlPunkt.PluginSdk.Settings.Options.IconOption;
global using IconToggleOption = PixlPunkt.PluginSdk.Settings.Options.IconToggleOption;
global using IDensitySettings = PixlPunkt.PluginSdk.Settings.IDensitySettings;
// Effect registration - Core uses SDK interface
global using IEffectRegistration = PixlPunkt.PluginSdk.Effects.IEffectRegistration;
global using IExportContext = PixlPunkt.PluginSdk.IO.IExportContext;
global using IExportRegistration = PixlPunkt.PluginSdk.IO.IExportRegistration;
global using IImageExportData = PixlPunkt.PluginSdk.IO.IImageExportData;
global using IImportContext = PixlPunkt.PluginSdk.IO.IImportContext;
// Plugin window descriptor for SDK windows

// Import/Export types - Core uses SDK types for dogfooding
global using IImportRegistration = PixlPunkt.PluginSdk.IO.IImportRegistration;
global using ImageImportResult = PixlPunkt.PluginSdk.IO.ImageImportResult;
// Import/Export builders - Core uses SDK fluent builders
global using ImportBuilders = PixlPunkt.PluginSdk.IO.Builders.ImportBuilders;
global using ImportCategory = PixlPunkt.PluginSdk.IO.ImportCategory;
global using IOpacitySettings = PixlPunkt.PluginSdk.Settings.IOpacitySettings;
global using IPaletteExportData = PixlPunkt.PluginSdk.IO.IPaletteExportData;
// Shape builder interface - Core uses SDK type
global using IShapeBuilder = PixlPunkt.PluginSdk.Shapes.IShapeBuilder;
// Settings interfaces - Core uses SDK interface types
global using IStrokeSettings = PixlPunkt.PluginSdk.Settings.IStrokeSettings;
// Custom brush settings interface - Core uses SDK type
global using ICustomBrushSettings = PixlPunkt.PluginSdk.Settings.ICustomBrushSettings;
// Tool options - Core uses SDK option types
global using IToolOption = PixlPunkt.PluginSdk.Settings.IToolOption;
// Key binding for tool shortcuts - uses SDK type but VirtualKey remains Windows.System.VirtualKey
// because UI code extensively uses Windows.System.VirtualKey for keyboard handling
global using KeyBinding = PixlPunkt.PluginSdk.Settings.KeyBinding;
global using LabelOption = PixlPunkt.PluginSdk.Settings.Options.LabelOption;
// Base classes - Core uses SDK implementations
global using LayerEffectBase = PixlPunkt.PluginSdk.Compositing.LayerEffectBase;
global using NumberBoxOption = PixlPunkt.PluginSdk.Settings.Options.NumberBoxOption;
global using PaletteImportResult = PixlPunkt.PluginSdk.IO.PaletteImportResult;
global using PaletteOption = PixlPunkt.PluginSdk.Settings.Options.PaletteOption;
global using PluginWindowOption = PixlPunkt.PluginSdk.Settings.Options.PluginWindowOption;
global using SeparatorOption = PixlPunkt.PluginSdk.Settings.Options.SeparatorOption;
global using ShapeOption = PixlPunkt.PluginSdk.Settings.Options.ShapeOption;
global using SliderOption = PixlPunkt.PluginSdk.Settings.Options.SliderOption;
global using ToggleOption = PixlPunkt.PluginSdk.Settings.Options.ToggleOption;
global using ToolCategory = PixlPunkt.PluginSdk.Enums.ToolCategory;
global using ToolInputPattern = PixlPunkt.PluginSdk.Enums.ToolInputPattern;
global using ToolOverlayStyle = PixlPunkt.PluginSdk.Enums.ToolOverlayStyle;

// Constants - Core uses SDK constants for single source of truth
global using ToolLimits = PixlPunkt.PluginSdk.Constants.ToolLimits;
global using EffectLimits = PixlPunkt.PluginSdk.Constants.EffectLimits;
global using ColorConstants = PixlPunkt.PluginSdk.Constants.ColorConstants;
global using MathConstants = PixlPunkt.PluginSdk.Constants.MathConstants;

// Graphics struct helpers for Uno Platform (uses object initializers instead of constructors)
global using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
