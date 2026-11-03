const { app, BrowserWindow, screen, ipcMain, globalShortcut, Tray, Menu, nativeImage } = require('electron');
const path = require('path');
const { exec } = require('child_process');
const fs = require('fs');
const shelf = require('./shelf');
const converter = require('./converter');
const store = require('./store');
const clipboardHistory = require('./clipboard');
const systemStats = require('./system');

const COLLAPSED = { width: 180, height: 36 };
const EXPANDED = { width: 760, height: 480 };
const HOVER_ZONE_HEIGHT = 90;

let win = null;
let tray = null;
let expanded = false;
let pollTimer = null;
let shrinkTimer = null;

function topCenterBounds(size) {
  const { width: sw } = screen.getPrimaryDisplay().workAreaSize;
  return {
    x: Math.round((sw - size.width) / 2),
    y: 0,
    width: size.width,
    height: size.height,
  };
}

function createWindow() {
  win = new BrowserWindow({
    ...topCenterBounds(COLLAPSED),
    frame: false,
    transparent: true,
    backgroundColor: '#00000000',
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    movable: false,
    hasShadow: false,
    show: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      webviewTag: true,
    },
  });

  win.setAlwaysOnTop(true, 'screen-saver');
  win.setVisibleOnAllWorkspaces(true, { visibleOnFullScreen: true });
  win.loadFile(path.join(__dirname, '..', 'src', 'index.html'));

  win.on('blur', () => {
    if (expanded) scheduleCollapse();
  });

  startHoverPoll();
}

function setExpanded(next) {
  if (next === expanded) return;
  expanded = next;
  const size = expanded ? EXPANDED : COLLAPSED;
  win.setBounds(topCenterBounds(size), true);
  win.webContents.send('island:state', expanded ? 'expanded' : 'collapsed');
  win.setIgnoreMouseEvents(false);
}

function scheduleCollapse() {
  clearTimeout(shrinkTimer);
  shrinkTimer = setTimeout(() => setExpanded(false), 220);
}

// Cursor lives outside our own window most of the time, so a plain
// mouseenter on the BrowserWindow only fires once it's already big.
// Polling the global cursor position is what lets the pill react
// before the user actually touches it.
function startHoverPoll() {
  pollTimer = setInterval(() => {
    const p = screen.getCursorScreenPoint();
    const b = win.getBounds();
    const inPill = p.x >= b.x && p.x <= b.x + b.width && p.y >= b.y && p.y <= b.y + b.height;
    const inHoverStrip = !expanded && p.y <= HOVER_ZONE_HEIGHT &&
      p.x >= b.x - 40 && p.x <= b.x + b.width + 40;

    if ((inPill || inHoverStrip) && !expanded) {
      clearTimeout(shrinkTimer);
      setExpanded(true);
    } else if (expanded && !inPill) {
      scheduleCollapse();
    } else if (expanded && inPill) {
      clearTimeout(shrinkTimer);
    }
  }, 60);
}

function createTray() {
  const iconPath = path.join(__dirname, '..', 'assets', 'tray.png');
  const icon = fs.existsSync(iconPath) ? nativeImage.createFromPath(iconPath) : nativeImage.createEmpty();
  tray = new Tray(icon);
  tray.setToolTip('VAK');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Show / Hide', click: () => win.isVisible() ? win.hide() : win.show() },
    { label: 'Start with Windows', type: 'checkbox', checked: app.getLoginItemSettings().openAtLogin,
      click: (item) => app.setLoginItemSettings({ openAtLogin: item.checked }) },
    { type: 'separator' },
    { label: 'Quit VAK', click: () => app.quit() },
  ]));
}

ipcMain.handle('wallpaper:get', async () => {
  return new Promise((resolve) => {
    const cmd = 'powershell -NoProfile -Command "(Get-ItemProperty -Path \'HKCU:\\\\Control Panel\\\\Desktop\').WallPaper"';
    exec(cmd, (err, stdout) => resolve(err ? null : stdout.trim()));
  });
});

ipcMain.handle('island:forceCollapse', () => setExpanded(false));
ipcMain.handle('island:forceExpand', () => setExpanded(true));

ipcMain.handle('shelf:add', (_e, paths) => shelf.add(paths));
ipcMain.handle('shelf:list', () => shelf.list());
ipcMain.handle('shelf:open', (_e, name) => require('electron').shell.openPath(shelf.fullPath(name)));
ipcMain.on('shelf:dragOut', (e, name) => {
  const icon = nativeImage.createEmpty();
  e.sender.startDrag({ file: shelf.fullPath(name), icon });
});

ipcMain.handle('convert:run', (_e, filePath, target) => converter.convert(filePath, target));

ipcMain.handle('store:get', (_e, key, fallback) => store.get(key, fallback));
ipcMain.handle('store:set', (_e, key, value) => store.set(key, value));

ipcMain.handle('clipboard:list', () => clipboardHistory.history());
ipcMain.handle('clipboard:copy', (_e, item) => clipboardHistory.copyBack(item));
ipcMain.handle('clipboard:clear', () => clipboardHistory.clear());
ipcMain.handle('clipboard:imageDir', () => clipboardHistory.imageDir());

ipcMain.handle('system:stats', () => systemStats.stats());

app.whenReady().then(() => {
  createWindow();
  createTray();
  clipboardHistory.start();
  globalShortcut.register('Control+Alt+Space', () => setExpanded(!expanded));
});

app.on('window-all-closed', (e) => e.preventDefault());
app.on('will-quit', () => {
  globalShortcut.unregisterAll();
  clearInterval(pollTimer);
  clipboardHistory.stop();
});
