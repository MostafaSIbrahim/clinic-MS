# Slice S3 — Secure attachment storage

Roadmap item **S3** (Group 1). Fixes **F01** (Critical) and **F17**.

| | |
|---|---|
| Branch | `chore/s3-attachments` (from an up-to-date `main`, after the S4 PR is merged) |
| Behaviour change | 1. Attachment files are no longer reachable at `/uploads/...` without signing in (404). 2. Only **PDF, JPG and PNG** can be attached, up to the configured size (10 MB); anything else gets a clear message. 3. Deleting an attachment also deletes its file. 4. If saving the record fails, the uploaded file is removed. Viewing and downloading existing attachments works as before |
| Database | **No database changes and no data writes.** Existing rows keep their stored paths; the new code understands both old and new path formats |
| Size | M |

## Background

- Files are written to `wwwroot/uploads/...`, and everything under `wwwroot` is served to anyone (F01 — verified: an analysis image downloaded with no sign-in).
- Any file type is accepted and served back with the content type the *browser* claimed. An uploaded HTML or SVG file would run inside the app (stored cross-site scripting).
- `FileStorage:MaxFileSizeBytes` exists but is never used. Deleting an attachment leaves the file behind. The file is saved before the database row, and a failed insert is ignored.
- One upload file is committed to git (`wwwroot/uploads/analysis/ace36a35-….jpeg`).

## Design

| Decision | Choice |
|---|---|
| Where files live | `App_Data/uploads/<area>/<file>` under the app's content root (`FileStorage:BasePath`, default `App_Data/uploads`; an absolute path is also accepted). Not under `wwwroot`, so never served as static files. IIS also refuses to serve `App_Data`, and Web Deploy leaves it alone by default |
| What the database stores for new files | A short key such as `analysis/9f3c….pdf` |
| Old rows | Stored paths like `wwwroot/uploads\analysis\x.jpeg` or `D:\…\uploads\prescriptions\x.jpeg` are mapped to the same key (area folder + file name). So moving the files is enough — no SQL update |
| Allowed types | `.pdf`, `.jpg`/`.jpeg`, `.png`, checked by extension **and** by the file's first bytes. DOCX is left out for now (it cannot be previewed; add it later if needed) |
| Serving | Only through the existing `ViewAttachment` / `DownloadAttachment` actions (role checks unchanged). Content type comes from the server's allow-list, never from the browser; `X-Content-Type-Options: nosniff`; any old file with another type is downloaded, never shown inline |
| Safety net | A tiny middleware returns 404 for any `/uploads/...` URL, even if old files are left in `wwwroot` |

## Scope

1. New `IFileStorage` / `LocalFileStorage` (validation, save, find, delete).
2. Analysis and prescription upload, view, download and delete use it.
3. Block `/uploads/...`; `App_Data` storage path in settings.
4. Upload inputs suggest the allowed types.
5. Move the existing local files; untrack the committed upload; ignore both folders in git.
6. 15 storage tests.

Out of scope: who may upload/delete (unchanged; becomes a permission in S7/P-phase), the `ContentType` column length difference (M-phase), soft-delete, lab workflow (E1).

## Affected files

| Action | Path |
|---|---|
| Add | `SafyaClinic/SafyaClinic.Web/Services/Files/LocalFileStorage.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Program.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/BaseController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/AnalysisController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Controllers/MedicalRecordsController.cs` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/Analysis/Details.cshtml` |
| Edit | `SafyaClinic/SafyaClinic.Web/Views/MedicalRecords/PrescriptionDetails.cshtml` |
| Edit | `SafyaClinic/SafyaClinic.Web/appsettings.json`, `SafyaClinic/docs/demo-appsettings.template.json` |
| Edit | `.gitignore` |
| Remove from git | `SafyaClinic/SafyaClinic.Web/wwwroot/uploads/analysis/ace36a35-3433-44b9-afc4-0fc6b41b49bd.jpeg` |
| Add | `SafyaClinic/tests/SafyaClinic.Tests/Web/Files/LocalFileStorageTests.cs` |
| Add | `Docs/implementation/S3-secure-attachment-storage.md` |

## Risks

| Risk | Mitigation |
|---|---|
| Existing attachments stop opening | Old paths are mapped to the new location (tests cover relative, absolute and new formats); manual check on an existing attachment |
| Files on the test/demo servers stay in `wwwroot/uploads` | They are blocked (404) immediately after publishing; the *Hosted servers* section moves them so they open again |
| The host cannot write to `App_Data` | Check one upload on the test site right after publishing, before the demo site |
| Someone relies on uploading other types (Word, etc.) | The message names the allowed types; DOCX can be added in one line if needed |

## Rollback

Revert the PR's merge commit, then copy the files back from `App_Data/uploads` to `wwwroot/uploads` (locally and on any server already moved). Nothing in the database changes, so no data rollback is needed.

---

## Steps

PowerShell, from `D:\MyDevWork\clinic-MS`.

### Step 0 — Branch

```powershell
git switch main
git pull
git switch -c chore/s3-attachments
```

### Step 1 — The storage service

Create `SafyaClinic/SafyaClinic.Web/Services/Files/LocalFileStorage.cs`:

```csharp
namespace SafyaClinic.Web.Services.Files;

