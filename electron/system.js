const os = require('os');

function cpuSnapshot() {
  return os.cpus().map((c) => ({ ...c.times, total: Object.values(c.times).reduce((a, b) => a + b, 0) }));
}

let lastSnapshot = cpuSnapshot();

function cpuPercent() {
  const now = cpuSnapshot();
  let idleDelta = 0, totalDelta = 0;
  now.forEach((core, i) => {
    idleDelta += core.idle - lastSnapshot[i].idle;
    totalDelta += core.total - lastSnapshot[i].total;
  });
  lastSnapshot = now;
  if (totalDelta <= 0) return 0;
  return Math.round((1 - idleDelta / totalDelta) * 100);
}

function stats() {
  const total = os.totalmem();
  const free = os.freemem();
  return {
    cpu: cpuPercent(),
    ramPercent: Math.round(((total - free) / total) * 100),
    ramUsedGB: +((total - free) / 1024 ** 3).toFixed(1),
    ramTotalGB: +(total / 1024 ** 3).toFixed(1),
    uptimeMin: Math.round(os.uptime() / 60),
  };
}

module.exports = { stats };
