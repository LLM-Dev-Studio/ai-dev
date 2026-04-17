namespace AiDevNet.Tests.Unit;

public class AtomicFileWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"atomic-writer-test-{Guid.NewGuid():N}");
    private readonly AtomicFileWriter _writer = new();

    public AtomicFileWriterTests()
    {
        // Ensure temp directory exists
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Cleanup temp directory
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // ignore cleanup errors
        }
    }

    // -------------------------------------------------------------------------
    // WriteAllText — happy paths
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteAllText_WhenFileDoesNotExist_CreatesFile()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "test.txt");
        var content = "Hello, World!";

        // Act
        _writer.WriteAllText(targetPath, content);

        // Assert
        File.Exists(targetPath).ShouldBeTrue();
        File.ReadAllText(targetPath).ShouldBe(content);
    }

    [Fact]
    public void WriteAllText_WhenFileExists_ReplaceContent()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(targetPath, "old content");
        var newContent = "new content";

        // Act
        _writer.WriteAllText(targetPath, newContent);

        // Assert
        File.ReadAllText(targetPath).ShouldBe(newContent);
    }

    [Fact]
    public void WriteAllText_CreatesParentDirectories()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "subdir", "nested", "deep", "test.txt");
        var content = "nested file";

        // Act
        _writer.WriteAllText(targetPath, content);

        // Assert
        Directory.Exists(Path.GetDirectoryName(targetPath)).ShouldBeTrue();
        File.Exists(targetPath).ShouldBeTrue();
        File.ReadAllText(targetPath).ShouldBe(content);
    }

    [Fact]
    public void WriteAllText_LargeContent_WritesSuccessfully()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "large.txt");
        var content = new string('x', 10_000_000); // 10 MB

        // Act
        _writer.WriteAllText(targetPath, content);

        // Assert
        File.Exists(targetPath).ShouldBeTrue();
        var written = File.ReadAllText(targetPath);
        written.Length.ShouldBe(content.Length);
        written.ShouldBe(content);
    }

    [Fact]
    public void WriteAllText_PreservesUnicodeContent()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "unicode.txt");
        var content = "Hello 世界 🌍 مرحبا мир";

        // Act
        _writer.WriteAllText(targetPath, content);

        // Assert
        File.ReadAllText(targetPath).ShouldBe(content);
    }

    // -------------------------------------------------------------------------
    // WriteAllText — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteAllText_WhenPathIsNull_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.WriteAllText(null!, "content"));
    }

    [Fact]
    public void WriteAllText_WhenPathIsWhitespace_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.WriteAllText("   ", "content"));
    }

    [Fact]
    public void WriteAllText_WhenPathHasNoParentDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.WriteAllText(".", "content"));
    }

    [Fact]
    public void WriteAllText_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "test.txt");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _writer.WriteAllText(targetPath, null!));
    }

    [Fact]
    public void WriteAllText_WhenContentIsEmptyString_WritesSuccessfully()
    {
        // Arrange
        var targetPath = Path.Combine(_tempDir, "empty.txt");

        // Act
        _writer.WriteAllText(targetPath, "");

        // Assert
        File.Exists(targetPath).ShouldBeTrue();
        File.ReadAllText(targetPath).ShouldBe("");
    }

    // -------------------------------------------------------------------------
    // WriteAllText — atomicity (temp file cleanup on failure)
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteAllText_WhenDirectoryCannotBeCreated_CleansTempFile()
    {
        // Arrange — use an invalid path that can't have directories created
        // This is platform-dependent; on Windows, certain characters are invalid
        var invalidPath = Path.Combine(_tempDir, "test\0.txt");

        // Act & Assert
        Should.Throw<Exception>(() => _writer.WriteAllText(invalidPath, "content"));

        // Assert — no orphaned temp files in _tempDir
        var tempFiles = Directory.EnumerateFiles(_tempDir, ".*.tmp", SearchOption.AllDirectories).ToList();
        tempFiles.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // ReplaceFile — happy paths
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplaceFile_WhenDestinationExists_ReplacesContent()
    {
        // Arrange
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        var destPath = Path.Combine(_tempDir, "dest.txt");
        File.WriteAllText(sourcePath, "source content");
        File.WriteAllText(destPath, "old dest content");

        // Act
        _writer.ReplaceFile(sourcePath, destPath);

        // Assert
        File.Exists(destPath).ShouldBeTrue();
        File.ReadAllText(destPath).ShouldBe("source content");
        File.Exists(sourcePath).ShouldBeFalse(); // source is moved
    }

    [Fact]
    public void ReplaceFile_WhenDestinationDoesNotExist_MovesSourceToDestination()
    {
        // Arrange
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        var destPath = Path.Combine(_tempDir, "dest.txt");
        File.WriteAllText(sourcePath, "content");

        // Act
        _writer.ReplaceFile(sourcePath, destPath);

        // Assert
        File.Exists(destPath).ShouldBeTrue();
        File.ReadAllText(destPath).ShouldBe("content");
        File.Exists(sourcePath).ShouldBeFalse();
    }

    [Fact]
    public void ReplaceFile_CreatesDestinationDirectory()
    {
        // Arrange
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        var destPath = Path.Combine(_tempDir, "subdir", "nested", "dest.txt");
        File.WriteAllText(sourcePath, "content");

        // Act
        _writer.ReplaceFile(sourcePath, destPath);

        // Assert
        File.Exists(destPath).ShouldBeTrue();
        File.ReadAllText(destPath).ShouldBe("content");
    }

    // -------------------------------------------------------------------------
    // ReplaceFile — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void ReplaceFile_WhenSourcePathIsNull_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.ReplaceFile(null!, "dest"));
    }

    [Fact]
    public void ReplaceFile_WhenSourcePathIsWhitespace_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.ReplaceFile("   ", "dest"));
    }

    [Fact]
    public void ReplaceFile_WhenDestinationPathIsNull_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.ReplaceFile("source", null!));
    }

    [Fact]
    public void ReplaceFile_WhenDestinationPathIsWhitespace_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.ReplaceFile("source", "   "));
    }

    [Fact]
    public void ReplaceFile_WhenDestinationHasNoParentDirectory_ThrowsArgumentException()
    {
        // Arrange
        var sourcePath = Path.Combine(_tempDir, "source.txt");
        File.WriteAllText(sourcePath, "content");

        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.ReplaceFile(sourcePath, "."));
    }

    // -------------------------------------------------------------------------
    // DeleteFile — happy paths
    // -------------------------------------------------------------------------

    [Fact]
    public void DeleteFile_WhenFileExists_DeletesFile()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "content");
        File.Exists(filePath).ShouldBeTrue();

        // Act
        _writer.DeleteFile(filePath);

        // Assert
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void DeleteFile_WhenFileDoesNotExist_IsNoOp()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "nonexistent.txt");

        // Act & Assert — should not throw
        _writer.DeleteFile(filePath);
    }

    // -------------------------------------------------------------------------
    // DeleteFile — validation
    // -------------------------------------------------------------------------

    [Fact]
    public void DeleteFile_WhenPathIsNull_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.DeleteFile(null!));
    }

    [Fact]
    public void DeleteFile_WhenPathIsWhitespace_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => _writer.DeleteFile("   "));
    }

    // -------------------------------------------------------------------------
    // Concurrent safety (basic check)
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteAllText_WithConcurrentWrites_ProducesValidFiles()
    {
        // Arrange
        var paths = Enumerable.Range(0, 10)
            .Select(i => Path.Combine(_tempDir, $"file{i}.txt"))
            .ToList();

        // Act
        Parallel.ForEach(paths, (path, i) =>
        {
            _writer.WriteAllText(path, $"content-{i}");
        });

        // Assert
        paths.ForEach(path =>
        {
            File.Exists(path).ShouldBeTrue();
        });
    }

    // -------------------------------------------------------------------------
    // Special characters in filenames
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteAllText_WithSpecialCharactersInFilename_WritesSuccessfully()
    {
        // Arrange
        var filename = "test-file_with.special(chars)v2.1.txt";
        var targetPath = Path.Combine(_tempDir, filename);
        var content = "content";

        // Act
        _writer.WriteAllText(targetPath, content);

        // Assert
        File.Exists(targetPath).ShouldBeTrue();
        File.ReadAllText(targetPath).ShouldBe(content);
    }
}