/// <summary>A file saved by <see cref="IFileStorage"/>; <c>Key</c> is what the database stores.</summary>
public sealed record StoredFile(string Key, string FileName, string ContentType, long Length);

/// <summary>A stored file located on disk and ready to stream.</summary>
public sealed record FileToServe(string FullPath, string ContentType, bool Inline, string DownloadName);

public interface IFileStorage
{
    /// <summary>Validates and saves an upload. Returns the stored file, or a message for the user.</summary>
    Task<(StoredFile? File, string? Error)> SaveAsync(IFormFile? file, string area);

    /// <summary>Finds a file by its key or by an old-style stored path. Null if unknown, missing or outside storage.</summary>
    FileToServe? Find(string? storedPath);

    /// <summary>Deletes the file if it exists.</summary>
    void Delete(string? storedPath);
}

/// <summary>
/// Attachments on local disk, outside wwwroot (slice S3).
/// Only PDF, JPEG and PNG are accepted, checked by extension and by the file's first bytes.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    public const string Analysis = "analysis";
    public const string Prescriptions = "prescriptions";
    private static readonly string[] Areas = { Analysis, Prescriptions };

    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
            [".png"] = ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            [".jpg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
            [".jpeg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        };

    private readonly string _root;
    private readonly long _maxBytes;

    public LocalFileStorage(string rootPath, long maxFileSizeBytes)
    {
        _root = Path.GetFullPath(rootPath);
        _maxBytes = maxFileSizeBytes;
    }

    public async Task<(StoredFile? File, string? Error)> SaveAsync(IFormFile? file, string area)
    {
        if (!Areas.Contains(area))
            throw new ArgumentException($"Unknown attachment area '{area}'.", nameof(area));

        if (file is null || file.Length == 0)
            return (null, "No file selected.");

        if (file.Length > _maxBytes)
            return (null, $"The file is too large. The limit is {_maxBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(extension, out var rule))
            return (null, "Only PDF, JPG and PNG files can be attached.");

        var header = new byte[rule.Signature.Length];
        await using (var probe = file.OpenReadStream())
        {
            var read = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            if (read < header.Length || !header.AsSpan().SequenceEqual(rule.Signature))
                return (null, "The file's content does not match its type. Only real PDF, JPG and PNG files can be attached.");
        }

        var fileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var key = $"{area}/{fileName}";
        var fullPath = FullPathFor(key)!;

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var target = new FileStream(fullPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(target);
        }

        return (new StoredFile(key, fileName, rule.ContentType, file.Length), null);
    }

    public FileToServe? Find(string? storedPath)
    {
        var fullPath = FullPathFor(ToKey(storedPath));
        if (fullPath is null || !File.Exists(fullPath)) return null;

        var name = Path.GetFileName(fullPath);
        return Allowed.TryGetValue(Path.GetExtension(name), out var rule)
            ? new FileToServe(fullPath, rule.ContentType, Inline: true, name)
            : new FileToServe(fullPath, "application/octet-stream", Inline: false, name);
    }

    public void Delete(string? storedPath)
    {
        var fullPath = FullPathFor(ToKey(storedPath));
        if (fullPath is not null && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// "analysis/x.pdf", "wwwroot/uploads\analysis\x.pdf" and "D:\site\wwwroot\uploads\analysis\x.pdf"
    /// all become "analysis/x.pdf". Anything that is not area + file name gives null.
    /// </summary>
    public static string? ToKey(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;

        var parts = storedPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var area = parts[^2].ToLowerInvariant();
        return Areas.Contains(area) ? $"{area}/{parts[^1]}" : null;
    }

    private string? FullPathFor(string? key)
    {
        if (key is null) return null;

        var parts = key.Split('/');
        var name = parts[1];
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;

        var fullPath = Path.GetFullPath(Path.Combine(_root, parts[0], name));
        return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}
```

### Step 2 — Register it and block `/uploads` (`Program.cs`)

**2a.** Add at the top with the other `using` lines:

```csharp
using SafyaClinic.Web.Services.Files;
```

**2b.** Directly **after** the last `.AddPolicy(...)` line (the `ClinicalStaff` policy, ending with `;`), add:

```csharp

// ── Attachment storage (S3): outside wwwroot, never served statically ──
var storageRoot = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["FileStorage:BasePath"] ?? "App_Data/uploads");
var maxUploadBytes = builder.Configuration.GetValue<long?>("FileStorage:MaxFileSizeBytes") ?? 10 * 1024 * 1024;
builder.Services.AddSingleton<IFileStorage>(new LocalFileStorage(storageRoot, maxUploadBytes));
```

**2c.** In the pipeline, replace:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();
```

with:

```csharp
app.UseHttpsRedirection();

// The old upload folder must never be served, even if files are left behind (S3).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseStaticFiles();
```

### Step 3 — One way to send a file (`BaseController.cs`)

**3a.** Add with the `using` lines at the top:

```csharp
using SafyaClinic.Web.Services.Files;
```

**3b.** Inside the class, after the `Warning(...)` helper, add:

```csharp
        // ── Attachments (S3) ────────────────────────────────────

        // Streams a stored attachment. Only PDF/JPG/PNG are shown inline;
        // anything else is downloaded. nosniff stops browsers guessing a type.
        protected IActionResult AttachmentResult(FileToServe? file, bool download)
        {
            if (file is null) return NotFound();

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return download || !file.Inline
                ? PhysicalFile(file.FullPath, file.ContentType, file.DownloadName)
                : PhysicalFile(file.FullPath, file.ContentType);
        }
```

### Step 4 — Analysis attachments (`AnalysisController.cs`)

**4a.** Add with the `using` lines:

```csharp
using SafyaClinic.Web.Services.Files;
```

**4b.** Replace the fields and constructor:

```csharp
        private readonly IAnalysisService _analysisService;
        private readonly IPatientService _patientService;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public AnalysisController(
            IAnalysisService analysisService,
            IPatientService patientService,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _analysisService = analysisService;
            _patientService = patientService;
            _config = config;
            _env = env;
        }
```

with:

```csharp
        private readonly IAnalysisService _analysisService;
        private readonly IPatientService _patientService;
        private readonly IFileStorage _files;

        public AnalysisController(
            IAnalysisService analysisService,
            IPatientService patientService,
            IFileStorage files)
        {
            _analysisService = analysisService;
            _patientService = patientService;
            _files = files;
        }
```

**4c.** Replace everything from the `UploadAttachment` action (start at its `[HttpPost]` line) **down to the end of `LoadAttachmentBytesAsync`** (its closing `}`, just before the `[HttpPost]` of `DeleteAttachment`) with:

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ClinicalStaff")]
        public async Task<IActionResult> UploadAttachment(int analysisId, IFormFile? file)
        {
            var (stored, error) = await _files.SaveAsync(file, LocalFileStorage.Analysis);
            if (stored is null)
            {
                Error(error!);
                return RedirectToAction(nameof(Details), new { id = analysisId });
            }

            try
            {
                var result = await _analysisService.AddAttachmentAsync(
                    analysisId, stored.Key, stored.FileName, stored.ContentType, stored.Length, CurrentUserId);

                if (!result.IsSuccess)
                {
                    _files.Delete(stored.Key);
                    Error(result.Errors.FirstOrDefault() ?? "The file could not be attached.");
                }
            }
            catch
            {
                _files.Delete(stored.Key);   // no orphan file if the database insert fails
                throw;
            }

            return RedirectToAction(nameof(Details), new { id = analysisId });
        }

        // ── View (inline, for the pop-up preview) vs Download (forced) ────
        [HttpGet]
        public Task<IActionResult> ViewAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: false);

        [HttpGet]
        public Task<IActionResult> DownloadAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: true);

        private async Task<IActionResult> ServeAttachmentAsync(int attachmentId, bool download)
        {
            var result = await _analysisService.GetAttachmentAsync(attachmentId);
            if (!result.IsSuccess) return NotFound();

            return AttachmentResult(_files.Find(result.Data!.FilePath), download);
        }
```

**4d.** Replace the body of `DeleteAttachment`:

```csharp
            await _analysisService.DeleteAttachmentAsync(attachmentId);
            return RedirectToAction(nameof(Details), new { id = analysisId });
```

with:

```csharp
            var attachment = await _analysisService.GetAttachmentAsync(attachmentId);
            var result = await _analysisService.DeleteAttachmentAsync(attachmentId);
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = analysisId });
```

**4e.** `using Microsoft.AspNetCore.StaticFiles;` at the top is no longer needed — delete it.

**Check:** searching `AnalysisController.cs` for `_config`, `_env` and `FileStorage:BasePath` finds nothing.

### Step 5 — Prescription attachments (`MedicalRecordsController.cs`)

**5a.** Add with the `using` lines, and delete `using Microsoft.AspNetCore.StaticFiles;`:

```csharp
using SafyaClinic.Web.Services.Files;
```

**5b.** In the fields, replace

```csharp
        private readonly IWebHostEnvironment _env;
