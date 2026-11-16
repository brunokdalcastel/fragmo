namespace Fragmo.Tutorial.Capture;

/// <summary>O que a UI Automation disse sobre o elemento. <see cref="ScreenBounds"/> em pixels físicos.</summary>
internal sealed record ElementInfo(string Name, ElementKind Kind, Rectangle ScreenBounds, bool IsPassword, bool? IsToggledOn);
