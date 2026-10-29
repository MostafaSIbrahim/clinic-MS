using System.Text;
using Microsoft.AspNetCore.Http;
using SafyaClinic.Web.Services.Files;
using Xunit;

namespace SafyaClinic.Tests.Web.Files;

/// <summary>S3 / F01, F17: allow-list, content check, size limit, old paths, no escape from storage.</summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 test");

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"safya-files-{Guid.NewGuid()}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LocalFileStorage Storage(long maxBytes = 1024) => new(_root, maxBytes);

    private static IFormFile Upload(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    private string PlaceFile(string area, string name)
    {
        var path = Path.Combine(_root, area, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Png);
        return path;
    }

    private int FilesOnDisk() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length : 0;

    [Fact]
    public async Task Valid_png_is_saved_under_its_area_with_a_new_name()
    {
        var (stored, error) = await Storage().SaveAsync(Upload("scan.PNG", Png), LocalFileStorage.Analysis);

        Assert.Null(error);
        Assert.NotNull(stored);
        Assert.StartsWith("analysis/", stored!.Key);
        Assert.EndsWith(".png", stored.Key);
        Assert.Equal("image/png", stored.ContentType);
        Assert.True(File.Exists(Path.Combine(_root, "analysis", stored.FileName)));
    }

    [Fact]
    public async Task Valid_pdf_is_accepted()
    {
        var (stored, _) = await Storage().SaveAsync(Upload("result.pdf", Pdf), LocalFileStorage.Prescriptions);

        Assert.Equal("application/pdf", stored!.ContentType);
    }

    [Theory]
    [InlineData("page.html")]
    [InlineData("logo.svg")]
    [InlineData("tool.exe")]
    public async Task Disallowed_types_are_rejected_and_nothing_is_written(string name)
    {
        var (stored, error) = await Storage().SaveAsync(Upload(name, Png), LocalFileStorage.Analysis);

        Assert.Null(stored);
        Assert.NotNull(error);
        Assert.Equal(0, FilesOnDisk());
    }

    [Fact]
    public async Task Content_that_does_not_match_the_extension_is_rejected()
    {
        var html = Encoding.ASCII.GetBytes("<html><script>alert(1)</script></html>");

        var (stored, error) = await Storage().SaveAsync(Upload("photo.png", html), LocalFileStorage.Analysis);

        Assert.Null(stored);
        Assert.NotNull(error);
        Assert.Equal(0, FilesOnDisk());
    }

    [Fact]
    public async Task Files_over_the_limit_are_rejected()
    {
        var (stored, error) = await Storage(maxBytes: 5).SaveAsync(Upload("scan.png", Png), LocalFileStorage.Analysis);

        Assert.Null(stored);
        Assert.Contains("too large", error);
    }

    [Theory]
    [InlineData(@"wwwroot/uploads\analysis\old.jpeg")]
    [InlineData(@"D:\sites\safya\wwwroot\uploads\analysis\old.jpeg")]
    [InlineData("analysis/old.jpeg")]
    public void Old_and_new_stored_paths_find_the_same_file(string storedPath)
    {
        var expected = PlaceFile("analysis", "old.jpeg");

        var file = Storage().Find(storedPath);

        Assert.NotNull(file);
        Assert.Equal(Path.GetFullPath(expected), file!.FullPath);
        Assert.True(file.Inline);
        Assert.Equal("image/jpeg", file.ContentType);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("analysis/..")]
    [InlineData("secret.txt")]
    public void Paths_outside_storage_are_never_resolved(string storedPath)
    {
        Assert.Null(Storage().Find(storedPath));
    }

    [Fact]
    public void Old_file_of_another_type_is_download_only()
    {
        PlaceFile("prescriptions", "old.docx");

        var file = Storage().Find("prescriptions/old.docx");

        Assert.False(file!.Inline);
        Assert.Equal("application/octet-stream", file.ContentType);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        var path = PlaceFile("analysis", "gone.png");

        Storage().Delete("analysis/gone.png");

        Assert.False(File.Exists(path));
    }
}