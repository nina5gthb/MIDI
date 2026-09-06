using System.Drawing;
using System.Windows.Forms;

namespace MidiShortcuts;

public class NewShortcutForm : Form
{
    private readonly MidiListener _listener;

    private readonly Label _targetLbl = new() { Text = "(sin capturar)", AutoSize = true, Left = 20, Top = 40 };
    private readonly Button _captureBtn = new() { Text = "Capturar", Left = 230, Top = 36, Width = 90 };

    private readonly RadioButton _rbTecla = new() { Text = "Tecla/atajo", Checked = true, AutoSize = true, Left = 20, Top = 100 };
    private readonly RadioButton _rbTexto = new() { Text = "Escribir texto", AutoSize = true, Left = 150, Top = 100 };
    private readonly RadioButton _rbApp = new() { Text = "Abrir programa", AutoSize = true, Left = 20, Top = 125 };
    private readonly RadioButton _rbCarpeta = new() { Text = "Abrir carpeta", AutoSize = true, Left = 150, Top = 125 };

    private readonly TextBox _valueBox = new() { Left = 20, Top = 190, Width = 270 };
    private readonly Button _browseBtn = new() { Text = "...", Left = 296, Top = 188, Width = 34 };
    private readonly Button _saveBtn = new() { Text = "Guardar atajo", Left = 150, Top = 255, Width = 120 };

    public string? CapturedKey { get; private set; }

    public string SelectedType =>
        _rbTecla.Checked ? "tecla" :
        _rbTexto.Checked ? "texto" :
        _rbCarpeta.Checked ? "carpeta" : "app";

    public string SelectedValue => _valueBox.Text;

    public NewShortcutForm(MidiListener listener)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));

        Text = "Nuevo atajo";
        Width = 440;
        Height = 345;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();

        _listener.Learned += OnLearned;
        FormClosed += (_, _) => _listener.Learned -= OnLearned;
    }

    private void BuildUi()
    {
        Controls.Add(new Label { Text = "1. Pulsa 'Capturar' y luego toca el pad/knob:", AutoSize = true, Left = 20, Top = 14 });
        Controls.Add(_targetLbl);
        Controls.Add(_captureBtn);

        Controls.Add(new Label { Text = "2. Tipo de accion:", AutoSize = true, Left = 20, Top = 75 });
        Controls.Add(_rbTecla);
        Controls.Add(_rbTexto);
        Controls.Add(_rbApp);
        Controls.Add(_rbCarpeta);

        Controls.Add(new Label { Text = "3. Valor:", AutoSize = true, Left = 20, Top = 165 });
        Controls.Add(_valueBox);
        Controls.Add(_browseBtn);

        Controls.Add(new Label
        {
            Text = "Ejemplos tecla: ctrl+c , alt+tab , win+d , ctrl+shift+esc",
            ForeColor = Color.Gray,
            AutoSize = true,
            Left = 20,
            Top = 220,
        });

        Controls.Add(_saveBtn);
        AcceptButton = _saveBtn;

        _captureBtn.Click += (_, _) =>
        {
            _targetLbl.Text = "Toca el pad ahora...";
            _listener.StartLearning();
        };

        // El boton "..." se adapta segun el tipo elegido: selector de
        // carpeta si es "Abrir carpeta", selector de archivo para los
        // demas (para tecla/texto no aplica mucho, pero no estorba).
        _browseBtn.Click += (_, _) =>
        {
            if (_rbCarpeta.Checked)
            {
                using var fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog(this) == DialogResult.OK) _valueBox.Text = fbd.SelectedPath;
            }
            else
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Programas y accesos directos (*.exe;*.lnk)|*.exe;*.lnk|Todos los archivos (*.*)|*.*",
                    Title = "Selecciona un programa o acceso directo",
                };
                if (ofd.ShowDialog(this) == DialogResult.OK) _valueBox.Text = ofd.FileName;
            }
        };

        _saveBtn.Click += (_, _) =>
        {
            if (CapturedKey == null)
            {
                MessageBox.Show(this, "Primero captura el pad/knob.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(_valueBox.Text))
            {
                MessageBox.Show(this, "Escribe un valor para la accion.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private void OnLearned(string key)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(OnLearned), key); return; }
        CapturedKey = key;
        _targetLbl.Text = MappingHelper.DescribeKey(key);
    }
}
