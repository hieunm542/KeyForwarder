namespace KeyForwarder;

static class Program
{
    private const string MutexName = "Local\\KeyForwarder_SingleInstance";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "KeyForwarder is already running.",
                "KeyForwarder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
