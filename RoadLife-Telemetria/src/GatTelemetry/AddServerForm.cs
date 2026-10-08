using System.Drawing;
using System.Windows.Forms;

namespace GatTelemetry;

internal sealed class AddServerForm : Form
{
	private readonly TextBox txtName;

	private readonly TextBox txtEndpoint;

	public string ServerName => txtName.Text;

	public string Endpoint => txtEndpoint.Text;

	public AddServerForm()
	{
		Text = "Adicionar servidor";
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MinimizeBox = false;
		MaximizeBox = false;
		ClientSize = new Size(510, 180);
		Controls.Add(new Label
		{
			Text = "Nome (opcional):",
			Left = 18,
			Top = 20,
			Width = 130
		});
		txtName = new TextBox
		{
			Left = 150,
			Top = 17,
			Width = 335
		};
		Controls.Add(txtName);
		Controls.Add(new Label
		{
			Text = "Endereço do servidor:",
			Left = 18,
			Top = 60,
			Width = 130
		});
		txtEndpoint = new TextBox
		{
			Left = 150,
			Top = 57,
			Width = 335
		};
		Controls.Add(txtEndpoint);
		Controls.Add(new Label
		{
			Text = "Ex.: https://nome.ts.net",
			Left = 150,
			Top = 86,
			Width = 335,
			ForeColor = Color.Gray
		});
		Button button = new Button
		{
			Text = "ADICIONAR",
			DialogResult = DialogResult.OK,
			Left = 285,
			Top = 125,
			Width = 96
		};
		Button button2 = new Button
		{
			Text = "CANCELAR",
			DialogResult = DialogResult.Cancel,
			Left = 389,
			Top = 125,
			Width = 96
		};
		Controls.Add(button);
		Controls.Add(button2);
		AcceptButton = button;
		CancelButton = button2;
	}
}
