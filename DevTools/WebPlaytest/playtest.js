#!/usr/bin/env node
// Plays the web demo in headless Chromium and checks what a person would otherwise check by hand (Docs/Playtest.md):
// it starts a solo game, uses the real keyboard and mouse on a PC-sized screen and real touches (taps, a stick
// dragged) on phone-sized ones, sets scenes up through the game's playtest remote control (Client/Playtest.cs),
// and writes screenshots, the game's own reports and every error the browser saw to an output folder with a
// report.md. The cloud session runs it and looks at the pictures.
//
//   node DevTools/WebPlaytest/playtest.js [--url URL] [--wait [SHA]] [--out DIR] [--profiles pc,phone,phone-wide]
//
//   --wait     first wait (up to 45 minutes) until the page is the given commit (version.txt), by default the newest
//              commit that changes what the web build is made of
//   --profiles which screens to play on (all three by default)
//
// Needs Playwright with Chromium (preinstalled in the cloud container: require('playwright') works anywhere).
// WebGL runs on SwiftShader there, about 20 frames a second.
'use strict';
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { chromium } = require('playwright');
const { makePilot } = require('./bot');

const args = process.argv.slice(2);
function option(name, fallback) {
  const i = args.indexOf(name);
  if (i < 0) return fallback;
  const next = args[i + 1];
  return next && !next.startsWith('--') ? next : true;
}
const url = option('--url', 'https://learc6910.github.io/pray-to/').replace(/\/?$/, '/');
const out = path.resolve(option('--out', path.join(__dirname, 'out')));
const profiles = String(option('--profiles', 'pc,phone,phone-wide')).split(',');
const wait = option('--wait', false);

const report = [];
const errors = [];
function note(line) {
  report.push(line);
  console.log(line);
}

async function waitForCommit(sha) {
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

// One browser page with the game loaded, its console collected.
async function open(browser, profile) {
  const context = await browser.newContext(profile.context);
  const page = await context.newPage();
  const logs = [];
  page.on('console', m => {
    const text = m.text();
    if (text.includes('memorysetup')) return;
    logs.push(text);
    if (m.type() === 'error' || /Exception|NullReference|IndexOutOfRange|KeyNotFound/.test(text)) errors.push(`${profile.name}: ${text.split('\n')[0]}`);
  });
  page.on('pageerror', e => errors.push(`${profile.name}: page error ${e.message}`));
  await page.goto(url + '?' + Date.now(), { waitUntil: 'load', timeout: 180000 });
  await page.waitForFunction(() => !!window.unityInstance, null, { timeout: 240000 });
  await page.waitForTimeout(2000);
  return { context, page, logs };
}

// Sends playtest commands and returns the [playtest] lines they answered with.
async function send(game, commands) {
  const mark = game.logs.length;
  await game.page.evaluate(text => window.unityInstance.SendMessage('Game', 'Playtest', text), commands);
  await game.page.waitForTimeout(300);
  const lines = game.logs.slice(mark).filter(l => l.startsWith('[playtest]')).map(l => l.slice(11));
  for (const line of lines) if (line.startsWith('error')) errors.push(`playtest ${line}`);
  return lines;
}

async function state(game) {
  const lines = await send(game, 'report');
  const line = lines.reverse().find(l => l.startsWith('report '));
  return line ? JSON.parse(line.slice(7)) : {};
}

async function shot(game, profile, name, caption) {
  const file = path.join(out, `${profile.name}-${name}.png`);
  await game.page.screenshot({ path: file });
  note(`- ${caption}: \`${path.basename(file)}\``);
}

// A new character has no class (2026-11-09): given a warrior and a fire mage, so the skill keys have something.
async function startSolo(game) {
  await send(game, 'solo');
  for (let i = 0; i < 40; i++) {
    await game.page.waitForTimeout(500);
    if ((await state(game)).playing) {
      await send(game, 'classes warrior,fire_mage');
      await game.page.waitForTimeout(400);
      return true;
    }
  }
  return false;
}

function check(ok, what) {
  note(`- ${ok ? '통과' : '**실패**'}: ${what}`);
  if (!ok) errors.push(`check failed: ${what}`);
}

// A finger on the screen at (x, y) moved by (dx, dy) over a second, through the DevTools protocol.
async function drag(game, x, y, dx, dy) {
  const cdp = await game.context.newCDPSession(game.page);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
  for (let i = 1; i <= 10; i++) {
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x + (dx * i) / 10, y: y + (dy * i) / 10 }] });
    await game.page.waitForTimeout(100);
  }
  await game.page.waitForTimeout(800);
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

