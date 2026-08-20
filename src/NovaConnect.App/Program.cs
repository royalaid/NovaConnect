namespace NovaConnect.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: @"Local\NovaConnect-3A01D694-8D08-4FA8-A3BF-A70888DB5F56",
            createdNew: out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new NovaConnectApplicationContext());
    }
}
