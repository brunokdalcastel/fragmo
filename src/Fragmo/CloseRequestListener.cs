namespace Fragmo;

/// <summary>
/// Escuta o pedido de fechamento que o instalador envia (evento nomeado do Windows) e repassa
/// para a thread da interface. Assim uma atualização ou desinstalação fecha o Fragmo normalmente,
/// em vez de encerrar o processo à força.
/// </summary>
internal sealed class CloseRequestListener : IDisposable
{
    /// <summary>Mesmo nome usado pelo instalador (installer/Fragmo.iss).</summary>
    public const string EventName = @"Local\Fragmo.CloseRequest";

    private readonly EventWaitHandle _event = new(false, EventResetMode.AutoReset, EventName);
    private readonly WindowsFormsSynchronizationContext _ui = new();
    private readonly RegisteredWaitHandle _wait;

    /// <param name="onRequest">Chamado na thread da interface a cada pedido.</param>
    public CloseRequestListener(Action onRequest)
    {
        _wait = ThreadPool.RegisterWaitForSingleObject(_event,
            (_, _) => _ui.Post(_ => onRequest(), null),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _wait.Unregister(null);
        _event.Dispose();
        _ui.Dispose();
    }
}
