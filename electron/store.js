const path = require('path');
const fs = require('fs');
const { app } = require('electron');

function filePath() {
  return path.join(app.getPath('userData'), 'settings.json');
}

function readAll() {
  try {
    return JSON.parse(fs.readFileSync(filePath(), 'utf8'));
  } catch {
    return {};
  }
}

function get(key, fallback) {
  const data = readAll();
  return key in data ? data[key] : fallback;
}

function set(key, value) {
  const data = readAll();
  data[key] = value;
  fs.writeFileSync(filePath(), JSON.stringify(data, null, 2));
  return value;
}

module.exports = { get, set };
