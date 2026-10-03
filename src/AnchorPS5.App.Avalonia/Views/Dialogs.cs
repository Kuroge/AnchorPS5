using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AnchorPS5.App.Views;

/// <summary>
/// Diálogos modales de la app (equivalente a los ContentDialog de WinUI). En Avalonia no hay
/// un ContentDialog nativo, así que se abre una ventana modal pequeña con los botones
/// pedidos. Devuelve qué botón se pulsó.
/// </summary>
public static class Dialogs
{
    public enum Result
    {
        None,
        Primary,
        Secondary,
    }

    /// <summary>
    /// Diálogo con un título, un contenido y hasta tres botones (primario, secundario y
    /// cancelación). <paramref name="secondary"/>/cancelar opcionales.
    /// </summary>
    public static async Task<Result> AskAsync(
        Control root,
        string title,
        Control content,
        string primary,
        string? secondary = null,
        string? cancel = null)
    {
        var window = new Window
        {
            Title = title,
            Width = 460,
            MinWidth = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(20, 0, 20, 20) };
        var resultHolder = new ResultHolder();

        void AddButton(string text, Result value, bool accent)
        {
            var b = new Button
            {
                Content = text,
                MinWidth = 90,
            };
            if (accent)
                b.Classes.Add("accent");
            b.Click += (_, _) =>
            {
                resultHolder.Result = value;
                window.Close();
            };
            buttons.Children.Add(b);
        }

        AddButton(primary, Result.Primary, true);
        if (secondary is not null)
            AddButton(secondary, Result.Secondary, false);
        if (cancel is not null)
            AddButton(cancel, Result.None, false);

        window.Content = new StackPanel
        {
            Margin = new Thickness(20, 20, 20, 8),
            Spacing = 12,
            Children = { content, buttons },
        };

        await window.ShowDialog(OwnerOf(root));
        return resultHolder.Result;
    }

    /// <summary>Inicio de sesión con GitHub (flujo de dispositivo).</summary>
    public static Task<bool> GitHubSignInAsync(Control root) => GitHubAccountDialogs.SignInAsync(root);

    /// <summary>
    /// Ventana dueña de un diálogo. Desde un menú emergente el ancestro es un PopupRoot, no
    /// la ventana: entonces se usa la principal.
    /// </summary>
    public static Window OwnerOf(Control root) =>
        root.FindAncestorOfType<Window>()
        ?? (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow
        ?? throw new InvalidOperationException("No hay ventana principal para el diálogo");

    private sealed class ResultHolder
    {
        public Result Result { get; set; } = Result.None;
    }
}
