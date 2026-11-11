#!/usr/bin/env node
// Hit feel side by side (Docs/Feel.md, the research report's candidate 10): plays the deployed web demo, strikes a
// sturdy slime for a few seconds with every piece of a blow's feedback on, then with each group off in turn (the
// playtest's "feel" command, Client/Feel.cs), records it all as one video and cuts it into a GIF for each, plus one
// with all on beside all off. A person looks at the GIFs and says which pieces earn their place.
//
//   node DevTools/WebPlaytest/feel.js [--wait [SHA]] [--url URL] [--out DIR]
//
// Writes out/feel/<variant>.gif, out/feel/compare.gif and out/feel/report.md. Needs ffmpeg.
'use strict';
const fs = require('fs');
const path = require('path');
const { execSync, execFileSync } = require('child_process');
const { chromium } = require('playwright');
const { createRun, Profiles, open, waitForCommit, deployedCommit, LaunchArgs } = require('./lib');

const args = process.argv.slice(2);
function option(name, fallback) {
  const i = args.indexOf(name);
  if (i < 0) return fallback;
  const next = args[i + 1];
  return next && !next.startsWith('--') ? next : true;
}
const url = option('--url', 'https://learc6910.github.io/pray-to/').replace(/\/?$/, '/');
const out = path.resolve(option('--out', path.join(__dirname, 'out', 'feel')));
const wait = option('--wait', false);
const run = createRun(out);
const { note } = run;

// Each variant: its file name, what it shows, and the pieces turned off (Feel.Names).
const Variants = [
  ['all-on', '모두 켬', []],
  ['no-shake', '흔들림·멈춤 끔', ['shake', 'hitstop']],
  ['no-impact', '별·번쩍임 끔', ['impact', 'flash']],
  ['no-knock', '밀림·내밀기 끔', ['knock', 'lunge']],
  ['no-numbers', '숫자 끔', ['numbers']],
  ['no-swing', '휘두름 호 끔', ['swing']],
  ['all-off', '모두 끔', ['all']],
];
const StrikeSeconds = 3.2;
// The middle of the 1280×720 view, where the player and the slime stand.
const Crop = { width: 480, height: 300 };

(async () => {
  fs.mkdirSync(out, { recursive: true });
  for (const file of fs.readdirSync(out)) if (/\.(gif|webm|md|png)$/.test(file)) fs.unlinkSync(path.join(out, file));
  let deployed = '';
  if (wait) {
    const root = path.join(__dirname, '..', '..');
    const sha = wait === true
      ? execSync("git log -1 --format=%H -- . ':!*.md' ':!Docs' ':!DevTools'", { cwd: root }).toString().trim()
      : execSync(`git rev-parse ${wait}`, { cwd: root }).toString().trim();
    deployed = await waitForCommit(url, sha);
  } else {
    deployed = await deployedCommit(url);
  }
  note(`# 손맛 비교\n\n${new Date().toISOString()}, ${url}, 커밋 ${/^[0-9a-f]{40}$/.test(deployed) ? deployed.slice(0, 7) : '알 수 없음'}\n`);
  const browser = await chromium.launch({ args: LaunchArgs });
  const videoDir = path.join(out, 'video');
  fs.rmSync(videoDir, { recursive: true, force: true });
  const context = await browser.newContext({ ...Profiles.pc.context, recordVideo: { dir: videoDir, size: Profiles.pc.context.viewport } });
  const started = Date.now();
  const spans = [];
  try {
    const game = await open(browser, run, Profiles.pc, url, context);
    await game.startSolo('feel');
    await game.wait(1500);
    await game.send('goto biome Cave; equip copper_sword; heal');
    await game.wait(800);
    for (const [name, label, off] of Variants) {
      await game.send('feel all on' + off.map(piece => `; feel ${piece} off`).join(''));
      // A slime that lives through it, just to the right; the player swings at it.
      await game.send('clear; heal; spawn slime 1.1 0 100000; face 1 0; attack on 1 0');
      const from = (Date.now() - started) / 1000;
      await game.wait(StrikeSeconds * 1000);
      spans.push({ name, label, from, to: (Date.now() - started) / 1000 });
      await game.send('attack off; clear');
      await game.wait(600);
    }
    await game.send('feel all on');
  } finally {
    await context.close();
    await browser.close();
  }

  const video = fs.readdirSync(videoDir).filter(f => f.endsWith('.webm')).map(f => path.join(videoDir, f))[0];
  if (!video) throw new Error('no video recorded');
  const [w, h] = [Profiles.pc.context.viewport.width, Profiles.pc.context.viewport.height];
  const crop = `crop=${Crop.width}:${Crop.height}:${(w - Crop.width) / 2}:${(h - Crop.height) / 2}`;
  const palette = 'split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=none';
  for (const span of spans) {
    // The page loads before the first frame of the video, so the times run a little late: start half a second in.
    execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-ss', String(span.from + 0.5), '-t', String(span.to - span.from - 0.6), '-i', video,
      '-vf', `${crop},fps=20,${palette}`, path.join(out, `${span.name}.gif`)]);
    note(`- ${span.name}.gif: ${span.label}`);
  }
  const on = spans.find(s => s.name === 'all-on'), offAll = spans.find(s => s.name === 'all-off');
  execFileSync('ffmpeg', ['-y', '-loglevel', 'error',
    '-ss', String(on.from + 0.5), '-t', '2.5', '-i', video, '-ss', String(offAll.from + 0.5), '-t', '2.5', '-i', video,
    '-filter_complex', `[0:v]${crop},fps=20[l];[1:v]${crop},fps=20[r];[l][r]hstack,${palette}`, path.join(out, 'compare.gif')]);
  note('- compare.gif: 왼쪽 모두 켬, 오른쪽 모두 끔');
  run.write();
  process.exit(run.errors.length === 0 ? 0 : 1);
})();