```

with

```csharp
        private readonly IFileStorage _files;
```

In the constructor parameters, replace `IWebHostEnvironment env,` with `IFileStorage files,` and in the body replace `_env = env;` with `_files = files;`. (Keep `_config` — the print page uses it.)

**5c.** Replace the `UploadPrescriptionAttachment` action (from its `[HttpPost]` line to its closing `}`) with:

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "ClinicalStaff")]
        public async Task<IActionResult> UploadPrescriptionAttachment(
            int prescriptionId, int recordId, IFormFile? file)
        {
            var (stored, error) = await _files.SaveAsync(file, LocalFileStorage.Prescriptions);
            if (stored is null)
            {
                Error(error!);
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            try
            {
                var result = await _recordService.AddPrescriptionAttachmentAsync(
                    prescriptionId, stored.Key, stored.FileName,
                    stored.ContentType, stored.Length, CurrentUserId);

                if (!result.IsSuccess)
                {
                    _files.Delete(stored.Key);
                    Error(result.Errors.FirstOrDefault() ?? "The file could not be attached.");
                }
            }
            catch
            {
                _files.Delete(stored.Key);   // no orphan file if the database insert fails
                throw;
            }

            return RedirectToAction(nameof(Details), new { id = recordId });
        }
```

**5d.** Replace the body of `DeleteAttachment(int attachmentId, int recordId)`:

