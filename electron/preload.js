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
});
