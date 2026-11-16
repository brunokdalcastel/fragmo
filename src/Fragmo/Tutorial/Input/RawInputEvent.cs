using Fragmo.Hotkeys;

namespace Fragmo.Tutorial.Input;

internal enum RawInputKind { LeftDown, RightDown, KeyDown }

/// <summary>
/// Um evento cru vindo dos ganchos. <see cref="ScreenPoint"/> em pixels físicos. Não existe caractere aqui:
/// o conteúdo digitado nunca fica em memória além do código da tecla.
/// </summary>
internal readonly record struct RawInputEvent(RawInputKind Kind, Point ScreenPoint, int VirtualKey, HotkeyModifiers Modifiers, uint TimeMs);