```csharp
            await _recordService.DeleteAttachmentAsync(attachmentId);
            return RedirectToAction(nameof(Details), new { id = recordId });
```

with:

```csharp
            var attachment = await _recordService.GetAttachmentAsync(attachmentId);
            var result = await _recordService.DeleteAttachmentAsync(attachmentId);
            if (result.IsSuccess && attachment.IsSuccess)
                _files.Delete(attachment.Data!.FilePath);

            return RedirectToAction(nameof(Details), new { id = recordId });
```

**5e.** Replace `ViewAttachment`, `DownloadAttachment` and `LoadAttachmentBytesAsync` (from the `// ── View (inline…` comment to the closing `}` of `LoadAttachmentBytesAsync`, just before `// ── Printable prescription`) with:

```csharp
        // ── View (inline, for the pop-up preview) vs Download (forced) ────
        [HttpGet]
        public Task<IActionResult> ViewAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: false);

        [HttpGet]
        public Task<IActionResult> DownloadAttachment(int attachmentId) => ServeAttachmentAsync(attachmentId, download: true);

        private async Task<IActionResult> ServeAttachmentAsync(int attachmentId, bool download)
        {
            var result = await _recordService.GetAttachmentAsync(attachmentId);
            if (!result.IsSuccess) return NotFound();

            return AttachmentResult(_files.Find(result.Data!.FilePath), download);
        }

```

**5f.** Delete the whole private `SaveFileAsync` method (from `private async Task<(string filePath, string savedName)> SaveFileAsync(` to its closing `}`, just before `//---Consultation Context…`).

**Check:** searching `MedicalRecordsController.cs` for `_env`, `SaveFileAsync` and `FileStorage:BasePath` finds nothing.

### Step 6 — Upload inputs suggest the allowed types

In **both** `Views/Analysis/Details.cshtml` and `Views/MedicalRecords/PrescriptionDetails.cshtml`, replace:

```html
<input type="file" name="file" class="form-control form-control-sm" />
```

with:

```html
<input type="file" name="file" class="form-control form-control-sm" accept=".pdf,.jpg,.jpeg,.png" aria-label="Attachment (PDF, JPG or PNG)" />
```

(This only filters the file picker; the server check in Step 1 is what enforces it.)

### Step 7 — Settings and git hygiene

