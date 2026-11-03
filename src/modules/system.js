let systemPollId = null;

window.islaModules.renderers.system = async function (el) {
  el.innerHTML = `
    <div class="sys">
      <div class="sys-row">
        <div class="sys-stat"><span class="card-label">CPU</span><div class="sys-value" id="sysCpu">--%</div></div>
        <div class="sys-stat"><span class="card-label">RAM</span><div class="sys-value" id="sysRam">--%</div></div>
        <div class="sys-stat"><span class="card-label">BATTERY</span><div class="sys-value" id="sysBattery">--%</div></div>
      </div>
      <div class="sys-detail" id="sysDetail"></div>
    </div>`;

  clearInterval(systemPollId);
  await refreshSystem(el);
  systemPollId = setInterval(() => refreshSystem(el), 2000);
};

async function refreshSystem(el) {
  if (!el.isConnected) { clearInterval(systemPollId); return; }
  const stats = await window.isla.systemStats();
  const cpuEl = el.querySelector('#sysCpu');
  const ramEl = el.querySelector('#sysRam');
  const detailEl = el.querySelector('#sysDetail');
  if (cpuEl) cpuEl.textContent = `${stats.cpu}%`;
  if (ramEl) ramEl.textContent = `${stats.ramPercent}%`;
  if (detailEl) detailEl.textContent = `${stats.ramUsedGB} / ${stats.ramTotalGB} GB used - up ${Math.floor(stats.uptimeMin / 60)}h ${stats.uptimeMin % 60}m`;

  if (navigator.getBattery) {
    const batt = await navigator.getBattery();
    const battEl = el.querySelector('#sysBattery');
    if (battEl) battEl.textContent = `${Math.round(batt.level * 100)}%${batt.charging ? ' +' : ''}`;
  }
}
