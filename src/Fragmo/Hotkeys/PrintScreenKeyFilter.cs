namespace Fragmo.Hotkeys;

/// <summary>
/// Decide o que fazer com cada evento de teclado visto pelo gancho da Print Screen: dispara a captura uma vez
/// por toque e segura a tecla (descida e subida) para o Windows não fazer a cópia clássica da tela.
/// Combinações com Ctrl, Alt, Shift ou Win passam direto, assim como as demais teclas.
/// </summary>
internal sealed class PrintScreenKeyFilter
{
    public const int VkSnapshot = 0x2C;

    private bool _down;

    public readonly record struct Decision(bool Swallow, bool Fire);

    public Decision Handle(int vk, bool keyUp, bool modifierDown)
    {
        if (vk != VkSnapshot)
        {
            return default;
        }

        if (keyUp)
        {
            bool wasOurs = _down;
            _down = false;
            return new Decision(Swallow: wasOurs, Fire: false);
        }

        if (_down)
        {
            return new Decision(Swallow: true, Fire: false); // repetição de tecla segurada
        }

        if (modifierDown)
        {
            return default;
        }

        _down = true;
        return new Decision(Swallow: true, Fire: true);
    }
}
