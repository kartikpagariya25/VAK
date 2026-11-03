window.islaModules.renderers.clipboard = async function (el) {
  const [items, imageDir] = await Promise.all([window.isla.clipboardList(), window.isla.clipboardImageDir()]);
  el.innerHTML = `
    <div class="clip">
      <div class="clip-toolbar">
        <span>${items.length} items</span>
        <button id="clipClear">Clear</button>
      </div>
      <div class="clip-list">${items.map((it) => clipRow(it, imageDir)).join('') || '<div class="shelf-empty">Nothing copied yet</div>'}</div>
    </div>`;

  el.querySelector('#clipClear')?.addEventListener('click', async () => {
    await window.isla.clipboardClear();
    window.islaModules.render('clipboard', el);
  });
  el.querySelectorAll('.clip-item').forEach((row, i) => {
    row.addEventListener('click', () => window.isla.clipboardCopy(items[i]));
  });
};

function clipRow(item, imageDir) {
  const time = new Date(item.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  if (item.type === 'image') {
    const src = 'file://' + `${imageDir}/${item.file}`.replace(/\\/g, '/');
    return `<div class="clip-item"><img src="${src}" class="clip-thumb" /><span class="clip-meta">${item.width}x${item.height} - ${time}</span></div>`;
  }
  const preview = item.text.length > 90 ? item.text.slice(0, 90) + '...' : item.text;
  return `<div class="clip-item"><span class="clip-text">${escapeHtml(preview)}</span><span class="clip-meta">${time}</span></div>`;
}

function escapeHtml(s) {
  return s.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));
}
