using System.Security.Cryptography;
using System.Text;

namespace AiDev.Features.Secrets;

/// <summary>
/// Per-project encrypted secrets store.
/// Secrets are saved to {projectDir}/secrets.json with values encrypted at rest.
/// On Windows, values are protected with DPAPI (user scope).
/// On other platforms, values are encrypted with AES-256-CBC using a key derived from the project ID via PBKDF2.
///
/// Secret values are NEVER written to logs, transcripts, or prompt output.
/// </summary>
public class SecretsService(WorkspacePaths paths, AtomicFileWriter fileWriter)
{
    // Schema stored in secrets.json
    private sealed record SecretEntry(string Name, string Encrypted);

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Returns secret names only — never values.</summary>
    public IReadOnlyList<string> ListSecrets(ProjectSlug projectSlug)
    {
        var entries = ReadEntries(projectSlug);
        return entries.Select(e => e.Name).ToList();
    }

    /// <summary>Adds or replaces a secret. The value is encrypted before persisting.</summary>
    public void SetSecret(ProjectSlug projectSlug, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name must not be empty.", nameof(name));

        var entries = ReadEntries(projectSlug).Where(e => e.Name != name).ToList();
        var encrypted = Encrypt(projectSlug, value);
        entries.Add(new SecretEntry(name, encrypted));
        WriteEntries(projectSlug, entries);
    }

    /// <summary>Removes a secret by name. No-op if not found.</summary>
    public void DeleteSecret(ProjectSlug projectSlug, string name)
    {
        var entries = ReadEntries(projectSlug).Where(e => e.Name != name).ToList();
        WriteEntries(projectSlug, entries);
    }

    /// <summary>
    /// Returns all secrets as a name→plaintext dictionary for injection into agent environments.
    /// Callers must treat the returned values as sensitive — do NOT log them.
    /// </summary>
    public IReadOnlyDictionary<string, string> LoadDecryptedSecrets(ProjectSlug projectSlug)
    {
        var entries = ReadEntries(projectSlug);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            try
            {
                result[entry.Name] = Decrypt(projectSlug, entry.Encrypted);
            }
            catch
            {
                // Skip secrets that fail to decrypt — protects against corrupt entries.
            }
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // File I/O
    // -------------------------------------------------------------------------

    private List<SecretEntry> ReadEntries(ProjectSlug projectSlug)
    {
        var path = paths.SecretsPath(projectSlug).Value;
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<SecretEntry>>(json, JsonDefaults.Read) ?? [];
        }
        catch { return []; }
    }

    private void WriteEntries(ProjectSlug projectSlug, List<SecretEntry> entries)
    {
        var path = paths.SecretsPath(projectSlug).Value;
        fileWriter.WriteAllText(path, JsonSerializer.Serialize(entries, JsonDefaults.Write));
    }

    // -------------------------------------------------------------------------
    // Encryption
    // -------------------------------------------------------------------------

    private static string Encrypt(ProjectSlug projectSlug, string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);

        if (OperatingSystem.IsWindows())
        {
            // DPAPI — encrypts to the current user; only the same user can decrypt.
            var entropy = DeriveEntropy(projectSlug);
            var cipherBytes = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }
        else
        {
            // AES-256-CBC with PBKDF2-derived key from the project ID.
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.GenerateIV();
            aes.Key = DeriveAesKey(projectSlug);
            using var encryptor = aes.CreateEncryptor();
            var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
            // Prefix IV to ciphertext so we can extract it on decrypt.
            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return Convert.ToBase64String(result);
        }
    }

    private static string Decrypt(ProjectSlug projectSlug, string cipherBase64)
    {
        var cipherBytes = Convert.FromBase64String(cipherBase64);

        if (OperatingSystem.IsWindows())
        {
            var entropy = DeriveEntropy(projectSlug);
            var plain = ProtectedData.Unprotect(cipherBytes, entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        else
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Key = DeriveAesKey(projectSlug);
            // Extract IV from the first 16 bytes.
            var iv = new byte[16];
            Buffer.BlockCopy(cipherBytes, 0, iv, 0, 16);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipherBytes, 16, cipherBytes.Length - 16);
            return Encoding.UTF8.GetString(plain);
        }
    }

    /// <summary>
    /// DPAPI entropy derived from the project ID — limits cross-project secret reuse.
    /// </summary>
    private static byte[] DeriveEntropy(ProjectSlug projectSlug) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"ads-secrets:{projectSlug.Value}"));

    /// <summary>
    /// AES key derived from the project ID via PBKDF2 with a fixed salt.
    /// This is not a substitute for a proper KMS but provides at-rest protection
    /// on non-Windows platforms where DPAPI is unavailable.
    /// </summary>
    private static byte[] DeriveAesKey(ProjectSlug projectSlug) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password:   Encoding.UTF8.GetBytes(projectSlug.Value),
            salt:       Encoding.UTF8.GetBytes("ai-dev-net-secrets-v1"),
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);
}
