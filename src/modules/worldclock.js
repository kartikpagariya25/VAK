const PRESET_ZONES = [
  { label: 'New York', tz: 'America/New_York' },
  { label: 'London', tz: 'Europe/London' },
  { label: 'Tokyo', tz: 'Asia/Tokyo' },
  { label: 'Dubai', tz: 'Asia/Dubai' },
  { label: 'Sydney', tz: 'Australia/Sydney' },
];

let worldClockTickId = null;

window.islaModules.renderers.worldclock = async function (el) {
  const cities = await window.isla.storeGet('worldClockCities', []);
  el.innerHTML = `
    <div class="wclock">
      <div class="wclock-local">
        <div class="wclock-time" id="wclockLocal">--:--</div>
        <span class="card-label">LOCAL</span>
      </div>
      <div class="wclock-list" id="wclockList"></div>
      <div class="wclock-add">
        ${PRESET_ZONES.filter((z) => !cities.some((c) => c.tz === z.tz)).map((z) => `<button data-tz="${z.tz}" data-label="${z.label}">+ ${z.label}</button>`).join('')}
      </div>
    </div>`;

  renderCities(el, cities);
  clearInterval(worldClockTickId);
  tickClocks(el, cities);
  worldClockTickId = setInterval(() => {
    if (!el.isConnected) { clearInterval(worldClockTickId); return; }
    tickClocks(el, cities);
  }, 1000 * 15);

  el.querySelectorAll('.wclock-add button').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const updated = [...cities, { label: btn.dataset.label, tz: btn.dataset.tz }];
      await window.isla.storeSet('worldClockCities', updated);
      window.islaModules.render('worldclock', el);
    });
  });
};

function renderCities(el, cities) {
  const list = el.querySelector('#wclockList');
  list.innerHTML = cities.map((c, i) => `
    <div class="wclock-row" data-i="${i}">
      <span>${c.label}</span>
      <span class="wclock-time-sm" data-tz="${c.tz}">--:--</span>
      <button class="wclock-remove" data-i="${i}">&times;</button>
    </div>`).join('') || '<div class="shelf-empty">Add a city below</div>';

  list.querySelectorAll('.wclock-remove').forEach((btn) => {
    btn.addEventListener('click', async () => {
      const next = cities.filter((_, i) => i !== Number(btn.dataset.i));
      await window.isla.storeSet('worldClockCities', next);
      window.islaModules.render('worldclock', el);
    });
  });
}

function tickClocks(el, cities) {
  const now = new Date();
  const localEl = el.querySelector('#wclockLocal');
  if (localEl) localEl.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  el.querySelectorAll('.wclock-time-sm').forEach((span) => {
    span.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', timeZone: span.dataset.tz });
  });
}
