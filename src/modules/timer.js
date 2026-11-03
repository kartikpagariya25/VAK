const timerState = { totalSec: 25 * 60, remaining: 25 * 60, running: false, intervalId: null };

window.islaModules.renderers.timer = function (el) {
  el.innerHTML = `
    <div class="timer">
      <div class="timer-face">
        <div class="timer-clock" id="timerClock">${formatTime(timerState.remaining)}</div>
        <div class="timer-label">Focus</div>
      </div>
      <div class="timer-controls">
        <button id="timerToggle">${timerState.running ? 'Pause' : 'Start'}</button>
        <button id="timerReset">Reset</button>
      </div>
      <div class="timer-presets">
        <button data-min="25">25m</button>
        <button data-min="5">5m</button>
        <button data-min="15">15m</button>
      </div>
    </div>`;

  renderTick(el);

  el.querySelector('#timerToggle').addEventListener('click', () => {
    timerState.running ? pause() : start(el);
    window.islaModules.render('timer', el);
  });
  el.querySelector('#timerReset').addEventListener('click', () => {
    pause();
    timerState.remaining = timerState.totalSec;
    window.islaModules.render('timer', el);
  });
  el.querySelectorAll('.timer-presets button').forEach((btn) => {
    btn.addEventListener('click', () => {
      pause();
      timerState.totalSec = Number(btn.dataset.min) * 60;
      timerState.remaining = timerState.totalSec;
      window.islaModules.render('timer', el);
    });
  });
};

function start(el) {
  timerState.running = true;
  timerState.intervalId = setInterval(() => {
    timerState.remaining -= 1;
    if (timerState.remaining <= 0) {
      pause();
      timerState.remaining = 0;
      notifyDone();
    }
    renderTick(el);
  }, 1000);
}

function pause() {
  timerState.running = false;
  clearInterval(timerState.intervalId);
}

function renderTick(el) {
  const clockEl = el.querySelector('#timerClock');
  if (clockEl) clockEl.textContent = formatTime(timerState.remaining);
}

function formatTime(sec) {
  const m = String(Math.floor(sec / 60)).padStart(2, '0');
  const s = String(sec % 60).padStart(2, '0');
  return `${m}:${s}`;
}

function notifyDone() {
  try { new Notification('Isla', { body: 'Focus session done - take a break.' }); } catch (e) { /* notifications may be blocked by the OS */ }
}
