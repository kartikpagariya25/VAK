const path = require('path');
const fs = require('fs');
const sharp = require('sharp');
const archiver = require('archiver');
const { app } = require('electron');

function outDir() {
  const dir = path.join(app.getPath('documents'), 'Isla Converted');
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

async function convert(filePath, target) {
  const base = path.parse(filePath).name;
  const dest = outDir();

  if (target.kind === 'image') {
    const outPath = path.join(dest, `${base}.${target.to}`);
    await sharp(filePath).toFormat(target.to).toFile(outPath);
    return outPath;
  }

  if (target.kind === 'zip') {
    const outPath = path.join(dest, `${base}.zip`);
    await new Promise((resolve, reject) => {
      const output = fs.createWriteStream(outPath);
      const archive = archiver('zip', { zlib: { level: 9 } });
      output.on('close', resolve);
      archive.on('error', reject);
      archive.pipe(output);
      archive.file(filePath, { name: path.basename(filePath) });
      archive.finalize();
    });
    return outPath;
  }

  if (target.kind === 'office') {
    // Needs LibreOffice installed (soffice on PATH) - see README for setup.
    const { exec } = require('child_process');
    const outPath = path.join(dest, `${base}.pdf`);
    await new Promise((resolve, reject) => {
      exec(`soffice --headless --convert-to pdf --outdir "${dest}" "${filePath}"`, (err) => err ? reject(err) : resolve());
    });
    return outPath;
  }

  throw new Error('Unsupported conversion');
}

module.exports = { convert, outDir };
