namespace CrepeBiometricBridge;

internal sealed class BridgeForm : Form
{
    private static readonly Color Navy = Color.FromArgb(18, 29, 53);
    private static readonly Color Indigo = Color.FromArgb(213, 49, 84);
    private static readonly Color Background = Color.FromArgb(255, 249, 250);
    private static readonly Color Surface = Color.White;
    private static readonly Color RoseSoft = Color.FromArgb(255, 238, 242);
    private static readonly Color RoseBorder = Color.FromArgb(253, 214, 223);
    private static readonly Color Success = Color.FromArgb(18, 145, 102);
    private static readonly Color Danger = Color.FromArgb(202, 68, 80);
    private readonly TextBox employeeSearch = new() { BackColor = Surface, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), PlaceholderText = "Escribe un nombre para buscar" };
    private readonly ListBox employeeList = new() { BackColor = Surface, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11), IntegralHeight = false };
    private readonly Label selectedEmployee = new() { AutoSize = true, ForeColor = Color.FromArgb(83, 95, 114), MaximumSize = new Size(440, 0), Text = "Busca y selecciona al colaborador para registrar su huella." };
    private readonly Label registrationNotice = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 11), ForeColor = Color.FromArgb(83, 95, 114), Text = "Selecciona a una persona y registra su huella.", TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button enroll = CreateButton("Registrar huella", Indigo);
    private readonly Button unenroll = CreateButton("Eliminar huella registrada", Color.FromArgb(251, 231, 233), Danger);
    private readonly Button settings = CreateButton("Configuración", Color.FromArgb(225, 231, 241), Navy);
    private readonly Button cancelEnrollment = CreateButton("Cancelar registro", Color.FromArgb(251, 231, 233), Danger);
    private readonly Label statusTitle = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 16), ForeColor = Navy, Text = "Lector activo", TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label statusDetail = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), ForeColor = Color.FromArgb(83, 95, 114), TextAlign = ContentAlignment.TopCenter };
    private readonly Label fingerprintIcon = new() { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 23), ForeColor = Indigo, Text = "HUELLA", TextAlign = ContentAlignment.MiddleCenter };
    private readonly PictureBox fingerprintPreview = new() { BackColor = RoseSoft, Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly TemplateVault vault = new();
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, Font = new Font("Segoe UI Semibold", 11), ItemSize = new Size(174, 42), SizeMode = TabSizeMode.Fixed };
    private readonly ListView history = new() { BackColor = Surface, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10), ForeColor = Navy, FullRowSelect = true, GridLines = false, HideSelection = false, View = View.Details };
    private readonly Button refreshHistory = CreateButton("Actualizar historial", Indigo);
    private CancellationTokenSource? kioskCancellation;
    private CancellationTokenSource? searchCancellation;
    private Task? kioskTask;
    private bool isUpdatingEmployees;
    private EmployeeOption? selectedEmployeeOption;
    private EmployeeOption? pendingEnrollment;
    private IReadOnlyDictionary<int, EmployeeOption> employeesById = new Dictionary<int, EmployeeOption>();

    public BridgeForm()
    {
        Text = "Crepe · Checador biométrico";
        BackColor = Background;
        ClientSize = new Size(1024, 720);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 11);
        cancelEnrollment.Visible = false;

        var root = new TableLayoutPanel { BackColor = Background, ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        root.Controls.Add(CreateBrandHeader(), 0, 0);
        tabs.TabPages.Add(CreateTab("Checador", CreateReaderCard()));
        tabs.TabPages.Add(CreateTab("Registrar huellas", CreateEnrollmentCard()));
        tabs.TabPages.Add(CreateTab("Historial", CreateHistoryCard()));
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(CreateStatusCard(), 0, 2);
        Controls.Add(root);

        employeeSearch.TextChanged += async (_, _) => await BeginEmployeeSearchAsync();
        employeeList.SelectedIndexChanged += (_, _) => UpdateSelectedEmployee();
        enroll.Click += (_, _) => ArmEnrollment();
        cancelEnrollment.Click += (_, _) => CancelEnrollment();
        unenroll.Click += async (_, _) => await UnenrollAsync();
        refreshHistory.Click += async (_, _) => await LoadHistoryAsync();
        settings.Click += (_, _) => EditSettings();
        tabs.DrawItem += DrawTab;
        tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (tabs.SelectedIndex == 2)
            {
                await LoadHistoryAsync();
            }
        };
        Shown += async (_, _) =>
        {
            await QueueEmployeeSearchAsync();
            StartKiosk();
        };
        FormClosing += (_, _) =>
        {
            StopKiosk();
            fingerprintPreview.Image?.Dispose();
        };
    }

    private Control CreateBrandHeader()
    {
        var header = new Panel { BackColor = Navy, Dock = DockStyle.Fill, Padding = new Padding(20, 13, 20, 13) };
        var brand = new Label { AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI Semibold", 18), ForeColor = Color.White, Text = "CREPE" };
        var subtitle = new Label { AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(255, 213, 223), Padding = new Padding(14, 7, 0, 0), Text = "CHECADOR BIOMÉTRICO" };
        var device = new Label { AutoSize = true, Dock = DockStyle.Right, Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.FromArgb(198, 247, 222), Padding = new Padding(0, 7, 0, 0), Text = "●  HID 4500 LISTO" };
        header.Controls.Add(device);
        header.Controls.Add(subtitle);
        header.Controls.Add(brand);

        return header;
    }

    private Control CreateEnrollmentCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(0, 0, 12, 0);
        var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 10 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Navy, Text = "Registrar colaborador" }, 0, 0);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = Color.FromArgb(83, 95, 114), Text = "Busca o toca un nombre para registrar su huella." }, 0, 1);
        layout.Controls.Add(employeeSearch, 0, 2);
        layout.Controls.Add(employeeList, 0, 3);
        layout.Controls.Add(selectedEmployee, 0, 4);
        layout.Controls.Add(enroll, 0, 5);
        layout.Controls.Add(unenroll, 0, 6);
        layout.Controls.Add(cancelEnrollment, 0, 7);
        layout.Controls.Add(settings, 0, 8);
        layout.Controls.Add(registrationNotice, 0, 9);
        card.Controls.Add(layout);
        return card;
    }

    private Control CreateReaderCard()
    {
        var card = CreateCard();
        card.Margin = new Padding(12, 0, 0, 0);
        var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(38), RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Navy, Text = "Punto de checada" }, 0, 0);
        var previewPanel = new Panel { BackColor = RoseSoft, Dock = DockStyle.Fill, Margin = new Padding(26, 26, 26, 20) };
        previewPanel.Controls.Add(fingerprintIcon);
        previewPanel.Controls.Add(fingerprintPreview);
        layout.Controls.Add(previewPanel, 0, 1);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = Success, Font = new Font("Segoe UI Semibold", 12), Text = "●  AUTOMÁTICO", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill }, 0, 2);
        layout.Controls.Add(statusTitle, 0, 3);
        layout.Controls.Add(statusDetail, 0, 4);
        card.Controls.Add(layout);
        return card;
    }

    private static TabPage CreateTab(string title, Control content)
    {
        var tab = new TabPage(title) { BackColor = Background, Padding = new Padding(0, 12, 0, 0), UseVisualStyleBackColor = false };
        content.Dock = DockStyle.Fill;
        tab.Controls.Add(content);

        return tab;
    }

    private void DrawTab(object? sender, DrawItemEventArgs eventArgs)
    {
        var isSelected = eventArgs.Index == tabs.SelectedIndex;
        var bounds = eventArgs.Bounds;
        using var background = new SolidBrush(isSelected ? RoseSoft : Background);
        using var textBrush = new SolidBrush(isSelected ? Indigo : Color.FromArgb(83, 95, 114));
        eventArgs.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(eventArgs.Graphics, tabs.TabPages[eventArgs.Index].Text, tabs.Font, bounds, textBrush.Color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (isSelected)
        {
            using var accent = new SolidBrush(Indigo);
            eventArgs.Graphics.FillRectangle(accent, new Rectangle(bounds.X + 16, bounds.Bottom - 3, Math.Max(0, bounds.Width - 32), 3));
        }
    }

    private Control CreateHistoryCard()
    {
        history.Columns.Add("Fecha", 130, HorizontalAlignment.Left);
        history.Columns.Add("Colaborador", 360, HorizontalAlignment.Left);
        history.Columns.Add("Entrada", 150, HorizontalAlignment.Center);
        history.Columns.Add("Salida", 150, HorizontalAlignment.Center);

        var card = CreateCard();
        var layout = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = new Padding(30), RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Navy, Text = "Historial de checadas" }, 0, 0);
        layout.Controls.Add(new Label { AutoSize = true, ForeColor = Color.FromArgb(83, 95, 114), Text = "Últimos 50 registros de entrada y salida guardados en Crepe local." }, 0, 1);
        layout.Controls.Add(history, 0, 2);
        layout.Controls.Add(refreshHistory, 0, 3);
        card.Controls.Add(layout);

        return card;
    }

    private Control CreateStatusCard()
    {
        var panel = new Panel { BackColor = Navy, Dock = DockStyle.Fill, Padding = new Padding(22, 12, 22, 12) };
        panel.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(207, 216, 232), Text = "Las plantillas se cifran en esta computadora. La imagen mostrada no se almacena.", Dock = DockStyle.Right, TextAlign = ContentAlignment.MiddleRight });
        panel.Controls.Add(new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 11), ForeColor = Color.White, Text = "CREPE  /  CHECADOR BIOMÉTRICO", Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft });
        return panel;
    }

    private async Task BeginEmployeeSearchAsync()
    {
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

            isUpdatingEmployees = true;
            employeeList.BeginUpdate();
            employeeList.Items.Clear();
            employeeList.Items.AddRange(employees.Cast<object>().ToArray());
            employeeList.EndUpdate();
            employeesById = employees.ToDictionary(employee => employee.Id);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetStatus("No se pudo consultar al equipo", exception.Message, Danger);
        }
        finally
        {
            isUpdatingEmployees = false;
        }
    }

    private void UpdateSelectedEmployee()
    {
        if (isUpdatingEmployees || employeeList.SelectedItem is not EmployeeOption employee)
        {
            return;
        }

        selectedEmployeeOption = employee;
        selectedEmployee.Text = $"Seleccionado: {employee.Name}{(string.IsNullOrWhiteSpace(employee.Position) ? string.Empty : $" · {employee.Position}")}";
        SetStatus("Listo para registrar", $"Pulsa Registrar huella para preparar a {employee.Name}.", Indigo);
        SetRegistrationNotice($"Listo para registrar la huella de {employee.Name}.", Indigo);
    }

    private void ArmEnrollment()
    {
        if (selectedEmployeeOption is not { } employee)
        {
            SetStatus("Selecciona un nombre de la lista antes de registrar", "Busca al colaborador y tócala en la lista.", Danger);
            return;
        }

        pendingEnrollment = employee;
        cancelEnrollment.Visible = true;
        enroll.Enabled = false;
        employeeList.Enabled = false;
        employeeSearch.Enabled = false;
        SetStatus("Esperando huella", $"{employee.Name}: coloca el dedo. El lector ya está activo.", Indigo);
        SetRegistrationNotice($"Esperando la huella de {employee.Name}…", Indigo);
    }

    private void CancelEnrollment()
    {
        pendingEnrollment = null;
        cancelEnrollment.Visible = false;
        SetButtonsEnabled(true);
        SetStatus("Lector activo", "Registro cancelado. Coloca un dedo para registrar entrada o salida.", Indigo);
        SetRegistrationNotice("Registro cancelado. Selecciona otra persona si quieres intentarlo de nuevo.", Indigo);
    }

    private async Task UnenrollAsync()
    {
        if (selectedEmployeeOption is not { } employee)
        {
            MessageBox.Show(this, "Selecciona un colaborador de la lista.", "Eliminar huella", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"¿Eliminar la huella registrada de {employee.Name}? El colaborador y su historial de asistencia no se borrarán.",
            "Confirmar eliminación de huella",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            await new CrepeClient(BridgeSettingsStore.Read()).UnenrollAsync(employee.Id);
            vault.DeleteEmployee(employee.Id);
            pendingEnrollment = null;
            cancelEnrollment.Visible = false;
            SetButtonsEnabled(true);
            selectedEmployee.Text = $"Huella eliminada: {employee.Name}. Ya puedes registrarla de nuevo.";
            SetRegistrationNotice($"Se eliminó la huella de {employee.Name}.", Success);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "No se pudo eliminar la huella", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadHistoryAsync()
    {
        refreshHistory.Enabled = false;

        try
        {
            var records = await new CrepeClient(BridgeSettingsStore.Read()).HistoryAsync(CancellationToken.None);
            history.BeginUpdate();
            history.Items.Clear();

            foreach (var record in records)
            {
                history.Items.Add(new ListViewItem([
                    record.WorkDate,
                    record.EmployeeName,
                    record.CheckedInAt ?? "—",
                    record.CheckedOutAt ?? "Pendiente",
                ]));
            }

            history.EndUpdate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "No se pudo cargar el historial", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            refreshHistory.Enabled = true;
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
            var currentEmployeeName = "Colaborador";

            try
            {
                if (pendingEnrollment is { } employee)
                {
                    SetStatus("Esperando huella", $"{employee.Name}: coloca el dedo. El lector ya está activo.", Indigo);
                }
                else
                {
                    SetStatus("Lector activo", "Coloca un dedo para registrar entrada o salida.", Indigo);
                }

                var capture = await FingerprintCapture.CaptureAsync(cancellationToken);
                ShowPreview(capture.Preview);

                if (pendingEnrollment is { } employeeToEnroll)
                {
                    await CompleteEnrollmentAsync(employeeToEnroll, capture);
                    continue;
                }

                var match = vault.Find(capture.Template);
                if (match is null || !match.IsRecognized)
                {
                    SetStatus("Huella no reconocida", "Pide a recepción que registre la huella del colaborador.", Danger);
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }

                currentEmployeeName = EmployeeNameFor(match.Template);
                var record = await new CrepeClient(BridgeSettingsStore.Read()).RegisterAsync(match.Template.TemplateId);
                var checkLabel = record.Status == "checked_in" ? "Entrada registrada" : "Salida registrada";
                SetStatus(record.EmployeeName, $"{checkLabel} · {record.CheckedAt}", Success);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (CrepeBridgeException exception) when (exception.StatusCode == 422)
            {
                SetStatus("Checada ya completada", $"{currentEmployeeName} · {exception.Message}", Color.FromArgb(190, 126, 24));
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception exception)
            {
                SetStatus("No se pudo leer la huella", exception.Message, Danger);
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
    }

    private async Task CompleteEnrollmentAsync(EmployeeOption employee, CapturedFingerprint capture)
    {
        var templateId = Guid.NewGuid();

        try
        {
            vault.Save(templateId, employee, [capture.Template]);
            var message = await new CrepeClient(BridgeSettingsStore.Read()).EnrollAsync(employee.Id, templateId);
            SetStatus("Huella registrada", $"{employee.Name} · {message}", Success);
            SetRegistrationNotice($"Se ha guardado la huella de {employee.Name}.", Success);
        }
        catch (Exception exception)
        {
            vault.Delete(templateId);
            SetStatus("No se pudo registrar la huella", $"{employee.Name} · {exception.Message}", Danger);
            SetRegistrationNotice($"No se pudo guardar la huella de {employee.Name}: {exception.Message}", Danger);
        }
        finally
        {
            if (pendingEnrollment?.Id == employee.Id)
            {
                pendingEnrollment = null;
            }

            cancelEnrollment.Visible = false;
            SetButtonsEnabled(true);
        }
    }

    private string EmployeeNameFor(StoredTemplate template)
    {
        return template.EmployeeName
            ?? employeesById.GetValueOrDefault(template.EmployeeId)?.Name
            ?? $"Colaborador #{template.EmployeeId}";
    }

    private void ShowPreview(Bitmap preview)
    {
        var previous = fingerprintPreview.Image;
        fingerprintPreview.Image = preview;
        fingerprintIcon.Visible = false;
        previous?.Dispose();
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
        unenroll.Enabled = enabled;
        settings.Enabled = enabled;
        employeeSearch.Enabled = enabled;
        employeeList.Enabled = enabled;
    }

    private void SetStatus(string title, string detail, Color color)
    {
        statusTitle.Text = title;
        statusTitle.ForeColor = color;
        statusDetail.Text = detail;
        fingerprintIcon.ForeColor = color;
    }

    private void SetRegistrationNotice(string message, Color color)
    {
        registrationNotice.Text = message;
        registrationNotice.ForeColor = color;
    }

    private static Button CreateButton(string text, Color backColor, Color? foreColor = null)
    {
        return new Button { AutoSize = false, BackColor = backColor, Cursor = Cursors.Hand, FlatAppearance = { BorderColor = RoseBorder, BorderSize = 1, MouseDownBackColor = backColor, MouseOverBackColor = backColor }, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 12), ForeColor = foreColor ?? Color.White, Height = 48, Text = text, Width = 440 };
    }

    private static Panel CreateCard()
    {
        return new Panel { BackColor = Surface, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
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
