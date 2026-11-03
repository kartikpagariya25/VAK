const path = require('path');
const fs = require('fs');
const { app } = require('electron');

function shelfDir() {
  const dir = path.join(app.getPath('userData'), 'shelf');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

function add(filePaths) {
  const dir = shelfDir();
  for (const src of filePaths) {
    const dest = path.join(dir, path.basename(src));
    fs.copyFileSync(src, dest);
  }
  return list();
}

function list() {
  return fs.readdirSync(shelfDir());
}

function fullPath(name) {
  return path.join(shelfDir(), name);
}

module.exports = { shelfDir, add, list, fullPath };
