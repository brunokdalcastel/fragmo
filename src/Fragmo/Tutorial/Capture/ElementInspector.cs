using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using UiaPoint = System.Windows.Point;

namespace Fragmo.Tutorial.Capture;

/// <summary>
/// Único arquivo que toca UI Automation (e o namespace System.Windows). As chamadas rodam numa thread MTA própria,
/// separada da UI, uma de cada vez; cada pedido tem limite de 500 ms e devolve <c>null</c> se estourar ou falhar.
/// </summary>
internal sealed class ElementInspector : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);

    private readonly BlockingCollection<Request> _queue = [];
    private readonly Thread _thread;
    private bool _disposed;

    private sealed record Request(Func<ElementInfo?> Work, TaskCompletionSource<ElementInfo?> Completion, long EnqueuedAt);

    public ElementInspector()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = nameof(ElementInspector) };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    /// <summary>Elemento sob o ponto (pixels físicos); <c>null</c> se não houver, se for do Fragmo ou se demorar.</summary>
    public Task<ElementInfo?> FromPointAsync(Point screenPoint) =>
        EnqueueAsync(() => Describe(AutomationElement.FromPoint(new UiaPoint(screenPoint.X, screenPoint.Y))));

    /// <summary>Elemento com o foco do teclado; mesmas regras de <see cref="FromPointAsync"/>.</summary>
    public Task<ElementInfo?> FocusedAsync() => EnqueueAsync(() => Describe(AutomationElement.FocusedElement));

    private async Task<ElementInfo?> EnqueueAsync(Func<ElementInfo?> work)
    {
        if (_disposed)
        {
            return null;
        }
        var completion = new TaskCompletionSource<ElementInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _queue.Add(new Request(work, completion, Stopwatch.GetTimestamp()));
        }
        catch (InvalidOperationException)
        {
            return null; // fila fechada pelo Dispose
        }
        Task finished = await Task.WhenAny(completion.Task, Task.Delay(Timeout)).ConfigureAwait(false);
        return finished == completion.Task ? await completion.Task.ConfigureAwait(false) : null;
    }

    private void Run()
    {
        foreach (Request request in _queue.GetConsumingEnumerable())
        {
            // Pedido que já estourou o limite esperando na fila: quem pediu já recebeu null.
            if (Stopwatch.GetElapsedTime(request.EnqueuedAt) > Timeout)
            {
                request.Completion.TrySetResult(null);
                continue;
            }
            try
            {
                request.Completion.TrySetResult(request.Work());
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException
                                           or ArgumentException or TimeoutException)
            {
                Debug.WriteLine($"ElementInspector: {ex.GetType().Name}: {ex.Message}");
                request.Completion.TrySetResult(null);
            }
        }
    }

    private static ElementInfo? Describe(AutomationElement? element)
    {
        if (element is null)
        {
            return null;
        }
        AutomationElement.AutomationElementInformation current = element.Current;
        if (current.ProcessId == Environment.ProcessId)
        {
            return null;
        }

        System.Windows.Rect r = current.BoundingRectangle;
        Rectangle bounds = r.IsEmpty || double.IsInfinity(r.Width) || double.IsInfinity(r.Height)
            ? Rectangle.Empty
            : Rectangle.FromLTRB((int)Math.Round(r.Left), (int)Math.Round(r.Top), (int)Math.Round(r.Right), (int)Math.Round(r.Bottom));

        bool? toggled = null;
        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out object pattern) && pattern is TogglePattern toggle)
        {
            toggled = toggle.Current.ToggleState switch
            {
                ToggleState.On => true,
                ToggleState.Off => false,
                _ => null,
            };
        }

        return new ElementInfo(current.Name ?? string.Empty, MapKind(current.ControlType), bounds, current.IsPassword, toggled);
    }

    private static ElementKind MapKind(ControlType? type)
    {
        if (type is null)
        {
            return ElementKind.Other;
        }
        if (type == ControlType.Button || type == ControlType.SplitButton)
        {
            return ElementKind.Button;
        }
        if (type == ControlType.MenuItem)
        {
            return ElementKind.MenuItem;
        }
        if (type == ControlType.TabItem)
        {
            return ElementKind.Tab;
        }
        if (type == ControlType.Hyperlink)
        {
            return ElementKind.Link;
        }
        if (type == ControlType.CheckBox)
        {
            return ElementKind.CheckBox;
        }
        if (type == ControlType.RadioButton)
        {
            return ElementKind.RadioButton;
        }
        if (type == ControlType.Edit || type == ControlType.Document)
        {
            return ElementKind.Edit;
        }
        if (type == ControlType.ListItem || type == ControlType.DataItem)
        {
            return ElementKind.ListItem;
        }
        if (type == ControlType.TreeItem)
        {
            return ElementKind.TreeItem;
        }
        if (type == ControlType.ComboBox)
        {
            return ElementKind.ComboBox;
        }
        return ElementKind.Other;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }
}
