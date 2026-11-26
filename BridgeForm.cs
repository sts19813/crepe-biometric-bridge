using SourceAFIS;

namespace CrepeBiometricBridge;

internal sealed class BridgeForm : Form
{
    private readonly TextBox employeeId = new() { Width = 230 };
    private readonly Button enroll = new() { AutoSize = true, Text = "Registrar huella" };
    private readonly Button check = new() { AutoSize = true, Text = "Checar entrada / salida" };
    private readonly Button settings = new() { AutoSize = true, Text = "Configuración" };
    private readonly Label status = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(16), TextAlign = ContentAlignment.MiddleCenter };
    private readonly TemplateVault vault = new();

    public BridgeForm()
    {
        Text = "Crepe · Checador biométrico";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(580, 310);
        Font = new Font(Font.FontFamily, 12);

        var panel = new FlowLayoutPanel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(32),
            WrapContents = false,
        };

        panel.Controls.Add(new Label { AutoSize = true, Text = "ID del empleado en Crepe" });
        panel.Controls.Add(employeeId);
        panel.Controls.Add(new Label { AutoSize = true, Height = 12 });
        panel.Controls.Add(enroll);
        panel.Controls.Add(check);
        panel.Controls.Add(settings);
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(470, 0),
            Padding = new Padding(0, 16, 0, 0),
            Text = "La aplicación guarda una plantilla cifrada para comparar localmente; no guarda imágenes de huellas.",
        });

        Controls.Add(panel);
        Controls.Add(status);

        enroll.Click += async (_, _) => await EnrollAsync();
        check.Click += async (_, _) => await CheckAsync();
        settings.Click += (_, _) => EditSettings();
        SetStatus("Configura Crepe y ejecuta este checador como administrador.");
    }

    private async Task EnrollAsync()
    {
        if (!int.TryParse(employeeId.Text, out var parsedEmployeeId) || parsedEmployeeId < 1)
        {
            SetStatus("Escribe un ID de empleado válido.", Color.DarkRed);
            return;
        }

        await RunAsync(async () =>
        {
            SetStatus("Coloca el dedo en el lector.");
            var template = await FingerprintCapture.CaptureAsync();
            var templateId = Guid.NewGuid();

            vault.Save(templateId, parsedEmployeeId, template);

            var message = await new CrepeClient(BridgeSettingsStore.Read()).EnrollAsync(parsedEmployeeId, templateId);
            SetStatus(message, Color.DarkGreen);
        });
    }

    private async Task CheckAsync()
    {
        await RunAsync(async () =>
        {
            SetStatus("Coloca el dedo en el lector.");
            var probe = await FingerprintCapture.CaptureAsync();
            var match = vault.Find(probe);

            if (match is null)
            {
                SetStatus("Huella no reconocida.", Color.DarkRed);
                return;
            }

            var message = await new CrepeClient(BridgeSettingsStore.Read()).RegisterAsync(match.TemplateId);
            SetStatus(message, Color.DarkGreen);
        });
    }

    private void EditSettings()
    {
        using var dialog = new SettingsForm(BridgeSettingsStore.Read());

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            BridgeSettingsStore.Write(dialog.Settings);
            SetStatus("Configuración guardada en esta computadora.", Color.DarkGreen);
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        SetButtonsEnabled(false);

        try
        {
            await action();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, Color.DarkRed);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        enroll.Enabled = enabled;
        check.Enabled = enabled;
        settings.Enabled = enabled;
    }

    private void SetStatus(string message, Color? color = null)
    {
        status.Text = message;
        status.ForeColor = color ?? Color.Black;
    }
}

internal sealed class SettingsForm : Form
{
    private readonly TextBox crepeUrl = new() { Width = 400 };
    private readonly TextBox bridgeKey = new() { PasswordChar = '•', Width = 400 };

    public SettingsForm(BridgeSettings settings)
    {
        Text = "Configuración de Crepe";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(510, 190);

        crepeUrl.Text = settings.CrepeUrl;
        bridgeKey.Text = settings.BridgeKey;

        var save = new Button { AutoSize = true, DialogResult = DialogResult.OK, Text = "Guardar" };
        var cancel = new Button { AutoSize = true, DialogResult = DialogResult.Cancel, Text = "Cancelar" };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(24), WrapContents = false };

        panel.Controls.Add(new Label { AutoSize = true, Text = "Dirección de Crepe local" });
        panel.Controls.Add(crepeUrl);
        panel.Controls.Add(new Label { AutoSize = true, Padding = new Padding(0, 10, 0, 0), Text = "Clave del puente" });
        panel.Controls.Add(bridgeKey);
        panel.Controls.Add(new FlowLayoutPanel { AutoSize = true, Controls = { save, cancel } });

        Controls.Add(panel);
        AcceptButton = save;
        CancelButton = cancel;
    }

    public BridgeSettings Settings => new(crepeUrl.Text.Trim(), bridgeKey.Text);
}
