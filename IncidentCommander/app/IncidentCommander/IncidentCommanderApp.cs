return await App.Run(args);

public record SessionIdentity(string? UserId);
public record ClientParameters(string Name = "Ikon");

[App]
public partial class IncidentCommanderApp(IApp<SessionIdentity, ClientParameters> app)
{
    private UI UI { get; } = new(app, new IkonTheme());

    public async Task Main()
    {
        UI.Root([Page.Default], content: view =>
        {
            view.Column([Container.Xl2, "py-8 px-4"], content: view =>
            {
                view.Text([Text.H2], nameof(IncidentCommanderApp));
            });
        });
    }
}