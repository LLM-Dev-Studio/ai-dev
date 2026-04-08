namespace AiDev.Services;

/// <summary>
/// Writes file content atomically by using a temporary file in the target directory and replacing the destination.
/// </summary>
public class AtomicFileWriter
{
    public void WriteAllText(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        AiDevTelemetry.AtomicWrites.Add(1, new KeyValuePair<string, object?>("path", Path.GetFileName(path)));

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Target path must have a parent directory.", nameof(path));

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, content);

            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            AiDevTelemetry.AtomicWriteFailures.Add(1, new KeyValuePair<string, object?>("path", Path.GetFileName(path)));
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    public void ReplaceFile(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Destination path must have a parent directory.", nameof(destinationPath));

        Directory.CreateDirectory(directory);
        if (File.Exists(destinationPath))
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(sourcePath, destinationPath);
    }

    public void DeleteFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
