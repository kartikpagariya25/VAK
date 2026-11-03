const island = document.getElementById('island');
const panel = document.getElementById('panel');
const cardGrid = document.getElementById('cardGrid');
const dock = document.querySelector('.dock');
const pageTitle = document.getElementById('pageTitle');
const pageSubtitle = document.getElementById('pageSubtitle');

const MODULE_META = {
  home: { title: 'Home', subtitle: 'Your widgets at a glance' },
  clipboard: { title: 'Clipboard', subtitle: 'Recent copies' },
  timer: { title: 'Focus', subtitle: 'Pomodoro timer' },
  weather: { title: 'Weather', subtitle: 'Current conditions' },
  system: { title: 'System', subtitle: 'CPU, RAM, battery' },
  worldclock: { title: 'World Clock', subtitle: 'Local and remote times' },
  units: { title: 'Units', subtitle: 'Quick conversion' },
  'chat-claude': { title: 'Claude', subtitle: 'Chat' },
  'chat-gpt': { title: 'ChatGPT', subtitle: 'Chat' },
  fileshelf: { title: 'File Shelf', subtitle: 'Drag files in, drag them out anywhere' },
  converter: { title: 'Converter', subtitle: 'Images, PDFs, ZIP' },
};

window.isla.onStateChange((state) => {
  island.classList.toggle('collapsed', state === 'collapsed');
  if (state === 'collapsed') showModule('home');
});

function showModule(name) {
  dock.querySelectorAll('button').forEach((b) => b.classList.toggle('active', b.dataset.module === name));
  const meta = MODULE_META[name] || MODULE_META.home;
  pageTitle.textContent = meta.title;
  pageSubtitle.textContent = meta.subtitle;

  if (name === 'home') {
    panel.hidden = true;
    panel.innerHTML = '';
    cardGrid.style.display = 'grid';
    return;
  }
  cardGrid.style.display = 'none';
  panel.hidden = false;
  window.islaModules.render(name, panel);
}

document.querySelectorAll('.card, .dock button').forEach((el) => {
  el.addEventListener('click', () => showModule(el.dataset.module));
});

document.getElementById('closeBtn').addEventListener('click', () => window.isla.forceCollapse());
document.getElementById('settingsBtn').addEventListener('click', () => showModule('home'));

// Adaptive accent color from the current wallpaper.
async function applyWallpaperTheme() {
  try {
    const wallpaperPath = await window.isla.getWallpaper();
    if (!wallpaperPath) return;
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = 48; canvas.height = 48;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(img, 0, 0, 48, 48);
      const { data } = ctx.getImageData(0, 0, 48, 48);
      let r = 0, g = 0, b = 0, n = 0;
      for (let i = 0; i < data.length; i += 4) {
        const sat = Math.max(data[i], data[i + 1], data[i + 2]) - Math.min(data[i], data[i + 1], data[i + 2]);
        if (sat < 15) continue; // skip near-greys, they wash the accent out
        r += data[i]; g += data[i + 1]; b += data[i + 2]; n++;
      }
      if (n === 0) return;
      r = Math.round(r / n); g = Math.round(g / n); b = Math.round(b / n);
      document.documentElement.style.setProperty('--accent', `${r}, ${g}, ${b}`);
    };
    img.src = 'file://' + wallpaperPath.replace(/\\/g, '/');
  } catch (e) { /* non-fatal: keep default accent */ }
}
applyWallpaperTheme();

// Fade the pill when another window is in front and the cursor is away.
let idleTimer = null;
document.addEventListener('mousemove', () => {
  island.classList.remove('dimmed');
  clearTimeout(idleTimer);
  idleTimer = setTimeout(() => {
    if (island.classList.contains('collapsed')) island.classList.add('dimmed');
  }, 2500);
});
