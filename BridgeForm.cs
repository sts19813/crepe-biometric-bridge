using SourceAFIS;

namespace CrepeBiometricBridge;

internal sealed class BridgeForm : Form
{
    private static readonly Color Navy = Color.FromArgb(16, 33, 62);
    private static readonly Color Indigo = Color.FromArgb(66, 75, 190);
    private static readonly Color Background = Color.FromArgb(244, 247, 252);
    private static readonly Color Success = Color.FromArgb(18, 145, 102);
    private static readonly Color Danger = Color.FromArgb(202, 68, 80);
    private readonly ComboBox employeeSearch = new() { DropDownStyle = ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 13), Height = 44, IntegralHeight = false, Width = 440 };
    private readonly Label selectedEmployee = new() { AutoSize = true, ForeColor = Color.FromArgb(83, 95, 114), MaximumSize = new Size(440, 0), Text = "Busca y selecciona al colaborador para registrar su huella." };
    private readonly Button enroll = CreateButton("Registrar huella", Indigo);
    private readonly Button settings = CreateButton("Configuración", Color.FromArgb(225, 231, 241), Navy);
    private readonly Label statusTitle = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 16), ForeColor = Navy, Text = "Lector activo", TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label statusDetail = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), ForeColor = Color.FromArgb(83, 95, 114), TextAlign = ContentAlignment.TopCenter };
    private readonly Label fingerprintIcon = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 23), ForeColor = Indigo, Text = "HUELLA", TextAlign = ContentAlignment.MiddleCenter };
    private readonly TemplateVault vault = new();
    private CancellationTokenSource? kioskCancellation;
    private CancellationTokenSource? searchCancellation;
    private Task? kioskTask;
    private bool isUpdatingEmployees;
    private EmployeeOption? selectedEmployeeOption;

    public BridgeForm()
    {
        Text = "Crepe · Checador biométrico";
        BackColor = Background;
        ClientSize = new Size(760, 780);
        MinimumSize = new Size(620, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 11);

        var root = new TableLayoutPanel
        {
            BackColor = Background,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 2,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

        var content = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 20), RowCount = 2 };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        content.Controls.Add(CreateEnrollmentCard(), 0, 0);
        content.Controls.Add(CreateReaderCard(), 0, 1);
        root.Controls.Add(content, 0, 0);
        root.Controls.Add(CreateStatusCard(), 0, 1);
        Controls.Add(root);

        employeeSearch.TextUpdate += async (_, _) => await BeginEmployeeSearchAsync();
        employeeSearch.SelectedIndexChanged += (_, _) => UpdateSelectedEmployee();
        enroll.Click += async (_, _) => await EnrollAsync();
        settings.Click += (_, _) => EditSettings();
        Shown += async (_, _) =>
        {
            await QueueEmployeeSearchAsync();
            StartKiosk();
        };
        FormClosing += (_, _) => StopKiosk();
    }

    private Control CreateEnrollmentCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(0, 0, 0, 12);
        var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(38), RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Navy, Text = "Registrar colaborador" }, 0, 0);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = Color.FromArgb(83, 95, 114), Text = "Escribe el nombre y selecciona a la persona de Crepe." }, 0, 1);
        layout.Controls.Add(employeeSearch, 0, 2);
        layout.Controls.Add(selectedEmployee, 0, 3);
        layout.Controls.Add(enroll, 0, 4);
        layout.Controls.Add(settings, 0, 5);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(106, 117, 136),
            MaximumSize = new Size(440, 0),
            Text = "El registro toma tres lecturas del mismo dedo. Retíralo entre cada lectura para mejorar el reconocimiento.",
        }, 0, 7);

        card.Controls.Add(layout);
        return card;
    }

    private Control CreateReaderCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(0, 12, 0, 0);
        var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(38), RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Navy, Text = "Punto de checada" }, 0, 0);
        var iconCircle = new Panel { BackColor = Color.FromArgb(234, 237, 255), Dock = DockStyle.Fill, Margin = new Padding(26, 26, 26, 20) };
        iconCircle.Controls.Add(fingerprintIcon);
        layout.Controls.Add(iconCircle, 0, 1);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = Success, Font = new Font("Segoe UI Semibold", 12), Text = "●  AUTOMÁTICO", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(statusTitle, 0, 3);
        layout.Controls.Add(statusDetail, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateStatusCard()
    {
        var panel = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Padding = new Padding(22, 14, 22, 14) };
        var title = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 13), ForeColor = Navy, Text = "CREPE  /  CHECADOR BIOMÉTRICO", Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft };
        var note = new Label { AutoSize = true, Font = new Font("Segoe UI", 10), ForeColor = Color.FromArgb(106, 117, 136), Text = "Las plantillas se cifran en esta computadora. No se almacenan imágenes.", Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight };
        panel.Controls.Add(note);
        panel.Controls.Add(title);
        return panel;
    }

    private async Task BeginEmployeeSearchAsync()
    {
        selectedEmployeeOption = null;
        selectedEmployee.Text = "Busca y selecciona al colaborador para registrar su huella.";
        await QueueEmployeeSearchAsync();
    }

    private async Task QueueEmployeeSearchAsync()
    {
        if (isUpdatingEmployees)
        {
            return;
        }

        searchCancellation?.Cancel();
        searchCancellation?.Dispose();
        searchCancellation = new CancellationTokenSource();
        var cancellationToken = searchCancellation.Token;

        try
        {
            await Task.Delay(250, cancellationToken);
            var employees = await new CrepeClient(BridgeSettingsStore.Read()).FindEmployeesAsync(employeeSearch.Text, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var currentText = employeeSearch.Text;
            isUpdatingEmployees = true;
            employeeSearch.BeginUpdate();
            employeeSearch.Items.Clear();
            employeeSearch.Items.AddRange(employees.Cast<object>().ToArray());
            employeeSearch.Text = currentText;
            employeeSearch.SelectionStart = currentText.Length;
            employeeSearch.EndUpdate();
            employeeSearch.DroppedDown = employeeSearch.Focused && employees.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetStatus("No se pudo consultar al equipo.", exception.Message, Danger);
        }
        finally
        {
            isUpdatingEmployees = false;
        }
    }

    private void UpdateSelectedEmployee()
    {
        if (isUpdatingEmployees)
        {
            return;
        }

        if (employeeSearch.SelectedItem is EmployeeOption employee)
        {
            selectedEmployeeOption = employee;
            selectedEmployee.Text = $"Seleccionado: {employee.Name}{(string.IsNullOrWhiteSpace(employee.Position) ? string.Empty : $" · {employee.Position}")}";
        }
    }

    private async Task EnrollAsync()
    {
        if (selectedEmployeeOption is not { } employee)
        {
            SetStatus("Selecciona un nombre de la lista antes de registrar.", "Busca al colaborador y tócala en la lista.", Danger);
            return;
        }

        await StopKioskAsync();
        SetButtonsEnabled(false);

        try
        {
            var templates = new List<FingerprintTemplate>();

            for (var sample = 1; sample <= 3; sample++)
            {
                SetStatus($"Registro {sample} de 3", "Coloca el mismo dedo sobre el lector.", Indigo);
                templates.Add(await FingerprintCapture.CaptureAsync(CancellationToken.None));

                if (sample < 3)
                {
                    SetStatus($"Registro {sample} guardado", "Retira el dedo y vuelve a colocarlo para la siguiente lectura.", Indigo);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            var templateId = Guid.NewGuid();
            vault.Save(templateId, employee.Id, templates);

            try
            {
                var message = await new CrepeClient(BridgeSettingsStore.Read()).EnrollAsync(employee.Id, templateId);
                SetStatus("Registro terminado", message, Success);
            }
            catch
            {
                vault.Delete(templateId);
                throw;
            }
        }
        catch (Exception exception)
        {
            SetStatus("No se pudo registrar la huella", exception.Message, Danger);
        }
        finally
        {
            SetButtonsEnabled(true);
            StartKiosk();
        }
    }

    private void StartKiosk()
    {
        if (kioskTask is { IsCompleted: false })
        {
            return;
        }

        kioskCancellation = new CancellationTokenSource();
        kioskTask = RunKioskAsync(kioskCancellation.Token);
    }

    private void StopKiosk()
    {
        kioskCancellation?.Cancel();
    }

    private async Task StopKioskAsync()
    {
        StopKiosk();

        if (kioskTask is not null)
        {
            await kioskTask;
        }

        kioskCancellation?.Dispose();
        kioskCancellation = null;
        kioskTask = null;
    }

    private async Task RunKioskAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                SetStatus("Lector activo", "Coloca un dedo para registrar entrada o salida.", Indigo);
                var probe = await FingerprintCapture.CaptureAsync(cancellationToken);
                var match = vault.Find(probe);

                if (match is null || !match.IsRecognized)
                {
                    SetStatus("Huella no reconocida", "Pide a recepción que registre la huella del colaborador.", Danger);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                var message = await new CrepeClient(BridgeSettingsStore.Read()).RegisterAsync(match.Template.TemplateId);
                SetStatus("Registro guardado", message, Success);
                await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (CrepeBridgeException exception) when (exception.StatusCode == 422)
            {
                SetStatus("Checada completada", exception.Message, Color.FromArgb(190, 126, 24));
                await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            }
            catch (Exception exception)
            {
                SetStatus("No se pudo leer la huella", exception.Message, Danger);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private void EditSettings()
    {
        using var dialog = new SettingsForm(BridgeSettingsStore.Read());

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            BridgeSettingsStore.Write(dialog.Settings);
            SetStatus("Configuración guardada", "El lector seguirá usando esta computadora y esta instancia de Crepe.", Success);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        enroll.Enabled = enabled;
        settings.Enabled = enabled;
        employeeSearch.Enabled = enabled;
    }

    private void SetStatus(string title, string detail, Color color)
    {
        statusTitle.Text = title;
        statusTitle.ForeColor = color;
        statusDetail.Text = detail;
        fingerprintIcon.ForeColor = color;
    }

    private static Button CreateButton(string text, Color backColor, Color? foreColor = null)
    {
        return new Button
        {
            AutoSize = false,
            BackColor = backColor,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 },
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 12),
            ForeColor = foreColor ?? Color.White,
            Height = 48,
            Text = text,
            Width = 440,
        };
    }

    private static Panel CreateCard()
    {
        return new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
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
