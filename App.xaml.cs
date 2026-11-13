using System.IO;
using System.Windows;

namespace GmStudio;

public partial class App : Application
{
    internal FontPreferences Appearance { get; private set; } = null!;
    internal string PreferencesDirectory { get; private set; } = null!;

    private void LoadPreferences(string directory)
    {
        PreferencesDirectory = directory;
        Appearance = new FontPreferences(Path.Combine(directory, "appearance.json"));
        Resources["InterfaceFont"] = FontPreferences.Compose(Appearance.Current);
    }

    internal void ApplyFonts(FontSelection choice)
    {
        var font = FontPreferences.Compose(choice);
        Appearance.Save(choice);
        Resources["InterfaceFont"] = font;
    }

    private void Start(object sender, StartupEventArgs e)
    {
#if QA
        if(e.Args is ["--qa-recover",var recoveryRoot,var recoveryReport])
        {
            try
            {
                File.WriteAllText(recoveryReport+".waiting","waiting");
                using(var held=StorageTransaction.Acquire(recoveryRoot))StorageTransaction.RecoverLocked(recoveryRoot);
                File.WriteAllText(recoveryReport,"PASS");Shutdown();
            }
            catch(Exception error){File.WriteAllText(recoveryReport,error.ToString());Shutdown(1);}
            return;
        }
        if (e.Args is ["--qa", var testRoot, var captureDirectory])
        {
            LoadPreferences(testRoot);
            var testWindow = new MainWindow(testRoot);
            MainWindow = testWindow;
            testWindow.Loaded += async (_, _) => await testWindow.QualityProbe(captureDirectory);
            testWindow.Show();
            return;
        }
#endif
        LoadPreferences(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zm3GmStudio"));
        string?[] guesses = [
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)),
            @"D:\Media\Game\造梦西游3单机版",
            @"D:\Media\Game\造梦西游3单机版\造梦西游3单机版"
        ];
        string? root = e.Args.Length > 0 ? e.Args[0] : guesses.FirstOrDefault(path => path is not null && GameFolder.IsGameFolder(path));
        var window = new MainWindow(root);
        MainWindow = window;
        window.Show();
    }
}
