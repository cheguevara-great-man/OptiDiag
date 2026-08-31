using System.Globalization;
using System.Windows;
using OptiDiag.Application;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.App;

public enum RegisterDialogAction
{
    None,
    Read,
    Write
}

public partial class RegisterAccessDialog : Window
{
    private readonly bool _canWrite;

    public RegisterAccessDialog(RegisterValue register)
    {
        InitializeComponent();
        Register = register;
        var assessment = RegisterWritePolicy.Assess(register);
        _canWrite = assessment.IsAllowed;
        AddressText.Text = register.AddressText;
        CurrentValueText.Text = $"0x{register.HexValue}";
        AccessText.Text = AccessDisplay(register.Access);
        RiskText.Text = $"{assessment.Risk} · {assessment.Title}：{assessment.Reason}";
        NewValueBox.Text = register.HexValue;
        NewValueBox.IsReadOnly = !_canWrite;
        WriteUnlockCheckBox.IsEnabled = _canWrite;
        NewValueBox.SelectAll();
        Loaded += (_, _) => NewValueBox.Focus();
    }

    public RegisterValue Register { get; }

    public RegisterDialogAction RequestedAction { get; private set; }

    public string RequestedHexValue { get; private set; } = string.Empty;

    public bool WriteUnlocked => WriteUnlockCheckBox.IsChecked == true;

    private void ReadButton_Click(object sender, RoutedEventArgs e)
    {
        RequestedAction = RegisterDialogAction.Read;
        DialogResult = true;
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        var normalized = NormalizeHex(NewValueBox.Text);
        if (!byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            MessageBox.Show(
                this,
                "请输入 00–FF 范围内的两位十六进制数。",
                "无效的寄存器值",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        RequestedAction = RegisterDialogAction.Write;
        RequestedHexValue = value.ToString("X2", CultureInfo.InvariantCulture);
        DialogResult = true;
    }

    private void WriteUnlockCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateWriteButton();

    private void NewValueBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        UpdateWriteButton();

    private void UpdateWriteButton()
    {
        if (WriteButton is null || NewValueBox is null || WriteUnlockCheckBox is null)
        {
            return;
        }

        var normalized = NormalizeHex(NewValueBox.Text);
        WriteButton.IsEnabled = _canWrite
            && WriteUnlockCheckBox.IsChecked == true
            && byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }

    private static string NormalizeHex(string value) =>
        value.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string AccessDisplay(RegisterAccess access) => access switch
    {
        RegisterAccess.ReadOnly => "只读（禁止写入）",
        RegisterAccess.ReadOnlyClearOnRead => "只读/读清除（禁止写入）",
        RegisterAccess.ReadWrite => "可读写",
        RegisterAccess.ReadWriteSelfClearing => "读写/写 1 自清除",
        RegisterAccess.WriteOnly => "只写",
        RegisterAccess.WriteOnlySelfClearing => "只写/写 1 自清除",
        RegisterAccess.Mixed => "混合字段（请使用协议操作）",
        RegisterAccess.Reserved => "保留（禁止写入）",
        RegisterAccess.VendorSpecific => "厂商定义（需要高风险二次确认）",
        _ => access.ToString()
    };
}
