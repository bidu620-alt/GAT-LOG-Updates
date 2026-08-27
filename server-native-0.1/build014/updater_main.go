package main

import (
	"bytes"
	"embed"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"time"
	"unsafe"
)

//go:embed assets/GAT_LOG_AGENT.exe
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
	messageBoxW.Call(0, uintptr(unsafe.Pointer(u16(s))), uintptr(unsafe.Pointer(u16("GAT-LOG | Atualização 0.1.4"))), f)
}
func localAppData() string {
	if v := os.Getenv("LOCALAPPDATA"); v != "" { return v }
	h, _ := os.UserHomeDir()
	return filepath.Join(h, "AppData", "Local")
}
func hidden(name string, args ...string) *exec.Cmd {
	c := exec.Command(name, args...)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
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
	tmp := path + ".new"
	_ = os.Remove(tmp)
	if err := os.WriteFile(tmp, b, 0755); err != nil { return err }
	_ = os.Remove(path)
	return os.Rename(tmp, path)
}
func launch(path string) {
	c := exec.Command(path)
	c.Dir = filepath.Dir(path)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	_ = c.Start()
}
func patchUI(path string) error {
	b, err := os.ReadFile(path)
	if err != nil { return err }
	b = bytes.ReplaceAll(b, []byte("0.1.2"), []byte("0.1.4"))
	b = bytes.ReplaceAll(b, []byte("0.1.3"), []byte("0.1.4"))
	return atomicWrite(path, b)
}
func migrateTelemetry() {
	root := filepath.Join(localAppData(), "GAT-LOG")
	dst := filepath.Join(root, "telemetry.json")
	if _, err := os.Stat(dst); err == nil { return }
	src := filepath.Join(root, "telemetry", "current.json")
	if b, err := os.ReadFile(src); err == nil { _ = os.WriteFile(dst, b, 0644) }
}

func main() {
	install := filepath.Join(localAppData(), "Programs", "GAT-LOG Server")
	if err := os.MkdirAll(install, 0755); err != nil {
		msg("Não foi possível acessar a pasta de instalação:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	serverPath := filepath.Join(install, "GAT_LOG_SERVER.exe")
	agentPath := filepath.Join(install, "GAT_LOG_AGENT.exe")

	kill("GAT_LOG_SERVER.exe")
	kill("GAT_LOG_AGENT.exe")
	time.Sleep(900 * time.Millisecond)

	backupDir := filepath.Join(install, "backup_antes_0.1.4")
	backup(serverPath, backupDir)
	backup(agentPath, backupDir)

	if err := patchUI(serverPath); err != nil {
		msg("Falha ao atualizar a interface:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	agentData, err := payload.ReadFile("assets/GAT_LOG_AGENT.exe")
	if err != nil {
		msg("Falha ao ler o agente da atualização:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	if err := atomicWrite(agentPath, agentData); err != nil {
		msg("Falha ao atualizar o agente:\r\n"+err.Error(), MB_OK|MB_ICONERROR)
		return
	}
	migrateTelemetry()

	launch(agentPath)
	time.Sleep(1300 * time.Millisecond)
	launch(serverPath)

	msg(fmt.Sprintf("GAT-LOG Server 0.1.4 instalado com sucesso.\r\n\r\n• telemetria corrigida na interface nativa;\r\n• Funnel 5055 iniciado automaticamente;\r\n• contagem duplicada de jogador corrigida;\r\n• configurações, mods, moderador e histórico preservados.\r\n\r\nO servidor dedicado do ETS2 não foi encerrado."), MB_OK|MB_ICONINFORMATION)
}
