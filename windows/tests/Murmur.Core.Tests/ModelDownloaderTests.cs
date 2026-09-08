using Xunit;
using System.Net;
using Murmur.Speech;
using Shouldly;

namespace Murmur.CoreTests;

/// <summary>
/// The in-app model download, against a fake server. The real one is 660 MB from Hugging
/// Face; what matters here is the part-file discipline and the size check.
/// </summary>
public sealed class ModelDownloaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"murmur-dl-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { /* best effort */ }
    }

    [Fact]
    public async Task Downloads_every_file_and_reports_progress()
    {
        var sizes = ModelDownloader.Files.ToDictionary(f => f.Name, f => f.MinimumBytes + 10);
        using var downloader = new ModelDownloader(new FakeServer(sizes), new Uri("https://example.test/model/"));
        var reports = new List<DownloadProgress>();

        await downloader.DownloadAsync(_directory, new SyncProgress(reports), CancellationToken.None);

        ModelDownloader.IsComplete(_directory).ShouldBeTrue();
        ParakeetTranscriber.IsComplete(_directory).ShouldBeTrue();
        Directory.GetFiles(_directory, "*.part").ShouldBeEmpty();
        reports.Last().Fraction.ShouldBe(1, 0.001);
        reports.Select(r => r.File).Distinct().Count().ShouldBe(ModelDownloader.Files.Count);
    }

    [Fact]
    public async Task A_short_file_is_rejected_and_never_renamed_into_place()
    {
        var sizes = ModelDownloader.Files.ToDictionary(f => f.Name, f => f.MinimumBytes + 10);
        sizes["encoder.int8.onnx"] = 1000;   // truncated
        using var downloader = new ModelDownloader(new FakeServer(sizes), new Uri("https://example.test/model/"));

        await Should.ThrowAsync<IOException>(() => downloader.DownloadAsync(_directory, null, CancellationToken.None));

        File.Exists(Path.Combine(_directory, "encoder.int8.onnx")).ShouldBeFalse();
        File.Exists(Path.Combine(_directory, "encoder.int8.onnx.part")).ShouldBeFalse();
        // The small files that came first are kept, so a retry resumes.
        File.Exists(Path.Combine(_directory, "tokens.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task Files_already_present_are_not_fetched_again()
    {
        Directory.CreateDirectory(_directory);
        foreach (var file in ModelDownloader.Files)
        {
            File.WriteAllBytes(Path.Combine(_directory, file.Name), new byte[file.MinimumBytes]);
        }

        var server = new FakeServer(new Dictionary<string, long>());
        using var downloader = new ModelDownloader(server, new Uri("https://example.test/model/"));
        await downloader.DownloadAsync(_directory, null, CancellationToken.None);

        server.Requests.ShouldBe(0);
    }

    [Fact]
    public void Progress_fraction_weights_by_file_size()
    {
        var first = new DownloadProgress("tokens.txt", 0, 4, 0, null);
        first.Fraction.ShouldBe(0, 0.001);

        var lastDone = new DownloadProgress("encoder.int8.onnx", 3, 4, ModelDownloader.Files[3].MinimumBytes, null);
        lastDone.Fraction.ShouldBe(1, 0.001);

        var halfway = new DownloadProgress("encoder.int8.onnx", 3, 4, ModelDownloader.Files[3].MinimumBytes / 2, null);
        halfway.Fraction.ShouldBeInRange(0.45, 0.55);
    }

    /// <summary>Serves each named file as that many zero bytes.</summary>
    private sealed class FakeServer(IReadOnlyDictionary<string, long> sizes) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var name = request.RequestUri!.Segments[^1];
            if (!sizes.TryGetValue(name, out var size))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ZeroStream(size)),
            };
            response.Content.Headers.ContentLength = size;
            return Task.FromResult(response);
        }
    }

    /// <summary>A read-only stream of zeros, so a 640 MB "file" costs nothing.</summary>
    private sealed class ZeroStream(long length) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - _position);
            if (n <= 0) return 0;
            Array.Clear(buffer, offset, n);
            _position += n;
            return n;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Progress that reports on the calling thread, so tests see every update.</summary>
    private sealed class SyncProgress(List<DownloadProgress> sink) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => sink.Add(value);
    }
}
