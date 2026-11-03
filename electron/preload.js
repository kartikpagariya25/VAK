const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('isla', {
  getWallpaper: () => ipcRenderer.invoke('wallpaper:get'),
  forceCollapse: () => ipcRenderer.invoke('island:forceCollapse'),
  forceExpand: () => ipcRenderer.invoke('island:forceExpand'),
  onStateChange: (cb) => ipcRenderer.on('island:state', (_e, state) => cb(state)),

  shelfAdd: (paths) => ipcRenderer.invoke('shelf:add', paths),
  shelfList: () => ipcRenderer.invoke('shelf:list'),
  shelfOpen: (name) => ipcRenderer.invoke('shelf:open', name),
  shelfDragOut: (name) => ipcRenderer.send('shelf:dragOut', name),

  convert: (filePath, target) => ipcRenderer.invoke('convert:run', filePath, target),

  storeGet: (key, fallback) => ipcRenderer.invoke('store:get', key, fallback),
  storeSet: (key, value) => ipcRenderer.invoke('store:set', key, value),

  clipboardList: () => ipcRenderer.invoke('clipboard:list'),
  clipboardCopy: (item) => ipcRenderer.invoke('clipboard:copy', item),
  clipboardClear: () => ipcRenderer.invoke('clipboard:clear'),
  clipboardImageDir: () => ipcRenderer.invoke('clipboard:imageDir'),

  systemStats: () => ipcRenderer.invoke('system:stats'),
});
