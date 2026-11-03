// The pieces the web playtests share (playtest.js, sweep.js): a run's notes and checks, a browser page with the game
// loaded, the game's playtest remote control (Client/Playtest.cs), and pressing the game's own buttons with real
// clicks and taps where the game says they are drawn (Ui.LastDrawn, the "buttons" command).
'use strict';
const fs = require('fs');
const path = require('path');

// The notes, checks and errors of one run, written to report.md at the end.
function createRun(out) {
  const run = { out, report: [], errors: [], passed: 0, failed: 0 };
  run.note = line => {
    run.report.push(line);
    console.log(line);
  };
  run.check = (ok, what) => {
    run.note(`- ${ok ? '통과' : '**실패**'}: ${what}`);
    if (ok) run.passed++;
    else {
      run.failed++;
      run.errors.push(`check failed: ${what}`);
    }
    return ok;
  };
  run.write = () => {
    run.note(`\n## 에러\n`);
    if (run.errors.length === 0) run.note('- 없음');
    for (const error of [...new Set(run.errors)]) run.note(`- ${error}`);
    fs.writeFileSync(path.join(out, 'report.md'), run.report.join('\n') + '\n');
  };
  return run;
}

const Profiles = {
  pc: { name: 'pc', context: { viewport: { width: 1280, height: 720 } } },
  'pc-small': { name: 'pc-small', context: { viewport: { width: 800, height: 600 } } },
  phone: { name: 'phone', touch: true, context: { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  'phone-small': { name: 'phone-small', touch: true, context: { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  'phone-wide': { name: 'phone-wide', touch: true, context: { viewport: { width: 844, height: 390 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  'phone-wide-small': { name: 'phone-wide-small', touch: true, context: { viewport: { width: 740, height: 360 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
};

// One browser page with the game loaded and its console collected. `context` reuses a browser context (its saves).
async function open(browser, run, profile, url, context) {
  context = context || (await browser.newContext({ ...profile.context, acceptDownloads: true }));
  const page = await context.newPage();
  const game = { run, profile, context, page, logs: [], touch: !!profile.touch };
  page.on('console', m => {
    const text = m.text();
    if (text.includes('memorysetup')) return;
    game.logs.push(text);
    if (m.type() === 'error' || /Exception|NullReference|IndexOutOfRange|KeyNotFound|ArgumentOutOfRange/.test(text)) {
      run.errors.push(`${profile.name}: ${text.split('\n')[0]}`);
    }
  });
  page.on('pageerror', e => run.errors.push(`${profile.name}: page error ${e.message}`));
  await page.goto(url + '?' + Date.now(), { waitUntil: 'load', timeout: 180000 });
  await page.waitForFunction(() => !!window.unityInstance, null, { timeout: 240000 });
  await page.waitForTimeout(2000);
  Object.assign(game, helpers(game));
  await game.send('probe on');
  await game.wait(300);
  return game;
}

function helpers(game) {
  const { page, run, profile } = game;
  const h = {};
  h.wait = ms => page.waitForTimeout(ms);

  // Sends playtest commands and returns the [playtest] lines they answered with.
  // Each command answers with one line, so the wait ends as soon as all have.
  h.send = async commands => {
    const mark = game.logs.length;
    const expected = commands.split(';').filter(c => c.trim()).length;
    await page.evaluate(text => window.unityInstance.SendMessage('Game', 'Playtest', text), commands);
    const answered = () => game.logs.slice(mark).filter(l => l.startsWith('[playtest]'));
    const deadline = Date.now() + 3000;
    while (answered().length < expected && Date.now() < deadline) await page.waitForTimeout(15);
    const lines = answered().map(l => l.slice(11));
    for (const line of lines) if (line.startsWith('error')) run.errors.push(`${profile.name}: playtest ${line}`);
    return lines;
  };

  // A command answering with JSON after its name.
  h.ask = async command => {
    const word = command.split(' ')[0];
    const lines = await h.send(command);
    const line = lines.reverse().find(l => l.startsWith(word + ' '));
    try {
      return line ? JSON.parse(line.slice(word.length + 1)) : {};
    } catch (e) {
      run.errors.push(`${profile.name}: bad JSON from ${command}: ${line.slice(0, 200)}`);
      return {};
    }
  };
  h.state = () => h.ask('report');
  h.texts = () => h.ask('texts');
  h.inventory = () => h.ask('inventory');
  h.look = (dx, dy) => h.ask(`look ${dx} ${dy}`);

  // Every line the screen shows, for looking a word up.
  h.allText = async () => {
    const t = await h.texts();
    return [...(t.hud || []), ...(t.acquired || []), ...(t.floating || []), t.banner || '', t.status || ''].join(' | ');
  };

  h.canvas = () =>
    page.evaluate(() => {
      const r = document.querySelector('canvas').getBoundingClientRect();
      return { left: r.left, top: r.top, width: r.width, height: r.height };
    });

  h.buttons = async () => {
    const answer = await h.ask('buttons');
    const box = await h.canvas();
    const sx = box.width / (answer.width || box.width);
    const sy = box.height / (answer.height || box.height);
    return (answer.buttons || []).map(b => ({
      ...b,
      cx: box.left + (b.rect[0] + b.rect[2] / 2) * sx,
      cy: box.top + (b.rect[1] + b.rect[3] / 2) * sy,
      screen: { width: answer.width, height: answer.height },
    }));
  };

  function matches(b, label, row) {
    const labelOk = label instanceof RegExp ? label.test(b.label) : b.label === label;
    const rowOk = !row || (row instanceof RegExp ? row.test(b.row) : b.row.includes(row));
    return labelOk && rowOk;
  }

  h.find = async (label, row) => (await h.buttons()).filter(b => matches(b, label, row));

  // A real press where the button is drawn: a click on PC, a tap on a touch screen. A button scrolled out of its
  // list is first scrolled to with the mouse wheel (the game's own wheel step). False when there is none to press.
  h.press = async (label, row, { quiet } = {}) => {
    for (let attempt = 0; attempt < 12; attempt++) {
      const all = await h.find(label, row);
      const shown = all.find(b => b.shown && b.enabled);
      if (shown) {
        await h.tapAt(shown.cx, shown.cy);
        await page.waitForTimeout(450);
        return true;
      }
      const hidden = all.find(b => !b.shown);
      if (!hidden) break;
      // Scroll toward it with the wheel over the middle of the screen in its column: below the middle, down.
      const box = await h.canvas();
      const middle = box.top + box.height / 2;
      await page.mouse.move(hidden.cx, middle);
      await page.mouse.wheel(0, hidden.cy > middle ? 200 : -200);
      await page.waitForTimeout(300);
    }
    if (!quiet) run.errors.push(`${profile.name}: no button "${label}"${row ? ` in row "${row}"` : ''} to press`);
    return false;
  };

  h.tapAt = async (x, y) => {
    if (game.touch) {
      await page.touchscreen.tap(x, y);
    } else {
      await page.mouse.move(x, y);
      await page.mouse.down();
      await page.waitForTimeout(60);
      await page.mouse.up();
    }
  };

  h.shot = async (name, caption) => {
    const file = path.join(run.out, `${profile.name}-${name}.png`);
    await page.screenshot({ path: file });
    run.note(`- 사진 ${caption}: \`${path.basename(file)}\``);
  };

  // Waits until the test passes on the game's report (polled), up to the time given.
  h.until = async (test, ms = 8000, ask = h.state) => {
    const deadline = Date.now() + ms;
    let last = {};
    while (Date.now() < deadline) {
      last = await ask();
      if (test(last)) return last;
      await page.waitForTimeout(400);
    }
    return null;
  };

  h.startSolo = async (name = 'playtest', keep = false) => {
    await h.send(`solo ${name}${keep ? ' keep' : ''}`);
    return !!(await h.until(s => s.playing, 25000));
  };

  // A finger on the screen at (x, y) moved by (dx, dy) over a second, through the DevTools protocol.
  h.drag = async (x, y, dx, dy, hold = 800) => {
    const cdp = await game.context.newCDPSession(page);
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
    for (let i = 1; i <= 10; i++) {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x + (dx * i) / 10, y: y + (dy * i) / 10 }] });
      await page.waitForTimeout(80);
    }
    await page.waitForTimeout(hold);
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await page.waitForTimeout(200);
  };

  return h;
}

// Waits (up to 45 minutes) until the page says it is the given commit (version.txt).
async function waitForCommit(url, sha) {
  const deadline = Date.now() + 45 * 60 * 1000;
  for (;;) {
    let deployed = '';
    try {
      deployed = (await (await fetch(url + 'version.txt?' + Date.now())).text()).trim();
    } catch (e) {
      deployed = '';
    }
    if (deployed === sha) return deployed;
    if (Date.now() > deadline) throw new Error(`the page is still ${deployed || 'unknown'}, not ${sha}`);
    console.log(`waiting for ${sha.slice(0, 7)} (the page is ${deployed.slice(0, 7) || 'unknown'})`);
    await new Promise(r => setTimeout(r, 30000));
  }
}

async function deployedCommit(url) {
  try {
    return (await (await fetch(url + 'version.txt?' + Date.now())).text()).trim();
  } catch (e) {
    return '';
  }
}

const LaunchArgs = ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'];

module.exports = { createRun, Profiles, open, waitForCommit, deployedCommit, LaunchArgs };
