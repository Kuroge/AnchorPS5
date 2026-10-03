using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AnchorPS5.Core.Diagnostics;
using Avalonia.Media.Imaging;

namespace AnchorPS5.App.Services;

/// <summary>
/// Carga los iconos de las apps (http/https o fichero local). Avalonia no descarga imágenes
/// remotas por sí mismo (WinUI sí, con BitmapImage), así que se hace aquí: una sola petición
/// por Uri, copia en disco (config/cache/icons) y decodificado a 256 px fuera del hilo de la UI.
/// </summary>
public static class IconLoader
{
    private const int DecodeWidth = 256;
    private static readonly ConcurrentDictionary<Uri, Task<Bitmap?>> Loads = new();

    private static string CacheDirectory => Path.Combine(App.Paths.ConfigDirectory, "cache", "icons");

    public static Task<Bitmap?> LoadAsync(Uri uri) => Loads.GetOrAdd(uri, u => Task.Run(() => LoadCoreAsync(u)));

    private static async Task<Bitmap?> LoadCoreAsync(Uri uri)
    {
        try
        {
            if (uri.IsFile)
                return Decode(await File.ReadAllBytesAsync(uri.LocalPath));

            var cachePath = Path.Combine(CacheDirectory, Hash(uri.AbsoluteUri));
            if (File.Exists(cachePath))
            {
                try { return Decode(await File.ReadAllBytesAsync(cachePath)); }
                catch (Exception) { File.Delete(cachePath); } // copia corrupta: se vuelve a bajar
            }

            var bytes = await App.Http.GetByteArrayAsync(uri);
            var bitmap = Decode(bytes);
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                await File.WriteAllBytesAsync(cachePath, bytes);
            }
            catch (IOException) { } // sin caché en disco no pasa nada
            return bitmap;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Icono no cargado: {uri}", ex);
            Loads.TryRemove(uri, out _); // se reintenta en la próxima carga del catálogo
            return null;
        }
    }

    private static Bitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.HighQuality);
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
