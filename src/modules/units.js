const UNIT_CATEGORIES = {
  length: { base: 'm', units: { m: 1, km: 1000, cm: 0.01, mm: 0.001, mi: 1609.34, yd: 0.9144, ft: 0.3048, in: 0.0254 } },
  mass: { base: 'kg', units: { kg: 1, g: 0.001, mg: 0.000001, lb: 0.453592, oz: 0.0283495, ton: 1000 } },
  volume: { base: 'l', units: { l: 1, ml: 0.001, gal: 3.78541, qt: 0.946353, cup: 0.236588 } },
  speed: { base: 'ms', units: { ms: 1, kmh: 0.277778, mph: 0.44704, knot: 0.514444 } },
  data: { base: 'mb', units: { b: 1 / (1024 * 1024), kb: 1 / 1024, mb: 1, gb: 1024, tb: 1024 * 1024 } },
};

let activeCategory = 'length';

window.islaModules.renderers.units = function (el) {
  el.innerHTML = `
    <div class="units">
      <div class="units-tabs">${Object.keys(UNIT_CATEGORIES).map((c) => `<button data-cat="${c}" class="${c === activeCategory ? 'active' : ''}">${c}</button>`).join('')}</div>
      <div class="units-row">
        <input id="unitsFrom" type="number" value="1" />
        <select id="unitsFromUnit"></select>
      </div>
      <div class="units-row">
        <div id="unitsTo" class="units-result">0</div>
        <select id="unitsToUnit"></select>
      </div>
    </div>`;

  populateSelects(el);
  compute(el);

  el.querySelectorAll('.units-tabs button').forEach((btn) => {
    btn.addEventListener('click', () => {
      activeCategory = btn.dataset.cat;
      window.islaModules.render('units', el);
    });
  });
  el.querySelector('#unitsFrom').addEventListener('input', () => compute(el));
  el.querySelector('#unitsFromUnit').addEventListener('change', () => compute(el));
  el.querySelector('#unitsToUnit').addEventListener('change', () => compute(el));
};

function populateSelects(el) {
  const units = Object.keys(UNIT_CATEGORIES[activeCategory].units);
  const fromSel = el.querySelector('#unitsFromUnit');
  const toSel = el.querySelector('#unitsToUnit');
  fromSel.innerHTML = units.map((u) => `<option value="${u}">${u}</option>`).join('');
  toSel.innerHTML = units.map((u) => `<option value="${u}">${u}</option>`).join('');
  toSel.selectedIndex = Math.min(1, units.length - 1);
}

function compute(el) {
  const { units } = UNIT_CATEGORIES[activeCategory];
  const value = parseFloat(el.querySelector('#unitsFrom').value) || 0;
  const fromUnit = el.querySelector('#unitsFromUnit').value;
  const toUnit = el.querySelector('#unitsToUnit').value;
  const baseValue = value * units[fromUnit];
  const result = baseValue / units[toUnit];
  el.querySelector('#unitsTo').textContent = Number(result.toPrecision(6)).toString();
}
