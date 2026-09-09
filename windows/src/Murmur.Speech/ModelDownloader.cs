using System.Net.Http.Headers;

namespace Murmur.Speech;

/// <summary>One file the model consists of.</summary>
/// <param name="Name">File name inside the model directory.</param>
/// <param name="MinimumBytes">
/// The smallest size a good copy can be. A truncated download is the most common failure
/// and it does not announce itself — a partial encoder fails at load time with an opaque
/// protobuf error — so size is checked before a file is accepted.
/// </param>
public sealed record ModelFile(string Name, long MinimumBytes);

/// <summary>Where a download has got to.</summary>
/// <param name="File">The file being fetched.</param>
/// <param name="FileIndex">Zero-based position in the file list.</param>
/// <param name="FileCount">How many files there are.</param>
/// <param name="BytesReceived">Bytes of this file received so far.</param>
/// <param name="TotalBytes">Size of this file, when the server said.</param>
public sealed record DownloadProgress(
    string File, int FileIndex, int FileCount, long BytesReceived, long? TotalBytes)
{
    /// <summary>Overall fraction complete, 0…1, weighted by the known file sizes.</summary>
    public double Fraction
    {
        get
        {
            var done = ModelDownloader.Files.Take(FileIndex).Sum(f => f.MinimumBytes);
            var total = ModelDownloader.Files.Sum(f => f.MinimumBytes);
            var current = Math.Min(BytesReceived, ModelDownloader.Files[FileIndex].MinimumBytes);
            return Math.Clamp((done + current) / (double)total, 0, 1);
        }
    }
}

/// <summary>
/// Fetches the Parakeet model into the app's data folder.
/// </summary>
/// <remarks>
/// <para>
/// Windows has no built-in speech engine, so a fresh install cannot transcribe until these
/// files exist. Asking a user to follow <c>docs/PARAKEET-WINDOWS.md</c> by hand is fine for
/// a developer and wrong for an app; this is the button.
/// </para>
/// <para>
/// Each file is written to <c>name.part</c> and renamed only once its size clears the
/// minimum, so <see cref="ParakeetTranscriber.IsComplete"/> can never see a half-written
/// file. Files already present and large enough are skipped, which makes a retry resume.
/// </para>
/// <para>
/// The HTTP handler is injectable so the whole flow is tested with a fake server.
/// </para>
/// </remarks>
public sealed class ModelDownloader : IDisposable
{
    /// <summary>The public Hugging Face repository. No token required.</summary>
    public const string Repository = "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v2-int8";

    /// <summary>Where the files are fetched from.</summary>
    public static Uri DefaultBaseUri { get; } =
        new($"https://huggingface.co/{Repository}/resolve/main/");

    /// <summary>The files, smallest first so a failure shows up before 600 MB is spent.</summary>
    public static IReadOnlyList<ModelFile> Files { get; } =
    [
        new("tokens.txt", 8_000),
        new("joiner.int8.onnx", 1_700_000),
        new("decoder.int8.onnx", 7_000_000),
        new("encoder.int8.onnx", 640_000_000),
    ];

    /// <summary>Total download, for display.</summary>
    public static long ApproximateBytes => Files.Sum(f => f.MinimumBytes);

    /// <summary>Where a download lands by default: the first search path.</summary>
    public static string DefaultTarget => ParakeetTranscriber.DefaultSearchPaths().First();

    private const int BufferSize = 128 * 1024;

    private readonly HttpClient _http;
    private readonly Uri _base;

    /// <summary>Creates a downloader against the real repository.</summary>
    public ModelDownloader() : this(null, null) { }

    /// <summary>Creates a downloader with a custom transport and origin, for tests.</summary>
    public ModelDownloader(HttpMessageHandler? handler, Uri? baseUri)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = Timeout.InfiniteTimeSpan;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Acapella", "1.0"));
        _base = baseUri ?? DefaultBaseUri;
    }

    /// <summary>Whether every file in <paramref name="directory"/> is present and large enough.</summary>
    public static bool IsComplete(string directory) => Files.All(f => IsGood(directory, f));

    /// <summary>Downloads whatever is missing into <paramref name="directory"/>.</summary>
    /// <param name="directory">Target folder; created if needed.</param>
    /// <param name="progress">Receives updates roughly every 128 KB.</param>
    /// <param name="cancellationToken">Cancels; a partial <c>.part</c> file is deleted.</param>
    /// <exception cref="HttpRequestException">The server refused or the connection dropped.</exception>
    /// <exception cref="IOException">A file came back smaller than a good copy can be.</exception>
    public async Task DownloadAsync(
        string directory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);

        for (var index = 0; index < Files.Count; index++)
        {
            var file = Files[index];
            if (IsGood(directory, file))
            {
                progress?.Report(new DownloadProgress(file.Name, index, Files.Count, file.MinimumBytes, file.MinimumBytes));
                continue;
            }

            await FetchAsync(directory, file, index, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FetchAsync(
        string directory,
        ModelFile file,
        int index,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var final = Path.Combine(directory, file.Name);
        var partial = final + ".part";

        using var response = await _http
            .GetAsync(new Uri(_base, file.Name), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        long received = 0;

        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var sink = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await sink.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new DownloadProgress(file.Name, index, Files.Count, received, total));
                }
            }

            if (received < file.MinimumBytes)
            {
                throw new IOException(
                    $"{file.Name} came back as {received:N0} bytes; a good copy is at least {file.MinimumBytes:N0}. "
                    + "The download was cut short — try again.");
            }

            File.Move(partial, final, overwrite: true);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    private static bool IsGood(string directory, ModelFile file)
    {
        var path = Path.Combine(directory, file.Name);
        return File.Exists(path) && new FileInfo(path).Length >= file.MinimumBytes;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
