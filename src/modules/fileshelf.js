window.islaModules.renderers.fileshelf = async function (el) {
  const items = await window.isla.shelfList();
  el.innerHTML = `
    <div class="shelf">
      <div class="shelf-drop">Drag files onto the island to add them here</div>
      <div class="shelf-list">${items.map(itemRow).join('') || '<div class="shelf-empty">No files yet</div>'}</div>
    </div>`;
  el.querySelectorAll('.shelf-item').forEach((row) => {
    row.addEventListener('dblclick', () => window.isla.shelfOpen(row.dataset.name));
    row.setAttribute('draggable', 'true');
    row.addEventListener('dragstart', (e) => {
      e.preventDefault();
      window.isla.shelfDragOut(row.dataset.name);
    });
  });
};

function itemRow(name) {
  return `<div class="shelf-item" data-name="${name}">${name}</div>`;
}

// Capture drops anywhere on the island, not just inside the panel,
// so dropping onto the collapsed pill still files something away.
const islandEl = document.getElementById('island');
['dragenter', 'dragover'].forEach((evt) => {
  islandEl.addEventListener(evt, (e) => {
    e.preventDefault();
    islandEl.classList.add('drag-over');
  });
});
['dragleave', 'drop'].forEach((evt) => {
  islandEl.addEventListener(evt, () => islandEl.classList.remove('drag-over'));
});
islandEl.addEventListener('drop', async (e) => {
  e.preventDefault();
  const paths = Array.from(e.dataTransfer.files).map((f) => f.path).filter(Boolean);
  if (!paths.length) return;
  await window.isla.shelfAdd(paths);
  const countEl = document.getElementById('shelfCount');
  if (countEl) countEl.textContent = 'Saved';
  if (document.querySelector('.dock button.active')?.dataset.module === 'fileshelf') {
    window.islaModules.render('fileshelf', document.getElementById('panel'));
  }
});
