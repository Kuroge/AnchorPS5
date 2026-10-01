using System.Diagnostics;

namespace AnchorPS5.Core.Downloads;

/// <summary>Extrae un archivo comprimido en una carpeta.</summary>
public interface IArchiveExtractor
{
    /// <exception cref="ExtractionException">Si el archivo no se puede extraer.</exception>
    Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken);
}

public sealed class ExtractionException(string message) : Exception(message);

/// <summary>Qué ficheros se tratan como comprimidos (el resto se guarda tal cual).</summary>
public static class ArchiveTypes
{
    private static readonly string[] Extensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz", ".zst"];

    public static bool IsArchive(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}

/// <summary>Extracción con el 7-Zip incluido en la app (tools\7zip\7z.exe).</summary>
public sealed class SevenZipExtractor : IArchiveExtractor
{
    private readonly string _sevenZipPath;

    public SevenZipExtractor(string sevenZipPath)
    {
        _sevenZipPath = sevenZipPath;
    }

    public async Task ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        if (!File.Exists(_sevenZipPath))
            throw new ExtractionException($"No se encuentra 7-Zip en {_sevenZipPath}");

        Directory.CreateDirectory(destination);
        await RunAsync(archivePath, destination, cancellationToken);

        // .tar.gz y similares: 7-Zip saca primero el .tar; se extrae también.
        var extracted = Directory.GetFileSystemEntries(destination);
        if (extracted.Length == 1 && File.Exists(extracted[0])
            && string.Equals(Path.GetExtension(extracted[0]), ".tar", StringComparison.OrdinalIgnoreCase))
        {
            var tar = extracted[0];
            await RunAsync(tar, destination, cancellationToken);
            File.Delete(tar);
        }
    }

    private async Task RunAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_sevenZipPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // x: con rutas · -y: sin preguntas · -p: sin contraseña (no se queda esperando) ·
        // -bd: sin indicador de progreso · -o: destino.
        foreach (var arg in new[] { "x", "-y", "-p", "-bd", "-o" + destination, "--", archivePath })
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new ExtractionException("No se pudo iniciar 7-Zip.");
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }

        // 0 = bien, 1 = avisos no graves; el resto son errores.
        if (process.ExitCode > 1)
        {
            var output = (await stderr).Trim();
            if (output.Length == 0)
                output = (await stdout).Trim();
            throw new ExtractionException($"7-Zip terminó con código {process.ExitCode}. {output}".Trim());
        }
    }
}
