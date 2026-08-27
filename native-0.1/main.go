//go:build windows

package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"image"
	"image/png"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"syscall"
	"time"
	"unsafe"
)

const (
	appName        = "GAT Telemetria"
	appVersion     = "2.0.2"
	displayVersion = "0.1"
	truckURL       = "http://127.0.0.1:31377/api/ets2/telemetry"
	truckRoot      = "http://127.0.0.1:31377/"
	versionURL     = "https://raw.githubusercontent.com/bidu620-alt/GAT-LOG-Updates/main/cliente2_version.json"
)

const (
	WS_OVERLAPPED      = 0x00000000
	WS_CAPTION         = 0x00C00000
	WS_SYSMENU         = 0x00080000
	WS_MINIMIZEBOX     = 0x00020000
	WS_VISIBLE         = 0x10000000
	WS_CHILD           = 0x40000000
	WS_BORDER          = 0x00800000
	WS_TABSTOP         = 0x00010000
	WS_VSCROLL         = 0x00200000
	BS_PUSHBUTTON      = 0x00000000
	BS_AUTOCHECKBOX    = 0x00000003
	BS_OWNERDRAW       = 0x0000000B
	ES_AUTOHSCROLL     = 0x0080
	ES_READONLY        = 0x0800
	CBS_DROPDOWNLIST   = 0x0003
	CBS_HASSTRINGS     = 0x0200
	SS_LEFT            = 0x00000000
	SW_SHOW            = 5
	SW_HIDE            = 0
	CW_USEDEFAULT      = 0x80000000
	WM_DESTROY         = 0x0002
	WM_CLOSE           = 0x0010
	WM_COMMAND         = 0x0111
	WM_PAINT           = 0x000F
	WM_ERASEBKGND      = 0x0014
	WM_DRAWITEM        = 0x002B
	WM_CTLCOLORSTATIC  = 0x0138
	WM_CTLCOLOREDIT    = 0x0133
	WM_CTLCOLORBTN     = 0x0135
	WM_CTLCOLORLISTBOX = 0x0134
	WM_TIMER           = 0x0113
	WM_SETFONT         = 0x0030
	WM_SETTEXT         = 0x000C
	WM_GETTEXT         = 0x000D
	WM_GETTEXTLENGTH   = 0x000E
	WM_APP             = 0x8000
	WM_APP_TICK_DONE   = WM_APP + 1
	WM_APP_INFO_DONE   = WM_APP + 2
	WM_APP_UPDATE_DONE = WM_APP + 3
	CB_ADDSTRING       = 0x0143
	CB_RESETCONTENT    = 0x014B
	CB_GETCURSEL       = 0x0147
	CB_SETCURSEL       = 0x014E
	BM_GETCHECK        = 0x00F0
	BM_SETCHECK        = 0x00F1
	BST_CHECKED        = 1
	BST_UNCHECKED      = 0
	EM_SETREADONLY     = 0x00CF
	BN_CLICKED         = 0
	CBN_SELCHANGE      = 1
	MB_OK              = 0x00000000
	MB_OKCANCEL        = 0x00000001
	MB_YESNO           = 0x00000004
	MB_ICONINFORMATION = 0x00000040
	MB_ICONWARNING     = 0x00000030
	MB_ICONERROR       = 0x00000010
	IDOK               = 1
	IDCANCEL           = 2
	IDYES              = 6
	COLOR_WINDOW       = 5
	DEFAULT_GUI_FONT   = 17
	CF_UNICODETEXT     = 13
	GMEM_MOVEABLE      = 0x0002
	CREATE_NO_WINDOW   = 0x08000000
	TRANSPARENT        = 1
	DT_CENTER          = 0x00000001
	DT_VCENTER         = 0x00000004
	DT_SINGLELINE      = 0x00000020
	ODS_SELECTED       = 0x0001
	DIB_RGB_COLORS     = 0
	SRCCOPY            = 0x00CC0020
	BI_RGB             = 0
	NULL_BRUSH         = 5
	IMAGE_ICON         = 1
	LR_LOADFROMFILE    = 0x0010
	LR_DEFAULTSIZE     = 0x0040
	WM_SETICON         = 0x0080
	ICON_SMALL         = 0
	ICON_BIG           = 1
)

const (
	idServerCombo = 1001
	idAdd         = 1002
	idRemove      = 1003
	idRefresh     = 1004
	idCopy        = 1005
	idAuto        = 1006
	idEnter       = 1007
	idUpdate      = 1008
	idTruck       = 1009
	idSwitch      = 1010
	idExit        = 1011
)

type POINT struct{ X, Y int32 }
type MSG struct {
	Hwnd    syscall.Handle
	Message uint32
	WParam  uintptr
	LParam  uintptr
	Time    uint32
	Pt      POINT
}
type WNDCLASSEX struct {
	CbSize        uint32
	Style         uint32
	LpfnWndProc   uintptr
	CbClsExtra    int32
	CbWndExtra    int32
	HInstance     syscall.Handle
	HIcon         syscall.Handle
	HCursor       syscall.Handle
	HbrBackground syscall.Handle
	LpszMenuName  *uint16
	LpszClassName *uint16
	HIconSm       syscall.Handle
}
type RECT struct{ Left, Top, Right, Bottom int32 }
type PAINTSTRUCT struct {
	Hdc         syscall.Handle
	FErase      int32
	RcPaint     RECT
	FRestore    int32
	FIncUpdate  int32
	RgbReserved [32]byte
}
type BITMAPINFOHEADER struct {
	Size          uint32
	Width         int32
	Height        int32
	Planes        uint16
	BitCount      uint16
	Compression   uint32
	SizeImage     uint32
	XPelsPerMeter int32
	YPelsPerMeter int32
	ClrUsed       uint32
	ClrImportant  uint32
}
type BITMAPINFO struct {
	Header BITMAPINFOHEADER
	Colors [1]uint32
}
type DRAWITEMSTRUCT struct {
	CtlType    uint32
	CtlID      uint32
	ItemID     uint32
	ItemAction uint32
	ItemState  uint32
	HwndItem   syscall.Handle
	HDC        syscall.Handle
	RcItem     RECT
	ItemData   uintptr
}

type DATA_BLOB struct {
	cbData uint32
	pbData *byte
}

type Server struct {
	Name     string `json:"name"`
	Endpoint string `json:"endpoint"`
}
type Credential struct {
	Endpoint string `json:"endpoint"`
	Driver   string `json:"driver"`
	Token    string `json:"token"`
	SavedAt  string `json:"saved_at,omitempty"`
}
type Settings struct {
	AutoConnect bool   `json:"auto_connect"`
	LastServer  string `json:"last_server"`
	UpdatedAt   string `json:"updated_at,omitempty"`
}
type APIResult struct {
	Status int
	JSON   map[string]any
	Text   string
	Err    error
}
type ServerInfo struct {
	Reachable  bool
	Supported  bool
	Online     bool
	ServerName string
	SessionID  string
	Players    int
	MaxPlayers int
}
type RemoteVersion struct {
	App         string `json:"app"`
	Version     string `json:"version"`
	Notes       string `json:"notas"`
	DownloadURL string `json:"download_url"`
	SHA256      string `json:"sha256"`
}

var bannerImage image.Image
var logoImage image.Image
var bannerPixels []byte
var logoPixels []byte
var bannerW, bannerH int
var logoW, logoH int

var (
	brushBg      syscall.Handle
	brushCard    syscall.Handle
	brushEdit    syscall.Handle
	brushBlue    syscall.Handle
	brushGreen   syscall.Handle
	brushRed     syscall.Handle
	brushSlate   syscall.Handle
	brushAmber   syscall.Handle
	fontTitle    syscall.Handle
	fontSubtitle syscall.Handle
	fontNormal   syscall.Handle
	fontBold     syscall.Handle
	fontSmall    syscall.Handle
	appIcon      syscall.Handle
)

var (
	modUser32   = syscall.NewLazyDLL("user32.dll")
	modKernel32 = syscall.NewLazyDLL("kernel32.dll")
	modGdi32    = syscall.NewLazyDLL("gdi32.dll")
	modCrypt32  = syscall.NewLazyDLL("crypt32.dll")

	procRegisterClassEx     = modUser32.NewProc("RegisterClassExW")
	procCreateWindowEx      = modUser32.NewProc("CreateWindowExW")
	procDefWindowProc       = modUser32.NewProc("DefWindowProcW")
	procShowWindow          = modUser32.NewProc("ShowWindow")
	procUpdateWindow        = modUser32.NewProc("UpdateWindow")
	procGetMessage          = modUser32.NewProc("GetMessageW")
	procTranslateMessage    = modUser32.NewProc("TranslateMessage")
	procDispatchMessage     = modUser32.NewProc("DispatchMessageW")
	procPostQuitMessage     = modUser32.NewProc("PostQuitMessage")
	procMessageBox          = modUser32.NewProc("MessageBoxW")
	procSendMessage         = modUser32.NewProc("SendMessageW")
	procSetWindowText       = modUser32.NewProc("SetWindowTextW")
	procGetWindowText       = modUser32.NewProc("GetWindowTextW")
	procGetWindowTextLength = modUser32.NewProc("GetWindowTextLengthW")
	procEnableWindow        = modUser32.NewProc("EnableWindow")
	procSetTimer            = modUser32.NewProc("SetTimer")
	procKillTimer           = modUser32.NewProc("KillTimer")
	procPostMessage         = modUser32.NewProc("PostMessageW")
	procLoadCursor          = modUser32.NewProc("LoadCursorW")
	procLoadImage           = modUser32.NewProc("LoadImageW")
	procGetStockObject      = modGdi32.NewProc("GetStockObject")
	procBeginPaint          = modUser32.NewProc("BeginPaint")
	procEndPaint            = modUser32.NewProc("EndPaint")
	procFillRect            = modUser32.NewProc("FillRect")
	procDrawText            = modUser32.NewProc("DrawTextW")
	procSetBkMode           = modGdi32.NewProc("SetBkMode")
	procSetTextColor        = modGdi32.NewProc("SetTextColor")
	procSetBkColor          = modGdi32.NewProc("SetBkColor")
	procCreateSolidBrush    = modGdi32.NewProc("CreateSolidBrush")
	procDeleteObject        = modGdi32.NewProc("DeleteObject")
	procSelectObject        = modGdi32.NewProc("SelectObject")
	procCreateFont          = modGdi32.NewProc("CreateFontW")
	procStretchDIBits       = modGdi32.NewProc("StretchDIBits")

	procOpenClipboard              = modUser32.NewProc("OpenClipboard")
	procCloseClipboard             = modUser32.NewProc("CloseClipboard")
	procEmptyClipboard             = modUser32.NewProc("EmptyClipboard")
	procSetClipboardData           = modUser32.NewProc("SetClipboardData")
	procGetClipboardData           = modUser32.NewProc("GetClipboardData")
	procIsClipboardFormatAvailable = modUser32.NewProc("IsClipboardFormatAvailable")
	procGlobalAlloc                = modKernel32.NewProc("GlobalAlloc")
	procGlobalLock                 = modKernel32.NewProc("GlobalLock")
	procGlobalUnlock               = modKernel32.NewProc("GlobalUnlock")
	procGlobalFree                 = modKernel32.NewProc("GlobalFree")
	procLocalFree                  = modKernel32.NewProc("LocalFree")

	procCryptProtectData   = modCrypt32.NewProc("CryptProtectData")
	procCryptUnprotectData = modCrypt32.NewProc("CryptUnprotectData")
)