**7a.** In `SafyaClinic/SafyaClinic.Web/appsettings.json` **and** `SafyaClinic/docs/demo-appsettings.template.json`, change

```json
    "BasePath": "wwwroot/uploads",
```

to

```json
    "BasePath": "App_Data/uploads",
```

**7b.** Append to `.gitignore` (repository root):

```gitignore
# Attachment files are runtime data, never committed (S3)
App_Data/
**/wwwroot/uploads/
```

**7c.** Move your local files and untrack the committed one:

```powershell
$web = "SafyaClinic\SafyaClinic.Web"
New-Item -ItemType Directory -Force "$web\App_Data\uploads" | Out-Null
Copy-Item "$web\wwwroot\uploads\*" "$web\App_Data\uploads\" -Recurse -Force
Get-ChildItem "$web\App_Data\uploads" -Recurse -File | Select-Object FullName
git rm -r --cached SafyaClinic/SafyaClinic.Web/wwwroot/uploads
Remove-Item "$web\wwwroot\uploads" -Recurse -Force
```

Expected: the list shows `analysis\…` and `prescriptions\…` files under `App_Data\uploads`; `git rm` prints the removed `ace36a35-….jpeg`. If `Remove-Item` reports *access denied* (the same folder lock as in S4), leave it — the folder is ignored by git and blocked by Step 2c; delete it later.

### Step 8 — Tests

Create `SafyaClinic/tests/SafyaClinic.Tests/Web/Files/LocalFileStorageTests.cs`:

```csharp
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
```

**Check:**

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected **Passed: 47** (32 + 15), Failed: 0. If anything fails, send me the output — do not change the tests to make them pass.

### Step 9 — Manual check (Development is fine)

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **No anonymous access.** In a private/incognito window (not signed in) open
   `http://localhost:5070/uploads/analysis/ace36a35-3433-44b9-afc4-0fc6b41b49bd.jpeg` → **404**.
2. **Existing attachments still open.** Signed in as Admin, open an analysis and a prescription that already have attachments; preview and download both work.
3. **Upload works.** Attach a real PNG or PDF to an analysis → it appears; the file is in `SafyaClinic\SafyaClinic.Web\App_Data\uploads\analysis\`.
4. **Wrong types refused.** Try an `.html` or `.svg` file → red message *Only PDF, JPG and PNG…*. Rename a `.txt` file to `.png` and try it → *content does not match*. In both cases no new file appears in `App_Data\uploads`.
5. **Delete removes the file.** Delete the attachment from step 3 (Admin) → it disappears from the page **and** from `App_Data\uploads\analysis\`.
6. *(Optional)* In the browser's DevTools → Network, the `ViewAttachment` response has `X-Content-Type-Options: nosniff`.

### Step 10 — Commit, push, PR

```powershell
git add .gitignore SafyaClinic/SafyaClinic.Web SafyaClinic/docs/demo-appsettings.template.json SafyaClinic/tests/SafyaClinic.Tests Docs/implementation
git reset SafyaClinic/SafyaClinic.Web/SafyaClinic.Web.csproj.user
git status
git commit -m "S3: store attachments outside wwwroot; allow-list, size and content checks; serve with nosniff; delete files with rows"
git push -u origin chore/s3-attachments
```

`git status` must **not** list anything under `App_Data`. Open the PR and send me the number and CI result. Do not merge before review.

---

## Hosted servers (when you next publish)

Do this on the **test site first**; repeat for the demo site only after the test site works. In the MonsterASP control panel's File Manager:

1. **Before publishing**, download a backup of `wwwroot/uploads` (if it exists).
2. Publish the new version.
3. Create `App_Data/uploads` in the site root and upload the `analysis` and `prescriptions` folders into it (same file names).
4. Sign in, open an existing attachment (must show), upload a small PNG (must succeed — proves the site can write to `App_Data`), then delete it.
5. Delete `wwwroot/uploads` on the server.

If step 4's upload fails with a permissions error, tell me before touching the demo site.

## Acceptance criteria

- [ ] Anonymous `GET /uploads/...` returns 404.
- [ ] Existing attachments (old stored paths) still preview and download for authorised users.
- [ ] Only PDF/JPG/PNG within the size limit are accepted; others get a clear message and leave no file.
- [ ] Files are served with the server's content type and `nosniff`; non-allowed old files only download.
- [ ] A failed database insert leaves no file; deleting an attachment deletes its file.
- [ ] No upload files tracked by git; `App_Data` ignored.
- [ ] 47 tests pass locally and in CI.
