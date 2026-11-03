const IMAGE_FORMATS = ['png', 'jpeg', 'webp'];
let pendingFile = null;

window.islaModules.renderers.converter = (el) => {
  el.innerHTML = `
    <div class="converter">
      <div class="conv-drop" id="convDrop">Drop a file here to convert</div>
      <div class="conv-targets" id="convTargets" hidden></div>
      <div class="conv-status" id="convStatus"></div>
    </div>`;

  const drop = el.querySelector('#convDrop');
  const targets = el.querySelector('#convTargets');
  const status = el.querySelector('#convStatus');

  ['dragenter', 'dragover'].forEach((evt) => drop.addEventListener(evt, (e) => e.preventDefault()));
  drop.addEventListener('drop', (e) => {
    e.preventDefault();
    const file = e.dataTransfer.files[0];
    if (!file) return;
    pendingFile = file.path;
    drop.textContent = file.name;
    renderTargets(file.name, targets, status);
  });
};

function renderTargets(name, targets, status) {
  const ext = name.split('.').pop().toLowerCase();
  const options = [];
  if (IMAGE_FORMATS.includes(ext) || ['jpg', 'bmp', 'tiff'].includes(ext)) {
    IMAGE_FORMATS.filter((f) => f !== ext).forEach((f) => options.push({ label: f.toUpperCase(), kind: 'image', to: f }));
  }
  if (['doc', 'docx'].includes(ext)) options.push({ label: 'PDF', kind: 'office', to: 'pdf' });
  options.push({ label: 'ZIP (compress)', kind: 'zip', to: 'zip' });

  targets.hidden = false;
  targets.innerHTML = options.map((o, i) => `<button data-i="${i}">${o.label}</button>`).join('');
  targets.querySelectorAll('button').forEach((btn, i) => {
    btn.addEventListener('click', async () => {
      const o = options[i];
      status.textContent = 'Converting...';
      try {
        const outPath = await window.isla.convert(pendingFile, o);
        status.textContent = `Done -> ${outPath}`;
      } catch (err) {
        status.textContent = `Failed: ${err.message || err}`;
      }
    });
  });
}
