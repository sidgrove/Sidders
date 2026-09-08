using Murmur.Abstractions;

namespace Murmur.Core;

/// <summary>
/// A transcriber that can find its model after the app has started.
/// </summary>
/// <remarks>
/// <para>
/// On Windows the model is not there on first launch — it is downloaded from Settings while
/// the app runs. Constructing the real engine once at startup would mean "download, then
/// restart", which is not how a real app behaves. This wrapper asks <c>locate</c> for a
/// model directory each time it needs one, builds the engine on demand, and hands over.
/// </para>
/// <para>
/// Platform-neutral and tested with fakes: the locate and create steps are injected.
/// </para>
/// </remarks>
public sealed class ReloadableTranscriber : ITranscriber
{
    private readonly Func<string?> _locate;
    private readonly Func<string, ITranscriber> _create;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ITranscriber? _inner;

    /// <summary>Creates a transcriber that resolves its model lazily.</summary>
    /// <param name="locate">Returns a directory holding a complete model, or null.</param>
    /// <param name="create">Builds the real engine for a directory.</param>
    public ReloadableTranscriber(Func<string?> locate, Func<string, ITranscriber> create)
    {
        _locate = locate;
        _create = create;
    }

    /// <summary>The directory the loaded model came from, or null.</summary>
    public string? ModelDirectory { get; private set; }

    /// <inheritdoc />
    public bool IsReady => _inner?.IsReady == true;

    /// <inheritdoc />
    public async ValueTask<bool> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_inner?.IsReady == true) return true;

            var directory = _locate();
            if (directory is null) return false;

            _inner ??= _create(directory);
            ModelDirectory = directory;

            // Loading is CPU-bound and takes seconds; keep it off whichever thread asked.
            return await Task.Run(() => _inner.LoadAsync(cancellationToken).AsTask(), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<string> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        IReadOnlyList<string> biasPhrases,
        CancellationToken cancellationToken)
    {
        if (!IsReady && !await LoadAsync(cancellationToken).ConfigureAwait(false)) return string.Empty;

        return await Task.Run(
                () => _inner!.TranscribeAsync(samples, biasPhrases, cancellationToken).AsTask(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_inner is not null) await _inner.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