var (
	hwndMain                                                                                             syscall.Handle
	hServerCombo, hServerStatus, hPlayers, hRoomID, hCopy, hAdd, hRemove, hRefresh                       syscall.Handle
	hAuto, hEnter, hLoginMsg, hUpdate                                                                    syscall.Handle
	hSessionServer, hSessionDriver, hTruckStatus, hGatStatus, hTelStatus, hCargo, hTruck, hSwitch, hExit syscall.Handle
	hVersion                                                                                             syscall.Handle
	hTitle, hSubtitle                                                                                    syscall.Handle

	servers                 []Server
	settings                Settings
	endpoint, driver, token string
	deviceID                string
	inSession               bool
	waiting                 bool
	autoConnect             bool
	selectedInfo            ServerInfo
	lastHeartbeat           time.Time
	lastInfo                time.Time
	lastAuto                time.Time
	appDataDir              string
	serversFile             string
	credFile                string
	settingsFile            string
	debugFile               string
	logFile                 string
	tickBusy                int32
	infoBusy                int32
	updateBusy              int32
	mu                      sync.Mutex
)

func utf16Ptr(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func loword(v uintptr) uint16   { return uint16(v & 0xffff) }
func hiword(v uintptr) uint16   { return uint16((v >> 16) & 0xffff) }
func msgBox(text, title string, flags uintptr) int {
	r, _, _ := procMessageBox.Call(uintptr(hwndMain), uintptr(unsafe.Pointer(utf16Ptr(text))), uintptr(unsafe.Pointer(utf16Ptr(title))), flags)
	return int(r)
}
func setText(h syscall.Handle, s string) {
	procSetWindowText.Call(uintptr(h), uintptr(unsafe.Pointer(utf16Ptr(s))))
}
func getText(h syscall.Handle) string {
	n, _, _ := procGetWindowTextLength.Call(uintptr(h))
	buf := make([]uint16, n+1)
	procGetWindowText.Call(uintptr(h), uintptr(unsafe.Pointer(&buf[0])), n+1)
	return syscall.UTF16ToString(buf)
}
func show(h syscall.Handle, yes bool) {
	if yes {
		procShowWindow.Call(uintptr(h), SW_SHOW)
	} else {
		procShowWindow.Call(uintptr(h), SW_HIDE)
	}
}
func enable(h syscall.Handle, yes bool) {
	v := uintptr(0)
	if yes {
		v = 1
	}
	procEnableWindow.Call(uintptr(h), v)
}
func send(h syscall.Handle, msg uint32, w, l uintptr) uintptr {
	r, _, _ := procSendMessage.Call(uintptr(h), uintptr(msg), w, l)
	return r
}

func rgb(r, g, b byte) uintptr { return uintptr(uint32(r) | uint32(g)<<8 | uint32(b)<<16) }

func makeBrush(r, g, b byte) syscall.Handle {
	h, _, _ := procCreateSolidBrush.Call(rgb(r, g, b))
	return syscall.Handle(h)
}

func makeFont(height int32, weight int32, face string) syscall.Handle {
	h, _, _ := procCreateFont.Call(
		uintptr(height), 0, 0, 0, uintptr(weight), 0, 0, 0,
		1, 0, 0, 5, 0,
		uintptr(unsafe.Pointer(utf16Ptr(face))),
	)
	return syscall.Handle(h)
}

func imageToBGRA(img image.Image) ([]byte, int, int) {
	b := img.Bounds()
	w, h := b.Dx(), b.Dy()
	pix := make([]byte, w*h*4)
	for y := 0; y < h; y++ {
		for x := 0; x < w; x++ {
			r, g, bb, _ := img.At(b.Min.X+x, b.Min.Y+y).RGBA()
			i := (y*w + x) * 4
			pix[i+0] = byte(bb >> 8)
			pix[i+1] = byte(g >> 8)
			pix[i+2] = byte(r >> 8)
			pix[i+3] = 0
		}
	}
	return pix, w, h
}

func initTheme() {
	brushBg = makeBrush(5, 20, 33)
	brushCard = makeBrush(13, 39, 58)
	brushEdit = makeBrush(18, 49, 72)
	brushBlue = makeBrush(27, 102, 196)
	brushGreen = makeBrush(25, 132, 72)
	brushRed = makeBrush(177, 44, 34)
	brushSlate = makeBrush(46, 73, 94)
	brushAmber = makeBrush(196, 126, 7)
	fontTitle = makeFont(-32, 700, "Segoe UI")
	fontSubtitle = makeFont(-17, 400, "Segoe UI")
	fontNormal = makeFont(-16, 400, "Segoe UI")
	fontBold = makeFont(-16, 700, "Segoe UI")
	fontSmall = makeFont(-14, 400, "Segoe UI")
	if img, err := png.Decode(bytes.NewReader(bannerBytes)); err == nil {
		bannerImage = img
		bannerPixels, bannerW, bannerH = imageToBGRA(img)
	}
	if img, err := png.Decode(bytes.NewReader(logoBytes)); err == nil {
		logoImage = img
		logoPixels, logoW, logoH = imageToBGRA(img)
	}
}

func drawEmbeddedImage(hdc syscall.Handle, pix []byte, sw, sh int, x, y, w, h int32) {
	if len(pix) == 0 || sw <= 0 || sh <= 0 {
		return
	}
	bmi := BITMAPINFO{}
	bmi.Header.Size = uint32(unsafe.Sizeof(BITMAPINFOHEADER{}))
	bmi.Header.Width = int32(sw)
	bmi.Header.Height = -int32(sh)
	bmi.Header.Planes = 1
	bmi.Header.BitCount = 32
	bmi.Header.Compression = BI_RGB
	procStretchDIBits.Call(
		uintptr(hdc), uintptr(x), uintptr(y), uintptr(w), uintptr(h),
		0, 0, uintptr(sw), uintptr(sh),
		uintptr(unsafe.Pointer(&pix[0])), uintptr(unsafe.Pointer(&bmi)),
		DIB_RGB_COLORS, SRCCOPY,
	)
}

func fillRectHDC(hdc syscall.Handle, r RECT, brush syscall.Handle) {
	procFillRect.Call(uintptr(hdc), uintptr(unsafe.Pointer(&r)), uintptr(brush))
}

func drawTextBlock(hdc syscall.Handle, text string, r RECT, font syscall.Handle, color uintptr) {
	procSetBkMode.Call(uintptr(hdc), TRANSPARENT)
	procSetTextColor.Call(uintptr(hdc), color)
	old, _, _ := procSelectObject.Call(uintptr(hdc), uintptr(font))
	procDrawText.Call(uintptr(hdc), uintptr(unsafe.Pointer(utf16Ptr(text))), uintptr(^uint32(0)), uintptr(unsafe.Pointer(&r)), DT_VCENTER|DT_SINGLELINE)
	if old != 0 {
		procSelectObject.Call(uintptr(hdc), old)
	}
}

func paintMain(hwnd syscall.Handle) {
	var ps PAINTSTRUCT
	hdc, _, _ := procBeginPaint.Call(uintptr(hwnd), uintptr(unsafe.Pointer(&ps)))
	if hdc == 0 {
		return
	}
	defer procEndPaint.Call(uintptr(hwnd), uintptr(unsafe.Pointer(&ps)))
	dc := syscall.Handle(hdc)

	// Janela mais compacta e equilibrada: area cliente ~824x615.
	fillRectHDC(dc, RECT{0, 0, 824, 615}, brushBg)
	drawEmbeddedImage(dc, bannerPixels, bannerW, bannerH, 0, 0, 824, 150)
	fillRectHDC(dc, RECT{0, 147, 824, 153}, brushBlue)
	fillRectHDC(dc, RECT{18, 164, 806, 565}, brushCard)
	fillRectHDC(dc, RECT{18, 574, 806, 610}, brushCard)

	// Identidade desenhada direto no banner: sem caixas brancas de STATIC.
	drawEmbeddedImage(dc, logoPixels, logoW, logoH, 24, 24, 88, 88)
	drawTextBlock(dc, "GAT TELEMETRIA", RECT{130, 28, 650, 66}, fontTitle, rgb(245, 250, 255))
	drawTextBlock(dc, "Cliente nativo ETS2  |  GAT-LOG", RECT{132, 68, 690, 94}, fontSubtitle, rgb(144, 204, 255))
	drawTextBlock(dc, "VERSAO  "+displayVersion, RECT{642, 112, 802, 136}, fontSmall, rgb(144, 204, 255))
}

func buttonBrush(id uint32) syscall.Handle {
	switch int(id) {
	case idEnter:
		return brushGreen
	case idExit:
		return brushRed
	case idRemove:
		return brushSlate
	case idUpdate, idCopy, idAdd, idRefresh, idTruck, idSwitch:
		return brushBlue
	default:
		return brushBlue
	}
}

func drawOwnerButton(dis *DRAWITEMSTRUCT) {
	if dis == nil || dis.HDC == 0 {
		return
	}
	br := buttonBrush(dis.CtlID)
	if dis.ItemState&ODS_SELECTED != 0 {
		br = brushSlate
	}
	fillRectHDC(dis.HDC, dis.RcItem, br)
	procSetBkMode.Call(uintptr(dis.HDC), TRANSPARENT)
	procSetTextColor.Call(uintptr(dis.HDC), rgb(255, 255, 255))
	old, _, _ := procSelectObject.Call(uintptr(dis.HDC), uintptr(fontBold))
	text := getText(dis.HwndItem)
	procDrawText.Call(
		uintptr(dis.HDC), uintptr(unsafe.Pointer(utf16Ptr(text))), uintptr(^uint32(0)),
		uintptr(unsafe.Pointer(&dis.RcItem)), DT_CENTER|DT_VCENTER|DT_SINGLELINE,
	)
	if old != 0 {
		procSelectObject.Call(uintptr(dis.HDC), old)
	}
}

func createControl(class, text string, style uint32, x, y, w, h int32, parent syscall.Handle, id int) syscall.Handle {
	inst, _, _ := modKernel32.NewProc("GetModuleHandleW").Call(0)
	hwnd, _, _ := procCreateWindowEx.Call(
		0,
		uintptr(unsafe.Pointer(utf16Ptr(class))),
		uintptr(unsafe.Pointer(utf16Ptr(text))),
		uintptr(style),
		uintptr(x), uintptr(y), uintptr(w), uintptr(h),
		uintptr(parent), uintptr(id), inst, 0,
	)
	font := uintptr(fontNormal)
	if font == 0 {
		font, _, _ = procGetStockObject.Call(DEFAULT_GUI_FONT)
	}
	send(syscall.Handle(hwnd), WM_SETFONT, font, 1)
	return syscall.Handle(hwnd)
}

func initPaths() {
	local := os.Getenv("LOCALAPPDATA")
	appDataDir = filepath.Join(local, "GAT Telemetria Cliente") // compartilha dados da 1.8.x
	_ = os.MkdirAll(appDataDir, 0755)
	serversFile = filepath.Join(appDataDir, "servers.json")
	credFile = filepath.Join(appDataDir, "credentials.json")
	settingsFile = filepath.Join(appDataDir, "client_settings.json")
	debugFile = filepath.Join(appDataDir, "telemetry_mass_debug.json")
	logFile = filepath.Join(appDataDir, "gat2_startup.log")
}

func logLine(s string) {
	if logFile == "" {
		return
	}
	f, e := os.OpenFile(logFile, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0644)
	if e != nil {
		return
	}
	defer f.Close()
	_, _ = fmt.Fprintf(f, "%s %s\r\n", time.Now().Format("2006-01-02 15:04:05"), s)
}

func loadServers() {
	b, err := os.ReadFile(serversFile)
	if err == nil {
		_ = json.Unmarshal(b, &servers)
	}
	if len(servers) == 0 {
		servers = []Server{
			{Name: "BIDUZAO - DOUGLAS", Endpoint: "https://douglas.tail4577e8.ts.net"},
			{Name: "JC - JEAN", Endpoint: "https://jean-jc.tailf14a00.ts.net"},
		}
		saveJSON(serversFile, servers)
	}
	for i := range servers {
		servers[i].Endpoint = strings.TrimRight(servers[i].Endpoint, "/")
	}
}
func saveServers() { saveJSON(serversFile, servers) }
func loadSettings() {
	settings = Settings{AutoConnect: true}
	if b, err := os.ReadFile(settingsFile); err == nil {
		_ = json.Unmarshal(b, &settings)
	}
	autoConnect = settings.AutoConnect
}
func saveSettings() {
	settings.AutoConnect = autoConnect
	settings.UpdatedAt = time.Now().Format(time.RFC3339)
	saveJSON(settingsFile, settings)
}
func loadCredentials() []Credential {
	var c []Credential
	if b, e := os.ReadFile(credFile); e == nil {
		_ = json.Unmarshal(b, &c)
	}
	return c
}
func saveCredentials(c []Credential) { saveJSON(credFile, c) }
func saveJSON(path string, v any) {
	if b, e := json.MarshalIndent(v, "", "  "); e == nil {
		_ = os.WriteFile(path, b, 0644)
	}
}

func dpapiProtect(s string) string {
	if s == "" {
		return ""
	}
	inb := []byte(s)
	in := DATA_BLOB{cbData: uint32(len(inb)), pbData: &inb[0]}
	var out DATA_BLOB
	r, _, _ := procCryptProtectData.Call(uintptr(unsafe.Pointer(&in)), 0, 0, 0, 0, 0, uintptr(unsafe.Pointer(&out)))
	if r == 0 || out.pbData == nil {
		return ""
	}
	defer procLocalFree.Call(uintptr(unsafe.Pointer(out.pbData)))
	data := unsafe.Slice(out.pbData, out.cbData)
	return base64.StdEncoding.EncodeToString(data)
}
func dpapiUnprotect(v string) string {
	if v == "" {
		return ""
	}
	b, e := base64.StdEncoding.DecodeString(v)
	if e != nil || len(b) == 0 {
		return ""
	}
	in := DATA_BLOB{cbData: uint32(len(b)), pbData: &b[0]}
	var out DATA_BLOB
	r, _, _ := procCryptUnprotectData.Call(uintptr(unsafe.Pointer(&in)), 0, 0, 0, 0, 0, uintptr(unsafe.Pointer(&out)))
	if r == 0 || out.pbData == nil {
		return ""
	}
	defer procLocalFree.Call(uintptr(unsafe.Pointer(out.pbData)))
	data := unsafe.Slice(out.pbData, out.cbData)
	return string(data)
}
func getSavedToken(ep, drv string) string {
	ep = strings.ToLower(strings.TrimRight(ep, "/"))
	for _, c := range loadCredentials() {
		if strings.ToLower(strings.TrimRight(c.Endpoint, "/")) == ep && c.Driver == drv {
			return dpapiUnprotect(c.Token)
		}
	}
	return ""
}
func credentialsForEndpoint(ep string) []Credential {
	ep = strings.ToLower(strings.TrimRight(ep, "/"))
	out := []Credential{}
	for _, c := range loadCredentials() {
		if strings.ToLower(strings.TrimRight(c.Endpoint, "/")) == ep {
			out = append(out, c)
		}
	}
	return out
}
func saveToken(ep, drv, tok string) {
	ep = strings.TrimRight(ep, "/")
	all := loadCredentials()
	out := []Credential{}
	for _, c := range all {
		if strings.EqualFold(strings.TrimRight(c.Endpoint, "/"), ep) && c.Driver == drv {
			continue
		}
		out = append(out, c)
	}
	out = append(out, Credential{Endpoint: ep, Driver: drv, Token: dpapiProtect(tok), SavedAt: time.Now().Format(time.RFC3339)})
	saveCredentials(out)
}
func removeOtherTokens(ep, keep string) {
	ep = strings.ToLower(strings.TrimRight(ep, "/"))
	out := []Credential{}
	for _, c := range loadCredentials() {
		if strings.ToLower(strings.TrimRight(c.Endpoint, "/")) == ep && c.Driver != keep {
			continue
		}
		out = append(out, c)
	}
	saveCredentials(out)
}

func hiddenCommand(name string, args ...string) *exec.Cmd {
	cmd := exec.Command(name, args...)
	cmd.SysProcAttr = &syscall.SysProcAttr{HideWindow: true, CreationFlags: CREATE_NO_WINDOW}
	return cmd
}
func getDeviceID() string {
	raw := ""
	out, e := hiddenCommand("reg.exe", "query", `HKLM\SOFTWARE\Microsoft\Cryptography`, "/v", "MachineGuid").CombinedOutput()
	if e == nil {
		lines := strings.Split(string(out), "\n")
		for _, l := range lines {
			if strings.Contains(strings.ToLower(l), "machineguid") {
				f := strings.Fields(l)
				if len(f) > 0 {
					raw = f[len(f)-1]
				}
			}
		}
	}
	if raw == "" {
		raw = os.Getenv("COMPUTERNAME") + "|" + os.Getenv("USERNAME")
	}
	h := sha256.Sum256([]byte(raw))
	return hex.EncodeToString(h[:])
}
func isEts2Running() bool {
	out, _ := hiddenCommand("tasklist.exe", "/FI", "IMAGENAME eq eurotrucks2.exe", "/NH").CombinedOutput()
	return strings.Contains(strings.ToLower(string(out)), "eurotrucks2.exe")
}
func truckHealthy() bool { r := apiCall("GET", truckRoot, nil, 2*time.Second); return r.Status == 200 }
func findTruckExe() string {
	candidates := []string{
		filepath.Join(os.Getenv("LOCALAPPDATA"), "Programs", "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
		filepath.Join(os.Getenv("LOCALAPPDATA"), "Programs", "TruckSim GPS", "TruckSimGPS_Server.exe"),
		filepath.Join(os.Getenv("ProgramFiles"), "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
		filepath.Join(os.Getenv("ProgramFiles(x86)"), "TruckSim GPS Telemetry Server", "TruckSimGPS_Server.exe"),
	}
	for _, p := range candidates {
		if p != "" {
			if _, e := os.Stat(p); e == nil {
				return p
			}
		}
	}
	return ""
}
func startTruck() {
	if truckHealthy() {
		return
	}
	exe := findTruckExe()
	if exe == "" {
		msgBox("O TruckSim GPS nao foi encontrado. Abra-o pelo Menu Iniciar ou instale o TruckSim GPS Server.", "GAT Telemetria", MB_OK|MB_ICONWARNING)
		return
	}
	cmd := exec.Command(exe)
	_ = cmd.Start()
}

func apiCall(method, url string, body any, timeout time.Duration) APIResult {
	var rdr io.Reader
	if body != nil {
		b, e := json.Marshal(body)
		if e != nil {
			return APIResult{Err: e}
		}
		rdr = bytes.NewReader(b)
	}
	req, e := http.NewRequest(method, url, rdr)
	if e != nil {
		return APIResult{Err: e}
	}
	req.Header.Set("Cache-Control", "no-cache")
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	cl := &http.Client{Timeout: timeout}
	resp, e := cl.Do(req)
	if e != nil {
		return APIResult{Err: e}
	}
	defer resp.Body.Close()
	b, _ := io.ReadAll(resp.Body)
	var obj map[string]any
	_ = json.Unmarshal(b, &obj)
	return APIResult{Status: resp.StatusCode, JSON: obj, Text: string(b)}
}
func boolVal(m map[string]any, k string) bool {
	v, ok := m[k]
	if !ok {
		return false
	}
	b, _ := v.(bool)
	return b
}
func strVal(m map[string]any, k string) string {
	if v, ok := m[k]; ok && v != nil {
		return fmt.Sprint(v)
	}
	return ""
}
func intVal(m map[string]any, k string) int {
	if v, ok := m[k]; ok {
		switch x := v.(type) {
		case float64:
			return int(x)
		case json.Number:
			i, _ := x.Int64()
			return int(i)
		case int:
			return x
		case string:
			i, _ := strconv.Atoi(x)
			return i
		}
	}
	return 0
}

func getServerInfo(ep string) ServerInfo {
	ep = strings.TrimRight(ep, "/")
	r := apiCall("GET", ep+"/api/client/server-info", nil, 5*time.Second)
	if r.Status == 200 && r.JSON != nil && boolVal(r.JSON, "ok") {
		return ServerInfo{Reachable: true, Supported: true, Online: boolVal(r.JSON, "online"), ServerName: strVal(r.JSON, "server_name"), SessionID: strVal(r.JSON, "session_id"), Players: intVal(r.JSON, "players"), MaxPlayers: intVal(r.JSON, "max_players")}
	}
	h := apiCall("GET", ep+"/health", nil, 4*time.Second)
	if h.Status == 200 {
		return ServerInfo{Reachable: true, Supported: false}
	}
	return ServerInfo{}
}
func getPlayers(ep string) []string {
	r := apiCall("GET", strings.TrimRight(ep, "/")+"/api/client/players", nil, 5*time.Second)
	if r.Status != 200 || r.JSON == nil || !boolVal(r.JSON, "ok") {
		return nil
	}
	v, ok := r.JSON["players"].([]any)
	if !ok {
		return nil
	}
	out := []string{}
	seen := map[string]bool{}
	for _, x := range v {
		s := strings.TrimSpace(fmt.Sprint(x))
		if s != "" && !seen[s] {
			seen[s] = true
			out = append(out, s)
		}
	}
	return out
}

func decodeServerCode(code string) (Server, bool) {
	c := strings.TrimSpace(code)
	if strings.HasPrefix(strings.ToLower(c), "https://") {
		u := strings.TrimRight(c, "/")
		host := strings.TrimPrefix(strings.TrimPrefix(u, "https://"), "http://")
		name := strings.Split(host, ".")[0]
		if name == "" {
			name = "Servidor GAT"
		}
		return Server{Name: name, Endpoint: u}, true
	}
	if !strings.HasPrefix(c, "GAT1:") {
		return Server{}, false
	}
	s := strings.TrimPrefix(c, "GAT1:")
	s = strings.ReplaceAll(s, "-", "+")
	s = strings.ReplaceAll(s, "_", "/")
	for len(s)%4 != 0 {
		s += "="
	}
	b, e := base64.StdEncoding.DecodeString(s)
	if e != nil {
		return Server{}, false
	}
	var x Server
	if json.Unmarshal(b, &x) != nil || x.Endpoint == "" {
		return Server{}, false
	}
	x.Endpoint = strings.TrimRight(x.Endpoint, "/")
	if x.Name == "" {
		x.Name = "Servidor GAT"
	}
	return x, true
}

func getTelemetry() (map[string]any, error) {
	req, e := http.NewRequest("GET", truckURL, nil)
	if e != nil {
		return nil, e
	}
	cl := &http.Client{Timeout: 1200 * time.Millisecond}
	resp, e := cl.Do(req)
	if e != nil {
		return nil, e
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("HTTP %d", resp.StatusCode)
	}
	dec := json.NewDecoder(resp.Body)
	dec.UseNumber()
	var m map[string]any
	e = dec.Decode(&m)
	return m, e
}
func pathValue(m map[string]any, path string) any {
	var cur any = m
	for _, p := range strings.Split(path, ".") {
		mm, ok := cur.(map[string]any)
		if !ok {
			return nil
		}
		cur = mm[p]
		if cur == nil {
			return nil
		}
	}
	return cur
}
func num(v any) (float64, bool) {
	if v == nil {
		return 0, false
	}
	switch x := v.(type) {
	case json.Number:
		f, e := x.Float64()
		if e == nil && f != 0 {
			if f < 0 {
				f = -f
			}
			return f, true
		}
	case float64:
		if x != 0 {
			if x < 0 {
				x = -x
			}
			return x, true
		}
	case string:
		s := strings.ReplaceAll(strings.TrimSpace(x), ",", ".")
		f, e := strconv.ParseFloat(s, 64)
		if e == nil && f != 0 {
			if f < 0 {
				f = -f
			}
			return f, true
		}
	}
	return 0, false
}
func telemetryMass(m map[string]any) (float64, bool) {
	paths := []string{"job.cargoMass", "Job.CargoMass", "mass_kg", "cargoMass", "cargo_mass", "cargoMassKg", "cargo_mass_kg", "cargoWeight", "cargo_weight", "weight_kg", "job.mass", "job.mass_kg", "job.cargo_mass", "job.cargoMassKg", "job.cargo_mass_kg", "job.cargoWeight", "job.weight", "job.cargo.mass", "job.cargo.mass_kg", "job.cargo.massKg", "job.cargo.weight", "cargo.mass", "cargo.mass_kg", "cargo.massKg", "cargo.weight", "trailer.mass", "trailerMass", "trailer.cargoMass", "trailer.cargo_mass", "game.job.cargoMass", "game.job.mass"}
	for _, p := range paths {
		if n, ok := num(pathValue(m, p)); ok && n > 0 {
			return n, true
		}
	}
	return 0, false
}
func normalizeTelemetry(m map[string]any) map[string]any {
	if n, ok := telemetryMass(m); ok {
		m["mass_kg"] = n
		m["cargoMass"] = n
		m["cargo_mass"] = n
	}
	return m
}
func formatMass(n float64, ok bool) string {
	if !ok {
		return "-"
	}
	if n >= 1000 {
		return strings.ReplaceAll(strconv.FormatFloat(n/1000, 'f', 2, 64), ".00", "") + " t"
	}
	return fmt.Sprintf("%.0f kg", n)
}

func recursivePlayerHint(v any, players []string, depth int) string {
	if depth > 5 {
		return ""
	}
	switch x := v.(type) {
	case map[string]any:
		for k, val := range x {
			kl := strings.ToLower(k)
			if kl == "playername" || kl == "profilename" || kl == "steamname" || kl == "username" || kl == "multiplayername" || kl == "player_name" || kl == "profile_name" {
				s := strings.TrimSpace(fmt.Sprint(val))
				if p := matchPlayer(s, players); p != "" {
					return p
				}
			}
			if p := recursivePlayerHint(val, players, depth+1); p != "" {
				return p
			}
		}
	case []any:
		for _, val := range x {
			if p := recursivePlayerHint(val, players, depth+1); p != "" {
				return p
			}
		}
	}
	return ""
}
func normalizeName(s string) string { return strings.ToLower(strings.Join(strings.Fields(s), " ")) }
func compactName(s string) string {
	var b strings.Builder
	for _, r := range normalizeName(s) {
		if (r >= 'a' && r <= 'z') || (r >= '0' && r <= '9') || r > 127 {
			b.WriteRune(r)
		}
	}
	return b.String()
}
func matchPlayer(h string, players []string) string {
	if strings.TrimSpace(h) == "" {
		return ""
	}
	n := normalizeName(h)
	for _, p := range players {
		if normalizeName(p) == n {
			return p
		}
	}
	c := compactName(h)
	if len(c) >= 3 {
		matches := []string{}
		for _, p := range players {
			if compactName(p) == c {
				matches = append(matches, p)
			}
		}
		if len(matches) == 1 {
			return matches[0]
		}
	}
	return ""
}
func steamPersona() string {
	paths := []string{filepath.Join(os.Getenv("ProgramFiles(x86)"), "Steam", "config", "loginusers.vdf"), filepath.Join(os.Getenv("ProgramFiles"), "Steam", "config", "loginusers.vdf")}
	if out, e := hiddenCommand("reg.exe", "query", `HKCU\Software\Valve\Steam`, "/v", "SteamPath").CombinedOutput(); e == nil {
		for _, l := range strings.Split(string(out), "\n") {
			if strings.Contains(strings.ToLower(l), "steampath") {
				f := strings.Fields(l)
				if len(f) > 0 {
					paths = append([]string{filepath.Join(f[len(f)-1], "config", "loginusers.vdf")}, paths...)
				}
			}
		}
	}
	reName := regexp.MustCompile(`(?i)"PersonaName"\s*"([^"]+)"`)
	reRecent := regexp.MustCompile(`(?i)"MostRecent"\s*"1"`)
	for _, p := range paths {
		b, e := os.ReadFile(p)
		if e != nil {
			continue
		}
		text := string(b)
		blocks := regexp.MustCompile(`(?s)"\d{15,20}"\s*\{(.*?)\}`).FindAllStringSubmatch(text, -1)
		fallback := ""
		for _, bl := range blocks {
			m := reName.FindStringSubmatch(bl[1])
			if len(m) < 2 {
				continue
			}
			name := strings.TrimSpace(m[1])
			if fallback == "" {
				fallback = name
			}
			if reRecent.MatchString(bl[1]) {
				return name
			}
		}
		if fallback != "" {
			return fallback
		}
	}
	return ""
}
func gameLogHints() []string {
	docs := filepath.Join(os.Getenv("USERPROFILE"), "Documents")
	p := filepath.Join(docs, "Euro Truck Simulator 2", "game.log.txt")
	b, e := os.ReadFile(p)
	if e != nil {
		return nil
	}
	text := string(b)
	if len(text) > 800000 {
		text = text[len(text)-800000:]
	}
	re := regexp.MustCompile(`(?im)(?:player|steam|persona|profile)[ _-]*(?:name)?\s*[:=]\s*["']?([^"',\r\n\[\]]{2,64})`)
	out := []string{}
	seen := map[string]bool{}
	for _, m := range re.FindAllStringSubmatch(text, -1) {
		s := strings.TrimSpace(m[1])
		if s != "" && !seen[s] {
			seen[s] = true
			out = append(out, s)
		}
	}
	return out
}
func resolveDriver(ep string, players []string) string {
	for _, c := range credentialsForEndpoint(ep) {
		if p := matchPlayer(c.Driver, players); p != "" {
			return p
		}
	}
	if tele, e := getTelemetry(); e == nil {
		if p := recursivePlayerHint(tele, players, 0); p != "" {
			return p
		}
	}
	if p := matchPlayer(steamPersona(), players); p != "" {
		return p
	}
	for _, h := range gameLogHints() {
		if p := matchPlayer(h, players); p != "" {
			return p
		}
	}
	if len(players) == 1 {
		return players[0]
	}
	return ""
}

func renewCredential() bool {
	mu.Lock()
	ep := endpoint
	drv := driver
	mu.Unlock()
	if ep == "" || drv == "" {
		return false
	}
	r := apiCall("POST", strings.TrimRight(ep, "/")+"/api/client/login", map[string]any{"driver": drv, "device_id": deviceID, "token": ""}, 8*time.Second)
	if r.Status == 200 && r.JSON != nil && boolVal(r.JSON, "ok") {
		canonical := drv
		if s := strVal(r.JSON, "driver"); s != "" {
			canonical = s
		}
		nt := strVal(r.JSON, "token")
		if nt != "" {
			mu.Lock()
			driver = canonical
			token = nt
			mu.Unlock()
			saveToken(ep, canonical, nt)
			removeOtherTokens(ep, canonical)
			setText(hSessionDriver, "Motorista: "+canonical+"  (detectado automaticamente)")
			return true
		}
	}
	return false
}
func sendTelemetry(tele map[string]any) APIResult {
	tele = normalizeTelemetry(tele)
	mu.Lock()
	ep, drv, tok := endpoint, driver, token
	mu.Unlock()
	body := map[string]any{"driver": drv, "device_id": deviceID, "token": tok, "telemetry": tele}
	r := apiCall("POST", ep+"/api/client/telemetry", body, 8*time.Second)
	errCode := ""
	if r.JSON != nil {
		errCode = strVal(r.JSON, "error")
	}
	if r.Status == 401 || errCode == "token_required" {
		if renewCredential() {
			mu.Lock()
			tok = token
			mu.Unlock()
			body["token"] = tok
			r = apiCall("POST", ep+"/api/client/telemetry", body, 8*time.Second)
		}
	}
	return r
}
func sendHeartbeat() APIResult {
	mu.Lock()
	ep, drv, tok := endpoint, driver, token
	mu.Unlock()
	body := map[string]any{"driver": drv, "device_id": deviceID, "token": tok}
	r := apiCall("POST", ep+"/api/client/heartbeat", body, 6*time.Second)
	errCode := ""
	if r.JSON != nil {
		errCode = strVal(r.JSON, "error")
	}
	if r.Status == 401 || errCode == "token_required" {
		if renewCredential() {
			mu.Lock()
			tok = token
			mu.Unlock()
			body["token"] = tok
			r = apiCall("POST", ep+"/api/client/heartbeat", body, 6*time.Second)
		}
	}
	return r
}

func selectedServer() (Server, bool) {
	idx := int(send(hServerCombo, CB_GETCURSEL, 0, 0))
	if idx < 0 || idx >= len(servers) {
		return Server{}, false
	}
	return servers[idx], true
}
func reloadCombo(selectEp string) {
	send(hServerCombo, CB_RESETCONTENT, 0, 0)
	idx := 0
	for i, s := range servers {
		send(hServerCombo, CB_ADDSTRING, 0, uintptr(unsafe.Pointer(utf16Ptr(s.Name))))
		if selectEp != "" && strings.EqualFold(strings.TrimRight(s.Endpoint, "/"), strings.TrimRight(selectEp, "/")) {
			idx = i
		}
	}
	if len(servers) > 0 {
		send(hServerCombo, CB_SETCURSEL, uintptr(idx), 0)
	}
}
func refreshServerInfoAsync() {
	if !atomic.CompareAndSwapInt32(&infoBusy, 0, 1) {
		return
	}
	s, ok := selectedServer()
	if !ok {
		atomic.StoreInt32(&infoBusy, 0)
		return
	}
	go func() {
		info := getServerInfo(s.Endpoint)
		mu.Lock()
		selectedInfo = info
		lastInfo = time.Now()
		mu.Unlock()
		atomic.StoreInt32(&infoBusy, 0)
		procPostMessage.Call(uintptr(hwndMain), WM_APP_INFO_DONE, 0, 0)
	}()
}
func applyServerInfo() {
	mu.Lock()
	info := selectedInfo
	mu.Unlock()
	if !info.Reachable {
		setText(hServerStatus, "Status: GAT LOG INACESSIVEL")
		setText(hPlayers, "Jogadores: -")
		setText(hRoomID, "-")
		enable(hCopy, false)
		return
	}
	if !info.Supported {
		setText(hServerStatus, "Status: GAT LOG ONLINE | SERVIDOR ANTIGO")
		setText(hPlayers, "Jogadores: -")
		setText(hRoomID, "ID indisponivel")
		enable(hCopy, false)
		return
	}
	if info.Online {
		setText(hServerStatus, "Status: SERVIDOR ONLINE")
		if info.MaxPlayers > 0 {
			setText(hPlayers, fmt.Sprintf("Jogadores: %d / %d", info.Players, info.MaxPlayers))
		} else {
			setText(hPlayers, fmt.Sprintf("Jogadores: %d", info.Players))
		}
		if info.SessionID != "" {
			setText(hRoomID, info.SessionID)
			enable(hCopy, true)
		} else {
			setText(hRoomID, "Aguardando ID...")
			enable(hCopy, false)
		}
	} else {
		setText(hServerStatus, "Status: SERVIDOR OFFLINE")
		setText(hPlayers, "Jogadores: 0")
		setText(hRoomID, "-")
		enable(hCopy, false)
	}
}

func startDetectedSession(drv string, s Server) bool {
	if strings.TrimSpace(drv) == "" {
		return false
	}
	ep := strings.TrimRight(s.Endpoint, "/")
	tok := getSavedToken(ep, drv)
	r := apiCall("POST", ep+"/api/client/login", map[string]any{"driver": drv, "device_id": deviceID, "token": tok}, 10*time.Second)
	if r.Status == 200 && r.JSON != nil && boolVal(r.JSON, "ok") {
		canonical := drv
		if x := strVal(r.JSON, "driver"); x != "" {
			canonical = x
		}
		if x := strVal(r.JSON, "token"); x != "" {
			tok = x
			saveToken(ep, canonical, tok)
		}
		removeOtherTokens(ep, canonical)
		mu.Lock()
		endpoint = ep
		driver = canonical
		token = tok
		inSession = true
		waiting = false
		lastHeartbeat = time.Time{}
		mu.Unlock()
		setText(hSessionServer, "Servidor: "+s.Name)
		setText(hSessionDriver, "Motorista: "+canonical+"  (detectado automaticamente)")
		showSession(true)
		return true
	}
	code := ""
	if r.JSON != nil {
		code = strVal(r.JSON, "error")
	}
	switch code {
	case "device_mismatch":
		setText(hLoginMsg, "Seu nome ja esta vinculado a outro PC. Use DESVINCULAR PC no GAT LOG.")
	case "token_required":
		setText(hLoginMsg, "Credencial desatualizada. Tentando renovar automaticamente...")
	case "blocked":
		setText(hLoginMsg, "Motorista bloqueado pelo administrador.")
	case "registration_closed":
		setText(hLoginMsg, "Novos vinculos estao bloqueados neste servidor.")
	case "not_in_server":
		setText(hLoginMsg, "Entre primeiro na sala do ETS2 e tente novamente.")
	case "disconnected_by_admin":
		setText(hLoginMsg, "Desconectado pelo administrador. Aguarde e tente novamente.")
	default:
		if r.Status == 0 {
			setText(hLoginMsg, "Servidor inacessivel. Verifique internet/Funnel.")
		} else {
			setText(hLoginMsg, fmt.Sprintf("Falha ao conectar (HTTP %d).", r.Status))
		}
	}
	return false
}
func tryConnect() {
	mu.Lock()
	if inSession {
		mu.Unlock()
		return
	}
	waiting = true
	mu.Unlock()
	s, ok := selectedServer()
	if !ok {
		setText(hLoginMsg, "Escolha um servidor para iniciar o modo automatico.")
		return
	}
	settings.LastServer = s.Endpoint
	saveSettings()
	if !isEts2Running() {
		setText(hLoginMsg, "AGUARDANDO ETS2... Abra o jogo e entre na sessao.")
		setText(hEnter, "AGUARDANDO ETS2...")
		return
	}
	setText(hLoginMsg, "ETS2 aberto. Verificando sua sessao...")
	setText(hEnter, "VERIFICANDO SESSAO...")
	for _, c := range credentialsForEndpoint(s.Endpoint) {
		if strings.TrimSpace(c.Driver) != "" && startDetectedSession(c.Driver, s) {
			return
		}
	}
	players := getPlayers(s.Endpoint)
	if len(players) == 0 {
		setText(hLoginMsg, "ETS2 aberto. Aguardando voce entrar na sessao selecionada...")
		setText(hEnter, "AGUARDANDO ENTRAR NA SESSAO...")
		return
	}
	drv := resolveDriver(s.Endpoint, players)
	if drv != "" {
		_ = startDetectedSession(drv, s)
		return
	}
	setText(hLoginMsg, fmt.Sprintf("Sessao detectada com %d jogador(es). Aguardando reconhecer seu motorista...", len(players)))
}
func endSession(msg string) {
	mu.Lock()
	inSession = false
	waiting = autoConnect
	mu.Unlock()
	showSession(false)
	setText(hEnter, "ENTRAR / AGUARDAR SESSAO")
	if msg != "" {
		setText(hLoginMsg, msg)
	} else {
		setText(hLoginMsg, "Aguardando servidor/ETS2.")
	}
	refreshServerInfoAsync()
}

func tickAsync() {
	if !atomic.CompareAndSwapInt32(&tickBusy, 0, 1) {
		return
	}
	go func() {
		defer atomic.StoreInt32(&tickBusy, 0)
		mu.Lock()
		session := inSession
		wait := waiting
		lastI := lastInfo
		lastA := lastAuto
		mu.Unlock()
		if !session {
			if time.Since(lastI) >= 3*time.Second {
				refreshServerInfoAsync()
			}
			if wait && time.Since(lastA) >= 2500*time.Millisecond {
				mu.Lock()
				lastAuto = time.Now()
				mu.Unlock()
				tryConnect()
			}
			return
		}
		tele, e := getTelemetry()
		if e == nil && tele != nil {
			normalizeTelemetry(tele)
			setText(hTruckStatus, "TruckSim GPS       ● CONECTADO")
			setText(hTruck, "ABRIR TRUCKSIM GPS")
			if _, ok := telemetryMass(tele); !ok {
				if st, e := os.Stat(debugFile); e != nil || time.Since(st.ModTime()) > 5*time.Second {
					if b, e := json.MarshalIndent(tele, "", "  "); e == nil {
						_ = os.WriteFile(debugFile, b, 0644)
					}
				}
			}
			r := sendTelemetry(tele)
			if r.Status == 200 && r.JSON != nil && boolVal(r.JSON, "ok") {
				setText(hGatStatus, "GAT LOG            ● CONECTADO")
				setText(hTelStatus, "Telemetria         ● ENVIANDO")
				c := strVal(r.JSON, "cargo")
				if c == "" {
					c = "Sem carga"
				}
				km := "-"
				if x, ok := num(r.JSON["distance_m"]); ok {
					km = fmt.Sprintf("%.1f km", x/1000)
				}
				vel := "-"
				if x, ok := num(r.JSON["speed_kmh"]); ok {
					vel = fmt.Sprintf("%.0f km/h", x)
				}
				mass, mok := telemetryMass(tele)
				if !mok {
					if x, ok := num(r.JSON["mass_kg"]); ok {
						mass = x
						mok = true
					} else if x, ok := num(r.JSON["cargo_mass"]); ok {
						mass = x
						mok = true
					}
				}
				setText(hCargo, fmt.Sprintf("Carga: %s   |   Peso: %s\r\nKm restantes: %s\r\nVelocidade: %s", c, formatMass(mass, mok), km, vel))
			} else {
				code := ""
				if r.JSON != nil {
					code = strVal(r.JSON, "error")
				}
				if code == "blocked" {
					endSession("Motorista bloqueado pelo administrador.")
					return
				}
				if code == "device_mismatch" {
					endSession("PC nao autorizado. Use DESVINCULAR PC no servidor.")
					return
				}
				if code == "disconnected_by_admin" {
					endSession("Voce foi desconectado pelo administrador.")
					return
				}
				if r.Status == 0 {
					setText(hGatStatus, "GAT LOG            ● SERVIDOR INACESSIVEL")
				} else {
					setText(hGatStatus, fmt.Sprintf("GAT LOG            ● ERRO HTTP %d", r.Status))
				}
				setText(hTelStatus, "Telemetria         ● NAO ENVIANDO")
			}
		} else {
			if findTruckExe() != "" {
				setText(hTruckStatus, "TruckSim GPS       ● INSTALADO / FECHADO")
				setText(hTruck, "ABRIR TRUCKSIM GPS")
			} else {
				setText(hTruckStatus, "TruckSim GPS       ● NAO INSTALADO")
				setText(hTruck, "TRUCKSIM GPS NAO INSTALADO")
			}
			setText(hTelStatus, "Telemetria         ● AGUARDANDO ETS2")
			setText(hCargo, "Aguardando o ETS2/TruckSim GPS para iniciar a telemetria.")
			mu.Lock()
			due := time.Since(lastHeartbeat) >= 5*time.Second
			if due {
				lastHeartbeat = time.Now()
			}
			mu.Unlock()
			if due {
				r := sendHeartbeat()
				if r.Status == 200 {
					setText(hGatStatus, "GAT LOG            ● CONECTADO")
				} else {
					code := ""
					if r.JSON != nil {
						code = strVal(r.JSON, "error")
					}
					if code == "blocked" {
						endSession("Motorista bloqueado pelo administrador.")
						return
					}
					if code == "disconnected_by_admin" {
						endSession("Voce foi desconectado pelo administrador.")
						return
					}
					setText(hGatStatus, "GAT LOG            ● SEM CONEXAO")
				}
			}
		}
	}()
}

func compareVersion(a, b string) int { // a vs b
	clean := func(s string) []int {
		parts := strings.FieldsFunc(s, func(r rune) bool { return r == '.' || r == '-' || r == '+' })
		out := []int{}
		for _, p := range parts {
			n, _ := strconv.Atoi(p)
			out = append(out, n)
			if n == 0 && p != "0" {
				break
			}
		}
		return out
	}
	x, y := clean(a), clean(b)
	n := len(x)
	if len(y) > n {
		n = len(y)
	}
	for i := 0; i < n; i++ {
		xi, yi := 0, 0
		if i < len(x) {
			xi = x[i]
		}
		if i < len(y) {
			yi = y[i]
		}
		if xi < yi {
			return -1
		}
		if xi > yi {
			return 1
		}
	}
	return 0
}
func checkUpdate(interactive bool) {
	if !atomic.CompareAndSwapInt32(&updateBusy, 0, 1) {
		return
	}
	go func() {
		defer atomic.StoreInt32(&updateBusy, 0)
		r := apiCall("GET", versionURL+"?t="+strconv.FormatInt(time.Now().Unix(), 10), nil, 6*time.Second)
		if r.Status != 200 || r.Text == "" {
			if interactive {
				msgBox("Nao foi possivel consultar o GitHub agora.", "GAT Telemetria | Atualizacao", MB_OK|MB_ICONWARNING)
			}
			return
		}
		var v RemoteVersion
		if json.Unmarshal([]byte(r.Text), &v) != nil {
			return
		}
		if compareVersion(v.Version, appVersion) > 0 {
			msg := "Nova versao disponivel: " + v.Version + "\r\nVersao instalada: " + appVersion
			if v.Notes != "" {
				msg += "\r\n\r\n" + v.Notes
			}
			if v.DownloadURL == "" {
				msg += "\r\n\r\nO pacote ainda nao foi publicado."
				msgBox(msg, "GAT Telemetria | Atualizacao", MB_OK|MB_ICONINFORMATION)
				return
			}
			msg += "\r\n\r\nDeseja atualizar agora pelo GitHub?"
			if msgBox(msg, "GAT Telemetria | Nova atualizacao", MB_YESNO|MB_ICONINFORMATION) == IDYES {
				downloadAndInstall(v)
			}
		} else if interactive {
			msgBox("Voce ja esta usando a versao mais recente ("+appVersion+").", "GAT Telemetria | Atualizacao", MB_OK|MB_ICONINFORMATION)
		}
	}()
}
func downloadAndInstall(v RemoteVersion) {
	resp, e := http.Get(v.DownloadURL)
	if e != nil {
		msgBox("Falha ao baixar atualizacao: "+e.Error(), "GAT Telemetria", MB_OK|MB_ICONERROR)
		return
	}
	defer resp.Body.Close()
	b, e := io.ReadAll(resp.Body)
	if e != nil || resp.StatusCode != 200 {
		msgBox("Falha ao baixar o pacote de atualizacao.", "GAT Telemetria", MB_OK|MB_ICONERROR)
		return
	}
	if v.SHA256 != "" {
		h := sha256.Sum256(b)
		if !strings.EqualFold(hex.EncodeToString(h[:]), strings.TrimSpace(v.SHA256)) {
			msgBox("Falha de integridade SHA-256 no pacote.", "GAT Telemetria", MB_OK|MB_ICONERROR)
			return
		}
	}
	tmp := filepath.Join(os.TempDir(), fmt.Sprintf("gat_telemetria2_update_%d.exe", time.Now().UnixNano()))
	if os.WriteFile(tmp, b, 0755) != nil {
		return
	}
	cur, _ := os.Executable()
	cmd := exec.Command(tmp, "--install-update", cur, "--parent", strconv.Itoa(os.Getpid()))
	_ = cmd.Start()
	procPostMessage.Call(uintptr(hwndMain), WM_CLOSE, 0, 0)
}
func runUpdateInstaller(args []string) {
	target := ""
	for i := 0; i < len(args); i++ {
		if args[i] == "--install-update" && i+1 < len(args) {
			target = args[i+1]
			i++
		}
	}
	if target == "" {
		return
	}
	self, _ := os.Executable()
	b, e := os.ReadFile(self)
	if e != nil {
		return
	}
	for i := 0; i < 30; i++ {
		e = os.WriteFile(target, b, 0755)
		if e == nil {
			break
		}
		time.Sleep(time.Second)
	}
	if e == nil {
		_ = exec.Command(target).Start()
	}
}

func clipboardSet(s string) bool {
	r, _, _ := procOpenClipboard.Call(0)
	if r == 0 {
		return false
	}
	defer procCloseClipboard.Call()
	procEmptyClipboard.Call()
	u := syscall.StringToUTF16(s)
	size := uintptr(len(u) * 2)
	h, _, _ := procGlobalAlloc.Call(GMEM_MOVEABLE, size)
	if h == 0 {
		return false
	}
	p, _, _ := procGlobalLock.Call(h)
	if p == 0 {
		procGlobalFree.Call(h)
		return false
	}
	dst := unsafe.Slice((*uint16)(unsafe.Pointer(p)), len(u))
	copy(dst, u)
	procGlobalUnlock.Call(h)
	r, _, _ = procSetClipboardData.Call(CF_UNICODETEXT, h)
	if r == 0 {
		procGlobalFree.Call(h)
		return false
	}
	return true
}
func clipboardGet() string {
	avail, _, _ := procIsClipboardFormatAvailable.Call(CF_UNICODETEXT)
	if avail == 0 {
		return ""
	}
	r, _, _ := procOpenClipboard.Call(0)
	if r == 0 {
		return ""
	}
	defer procCloseClipboard.Call()
	h, _, _ := procGetClipboardData.Call(CF_UNICODETEXT)
	if h == 0 {
		return ""
	}
	p, _, _ := procGlobalLock.Call(h)
	if p == 0 {
		return ""
	}
	defer procGlobalUnlock.Call(h)
	ptr := (*uint16)(unsafe.Pointer(p))
	arr := make([]uint16, 0, 1024)
	for i := 0; i < 65536; i++ {
		v := *(*uint16)(unsafe.Pointer(uintptr(unsafe.Pointer(ptr)) + uintptr(i*2)))
		if v == 0 {
			break
		}
		arr = append(arr, v)
	}
	return syscall.UTF16ToString(arr)
}

func addServerFromClipboard() {
	clip := strings.TrimSpace(clipboardGet())
	if clip == "" {
		msgBox("Copie primeiro o CODIGO DO SERVIDOR (GAT1:...) ou o endereco https://... e clique ADICIONAR.", "GAT Telemetria", MB_OK|MB_ICONINFORMATION)
		return
	}
	s, ok := decodeServerCode(clip)
	if !ok {
		msgBox("O conteudo da area de transferencia nao e um codigo/endereco GAT valido.", "GAT Telemetria", MB_OK|MB_ICONWARNING)
		return
	}
	found := false
	for i, x := range servers {
		if strings.EqualFold(strings.TrimRight(x.Endpoint, "/"), s.Endpoint) {
			servers[i] = s
			found = true
			break
		}
	}
	if !found {
		servers = append(servers, s)
	}
	saveServers()
	reloadCombo(s.Endpoint)
	settings.LastServer = s.Endpoint
	saveSettings()
	refreshServerInfoAsync()
}
func removeSelectedServer() {
	s, ok := selectedServer()
	if !ok {
		return
	}
	if msgBox("Remover o servidor "+s.Name+" desta lista?", "GAT Telemetria", MB_YESNO|MB_ICONWARNING) != IDYES {
		return
	}
	idx := int(send(hServerCombo, CB_GETCURSEL, 0, 0))
	servers = append(servers[:idx], servers[idx+1:]...)
	saveServers()
	reloadCombo("")
	refreshServerInfoAsync()
}

func showSession(session bool) {
	loginControls := []syscall.Handle{hServerCombo, hServerStatus, hPlayers, hRoomID, hCopy, hAdd, hRemove, hRefresh, hAuto, hEnter, hLoginMsg}
	sessionControls := []syscall.Handle{hSessionServer, hSessionDriver, hTruckStatus, hGatStatus, hTelStatus, hCargo, hTruck, hSwitch, hExit}
	for _, h := range loginControls {
		show(h, !session)
	}
	for _, h := range sessionControls {
		show(h, session)
	}
}

func wndProc(hwnd syscall.Handle, msg uint32, wParam, lParam uintptr) uintptr {
	switch msg {
	case WM_PAINT:
		paintMain(hwnd)
		return 0
	case WM_ERASEBKGND:
		return 1
	case WM_DRAWITEM:
		dis := (*DRAWITEMSTRUCT)(unsafe.Pointer(lParam))
		drawOwnerButton(dis)
		return 1
	case WM_CTLCOLORSTATIC:
		hdc := syscall.Handle(wParam)
		child := syscall.Handle(lParam)

		// Os STATICs ficam sobre os cards. Fundo opaco evita que o texto
		// anterior permaneça visível quando Status/Telemetria são atualizados.
		procSetBkMode.Call(uintptr(hdc), 2) // OPAQUE
		procSetBkColor.Call(uintptr(hdc), rgb(13, 39, 58))
		col := rgb(235, 245, 255)
		if child == hServerStatus {
			statusText := strings.ToUpper(getText(hServerStatus))
			switch {
			case strings.Contains(statusText, "INACESSIVEL"), strings.Contains(statusText, "OFFLINE"):
				col = rgb(255, 82, 82)
			case strings.Contains(statusText, "ONLINE"):
				col = rgb(73, 232, 132)
			default:
				col = rgb(255, 194, 59)
			}
		}
		if child == hLoginMsg {
			col = rgb(255, 194, 59)
		}
		if child == hTruckStatus || child == hGatStatus || child == hTelStatus {
			col = rgb(116, 211, 255)
		}
		procSetTextColor.Call(uintptr(hdc), col)
		return uintptr(brushCard)
	case WM_CTLCOLOREDIT:
		hdc := syscall.Handle(wParam)
		procSetTextColor.Call(uintptr(hdc), rgb(245, 250, 255))
		procSetBkColor.Call(uintptr(hdc), rgb(18, 49, 72))
		return uintptr(brushEdit)
	case WM_CTLCOLORLISTBOX:
		hdc := syscall.Handle(wParam)
		procSetTextColor.Call(uintptr(hdc), rgb(245, 250, 255))
		procSetBkColor.Call(uintptr(hdc), rgb(18, 49, 72))
		return uintptr(brushEdit)
	case WM_CTLCOLORBTN:
		hdc := syscall.Handle(wParam)
		procSetBkMode.Call(uintptr(hdc), TRANSPARENT)
		procSetTextColor.Call(uintptr(hdc), rgb(235, 245, 255))
		return uintptr(brushCard)
	case WM_COMMAND:
		id := int(loword(wParam))
		notify := hiword(wParam)
		if notify == BN_CLICKED {
			switch id {
			case idAdd:
				addServerFromClipboard()
			case idRemove:
				removeSelectedServer()
			case idRefresh:
				refreshServerInfoAsync()
			case idCopy:
				if clipboardSet(getText(hRoomID)) {
					setText(hLoginMsg, "ID da sala copiado. Agora abra o ETS2 e entre no comboio.")
				}
			case idAuto:
				autoConnect = send(hAuto, BM_GETCHECK, 0, 0) == BST_CHECKED
				if s, ok := selectedServer(); ok {
					settings.LastServer = s.Endpoint
				}
				saveSettings()
				mu.Lock()
				waiting = autoConnect
				lastAuto = time.Time{}
				mu.Unlock()
				if autoConnect {
					setText(hLoginMsg, "Modo automatico ativo: aguardando ETS2/sessao...")
				} else {
					setText(hLoginMsg, "Modo automatico desativado. Clique ENTRAR quando quiser.")
				}
			case idEnter:
				mu.Lock()
				waiting = true
				lastAuto = time.Time{}
				mu.Unlock()
				go tryConnect()
			case idUpdate:
				checkUpdate(true)
			case idTruck:
				startTruck()
			case idSwitch:
				mu.Lock()
				inSession = false
				waiting = false
				mu.Unlock()
				showSession(false)
				send(hServerCombo, CB_SETCURSEL, ^uintptr(0), 0)
				setText(hLoginMsg, "Escolha outro servidor. Ao selecionar, ele vira o servidor padrao.")
			case idExit:
				procPostMessage.Call(uintptr(hwndMain), WM_CLOSE, 0, 0)
			}
		}
		if id == idServerCombo && notify == CBN_SELCHANGE {
			if s, ok := selectedServer(); ok {
				settings.LastServer = s.Endpoint
				saveSettings()
				mu.Lock()
				waiting = autoConnect
				lastAuto = time.Time{}
				mu.Unlock()
				if autoConnect {
					setText(hLoginMsg, "Servidor selecionado. Modo automatico ativo: aguardando ETS2/sessao...")
				} else {
					setText(hLoginMsg, "Servidor selecionado. Clique ENTRAR para aguardar a sessao.")
				}
				refreshServerInfoAsync()
			}
		}
		return 0
	case WM_TIMER:
		tickAsync()
		return 0
	case WM_APP_INFO_DONE:
		applyServerInfo()
		return 0
	case WM_CLOSE:
		procKillTimer.Call(uintptr(hwnd), 1)
		procDestroyWindow := modUser32.NewProc("DestroyWindow")
		procDestroyWindow.Call(uintptr(hwnd))
		return 0
	case WM_DESTROY:
		procPostQuitMessage.Call(0)
		return 0
	}
	r, _, _ := procDefWindowProc.Call(uintptr(hwnd), uintptr(msg), wParam, lParam)
	return r
}

func loadAppIcon() syscall.Handle {
	if appIcon != 0 {
		return appIcon
	}
	path := filepath.Join(os.TempDir(), "gat_telemetria_01.ico")
	_ = os.WriteFile(path, appIconBytes, 0644)
	h, _, _ := procLoadImage.Call(0, uintptr(unsafe.Pointer(utf16Ptr(path))), IMAGE_ICON, 0, 0, LR_LOADFROMFILE|LR_DEFAULTSIZE)
	appIcon = syscall.Handle(h)
	return appIcon
}

func registerMainClass() string {
	cls := "GATTelemetry2Main"
	inst, _, _ := modKernel32.NewProc("GetModuleHandleW").Call(0)
	cursor, _, _ := procLoadCursor.Call(0, 32512)
	icon := loadAppIcon()
	wc := WNDCLASSEX{CbSize: uint32(unsafe.Sizeof(WNDCLASSEX{})), LpfnWndProc: syscall.NewCallback(wndProc), HInstance: syscall.Handle(inst), HIcon: icon, HCursor: syscall.Handle(cursor), HbrBackground: brushBg, LpszClassName: utf16Ptr(cls), HIconSm: icon}
	procRegisterClassEx.Call(uintptr(unsafe.Pointer(&wc)))
	return cls
}

func createUI() {
	cls := registerMainClass()
	inst, _, _ := modKernel32.NewProc("GetModuleHandleW").Call(0)
	hwnd, _, _ := procCreateWindowEx.Call(
		0,
		uintptr(unsafe.Pointer(utf16Ptr(cls))),
		uintptr(unsafe.Pointer(utf16Ptr("GAT TELEMETRIA 0.1 | Cliente Nativo ETS2"))),
		WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX|WS_VISIBLE,
		uintptr(CW_USEDEFAULT), uintptr(CW_USEDEFAULT), 840, 650, 0, 0, inst, 0,
	)
	hwndMain = syscall.Handle(hwnd)
	if appIcon != 0 {
		procSendMessage.Call(uintptr(hwndMain), WM_SETICON, ICON_BIG, uintptr(appIcon))
		procSendMessage.Call(uintptr(hwndMain), WM_SETICON, ICON_SMALL, uintptr(appIcon))
	}

	// Titulo, subtitulo e versao sao desenhados em WM_PAINT sobre o banner.
	hTitle, hSubtitle, hVersion = 0, 0, 0

	// Tela de seleção / login — layout compacto e proporcional.
	createControl("STATIC", "SERVIDOR", WS_CHILD|WS_VISIBLE, 33, 174, 110, 20, hwndMain, 0)
	hServerCombo = createControl("COMBOBOX", "", WS_CHILD|WS_VISIBLE|WS_TABSTOP|CBS_DROPDOWNLIST|CBS_HASSTRINGS|WS_VSCROLL, 33, 196, 758, 240, hwndMain, idServerCombo)
	hServerStatus = createControl("STATIC", "Status: CONSULTANDO...", WS_CHILD|WS_VISIBLE, 33, 232, 485, 22, hwndMain, 0)
	send(hServerStatus, WM_SETFONT, uintptr(fontBold), 1)
	hPlayers = createControl("STATIC", "Jogadores: -", WS_CHILD|WS_VISIBLE, 592, 232, 199, 22, hwndMain, 0)

	createControl("STATIC", "ID DA SALA", WS_CHILD|WS_VISIBLE, 33, 263, 120, 20, hwndMain, 0)
	hRoomID = createControl("EDIT", "Aguardando...", WS_CHILD|WS_VISIBLE|WS_BORDER|ES_AUTOHSCROLL|ES_READONLY, 33, 284, 515, 28, hwndMain, 0)
	hCopy = createControl("BUTTON", "COPIAR ID", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 562, 282, 229, 32, hwndMain, idCopy)
	enable(hCopy, false)

	hAdd = createControl("BUTTON", "ADICIONAR SERVIDOR", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 33, 332, 230, 34, hwndMain, idAdd)
	hRemove = createControl("BUTTON", "REMOVER", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 273, 332, 155, 34, hwndMain, idRemove)
	hRefresh = createControl("BUTTON", "ATUALIZAR STATUS", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 438, 332, 353, 34, hwndMain, idRefresh)

	hAuto = createControl("BUTTON", "Entrar automaticamente ao abrir o cliente", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_AUTOCHECKBOX, 33, 382, 500, 24, hwndMain, idAuto)
	if autoConnect {
		send(hAuto, BM_SETCHECK, BST_CHECKED, 0)
	}
	hEnter = createControl("BUTTON", "ENTRAR / AGUARDAR SESSAO", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 33, 416, 758, 44, hwndMain, idEnter)
	hLoginMsg = createControl("STATIC", "Selecione o servidor. O cliente pode aguardar mesmo com o ETS2 fechado.", WS_CHILD|WS_VISIBLE, 33, 474, 758, 26, hwndMain, 0)
	send(hLoginMsg, WM_SETFONT, uintptr(fontSmall), 1)

	// Tela da sessão/telemetria — mesma densidade visual do login.
	hSessionServer = createControl("STATIC", "Servidor: -", WS_CHILD, 33, 178, 758, 26, hwndMain, 0)
	send(hSessionServer, WM_SETFONT, uintptr(fontBold), 1)
	hSessionDriver = createControl("STATIC", "Motorista: -", WS_CHILD, 33, 210, 758, 24, hwndMain, 0)
	hTruckStatus = createControl("STATIC", "TruckSim GPS       ● AGUARDANDO", WS_CHILD, 33, 252, 758, 26, hwndMain, 0)
	hGatStatus = createControl("STATIC", "GAT LOG            ● AGUARDANDO", WS_CHILD, 33, 284, 758, 26, hwndMain, 0)
	hTelStatus = createControl("STATIC", "Telemetria         ● AGUARDANDO", WS_CHILD, 33, 316, 758, 26, hwndMain, 0)
	hCargo = createControl("STATIC", "Aguardando telemetria...", WS_CHILD, 33, 357, 758, 74, hwndMain, 0)
	send(hCargo, WM_SETFONT, uintptr(fontBold), 1)
	hTruck = createControl("BUTTON", "ABRIR TRUCKSIM GPS", WS_CHILD|WS_TABSTOP|BS_OWNERDRAW, 33, 458, 238, 38, hwndMain, idTruck)
	hSwitch = createControl("BUTTON", "TROCAR SERVIDOR", WS_CHILD|WS_TABSTOP|BS_OWNERDRAW, 285, 458, 238, 38, hwndMain, idSwitch)
	hExit = createControl("BUTTON", "SAIR", WS_CHILD|WS_TABSTOP|BS_OWNERDRAW, 537, 458, 254, 38, hwndMain, idExit)

	hUpdate = createControl("BUTTON", "VERIFICAR ATUALIZACAO", WS_CHILD|WS_VISIBLE|WS_TABSTOP|BS_OWNERDRAW, 570, 580, 221, 28, hwndMain, idUpdate)
	createControl("STATIC", "GAT-LOG  •  Telemetria 0.1  •  Biduzao", WS_CHILD|WS_VISIBLE, 33, 584, 500, 20, hwndMain, 0)

	reloadCombo(settings.LastServer)
	showSession(false)
	refreshServerInfoAsync()
	if autoConnect && len(servers) > 0 {
		mu.Lock()
		waiting = true
		mu.Unlock()
		setText(hLoginMsg, "Modo automatico ativo. AGUARDANDO ETS2...")
		setText(hEnter, "AGUARDANDO ETS2...")
	}
	procSetTimer.Call(uintptr(hwndMain), 1, 1200, 0)
	procShowWindow.Call(uintptr(hwndMain), SW_SHOW)
	procUpdateWindow.Call(uintptr(hwndMain))
	go func() { time.Sleep(1600 * time.Millisecond); checkUpdate(false) }()
}

func main() {
	if len(os.Args) > 1 && os.Args[1] == "--install-update" {
		runUpdateInstaller(os.Args[1:])
		return
	}
	runtime.LockOSThread()
	initTheme()
	initPaths()
	logLine("inicio " + appVersion)
	defer func() {
		if r := recover(); r != nil {
			logLine(fmt.Sprintf("PANIC: %v", r))
			msgBox(fmt.Sprintf("O GAT Telemetria encontrou um erro.\r\n\r\n%v\r\n\r\nLog: %s", r, logFile), "GAT Telemetria", MB_OK|MB_ICONERROR)
		}
	}()
	loadServers()
	logLine(fmt.Sprintf("servidores carregados: %d", len(servers)))
	loadSettings()
	deviceID = getDeviceID()
	logLine("device id ok")
	createUI()
	logLine("interface criada")
	var msg MSG
	for {
		r, _, _ := procGetMessage.Call(uintptr(unsafe.Pointer(&msg)), 0, 0, 0)
		if int32(r) <= 0 {
			break
		}
		procTranslateMessage.Call(uintptr(unsafe.Pointer(&msg)))
		procDispatchMessage.Call(uintptr(unsafe.Pointer(&msg)))
	}
	logLine("encerrado")
}
