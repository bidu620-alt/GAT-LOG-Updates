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

//go:embed payload/GAT_LOG_SERVER.exe payload/GAT_LOG_AGENT.exe payload/assets/logo.png payload/assets/banner.png
var payload embed.FS

var (
	user32     = syscall.NewLazyDLL("user32.dll")
	messageBox = user32.NewProc("MessageBoxW")
)

func u16(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func msg(s string, flags uintptr) {
	messageBox.Call(0, uintptr(unsafe.Pointer(u16(s))), uintptr(unsafe.Pointer(u16("GAT-LOG | Atualização 0.1.7"))), flags)
}
func hidden(name string, args ...string) *exec.Cmd {
	c := exec.Command(name, args...)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	return c
}
func kill(name string) { _ = hidden("taskkill.exe", "/F", "/IM", name).Run() }
func localAppData() string {
	if v := os.Getenv("LOCALAPPDATA"); v != "" { return v }
	h, _ := os.UserHomeDir()
	return filepath.Join(h, "AppData", "Local")
}
func atomicWrite(path string, data []byte, mode os.FileMode) error {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil { return err }
	tmp := path + ".new"
	_ = os.Remove(tmp)
	if err := os.WriteFile(tmp, data, mode); err != nil { return err }
	_ = os.Remove(path)
	if err := os.Rename(tmp, path); err != nil { return err }
	return os.Chmod(path, mode)
}
func install(asset, dst string, mode os.FileMode) error {
	b, err := payload.ReadFile(asset)
	if err != nil { return err }
	return atomicWrite(dst, b, mode)
}
func backup(src, dstDir string) {
	b, err := os.ReadFile(src)
	if err != nil { return }
	_ = os.MkdirAll(dstDir, 0755)
	_ = os.WriteFile(filepath.Join(dstDir, filepath.Base(src)), b, 0755)
}
func launch(path string, args ...string) {
	c := exec.Command(path, args...)
	c.Dir = filepath.Dir(path)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	_ = c.Start()
}

func main() {
	base := localAppData()
	installDir := filepath.Join(base, "Programs", "GAT-LOG Server")
	assetsDir := filepath.Join(installDir, "assets")
	if err := os.MkdirAll(assetsDir, 0755); err != nil {
		msg("Não foi possível preparar a pasta do GAT-LOG:\r\n"+err.Error(), 0x10)
		return
	}

	serverExe := filepath.Join(installDir, "GAT_LOG_SERVER.exe")
	agentExe := filepath.Join(installDir, "GAT_LOG_AGENT.exe")
	backupDir := filepath.Join(installDir, "backup_antes_0.1.7")

	// Do not touch eurotrucks2_server.exe. Only the GAT-LOG UI and agent are replaced.
	kill("GAT_LOG_SERVER.exe")
	kill("GAT_LOG_AGENT.exe")
	time.Sleep(800 * time.Millisecond)

	backup(serverExe, backupDir)
	backup(agentExe, backupDir)

	files := []struct{ src, dst string; mode os.FileMode }{
		{"payload/assets/logo.png", filepath.Join(assetsDir, "logo.png"), 0644},
		{"payload/assets/banner.png", filepath.Join(assetsDir, "banner.png"), 0644},
		{"payload/GAT_LOG_SERVER.exe", serverExe, 0755},
		{"payload/GAT_LOG_AGENT.exe", agentExe, 0755},
	}
	for _, f := range files {
		if err := install(f.src, f.dst, f.mode); err != nil {
			msg("Falha ao instalar "+filepath.Base(f.dst)+":\r\n"+err.Error(), 0x10)
			return
		}
	}

	launch(agentExe, "--background")
	time.Sleep(450 * time.Millisecond)
	launch(serverExe)

	msg(fmt.Sprintf("GAT-LOG Server 0.1.7 instalado com sucesso.\r\n\r\nCorreções de estabilidade:\r\n• removida a repintura completa a cada 1 segundo;\r\n• atualizações da interface não se sobrepõem;\r\n• repintura agora é enviada para a thread correta da janela;\r\n• leitura do status/log foi desacelerada e não pode executar em paralelo;\r\n• logo e tema permanecem em %s.\r\n\r\nConfigurações, moderador, histórico, mods e servidor dedicado do ETS2 foram preservados.", assetsDir), 0x40)
}
