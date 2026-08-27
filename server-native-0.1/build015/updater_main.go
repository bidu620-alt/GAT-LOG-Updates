package main

import (
	"embed"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"time"
	"unsafe"
)

//go:embed assets/GAT_LOG_SERVER.exe assets/GAT_LOG_AGENT.exe
var payload embed.FS

var (
	user32      = syscall.NewLazyDLL("user32.dll")
	messageBoxW = user32.NewProc("MessageBoxW")
)

const (
	MB_OK              = 0x00000000
	MB_ICONINFORMATION = 0x00000040
	MB_ICONERROR       = 0x00000010
)

func u16(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func msg(s string, f uintptr) {
	messageBoxW.Call(0, uintptr(unsafe.Pointer(u16(s))), uintptr(unsafe.Pointer(u16("GAT-LOG | Atualização 0.1.5"))), f)
}
func localAppData() string {
	if v := os.Getenv("LOCALAPPDATA"); v != "" { return v }
	h, _ := os.UserHomeDir()
	return filepath.Join(h, "AppData", "Local")
}
func hidden(name string, args ...string) *exec.Cmd {
	c := exec.Command(name, args...)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	return c
}
func kill(name string) { _ = hidden("taskkill.exe", "/F", "/IM", name).Run() }
func backup(path, dir string) {
	b, err := os.ReadFile(path)
	if err != nil { return }
	_ = os.MkdirAll(dir, 0755)
	_ = os.WriteFile(filepath.Join(dir, filepath.Base(path)), b, 0755)
}
func atomicWrite(path string, b []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil { return err }
	tmp := path + ".new"
	_ = os.Remove(tmp)
	if err := os.WriteFile(tmp, b, 0755); err != nil { return err }
	_ = os.Remove(path)
	return os.Rename(tmp, path)
}
func installEmbedded(dst, asset string) error {
	b, err := payload.ReadFile(asset)
	if err != nil { return err }
	return atomicWrite(dst, b)
}
func launch(path string, args ...string) {
	c := exec.Command(path, args...)
	c.Dir = filepath.Dir(path)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	_ = c.Start()
}
func migrateTelemetry() {
	root := filepath.Join(localAppData(), "GAT-LOG")
	dst := filepath.Join(root, "telemetry.json")
	if _, err := os.Stat(dst); err == nil { return }
	src := filepath.Join(root, "telemetry", "current.json")
	if b, err := os.ReadFile(src); err == nil {
		_ = os.MkdirAll(root, 0755)
		_ = os.WriteFile(dst, b, 0644)
	}
}

func main() {
	install := filepath.Join(localAppData(), "Programs", "GAT-LOG Server")
	if err := os.MkdirAll(install, 0755); err != nil {
		msg("Não foi possível acessar a pasta de instalação:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	serverPath := filepath.Join(install, "GAT_LOG_SERVER.exe")
	agentPath := filepath.Join(install, "GAT_LOG_AGENT.exe")

	// Never touch eurotrucks2_server.exe: the ETS2 dedicated server stays online.
	kill("GAT_LOG_SERVER.exe")
	kill("GAT_LOG_AGENT.exe")
	time.Sleep(900 * time.Millisecond)

	backupDir := filepath.Join(install, "backup_antes_0.1.5")
	backup(serverPath, backupDir)
	backup(agentPath, backupDir)

	if err := installEmbedded(serverPath, "assets/GAT_LOG_SERVER.exe"); err != nil {
		msg("Falha ao atualizar a interface:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	if err := installEmbedded(agentPath, "assets/GAT_LOG_AGENT.exe"); err != nil {
		msg("Falha ao atualizar o agente:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	migrateTelemetry()

	launch(agentPath, "--background")
	time.Sleep(450 * time.Millisecond)
	launch(serverPath)

	msg(fmt.Sprintf("GAT-LOG Server 0.1.5 instalado com sucesso.\r\n\r\n• a janela abre sem esperar o agente;\r\n• leitura de sessão/log roda em segundo plano;\r\n• status da interface usa cache e não reprocessa o log a cada consulta;\r\n• timeout da API local foi reduzido;\r\n• telemetria é gravada sem atrasar o cliente;\r\n• Funnel permanece em segundo plano;\r\n• configurações, mods, moderador e histórico foram preservados.\r\n\r\nO servidor dedicado do ETS2 não foi encerrado."), MB_OK|MB_ICONINFORMATION)
}
