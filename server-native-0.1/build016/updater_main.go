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
	user32 = syscall.NewLazyDLL("user32.dll")
	messageBoxW = user32.NewProc("MessageBoxW")
)

func u16(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func msg(s string, flags uintptr) {
	messageBoxW.Call(0, uintptr(unsafe.Pointer(u16(s))), uintptr(unsafe.Pointer(u16("GAT-LOG | Atualização 0.1.6"))), flags)
}
func hidden(name string, args ...string) *exec.Cmd {
	c := exec.Command(name, args...)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	return c
}
func kill(name string) { _ = hidden("taskkill.exe", "/F", "/IM", name).Run() }
func atomicWrite(path string, data []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil { return err }
	tmp := path + ".new"
	_ = os.Remove(tmp)
	if err := os.WriteFile(tmp, data, 0644); err != nil { return err }
	_ = os.Remove(path)
	return os.Rename(tmp, path)
}
func install(asset, dst string, mode os.FileMode) error {
	b, err := payload.ReadFile(asset)
	if err != nil { return err }
	if err := atomicWrite(dst, b); err != nil { return err }
	return os.Chmod(dst, mode)
}
func launch(path string, args ...string) {
	c := exec.Command(path, args...)
	c.Dir = filepath.Dir(path)
	c.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: 0x08000000}
	_ = c.Start()
}
func main() {
	base := os.Getenv("LOCALAPPDATA")
	if base == "" { h,_:=os.UserHomeDir(); base=filepath.Join(h,"AppData","Local") }
	installDir := filepath.Join(base, "Programs", "GAT-LOG Server")
	assetsDir := filepath.Join(installDir, "assets")
	if err := os.MkdirAll(assetsDir, 0755); err != nil {
		msg("Não foi possível criar a pasta assets:\r\n"+err.Error(), 0x10)
		return
	}

	kill("GAT_LOG_SERVER.exe")
	kill("GAT_LOG_AGENT.exe")
	time.Sleep(700*time.Millisecond)

	files := []struct{src,dst string; mode os.FileMode}{
		{"payload/assets/logo.png", filepath.Join(assetsDir,"logo.png"), 0644},
		{"payload/assets/banner.png", filepath.Join(assetsDir,"banner.png"), 0644},
		{"payload/GAT_LOG_SERVER.exe", filepath.Join(installDir,"GAT_LOG_SERVER.exe"), 0755},
		{"payload/GAT_LOG_AGENT.exe", filepath.Join(installDir,"GAT_LOG_AGENT.exe"), 0755},
	}
	for _, f := range files {
		if err := install(f.src,f.dst,f.mode); err != nil {
			msg("Falha ao instalar "+filepath.Base(f.dst)+":\r\n"+err.Error(), 0x10)
			return
		}
	}

	launch(filepath.Join(installDir,"GAT_LOG_AGENT.exe"), "--background")
	time.Sleep(350*time.Millisecond)
	launch(filepath.Join(installDir,"GAT_LOG_SERVER.exe"))
	msg(fmt.Sprintf("GAT-LOG Server 0.1.6 instalado.\r\n\r\nA pasta %s foi criada e recebeu logo.png e banner.png.\r\nA interface também possui as mesmas imagens embutidas como fallback.\r\n\r\nNenhuma configuração, mod, histórico ou servidor dedicado do ETS2 foi alterado.", assetsDir), 0x40)
}
