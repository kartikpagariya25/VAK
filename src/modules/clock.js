window.islaModules = window.islaModules || { renderers: {}, render(name, el) { (this.renderers[name] || this.renderers.home)(el); } };

function tick() {
  const now = new Date();
  const timeLabel = document.getElementById('timeLabel');
  const dateLabel = document.getElementById('dateLabel');
  const clockMini = document.getElementById('clockMini');
  const time = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  if (timeLabel) timeLabel.textContent = time;
  if (dateLabel) dateLabel.textContent = now.toLocaleDateString([], { weekday: 'short', day: 'numeric', month: 'short' });
  if (clockMini) clockMini.textContent = time;
}
tick();
setInterval(tick, 1000 * 15);