const Profiles = {
  pc: { name: 'pc', context: { viewport: { width: 1280, height: 720 } } },
  phone: { name: 'phone', context: { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
  'phone-wide': { name: 'phone-wide', context: { viewport: { width: 844, height: 390 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true } },
};

async function playPc(browser, profile) {
  note(`\n## PC 화면 1280×720 (키보드·마우스)\n`);
  let game = await open(browser, profile);
  await shot(game, profile, 'menu', '메뉴');
  check(await startSolo(game), '혼자 하기가 시작된다');
  await game.page.mouse.move(900, 360);
  await game.page.mouse.click(900, 360);
  await shot(game, profile, 'solo', '혼자 하기 첫 화면(HUD, 마나 막대, 스킬·퀵슬롯)');

  const before = await state(game);
  await game.page.keyboard.down('d');
  await game.page.waitForTimeout(1200);
  await game.page.keyboard.up('d');
  const moved = await state(game);
  check(moved.x > before.x + 1, `D 키로 오른쪽으로 걷는다 (x ${before.x} → ${moved.x})`);

  await game.page.keyboard.press('q');
  await game.page.waitForTimeout(600);
  const cast = await state(game);
  check(cast.mana < before.maxMana, `Q 키로 스킬을 쓰면 마나가 준다 (${before.mana} → ${cast.mana})`);
  await shot(game, profile, 'skill', 'Q 스킬(회전베기)');

  await send(game, 'mana 5');
  await game.page.waitForTimeout(400);
  await shot(game, profile, 'low-mana', '마나 5: 마나가 모자란 스킬 칸이 어둡다');
  await send(game, 'potion mana_potion 1; drink mana_potion');
  const drunk = await state(game);
  check(drunk.mana >= 50, `마나 물약이 마나를 채운다 (5 → ${drunk.mana})`);

  // The combo: held mouse button against slimes, pictures through the three steps.
  await send(game, 'spawn slime 1.2 0 5000; spawn slime 1.2 0.5 5000; spawn slime 1.2 -0.5 5000');
  await game.page.mouse.move(1000, 360);
  await game.page.mouse.down();
  for (let i = 1; i <= 4; i++) {
    await game.page.waitForTimeout(260);
    await shot(game, profile, `combo-${i}`, `공격을 누르고 있는 중 ${i}`);
  }
  await game.page.waitForTimeout(1500);
  await game.page.mouse.up();
  const hit = await state(game);
  check(hit.hurtMonsters > 0, `마우스를 누르고 있으면 슬라임을 친다 (맞은 슬라임 ${hit.hurtMonsters})`);
  await send(game, 'clear; heal');

  // The bot by the game's state (bot.js, Client/PlaytestBot.cs, 2026-11-11): the game's path finding says where to go
  // next, and the real keys walk there; the nearest monster, wherever it is, and then rock dug for stone.
  await huntAndDig(game, profile);

  await game.page.keyboard.press('Tab');
  await game.page.waitForTimeout(600);
  check((await state(game)).bag, 'Tab 키로 가방이 열린다');
  await shot(game, profile, 'bag', '가방 창');
  await game.page.keyboard.press('Tab');
  await send(game, 'scene set; bag on');
  await game.page.waitForTimeout(800);
  await shot(game, profile, 'bag-set', '공허 순례자 세트를 입은 가방 창');
  await send(game, 'bag off');

  await send(game, 'scene machines');
  await game.page.waitForTimeout(3500);
  const machines = await state(game);
  check(/WaterWheel\+/.test(machines.machines), `물가의 물레방아가 돈다 (${machines.machines})`);
  await shot(game, profile, 'machines', '새 기계: 물레방아·태양광·전기로·번개 탑·포탑·거름망·수확기');

  await send(game, 'clock 3 23');
  await game.page.waitForTimeout(1500);
  const night = await state(game);
  check(/SolarPanel(?!\+)/.test(night.machines), `밤에는 태양광 판이 쉰다 (${night.machines})`);
  await send(game, 'scene skylight; clock 3 9');
  await game.page.waitForTimeout(1500);
  await shot(game, profile, 'morning', '아침 빛 구멍과 채광창');
  await send(game, 'clock 3 19');
  await game.page.waitForTimeout(1500);
  await shot(game, profile, 'evening', '저녁 노을');
  await send(game, 'clock 8 23');
  await game.page.waitForTimeout(1500);
  const moon = await state(game);
  check(moon.event === 'FullMoon', `8일째는 보름달 (${moon.event})`);
  await shot(game, profile, 'fullmoon-night', '보름달 밤(달빛)');
  await send(game, 'clock 5 13');
  await game.page.waitForTimeout(1500);
  await shot(game, profile, 'market', '5일째 장날 낮(HUD에 행사)');
  for (const weather of ['storm', 'fog', 'rain']) {
    await send(game, `weather ${weather}`);
    await game.page.waitForTimeout(weather === 'storm' ? 4000 : 2000);
    await shot(game, profile, `weather-${weather}`, `날씨: ${weather}`);
  }

  // Gear that lasts, so the pictures are of the scene rather than of the player lying down.
  await send(game, 'clear; equip void_sword void_helmet void_armor void_boots; heal; scene defense');
  await game.page.waitForTimeout(5000);
  const defense = await state(game);
  check(!!defense.defense, `기지 방어가 시작된다 (${defense.defense})`);
  await shot(game, profile, 'defense', '기지 방어');
  await game.context.close();

  game = await open(browser, profile);
  await startSolo(game);
  await send(game, 'equip void_sword void_helmet void_armor void_boots; heal; scene rift');
  await game.page.waitForTimeout(5000);
  const rift = await state(game);
  check(!!rift.rift, `균열이 열린다 (${rift.rift})`);
  await shot(game, profile, 'rift', '공허 균열');
  await game.context.close();
}

// The playtest's JSON answer to a command.
async function ask(game, command) {
  const word = command.split(' ')[0];
  const line = (await send(game, command)).reverse().find(l => l.startsWith(word + ' '));
  try {
    return line ? JSON.parse(line.slice(word.length + 1)) : {};
  } catch (e) {
    return {};
  }
}

async function huntAndDig(game, profile) {
  const keys = { '1,0': 'd', '-1,0': 'a', '0,1': 'w', '0,-1': 's' };
  const wait = ms => game.page.waitForTimeout(ms);
  const pilot = makePilot({
    ask: command => ask(game, command),
    wait,
    // Both keys of a diagonal, about as long as the way takes at a walk.
    move: async (dx, dy) => {
      const d = Math.hypot(dx, dy);
      if (d < 0.05) return;
      const held = [];
      if (Math.abs(dx) > d * 0.38) held.push(keys[`${Math.sign(dx)},0`]);
      if (Math.abs(dy) > d * 0.38) held.push(keys[`0,${Math.sign(dy)}`]);
      for (const k of held) await game.page.keyboard.down(k);
      await wait(Math.min(900, Math.round(d * 230 + 60)));
      for (const k of held) await game.page.keyboard.up(k);
    },
    dig: async (dx, dy) => {
      await send(game, `face ${dx} ${dy}; attack on ${dx} ${dy}`);
      await wait(1500);
      await send(game, 'attack off; face off');
    },
    strike: async (dx, dy, ms) => {
      const d = Math.hypot(dx, dy) || 1;
      await send(game, `attack on ${(dx / d).toFixed(2)} ${(dy / d).toFixed(2)}`);
      await wait(ms);
      await send(game, 'attack off');
    },
    hold: async what => {
      await game.page.keyboard.press(what === 'pickaxe' ? '2' : '1');
      await wait(200);
    },
    note,
    shot: (name, caption) => shot(game, profile, name, caption),
  });
  await send(game, 'probe on; botreset');
  const started = Date.now();
  const kills = async () => (await ask(game, 'sense')).kills || '';
  const hunted = await pilot.go('monster', { until: async () => (await kills()) !== '', seconds: 120 });
  const s = await ask(game, 'sense');
  check(hunted === 'done', `길잡이(nav)로 가장 가까운 몬스터에게 걸어가 잡는다 (${hunted}, ${Math.round((Date.now() - started) / 1000)}초, 잡은 것 ${s.kills || '없음'}, 걸음 ${pilot.counts.moves}, 캔 벽 ${pilot.counts.digs})`);
  await shot(game, profile, 'nav-hunt', '길잡이로 사냥한 뒤');
  const stone = async () => ((await ask(game, 'inventory')).materials || {}).stone || 0;
  const before = await stone();
  const dug = await pilot.go('wall', { until: async () => (await stone()) >= before + 3, seconds: 60 });
  check(dug === 'done', `길잡이로 벽을 캐서 돌 3개를 모은다 (${dug})`);
  const numbers = await ask(game, 'botreport 웹 PC 짧은 점검');
  if (typeof numbers === 'string') note('\n' + numbers);
  await send(game, 'clear; heal');
}

async function playPhone(browser, profile) {
  const { width, height } = profile.context.viewport;
  note(`\n## ${profile.name === 'phone' ? '휴대폰 세로' : '휴대폰 가로'} ${width}×${height} (터치)\n`);
  const game = await open(browser, profile);
  await shot(game, profile, 'menu', '메뉴');
  check(await startSolo(game), '혼자 하기가 시작된다');
  // A tap on empty ground: from then on the game lays itself out for touch.
  await game.page.touchscreen.tap(width / 2, height / 3);
  await game.page.waitForTimeout(800);
  const start = await state(game);
  check(start.touch, '터치를 하면 터치 화면(스틱·단추)이 된다');
  await shot(game, profile, 'solo', '터치 화면 첫 화면');

  // The move stick: a finger dragged on the left half.
  await drag(game, width * 0.2, height * 0.75, 60, 0);
  const moved = await state(game);
  check(moved.x > start.x + 0.5, `왼쪽 스틱을 끌면 걷는다 (x ${start.x} → ${moved.x})`);

  // The first skill button, bottom right: two columns of 62-point buttons 12 points from the edges.
  const rows = 2;
  const left = width - 12 - 2 * 62 - 6;
  const top = height - 12 - 62 - (rows - 1) * 68;
  await game.page.touchscreen.tap(left + 31, top + 31);
  await game.page.waitForTimeout(600);
  const cast = await state(game);
  check(cast.mana < start.maxMana, `스킬 단추를 누르면 스킬이 나간다 (마나 ${start.mana} → ${cast.mana})`);

  await send(game, 'spawn slime 1.2 0 5000; spawn slime -1.2 0 5000');
  await drag(game, width * 0.8, height * 0.4, -50, 0);
  await shot(game, profile, 'attack', '공격 스틱을 끌어 공격');
  await send(game, 'clear; heal');

  // The bag button under the minimap.
  await game.page.touchscreen.tap(width - 10 - 120 + 27, 10 + 120 + 10 + 20);
  await game.page.waitForTimeout(800);
  check((await state(game)).bag, '미니맵 아래 [가방] 단추로 가방이 열린다');
  await shot(game, profile, 'bag', '가방 창');
  await send(game, 'bag off; scene machines');
  await game.page.waitForTimeout(2500);
  await shot(game, profile, 'machines', '기계 장면');
  await game.context.close();
}

(async () => {
  fs.mkdirSync(out, { recursive: true });
  for (const file of fs.readdirSync(out)) if (/\.(png|md)$/.test(file)) fs.unlinkSync(path.join(out, file));
  let deployed = '';
  if (wait) {
    const sha = wait === true
      ? execSync("git log -1 --format=%H -- . ':!*.md' ':!Docs' ':!DevTools'", { cwd: path.join(__dirname, '..', '..') }).toString().trim()
      : execSync(`git rev-parse ${wait}`, { cwd: path.join(__dirname, '..', '..') }).toString().trim();
    deployed = await waitForCommit(sha);
  } else {
    try {
      deployed = (await (await fetch(url + 'version.txt?' + Date.now())).text()).trim();
    } catch (e) {
      deployed = '';
    }
  }
  note(`# 웹 플레이 시험\n\n${new Date().toISOString()}, ${url}, 커밋 ${/^[0-9a-f]{40}$/.test(deployed) ? deployed.slice(0, 7) : '알 수 없음'}`);

  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  try {
    for (const name of profiles) {
      const profile = Profiles[name];
      if (!profile) throw new Error(`no profile '${name}'`);
      try {
        if (name === 'pc') await playPc(browser, profile);
        else await playPhone(browser, profile);
      } catch (e) {
        errors.push(`${name}: ${e.message.split('\n')[0]}`);
        note(`- **중단**: ${e.message.split('\n')[0]}`);
      }
    }
  } finally {
    await browser.close();
  }
  note(`\n## 에러\n`);
  if (errors.length === 0) note('- 없음');
  for (const error of [...new Set(errors)]) note(`- ${error}`);
  fs.writeFileSync(path.join(out, 'report.md'), report.join('\n') + '\n');
  process.exit(errors.length === 0 ? 0 : 1);
})();
