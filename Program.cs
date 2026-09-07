namespace KiwiDX;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        try
        {
            UserData.Initialize();
            if (!File.Exists(UserData.PathFor("startup.json")) &&
                MessageBox.Show("Do you want to migrate settings from an older portable KiwiDX folder?", "Welcome to KiwiDX", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                using var folder = new FolderBrowserDialog { Description = "Select your old KiwiDX folder", UseDescriptionForTitle = true };
                if (folder.ShowDialog() == DialogResult.OK) UserData.MigrateFrom(folder.SelectedPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not initialize user data. Your original files have been preserved.\n\n{ex.Message}", "KiwiDX", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new Form1());
    }    
}
