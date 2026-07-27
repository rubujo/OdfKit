using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies OpenPGP package interoperability with an external GnuPG process.
/// 以外部 GnuPG 程序驗證 OpenPGP 封裝互通。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public sealed class OpenPgpExternalInteropTests
{
    /// <summary>
    /// Verifies bidirectional session-key transport and a standards-shaped ODF package with a real key.
    /// 以真實金鑰驗證雙向工作階段金鑰傳送與規範形狀的 ODF 封裝。
    /// </summary>
    [Fact]
    public void GnuPgDecryptsOdfKitSessionKeyAndPackageRoundTrips()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ODFKIT_RUN_GNUPG_INTEROP"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Skip("設定 ODFKIT_RUN_GNUPG_INTEROP=1 才執行真實 GnuPG 互通測試。");
        }

        string gpg = Environment.GetEnvironmentVariable("ODFKIT_GPG_PATH") ?? "gpg";
        string baseTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP")
            ?? Path.GetTempPath();
        string tempRoot = Path.Combine(
            baseTemp,
            global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("opgp-"));
        string gpgHome = Path.Combine(tempRoot, "g");
        Directory.CreateDirectory(gpgHome);

        try
        {
            const string userId = "OdfKit CI Interop <odfkit-interop@example.invalid>";
            RunGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--pinentry-mode",
                "loopback",
                "--passphrase",
                string.Empty,
                "--quick-generate-key",
                userId,
                "rsa2048",
                "encr",
                "1d");

            string listing = RunGpg(
                gpg,
                gpgHome,
                "--batch",
                "--with-colons",
                "--fingerprint",
                "--list-keys",
                userId);
            string fingerprint = listing
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(':'))
                .Where(fields => fields.Length > 9 && fields[0] == "fpr")
                .Select(fields => fields[9])
                .First();

            string publicKeyPath = Path.Combine(tempRoot, "public.gpg");
            string secretKeyPath = Path.Combine(tempRoot, "secret.gpg");
            RunGpg(gpg, gpgHome, "--batch", "--yes", "--output", publicKeyPath, "--export", fingerprint);
            RunGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--pinentry-mode",
                "loopback",
                "--passphrase",
                string.Empty,
                "--output",
                secretKeyPath,
                "--export-secret-keys",
                fingerprint);

            byte[] publicKey = File.ReadAllBytes(publicKeyPath);
            byte[] secretKey = File.ReadAllBytes(secretKeyPath);
            var keyProvider = new OdfBouncyCastleOpenPgpProvider(secretKey, _ => []);

            byte[] sessionKey = RandomNumberGenerator.GetBytes(32);
            var recipient = new OdfOpenPgpRecipient
            {
                KeyId = fingerprint,
                Recipient = userId,
                PublicKey = publicKey
            };

            byte[] bouncyCipher = keyProvider.EncryptSessionKey(sessionKey, recipient);
            string bouncyCipherPath = Path.Combine(tempRoot, "bouncy-session-key.pgp");
            string gpgPlainPath = Path.Combine(tempRoot, "gpg-decrypted-session-key.bin");
            File.WriteAllBytes(bouncyCipherPath, bouncyCipher);
            RunGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--pinentry-mode",
                "loopback",
                "--passphrase",
                string.Empty,
                "--output",
                gpgPlainPath,
                "--decrypt",
                bouncyCipherPath);
            Assert.Equal(sessionKey, File.ReadAllBytes(gpgPlainPath));

            string gpgPlainSessionKeyPath = Path.Combine(tempRoot, "gpg-session-key.bin");
            string gpgAeadCipherPath = Path.Combine(tempRoot, "gpg-aead-session-key.pgp");
            File.WriteAllBytes(gpgPlainSessionKeyPath, sessionKey);
            RunGpg(
                gpg,
                gpgHome,
                "--batch",
                "--yes",
                "--trust-model",
                "always",
                "--recipient",
                fingerprint,
                "--cipher-algo",
                "AES256",
                "--force-ocb",
                "--compress-algo",
                "none",
                "--output",
                gpgAeadCipherPath,
                "--encrypt",
                gpgPlainSessionKeyPath);

            byte[] gpgAeadCipher = File.ReadAllBytes(gpgAeadCipherPath);
            Assert.Contains((byte)0xD4, gpgAeadCipher);
            Assert.Equal(sessionKey, keyProvider.DecryptSessionKey(gpgAeadCipher, fingerprint));

            byte[] tamperedAeadCipher = (byte[])gpgAeadCipher.Clone();
            tamperedAeadCipher[tamperedAeadCipher.Length - 1] ^= 0x01;
            Assert.Throws<CryptographicException>(
                () => keyProvider.DecryptSessionKey(tamperedAeadCipher, fingerprint));

            string packagePath = Path.Combine(tempRoot, "openpgp-interop.odt");
            var cryptographyProvider = new OdfOpenPgpCryptographyProvider(keyProvider);
            using (var package = OdfPackage.Create(packagePath))
            {
                package.SetMimeType("application/vnd.oasis.opendocument.text");
                package.WriteEntry("content.xml", Encoding.UTF8.GetBytes("<content>OpenPGP interop</content>"), "text/xml");
                package.WriteEntry("styles.xml", Encoding.UTF8.GetBytes("<styles/>"), "text/xml");
                package.SaveOptions.EncryptionAlgorithm = OdfEncryptionAlgorithm.OpenPgp;
                package.SaveOptions.CryptographyProvider = cryptographyProvider;
                package.SaveOptions.OpenPgpRecipients.Add(recipient);
                package.Save();
            }

            using (var package = OdfPackage.Open(
                packagePath,
                new OdfLoadOptions { CryptographyProvider = cryptographyProvider }))
            {
                Assert.Equal(
                    "<content>OpenPGP interop</content>",
                    Encoding.UTF8.GetString(package.ReadEntry("content.xml")));
                Assert.Equal("<styles/>", Encoding.UTF8.GetString(package.ReadEntry("styles.xml")));
            }

            string? outputDirectory = Environment.GetEnvironmentVariable("ODFKIT_OPENPGP_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                File.Copy(packagePath, Path.Combine(outputDirectory, "openpgp-interop.odt"), overwrite: true);
                using var archive = ZipFile.OpenRead(packagePath);
                using Stream manifest = archive.GetEntry("META-INF/manifest.xml")!.Open();
                using var output = File.Create(Path.Combine(outputDirectory, "openpgp-manifest.xml"));
                manifest.CopyTo(output);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // GnuPG 在 Windows 上可能短暫保留 keybox handle；測試結果不依賴清理成功。
            }
            catch (UnauthorizedAccessException)
            {
                // 同上。
            }
        }
    }

    private static string RunGpg(string executable, string home, params string[] arguments)
    {
        string workingDirectory = Directory.GetParent(home)!.FullName;
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        startInfo.Environment["GNUPGHOME"] = Path.GetRelativePath(workingDirectory, home);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(
                Path.IsPathRooted(argument)
                    ? Path.GetRelativePath(workingDirectory, argument)
                    : argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("無法啟動 GnuPG。");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"GnuPG 結束碼 {process.ExitCode}。{Environment.NewLine}{stderr}");
        return stdout;
    }
}
