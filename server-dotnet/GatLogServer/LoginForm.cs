using System;
using System.Drawing;
using System.Windows.Forms;

namespace GatLogServer
{
    internal sealed class LoginForm : Form
    {
        private readonly TextBox _user = new TextBox();
        private readonly TextBox _pass = new TextBox();
        private readonly Label _error = new Label();

        public LoginForm()
        {
            Text = "GAT-LOG Server | Login";
            ClientSize = new Size(430, 300);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(5, 31, 47);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            var title = new Label
            {
                Text = "GAT-LOG SERVER",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(72, 34)
            };
            Controls.Add(title);

            Controls.Add(MakeLabel("Usuário", 55, 112));
            _user.SetBounds(145, 108, 225, 30);
            _user.Text = AuthService.EnsureAuth().User;
            Controls.Add(_user);

            Controls.Add(MakeLabel("Senha", 55, 157));
            _pass.SetBounds(145, 153, 225, 30);
            _pass.UseSystemPasswordChar = true;
            Controls.Add(_pass);

            var enter = new Button
            {
                Text = "ENTRAR",
                Bounds = new Rectangle(145, 205, 225, 42),
                BackColor = Color.FromArgb(28, 111, 211),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            enter.FlatAppearance.BorderSize = 0;
            enter.Click += (s, e) => DoLogin();
            Controls.Add(enter);
            AcceptButton = enter;

            _error.SetBounds(55, 255, 320, 25);
            _error.ForeColor = Color.FromArgb(255, 95, 95);
            _error.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_error);
        }

        private Label MakeLabel(string text, int x, int y) => new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y + 5),
            ForeColor = Color.FromArgb(210, 230, 242)
        };

        private void DoLogin()
        {
            if (AuthService.Verify(_user.Text, _pass.Text))
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            _error.Text = "Usuário ou senha incorretos.";
            _pass.SelectAll();
            _pass.Focus();
        }
    }
}
