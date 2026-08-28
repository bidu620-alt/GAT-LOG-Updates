from pathlib import Path
p=Path('client-dotnet/GatTelemetry/MainForm.cs')
s=p.read_text(encoding='utf-8')
needle='''            txtAccountUser = new TextBox { Left = 18, Top = 34, Width = 210, BackColor = Color.White, ForeColor = Color.Black };\n            txtAccountPassword = new TextBox { Left = 240, Top = 34, Width = 210, BackColor = Color.White, ForeColor = Color.Black, UseSystemPasswordChar = true };\n'''
replacement='''            accountBox.Controls.Add(new Label { Text = "Usuário do site", Left = 18, Top = 18, Width = 210, Height = 18, ForeColor = Color.Silver });\n            accountBox.Controls.Add(new Label { Text = "Senha", Left = 240, Top = 18, Width = 210, Height = 18, ForeColor = Color.Silver });\n            txtAccountUser = new TextBox { Left = 18, Top = 38, Width = 210, BackColor = Color.White, ForeColor = Color.Black };\n            txtAccountPassword = new TextBox { Left = 240, Top = 38, Width = 210, BackColor = Color.White, ForeColor = Color.Black, UseSystemPasswordChar = true };\n'''
if needle not in s:
    if 'Text = "Usuário do site"' in s:
        print('labels already present')
    else:
        raise SystemExit('Conta GAT fields not found')
else:
    s=s.replace(needle,replacement,1)
    p.write_text(s,encoding='utf-8')
print('Conta GAT field labels applied')
