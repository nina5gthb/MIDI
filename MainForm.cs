using System.Drawing;
using System.Windows.Forms;

namespace MidiShortcuts;

public class MainForm : Form
{
    private readonly ComboBox _portCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, FlatStyle = FlatStyle.System };
    private readonly Button _refreshBtn = new() { Text = "Actualizar", AutoSize = true };
    private readonly Button _startBtn = new() { Text = "Iniciar escucha", AutoSize = true };
    private readonly Label _statusLbl = new() { Text = "Detenido", ForeColor = Color.Red, AutoSize = true, Padding = new Padding(6, 8, 0, 0) };
    private readonly Label _lastPadLbl = new() { Text = "", ForeColor = Color.Blue, AutoSize = true, Padding = new Padding(6, 8, 0, 0) };

    private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, GridLines = true };
    private readonly Button _newBtn = new() { Text = "Nuevo atajo", AutoSize = true };
    private readonly Button _delBtn = new() { Text = "Eliminar seleccionado", AutoSize = true };

    private readonly Label _infoLbl = new()
    {
        Text = "Al cerrar la ventana (X) la app sigue escuchando en la bandeja del sistema. Para salir del todo, click derecho en el icono de la bandeja > Salir.",
        ForeColor = Color.Gray,
        AutoSize = true,
        Dock = DockStyle.Bottom,
        Padding = new Padding(8),
    };

    private readonly System.Windows.Forms.Timer _watchdog = new() { Interval = 2000 };
    private readonly NotifyIcon _trayIcon = new();

    private AppConfig _cfg = AppConfig.Load();
    private MidiListener? _listener;
    private string[] _deviceNames = Array.Empty<string>();
    private string? _listeningPortName;

    // True solo cuando el usuario pulsa "Detener escucha" a proposito;
    // evita que el watchdog reconecte solo si el usuario quiso parar.
    private bool _manualStop;
    private DateTime _nextRetryAt = DateTime.MinValue;

    // True solo cuando se elige "Salir" desde el icono de la bandeja;
    // distingue un cierre real de solo minimizar a la bandeja al darle a la X.
    private bool _reallyExit;

    // Si es true, la ventana nunca se muestra al arrancar: se va directo
    // a la bandeja (usado por instalar_inicio.ps1 con --minimized).
    private bool _startHidden;

    public MainForm(bool startHidden = false)
    {
        _startHidden = startHidden;
        Text = "MIDI Shortcuts - Starrypad mini";
        Width = 720;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        BuildTrayIcon();
        RefreshPorts();
        RefreshTable();

        FormClosing += OnFormClosing;

        _watchdog.Tick += (_, _) => WatchdogTick();
        _watchdog.Start();
        // Primer chequeo justo cuando la ventana ya esta lista (Load se
        // dispara despues de crear el handle), asi la escucha arranca
        // sola apenas se abre la ventana en vez de esperar el intervalo
        // completo del timer.
        Load += (_, _) => WatchdogTick();
    }

    /// <summary>
    /// Evita que la ventana llegue a mostrarse la primera vez cuando se
    /// arranca con --minimized (ej. al iniciar sesion en Windows), sin
    /// depender de la propiedad WindowStyle del acceso directo, que
    /// WinForms no siempre respeta.
    /// </summary>
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
        _startHidden = false; // solo aplica la primera vez
    }

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApplication());

        _trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        _trayIcon.Text = "MIDI Shortcuts";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
        {
            // Le dan a la X: se oculta a la bandeja pero sigue escuchando.
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(
                2000, "MIDI Shortcuts",
                "Sigue escuchando en segundo plano. Click derecho en el icono de la bandeja para salir.",
                ToolTipIcon.Info);
            return;
        }

        // Cierre real (se eligio "Salir" desde la bandeja): apaga todo.
        _watchdog.Stop();
        StopListening();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        top.Controls.Add(new Label { Text = "Puerto MIDI:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        top.Controls.Add(_portCombo);
        top.Controls.Add(_refreshBtn);
        top.Controls.Add(_startBtn);
        top.Controls.Add(_statusLbl);
        top.Controls.Add(_lastPadLbl);

        _list.Columns.Add("Pad / Control", 180);
        _list.Columns.Add("Tipo de accion", 120);
        _list.Columns.Add("Accion", 320);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        bottom.Controls.Add(_newBtn);
        bottom.Controls.Add(_delBtn);

        // El orden importa para el docking: se agrega primero lo que va al
        // centro (Fill) y al final lo que va arriba/abajo, para que quede
        // bien acomodado.
        Controls.Add(_list);
        Controls.Add(bottom);
        Controls.Add(_infoLbl);
        Controls.Add(top);

        _refreshBtn.Click += (_, _) => RefreshPorts();
        _startBtn.Click += (_, _) => ToggleListen();
        _newBtn.Click += (_, _) => OpenNewShortcutDialog();
        _delBtn.Click += (_, _) => DeleteSelected();
    }

    private void RefreshPorts()
    {
        _deviceNames = MidiInputDevice.GetDeviceNames();
        var selected = _portCombo.SelectedItem as string;

        _portCombo.Items.Clear();
        _portCombo.Items.AddRange(_deviceNames);

        if (selected != null && Array.IndexOf(_deviceNames, selected) >= 0)
        {
            _portCombo.SelectedItem = selected;
        }
        else
        {
            var chosen = DeviceFinder.FindPreferredDevice(_deviceNames, _cfg.PortName);
            if (chosen != null) _portCombo.SelectedItem = chosen;
        }
    }

    private void RefreshTable()
    {
        _list.Items.Clear();
        foreach (var kv in _cfg.Mappings)
        {
            var item = new ListViewItem(MappingHelper.DescribeKey(kv.Key)) { Tag = kv.Key };
            item.SubItems.Add(kv.Value.Type);
            item.SubItems.Add(kv.Value.Value);
            _list.Items.Add(item);
        }
    }

    // ------------------------------------------------------------------
    // Iniciar / detener / vigilar la conexion
    // ------------------------------------------------------------------

    private void ToggleListen()
    {
        if (_listener == null)
        {
            var portName = _portCombo.SelectedItem as string;
            if (portName == null)
            {
                MessageBox.Show(this, "Selecciona primero un puerto MIDI.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _manualStop = false;
            StartListen(portName, silent: false);
        }
        else
        {
            _manualStop = true;
            StopListening();
            _statusLbl.Text = "Detenido";
            _statusLbl.ForeColor = Color.Red;
            _lastPadLbl.Text = "";
            _startBtn.Text = "Iniciar escucha";
        }
    }

    private void StartListen(string portName, bool silent)
    {
        int deviceId = Array.IndexOf(_deviceNames, portName);
        if (deviceId < 0) return;

        // Le pide a una instancia --background (si existe) que libere el puerto.
        BackgroundSignal.RequestStop();

        _cfg.PortName = portName;
        _cfg.Save();

        var listener = new MidiListener();
        listener.Trigger += OnTrigger;
        listener.AnyMessage += OnAnyMessage;
        listener.Disconnected += OnDisconnected;

        if (!listener.Start(deviceId))
        {
            _nextRetryAt = DateTime.UtcNow.AddSeconds(15);
            listener.Dispose();
            if (silent)
            {
                Logger.Log("No se pudo reconectar automaticamente: puerto ocupado.");
                _statusLbl.Text = "Reconectando...";
                _statusLbl.ForeColor = Color.Orange;
            }
            else
            {
                MessageBox.Show(this,
                    "No se pudo abrir el puerto MIDI.\n\n" +
                    "Cierra el servicio en segundo plano (MidiShortcuts.exe en el Administrador de tareas) " +
                    "o reinicia el PC, y vuelve a intentarlo.",
                    "Puerto MIDI ocupado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLbl.Text = "Error de puerto";
                _statusLbl.ForeColor = Color.Red;
                _startBtn.Text = "Iniciar escucha";
            }
            return;
        }

        _listener = listener;
        _listeningPortName = portName;
        _statusLbl.Text = "Escuchando...";
        _statusLbl.ForeColor = Color.Green;
        _startBtn.Text = "Detener escucha";
    }

    private void StopListening()
    {
        if (_listener == null) return;
        _listener.Trigger -= OnTrigger;
        _listener.AnyMessage -= OnAnyMessage;
        _listener.Disconnected -= OnDisconnected;
        _listener.Dispose();
        _listener = null;
        _listeningPortName = null;
    }

    private void OnDisconnected()
    {
        // Este evento puede llegar desde el hilo de winmm.
        if (InvokeRequired) { BeginInvoke(new Action(OnDisconnected)); return; }
        Logger.Log("Dispositivo MIDI cerrado (posible corte de Bluetooth).");
        StopListening();
        if (!_manualStop)
        {
            _statusLbl.Text = "Reconectando...";
            _statusLbl.ForeColor = Color.Orange;
            _lastPadLbl.Text = "";
        }
    }

    private void WatchdogTick()
    {
        // Ademas de la notificacion MIM_CLOSE, se revisa que el puerto
        // siga en la lista, por si Windows no llega a avisar el cierre.
        if (_listener != null && _listeningPortName != null)
        {
            var currentNames = MidiInputDevice.GetDeviceNames();
            if (Array.IndexOf(currentNames, _listeningPortName) < 0)
            {
                OnDisconnected();
            }
            return;
        }

        if (_manualStop) return;
        if (DateTime.UtcNow < _nextRetryAt) return;

        TryAutoConnect();
    }

    private void TryAutoConnect()
    {
        _deviceNames = MidiInputDevice.GetDeviceNames();
        if (_deviceNames.Length == 0) return;

        var selected = _portCombo.SelectedItem as string;
        _portCombo.Items.Clear();
        _portCombo.Items.AddRange(_deviceNames);
        if (selected != null && Array.IndexOf(_deviceNames, selected) >= 0) _portCombo.SelectedItem = selected;

        var chosen = (_cfg.PortName != null && Array.IndexOf(_deviceNames, _cfg.PortName) >= 0)
            ? _cfg.PortName
            : DeviceFinder.FindPreferredDevice(_deviceNames, _cfg.PortName);
        if (chosen == null) return;

        _portCombo.SelectedItem = chosen;
        StartListen(chosen, silent: true);
    }

    // ------------------------------------------------------------------
    // Callbacks del listener (pueden llegar desde el hilo de winmm)
    // ------------------------------------------------------------------

    private void OnAnyMessage(string key)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(OnAnyMessage), key); return; }
        _lastPadLbl.Text = $"Ultimo pad: {MappingHelper.DescribeKey(key)}";
    }

    private DateTime _cfgLoadedAt = DateTime.MinValue;

    private void OnTrigger(string key)
    {
        // Antes esto releia config.json en CADA pulsacion, lo que podia
        // coincidir con una escritura a medias (ej. justo estas guardando
        // un atajo nuevo) y traer un config vacio que despues se guardaba
        // encima, borrando todo. Ahora solo se relee si el archivo
        // realmente cambio desde la ultima vez, y si algo falla se sigue
        // usando la copia que ya esta en memoria en vez de reemplazarla.
        ReloadConfigIfChanged();
        var action = MappingHelper.FindMapping(_cfg.Mappings, key);
        if (action == null) return;
        try { action.Execute(); }
        catch (Exception ex) { Logger.Log($"Error ejecutando accion: {ex.Message}"); }
    }

    private void ReloadConfigIfChanged()
    {
        try
        {
            if (!File.Exists(AppConfig.ConfigPath)) return;
            var lastWrite = File.GetLastWriteTimeUtc(AppConfig.ConfigPath);
            if (lastWrite <= _cfgLoadedAt) return;

            var fresh = AppConfig.Load();
            _cfg = fresh;
            _cfgLoadedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            // Si algo falla al revisar/leer, se sigue usando la config que
            // ya esta cargada en memoria en vez de arriesgarse a perderla.
            Logger.Log($"No se pudo revisar cambios en config.json, se sigue usando la copia en memoria: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Gestion de atajos
    // ------------------------------------------------------------------

    private void DeleteSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        foreach (ListViewItem item in _list.SelectedItems)
        {
            if (item.Tag is string key) _cfg.Mappings.Remove(key);
        }
        _cfg.Save();
        RefreshTable();
    }

    private void OpenNewShortcutDialog()
    {
        if (_listener == null)
        {
            MessageBox.Show(this, "Primero pulsa 'Iniciar escucha' arriba para poder capturar un pad.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new NewShortcutForm(_listener);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.CapturedKey != null)
        {
            _cfg.Mappings[dlg.CapturedKey] = new ShortcutAction { Type = dlg.SelectedType, Value = dlg.SelectedValue };
            _cfg.Save();
            RefreshTable();
        }
    }
}
