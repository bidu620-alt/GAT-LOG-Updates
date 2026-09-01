using System.Drawing;
using System.Windows.Forms;

namespace GatLogServer;

internal sealed class LoginForm : Form
{
	private readonly TextBox _user = new TextBox();

	private readonly TextBox _pass = new TextBox();

	private readonly Label _error = new Label();

	public LoginForm()
	{
		Text = "GAT-LOG Server | Login";
		base.ClientSize = new Size(430, 300);
		base.StartPosition = FormStartPosition.CenterScreen;
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		BackColor = Color.FromArgb(5, 31, 47);
		ForeColor = Color.White;
		Font = new Font("Segoe UI", 10f);
		Label value = new Label
		{
			Text = "GAT-LOG SERVER",
			Font = new Font("Segoe UI", 22f, FontStyle.Bold),
			ForeColor = Color.White,
			AutoSize = true,
			Location = new Point(72, 34)
		};
		base.Controls.Add(value);
		base.Controls.Add(MakeLabel("Usuário", 55, 112));
		_user.SetBounds(145, 108, 225, 30);
		_user.Text = AuthService.EnsureAuth().User;
		base.Controls.Add(_user);
		base.Controls.Add(MakeLabel("Senha", 55, 157));
		_pass.SetBounds(145, 153, 225, 30);
		_pass.UseSystemPasswordChar = true;
		base.Controls.Add(_pass);
		Button button = new Button
		{
			Text = "ENTRAR",
			Bounds = new Rectangle(145, 205, 225, 42),
			BackColor = Color.FromArgb(28, 111, 211),
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Font = new Font("Segoe UI", 10f, FontStyle.Bold)
		};
		button.FlatAppearance.BorderSize = 0;
		button.Click += delegate
		{
			DoLogin();
		};
		base.Controls.Add(button);
		base.AcceptButton = button;
		_error.SetBounds(55, 255, 320, 25);
		_error.ForeColor = Color.FromArgb(255, 95, 95);
		_error.TextAlign = ContentAlignment.MiddleCenter;
		base.Controls.Add(_error);
	}

	private Label MakeLabel(string text, int x, int y)
	{
		return new Label
		{
			Text = text,
			AutoSize = true,
			Location = new Point(x, y + 5),
			ForeColor = Color.FromArgb(210, 230, 242)
		};
	}

	private void DoLogin()
	{
		if (AuthService.Verify(_user.Text, _pass.Text))
		{
			base.DialogResult = DialogResult.OK;
			Close();
		}
		else
		{
			_error.Text = "Usuário ou senha incorretos.";
			_pass.SelectAll();
			_pass.Focus();
		}
	}
}
