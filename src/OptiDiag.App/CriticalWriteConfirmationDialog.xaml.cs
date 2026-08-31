using System.Windows;
using System.Windows.Controls;
using OptiDiag.Application;
using OptiDiag.Protocols.Abstractions;

namespace OptiDiag.App;

public partial class CriticalWriteConfirmationDialog : Window
{
    private readonly string _phrase;

    public CriticalWriteConfirmationDialog(
        RegisterValue register,
        string requestedHexValue,
        RegisterWriteAssessment assessment)
    {
        InitializeComponent();
        _phrase = $"WRITE {requestedHexValue.ToUpperInvariant()}";
        TitleText.Text = assessment.Title;
        DetailText.Text =
            $"地址：{register.AddressText}\n当前值：0x{register.HexValue}\n新值：0x{requestedHexValue.ToUpperInvariant()}\n\n"
            + assessment.Reason;
        PromptText.Text = $"若已核对协议和目标模块，请输入 {_phrase}：";
        Loaded += (_, _) => ConfirmationBox.Focus();
    }

    private void ConfirmationBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ConfirmButton.IsEnabled = string.Equals(
            ConfirmationBox.Text.Trim(),
            _phrase,
            StringComparison.OrdinalIgnoreCase);

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
