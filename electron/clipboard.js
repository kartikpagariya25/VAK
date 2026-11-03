const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const { clipboard, app } = require('electron');
const store = require('./store');

const MAX_ITEMS = 50;
let lastSignature = null;
let watchTimer = null;

function imageDir() {
  const dir = path.join(app.getPath('userData'), 'clipboard-images');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function history() {
  return store.get('clipboard', []);
}

function pushItem(item) {
  const list = [item, ...history()].slice(0, MAX_ITEMS);
  store.set('clipboard', list);
  return list;
}

function captureTick() {
  const image = clipboard.readImage();
  if (!image.isEmpty()) {
    const buf = image.toPNG();
    const sig = 'img:' + crypto.createHash('md5').update(buf).digest('hex');
    if (sig === lastSignature) return;
    lastSignature = sig;
    const size = image.getSize();
    const filename = `${Date.now()}.png`;
    fs.writeFileSync(path.join(imageDir(), filename), buf);
    pushItem({ type: 'image', file: filename, width: size.width, height: size.height, at: Date.now() });
    return;
  }

  const text = clipboard.readText();
  if (!text) return;
  const sig = 'text:' + text;
  if (sig === lastSignature) return;
  lastSignature = sig;
  pushItem({ type: 'text', text, at: Date.now() });
}

function start() {
  watchTimer = setInterval(captureTick, 800);
}

function stop() {
  clearInterval(watchTimer);
}

function copyBack(item) {
  if (item.type === 'text') {
    clipboard.writeText(item.text);
  } else if (item.type === 'image') {
    const { nativeImage } = require('electron');
    const img = nativeImage.createFromPath(path.join(imageDir(), item.file));
    clipboard.writeImage(img);
  }
  // Writing it ourselves changes the clipboard, so the next poll would
  // otherwise re-log this exact item as if the user had copied it again.
  lastSignature = item.type === 'text' ? 'text:' + item.text : null;
}

function clear() {
  store.set('clipboard', []);
}

module.exports = { start, stop, history, copyBack, clear, imageDir };
