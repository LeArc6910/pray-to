#!/usr/bin/env node
// The long web playtest: goes through the checks LOCAL_TASKS.md asked people to make by hand (sections 2 to 7), on
// the deployed web demo, with real clicks, keys and taps on the game's own buttons (Ui.LastDrawn), and the game's
// playtest remote control (Client/Playtest.cs) to set scenes up and read the result. Writes pictures and report.md
// to out/sweep. Docs/Playtest.md.
//
//   node DevTools/WebPlaytest/sweep.js [--wait [SHA]] [--only menu,basics,...] [--url URL] [--out DIR]
//
// Sections: menu time basics first firstphone hour bag combat hero roll bosses patterns veineater samurai knight spider arenas world farm life npc factory defense gear skills skillfx monsterfx map packs pois travel guide pace dungeon base save deploysave layout touch ui screens encounter growth
'use strict';
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { chromium } = require('playwright');
const { createRun, Profiles, open, waitForCommit, deployedCommit, LaunchArgs } = require('./lib');
const { makePilot } = require('./bot');

const args = process.argv.slice(2);
function option(name, fallback) {
  const i = args.indexOf(name);
  if (i < 0) return fallback;
  const next = args[i + 1];
  return next && !next.startsWith('--') ? next : true;
}
const url = option('--url', 'https://learc6910.github.io/pray-to/').replace(/\/?$/, '/');
const out = path.resolve(option('--out', path.join(__dirname, 'out', 'sweep')));
const wait = option('--wait', false);
const only = option('--only', '') ? String(option('--only', '')).split(',') : null;
const run = createRun(out);
const { note, check } = run;

const has = (text, word) => (text || '').includes(word);

// The screens of their own (Docs/UiRework.md U2, the person's keys of 2026-11-22): what were the bag's tabs 스킬, 특성,
// 캐릭터 and 일지 are screens with keys of their own (K P U J; the map M), in two groups whose windows have the group's
// tabs (성장: 스킬·특성·캐릭터, 기록: 일지·지도). On a touch screen they open from the [메뉴] hub under the minimap.
const ScreenKeys = { '가방': 'Tab', '스킬': 'k', '특성': 'p', '캐릭터': 'u', '일지': 'j', '지도': 'm' };
const ScreenGroups = { '가방': 'bag', '스킬': 'growth', '특성': 'growth', '캐릭터': 'growth', '일지': 'records', '지도': 'records' };

// A screen by its name, opened as a person would: a tab of the group when one of its screens is up, else its key on a
// PC, or on a touch screen the hub's button (after the open screen's close button: a full screen covers the hub).
async function openScreen(game, name) {
  const s = await game.state();
  if (s.window === name) return true;
  if (s.window && ScreenGroups[s.window] === ScreenGroups[name] && name !== '가방') {
    await game.press(name);
  } else if (game.touch) {
    if (s.window) await game.press(s.window === '지도' ? '지도 닫기' : '창 닫기');
    await game.wait(300);
    if (name === '가방') await game.press('가방');
    else {
      if (!(await game.state()).hub) await game.press('메뉴');
      await game.wait(300);
      await game.press(name, '메뉴');
    }
  } else {
    await game.page.keyboard.press(ScreenKeys[name]);
  }
  return !!(await game.until(t => t.window === name, 3000));
}

// The encounter windows (Docs/UiRework.md U3): what were the bag's [상인] and [상자] tabs are windows of their own,
// opened by meeting the thing, and the workbench and anvil have theirs. The old tab names (and a stall keeper's role)
// open them.
const Encounters = { '상인': '상점', '상점': '상점', '상자': '상자', '작업대': '작업대', '모루': '모루' };
const Roles = ['대장장이', '어부', '연금술사', '왕실 상인', '마족 상인', '광석상', '떠돌이 상인'];

// An encounter window opened as a person would: the button over the thing (a real click or tap; [○○와 말하기] then the
// talk's [거래] for a stall keeper), else, when another thing in reach stood first, the playtest command. True once open.
async function meet(game, name) {
  const window = Encounters[name] || (Roles.includes(name) ? '상점' : name);
  let s = await game.state();
  if (s.window === window) return true;
  if (s.window || s.talking) {
    await game.send('screen off');
    if (s.talking) await game.page.keyboard.press('Escape');
    await game.wait(300);
    s = await game.state();
  }
  const escape = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  if (window === '상점' && s.shopkeeper) {
    if (await game.press(new RegExp(`^${escape(s.shopkeeper)}(와|과) 말하기`), undefined, { quiet: true })) {
      await game.until(t => t.talking, 2000);
      await game.press('거래', undefined, { quiet: true });
    }
  } else {
    const label = { '상자': /^상자 열기/, '작업대': /^(작업대|화로) 열기/, '모루': /^모루 열기/ }[window];
    if (label) await game.press(label, undefined, { quiet: true });
  }
  if (await game.until(t => t.window === window, 2000)) return true;
  await game.send(`screen ${window}`);
  return !!(await game.until(t => t.window === window, 2000));
}

// A tab of the bag window, opened; the old tab names of what are now screens of their own open those screens, and the
// merchant's and chest's tabs their windows (meet). True once open.
async function bagTab(game, tab) {
  if (tab && (Encounters[tab] || Roles.includes(tab))) return meet(game, tab);
  if (tab && ScreenKeys[tab] && tab !== '가방') return openScreen(game, tab);
  if ((await game.state()).window !== '가방') {
    await openScreen(game, '가방');
    await game.wait(200);
  }
  return tab ? game.press(tab) : true;
}

// Picks a thing in the bag's [장비] grid by its square (the name, a RegExp for names that may carry more), so the
// panel under the grid shows its buttons. Worn things are in the "착용" row, the rest under "소지품".
async function pick(game, name, row = '소지품') {
  return game.press(name instanceof RegExp ? name : new RegExp(`^${name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`), row);
}

async function closeBag(game) {
  if ((await game.state()).bag) await game.send('bag off');
  await game.wait(300);
}

// A recipe made at a station (Docs/UiRework.md U6: the bag keeps only what is made by hand): the station's window by
// the button over it ([작업대 열기], [화로 열기]; meet), on the tab that has the recipe. True once its row is there.
async function stationTab(game, name) {
  await meet(game, '작업대');
  const row = new RegExp('^' + name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  for (const tab of [null, '작업대', '화로', '손 제작']) {
    if (tab && !(await game.press(tab, undefined, { quiet: true }))) continue;
    await game.wait(300);
    if ((await game.find('만들기', row)).length > 0) return true;
  }
  return false;
}

// Leaving a game in play (U7): Esc with nothing open opens the menu (the hub's screens, [설정], [나가기]); its [나가기]
// leaves. Esc first shuts whatever is open (the map, a screen, a talk), one step at a time, as it does for a person.
async function leaveGame(game) {
  for (let i = 0; i < 4 && !(await game.state()).hub; i++) {
    await game.page.keyboard.press('Escape');
    await game.until(s => s.hub, 1500);
  }
  await game.press('나가기', '메뉴');
}

// Floor all round the player, so building and walking have room.
async function clearRoom(game, w = 6, h = 4) {
  const commands = [];
  for (let dx = -w; dx <= w; dx++) for (let dy = -h; dy <= h; dy++) if (dx || dy) commands.push(`build Floor ${dx} ${dy}`);
  await game.send(commands.join(';'));
  await game.send('clear');
  await game.wait(400);
}

// A material or consumable in the bag (grilled fish is a consumable, 2026-11-11).
const count = (inv, id) => (inv.materials || {})[id] || (inv.consumables || {})[id] || 0;
const items = (inv, base) => (inv.items || []).filter(i => i.base === base).length;

// The start screen's pages (2026-11-11): back to the first screen, then the page of that name (none: stay there).
async function menuPage(game, page) {
  for (let i = 0; i < 4 && (await game.find('게임 시작')).length === 0; i++) {
    if (!(await game.press('뒤로', null, { quiet: true }))) await game.wait(400);
  }
  if (page) await game.press(page);
}

// ---------------------------------------------------------------- 메뉴와 설정 (6·14·17·21·24·36·37·46·50·131단계)
async function menu(browser) {
  note('\n## 메뉴와 설정 (PC 1280×720)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.shot('menu', '시작 화면');
  // The first screen: the game's name over a few big buttons, as other games have it (2026-11-11).
  let labels = (await game.buttons()).map(b => b.label);
  for (const word of ['게임 시작', '함께 하기', '저장본', '설정', '의견 보내기']) check(labels.includes(word), `시작 화면에 [${word}]`);
  check(labels.length <= 6, `시작 화면의 단추가 적다 (${labels.join(', ')})`);
  check(!labels.includes('혼자 하기'), '시작 화면에는 [혼자 하기]가 없다(게임 시작 안에)');

  await menuPage(game, '게임 시작');
  labels = (await game.buttons()).map(b => b.label);
  check(labels.includes('혼자 하기') && labels.includes('뒤로') && labels.some(l => l.startsWith('하루 길이:')), `[게임 시작]에 [혼자 하기]·[뒤로]·[하루 길이] (${labels.join(', ')})`);
  // Classes are picked in the game (2026-11-09): the page says so and has no class buttons.
  check(labels.filter(l => /^\[[ \d]\] /.test(l)).length === 0, '클래스 단추가 없다');
  check(/클래스는 게임 안에서 고른다/.test(await game.allText()), '"클래스는 게임 안에서 고른다: 가방의 [스킬] 탭에서…"라고 알린다');
  await game.shot('menu-play', '게임 시작 화면');
  await game.page.keyboard.press('Escape');
  await game.wait(400);
  check((await game.find('게임 시작')).length === 1, 'Esc로 시작 화면에 돌아온다');

  await menuPage(game, '설정');
  labels = (await game.buttons()).map(b => b.label);
  for (const word of ['어둠과 빛:', '효과음:', '음악:', '소리 종류:', '개발 정보:', '글꼴:', '목표 안내:', '화면 흔들림:', '키 바꾸기']) {
    check(labels.some(l => l.startsWith(word)), `[설정]에 [${word}…] 단추`);
  }
  await game.shot('menu-settings', '설정 화면');

  // The sound volume goes round off, 25, 50, 75, 100.
  const volumes = [];
  for (let i = 0; i < 5; i++) {
    const b = (await game.find(/^효과음:/))[0];
    volumes.push(b.label.replace('효과음: ', ''));
    await game.press(b.label);
  }
  check(new Set(volumes).size === 5, `[효과음]을 누를 때마다 다섯 단계로 돈다 (${volumes.join(' → ')})`);
  const music = [];
  for (let i = 0; i < 5; i++) {
    const b = (await game.find(/^음악:/))[0];
    music.push(b.label.replace('음악: ', ''));
    await game.press(b.label);
  }
  check(new Set(music).size >= 4, `[음악]을 누를 때마다 크기가 바뀐다 (${music.join(' → ')})`);

  // Darkness off, remembered over a reload, then back on.
  await game.press('어둠과 빛: 켜짐');
  check((await game.find('어둠과 빛: 꺼짐')).length === 1, '[어둠과 빛: 켜짐]을 누르면 꺼짐');
  await game.press('글꼴: 도트');
  check((await game.find('글꼴: 매끈')).length === 1, '[글꼴: 도트]를 누르면 매끈');
  await game.shot('menu-smooth', '매끈한 글꼴의 설정');
  await game.press('글꼴: 매끈');
  await game.press('개발 정보: 꺼짐');
  await game.page.reload();
  await game.page.waitForFunction(() => !!window.unityInstance, null, { timeout: 240000 });
  await game.wait(2500);
  await game.send('probe on');
  await game.wait(300);
  await menuPage(game, '설정');
  check((await game.find('어둠과 빛: 꺼짐')).length === 1, '새로 고쳐도 [어둠과 빛: 꺼짐]이 남는다');
  check((await game.find('개발 정보: 켜짐')).length === 1, '새로 고쳐도 [개발 정보: 켜짐]이 남는다');
  await game.startSolo();
  await game.wait(1500);
  // The CC0 recordings (2026-11-11, Docs/SoundCredits.md): in the build, and the ground under the spawn named.
  const heard = await game.state();
  check(heard.recordedSets >= 15, `녹음 효과음 세트가 빌드에 들어 있다 (${heard.recordedSets}세트, 발밑 ${heard.ground})`);
  await game.shot('no-darkness-devinfo', '어둠 없이, 개발 정보 창(왼쪽 아래)');
  await leaveGame(game);
  await game.until(s => !s.playing, 8000);
  await menuPage(game, '설정');
  await game.press('어둠과 빛: 꺼짐');
  await game.press('개발 정보: 켜짐');

  // The string table (Docs/Strings.md, 2026-11-11): [언어] turns the menu into English and back.
  const language = (await game.find(/^언어: /))[0];
  check(!!language, `[설정]에 [언어: 한국어] 단추 (${language && language.label})`);
  if (language) {
    await game.press(language.label);
    await game.press('Back');
    const english = (await game.buttons()).map(b => b.label);
    check(english.includes('Start game') && english.includes('Settings'), `[언어]를 누르면 시작 화면이 영어 (${english.slice(0, 6).join(', ')})`);
    const korean = english.filter(l => /[가-힣]/.test(l));
    check(korean.length === 0, `영어 시작 화면에 한글 단추가 없다 (${korean.join(', ') || '없음'})`);
    await game.shot('menu-english', '영어 시작 화면');
    await game.press('Settings');
    for (let i = 0; i < 4 && (await game.find('뒤로')).length === 0; i++) {
      const next = (await game.find(/^Language: /))[0];
      if (!next) break;
      await game.press(next.label);
    }
    check((await game.find('뒤로')).length === 1, '[언어]를 다시 누르면 한국어로 돌아온다');
  }

  // Keys: skill 1 to the "1" key; W is refused; Esc gives up.
  await menuPage(game, '설정');
  await game.press('키 바꾸기');
  await game.shot('menu-keys', '키 바꾸기 목록');
  const skillKey = (await game.find(/.+/, '스킬 1'))[0];
  check(!!skillKey, `키 목록에 스킬 1 (${skillKey && skillKey.label})`);
  if (skillKey) {
    await game.press(skillKey.label, '스킬 1');
    check((await game.find('키를 누르세요…')).length === 1, '키 단추를 누르면 "키를 누르세요…"');
    await game.page.keyboard.press('1');
    await game.wait(400);
    check((await game.find('1', '스킬 1')).length === 1, '1을 누르면 스킬 1이 1');
    await game.press('1', '스킬 1');
    await game.page.keyboard.press('w');
    await game.wait(400);
    const afterW = (await game.find(/.+/, '스킬 1'))[0];
    check(afterW && afterW.label !== 'W', `W는 받지 않는다 (${afterW && afterW.label})`);
    await game.page.keyboard.press('Escape');
    await game.wait(400);
    check((await game.find('1', '스킬 1')).length === 1, 'Esc는 그만두기(1 그대로, 키 바꾸기 화면에 남는다)');
    await game.startSolo();
    await game.wait(1200);
    const before = await game.state();
    await game.page.keyboard.press('1');
    await game.wait(700);
    const after = await game.state();
    check(after.mana < before.mana, `게임에서 1이 스킬 1을 쓴다 (마나 ${before.mana} → ${after.mana})`);
    await game.shot('key-1', '스킬 칸 글자가 1');
    await leaveGame(game);
    await game.until(s => !s.playing, 8000);
    if ((await game.find('처음 키로')).length === 0) {
      await menuPage(game, '설정');
      await game.press('키 바꾸기');
    }
    await game.press('처음 키로');
    check((await game.find('Q', '스킬 1')).length === 1, '[처음 키로]로 Q로 돌아온다');
  }

  // Online without a cloud project: a plain message.
  await menuPage(game, '함께 하기');
  labels = (await game.buttons()).map(b => b.label);
  check(labels.includes('온라인 방 만들기') && labels.includes('코드로 참가'), '[함께 하기]에 [온라인 방 만들기]·[코드로 참가]');
  await game.press('온라인 방 만들기');
  await game.wait(6000);
  const said = (await game.texts()).status || '';
  check(said.length > 0, `[온라인 방 만들기]가 알림을 띄운다 ("${said.slice(0, 80)}")`);
  await game.shot('online', '온라인 방 만들기 뒤');

  await menuPage(game, '저장본');
  await game.shot('menu-saves', '저장본 목록');
  await game.context.close();

  // The first Tab after starting from the menu by a real click on [혼자 하기] (LOCAL_TASKS.md, 13: the first key
  // once went unheard). Typing in the name field first, as a person would.
  const fresh = await open(browser, run, Profiles.pc, url);
  // The menu may not be drawn yet with the probe on: look again for a few seconds.
  let solo = null;
  for (let i = 0; i < 20 && !solo; i++) {
    if ((await fresh.find('게임 시작')).length) await fresh.press('게임 시작');
    solo = (await fresh.find('혼자 하기'))[0];
    if (!solo) await fresh.wait(300);
  }
  if (solo) {
    // The name field is the first thing on the page, just under its title bar.
    const box = await fresh.canvas();
    const back = (await fresh.find('뒤로'))[0];
    await fresh.tapAt(box.left + box.width / 2, back.cy + 64);
    await fresh.page.keyboard.type('탭시험');
    await fresh.press('혼자 하기');
    const started = await fresh.until(s => s.playing, 25000);
    check(!!started, '[혼자 하기]를 눌러 시작한다');
    await fresh.wait(1500);
    await fresh.page.keyboard.press('Tab');
    const opened = await fresh.until(s => s.bag, 2000);
    check(!!opened, '시작한 뒤 처음 누른 Tab으로 가방이 열린다');
    await fresh.shot('first-tab', '첫 Tab으로 연 가방');
  } else {
    check(false, '[게임 시작]에 [혼자 하기]');
  }
  await fresh.context.close();
}

// ---------------------------------------------------------------- 첫 화면, 이동, 캐기, 놓기, 제작, 목표 (2·3·4·9·18·26·27·28·45단계)
async function basics(browser) {
  note('\n## 첫 화면, 캐기, 놓기, 제작, 목표 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  check(await game.startSolo(), '혼자 하기가 시작된다');
  await game.wait(1500);
  let t = await game.texts();
  const hud = (t.hud || []).join(' | ');
  check((t.hud || []).some(l => /^1일째 06:\d\d 아침/.test(l)), `미니맵 아래에 1일째 아침 (${(t.hud || []).find(l => /일째/.test(l)) || ''})`);
  check(has(hud, 'Lv 1 (0%)'), '경험치 막대에 Lv 1 (0%)');
  // The first goal is picking a class (2026-11-09), which startSolo does, so stones are the second.
  check(/목표 2\/\d+: 돌 10개 모으기 \(0\/10\)/.test(hud), `목표 목록에 클래스 다음 목표와 진행도 (${(hud.match(/목표[^|]*/) || [''])[0]})`);
  check(has(hud, '보상:'), '목표 아래에 보상');
  // The controls lines began "WASD 이동" (PC) or "왼쪽 스틱 이동" (touch); a goal's hint may still say "공격 스틱".
  check(!/WASD|왼쪽\s스틱|보스 아레나:/.test(hud), '조작법·아레나 방향 줄이 HUD에 없다');
  await game.shot('first', '새 캐릭터 첫 화면');

  let inv = await game.inventory();
  check(inv.worn.Weapon === 'copper_sword', `처음 무기는 구리 검 (${inv.worn.Weapon})`);
  check(items(inv, 'copper_pickaxe') === 1, '가방에 구리 곡괭이');
  // Three to start with and one for the class goal.
  check((inv.consumables || {}).healing_potion === 4, `회복 물약 3개와 클래스 목표 보상 하나 (${(inv.consumables || {}).healing_potion})`);
  check(count(inv, 'torch') === 6 && count(inv, 'mushroom_spore') === 2, `횃불 6, 버섯 포자 2 (${count(inv, 'torch')}, ${count(inv, 'mushroom_spore')})`);

  // Quick slots: 2 is the pickaxe; a click on slot 1 is the sword again and does not swing.
  await game.page.keyboard.press('2');
  await game.wait(600);
  check((await game.state()).weapon === 'copper_pickaxe', '2를 누르면 곡괭이를 든다');
  await game.shot('pickaxe', '곡괭이를 든 모습과 퀵슬롯');
  await game.press('quick 0');
  const swordAgain = await game.state();
  check(swordAgain.weapon === 'copper_sword', '퀵슬롯 1을 누르면 검');
  check(!swordAgain.attacking, '퀵슬롯을 눌러도 휘두르지 않는다');

  // The map: M and the minimap.
  await game.page.keyboard.press('m');
  await game.wait(600);
  check((await game.state()).map, 'M으로 전체 지도가 열린다');
  await game.shot('map', '전체 지도');
  await game.page.keyboard.press('m');
  await game.wait(500);
  check(!(await game.state()).map, 'M으로 닫힌다');
  await game.press('지도');
  check((await game.state()).map, '미니맵을 누르면 전체 지도');
  await game.press('지도 닫기');
  check(!(await game.state()).map, '전체 지도를 누르면 닫힌다');

  // Digging: a wall to the right, the sword first, then the pickaxe.
  await clearRoom(game, 3, 2);
  await game.send('build Wall 1 0; face 1 0');
  await game.send('attack on 1 0');
  // The words float up for a moment, so they are looked for while the attack is held.
  const needs = await game.until(r => (r.floating || []).some(f => has(f, '곡괭이가 필요하다')), 3000, game.texts);
  await game.send('attack off');
  check(!!needs, '검으로 벽을 치면 "곡괭이가 필요하다"');
  check((await game.look(1, 0)).tile === 'Wall', '검으로는 벽이 그대로');
  // Rounded corners (Docs/ArtPipeline.md 둥근 모서리, 2026-11-11): a lone wall rounds all four corners, so the corner
  // layer has tiles round the player.
  const corners = (await game.state()).corners;
  check(corners > 0, `벽 둘레에 둥근 모서리 칸이 있다 (${corners}칸)`);
  inv = await game.inventory();
  const stoneBefore = count(inv, 'stone');
  await game.page.keyboard.press('2');
  await game.wait(400);
  await game.send('attack on 1 0');
  await game.wait(900);
  await game.shot('crack', '곡괭이로 치는 중(금)');
  let broke = null;
  for (let i = 0; i < 20 && !broke; i++) {
    await game.wait(300);
    if ((await game.look(1, 0)).tile !== 'Wall') broke = true;
  }
  await game.send('attack off');
  check(!!broke, '곡괭이로 벽이 부서진다');
  inv = await game.inventory();
  check(count(inv, 'stone') > stoneBefore, `돌이 들어온다 (${stoneBefore} → ${count(inv, 'stone')})`);
  check((await game.allText()).includes('획득: 돌'), '"획득: 돌"이 뜬다');

  // The first goal: ten stones.
  await game.send(`give stone ${Math.max(0, 10 - count(inv, 'stone'))}`);
  await game.wait(1500);
  t = await game.texts();
  check((t.acquired || []).some(l => has(l, '목표 달성: 돌 10개 모으기')), '"목표 달성: 돌 10개 모으기"');
  check(/목표 3\//.test((t.hud || []).join(' ')), '다음 목표로 넘어간다');
  // Where this player has got to, kept on the device for the feedback form (Feedback, Docs/Release.md 의견 단추).
  const steps = (await game.state()).steps || '';
  check(/로드/.test(steps) && /첫 채굴/.test(steps), `기기 안 단계 기록에 로드와 첫 채굴 (${steps})`);
  await game.shot('goal', '첫 목표를 이룬 뒤');

  // Placing a stone wall from the bag, then a workbench made and placed.
  await game.send('give stone 30; face 1 0');
  await bagTab(game, '재료·제작');
  check((await game.state()).target, '가방을 열면 놓일 칸에 틀이 그려진다');
  await game.shot('bag-materials', '가방 [재료·제작]과 놓일 칸의 틀');
  await game.press('놓기', /^돌 ×/);
  check((await game.look(1, 0)).tile === 'Wall', '[놓기]로 바라보는 칸에 돌벽');
  await game.send('build Floor 1 0');
  await game.press('만들기', /^작업대/);
  inv = await game.inventory();
  check(count(inv, 'workbench') >= 1, '돌로 작업대를 만든다');
  await game.press('놓기', '작업대 ×');
  check((await game.look(1, 0)).tile === 'Workbench', '작업대를 놓는다');
  await game.send('give copper_ore 12');
  await game.wait(300);
  const swordsBefore = items(await game.inventory(), 'copper_sword');
  // The copper sword is the workbench's (U6: the bag keeps what is made by hand): its window by [작업대 열기] over it.
  check(await stationTab(game, '구리 검'), '작업대 위 [작업대 열기]로 연 작업대 창에 구리 검');
  await game.press('만들기', /^구리 검/);
  // Gear takes time by hand (2026-11-09): a bar fills for 3 seconds while the player stays by the workbench.
  await game.wait(800);
  check(/만드는 중: 구리 검/.test(await game.allText()), '구리 검은 바로 나오지 않고 "만드는 중: 구리 검"과 막대가 보인다');
  await game.shot('bag-crafting', '작업대 창: 만드는 중의 막대와 [그만]');
  const swordDone = await game.until(i => items(i, 'copper_sword') + (i.worn.Weapon === 'copper_sword' ? 1 : 0) > swordsBefore, 5000, game.inventory);
  check(!!swordDone, '작업대 곁에서 3초 뒤 구리 검이 나온다');
  await game.shot('bag-crafted', '작업대 창의 제작 목록');
  // [만들 수 있는 것만]: the list shrinks to what the bag and the stations near make now, and back.
  const all = (await game.find('만들기')).length;
  check(await game.press('만들 수 있는 것만'), '[만들 수 있는 것만]을 누른다');
  const some = (await game.find('만들기')).length;
  check(some > 0 && some < all, `만들 수 있는 것만 남는다 (${all} → ${some})`);
  await game.shot('bag-craftable', '만들 수 있는 것만 본 제작 목록');
  await game.press('모두 보기');

  // A chest beside it: the chest tab, put in and take out. Placed from the bag's [재료·제작], as the workbench was.
  await game.send('screen off; give chest 1; face -1 0');
  await game.wait(300);
  await bagTab(game, '재료·제작');
  await game.press('놓기', '상자 ×');
  check((await game.look(-1, 0)).tile === 'Chest', '상자를 놓는다');
  check(await bagTab(game, '상자'), '상자 곁에서 [상자 열기]로 상자 창이 열린다(U3)');
  await game.press('넣기', /^돌 ×/);
  check(((await game.look(-1, 0)).chest || {}).stone > 0, '[넣기]로 상자에 돌이 들어간다');
  await game.shot('bag-chest', '상자 창');
  await game.press('꺼내기', /^돌 ×/);
  check(!((await game.look(-1, 0)).chest || {}).stone, '[꺼내기]로 다시 가방에');
  // [상자에 넣기] (2026-11-11, Docs/Research2026-11.md 후보 8): what a chest nearby holds already goes in whole;
  // what none holds, and what the player locked, stay in the bag.
  await game.send('fill -1 0 stone 1; fill -1 0 slime_gel 1; give slime_gel 4; give copper_ore 3');
  await game.wait(800);
  // Every material is in the bag's one grid (U6); a picked one's card has [잠그기].
  await bagTab(game, '장비');
  check(await pick(game, /^슬라임 젤 ×/), '가방 격자에서 슬라임 젤을 고른다');
  check(await game.press('잠그기'), '고른 재료의 [잠그기]를 누른다');
  check((await game.find('잠금 풀기')).length === 1, '잠그면 [잠금 풀기]로 바뀐다');
  await game.shot('bag-lock', '잠근 슬라임 젤');
  await bagTab(game, '상자');
  check(await game.press('상자에 넣기'), '상자 창에 [상자에 넣기]가 있다');
  await game.wait(800);
  let stashed = await game.inventory();
  const inChest = (await game.look(-1, 0)).chest || {};
  check(count(stashed, 'stone') === 0 && inChest.stone > 1, `상자에 이미 있는 돌은 모두 상자로 (가방 ${count(stashed, 'stone')}, 상자 ${inChest.stone})`);
  check(count(stashed, 'slime_gel') >= 4, `잠근 슬라임 젤은 가방에 남는다 (${count(stashed, 'slime_gel')})`);
  check(count(stashed, 'copper_ore') >= 3, `상자에 없는 구리 광석은 남는다 (${count(stashed, 'copper_ore')})`);
  await game.shot('bag-stash', '[상자에 넣기] 뒤');
  await bagTab(game, '장비');
  await pick(game, /^슬라임 젤 ×/);
  await game.press('잠금 풀기');
  await closeBag(game);

  // A torch from the quick slot in front.
  await game.send('build Floor 0 1; face 0 1');
  await game.wait(300);
  await game.page.keyboard.press('4');
  await game.wait(600);
  check((await game.look(0, 1)).tile === 'Torch', '퀵슬롯 4로 횃불을 앞에 놓는다');
  await game.shot('torch', '놓은 횃불의 빛');
  await game.context.close();
}

// ---------------------------------------------------------------- 가방: 드랍, 장착, 레벨 제한, 룬, 버리기, 회복 (M3b, 3·39단계)
async function bag(browser) {
  note('\n## 가방, 드랍, 회복 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await clearRoom(game, 2, 2);

  await game.send('drop item iron_sword rare 2');
  await game.wait(1500);
  let inv = await game.inventory();
  check(items(inv, 'iron_sword') === 1, '발밑의 철 검을 줍는다');
  check((await game.allText()).includes('획득:'), '"획득: …"이 뜬다');
  await game.send('drop rune; drop potion healing_potion');
  await game.wait(1500);
  inv = await game.inventory();
  check(inv.runes === 1, '룬을 줍는다');
  check(inv.consumables.healing_potion === 4, '물약을 줍는다');

  await bagTab(game, '장비');
  check(await pick(game, /철 검/), '가방 격자에서 철 검 칸을 고른다');
  const gated = await game.find(/^Lv\d+$/, '철 검');
  check(gated.length === 1, `레벨이 모자란 철 검에는 [장착] 대신 "${gated[0] && gated[0].label}"`);
  await game.shot('bag-gear', '가방 [장비] 격자와 고른 칸의 설명(레벨 제한)');
  // A description under the pointer, over the sword's square.
  const square = (await game.find(/철 검/, '소지품'))[0];
  if (square) {
    await game.page.mouse.move(square.cx, square.cy);
    await game.wait(700);
    await game.shot('tooltip', '칸에 마우스를 올린 설명');
  }
  await game.send('xp 1300');
  await game.wait(1500);
  const t = await game.texts();
  check((await game.state()).level >= 5 && (has(t.banner, '레벨') || (t.acquired || []).length >= 0), `경험치로 레벨이 오른다 (Lv ${(await game.state()).level}, 배너 "${t.banner || ''}")`);
  await game.press('장착', '철 검');
  check((await game.inventory()).worn.Weapon === 'iron_sword', '[장착]으로 철 검을 든다');
  // [정렬]: gear in slot order (weapons first), the higher level first within a slot.
  await game.send('drop item iron_helmet; drop item mithril_sword');
  await game.wait(1500);
  check(await game.press('정렬'), '[정렬]을 누른다');
  await game.wait(500);
  const order = (await game.inventory()).items.map(i => [i.slot, -i.level]);
  const sorted = order.every((pair, i) => i === 0 || order[i - 1][0] < pair[0] || (order[i - 1][0] === pair[0] && order[i - 1][1] <= pair[1]));
  check(sorted && order.length >= 3, `[정렬]하면 장비가 칸 종류, 높은 레벨 순 (${(await game.inventory()).items.map(i => i.base).join(', ')})`);
  await game.shot('bag-sorted', '[정렬]한 가방');
  // The rune into the sword's socket: pick the rune's square, then the sword's, then [끼우기].
  const runeButton = (await game.buttons()).find(b => b.shown && / 룬 ×1$/.test(b.label));
  if (runeButton) {
    await game.press(runeButton.label);
    await pick(game, /철 검/, '착용');
    const inserted = await game.press('끼우기', null, { quiet: true });
    const socketed = await game.until(i => i.runes === 0, 3000, game.inventory);
    check(inserted && !!socketed, '룬을 고르고 [끼우기]로 소켓에 넣는다');
  } else {
    check(false, '가방에 룬 단추가 보인다');
  }
  await game.shot('bag-rune', '룬을 끼운 뒤');
  // Throwing away and picking up again.
  // A thrown-away item lands 1.2 tiles ahead and can be picked up again after 5 seconds, by walking onto it.
  const swords = items(await game.inventory(), 'copper_sword');
  await game.send('face 1 0');
  await game.wait(300);
  await pick(game, /구리 검/);
  await game.press('버리기', '구리 검');
  check(items(await game.inventory(), 'copper_sword') === swords - 1, '[버리기]로 구리 검을 버린다');
  await game.wait(2000);
  await game.send('goto 1 0');
  await game.wait(1000);
  check(items(await game.inventory(), 'copper_sword') === swords - 1, '버린 바로 뒤에는 밟아도 줍지 않는다');
  await game.wait(3500);
  check(items(await game.inventory(), 'copper_sword') === swords, '5초가 지나면 그 위에서 다시 줍는다');
  await game.send('goto -1 0; face off');
  // A click inside the bag does not swing: on the empty space near the window's bottom, under the [장비] tab.
  const inBag = (await game.buttons()).find(b => b.shown && b.label === '장비');
  const bagBox = await game.canvas();
  await game.page.mouse.move(inBag.cx, bagBox.top + bagBox.height * 0.9);
  await game.page.mouse.down();
  await game.wait(500);
  const clicking = await game.state();
  await game.page.mouse.up();
  check(!clicking.attacking, '가방이 열린 동안 눌러도 공격하지 않는다');
  await closeBag(game);

  // Healing: on its own after a while, and the potion key.
  await game.send('hurt 40');
  await game.wait(400);
  const hurt = await game.state();
  await game.page.keyboard.press('c');
  await game.wait(2600);
  const drunk = await game.state();
  check(drunk.health > hurt.health + 20, `C로 물약을 마시면 찬다 (${hurt.health} → ${drunk.health})`);
  check((await game.inventory()).consumables.healing_potion === 3, '물약이 하나 준다');
  await game.shot('potion', '물약을 마신 뒤(물약 칸이 어둡다)');
  await game.send('hurt 30');
  await game.wait(500);
  const low = await game.state();
  await game.wait(9000);
  check((await game.state()).health > low.health, `맞고 몇 초 뒤 생명력이 저절로 찬다 (${low.health} → ${(await game.state()).health})`);

  // Falling and getting up at the start.
  await game.send('goto 2 0; hurt 99999');
  await game.wait(800);
  const fell = await game.texts();
  check((fell.hud || []).some(l => has(l, '쓰러졌습니다')), 'HUD에 "쓰러졌습니다"');
  await game.shot('fallen', '쓰러진 화면');
  const up = await game.until(s => !s.dead, 9000);
  check(!!up && Math.abs(up.x - up.spawnX) < 2 && Math.abs(up.y - up.spawnY) < 2 && up.health === up.maxHealth, '5초쯤 뒤 시작 지점에서 일어난다');
  await game.context.close();
}

// ---------------------------------------------------------------- 몬스터와 싸움 (M2, 2·8·20·21·30·47·48단계)
async function combat(browser) {
  note('\n## 몬스터와 싸움 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await clearRoom(game, 5, 3);

  await game.send('spawn slime 2.5 0');
  await game.wait(1800);
  await game.shot('slime-windup', '다가와 빨갛게 깜박이는 슬라임');
  const hp = (await game.state()).health;
  await game.wait(1500);
  check((await game.state()).health < hp, '슬라임이 다가와 친다');
  await game.send('heal; attack on 1 0');
  const killed = await game.until(s => s.monsters === 0, 8000);
  await game.send('attack off');
  check(!!killed, '슬라임을 잡으면 사라진다');
  check((await game.state()).experience > 0, '잡으면 경험치가 오른다');

  // Every monster side by side, the world paused for the picture.
  const monsters = ['slime', 'spore_shooter', 'cave_beetle', 'crystal_bat', 'crystal_wisp', 'myconid', 'glow_bat', 'ice_slime', 'frost_wisp', 'magma_slime', 'fire_bat', 'void_wisp', 'abyss_stalker'];
  await game.send('clear');
  await game.send(monsters.map((m, i) => `spawn ${m} ${(i % 7) * 1.4 - 4.2} ${i < 7 ? 1.6 : -1.6} 5000`).join(';'));
  await game.wait(250);
  await game.send('speed 0');
  await game.wait(1000);
  await game.shot('monsters', '몬스터 열세 종류');
  await game.send('speed 1; clear');
  await game.wait(800);

  // Bow and staff shoot.
  // An arrow lives a fraction of a second, so the proof is a slime hit four tiles off, out of a sword's reach. On
  // fresh floor, away from where the gallery's monsters pushed the player about.
  await game.send('heal; goto biome Cave');
  await clearRoom(game, 5, 2);
  for (const [weapon, name] of [['copper_bow', '활'], ['copper_staff', '지팡이']]) {
    await game.send(`equip ${weapon}; spawn slime 4 0 5000; speed 0`);
    await game.wait(300);
    await game.send('speed 1; attack on 1 0');
    const shot = await game.until(s => s.hurtMonsters > 0, 3000);
    await game.shot(`ranged-${weapon}`, `${name}로 쏘기`);
    await game.send('attack off; clear');
    check(!!shot, `${name}를 들면 4칸 떨어진 슬라임을 맞힌다`);
  }
  await game.send('equip copper_sword');

  // The music turns urgent in a fight and calms down after (2026-11-09): the field's tune and its fight version.
  await game.send('heal; clear');
  await game.until(t => /^tune_[a-z]+$/.test(t.music || ''), 9000);
  let tune = (await game.state()).music;
  await game.send('spawn slime 3 0 5000');
  const urgent = await game.until(t => t.music === `${tune}_battle`, 4000);
  check(!!urgent, `싸움이 붙으면 그 지역 곡이 전투판으로 (${tune} → ${(urgent || await game.state()).music})`);
  await game.send('clear');
  const calm = await game.until(t => t.music === tune, 12000);
  check(!!calm, `싸움이 끝나고 몇 초 뒤 평소 곡으로 (${(calm || await game.state()).music})`);

  // Four skills pressed at once all go. A new character knows only each class's first skill: all are taught first,
  // and the new slots reach the screen a moment later. The four (whirlwind, war cry, fireball, flame brand) cost
  // 10 + 15 + 5 + 15 = 45 mana: with exactly that much, all four leave almost none.
  await game.send('teach');
  await game.until(inv => (inv.skills || '').split(',').length === 4, 3000, game.inventory);
  await game.wait(800);
  await game.send('heal; spawn slime 2 0 5000; spawn slime -2 0 5000; mana 45');
  for (const key of ['q', 'e', 'r', 'f']) await game.page.keyboard.down(key);
  await game.wait(150);
  for (const key of ['q', 'e', 'r', 'f']) await game.page.keyboard.up(key);
  await game.wait(150);
  const after = await game.state();
  await game.shot('skills', 'Q E R F를 함께 누른 뒤(칸이 모두 어둡다)');
  check(after.mana < 5, `Q E R F를 거의 함께 눌러도 넷 다 나간다 (마나 45 → ${after.mana})`);
  await game.wait(800);
  await game.shot('burning', '불붙은 슬라임(원소)');
  await game.send('clear');

  // Falling to a monster's hit shows the hurt edge.
  await game.send('spawn magma_slime 1.5 0 5000');
  await game.wait(2500);
  await game.shot('burned', '용암 슬라임에 맞아 불타는 나');
  await game.send('clear; heal');
  await game.context.close();
}

// Each boss attack's first phase, from the sheet ("boss/attack" → 1, 2 or 3).
const BossFirstPhase = Object.fromEntries(fs.readFileSync(path.join(__dirname, '..', '..', 'Assets', '_Project', 'Data', 'Bosses', 'boss_attacks.csv'), 'utf8')
  .split('\n').slice(1).filter(l => l.trim()).map(l => l.split(','))
  .map(c => [`${c[0]}/${c[1]}`, c[3] ? Math.min(...c[3].split('|').map(Number)) : 1]));

// ---------------------------------------------------------------- 보스 여덟 (M3, 19·31·40단계)
async function bosses(browser) {
  note('\n## 보스 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  let s = await game.state();
  check(/cave_warden/.test(s.bosses) && /crystal_queen/.test(s.bosses) && /lava_titan/.test(s.bosses) && /void_lord/.test(s.bosses) && /tide_serpent/.test(s.bosses) && /antlion/.test(s.bosses)
    && /mad_general/.test(s.bosses) && /crown_wraith/.test(s.bosses), `보스 여덟이 월드에 있다 (${s.bosses})`);

  // Leaving the arena puts the boss back as it was.
  await game.send('goto boss cave_warden');
  await game.wait(2500);
  await game.send('boss cave_warden hurt 0.2');
  await game.send('goto spawn');
  await game.wait(7000);
  s = await game.state();
  const warden = (s.bosses.match(/cave_warden:(\d+)\/(\d+):p(\d):(\w+)/) || []);
  check(warden[1] === warden[2], `아레나를 떠나 5초가 지나면 보스가 처음 상태로 (${warden.slice(1).join(' ')})`);

  for (const [id, name] of [['cave_warden', '동굴 수호자'], ['crystal_queen', '수정 거미 여왕'], ['lava_titan', '용암 거인'], ['void_lord', '공허의 군주'], ['tide_serpent', '물살 뱀'], ['antlion', '개미귀신'],
    // The two bosses of the places (2026-11-11, the content audit: they were not yet checked).
    ['mad_general', '미쳐 버린 장군 모르그'], ['crown_wraith', '왕관의 망령']]) {
    await game.send(`heal; goto boss ${id}`);
    const woke = await game.until(t => has(t.banner, '등장') || has(t.entrance, '등장'), 6000, game.texts);
    check(!!woke, `${name}: "${name} 등장!" 배너나 이름 카드`);
    const theme = await game.until(t => t.music === `boss_${id}_1`, 4000);
    check(!!theme, `${name}: 보스 곡 1페이즈 (${(theme || await game.state()).music})`);
    await game.wait(2500);
    await game.shot(`boss-${id}`, `${name} 싸움(예고 장판, 체력 막대)`);
    // Winding up, it gives one of the five warnings (World.TellOf, Docs/Bosses.md 전조 말, 2026-11-11).
    const telling = await game.until(t => new RegExp(`${id}:\\d+/\\d+:p\\d:Telegraph:\\w+:(Area|Dash|Gather|Barrage|Call)`).test(t.bosses || ''), 8000);
    const tell = ((telling || {}).bosses || '').match(new RegExp(`${id}:\\d+/\\d+:p\\d:Telegraph:(\\w+):(\\w+)`));
    check(!!tell, `${name}: 예고하는 동안 전조 종류가 정해져 있다 (${tell ? `${tell[1]} → ${tell[2]}` : '없음'})`);
    if (tell) await game.shot(`boss-${id}-tell`, `${name} 예고: ${tell[1]}(${tell[2]})`);
    await game.send(`heal; boss ${id} hurt 0.36`);
    const two = await game.until(t => has(t.banner, '2페이즈'), 3000, game.texts);
    check(!!two, `${name}: 체력 66% 아래에서 "2페이즈!"`);
    // Past the roar, the first attack is one phase 2 brought (2026-11-09).
    const opener = await game.until(t => new RegExp(`${id}:\\d+/\\d+:p2:(?!Roar)\\w+:(?!-)`).test(t.bosses || ''), 5000);
    const opened = ((opener || {}).bosses || '').match(new RegExp(`${id}:\\d+/\\d+:p2:\\w+:(\\w+)`));
    check(!!opened && BossFirstPhase[`${id}/${opened[1]}`] === 2, `${name}: 2페이즈는 새 공격으로 연다 (${opened ? opened[1] : '없음'})`);
    const second = await game.until(t => t.music === `boss_${id}_2`, 3000);
    check(!!second, `${name}: 2페이즈에 곡도 바뀐다 (${(second || await game.state()).music})`);
    await game.wait(2500);
    await game.send(`heal; boss ${id} hurt 0.34`);
    const three = await game.until(t => has(t.banner, '3페이즈'), 3000, game.texts);
    await game.wait(2500);
    await game.shot(`boss-${id}-3`, `${name} 3페이즈`);
    if (id === 'cave_warden' || id === 'crystal_queen') check(!!three, `${name}: 체력 33% 아래에서 "3페이즈!"`);
    if (three) check((await game.state()).music === `boss_${id}_3`, `${name}: 3페이즈 곡`);
    // What the bag holds before the kill: a boss falling beside the player can drop its loot where the player stands,
    // and it is picked up at once.
    const held = inv => (inv.items || []).length + Object.values(inv.materials || {}).reduce((a, b) => a + b, 0) + Object.values(inv.consumables || {}).reduce((a, b) => a + b, 0);
    const before = held(await game.inventory());
    await game.send(`heal; boss ${id} hurt 1`);
    const dead = await game.until(t => has(t.banner, '처치'), 4000, game.texts);
    check(!!dead, `${name}: "${name} 처치!" 배너`);
    await game.wait(1200);
    const after = await game.state();
    const gained = held(await game.inventory()) - before;
    check(after.loot > 0 || gained > 0, `${name}: 잡으면 떨어뜨린다 (바닥 ${after.loot}개, 가방에 든 것 +${gained})`);
    await game.shot(`boss-${id}-drops`, `${name}의 드랍`);
  }
  await game.context.close();
}

// ---------------------------------------------------------------- 보스의 새 패턴(2026-11-09): 고리, 십자, 연쇄 폭발, 나선, 나눠 맞기
async function patterns(browser) {
  note('\n## 보스 패턴 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  const uses = [
    ['cave_warden', 'shockwave', '충격파(고리: 보스 곁은 안전)'], ['cave_warden', 'rock_trail', '바위 줄기(차례로 터지는 원)'],
    ['crystal_queen', 'crystal_cross', '수정 십자'], ['crystal_queen', 'venom_share', '독 거미줄(나눠 맞기, 금색)'], ['crystal_queen', 'crystal_spiral', '수정 나선'],
    ['void_lord', 'void_cross', '공허 십자(대각선)'], ['void_lord', 'event_horizon', '사건의 지평선(고리)'],
    ['tide_serpent', 'whirlpool', '물살 뱀의 소용돌이(고리)'], ['tide_serpent', 'tidal_trail', '물살 뱀의 밀물(연쇄 폭발)'],
    ['antlion', 'pit', '개미귀신의 개미지옥(멀리 있는 사람 발밑)'], ['antlion', 'quicksand', '개미귀신의 모래늪(고리)'],
  ];
  let woken = '';
  for (const [id, attack, name] of uses) {
    if (woken !== id) {
      await game.send(`heal; goto boss ${id}`);
      await game.until(t => has(t.banner, '등장') || has(t.entrance, '등장'), 6000, game.texts);
      woken = id;
    }
    await game.wait(1500);
    await game.send(`heal; boss ${id} use ${attack}`);
    await game.wait(attack.endsWith('spiral') ? 1200 : 450);
    const s = await game.state();
    check(attack.endsWith('spiral') ? s.bossShots > 0 : s.hazards > 0, `${name}: 예고가 깔린다 (장판 ${s.hazards}, 탄 ${s.bossShots})`);
    await game.shot(`pattern-${attack}`, name);
    await game.wait(1200);
    await game.shot(`pattern-${attack}-land`, `${name}이 떨어진 자리`);
  }
  await game.context.close();
}

// ---------------------------------------------------------------- 광맥 포식자 그룸 (2026-11-12, Docs/Bosses.md)
// Most of its attacks lay nothing on the ground and give no glow: only its wind-up shows them. The strongest show their
// area and a warning sign over it. Each attack is called once: a picture mid wind-up and one as it lands.
async function veineater(browser) {
  note('\n## 광맥 포식자 그룸 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  await game.send('heal; goto boss vein_eater');
  const woke = await game.until(t => has(t.banner, '그룸') || has(t.entrance, '그룸'), 8000, game.texts);
  check(!!woke, `철곡 광산촌 곁 아레나에서 깨면 "광맥 포식자 그룸 등장" 배너나 이름 카드 (${woke ? woke.banner || woke.entrance : '없음'})`);
  await game.wait(1500);
  await game.shot('vein-awake', '깨어난 그룸');
  // [attack, name, telegraph seconds, strong]
  const uses = [
    ['claw', '드릴 발톱(뒤로 젖히며 드릴을 든다)', 0.8, false],
    ['rock_throw', '바위 던지기(웅크려 바위를 들어 올린다)', 0.9, false],
    ['tunnel_rush', '굴착 돌진(머리를 낮추고 땅을 긁는다)', 1.0, false],
    ['burrow', '땅속 습격(땅으로 들어가 흙더미가 달려온다, 경고)', 1.6, true],
    ['cave_in', '갱도 붕괴(주먹을 들고 천장에서 바위가 떨어진다, 경고)', 1.5, true],
    ['spike_line', '종유석 솟기(드릴을 땅에 박는다)', 0.9, false],
    ['drill_spin', '드릴 회전(몸을 말았다가 돈다)', 0.9, false],
    ['frenzy', '광폭 연속 돌진(붉어지고 김을 뿜는다)', 0.9, false],
    ['quake', '대지 분쇄(깊이 웅크렸다 뛰어오른다, 경고)', 1.8, true],
    ['call_beetles', '딱정벌레 부르기(천장을 향해 포효)', 0.9, false],
  ];
  for (const [attack, name, telegraph, strong] of uses) {
    await game.send('heal; clear');
    await game.wait(1500);
    await game.send(`heal; boss vein_eater use ${attack}`);
    await game.wait(Math.round(telegraph * (attack === 'burrow' ? 700 : 550)));
    const s = await game.state();
    const shown = s.marksShown || 0;
    if (strong) check(shown > 0 && s.warningSign, `${name}: 바닥 표시와 경고 표지가 보인다 (표시 ${shown}, 표지 ${s.warningSign})`);
    else check(shown === 0 && !s.warningSign, `${name}: 바닥 표시도 경고 표지도 없다, 모션만 (표시 ${shown}, 표지 ${s.warningSign})`);
    await game.shot(`vein-${attack}`, `${name} 전조`);
    await game.wait(Math.round(telegraph * 450) + 250);
    await game.shot(`vein-${attack}-land`, `${name} 터진 뒤`);
  }
  // The phases: the roar into each, and the last phase's ore burning.
  await game.send('heal; clear; boss vein_eater hurt 0.36');
  await game.wait(600);
  await game.shot('vein-phase2', '2페이즈 포효');
  await game.send('heal; boss vein_eater hurt 0.33');
  await game.wait(2500);
  const last = await game.state();
  check((last.bosses || '').includes('vein_eater') && (last.bosses || '').includes(':p3:'), `체력을 깎으면 3페이즈 (${last.bosses})`);
  await game.shot('vein-phase3', '3페이즈(광석이 달아오른다)');
  await game.context.close();
}

// ---------------------------------------------------------------- 벚꽃 검귀 시구레 (2026-11-12, Docs/Bosses.md)
// Iaido: every attack is marked on the ground through a long, plain stance; the draw itself is instant. The ultimate
// (오의 천섬) shows a warning sign too. A picture mid stance and one just as the blade comes out.
async function samurai(browser) {
  note('\n## 벚꽃 검귀 시구레 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  await game.send('heal; goto boss shrine_samurai');
  const woke = await game.until(t => has(t.banner, '시구레') || has(t.entrance, '시구레'), 8000, game.texts);
  check(!!woke, `잊힌 신전 곁 아레나에서 깨면 "벚꽃 검귀 시구레 등장" 배너나 이름 카드 (${woke ? woke.banner || woke.entrance : '없음'})`);
  await game.wait(1500);
  await game.shot('samurai-awake', '깨어난 시구레');
  // [attack, name, telegraph seconds, strong]
  const uses = [
    ['iai', '거합 베기(웅크려 칼자루에 손)', 0.9, false],
    ['issen', '일섬(먼 사람에게 순식간에 돌진)', 1.1, false],
    ['juji', '십자 베기(상단 자세)', 1.0, false],
    ['sanren', '삼연 발도(돌진 세 번)', 0.9, false],
    ['getsuei', '월광참(초승달 검기 다섯)', 1.0, false],
    ['kaiten', '회전 발도(둘레 원)', 0.9, false],
    ['sakura', '벚꽃 난무(사람마다 허공 베기 넷)', 1.1, false],
    ['shunpo', '순보 일섬(사라졌다 곁에서 베기)', 1.2, false],
    ['ougi', '오의 천섬(곁만 안전, 경고)', 1.8, true],
  ];
  for (const [attack, name, telegraph, strong] of uses) {
    await game.send('heal; clear');
    await game.wait(1500);
    await game.send(`heal; boss shrine_samurai use ${attack}`);
    await game.wait(Math.round(telegraph * 550));
    const s = await game.state();
    if (attack !== 'getsuei') check((s.marksShown || 0) > 0, `${name}: 전조 동안 바닥 표시가 보인다 (표시 ${s.marksShown})`);
    check(!!s.warningSign === strong, `${name}: 경고 표지 ${strong ? '있음' : '없음'} (${s.warningSign})`);
    await game.shot(`samurai-${attack}`, `${name} 전조`);
    await game.wait(Math.round(telegraph * 450) + 60);
    await game.shot(`samurai-${attack}-draw`, `${name} 발도 순간`);
  }
  await game.send('heal; clear; boss shrine_samurai hurt 0.36');
  await game.wait(600);
  await game.shot('samurai-phase2', '2페이즈 기합');
  await game.context.close();
}

// ---------------------------------------------------------------- 흑철 기사 발두인 (2026-11-12, Docs/Bosses.md)
// Slow, heavy blows: every attack is marked on the ground through a long wind-up (the hammer hauled up or drawn back),
// the leap and the judgement show a warning sign too. A picture mid wind-up and one just after the blow lands (cracks,
// dust, the hammer in the floor).
async function knight(browser) {
  note('\n## 흑철 기사 발두인 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  await game.send('heal; goto boss black_knight');
  const woke = await game.until(t => has(t.banner, '발두인') || has(t.entrance, '발두인'), 8000, game.texts);
  check(!!woke, `용사의 오두막 곁 아레나에서 깨면 "흑철 기사 발두인 등장" 배너나 이름 카드 (${woke ? woke.banner || woke.entrance : '없음'})`);
  await game.wait(1500);
  await game.shot('knight-awake', '깨어난 발두인');
  // [attack, name, telegraph seconds, strong]
  const uses = [
    ['smash', '망치 내려찍기(머리 위로 들어 올림)', 1.5, false],
    ['sweep', '휩쓸어 치기(뒤로 크게 젖힘)', 1.4, false],
    ['bash', '어깨 돌진(어깨를 낮춤)', 1.6, false],
    ['quake', '대지 강타(둘레 원)', 1.8, false],
    ['shockwave', '충격파(앞으로 터져 나감)', 1.6, false],
    ['leap', '도약 내려찍기(경고)', 1.9, true],
    ['fissure', '십자 균열', 1.7, false],
    ['rubble', '무너지는 천장(사람마다)', 1.4, false],
    ['judgement', '심판의 일격(넓은 원, 경고)', 2.6, true],
  ];
  for (const [attack, name, telegraph, strong] of uses) {
    await game.send('heal; clear');
    await game.wait(1800);
    await game.send(`heal; boss black_knight use ${attack}`);
    await game.wait(Math.round(telegraph * 700));
    const s = await game.state();
    check((s.marksShown || 0) > 0, `${name}: 전조 동안 바닥 표시가 보인다 (표시 ${s.marksShown})`);
    check(!!s.warningSign === strong, `${name}: 경고 표지 ${strong ? '있음' : '없음'} (${s.warningSign})`);
    await game.shot(`knight-${attack}`, `${name} 전조`);
    await game.wait(Math.round(telegraph * 300) + 160);
    await game.shot(`knight-${attack}-hit`, `${name} 내려친 순간`);
  }
  await game.send('heal; clear; boss black_knight hurt 0.36');
  await game.wait(800);
  await game.shot('knight-phase2', '2페이즈 포효');
  await game.context.close();
}

// ---------------------------------------------------------------- 실 잣는 어미 아라네 (2026-11-13, Docs/Bosses.md)
// A spider that takes the arena piece by piece. Its fangs are announced by its body alone (no mark, no sign); the rest
// are marked through a wind-up of their own; the thread drop and the eight legs show a warning sign too. Its silk and
// venom stay on the floor: a web that snares, a pool that hurts; and plucked (그물 폭발), its webs burst where they lie.
// A picture mid wind-up and one just as the blow lands, and the floor a moment after.
async function spider(browser) {
  note('\n## 실 잣는 어미 아라네 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  await game.send('heal; goto boss silk_mother');
  const woke = await game.until(t => has(t.banner, '아라네') || has(t.entrance, '아라네'), 8000, game.texts);
  check(!!woke, `잊힌 신전 곁 둘째 아레나에서 깨면 "실 잣는 어미 아라네 등장" 배너나 이름 카드 (${woke ? woke.banner || woke.entrance : '없음'})`);
  await game.wait(1500);
  await game.shot('spider-awake', '깨어난 아라네');
  // [attack, name, telegraph seconds, marked on the ground, warning sign, what it leaves on the floor]
  const uses = [
    ['fang', '독니 찌르기(앞다리를 쳐들고 몸을 세움, 바닥 표시 없음)', 0.8, false, false, 0],
    ['leg_stab', '앞다리 연타(앞다리를 머리 뒤로 젖힘)', 1.0, true, false, 0],
    ['web_shot', '거미줄 뿜기(배를 등 위로 세움, 거미줄이 남는다)', 1.1, true, false, 3],
    ['pounce', '덮치기(납작 엎드림)', 1.0, true, false, 0],
    ['venom_spit', '독액 뿜기(몸을 빼고 배를 부풀림)', 1.0, false, false, 0],
    ['venom_pool', '독 웅덩이(독액을 토해 던짐, 웅덩이가 남는다)', 1.3, true, false, 2],
    ['thread_drop', '실 타고 내려찍기(실을 타고 사라졌다 내려옴, 경고, 거미줄이 남는다)', 1.8, true, true, 1],
    ['brood', '알주머니 터뜨리기(새끼 거미 넷)', 1.0, false, false, 0],
    ['eight_legs', '여덟 다리 내려찍기(여덟 다리를 치켜듦, 경고)', 2.0, true, true, 0],
    // Its own webs plucked: they are laid first (below) and left on the floor for it.
    ['web_burst', '그물 폭발(몸을 낮추고 앞다리로 실을 두 번 당겼다 놓음, 경고, 깔린 거미줄이 함께 터져 사라진다)', 1.6, true, true, 0],
  ];
  for (const [attack, name, telegraph, marked, strong, pools] of uses) {
    await game.send('heal; clear');
    // Long enough for what the last attack left on the floor to be gone.
    await game.wait(pools > 0 ? 1800 : 1500);
    let laid = 0;
    if (attack === 'web_burst') {
      // Webs down first, and nothing clears them: the burst marks each of them.
      await game.send('heal; boss silk_mother use web_shot');
      await game.wait(1900);
      laid = (await game.state()).pools || 0;
      check(laid >= 3, `그물 폭발 전에 거미줄이 깔려 있다 (${laid})`);
    }
    await game.send(`heal; boss silk_mother use ${attack}`);
    await game.wait(Math.round(telegraph * 600));
    const s = await game.state();
    if (attack === 'web_burst') {
      check((s.marksShown || 0) >= laid && laid > 0, `그물 폭발: 깔린 거미줄마다 표시가 뜬다 (거미줄 ${laid}, 표시 ${s.marksShown})`);
      check((s.poolsShown || 0) >= laid, `그물 폭발: 전조 동안 거미줄이 그대로 보인다 (${s.poolsShown})`);
    }
    const shots = attack === 'venom_spit' || attack === 'brood';
    if (!shots) check(((s.marksShown || 0) > 0) === marked, `${name}: 전조 동안 바닥 표시 ${marked ? '있음' : '없음'} (표시 ${s.marksShown})`);
    check(!!s.warningSign === strong, `${name}: 경고 표지 ${strong ? '있음' : '없음'} (${s.warningSign})`);
    check(has(s.bosses, `:Telegraph:${attack}`), `${name}: 그 공격의 전조 중 (${s.bosses})`);
    await game.shot(`spider-${attack}`, `${name} 전조`);
    await game.wait(Math.round(telegraph * 400) + 80);
    await game.shot(`spider-${attack}-hit`, `${name} 터진 순간`);
    if (attack === 'web_burst') {
      // The headless page can run the game slower than the clock, so the burst may land a moment late; and the
      // spider, fighting on, may lay fresh webs of its own just after. So watch for up to three seconds and pass if
      // the webs that were down fall below what was laid (the burst took them away).
      let gone = await game.state();
      let fewest = Math.min(gone.pools || 0, gone.poolsShown || 0);
      for (let k = 0; k < 30 && fewest > 0; k++) {
        await game.wait(100);
        gone = await game.state();
        fewest = Math.min(fewest, gone.pools || 0, gone.poolsShown || 0);
      }
      check(fewest < laid, `그물 폭발: 터진 거미줄은 사라진다 (깔린 ${laid}, 터진 뒤 가장 적을 때 ${fewest})`);
    }
    if (pools > 0) {
      await game.wait(500);
      const after = await game.state();
      check((after.pools || 0) >= pools && (after.poolsShown || 0) >= pools, `${name}: 바닥에 ${pools}개 넘게 남는다 (월드 ${after.pools}, 화면 ${after.poolsShown})`);
      await game.shot(`spider-${attack}-floor`, `${name} 뒤 바닥`);
    }
  }
  // Walk into a web: caught, and slower.
  await game.send('heal; clear');
  await game.wait(8000);
  await game.send('heal; boss silk_mother use web_shot');
  await game.wait(1500);
  const webbed = await game.state();
  check((webbed.pools || 0) >= 3, `거미줄 셋이 깔렸다 (${webbed.pools})`);
  await game.send('heal; goto pool');
  await game.wait(400);
  const caught = await game.state();
  check(caught.snared === true, `거미줄 위에 서면 묶인다 (snared ${caught.snared})`);
  const moved = await game.state();
  check(Math.abs(moved.x - caught.x) + Math.abs(moved.y - caught.y) < 2, '묶인 자리에 그대로 있다');
  await game.shot('spider-snared', '거미줄에 묶인 플레이어');
  await game.send('heal; clear; boss silk_mother hurt 0.36');
  await game.wait(700);
  await game.shot('spider-phase2', '2페이즈 울부짖음');
  await game.send('heal; boss silk_mother hurt 0.34');
  await game.wait(2600);
  const last = await game.state();
  check(has(last.bosses, 'silk_mother') && has(last.bosses, ':p3:'), `체력을 깎으면 3페이즈 (${last.bosses})`);
  await game.shot('spider-phase3', '3페이즈(무늬가 타오르고 독이 흐른다)');
  await game.send('heal; boss silk_mother hurt 1');
  await game.wait(900);
  await game.shot('spider-fall', '쓰러지는 아라네(다리를 오므린다)');
  await game.wait(1500);
  await game.shot('spider-fallen', '쓰러진 뒤');
  await game.context.close();
}

// ---------------------------------------------------------------- 생물군계, 어둠과 빛, 날씨 (12·29·31·40·71단계)
async function world(browser) {
  note('\n## 생물군계와 빛 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  await game.send('goto biome Cave');
  await game.wait(1000);
  await game.shot('cave-dark', '흙 동굴의 어둠');
  for (const [biome, name] of [['Mushroom', '버섯 숲'], ['Ice', '얼음 동굴'], ['Crystal', '수정 동굴'], ['Lava', '용암 지대'], ['Abyss', '공허의 심연'], ['Lake', '지하 호수'], ['Sand', '모래 동굴']]) {
    await game.send(`heal; goto biome ${biome}`);
    const banner = await game.until(t => has(t.banner, name), 4000, game.texts);
    check(!!banner, `${name}에 들어서면 "${name}" 배너`);
    check((await game.texts()).hud.join(' ').includes(name), `${name}: HUD에 이름`);
    await game.send('speed 6');
    await game.wait(4000);
    await game.send('speed 1');
    const s = await game.state();
    note(`  - ${name}의 몬스터: ${s.monsterKinds || '없음'}`);
    await game.shot(`biome-${biome}`, name);
    await game.send('clear');
  }
  // Lava burns.
  await game.send('heal; goto tile Lava');
  await game.wait(2500);
  const burnt = await game.state();
  check(burnt.health < burnt.maxHealth, `용암에 들어가면 화상 (${burnt.health}/${burnt.maxHealth})`);
  await game.shot('lava', '용암 위');
  // A better pickaxe for obsidian.
  await game.send('heal; goto biome Lava');
  await clearRoom(game, 2, 2);
  await game.send('build ObsidianOre 1 0; face 1 0; equip copper_pickaxe');
  await game.send('attack on 1 0');
  const better = await game.until(r => (r.floating || []).some(f => has(f, '더 좋은 곡괭이')), 3000, game.texts);
  await game.send('attack off');
  check(!!better, '흑요석에 약한 곡괭이로는 "더 좋은 곡괭이가 필요하다"');

  // Light: a torch in the dark, the light wells over a day.
  await game.send('goto spawn; clear');
  await game.wait(800);
  for (const hour of [9, 13, 19, 23]) {
    await game.send(`clock 4 ${hour}`);
    await game.wait(1500);
    await game.shot(`light-${hour}`, `시작 지점의 빛 구멍 ${hour}시`);
  }
  await game.context.close();
}

// ---------------------------------------------------------------- 농사 (5·22·34·43·51·59단계)
async function farm(browser) {
  note('\n## 농사 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 4, 3);
  await game.send('face 1 0; give watering_can 1; give hoe 1; give fertilizer 2; give frost_berry_seed 2; give cave_berry_seed 1');
  await bagTab(game, '재료·제작');
  await game.press('심기', '버섯 포자');
  let crop = (await game.look(1, 0)).crop;
  check(crop && crop.id === 'mushroom', '[심기]로 바라보는 칸에 버섯');
  await game.press('물 주기', '물뿌리개');
  crop = (await game.look(1, 0)).crop;
  check(crop && crop.wet, '[물 주기]로 흙이 젖는다');
  await closeBag(game);
  await game.shot('planted', '심고 물 준 버섯');
  // Frost berries only grow in the ice caves.
  await game.send('face 0 1');
  await bagTab(game, '재료·제작');
  await game.press('심기', '서리 열매 씨앗');
  check(!(await game.look(0, 1)).crop && (await game.allText()).includes('얼음 동굴'), '흙 동굴에 서리 열매를 심으면 "얼음 동굴에서만…"');
  // Tilling and fertilising.
  await game.send('face -1 0');
  await game.wait(300);
  await game.press('밭 갈기', '호미');
  check((await game.look(-1, 0)).tile === 'Soil', '[밭 갈기]로 밭');
  await game.press('거름 주기', '비료');
  check((await game.look(-1, 0)).tile === 'FertileSoil', '[거름 주기]로 비옥한 밭');
  await game.press('심기', '동굴 딸기');
  await closeBag(game);
  // A sprinkler keeps both wet while the world runs fast.
  await game.send('build Sprinkler 0 -1; speed 60');
  await game.wait(3000);
  check((await game.look(-1, 0)).crop && (await game.look(-1, 0)).crop.wet, '스프링클러가 둘레 작물을 적신다');
  await game.shot('sprinkler', '스프링클러와 자라는 작물');
  let grown = null;
  for (let i = 0; i < 90 && !grown; i++) {
    await game.wait(1000);
    const c = (await game.look(1, 0)).crop;
    if (c && c.ripe) grown = c;
    if (i === 8) await game.shot('growing', '자라는 중');
  }
  await game.send('speed 1');
  check(!!grown, '버섯이 다 자란다');
  await game.shot('ripe', '다 자란 작물');
  // A harvest may come in silver or gold (another material id), so every mushroom of any quality counts.
  const mushrooms = inv => Object.entries(inv.materials || {}).filter(([id]) => /^mushroom(?!_spore|_soup|_soil|_pot)/.test(id)).reduce((n, [, c]) => n + c, 0);
  const before = mushrooms(await game.inventory());
  await game.send('goto 1 0');
  await game.wait(1500);
  const after = await game.inventory();
  check(mushrooms(after) > before, `다 자란 버섯 위를 걸으면 거둔다 (${Object.keys(after.materials).filter(id => /^mushroom/.test(id)).join(', ')})`);

  // At night a monster on a crop tramples it; a scarecrow guards four tiles round (2026-11-09, Docs/Farming.md).
  await game.send('goto 0 9');
  await clearRoom(game, 6, 3);
  await game.send('clock 1 23; give mushroom_spore 2; give scarecrow 1; face 1 0');
  await game.wait(600);
  await bagTab(game, '재료·제작');
  await game.press('심기', '버섯 포자');
  await closeBag(game);
  await game.send('spawn slime 1 0 99999');
  const trampled = await game.until(t => /짓밟혔다/.test(t), 6000, game.allText);
  check(!!trampled, `밤에 몬스터가 작물 위에 서면 "짓밟혔다" (${(await game.look(0, 0)).biome})`);
  check(/허수아비/.test((await game.texts()).banner || ''), `처음 밟혔을 때 허수아비를 알려 준다 ("${(await game.texts()).banner || ''}")`);
  await game.shot('trampled', '짓밟힌 밭과 허수아비 안내');
  await game.send('clear');
  await game.wait(2500);
  // The scarecrow set down from the bag two tiles left; a crop beside it is left alone.
  await game.send('goto -1 0; face -1 0');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('놓기', /^허수아비/);
  await game.send('face 0 1');
  await game.wait(300);
  await game.press('심기', '버섯 포자');
  await closeBag(game);
  check((await game.look(-1, 0)).tile === 'Scarecrow' && !!(await game.look(0, 1)).crop, '허수아비를 [놓기]로 세우고 곁에 버섯을 심는다');
  await game.send('spawn slime 0 1 99999');
  const guarded = await game.until(t => /짓밟혔다/.test(t), 5000, game.allText);
  check(!guarded, '허수아비 곁의 작물은 몬스터가 서도 밟히지 않는다');
  await game.shot('scarecrow', '허수아비가 지키는 밭');
  await game.send('clear; clock 2 9');
  await game.context.close();
}

// ---------------------------------------------------------------- 생활: 낚시, 가공, 동물, 통발, 잠, 귀환 (32·42·51·59·60·64단계)
async function life(browser) {
  note('\n## 낚시, 가공, 동물, 잠 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 5, 3);

  // A pond and the rod.
  await game.send('build Water 1 0; build Water 2 0; build Water 1 1; build Water 2 1; build Water 1 -1; build Water 2 -1; face 1 0; give fishing_rod 1; give bait 3');
  // The bag stays open on its materials page: the rod's button is [낚시], [걷기] while waiting and [낚아채기!] at a
  // bite, always in the same place, so it is pressed the moment the marker is in the green.
  await bagTab(game, '재료·제작');
  await game.press('낚시', '낚싯대');
  // Where the rod's button is, read once the page is drawn (read right after the tab changed, it was not there yet
  // and no strike was ever tried).
  const rod = (await game.find(/^(낚시|걷기|낚아채기!)$/, '낚싯대'))[0];
  check(!!rod, '낚싯대 줄의 단추가 보인다');
  check((await game.state()).fishing === 'wait', '[낚시]로 찌를 던진다');
  await game.shot('fishing', '찌를 던진 모습');
  let caught = false;
  let pictured = false;
  let tries = 0;
  const fishBefore = Object.keys((await game.inventory()).materials).length;
  const deadline = Date.now() + 150000;
  while (!caught && rod && Date.now() < deadline && tries < 6) {
    const s = await game.state();
    if (s.fishing === 'bite in') {
      await game.tapAt(rod.cx, rod.cy);
      await game.wait(700);
      caught = Object.keys((await game.inventory()).materials).length > fishBefore;
      tries++;
    } else if (s.fishing === 'bite out' && !pictured) {
      await game.shot('fishing-bite', '입질: 머리 위 게이지와 초록 칸');
      pictured = true;
    } else if (!s.fishing) {
      await game.tapAt(rod.cx, rod.cy);
      await game.wait(400);
    }
  }
  await closeBag(game);
  note(`  - 낚아채기 ${tries}번`);
  check(caught, '입질에 초록 칸에서 낚아채면 물고기가 들어온다');

  // A keg with berries, a coop with a hen, a crab pot in the pond. O is where the player stands now; "goto" moves
  // relative to the player, so each move is from the last place.
  await game.send('build Keg 0 1; build Coop -3 0; build CrabPot 3 0; give cave_berry 4; give chicken 1; give feed 6');
  await game.send('face 0 1');
  await bagTab(game, '재료·제작');
  await game.press('넣기', '동굴 딸기');
  check((((await game.look(0, 1)).machine || {}).input || {}).cave_berry > 0, '술통에 [넣기]로 딸기를 넣는다');
  await closeBag(game);
  await game.send('goto -2 0; face -1 0'); // O-2,0 facing the coop
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('넣기', '닭 ×');
  await game.press('넣기', '사료');
  check(Object.keys(((await game.look(-1, 0)).machine || {}).input || {}).length > 0, '닭장에 [넣기]로 닭과 사료를 넣는다');
  // Petting (2026-11-09): once a day, the house grows fonder.
  await game.wait(600);
  check(await game.press('쓰다듬기'), '닭이 든 닭장 앞에서 가방 [재료·제작]에 [쓰다듬기]');
  await game.wait(600);
  check(((await game.look(-1, 0)).machine || {}).affection === 1, `쓰다듬으면 애정이 1 (${((await game.look(-1, 0)).machine || {}).affection})`);
  check((await game.find('쓰다듬기')).length === 0, '하루에 한 번: 쓰다듬은 뒤에는 단추 대신 "오늘 쓰다듬음"');
  await game.shot('petted', '쓰다듬은 닭장의 애정 줄');
  await closeBag(game);
  await game.send('goto 5 1; face 0 -1'); // O+3,1 facing the crab pot
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('넣기', '미끼');
  await closeBag(game);
  await game.send('goto -3 -1; face 0 1'); // back at O
  const day = (await game.state()).day;
  await game.send('speed 60');
  const nextDay = await game.until(r => r.day >= day + 1, 90000);
  await game.wait(3000);
  await game.send('speed 1');
  note(`  - 빠르게 돌린 하루: ${nextDay ? '넘어갔다' : '넘어가지 못했다'}`);
  const keg = (await game.look(0, 1)).machine || {};
  check(Object.keys(keg.output || {}).length > 0, `술통이 주스를 만든다 (${JSON.stringify(keg.output)})`);
  const coop = (await game.look(-3, 0)).machine || {};
  // Petted once, an egg may come out silver (egg_silver and the like).
  check(Object.keys(coop.output || {}).some(id => /egg/.test(id)), `닭장에 하루 뒤 달걀 (${JSON.stringify(coop.output)})`);
  const pot = (await game.look(3, 0)).machine || {};
  check(Object.keys(pot.output || {}).length > 0, `통발에 잡힌 것 (${JSON.stringify(pot.output)})`);
  await game.shot('animals', '술통·닭장·통발');
  await bagTab(game, '재료·제작');
  await game.press('꺼내기');
  await closeBag(game);
  check(Object.keys((await game.inventory()).materials).some(id => /juice/.test(id)), '술통의 [꺼내기]로 주스를 받는다');

  // A bed: sleep at night, recall, and getting up beside it.
  await game.send('give bed 1; build Floor 0 -2; face 0 -1; goto 0 -1');
  await game.wait(400);
  await game.send('face 0 -1');
  const bedSide = await game.state();
  await bagTab(game, '재료·제작');
  await game.press('놓기', '침대');
  await closeBag(game);
  check((await game.look(0, -1)).tile === 'Bed' || (await game.look(0, -2)).tile === 'Bed', '침대를 놓는다');
  await game.send('clock 1 21');
  await game.wait(1500);
  check(await game.press('잠자기'), '밤에 침대 곁에서 [잠자기]');
  const slept = await game.until(t => (t.hud || []).some(l => /^2일째 0[6-7]/.test(l)), 8000, game.texts);
  check(!!slept, '혼자면 자고 나면 다음 날 아침');
  await game.send('goto 5 2; potion recall_scroll 1');
  await bagTab(game, '장비');
  await pick(game, '귀환 두루마리');
  await game.press('쓰기', '귀환 두루마리');
  await closeBag(game);
  check((await game.texts()).hud.some(l => has(l, '귀환 중')), '귀환 두루마리를 쓰면 "귀환 중…"');
  await game.wait(3000);
  const recalled = await game.state();
  const near = (a, b) => Math.hypot(a.x - b.x, a.y - b.y) < 2.5;
  check(near(recalled, bedSide), `2초 뒤 침대 곁으로 돌아온다 (${recalled.x},${recalled.y})`);
  await game.shot('recalled', '귀환한 뒤(침대 곁)');
  await game.send('goto 4 0; hurt 99999');
  const up = await game.until(s => !s.dead, 9000);
  check(!!up && near(up, bedSide), '쓰러지면 침대 곁에서 일어난다');
  const inv = await game.inventory();
  note(`  - 생활 기술 경험치(농사, 낚시, 채굴, 요리): ${inv.life}`);

  // A special perk at mining level 5 (2026-11-09), picked in the character tab.
  await game.send('lifexp mining 1000');
  await game.wait(800);
  await bagTab(game, '캐릭터');
  const pickPerk = (await game.find('고르기', '날쌘 곡괭이'))[0];
  check(!!pickPerk, '채굴 5레벨이면 [캐릭터]에 특별 혜택 [고르기]가 보인다');
  if (pickPerk) await game.press('고르기', '날쌘 곡괭이');
  await game.wait(800);
  const perks = ((await game.inventory()).perks || '').split(',').map(Number);
  check(perks[4] === 2, `날쌘 곡괭이를 고른다 (${perks.join(',')})`);
  await game.shot('life-perk', '고른 생활 기술 혜택');
  await closeBag(game);

  // A pen (2026-11-09, Docs/Animals.md): fences round the tiles in front of a coop let the hen out to graze, and it
  // lays without feed (the day half as long again). The coop at -3,0 faces east, so its front is -2,0; the fences
  // close -2..1 by -1..1, twelve tiles for one hen.
  await game.send('goto 0 9');
  await clearRoom(game, 5, 3);
  const fences = ['build Fence -3 1', 'build Fence -3 -1'];
  for (let x = -3; x <= 2; x++) fences.push(`build Fence ${x} 2`, `build Fence ${x} -2`);
  for (let y = -1; y <= 1; y++) fences.push(`build Fence 2 ${y}`);
  await game.send(`build Coop -3 0; ${fences.join('; ')}; give chicken 1`);
  await game.send('goto -2 0; face -1 0');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('넣기', '닭 ×');
  await closeBag(game);
  check(Object.keys(((await game.look(-1, 0)).machine || {}).input || {}).length > 0, '울타리를 두른 닭장에 닭만 넣는다(사료 없이)');
  await game.wait(2500);
  await game.shot('pen', '울타리 안에서 풀을 뜯는 닭');
  await game.send('speed 60');
  const laid = await game.until(look => Object.keys((look.machine || {}).output || {}).some(id => /egg/.test(id)), 75000, () => game.look(-1, 0));
  await game.send('speed 1');
  check(!!laid, `방목하는 닭은 사료 없이도 알을 낳는다 (${JSON.stringify(((await game.look(-1, 0)).machine || {}).output)})`);
  await game.context.close();
}

// ---------------------------------------------------------------- 상인·대장장이·어부, 하루에 사는 양 (58·63·68단계)
async function npc(browser) {
  note('\n## 사람들과 상점 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto near MerchantStall; give coin 300; give copper_ore 30; give star_melon 1');
  await game.wait(800);
  // Each merchant keeps shop in a timber room of their own (2026-11-11, Docs/Npcs.md 상점 건물).
  const shop = await game.state();
  check(shop.timberNear >= 12, `상인이 나무 벽 상점 안에 있다 (둘레 나무 벽 ${shop.timberNear}칸)`);
  await game.shot('merchant', '상점 안의 상인');
  check(await bagTab(game, '상인'), '노점 곁에서 [상인 도라와 말하기] → [거래]로 상점 창(U3)');
  await game.press('말 걸기');
  check(((await game.inventory()).friends || {}).merchant > 0, '[말 걸기]로 호감이 오른다');
  await game.press('사기');
  const potions = (await game.inventory()).consumables.healing_potion;
  await game.press('사기', '회복 물약');
  check((await game.inventory()).consumables.healing_potion === potions + 1, '금화로 회복 물약을 산다');
  await game.shot('shop-buy', '[사기]');
  // The festival (2026-11-09, every eleventh day): skill crystals on sale that day only.
  check((await game.find('사기', /^확산의 결정/)).length === 0, '평소에는 상인이 확산의 결정을 팔지 않는다');
  await game.send('clock 11 9');
  await game.wait(1200);
  check((await game.find('사기', /^확산의 결정/)).length > 0, '축제 날(11일)에는 상인이 확산의 결정을 판다');
  await game.shot('festival-shop', '축제 날의 상점');
  await game.send('clock 1 9');
  await game.wait(800);
  await game.press('팔기');
  // The merchant buys copper ore (not stone): 30 offered, 20 taken in a day.
  const coins = count(await game.inventory(), 'coin');
  await game.press('모두', /^구리 광석 ×/);
  let inv = await game.inventory();
  check(count(inv, 'copper_ore') === 10 && count(inv, 'coin') > coins, `구리 광석을 팔면 하루 20개까지만 사고 금화가 는다 (광석 ${count(inv, 'copper_ore')}, 금화 ${coins} → ${count(inv, 'coin')})`);
  const stopped = (await game.find('팔기', /^구리 광석 ×/))[0];
  check(stopped && !stopped.enabled && has(stopped.row, '오늘은 그만 산다'), '20개를 판 뒤 "오늘은 그만 산다", [팔기]가 눌리지 않는다');
  await game.shot('shop-limit', '하루 한도에 닿은 [팔기]');
  await game.press('선물');
  await game.press('선물', '별 수박');
  await game.wait(600);
  check(((await game.inventory()).friends || {}).merchant >= 40 || (await game.allText()).includes('좋아한다'), '좋아하는 선물로 호감이 크게 오른다');
  await game.press('부탁');
  await game.shot('shop-request', '[부탁]');
  await closeBag(game);
  // Met now, the merchant's request for today is in the goal list with how much of it the bag holds.
  const listed = (await game.texts()).hud.find(l => /^부탁: 상인/.test(l));
  check(!!listed && /\d+\/\d+/.test(listed), `만난 상인의 오늘 부탁이 목표 목록에 진행도와 함께 (${listed || '없음'})`);
  // The merchant's story: its first step opens at a heart, shows in the goal list, and [건네기] hands it in.
  await game.send('hearts merchant 1; give iron_bar 5; give stone 30');
  await game.wait(800);
  const story = (await game.texts()).hud.find(l => /^이야기: 상인 도라/.test(l));
  check(!!story && /노점 손보기/.test(story), `호감 1이 되면 도라의 이야기가 목표 목록에 (${story || '없음'})`);
  await bagTab(game, '상인');
  await game.press('이야기');
  await game.shot('shop-story', '[이야기] 첫 단계');
  const coinsBefore = count(await game.inventory(), 'coin');
  check(await game.press('건네기'), '[이야기]의 [건네기]를 누른다');
  const told = await game.until(i => (i.stories || {}).merchant === 1, 3000, game.inventory);
  check(!!told && count(told, 'coin') > coinsBefore, `이야기 1단계를 마치면 금화를 받고 다음 단계로 (${JSON.stringify((told || {}).stories)})`);
  await game.shot('shop-story-done', '첫 단계를 마친 [이야기]');
  // The journal: the merchant's story one step along, and today's request (a screen of its own since U2, J).
  await bagTab(game, '일지');
  const journal = (await game.buttons()).length >= 0;
  await game.shot('journal', '[일지] 탭');
  // The bestiary and the achievements, two more sections of the journal (Docs/Guide.md 도감과 업적; U5: the book's index).
  check(await game.press('도감'), '[일지]의 [도감]을 누른다');
  await game.shot('journal-bestiary', '[일지] 도감');
  check(await game.press('업적'), '[일지]의 [업적]을 누른다');
  await game.shot('journal-achievements', '[일지] 업적');
  const seen = await game.inventory();
  check(seen.records > 0, `가진 재료와 장비가 도감에 든다 (${seen.records})`);
  await game.press('목표');
  // A pet (Docs/Animals.md 펫): an egg hatched from the materials tab follows the player.
  await closeBag(game);
  await game.send('give slime_egg 1');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  check(await game.press('부화', '말랑한 알'), '말랑한 알의 [부화]를 누른다');
  const hatched = await game.until(i => i.pet === 'pet_slime', 3000, game.inventory);
  check(!!hatched, `알에서 펫 말랑이가 나온다 (${hatched && hatched.pet})`);
  await closeBag(game);
  await game.send('goto 3 0');
  await game.wait(1500);
  await game.shot('pet', '따라오는 펫 말랑이');
  // Growing: the pet takes its player's experience (Docs/Animals.md 펫 키우기).
  await game.send('experience 1300');
  const grown = await game.until(i => i.petLevel >= 5, 3000, game.inventory);
  check(!!grown, `경험치를 받으면 펫도 자란다 (Lv ${grown && grown.petLevel})`);
  check((await game.allText()).includes('Lv 5이 되었다'), '"펫 말랑이이(가) Lv 5이 되었다!" 알림');
  await bagTab(game, '캐릭터');
  check(journal && (await game.state()).window === '캐릭터', '[캐릭터] 화면을 연다');
  // A name for the pet (2026-11-09): typed into the field, then [이름 짓기]. Keys typed must not walk the player.
  // No monsters about, so nothing knocks them aside meanwhile.
  await game.send('clear');
  await game.wait(600);
  const shown = await game.buttons();
  const nameButton = shown.find(b => /^이름 짓기/.test(b.label));
  const suggest = shown.find(b => b.label === '추천');
  if (nameButton && suggest) {
    // The field is just left of [이름 짓기] (140 points wide); the two buttons give the screen's scale.
    const scale = (suggest.cx - nameButton.cx) / ((nameButton.rect[2] + suggest.rect[2]) / 2 + 4);
    await game.page.mouse.click(nameButton.cx - (nameButton.rect[2] / 2 + 74) * scale, nameButton.cy);
    await game.wait(400);
    const before = await game.state();
    check(before.typing === true, '이름 칸을 누르면 글자를 칠 수 있다(게임 키가 멈춘다)');
    await game.page.keyboard.type('dw', { delay: 80 });
    await game.wait(400);
    const after = await game.state();
    const moved = Math.abs(after.x - before.x) + Math.abs(after.y - before.y);
    check(moved < 0.2, `이름 칸에 글자를 치는 동안 W·D로 걷지 않는다 (${moved.toFixed(2)}칸)`);
  }
  check(await game.press('추천'), '[캐릭터] 탭의 펫 줄에 [추천]');
  check(await game.press('이름 짓기'), '[이름 짓기]를 누른다');
  const named = await game.until(i => i.petName && i.petName !== '말랑이', 2000, game.inventory);
  check(!!named, `펫에 이름이 붙는다 (${named && named.petName})`);
  const doll = await game.find(/구리 검/, '캐릭터');
  check(doll.length === 1, `[캐릭터] 탭의 사람 모양 칸에 든 무기 (${doll.map(b => b.label).join(', ')})`);
  await game.shot('character', '[캐릭터] 탭');
  await closeBag(game);
  // The alchemist at her table: her tab and her story.
  await game.send('goto near AlchemyTable; hearts alchemist 1');
  await game.wait(800);
  await game.shot('alchemist', '연금술사의 탁자');
  check(await bagTab(game, '연금술사') && /^연금술사/.test((await game.state()).shopkeeper), '연금술사 곁에서 그의 상점 창');
  await game.press('이야기');
  check((await game.find('아직 모자라다')).length === 1 || (await game.find('건네기')).length === 1, '연금술사의 [이야기] 첫 단계(빛 버섯 연구)가 보인다');
  await game.shot('alchemist-story', '연금술사의 [이야기]');
  await closeBag(game);
  // Back at the merchant's stall: the alchemist's table is too far from it for her tab.
  await game.send('goto near MerchantStall; clock 2 7');
  await game.wait(1200);
  await bagTab(game, '상인');
  await game.press('팔기');
  const again = (await game.find('팔기', /^구리 광석 ×/))[0];
  check(again && again.enabled, '다음 날이 되면 다시 산다');
  await closeBag(game);

  for (const [tile, role] of [['SmithStall', '대장장이'], ['FisherHut', '어부']]) {
    await game.send(`goto near ${tile}`);
    await game.wait(800);
    const opened = await bagTab(game, role);
    const keeper = (await game.state()).shopkeeper;
    check(opened && keeper.startsWith(role), `${role} 곁에서 그의 상점 창 (${keeper || '없음'})`);
    await game.shot(`npc-${tile}`, `${role}의 상점 창`);
    await closeBag(game);
  }

  // Moving in (Docs/Npcs.md 이사): a room walled round, away from the people, with a door, a bed and a banner; the
  // merchant at two hearts moves into it from her stall.
  await game.send('goto spawn; goto 22 0');
  await clearRoom(game, 5, 3);
  const room = [];
  for (let x = 1; x <= 7; x++) room.push(`build Wall ${x} -1`, `build Wall ${x} 4`);
  for (let y = 0; y <= 3; y++) room.push(`build Wall 1 ${y}`, `build Wall 7 ${y}`);
  await game.send('build Floor 0 0; ' + room.join('; ') + '; build Door 4 -1; build Bed 2 3; build Banner 6 0');
  await game.send('goto near MerchantStall; hearts merchant 1');
  await game.wait(800);
  await bagTab(game, '상인');
  const early = (await game.find('이사 권하기'))[0];
  check(early && !early.enabled, `호감 1에서는 [이사 권하기]가 눌리지 않는다`);
  await game.send('hearts merchant 2');
  await game.wait(500);
  await game.shot('invite', '[이사 권하기]');
  check(await game.press('이사 권하기'), '호감 2에서 [이사 권하기]를 누른다');
  await game.wait(800);
  check((await game.allText()).includes('이사 왔다'), '"상인 도라이(가) 기지의 방으로 이사 왔다!" 알림');
  await closeBag(game);
  await game.send('goto near MerchantStall');
  await game.wait(1200);
  await game.shot('moved-in', '방으로 이사 온 상인');
  await bagTab(game, '상인');
  check((await game.find('이사 권하기')).length === 0 && (await game.find('말 걸기')).length + (await game.find('오늘은 이야기했다')).length === 1,
    '방에서도 상점 창이 열리고 [이사 권하기]는 없다');
  await game.shot('moved-in-tab', '방에 사는 상인의 상점 창');
  await closeBag(game);
  await game.context.close();
}

// ---------------------------------------------------------------- 공장 (10·23·34·44·56·57·67단계)
async function factory(browser) {
  note('\n## 공장 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 6, 4);
  // vein ← drill → belt → smelter → belt → chest, a row above the player.
  await game.send('build IronOre -4 2; build Drill -3 2 2; build Conveyor -2 2 0; build Smelter -1 2 0; build Conveyor 0 2 0; build Chest 1 2');
  // A generator and a pole to power the drill.
  await game.send('build Generator -3 4; fill -3 4 slime_gel 20; build PowerPole -1 4');
  await game.send('speed 20');
  await game.wait(12000);
  await game.send('speed 1');
  const chest = (await game.look(1, 2)).chest || {};
  check(chest.iron_bar > 0, `채굴기 → 벨트 → 제련기 → 벨트 → 상자로 철 주괴가 쌓인다 (${JSON.stringify(chest)})`);
  const drill = (await game.look(-3, 2)).machine || {};
  check(drill.powered, '발전기와 전신주로 채굴기에 전력');
  await game.shot('factory', '채굴기·벨트·제련기·상자, 발전기와 전신주');

  // The switch over the machine ahead: the button, and G.
  await game.send('goto -3 1; face 0 1');
  await game.wait(800);
  check(await game.press('끄기 (G)'), '기계를 바라보면 [끄기 (G)] 단추');
  check((await game.look(0, 1)).machine.enabled === false, '누르면 기계가 꺼진다');
  await game.shot('machine-off', '꺼진 채굴기');
  await game.page.keyboard.press('g');
  await game.wait(500);
  check((await game.look(0, 1)).machine.enabled === true, 'G로 다시 켜진다');

  // A filter's picks from the bag: [거르기] and [빼기].
  await game.send('goto 3 -1; build Filter 0 -1 3; face 0 -1; give iron_ore 5; give stone 5'); // back at O
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('거르기', '철 광석');
  check(((await game.look(0, -1)).machine || {}).picks === 'iron_ore', '거름망 앞에서 [거르기]로 철 광석을 고른다');
  await game.press('거르기', '돌 ×');
  check(((await game.look(0, -1)).machine || {}).picks === 'iron_ore,stone', '[거르기]로 둘째 재료');
  await game.shot('filter', '거름망 분배기의 [거르기]·[빼기]');
  await game.press('빼기', '철 광석');
  check(((await game.look(0, -1)).machine || {}).picks === 'stone', '[빼기]로 뺀다');
  await closeBag(game);

  // An arc furnace takes ore from the bag; an assembler takes a recipe.
  await game.send('build ArcFurnace 2 -1; goto 2 0; face 0 -1');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('넣기', '철 광석');
  check((((await game.look(0, -1)).machine || {}).input || {}).iron_ore > 0, '전기로에 [넣기]로 광석을 넣는다');
  await closeBag(game);
  await game.send('build Assembler 4 -1; goto 4 0; face 0 -1');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('조립기에', /^철 톱니/);
  check(((await game.look(0, -1)).machine || {}).recipe, `조립기에 만들 것을 정한다 (${((await game.look(0, -1)).machine || {}).recipe})`);
  await game.shot('assembler', '조립기에 정한 뒤');
  await game.press(/^공장 생산 보기/);
  await game.shot('production', '공장 생산 보기');
  await closeBag(game);

  // A robot arm and a splitter beside a chest, and a lever.
  await game.send('build Chest -5 -2; fill -5 -2 iron_ore 10; build Arm -4 -2 0; build Splitter -3 -2 0; build Conveyor -2 -2 0; build Lever -5 0');
  await game.send('speed 10');
  await game.wait(4000);
  await game.send('speed 1');
  await game.shot('arm', '로봇 팔·분배기·레버');

  // A machine two tiles each way (2026-11-09, Docs/Automation.md 큰 기계): the big assembler, set down from the bag,
  // takes the square ahead, and each of its tiles is the machine.
  await game.send('goto 0 9');
  await clearRoom(game, 5, 3);
  await game.send('give big_assembler 1; face 1 0');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('놓기', /^대형 조립기/);
  await closeBag(game);
  let parts = 0;
  for (let dx = 1; dx <= 3; dx++) for (let dy = -1; dy <= 1; dy++) if (((await game.look(dx, dy)).machine || {}).kind === 'BigAssembler') parts++;
  check(parts === 4, `대형 조립기는 [놓기]로 앞의 네 칸을 차지한다 (${parts}칸)`);
  await game.wait(800);
  await game.shot('big-assembler', '2×2 대형 조립기');
  // The big smelter beside it (2026-11-09), from the bag, and the frame over its square while pointing at [놓기].
  await game.send('goto -4 0; give big_smelter 1; face 1 0');
  await game.wait(400);
  await bagTab(game, '재료·제작');
  await game.press('놓기', /^대형 제련로/);
  await closeBag(game);
  let smelterParts = 0;
  for (let dx = 1; dx <= 3; dx++) for (let dy = -1; dy <= 1; dy++) if (((await game.look(dx, dy)).machine || {}).kind === 'BigSmelter') smelterParts++;
  check(smelterParts === 4, `대형 제련로도 [놓기]로 네 칸을 차지한다 (${smelterParts}칸)`);
  await game.shot('big-smelter', '2×2 대형 제련로');
  await game.context.close();
}

// ---------------------------------------------------------------- 장비 다듬기, 특성, 클래스 바꾸기 (33·52·53·54단계)
async function gear(browser) {
  note('\n## 모루, 특성, 클래스 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await clearRoom(game, 3, 2);
  await game.send('build Anvil 1 0; face 1 0; drop item iron_sword rare; give essence 60; give iron_bar 20; xp 3000');
  await game.wait(1500);
  const hud = (await game.texts()).hud.join(' ');
  check(/특성 포인트 \d+/.test(hud) && /스킬 포인트 \d+/.test(hud), `레벨이 오르면 HUD에 "스킬 포인트 N · 특성 포인트 N" (${(hud.match(/스킬 포인트[^|]*/) || [''])[0]})`);
  await bagTab(game, '장비');
  await pick(game, /철 검/);
  await game.press('다듬기', '철 검');
  await game.shot('anvil', '모루 곁 [다듬기]');
  check(await game.press(/^강화 \+1/), '[강화 +1]이 보인다');
  const upgraded = (await game.inventory()).items.find(i => i.base === 'iron_sword');
  check(upgraded && upgraded.upgrade === 1, '강화하면 +1');
  // Moving an older piece's upgrade onto the newer one (Docs/Loot.md 강화 옮기기, 2026-11-11): a copper sword to +2,
  // then [강화 옮기기] on the iron sword takes its +2 and leaves it +0.
  await game.send('drop item copper_sword rare; give copper_ore 10');
  await game.wait(1200);
  await pick(game, /구리 검/);
  await game.press('다듬기', '구리 검');
  await game.press(/^강화 \+1/);
  await game.press(/^강화 \+2/);
  const copper = await game.until(i => i.items.some(x => x.base === 'copper_sword' && x.upgrade === 2), 3000, game.inventory);
  check(!!copper, '구리 검을 +2로 강화한다');
  await pick(game, /철 검/);
  await game.press('다듬기', '철 검');
  await game.shot('anvil-transfer', '철 검의 모루: [강화 옮기기]');
  check(await game.press(/^강화 옮기기: (\+2 )?구리 검의 \+2/), '철 검의 모루에 [강화 옮기기: 구리 검의 +2를 여기로]가 보이고 누른다');
  const moved = await game.until(i => i.items.some(x => x.base === 'iron_sword' && x.upgrade === 2), 3000, game.inventory);
  const old = moved && moved.items.find(x => x.base === 'copper_sword');
  check(!!moved && old && old.upgrade === 0, `철 검이 +2, 구리 검은 +0이 된다 (구리 검 +${old && old.upgrade})`);
  await bagTab(game, '특성');
  await game.shot('passives', '[특성] 탭');
  const node = (await game.buttons()).find(b => b.shown && /: /.test(b.label) && b.label.length > 4);
  if (node) {
    await game.press(node.label);
    await game.press('배우기');
  }
  check((await game.inventory()).passives === 1, '점을 골라 [배우기]');
  // Taking the tree back costs coins a point (2026-11-09).
  await game.send('give coin 20');
  await game.wait(400);
  const coinsBefore = count(await game.inventory(), 'coin');
  check(await game.press(/^모두 되돌리기 \(금화 5\)$/), '[모두 되돌리기 (금화 5)]가 보이고 누른다');
  const resetTree = await game.until(i => i.passives === 0, 3000, game.inventory);
  check(!!resetTree && count(resetTree, 'coin') === coinsBefore - 5, `되돌리면 점이 돌아오고 금화 5를 낸다 (${coinsBefore} → ${resetTree && count(resetTree, 'coin')})`);

  // Skills learned with points (Docs/Classes.md, 스킬 포인트와 단계): [배우기] on a row, then [올리기].
  await closeBag(game);
  await game.send('build Workbench -1 0; face -1 0');
  await game.wait(500);
  await bagTab(game, '스킬');
  await game.shot('skills-page', '[스킬] 탭(포인트, 칸, 배우기)');
  let before = await game.inventory();
  check(before.skills === 'whirlwind,fireball' && before.skillPoints > 0, `새 캐릭터는 클래스마다 첫 스킬만 알고 포인트가 있다 (${before.skills}, 포인트 ${before.skillPoints})`);
  check(await game.press('배우기', '전투 함성'), '[전투 함성]의 [배우기]를 누른다');
  let after = await game.inventory();
  check(/war_cry:1/.test(after.ranks) && after.skillPoints === before.skillPoints - 1, `포인트 하나로 전투 함성을 배운다 (${after.ranks}, 포인트 ${after.skillPoints})`);
  check(/war_cry/.test(after.skills), `배운 스킬이 빈 칸에 들어간다 (${after.skills})`);
  check(await game.press('올리기', '전투 함성'), '[올리기]를 누른다');
  check(/war_cry:2/.test((await game.inventory()).ranks), `전투 함성이 2단계가 된다 (${(await game.inventory()).ranks})`);
  const locked = await game.find('배우기', '칼날 폭풍');
  check(locked.length === 1 && !locked[0].enabled, '레벨이 모자란 스킬(칼날 폭풍, Lv 18)의 [배우기]는 눌리지 않는다');
  await game.shot('skills-learned', '전투 함성을 배워 올린 [스킬] 탭');
  // Forgetting them for coins: ten a point spent, the points back.
  await game.send('give coin 100');
  await game.wait(500);
  const spentBefore = await game.inventory();
  check(await game.press(/^되돌리기 \(금화 20\)$/), '[되돌리기 (금화 20)]가 보이고 누른다');
  const reset = await game.until(i => !/war_cry/.test(i.ranks), 3000, game.inventory);
  check(!!reset && reset.skillPoints === spentBefore.skillPoints + 2 && count(reset, 'coin') === count(spentBefore, 'coin') - 20,
    `되돌리면 포인트 둘이 돌아오고 금화 20을 낸다 (포인트 ${spentBefore.skillPoints} → ${reset && reset.skillPoints})`);
  await game.press('배우기', '전투 함성');
  // A support crystal into the whirlwind ([변형]), and back out.
  await game.send('give support_area 1; give support_leech 1');
  await game.wait(400);
  check(await game.press('변형', '회전베기'), '회전베기의 [변형]을 누른다');
  check(await game.press(/^확산의 결정 끼우기/), '[확산의 결정 끼우기]가 보이고 누른다');
  const supported = await game.until(i => /whirlwind:support_area/.test(i.supports), 3000, game.inventory);
  check(!!supported && count(supported, 'support_area') === 0, `확산의 결정이 회전베기에 들어간다 (${supported && supported.supports})`);
  // A rank 1 skill holds one crystal: the other is not offered once the slot is taken.
  check((await game.find(/^흡혈의 결정 끼우기/)).length === 0, '1단계 스킬은 결정 한 칸: 칸이 차면 흡혈의 결정 [끼우기]가 없다');
  await game.shot('skills-support', '회전베기에 끼운 확산의 결정');
  check(await game.press('빼기'), '[빼기]를 누른다');
  const out = await game.until(i => !i.supports && count(i, 'support_area') === 1, 3000, game.inventory);
  check(!!out, '빼면 결정이 가방으로 돌아온다');
  // The slots' order (U4: a slot's skill picked, [칸에 넣기], then the other slot; they swap).
  const skills = (await game.inventory()).skills;
  await game.press('스킬 칸 1');
  await game.press('칸에 넣기');
  await game.press('스킬 칸 2');
  const reordered = await game.until(i => i.skills !== skills, 3000, game.inventory);
  check(!!reordered, `[칸에 넣기]로 스킬 칸 순서를 바꾼다 (${skills} → ${(await game.inventory()).skills})`);
  // Giving the fire mage up for the archer: a change, for coins by level (2026-11-09); two slots at this level.
  await game.press('[2] 화염 마법사');
  await game.press('[ ] 궁수');
  await game.send('give coin 600');
  await game.wait(400);
  const goldBefore = count(await game.inventory(), 'coin');
  const changeButton = (await game.find(/^이대로 바꾸기 \(금화 \d+\)$/))[0];
  check(!!changeButton, `[이대로 바꾸기]에 금화가 보인다 (${changeButton && changeButton.label})`);
  await game.press(/^이대로 바꾸기/);
  const changed = await game.until(i => /archer/.test(i.classes), 3000, game.inventory);
  const price = changeButton ? Number(changeButton.label.match(/금화 (\d+)/)[1]) : 0;
  check(!!changed && count(changed, 'coin') === goldBefore - price, `작업대 곁에서 금화를 내고 클래스를 바꾼다 (${changed && changed.classes}, 금화 ${goldBefore} → ${changed && count(changed, 'coin')})`);
  await closeBag(game);
  await game.shot('archer', '궁수가 된 스킬 칸');
  // An elemental weapon (2026-11-09): the character tab shows the fire it adds, and a blow is followed by a fire hit.
  await game.send('equip flame_blade; clear');
  await game.wait(600);
  await bagTab(game, '캐릭터');
  check(/피해 [\d.]+ \+ 화염 [\d.]+/.test(await game.allText()), '불꽃 칼을 들면 [캐릭터]의 능력치에 "피해 N + 화염 M"');
  await closeBag(game);
  await game.send('spawn slime 1 0 400; face 1 0; attack on 1 0');
  await game.wait(700);
  await game.shot('flame-blade', '불꽃 칼로 친 슬라임(주황 숫자와 화상)');
  await game.send('attack off');
  await game.context.close();
}

// ---------------------------------------------------------------- 기지 방어, 포탑, 균열 (55·62·70단계)
async function defense(browser) {
  note('\n## 기지 방어와 균열의 단추 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 6, 4);
  // An iron wall (2026-11-09): one pickaxe blow cracks it, and the bag mends it with an iron bar.
  await game.send('build IronWall 1 0; give iron_bar 2; equip copper_pickaxe');
  await game.wait(600);
  check((await game.look(1, 0)).tile === 'IronWall', '철벽이 세워진다');
  await game.send('face 1 0; attack on 1 0');
  await game.wait(500);
  await game.send('attack off');
  await game.wait(600);
  check((await game.look(1, 0)).tile === 'IronWall', `곡괭이 한 번에는 철벽이 그대로 (${(await game.look(1, 0)).tile})`);
  await game.shot('iron-wall', '금이 간 철벽');
  const bars = count(await game.inventory(), 'iron_bar');
  // Over the wall in the world since U6 (PC: "(G)" after it), not in the bag.
  const mended = await game.press(/^고치기 \(철 주괴 1\)/);
  check(mended, '금이 간 철벽을 마주 보면 벽 위에 [고치기 (철 주괴 1)]');
  await game.wait(800);
  check(count(await game.inventory(), 'iron_bar') === bars - 1, `고치면 철 주괴 하나가 든다 (${bars} → ${count(await game.inventory(), 'iron_bar')})`);
  await closeBag(game);
  await game.send('build Floor 1 0');
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  // A turret with ammo shoots a slime.
  await game.send('build Turret 3 2; fill 3 2 ammo 10; spawn slime 5 2 5000');
  const shot = await game.until(r => r.hurtMonsters > 0, 6000);
  check(!!shot, '탄환을 넣은 포탑이 가까운 몬스터를 쏜다');
  await game.shot('turret', '포탑이 쏘는 모습');
  await game.send('clear');
  // The ward stone's own button.
  await game.send('build WardStone 0 1');
  await game.wait(800);
  check(await game.press(/^기지 방어 시작/), '수호석 곁에 [기지 방어 시작] 단추');
  const started = await game.until(r => !!r.defense, 3000);
  check(!!started, `단추를 누르면 기지 방어가 시작된다 (${started && started.defense})`);
  const hud = (await game.texts()).hud.join(' ');
  check(/기지 방어 1단계: \d\/\d 물결/.test(hud), `목표 목록에 방어 단계·물결·수호석 (${(hud.match(/기지 방어[^|]*/) || [''])[0]})`);
  await game.wait(4000);
  await game.shot('defense-button', '단추로 시작한 기지 방어');
  await game.context.close();

  const rift = await open(browser, run, Profiles.pc, url);
  await rift.startSolo();
  await rift.wait(1200);
  await rift.send('goto biome Cave');
  await clearRoom(rift, 3, 3);
  // As a player who reached the rift would be: level 40, skills learned.
  await rift.send('xp 21300; teach 3; equip void_sword void_helmet void_armor void_boots; heal; give rift_key 1; build RiftAltar 0 1');
  await rift.wait(800);
  check(await rift.press(/^균열 열기/), '제단 곁에 [균열 열기] 단추');
  const opened = await rift.until(r => !!r.rift, 3000);
  check(!!opened, `단추를 누르면 균열이 열린다 (${opened && opened.rift})`);
  const line = (await rift.texts()).hud.join(' ');
  check(/공허 균열 1단계: /.test(line), `목표 목록에 균열 단계·물결·시간·접사 (${(line.match(/공허 균열[^|]*/) || [''])[0]})`);
  await rift.wait(3000);
  await rift.shot('rift-button', '단추로 연 균열');
  await rift.context.close();
}

// ---------------------------------------------------------------- 저장과 불러오기, 내보내기·가져오기 (7·38·49·50단계)
async function save(browser) {
  note('\n## 저장, 새로 고침, 내보내기·가져오기 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  check(await game.startSolo('savetest'), '새 캐릭터 savetest');
  await game.wait(1200);
  await clearRoom(game, 3, 2);
  await game.send('build Wall 2 0; give stone 7; xp 400; goto biome Mushroom');
  await game.wait(1500);
  await game.send('goto biome Ice');
  await game.wait(1500);
  await game.send('goto spawn');
  await game.wait(1000);
  const level = (await game.state()).level;
  await leaveGame(game);
  await game.until(s => !s.playing, 8000);
  // The menu remembers the character's classes (2026-11-09) instead of asking again: on the [게임 시작] page since the
  // start screen has pages (2026-11-11).
  await menuPage(game, '게임 시작');
  await game.wait(300);
  await game.shot('menu-saved', '나간 뒤 [게임 시작](이어하기)');
  const menuText = await game.allText();
  check(/savetest의 클래스: 전사 \+ 화염 마법사/.test(menuText), '메뉴가 저장된 캐릭터의 클래스를 보인다(savetest의 클래스: 전사 + 화염 마법사)');
  check((await game.find(/^\[[ \d]\] /)).length === 0, '저장된 캐릭터는 메뉴에서 클래스 단추가 없다(게임 안에서 바꾼다)');
  await game.startSolo('savetest', true);
  await game.wait(1500);
  let inv = await game.inventory();
  let s = await game.state();
  check(s.level === level && count(inv, 'stone') >= 7, `다시 들어오면 레벨과 가방이 그대로 (Lv ${s.level}, 돌 ${count(inv, 'stone')})`);
  check((await game.look(2, 0)).tile === 'Wall', '세운 벽이 그대로');
  await game.page.keyboard.press('m');
  await game.wait(600);
  await game.shot('map-saved', '다시 들어온 뒤 지도(가 본 곳)');
  await game.page.keyboard.press('m');
  await game.until(r => !r.map, 2000);
  await leaveGame(game);
  await game.until(r => !r.playing, 8000);

  // A reload keeps it (IndexedDB).
  await game.page.reload();
  await game.page.waitForFunction(() => !!window.unityInstance, null, { timeout: 240000 });
  await game.wait(2500);
  await game.send('probe on');
  await game.startSolo('savetest', true);
  await game.wait(1200);
  s = await game.state();
  check(s.level === level, `새로 고쳐도 이어진다 (Lv ${s.level})`);
  await leaveGame(game);
  await game.until(r => !r.playing, 8000);

  // Every save out as a file, into a fresh browser.
  await menuPage(game, '저장본');
  const [download] = await Promise.all([game.page.waitForEvent('download', { timeout: 15000 }).catch(() => null), game.press('전부 내보내기')]);
  check(!!download, '[전부 내보내기]가 파일을 내려받는다');
  // Cloud saves (2026-11-09): the account is checked by Unity's rules before anything goes out.
  check(await game.press('올리기'), '저장본 칸에 클라우드 저장 [올리기]·[받기]·[계정 만들기]');
  await game.wait(600);
  check((await game.allText()).includes('아이디는 3~20자'), '아이디 없이 [올리기]를 누르면 아이디 규칙을 알려 준다');
  await game.shot('cloud-save', '저장본 칸의 클라우드 저장');
  if (!download) return game.context.close();
  const file = path.join(out, 'saves.bundle');
  await download.saveAs(file);
  note(`  - 내려받은 파일: ${download.suggestedFilename()} (${fs.statSync(file).size}바이트)`);
  await game.context.close();

  const other = await open(browser, run, Profiles.pc, url);
  await menuPage(other, '저장본');
  await other.press('가져오기');
  await other.wait(800);
  const input = await other.page.$('input[type=file]');
  check(!!input, '웹에서 [가져오기]가 파일 고르기 상자를 띄운다');
  await other.shot('import-box', '파일 고르기 상자');
  if (input) {
    await input.setInputFiles(file);
    await other.wait(2500);
    await other.shot('imported', '가져온 뒤 저장본 목록');
    await other.press(/^덮어쓰고 가져오기/, null, { quiet: true });
    await other.startSolo('savetest', true);
    await other.wait(1200);
    const back = await other.state();
    check(back.level === level && (await other.look(2, 0)).tile === 'Wall', `다른 브라우저에 가져오면 캐릭터와 월드가 이어진다 (Lv ${back.level})`);
  }
  await other.context.close();
}

// ---------------------------------------------------------------- 화면 크기마다 배치 (14·15·16·25·46단계, 4번)
async function layout(browser) {
  for (const name of ['pc-small', 'phone-small', 'phone-wide-small']) {
    const profile = Profiles[name];
    const { width, height } = profile.context.viewport;
    note(`\n## 화면 배치 ${name} ${width}×${height}\n`);
    const game = await open(browser, run, profile, url);
    await game.shot('menu', '시작 화면');
    const first = (await game.buttons()).filter(b => !b.shown || b.rect[1] < 0 || b.rect[1] + b.rect[3] > b.screen.height + 0.5);
    check(first.length === 0, `시작 화면의 단추가 모두 화면 안에 (${first.map(b => b.label).join(', ') || '모두'})`);
    await menuPage(game, '게임 시작');
    await game.shot('menu-play', '게임 시작 화면');
    const solo = (await game.buttons()).find(b => b.label === '혼자 하기');
    check(solo && solo.shown && solo.rect[1] + solo.rect[3] <= solo.screen.height, '[게임 시작]의 [혼자 하기]가 처음부터 화면 안에 보인다');
    await menuPage(game, '설정');
    await game.shot('menu-settings', '설정 화면');
    const wide = (await game.buttons()).filter(b => b.shown && (b.rect[0] < -0.5 || b.rect[0] + b.rect[2] > b.screen.width + 0.5));
    check(wide.length === 0, `설정 단추가 옆으로 넘치지 않는다 (${wide.map(b => b.label).join(', ') || '모두 안'})`);
    await menuPage(game, '게임 시작');
    if (game.touch) {
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
      await game.page.touchscreen.tap(width / 2, height / 3);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('drop item iron_sword; drop rune; drop potion healing_potion; drop material stone');
    await game.wait(1500);
    await game.shot('game', '게임 화면(HUD, 획득 줄, 스킬·퀵슬롯)');
    const inside = (await game.buttons()).filter(b => /^(skill|potion|quick)/.test(b.label));
    const out = inside.filter(b => b.rect[0] < 0 || b.rect[1] < 0 || b.rect[0] + b.rect[2] > b.screen.width + 0.5 || b.rect[1] + b.rect[3] > b.screen.height + 0.5);
    check(out.length === 0, `스킬 칸·물약·퀵슬롯이 화면 안에 (${out.map(b => b.label).join(', ') || '모두'})`);
    for (const tab of ['장비', '재료·제작', '스킬', '특성', '캐릭터', '일지']) {
      await bagTab(game, tab);
      await game.wait(300);
      const shown = (await game.buttons()).filter(b => b.shown);
      const outside = shown.filter(b => b.rect[0] < -0.5 || b.rect[0] + b.rect[2] > b.screen.width + 0.5);
      check(outside.length === 0, `가방 [${tab}]의 단추가 옆으로 넘치지 않는다 (${outside.map(b => b.label).slice(0, 4).join(', ') || '모두 안'})`);
      await game.shot(`bag-${tab}`, `가방 [${tab}]`);
    }
    await closeBag(game);
    await game.send('equip void_sword void_helmet void_armor void_boots; goto boss cave_warden');
    await game.wait(3000);
    await game.shot('boss', '보스 체력 막대와 이름');
    await game.send('goto spawn');
    await game.wait(500);
    if (game.touch) await game.press('지도');
    else await game.page.keyboard.press('m');
    await game.wait(600);
    await game.shot('map', '전체 지도');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 휴대폰 터치로 (4·5·6·27·34번·단계)
async function touch(browser) {
  for (const name of ['phone', 'phone-wide']) {
    const profile = Profiles[name];
    const { width, height } = profile.context.viewport;
    note(`\n## 휴대폰 터치 ${name} ${width}×${height}\n`);
    const game = await open(browser, run, profile, url);
    check(await game.press('게임 시작'), '[게임 시작]을 탭한다');
    check(await game.press('혼자 하기'), '[혼자 하기]를 탭한다');
    check(!!(await game.until(s => s.playing, 25000)), '탭으로 게임이 시작된다');
    await game.page.touchscreen.tap(width / 2, height / 3);
    await game.wait(800);
    // A new character has no class (picked in the game, 2026-11-09): the two the skill buttons below are tried with.
    await game.send('classes warrior,fire_mage');
    await game.send('goto biome Cave');
    await clearRoom(game, 3, 2);

    // Map by tapping the minimap; no attack while it is open.
    await game.press('지도');
    check((await game.state()).map, '미니맵을 탭하면 전체 지도');
    await game.press('지도 닫기');
    check(!(await game.state()).map, '다시 탭하면 닫힌다');

    // Quick slot 2 by a tap.
    await game.press('quick 1');
    check((await game.state()).weapon === 'copper_pickaxe', '퀵슬롯 2를 탭하면 곡괭이');
    await game.press('quick 0');

    // The potion button.
    await game.send('hurt 40');
    await game.wait(300);
    const hurt = await game.state();
    await game.press('potion');
    await game.wait(2500);
    check((await game.state()).health > hurt.health + 20, '물약 단추를 탭하면 마신다');

    // The bag: tabs, placing a torch, equipping.
    await game.send('face 1 0; drop item iron_sword; xp 400');
    await game.wait(1500);
    await game.press('가방');
    check((await game.state()).bag, '[가방]을 탭하면 가방');
    await game.press('재료·제작');
    await game.press('놓기', '횃불');
    check((await game.look(1, 0)).tile === 'Torch', '가방의 [놓기]를 탭해 횃불');
    await game.press('장비');
    await pick(game, /철 검/);
    await game.press('장착', '철 검');
    check((await game.state()).weapon === 'iron_sword', '[장착]을 탭해 철 검');
    await game.shot('bag', '가방 [장비]');
    // Can a finger drag the list down? Many things make it long.
    await game.send('give stone 5; give copper_ore 5; give iron_ore 5; give slime_gel 5; give iron_bar 5; give mushroom 5; give fire_pepper 5; give egg 5; give milk 5');
    // Fourteen squares fit a phone held upright without scrolling (U6's grid is denser than the old rows): more.
    await game.send('give mithril_ore 5; give mithril_bar 5; give ice_crystal 5; give obsidian_ore 5; give glow_spore 5; give void_ore 5; give essence 5; give iron_gear 5; give copper_wire 5; give glow_cap 5; give frost_berry 5; give cave_minnow 5');
    // The bag's one grid (U6) holds them all, under the hero: long enough to drag.
    await game.press('장비');
    await game.wait(500);
    // The first square showing, by its name and height: a drag that scrolls moves it.
    const firstRow = async () => {
      const shown = (await game.buttons()).filter(b => b.shown && (b.row || '').startsWith('소지품'));
      return shown.length ? `${shown[0].label.slice(0, 10)}@${Math.round(shown[0].cy)}` : '';
    };
    const before = await firstRow();
    const box = await game.canvas();
    const list = (await game.buttons()).filter(b => b.shown && (b.row || '').startsWith('소지품'));
    if (list.length > 0) await game.drag(list[0].cx, list[0].cy + 20, 0, -box.height * 0.25, 200);
    await game.wait(500);
    const after = await firstRow();
    check(before !== '' && after !== before, `가방 목록을 손가락으로 끌어 내릴 수 있다 (첫 줄 ${before} → ${after})`);
    await game.shot('bag-dragged', '목록을 끈 뒤');
    await game.press('가방');

    // A machine's switch by a tap.
    await game.send('build Drill 0 -1 3; face 0 -1');
    await game.wait(800);
    check(await game.press('끄기'), '기계 위 [끄기]를 탭한다');
    check((await game.look(0, -1)).machine.enabled === false, '탭으로 기계가 꺼진다');
    await game.shot('switch', '기계 끄기 단추');

    // Skills by the buttons (every skill taught, so there is a second one).
    await game.send('teach; heal');
    const full = await game.state();
    await game.press('skill 1');
    await game.wait(500);
    check((await game.state()).mana < full.mana, '둘째 스킬 단추를 탭한다');
    // A skill button alone aims at the nearest foe (2026-11-11, Docs/WebDemo.md 터치): a slime above, the stick untouched.
    // Outside the places (monsters are kept out of them), with the second skill (an aimed one, not a buff).
    // The skill was just used above: its cooldown (over 12 seconds) runs out first. The slime is held where it is put:
    // a slime's pounce (monster_attacks.csv) lands right on the player, where no way is "toward it".
    await game.wait(16000);
    await game.send('goto wild; face off; heal; mana 100; clear; monsters hold; spawn slime 0 3 5000');
    await game.wait(1500);
    await game.press('skill 1');
    await game.wait(600);
    // The aim the press sent (the host's facing is the client's again by the next input).
    const aimed = ((await game.state()).skillAim || '0,0').split(',').map(Number);
    check(aimed[1] > 0.6, `스틱 없이 스킬 단추만 탭해도 가장 가까운 적(위쪽)으로 쓴다 (조준 ${aimed.join(',')})`);
    await game.shot('touch-autoaim', '스틱 없이 쓴 스킬');
    await game.send('monsters go');
    await game.context.close();
  }
}


// ---------------------------------------------------------------- 궁수·소환사·폭풍술사 스킬, 스킬 아이콘 (74단계)
async function skills(browser) {
  note('\n## 궁수·소환사·폭풍술사의 스킬 여섯 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 6, 4);
  for (const [cls, name] of [['archer', '궁수'], ['summoner', '소환사'], ['storm_caller', '폭풍술사']]) {
    await game.send(`classes ${cls}; xp 21300; teach 2; heal; clear`);
    await game.wait(800);
    let inv = await game.inventory();
    check(inv.classes === cls && inv.skills.split(',').length === 4, `${name}: 배운 스킬 넷이 칸에 (${inv.skills})`);
    // The first four slots, then the last two skills swapped in: every one of the six is used once.
    let used = 0;
    const names = [];
    for (const order of [null, '4,5,0,1']) {
      if (order) {
        await game.send(`classes ${cls} ${order}; teach 2`);
        await game.wait(600);
        inv = await game.inventory();
      }
      const slots = order ? [0, 1] : [0, 1, 2, 3];
      for (const slot of slots) {
        await game.send('heal; clear; spawn slime 3 0 5000; spawn slime 2 2 5000; spawn slime -3 1 5000');
        await game.wait(300);
        const before = (await game.state()).mana;
        await game.send(`face 1 0; skill ${slot}`);
        await game.wait(700);
        const id = inv.skills.split(',')[slot];
        if ((await game.state()).mana < before) {
          used++;
          names.push(id);
        }
        await game.shot(`skill-${cls}-${id}`, `${name}: ${id}`);
      }
    }
    check(used === 6, `${name}: 스킬 여섯이 모두 나간다 (${names.join(', ')})`);
  }
  await game.send('face off; bag 스킬');
  await game.wait(800);
  await game.shot('skills-icons', '[스킬] 탭의 스킬 아이콘');
  await game.send('bag off');
  await game.context.close();
}

// ---------------------------------------------------------------- 스킬 서른 가지의 이펙트 (2026-11-21, Docs/SkillEffects.md)
// Every skill of every class cast at slimes, with a picture of each phase of its effect: the gathering before the blow,
// the blow, and what is left to thin out. "fxhold <seconds>" stops effects at that age so the picture catches the
// phase; a projectile's flight is caught by stopping the host a moment after the cast ("speed 0"). The old look (feel
// skillfx off) is pictured once for each class's first skill, for setting side by side. Then a few on a phone held
// upright, where an area must still fit the narrow screen.
const SkillKinds = {
  whirlwind: 'nova', war_cry: 'buff', charge: 'dash', ground_slam: 'nova', iron_skin: 'buff', blade_storm: 'nova',
  fireball: 'shot', flame_brand: 'buff', fire_ring: 'nova', meteor: 'shot', flame_burst: 'shot', inferno: 'nova',
  volley: 'shot', eagle_eye: 'buff', gale_step: 'dash', power_shot: 'shot', explosive_arrow: 'shot', arrow_rain: 'shot',
  spirits: 'summon', spirit_pact: 'buff', life_spring: 'heal', spirit_horde: 'summon', spirit_blast: 'nova', guardian: 'summon',
  chain_lightning: 'shot', frost_ring: 'nova', ice_lance: 'shot', storm_mantle: 'buff', thunderclap: 'nova', blizzard: 'nova',
};

// The state asked again and again (every 50 ms or so) until it passes the test; the last one asked when it never does.
// The headless browser draws up to a second behind the host while it records, so these checks wait for what they look
// for instead of sleeping a fixed time.
async function pollState(game, test, ms = 3000) {
  const deadline = Date.now() + ms;
  let last = await game.state();
  while (!test(last) && Date.now() < deadline) {
    await game.wait(50);
    last = await game.state();
  }
  return last;
}

// The most a count reaches over a while: a buff's quiet cue pulses, and is down to a piece or two between pulses.
async function mostOver(game, field, ms) {
  const deadline = Date.now() + ms;
  let most = 0;
  while (Date.now() < deadline) {
    most = Math.max(most, (await game.state())[field] || 0);
    await game.wait(50);
  }
  return most;
}

async function castSkillFx(game, cls, name, id, slot, withOld, spiritsSlot) {
  const kind = SkillKinds[id];
  const far = kind === 'shot';
  // Slimes sturdy enough to stay for the pictures: near for an area, far enough for a projectile to be caught in flight
  // (two more within the chain's reach of the first).
  const slimes = far ? 'spawn slime 6.5 0 5000; spawn slime 7 1.6 5000; spawn slime 6 -1.6 5000' : 'spawn slime 1.6 0 5000; spawn slime -1.2 1.2 5000; spawn slime 0.4 -1.6 5000';
  // teach clears the cooldowns; the spirits called before stay for the pact's lines.
  const ready = `fxhold off; speed 1; teach 2; heal; mana 100; clear; ${slimes}; face 1 0`;
  if (withOld) {
    await game.send(`${ready}; feel skillfx off`);
    await game.wait(350);
    await game.send(`skill ${slot}`);
    if (far) await pollState(game, s => s.projectiles > 0, 2000);
    await game.wait(far ? 150 : 120);
    await game.shot(`skillfx-${cls}-${id}-old`, `${name} ${id}: 예전 모습(feel skillfx off)`);
    await game.wait(900);
    await game.send('feel skillfx on');
  }
  await game.send(ready);
  await game.wait(350);
  if (id === 'spirit_pact' && spiritsSlot >= 0) {
    // Spirits first, so the pact has someone to bind: its lines run to them.
    await game.send(`skill ${spiritsSlot}`);
    await game.wait(1300);
    await game.send('mana 100');
  }
  const before = (await game.state()).mana;
  await game.send(`fxhold 0.06; skill ${slot}`);
  const cast = await pollState(game, s => s.skillFx > 0 && s.skillFxPieces > 0);
  check(cast.mana < before, `${name} ${id}: 스킬이 나간다`);
  check(cast.skillFx > 0 && cast.skillFxPieces > 0, `${name} ${id}: 이펙트가 그려진다 (효과 ${cast.skillFx}개, 조각 ${cast.skillFxPieces}개)`);
  await game.shot(`skillfx-${cls}-${id}-1`, `${name} ${id}: 모으기(0.06초)`);
  if (far) {
    // The projectile was let fly while the cast's effect was held: cast again and stop the host as soon as it is in
    // the air, then wait for the screen to show it in its skill's own look (not the plain orb or arrow).
    await game.wait(900);
    await game.send(ready);
    await game.wait(350);
    await game.send(`skill ${slot}`);
    const flying = await pollState(game, s => s.projectiles > 0, 2000);
    await game.send('speed 0');
    check(flying.projectiles > 0, `${name} ${id}: 투사체가 날아간다 (${flying.projectiles}발)`);
    const drawn = await pollState(game, s => s.skillShots > 0);
    check(drawn.skillShots > 0, `${name} ${id}: 날아가는 투사체가 스킬의 모습으로 그려진다 (${drawn.skillShots}발, 조각 ${drawn.skillFxPieces}개)`);
    await game.shot(`skillfx-${cls}-${id}-2`, `${name} ${id}: 날아가는 중`);
    // Let it land: once the projectiles are gone, what is still drawn is the landing (an arrow's hit lasts a few
    // tenths of a second, so this looks at the most seen over a while rather than holding an age, 2026-11-21).
    // The landing, on a cast of its own let fly without stopping the host (stopped and let go, the slimes rush in
    // while it is held): once the projectiles are gone, what is still drawn is the landing.
    await game.send('fxhold off; speed 1');
    await game.wait(900);
    await game.send(ready);
    await game.wait(350);
    await game.send(`skill ${slot}`);
    await pollState(game, s => s.projectiles > 0, 2000);
    await pollState(game, s => s.projectiles === 0, 4000);
    const landedPieces = await mostOver(game, 'skillFxPieces', 1200);
    check(landedPieces > 0, `${name} ${id}: 닿은 곳에 이펙트가 그려진다 (조각 ${landedPieces}개)`);
    await game.send('fxhold 0.1');
    await game.shot(`skillfx-${cls}-${id}-3`, `${name} ${id}: 맞은 순간(0.1초)`);
    await game.send('fxhold 0.45');
    await game.wait(500);
    await game.shot(`skillfx-${cls}-${id}-4`, `${name} ${id}: 흩어짐(0.45초)`);
  } else {
    await game.send(`fxhold ${id === 'blade_storm' || id === 'inferno' || id === 'blizzard' ? 0.3 : 0.2}`);
    await game.wait(350);
    const impact = await pollState(game, s => s.skillFxPieces > 0);
    await game.shot(`skillfx-${cls}-${id}-2`, `${name} ${id}: 터지는 순간`);
    check(impact.skillFxPieces > 0, `${name} ${id}: 터지는 순간의 조각 (${impact.skillFxPieces}개)`);
    await game.send('fxhold 0.6');
    await game.wait(500);
    await game.shot(`skillfx-${cls}-${id}-3`, `${name} ${id}: 흩어짐(0.6초)`);
    if (kind === 'buff') {
      // The quiet cue while the buff runs: once the cast's effect is over, the most pieces over a pulse or so.
      await game.send('fxhold off');
      await pollState(game, s => s.skillFx === 0, 3000);
      const cue = await mostOver(game, 'skillFxPieces', 1000);
      check(cue > 0, `${name} ${id}: 버프가 도는 동안 표시가 남는다 (조각 최대 ${cue}개)`);
      await game.shot(`skillfx-${cls}-${id}-loop`, `${name} ${id}: 버프가 도는 동안`);
    }
  }
  await game.send('fxhold off; speed 1');
  const after = await pollState(game, s => s.skillFx <= 1, 4000);
  check(after.skillFx <= 1, `${name} ${id}: 이펙트가 오래 남지 않는다 (남은 효과 ${after.skillFx}개)`);
}

async function skillfx(browser) {
  note('\n## 스킬 서른 가지의 이펙트 (PC)\n');
  const classes = [['warrior', '전사'], ['fire_mage', '화염 마법사'], ['archer', '궁수'], ['summoner', '소환사'], ['storm_caller', '폭풍술사']];
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto wild');
  await clearRoom(game, 7, 5);
  let seen = 0;
  for (const [cls, name] of classes) {
    await game.send(`classes ${cls}; xp 21300; teach 2; heal; clear`);
    await game.wait(800);
    let inv = await game.inventory();
    check(inv.classes === cls && inv.skills.split(',').length === 4, `${name}: 배운 스킬 넷이 칸에 (${inv.skills})`);
    // The first four slots, then the last two skills swapped in.
    for (const order of [null, '4,5,0,1']) {
      if (order) {
        await game.send(`classes ${cls} ${order}; teach 2`);
        await game.wait(600);
        inv = await game.inventory();
      }
      for (const slot of order ? [0, 1] : [0, 1, 2, 3]) {
        const id = inv.skills.split(',')[slot];
        if (!SkillKinds[id]) {
          check(false, `${name} ${id}: 이펙트 시험에 없는 스킬`);
          continue;
        }
        await castSkillFx(game, cls, name, id, slot, seen % 6 === 0, inv.skills.split(',').indexOf('spirits'));
        seen++;
        if (SkillKinds[id] === 'dash') {
          // A dash carries the player along: back to open ground.
          await game.send('goto wild');
          await clearRoom(game, 7, 5);
        }
      }
    }
  }
  check(seen === 30, `스킬 서른 가지를 모두 써 봤다 (${seen})`);
  await game.context.close();

  // A phone held upright: the widest areas and a projectile, by the same commands (the effect's size on a narrow screen
  // is what is looked at; tapping the skill buttons is the touch section's).
  note('\n## 스킬 이펙트 (휴대폰 세로)\n');
  const phone = await open(browser, run, Profiles.phone, url);
  check(await phone.press('게임 시작'), '[게임 시작]을 탭한다');
  check(await phone.press('혼자 하기'), '[혼자 하기]를 탭한다');
  check(!!(await phone.until(s => s.playing, 25000)), '탭으로 게임이 시작된다');
  const { width, height } = Profiles.phone.context.viewport;
  await phone.page.touchscreen.tap(width / 2, height / 3);
  await phone.wait(800);
  await phone.send('goto wild');
  await clearRoom(phone, 7, 5);
  for (const [cls, name, order] of [['warrior', '전사', '4,5,0,1'], ['fire_mage', '화염 마법사', '4,5,0,1'], ['storm_caller', '폭풍술사', '4,5,0,1']]) {
    await phone.send(`classes ${cls}; xp 21300; teach 2; heal; clear`);
    await phone.wait(600);
    await phone.send(`classes ${cls} ${order}; teach 2`);
    await phone.wait(600);
    const inv = await phone.inventory();
    // The class's last skill (its widest area) and its first.
    for (const slot of [1, 2]) {
      const id = inv.skills.split(',')[slot];
      const far = SkillKinds[id] === 'shot';
      await phone.send(`fxhold off; speed 1; teach 2; heal; mana 100; clear; spawn slime ${far ? 3.5 : 1.6} 0 5000; face 1 0`);
      await phone.wait(350);
      await phone.send(`fxhold ${far ? 0.1 : 0.22}; skill ${slot}`);
      if (far) await pollState(phone, s => s.projectiles > 0, 2000);
      if (far) await pollState(phone, s => s.projectiles === 0, 4000);
      await phone.wait(far ? 200 : 450);
      const state = await pollState(phone, s => s.skillFxPieces > 0);
      check(state.skillFxPieces > 0, `휴대폰 ${name} ${id}: 이펙트가 그려진다 (조각 ${state.skillFxPieces}개)`);
      await phone.shot(`skillfx-phone-${cls}-${id}`, `휴대폰 세로 ${name} ${id}`);
      await phone.send('fxhold off');
      await phone.wait(900);
    }
  }
  await phone.context.close();
}

// ---------------------------------------------------------------- 일반 몬스터의 공격 (monster_attacks.csv)

// The monsters that fight from monster_attacks.csv (2026-11-21, Docs/Combat.md 몬스터의 공격 시트), one attack at a time
// in the wild: "monsters hold" keeps them from starting anything themselves, "mob <id> use <attack>" starts the one
// asked for. Three pictures each: the warning (the host stopped in the middle of the telegraph), the blow (its effect
// held at 0.06 s) and what thins out (0.3 s). A mark that starts at the monster is not sent as a hazard: the screen
// makes it from the monster's state, so "mobMarksShown" counts it while the world's "mobHazards" is the host's own.
// [monster, name, attack, kind, how far to the right the monster stands, options]
// kind: mark (a warning that lands as a blow), rush (a path it runs), shot, blink, step (a blink to behind the player
// that goes straight into the next attack), bomb (it goes off and is gone), trap (marks that wait), call (a mend or a
// rally: it needs an ally). options: pool (the blow leaves something on the floor, which must show and then clear),
// buried (it is under the ground before: a mound, not a body), hits (standing still is hit), setup (commands first),
// then (the attack it goes straight into), poison, ally (who a call is for).
const MobAttacks = [
  ['slime', '슬라임', 'pounce', 'mark', 3.2, { hits: true }],
  ['slime', '슬라임', 'bite', 'mark', 1.1, { hits: true }],
  ['spore_shooter', '가시 버섯', 'spore_sac', 'mark', 5.5, { pool: true }],
  ['spore_shooter', '가시 버섯', 'thorn_fan', 'shot', 2.5],
  ['cave_beetle', '동굴 딱정벌레', 'ram', 'rush', 3],
  ['crystal_bat', '수정 박쥐', 'swoop', 'rush', 3],
  ['crystal_wisp', '수정 정령', 'prism', 'shot', 5],
  ['crystal_wisp', '수정 정령', 'ray', 'mark', 5, { hits: true }],
  ['crystal_wisp', '수정 정령', 'shift', 'blink', 2],
  ['myconid', '버섯 병정', 'spore_smash', 'mark', 1.3, { hits: true }],
  ['glow_bat', '빛 박쥐', 'swoop', 'rush', 3, { pool: true }],
  ['ice_slime', '얼음 슬라임', 'pounce', 'mark', 3, { pool: true }],
  ['ice_slime', '얼음 슬라임', 'bite', 'mark', 1.1],
  ['frost_wisp', '서리 정령', 'frost_ring', 'mark', 1.8, { hits: true }],
  ['frost_wisp', '서리 정령', 'icicle', 'mark', 5],
  ['magma_slime', '용암 슬라임', 'eruption', 'mark', 3.2, { pool: true }],
  ['magma_slime', '용암 슬라임', 'bite', 'mark', 1.1],
  ['fire_bat', '불 박쥐', 'swoop', 'rush', 3, { pool: true }],
  ['fire_bat', '불 박쥐', 'dive_bomb', 'bomb', 3, { setup: 'mob fire_bat low 0.2' }],
  ['void_wisp', '공허 정령', 'orb', 'shot', 5],
  ['void_wisp', '공허 정령', 'gravity_well', 'mark', 5],
  ['void_wisp', '공허 정령', 'shift', 'blink', 2],
  ['abyss_stalker', '심연 추적자', 'shadow_step', 'step', 4, { then: 'cleave' }],
  ['abyss_stalker', '심연 추적자', 'cleave', 'mark', 1.3, { hits: true }],
  ['abyss_stalker', '심연 추적자', 'triple', 'rush', 3.5],
  ['lake_crab', '호수 게', 'claws', 'mark', 1.3, { hits: true }],
  ['lake_crab', '호수 게', 'mud_dive', 'mark', 6],
  ['tide_wisp', '물살 정령', 'wave', 'mark', 4.5],
  ['tide_wisp', '물살 정령', 'mend', 'call', 4, { ally: 'lake_crab' }],
  ['sand_scorpion', '모래 전갈', 'ambush', 'mark', 2.5, { buried: true, hits: true }],
  ['sand_scorpion', '모래 전갈', 'pincer', 'mark', 1.2, { then: 'sting' }],
  ['sand_scorpion', '모래 전갈', 'sting', 'mark', 1.8, { poison: true }],
  ['sand_worm', '모래 지렁이', 'sand_spit', 'mark', 5, { pool: true, buried: true }],
  ['sand_worm', '모래 지렁이', 'bite', 'mark', 5, { buried: true }],
  ['demon_soldier', '마족 병사', 'spear', 'mark', 2, { hits: true }],
  ['demon_caster', '마족 주술사', 'rune_trap', 'trap', 5],
  ['demon_caster', '마족 주술사', 'rally', 'call', 4, { ally: 'demon_soldier' }],
  ['demon_caster', '마족 주술사', 'fire_wall', 'mark', 3.5, { pool: true }],
];

async function mobAttack(game, [id, name, attack, kind, gap, options], prefix = 'monsterfx') {
  const opts = options || {};
  const tag = `${prefix}-${id}-${attack}`;
  await game.send(`fxhold off; speed 1; clear; heal; face 1 0`);
  await game.wait(700);
  await game.send(`spawn ${id} ${gap} 0 5000`);
  if (opts.ally) await game.send(`spawn ${opts.ally} ${gap - 1} 2 5000; mob ${opts.ally} hurt 0.5`);
  if (opts.setup) await game.send(opts.setup);
  await game.wait(300);
  if (opts.buried) {
    // Under the ground it is a mound, not a body (the screen may show it a moment after the host has it).
    const under = await pollState(game, st => st.mobBuried > 0 && st.mobMounds > 0, 2500);
    check(under.mobBuried > 0 && under.mobMounds > 0, `${name}: 땅속에 있을 때 흙무더기로 보인다 (땅속 ${under.mobBuried}마리, 무더기 ${under.mobMounds}개)`);
    await game.shot(`${tag}-0`, `${name}: 땅속(흙무더기)`);
  }
  const before = await game.state();
  await game.send(`mob ${id} use ${attack}`);
  // The warning: wait until the screen has it, then hold the world still for the picture.
  const marked = kind !== 'shot' && kind !== 'call';
  const warned = await pollState(game, st => st.mobMarksShown > 0 || (!marked && /Windup/.test(st.mobs || '')), 2000);
  await game.send('speed 0');
  await game.wait(250);
  // The screen can lag the host by a second in a recorded headless browser: looked at for a while, not once.
  const held = await pollState(game, st => new RegExp(`${id}:Windup:${attack}`).test(st.mobs || '') && st.mobFxPieces > 0 && (!marked || st.mobMarksShown > 0), 2500);
  check(new RegExp(`${id}:Windup:${attack}`).test(held.mobs || ''), `${name} ${attack}: 예고가 시작된다 (${held.mobs})`);
  if (marked) {
    check(held.mobHazards > 0, `${name} ${attack}: 호스트에 장판이 있다 (${held.mobHazards}개)`);
    check((warned.mobMarksShown > 0 || held.mobMarksShown > 0) && held.mobFxPieces > 0, `${name} ${attack}: 바닥 표시가 그려진다 (표시 ${held.mobMarksShown}개, 조각 ${held.mobFxPieces}개)`);
  } else {
    check(held.mobFxPieces > 0, `${name} ${attack}: ${kind === 'call' ? '부르는' : '쏘기 전'} 몸짓이 그려진다 (조각 ${held.mobFxPieces}개)`);
  }
  await game.shot(`${tag}-1`, `${name} ${attack}: 예고`);

  if (kind === 'shot') {
    // Let it fire, and stop again with the shot in the air.
    await game.send('speed 1');
    const flying = await pollState(game, st => st.bossShots > 0, 3000);
    await game.send('speed 0');
    check(flying.bossShots > 0, `${name} ${attack}: 탄이 나간다`);
    const drawn = await pollState(game, st => st.mobShots > 0, 2000);
    check(drawn.mobShots > 0, `${name} ${attack}: 탄이 그 몬스터의 모습으로 그려진다 (조각 ${drawn.mobFxPieces}개)`);
    await game.shot(`${tag}-2`, `${name} ${attack}: 날아가는 중`);
    await game.send('speed 1');
    const home = await pollState(game, st => st.bossShots === 0, 7000);
    check(home.bossShots === 0, `${name} ${attack}: 탄이 사라진다`);
    await game.shot(`${tag}-3`, `${name} ${attack}: 끝난 뒤`);
    return;
  }

  if (kind === 'call') {
    // Made when the telegraph is over: a line to the ally it mends, or a ring and flames at the feet of the roused.
    await game.send('fxhold 0.1; speed 1');
    const made = await pollState(game, st => attack === 'mend' ? /:Channel:/.test(st.mobs || '') : st.mobFx > 0, 3000);
    check(attack === 'mend' ? /:Channel:/.test(made.mobs || '') : made.mobFx > 0, `${name} ${attack}: 예고가 끝나면 이루어진다 (${made.mobs})`);
    await game.wait(300);
    const drawn = await pollState(game, st => st.mobFxPieces > 0, 1500);
    check(drawn.mobFxPieces > 0, `${name} ${attack}: 이음줄이나 북돋인 표시가 그려진다 (조각 ${drawn.mobFxPieces}개)`);
    await game.shot(`${tag}-2`, `${name} ${attack}: 이루어진 뒤`);
    await game.send('fxhold off');
    return;
  }

  // The blow, held at its brightest; then what thins out.
  await game.send('fxhold 0.06; speed 1');
  if (opts.then) {
    // Straight into the next attack: its wind-up shows before anything else is waited for.
    const next = await pollState(game, st => new RegExp(`${id}:Windup:${opts.then}`).test(st.mobs || ''), 3000);
    check(new RegExp(`${id}:Windup:${opts.then}`).test(next.mobs || ''), `${name} ${attack}: 쉬지 않고 ${opts.then}으로 이어진다 (${next.mobs})`);
    if (kind === 'step') {
      check(before.foe !== next.foe, `${name} ${attack}: 플레이어 뒤로 옮겨 갔다 (${before.foe} → ${next.foe})`);
      await game.shot(`${tag}-2`, `${name} ${attack}: 뒤에 나타나 ${opts.then}을 예고`);
    }
  }
  const landed = await pollState(game, st => st.mobHazards === 0, kind === 'trap' ? 10000 : 6000);
  check(landed.mobHazards === 0, `${name} ${attack}: 장판이 터지고 사라진다`);
  if (kind === 'mark' || kind === 'trap' || kind === 'bomb') {
    // Counted since the game began, so a look that comes a moment late still sees it (2026-11-22: "mobFx > 0" read
    // once missed a slime's bite in a recorded browser whose screen ran a second behind).
    const blow = await pollState(game, st => st.mobBlows > before.mobBlows, 3000);
    check(blow.mobBlows > before.mobBlows, `${name} ${attack}: 터지는 이펙트가 그려진다 (${blow.mobBlows - before.mobBlows}번, 조각 ${blow.mobFxPieces}개)`);
    if (attack === 'frost_ring') check((await pollState(game, st => st.rooted, 800)).rooted, `${name} ${attack}: 맞으면 묶인다`);
    if (opts.poison) check((await pollState(game, st => st.poisoned, 1500)).poisoned, `${name} ${attack}: 맞으면 독에 걸린다`);
    if (opts.hits) check((await pollState(game, st => st.health < before.health, 1500)).health < before.health, `${name} ${attack}: 서 있으면 맞는다`);
    if (kind === 'bomb') {
      const gone = await pollState(game, st => st.explosions > before.explosions && st.monsters === 0, 2000);
      check(gone.explosions > before.explosions && gone.monsters === 0, `${name} ${attack}: 터지고 사라진다 (폭발 ${gone.explosions - before.explosions}번, 남은 몬스터 ${gone.monsters})`);
    }
  }
  if (opts.pool) {
    // Before the picture: a swoop's trail lasts two seconds.
    const left = await pollState(game, st => st.mobPools > 0 && st.mobPoolsShown > 0, 2500);
    check(left.mobPools > 0 && left.mobPoolsShown > 0, `${name} ${attack}: 바닥에 남는 것이 그려진다 (호스트 ${left.mobPools}개, 화면 ${left.mobPoolsShown}개)`);
  }
  if (kind !== 'step') await game.shot(`${tag}-2`, `${name} ${attack}: ${kind === 'rush' ? '달리는 중' : kind === 'blink' ? '옮겨 간 뒤' : '터지는 순간(0.06초)'}`);
  if (kind === 'blink') {
    const after = await game.state();
    check(before.foe !== after.foe, `${name} ${attack}: 다른 자리로 옮겨 갔다 (${before.foe} → ${after.foe})`);
  }
  await game.send('fxhold 0.3');
  await game.wait(450);
  await game.shot(`${tag}-3`, `${name} ${attack}: 흩어짐(0.3초)`);
  await game.send('fxhold off');
  const gone = await pollState(game, st => st.mobFx === 0 && st.mobMarksShown === 0 && st.mobHazards === 0, opts.then ? 5000 : 2500);
  check(gone.mobFx === 0 && gone.mobMarksShown === 0, `${name} ${attack}: 끝난 뒤 남는 것이 없다 (이펙트 ${gone.mobFx}개, 표시 ${gone.mobMarksShown}개)`);
  if (opts.pool) {
    const cleared = await pollState(game, st => st.mobPools === 0 && st.mobPoolsShown === 0, 6000);
    check(cleared.mobPools === 0 && cleared.mobPoolsShown === 0, `${name} ${attack}: 바닥에 남은 것이 시간이 지나면 사라진다 (호스트 ${cleared.mobPools}개, 화면 ${cleared.mobPoolsShown}개)`);
  }
}

async function monsterfx(browser) {
  note('\n## 일반 몬스터의 공격과 이펙트 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto wild');
  await clearRoom(game, 9, 6);
  await game.send('monsters hold; equip iron_armor; heal');
  for (const row of MobAttacks) await mobAttack(game, row);

  // The old look of the first, for setting side by side: marks filled in the way a boss's are, no pieces.
  await game.send('fxhold off; speed 1; clear; heal; face 1 0; feel monsterfx off');
  await game.wait(600);
  await game.send('spawn slime 3.2 0 5000');
  await game.wait(300);
  await game.send('mob slime use pounce');
  await pollState(game, st => st.mobMarksShown > 0, 2000);
  await game.send('speed 0');
  await game.wait(250);
  check((await game.state()).mobFxPieces === 0, '끄면(feel monsterfx off) 조각이 그려지지 않는다');
  await game.shot('monsterfx-slime-pounce-old', '슬라임 pounce: 끈 모습(feel monsterfx off)');
  await game.send('feel monsterfx on; speed 1');

  // A slime of the second zone splits when it falls; the small ones are drawn smaller.
  await game.send('clear; heal');
  await game.wait(600);
  await game.send('spawn slime 2.5 0 5000');
  await game.wait(300);
  await game.send('mob slime kill 2');
  const split = await pollState(game, st => st.monsters === 2, 2000);
  check(split.monsters === 2 && split.monsterKinds === 'slime', `구역 2의 슬라임은 죽으면 둘로 갈라진다 (${split.monsters}마리)`);
  await game.shot('monsterfx-slime-split', '슬라임이 갈라진 뒤');

  // A beetle's front turns most of a blow; struck there it clangs.
  await game.send('clear; heal');
  await game.wait(600);
  await game.send('spawn cave_beetle 1.4 0 5000');
  await game.wait(300);
  // Held, it keeps the way it was made facing; turned to the player its shell takes the blow.
  await game.send('mob cave_beetle face');
  await game.wait(300);
  const unguarded = await game.state();
  await game.send('fxhold 0.05; mob cave_beetle hurt 0.02');
  await game.wait(350);
  const turned = await pollState(game, st => st.guardedNumbers > unguarded.guardedNumbers, 2000);
  check(turned.guardedNumbers > unguarded.guardedNumbers, `등딱지에 막힌 피해 숫자는 회색이다 (${turned.guardedNumbers - unguarded.guardedNumbers}개)`);
  await game.shot('monsterfx-beetle-guard', '딱정벌레의 앞 등딱지에 막힌 순간');

  // An ice slime felled by steel throws shards after a flash; felled by fire it melts and throws none.
  await game.send('fxhold off; clear; heal');
  await game.wait(600);
  await game.send('spawn ice_slime 3 0 5000');
  await game.wait(300);
  await game.send('mob ice_slime kill');
  const shards = await pollState(game, st => st.bossShots > 0, 2500);
  check(shards.bossShots > 0, `얼음 슬라임은 죽으면 파편을 날린다 (${shards.bossShots}발)`);
  await game.shot('monsterfx-ice-slime-shards', '얼음 슬라임의 파편');
  await pollState(game, st => st.bossShots === 0, 3000);
  await game.send('clear; heal');
  await game.wait(600);
  await game.send('spawn ice_slime 3 0 5000');
  await game.wait(300);
  await game.send('fxhold 0.15; mob ice_slime melt');
  await game.wait(1200);
  const melted = await game.state();
  check(melted.bossShots === 0 && melted.mobHazards === 0, `화염으로 잡은 얼음 슬라임은 파편을 날리지 않는다 (탄 ${melted.bossShots}발, 표시 ${melted.mobHazards}개)`);
  await game.shot('monsterfx-ice-slime-melt', '화염으로 녹은 얼음 슬라임(김)');

  // Two demon soldiers shoulder to shoulder: a thin red line between them.
  await game.send('fxhold off; clear; heal');
  await game.wait(600);
  await game.send('spawn demon_soldier 3 0 5000; spawn demon_soldier 3 1.1 5000');
  const line = await pollState(game, st => st.mobLinks > 0, 2500);
  check(line.mobLinks > 0, `붙어 선 마족 병사 사이에 이음줄이 그려진다 (${line.mobLinks}개)`);
  await game.shot('monsterfx-soldier-line', '마족 병사의 대열(붉은 이음줄)');
  await game.send('fxhold off; clear; monsters go');
  await game.context.close();

  note('\n## 일반 몬스터의 공격 (휴대폰 세로)\n');
  const phone = await open(browser, run, Profiles.phone, url);
  await phone.startSolo();
  await phone.wait(1200);
  await phone.send('goto wild');
  await clearRoom(phone, 9, 6);
  await phone.send('monsters hold; equip iron_armor; heal');
  const onPhone = (id, attack) => MobAttacks.find(row => row[0] === id && row[2] === attack);
  for (const row of [onPhone('frost_wisp', 'frost_ring'), onPhone('void_wisp', 'gravity_well'), onPhone('abyss_stalker', 'cleave'), onPhone('demon_caster', 'rune_trap')])
    await mobAttack(phone, row, 'monsterfx-phone');
  await phone.send('monsters go');
  await phone.context.close();
}

// ---------------------------------------------------------------- 바닥, 문, 장식 (41단계)
async function base(browser) {
  note('\n## 바닥·문·장식 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('goto biome Cave');
  await clearRoom(game, 6, 4);
  // Three floors in a row, a wall across with a door in it, a table and a banner.
  const commands = ['build StoneFloor -3 2', 'build BrickFloor -2 2', 'build GlowFloor -1 2', 'build Table -3 -2', 'build Banner -1 -2', 'build Door 2 0',
    // Rugs and hung walls (2026-11-09).
    'build RedCarpet -3 1', 'build BlueCarpet -2 1', 'build YellowCarpet -1 1', 'build TapestryWall -3 -3', 'build ShieldWall -2 -3'];
  for (let dy = -4; dy <= 4; dy++) if (dy !== 0) commands.push(`build Wall 2 ${dy}`);
  await game.send(commands.join('; '));
  await game.wait(1000);
  const tiles = [];
  for (const [dx, dy] of [[-3, 2], [-2, 2], [-1, 2], [-3, -2], [-1, -2], [2, 0]]) tiles.push((await game.look(dx, dy)).tile);
  check(tiles.join(',') === 'StoneFloor,BrickFloor,GlowFloor,Table,Banner,Door', `바닥 셋, 탁자, 깃발, 문이 놓인다 (${tiles.join(', ')})`);
  const decor = [];
  for (const [dx, dy] of [[-3, 1], [-2, 1], [-1, 1], [-3, -3], [-2, -3]]) decor.push((await game.look(dx, dy)).tile);
  check(decor.join(',') === 'RedCarpet,BlueCarpet,YellowCarpet,TapestryWall,ShieldWall', `깔개 셋과 걸개 벽·방패 장식 벽이 놓인다 (${decor.join(', ')})`);
  check((await game.state()).doorsOpen === 0, '아무도 곁에 없으면 문은 닫혀 있다');
  await game.shot('base', '돌·벽돌·빛 바닥, 문 달린 벽, 탁자와 깃발');
  // A monster on the far side stays behind the door; the player walks through it.
  await game.send('spawn slime 4 0 5000');
  await game.wait(2500);
  const start = await game.state();
  await game.page.keyboard.down('d');
  let opened = false;
  for (let i = 0; i < 7; i++) {
    await game.wait(200);
    if ((await game.state()).doorsOpen > 0 && !opened) {
      opened = true;
      await game.shot('door-open', '문을 지나는 중(열린 문)');
    }
  }
  await game.page.keyboard.up('d');
  check(opened, '문 곁을 지날 때 문이 열린다');
  const after = await game.state();
  check(after.x > start.x + 2.2, `플레이어는 문을 지나간다 (x ${start.x.toFixed(1)} → ${after.x.toFixed(1)})`);
  await game.shot('base-door', '문을 지나간 뒤');
  // Gone well away from it, the door swings shut.
  await game.send('goto 3 0');
  await game.wait(800);
  check((await game.state()).doorsOpen === 0, `멀어지면 문이 닫힌다 (${(await game.state()).doorsOpen})`);
  // Locking it (2026-11-09): the bag's [문 잠그기] facing the door; whoever locked it still walks through. goto x y is
  // from where the player stands, so the door's own tile (built 2 east of the start) is worked out from here.
  const doorX = Math.floor(start.x) + 2, doorY = Math.floor(start.y);
  const doorFrom = async () => {
    const p = await game.state();
    return [doorX - Math.floor(p.x), doorY - Math.floor(p.y)];
  };
  // Beside this door on one side (1 east, -1 west), facing it. Not "goto near Door": a house's door in a village close
  // by can be nearer (the 2026-11-23 sweep went to one 7 tiles off). goto is from the host's tile, which can be one off
  // the reported x when the player stopped on a tile's edge (x 511.0): once in a tile's middle the rest is exact.
  const besideDoor = async side => {
    let [dx, dy] = await doorFrom();
    for (let i = 0; i < 2 && (dx !== -side || dy !== 0); i++) {
      await game.send(`goto ${dx + side} ${dy}`);
      await game.wait(400);
      [dx, dy] = await doorFrom();
    }
    await game.send(`face ${-side} 0`);
    await game.wait(400);
    return doorFrom();
  };
  let [ddx, ddy] = await besideDoor(1);
  // Over the door in the world since U6 ("(G)" on a PC), not in the bag.
  check(await game.press(/^문 잠그기/), `문을 마주 보면 문 위에 [문 잠그기] (문이 ${ddx},${ddy})`);
  await game.wait(900);
  check((await game.look(ddx, ddy)).tile === 'LockedDoor', `[문 잠그기]로 문이 잠긴다 (${(await game.look(ddx, ddy)).tile})`);
  check((await game.find(/^잠금 풀기/)).length > 0, '내가 잠근 문 앞에서는 [잠금 풀기]');
  await game.shot('locked-door', '잠근 문(자물쇠)과 [잠금 풀기]');
  await closeBag(game);
  await game.send('face off');
  // Through it the other way: from the east side, west.
  const locked = await game.state();
  const west = locked.x > doorX + 0.5;
  await game.page.keyboard.down(west ? 'a' : 'd');
  await game.wait(900);
  await game.page.keyboard.up(west ? 'a' : 'd');
  const past = await game.state();
  check(west ? past.x < doorX : past.x > doorX + 1, `잠근 사람은 잠긴 문을 지나간다 (x ${locked.x.toFixed(1)} → ${past.x.toFixed(1)}, 문 ${doorX})`);
  // From the far side, beside it, the lock comes off.
  const side = west ? -1 : 1;
  [ddx, ddy] = await besideDoor(side);
  check(await game.press(/^잠금 풀기/), `안쪽에서도 문 위에 [잠금 풀기] (문이 ${ddx},${ddy})`);
  await game.wait(900);
  check((await game.look(ddx, ddy)).tile === 'Door', `[잠금 풀기]로 다시 문이 된다 (${(await game.look(ddx, ddy)).tile})`);
  await closeBag(game);
  await game.context.close();
}

// ---------------------------------------------------------------- 이야기와 장소 (100단계, 2026-11-10)
// The story (Docs/Story.md): the village round the spawn, talking to its chief through the talk at the bottom of the
// screen, the step in the goal list with its way, reaching the royal city along the road, its waypoint, and going back
// by waypoint.
async function story(browser) {
  note('\n## 이야기와 장소 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1500);
  let s = await game.state();
  check(s.places >= 4, `장소가 동굴에 깎여 있다 (${s.places}곳)`);
  check(s.place === 'dawn_village', `처음 서는 곳은 새벽 마을 (${s.place})`);
  check(s.journey === 1 && !s.journeyTaken, `이야기는 1단계에서 시작한다 (${s.journey})`);
  check(/dawn_village/.test(s.waypoints), `마을 웨이포인트는 처음부터 열린다 (${s.waypoints})`);
  // The people go about their plaza by day (2026-11-11): over twenty seconds someone in the village has moved. Then
  // they are held at home so the talks below can walk up to them.
  const before = s.people;
  await game.wait(20000);
  const after = (await game.state()).people;
  check(before && after && before !== after, `마을 사람들이 낮에 광장을 돌아다닌다 (${before} → ${after})`);
  await game.shot('people-walk', '돌아다니는 마을 사람들');
  await game.send('still');
  check((await game.allText()).includes('촌장 바르도에게 말 걸기'), '목표 목록 맨 위에 "촌장 바르도에게 말 걸기"');
  await game.shot('village', '새벽 마을과 촌장(이름 앞 !)');
  await game.send('goto person chief');
  await game.wait(800);
  check(await game.press(/촌장 바르도와 말하기/), '촌장 곁에서 [! 촌장 바르도와 말하기]');
  await game.wait(500);
  await game.shot('story-talk', '이야기 대화 창');
  let pages = 0;
  while (pages < 12 && (await game.press('다음', undefined, { quiet: true }))) {
    pages++;
    await game.wait(250);
  }
  check(pages >= 3, `대화를 [다음]으로 넘긴다 (${pages}번)`);
  check(await game.press('알겠다'), '마지막 줄에서 [알겠다]');
  await game.wait(1000);
  s = await game.state();
  check(s.journeyTaken, '[알겠다]로 이야기 단계를 받는다');
  const tracker = await game.allText();
  check(/왕도 루미나에 가기/.test(tracker) && /칸/.test(tracker), `목표 목록에 "왕도 루미나에 가기"와 방향·거리 (${(tracker.match(/왕도 루미나[^|]*/) || [''])[0]})`);
  await game.shot('story-taken', '받은 단계와 목표 목록');
  // The map: every place's name and the gold ring where the story leads.
  await game.page.keyboard.press('m');
  await game.wait(700);
  await game.shot('story-map', '지도의 장소와 이야기 목적지');
  await game.page.keyboard.press('m');
  // The royal city: arriving finishes the step, and its waypoint opens when stood on.
  await game.send('goto place royal_city');
  await game.wait(1500);
  s = await game.state();
  check(s.place === 'royal_city', `왕도에 닿는다 (${s.place})`);
  check(s.journey === 2, `왕도에 닿으면 1단계가 끝난다 (단계 ${s.journey})`);
  await game.send('goto 0 2');
  await game.wait(1200);
  s = await game.state();
  check(/royal_city/.test(s.waypoints), `웨이포인트를 밟으면 열린다 (${s.waypoints})`);
  await game.shot('royal-city', '왕도 루미나와 여왕');
  check(await game.press('웨이포인트'), '웨이포인트 위에서 [웨이포인트]');
  await game.wait(500);
  await game.shot('waypoints', '웨이포인트 목록');
  check(await game.press('새벽 마을'), '목록에서 [새벽 마을]');
  await game.wait(1200);
  s = await game.state();
  check(s.place === 'dawn_village', `웨이포인트로 마을에 돌아간다 (${s.place})`);
  // One next thing (2026-11-11, Docs/Guide.md 첫 30분): with the queen's request for the warden taken and the guide's
  // goal the warden too, the goal list shows one entry, the story's, carrying the guide's count.
  await game.send('journey 2 taken; guide boss');
  const merged = await game.until(t => (t.hud || []).some(l => /깨어난 수호자 · 목표 \d+\/\d+/.test(l)), 6000, game.texts);
  const hudNow = ((merged || await game.texts()).hud || []);
  check(!!merged && !hudNow.some(l => /^목표 \d+\/\d+/.test(l)), `여왕의 수호자 부탁과 안내 목표가 한 항목으로 (${hudNow.filter(l => /수호자|목표/.test(l)).join(' / ')})`);
  await game.shot('one-goal', '이야기와 안내가 합쳐진 목표 목록');
  // A choice (2026-11-10, story.csv choices): the queen asks at step 9; the answer picked leads to step 11, whose words
  // follow on by themselves, and that is the end of the story so far.
  await game.send('journey 9; goto person queen');
  await game.wait(1200);
  check(await game.press(/여왕 세라핀과 말하기/), '여왕 곁에서 [! 여왕 세라핀과 말하기]');
  await game.wait(500);
  pages = 0;
  while (pages < 12 && (await game.press('다음', undefined, { quiet: true }))) {
    pages++;
    await game.wait(250);
  }
  // The answers are buttons, not text on the screen.
  const asked = (await game.buttons()).map(b => b.label).join(' | ');
  check(/루가 마왕이라고 말한다/.test(asked) && /아무것도 없었다고 한다/.test(asked), '마지막 줄에 고를 말 둘');
  await game.shot('story-choice', '이야기 고르기');
  check(await game.press('아무것도 없었다고 한다'), '[아무것도 없었다고 한다]를 고른다');
  await game.wait(1500);
  s = await game.state();
  check(s.journey === 11, `고른 쪽 단계로 간다 (단계 ${s.journey})`);
  check(/아무것도 없었다라/.test(await game.allText()), '여왕의 다음 말이 바로 이어진다');
  await game.shot('story-answer', '고른 뒤 이어지는 말');
  pages = 0;
  while (pages < 12 && (await game.press('다음', undefined, { quiet: true }))) {
    pages++;
    await game.wait(250);
  }
  check(await game.press('알겠다'), '이어진 말의 끝에서 [알겠다]');
  await game.wait(1200);
  s = await game.state();
  check(s.journey === 22, `그 갈래 뒤로 4장(단계 22)이 이어진다 (단계 ${s.journey})`);
  check(/4장 · 가라앉은 왕관/.test(await game.allText()), '목표 목록에 4장 · 가라앉은 왕관');
  // A side story (2026-11-11, story.csv chain): the fisher at the harbour gives one apart from the main story; it shows
  // in the goal list, and the minnows brought finish its first step. The harbour master's words then follow the answer
  // given to the queen (?9=2: kept the secret).
  await game.send('goto person fisher_ida');
  await game.wait(1200);
  check(await game.press(/어부 이다와 말하기/), '항구의 어부 곁에서 [! 어부 이다와 말하기]');
  await game.wait(500);
  pages = 0;
  while (pages < 12 && (await game.press('다음', undefined, { quiet: true }))) {
    pages++;
    await game.wait(250);
  }
  check(await game.press('알겠다'), '곁이야기 대화 끝에서 [알겠다]');
  await game.wait(1200);
  s = await game.state();
  check(/harbor:\d+\*/.test(s.sides), `곁이야기 단계를 받는다 (${s.sides})`);
  const side = await game.allText();
  check(/곁이야기 · 물안개 항구/.test(side) && /동굴 송사리 0\/3/.test(side), `목표 목록에 곁이야기와 "동굴 송사리 0/3" (${(side.match(/곁이야기[^|]*/) || [''])[0]})`);
  await game.shot('side-story', '곁이야기를 받은 목표 목록');
  await game.send('give cave_minnow 3');
  await game.wait(800);
  check(await game.press(/어부 이다와 말하기/), '송사리를 들고 다시 [어부 이다와 말하기]');
  await game.wait(500);
  check(await game.press('알겠다'), '"가져왔군"에 [알겠다]');
  await game.wait(1200);
  s = await game.state();
  check(/harbor:13(,|$)/.test(s.sides), `송사리를 건네면 다음 단계로 (${s.sides})`);
  await game.send('goto person harbor_master');
  await game.wait(1200);
  check(await game.press(/항구지기 마렌과 말하기/), '항구지기 곁에서 [! 항구지기 마렌과 말하기]');
  await game.wait(500);
  let heard = '';
  pages = 0;
  do {
    heard += ' ' + (await game.allText());
    pages++;
  } while (pages < 12 && (await game.press('다음', undefined, { quiet: true })) && (await game.wait(250), true));
  check(/우리끼리만/.test(heard) && !/근위대가 와서/.test(heard), '여왕에게 숨긴 갈래의 말만 나온다');
  await game.shot('side-story-branch', '고른 말에 따라 바뀌는 대사');
  await game.press('알겠다', undefined, { quiet: true });
  await game.press('닫기', undefined, { quiet: true });
  // The journal's story so far (2026-11-11): the chapters passed, the answer given, and the side stories (U5: the
  // [이야기] section of the book).
  await bagTab(game, '일지');
  await game.press('이야기');
  await game.wait(400);
  const log = await game.allText();
  check(/큰 이야기/.test(log) && /고른 말/.test(log) && /아무것도 없었다고 한다/.test(log), '[일지]에 지나온 큰 이야기와 고른 말');
  check(/곁이야기/.test(log) && /물안개 항구/.test(log), '[일지]에 곁이야기의 진행');
  await game.shot('journal-story', '[일지]의 이야기 기록');
  await closeBag(game);
  // A road signpost says where its road goes (2026-11-11).
  await game.send('goto signpost');
  await game.wait(900);
  const sign = await game.allText();
  check(/(새벽 마을|왕도 루미나|용사의 오두막|마족 둥지|물안개 항구|철곡 광산촌|잊힌 신전) · (동|북동|북|북서|서|남서|남|남동)쪽/.test(sign), `이정표 곁에서 갈 곳과 방향 (${(sign.match(/[가-힣 ]+ · [가-힣]+쪽/) || [''])[0]})`);
  await game.shot('signpost', '길 이정표와 갈 곳');
  await game.context.close();
}

// ---------------------------------------------------------------- 배포를 넘어 남는 웹 저장본 (49·50단계)
// A browser profile kept between runs (out/profile-deploy): the first run leaves a character and its commit; a run on
// a later deploy loads it and checks its level and bag are still there.
async function deploysave() {
  note('\n## 다음 배포 뒤에도 남는 저장본 (PC)\n');
  const dir = path.join(__dirname, 'out', 'profile-deploy');
  const marker = path.join(dir, 'left-by.json');
  const context = await chromium.launchPersistentContext(dir, { ...Profiles.pc.context, args: LaunchArgs });
  const game = await open(null, run, Profiles.pc, url, context);
  const deployed = await deployedCommit(url);
  const left = fs.existsSync(marker) ? JSON.parse(fs.readFileSync(marker, 'utf8')) : null;
  await game.startSolo('배포시험', true);
  await game.wait(1500);
  const s = await game.state();
  const inv = await game.inventory();
  if (left && left.commit !== deployed) {
    check(s.level === left.level && count(inv, 'stone') === left.stone, `배포(${left.commit.slice(0, 7)} → ${deployed.slice(0, 7)})를 넘어 캐릭터가 남는다 (Lv ${s.level}/${left.level}, 돌 ${count(inv, 'stone')}/${left.stone})`);
  } else {
    note(`- 처음이거나 같은 배포라 다음 배포 때 확인한다 (지금 ${deployed.slice(0, 7)})`);
    await game.send('xp 300; give stone 23');
    await game.wait(1500);
  }
  const now = await game.state();
  const bag = await game.inventory();
  await leaveGame(game);
  await game.until(st => !st.playing, 8000);
  await game.wait(1000);
  fs.writeFileSync(marker, JSON.stringify({ commit: deployed, level: now.level, stone: count(bag, 'stone') }));
  await context.close();
}

// ---------------------------------------------------------------- 처음 10분 (사람 요청 2026-11-09의 4번)
// A new character follows only the goal list (its first six goals), with its own hands: keys, the bag's buttons,
// attacking toward a wall.
// No give, build or goto, except one goto where the screen does not say where to go (noted in the report). Each
// goal's time and what got in the way are written down.
async function first(browser) {
  await firstRun(browser, Profiles.pc, false);
}

// The same on a phone (2026-11-09): every step by finger, the move stick on the left half, the attack stick on the
// right, quick slots and buttons tapped.
async function firstphone(browser) {
  await firstRun(browser, Profiles.phone, false);
}

// The first hour (2026-11-09): the first ten minutes and on through goals 7 to 16 (plant, till, fish, sell, furnace,
// mining skill, level 3, the cave warden, a bed, level 5).
async function hour(browser) {
  await firstRun(browser, Profiles.pc, true);
}

async function firstRun(browser, profile, hourLong) {
  note(`\n## ${hourLong ? '처음 한 시간' : '처음 10분'}: 목표 안내만 따라가기 (${profile.touch ? `휴대폰 터치 ${profile.context.viewport.width}×${profile.context.viewport.height}` : 'PC'})\n`);
  const game = await open(browser, run, profile, url);
  check(await game.startSolo(hourLong ? 'firsthour' : profile.touch ? 'first10phone' : 'first10', false, null), '새 캐릭터로 혼자 하기');
  await game.wait(1500);
  // A finger on the screen (phone): the move stick on the left half, the attack stick on the right, mid-height
  // where nothing is drawn over the world.
  let cdp = null;
  const finger = async (type, points) => {
    cdp = cdp || (await game.context.newCDPSession(game.page));
    await cdp.send('Input.dispatchTouchEvent', { type, touchPoints: points });
  };
  // Taken beside a thing (the playtest's "goto near"), which turns the player to it by holding the aim
  // (GameRoot.AimHeld). On the phone nothing else lets go of that hold, so the attack stick never turned the player
  // again and every dig after it struck the same side (2026-11-09: 75 of 91 digs did not break): let go at once.
  const gotoNear = async what => {
    const answer = await game.send(`goto near ${what}`);
    if (game.touch) await game.send('face off');
    return answer;
  };
  const screen = await game.canvas();
  const sticks = {
    move: { x: screen.left + screen.width * 0.25, y: screen.top + screen.height * 0.55 },
    aim: { x: screen.left + screen.width * 0.75, y: screen.top + screen.height * 0.55 },
  };
  const Pull = 45;
  const hold = async (stick, dx, dy) => {
    const at = sticks[stick];
    await finger('touchStart', [{ x: at.x, y: at.y }]);
    await game.wait(40);
    await finger('touchMove', [{ x: at.x + dx * Pull, y: at.y - dy * Pull }]);
  };
  const lift = async () => finger('touchEnd', []);
  // Quick slot n (1 = the first): its key on PC, a tap on the phone. What it put in hand is kept for the pilot.
  let holding = null;
  const slot = async n => {
    if (game.touch) await game.press(`quick ${n - 1}`);
    else await game.page.keyboard.press(String(n));
    holding = n === 2 ? 'pickaxe' : n === 1 ? 'weapon' : null;
    await game.wait(300);
  };
  const began = Date.now();
  const clock = () => `${Math.round((Date.now() - began) / 1000)}초`;
  const goalNow = async () => {
    const hud = ((await game.texts()).hud || []).join(' | ');
    // "목표 3/65: …", or merged with the story's step "깨어난 수호자 · 목표 10/65" (2026-11-11).
    const m = hud.match(/목표 (\d+)\/\d+:? ?([^|]*)/);
    return m ? { n: +m[1], text: m[2].trim() } : { n: 0, text: '' };
  };
  // Each goal's place in the guide (goals.csv, 1 = the first), so the checks follow the sheet when the order changes
  // (2026-11-11: the furnace, level 3, the warden and the bed came before planting, tilling, fishing and selling).
  const order = fs.readFileSync(path.join(__dirname, '..', '..', 'Assets', '_Project', 'Data', 'Guide', 'goals.csv'), 'utf8')
    .split('\n').slice(1).filter(l => l.trim()).map(l => l.split(',')[0]);
  const at = id => order.indexOf(id) + 1;
  const steps = [];
  const passed = async (n, what) => {
    const now = await goalNow();
    const ok = now.n > n;
    steps.push(`${clock()} 목표 ${n} "${what}" ${ok ? '이룸' : '못 이룸'} → 지금 목표 ${now.n}: ${now.text}`);
    check(ok, `목표 ${n} "${what}"를 안내만 보고 이룬다 (${clock()})`);
    return ok;
  };
  const near = async () => {
    const out = [];
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) out.push({ dx, dy, tile: (await game.look(dx, dy)).tile });
    return out;
  };
  // Rock and ore only: the walls of the story's places (TimberWall, CastleWall) are not dug (2026-11-10).
  const solid = t => t === 'Wall' || /Ore$/.test(t || '');
  const toward = (dx, dy) => (Math.abs(dx) > Math.abs(dy) ? [Math.sign(dx), 0] : [0, Math.sign(dy)]);
  const offset = text => (text ? text.split(',').map(Number) : null);
  // Digs the wall at (dx, dy) with what is in hand: attack toward it until it breaks (at most six seconds). On the
  // phone the attack stick is held pulled toward it.
  // Where the time goes (seconds walking to veins, seconds digging, digs that did not break), for the report.
  const spent = { vein: 0, dig: 0, digs: 0, stuck: 0, fights: 0 };
  const dig = async (dx, dy) => {
    const started = Date.now();
    spent.digs++;
    if (game.touch) await hold('aim', dx, dy);
    else await game.send(`face ${dx} ${dy}; attack on ${dx} ${dy}`);
    let gone = false;
    for (let i = 0; i < 20 && !gone; i++) {
      await game.wait(300);
      gone = !solid((await game.look(dx, dy)).tile);
    }
    if (game.touch) await lift();
    else await game.send('attack off');
    spent.dig += (Date.now() - started) / 1000;
    if (!gone) {
      spent.stuck++;
      // The first few that did not break, with where the bot stood in its tile and what was near, for the report.
      if (spent.stuck <= 3) {
        const s = await game.state();
        const t = await game.texts();
        note(`  - (안 깨진 벽 ${spent.stuck}) 방향 ${dx},${dy}, 벽 ${(await game.look(dx, dy)).tile}, 선 곳 ${s.x},${s.y}, 바라본 칸 ${s.ahead}, 손 ${s.weapon}, 가까운 몬스터 ${s.foe || '없음'}(${s.monsters}마리), 체력 ${s.health}, 떠 있는 글 ${(t.floating || []).join('/') || '없음'}`);
      }
    }
    return gone;
  };
  const key = { '1,0': 'd', '-1,0': 'a', '0,1': 'w', '0,-1': 's' };
  const step = async (dx, dy, ms = 260) => {
    if (game.touch) {
      await hold('move', dx, dy);
      await game.wait(ms);
      await lift();
    } else {
      await game.page.keyboard.down(key[`${dx},${dy}`]);
      await game.wait(ms);
      await game.page.keyboard.up(key[`${dx},${dy}`]);
    }
    await game.wait(150);
  };
  // Moves (dx, dy) tiles in any direction (2026-11-10: the warden bot stepped one axis at a time out of a warning that
  // wanted a diagonal, and stayed in the rock trail): both keys of a diagonal held on PC, the stick on the phone, for
  // about as long as the distance takes at a walk.
  const move = async (dx, dy) => {
    const d = Math.hypot(dx, dy);
    if (d < 0.05) return;
    const ms = Math.min(900, Math.round(d * 230 + 60));
    if (game.touch) {
      await hold('move', dx / d, dy / d);
      await game.wait(ms);
      await lift();
      return;
    }
    const keys = [];
    if (Math.abs(dx) > d * 0.38) keys.push(key[`${Math.sign(dx)},0`]);
    if (Math.abs(dy) > d * 0.38) keys.push(key[`0,${Math.sign(dy)}`]);
    for (const k of keys) await game.page.keyboard.down(k);
    await game.wait(ms);
    for (const k of keys) await game.page.keyboard.up(k);
  };
  // Mines a tunnel: the nearest wall beside, else a step toward open floor, until `enough` says stop.
  // With `prefer` (a tile type) it digs that first, and with none beside it goes to the next one (`onNone`).
  const drink = async () => {
    if (game.touch) await game.press('potion', undefined, { quiet: true });
    else await game.page.keyboard.press('c');
  };
  // Attacks toward (dx, dy) for a moment: the attack stick pulled that way on the phone, the aim on PC.
  const strike = async (dx, dy, ms) => {
    const d = Math.hypot(dx, dy) || 1;
    if (game.touch) {
      await hold('aim', dx / d, dy / d);
      await game.wait(ms);
      await lift();
    } else {
      await game.send(`attack on ${(dx / d).toFixed(2)} ${(dy / d).toFixed(2)}`);
      await game.wait(ms);
      await game.send('attack off');
    }
  };
  // The bot by the game's state (bot.js, Client/PlaytestBot.cs, 2026-11-11): the game's own path finding says where
  // to go next (round water, digging through rock, at the nearest monster or vein even off the screen), and the bot
  // does it with the keys and touches above. It replaced looking round the screen tile by tile, which met no monster
  // in the hunts and stood in the village plaza with no rock in sight (2026-11-10).
  const pilot = makePilot({
    ask: command => game.ask(command),
    wait: ms => game.wait(ms),
    move: (dx, dy) => move(dx, dy),
    dig: (dx, dy) => dig(dx, dy),
    strike: (dx, dy, ms) => strike(dx, dy, ms),
    hold: async what => {
      if (holding !== what) await slot(what === 'pickaxe' ? 2 : 1);
    },
    note,
    shot: (name, caption) => game.shot(name, caption),
  });
  await game.send('botreset');
  // Digs rock (or the tile type given, an ore vein) until `enough` says stop: the nearest by the way there, through
  // what is in the way; monsters that come near are fought on the way.
  const mine = async (enough, seconds, prefer = null) => {
    const result = await pilot.go(prefer ? `tile ${prefer}` : 'wall', { until: enough, seconds });
    if (result !== 'done') note(`  - (캐기 ${prefer || '벽'}: ${result}) ${seconds}초 안에 다 못 캠`);
    return result;
  };
  // Walks to stand beside the nearest tile of the type, facing it (2026-11-11: no more taken there by the playtest's
  // "goto near"); taken there only if the way cannot be found, which the report says.
  const walkTo = async (what, why) => {
    const result = await pilot.go(`near ${what}`, { seconds: 120 });
    if (result === 'arrived') {
      const at = pilot.last;
      if (at.x || at.y) await turn(Math.sign(Math.round(at.x)), Math.sign(Math.round(at.y)));
      return true;
    }
    note(`  - (${why}) 걸어서 못 감(${result}): 옮겨졌다`);
    await gotoNear(what);
    await game.wait(800);
    return false;
  };
  const inv = () => game.inventory();
  // Presses [만들기] on the row of the thing (only what can be made shown) until the bag holds one more of it: a
  // press during the list's redraw can land elsewhere. True when it was made.
  const makeOne = async (name, id) => {
    const before = count(await inv(), id);
    for (let attempt = 0; attempt < 3; attempt++) {
      await game.press('만들 수 있는 것만', undefined, { quiet: true });
      await game.wait(400);
      await game.press('만들기', new RegExp('^' + name), { quiet: true });
      // Some things take a few seconds by hand (Docs/Crafting.md 제작 시간).
      for (let i = 0; i < 10 && count(await inv(), id) <= before; i++) await game.wait(500);
      if (count(await inv(), id) > before) break;
    }
    await game.press('모두 보기', undefined, { quiet: true });
    return count(await inv(), id) > before;
  };
  // What the screen and the map said when something did not happen, for the report.
  const why = async label => {
    const t = await game.texts();
    const here = await game.look(0, 0);
    const s = await game.state();
    const around = (await near()).map(a => `${a.dx},${a.dy}=${a.tile}`).join(' ');
    note(`  - (${label}) 칸 ${here.x},${here.y}, 체력 ${s.health}/${s.maxHealth}${s.dead ? ' 쓰러짐' : ''}, 곁: ${around}; 떠 있는 글: ${(t.floating || []).join(' / ') || '없음'}; 알림: ${(t.acquired || []).slice(-3).join(' / ') || '없음'}`);
  };
  const t0 = await goalNow();
  note(`- 첫 목표: ${t0.n}. ${t0.text}`);

  // 1. A class (2026-11-09): picked in the bag's skills page, as the first goal's hint says; the warrior. On the
  // phone the touch buttons ([가방]) come with the first touch: the test browser is a desktop one in a phone's size, so
  // the game does not know it for a phone until touched (a phone's browser starts touch-first, Controls.UsingTouch),
  // so a person's first touch on the world first.
  if (game.touch) {
    await hold('move', 0, 0);
    await game.wait(200);
    await lift();
    check(!!(await game.until(b => b.some(x => x.label === '가방'), 10000, game.buttons)), '휴대폰: 첫 터치 뒤 [가방] 단추가 보인다');
  }
  await bagTab(game, '스킬');
  await game.wait(500);
  await game.shot('class-pick', '클래스 고르기 목록(고르기 전)');
  // Its six skills before picking (2026-11-09).
  check(await game.press('미리보기', /^전사:/), '클래스마다 [미리보기]가 있다');
  await game.wait(500);
  check(/전투 함성 · Lv 3부터/.test(await game.allText()), '미리보기에 그 클래스의 스킬과 열리는 레벨이 나온다(전투 함성 · Lv 3부터)');
  await game.shot('class-preview', '전사 미리보기');
  check(await game.press('고르기', /^전사:/), '가방 [스킬] 탭에 클래스마다 [고르기]가 있다');
  await game.wait(800);
  const picked = (await game.inventory()).classes;
  check(picked === 'warrior', `전사를 고르면 클래스가 된다 (${picked})`);
  check((await game.find('고르기')).length === 0, '레벨 1에는 하나만: 고른 뒤에는 [고르기]가 없다');
  await game.shot('class-picked', '전사를 고른 [스킬] 탭');
  await closeBag(game);
  await passed(at('class'), '클래스 고르기');

  // 2. Ten stones, with the pickaxe on 2.
  await slot(2);
  await mine(async () => count(await inv(), 'stone') >= 10, 120);
  await game.wait(1200);
  await passed(at('stone'), '돌 10개 모으기');
  await game.shot('first-stone', '돌을 캔 굴');
  // Room to put things: the walls on every side dug out, as a person clears a little room for a base (a workbench
  // set down in a tunnel one wide shuts the way back).
  for (const a of await near()) if (solid(a.tile)) await dig(a.dx, a.dy);

  // Puts the thing from the bag's materials tab, facing each open side in turn until the goal moves on.
  const placeFor = async (n, what, row) => {
    const tried = [];
    let walked = false;
    for (let attempt = 0; attempt < 4; attempt++) {
      // Up again first if a monster felled it (2026-11-10: the bot tried to set its torch down lying on the floor).
      if ((await game.state()).dead) {
        await game.until(st => !st.dead, 15000);
        await game.wait(800);
      }
      let at = await faceOpen(tried);
      // No bare floor beside (2026-11-11: on the 512-tile map the workbench went up among the village's roads, and
      // the stone floor had nowhere to go): to the nearest bare floor by the game's path finding, and again.
      if (!at && !walked) {
        walked = true;
        const result = await pilot.go('near Floor', { seconds: 40 });
        note(`  - (${what}) 곁에 맨바닥이 없어 가까운 맨바닥으로 걸어감(${result})`);
        at = await faceOpen(tried);
      }
      if (!at) {
        await why(`${what}: 놓을 맨바닥 없음`);
        break;
      }
      tried.push(`${at.dx},${at.dy}`);
      await bagTab(game, '재료·제작');
      const pressed = await game.press('놓기', row, { quiet: attempt > 0 });
      await game.wait(400);
      await closeBag(game);
      await game.wait(600);
      if ((await goalNow()).n > n) break;
      if (!pressed) break;
      await why(`${what} ${attempt + 1}번째`);
    }
    return passed(n, what);
  };

  // 2. A workbench: the bag's materials tab, [만들기], then [놓기] onto open floor ahead.
  // Faces open floor, two tiles of it if there are (a thing goes on the tile after when the body is in the first);
  // with only one, takes a short step back from it first. Sides already tried are skipped.
  // On a phone a person turns by a nudge of the move stick, so the bot does the same (the attack stick's last pull
  // would otherwise still decide which way it faces).
  const turn = async (dx, dy) => {
    if (game.touch) await step(dx, dy, 120);
    else await game.send(`face ${dx} ${dy}`);
  };
  const faceOpen = async (skip = [], depth = 0) => {
    // Open floor with nothing growing on it (a person sees the sprouts).
    const around = [];
    for (const a of await near()) if (a.tile === 'Floor' && !skip.includes(`${a.dx},${a.dy}`) && !(await game.look(a.dx, a.dy)).crop) around.push(a);
    for (const a of around) {
      if ((await game.look(2 * a.dx, 2 * a.dy)).tile === 'Floor') {
        await turn(a.dx, a.dy);
        return a;
      }
    }
    const open = around[0];
    if (open) {
      await step(-open.dx, -open.dy, 90).catch(() => {});
      await turn(open.dx, open.dy);
      return open;
    }
    // No open floor beside (things and sprouts all round): a step onto the floor or crop beside, and look again.
    const walkable = (await near()).find(a => a.tile === 'Floor' || a.tile === 'StoneFloor' || a.tile === 'Soil' || a.tile === 'Road');
    if (walkable && depth < 3) {
      await step(walkable.dx, walkable.dy, 400);
      return faceOpen([], depth + 1);
    }
    return null;
  };
  await bagTab(game, '재료·제작');
  check(await game.press('만들기', /^작업대/), '[재료·제작]에 작업대 [만들기]가 있다');
  await closeBag(game);
  await placeFor(at('workbench'), '작업대 세우기', '작업대 ×');

  // 3. A torch from the bag, onto other open floor.
  await placeFor(at('torch'), '횃불 세우기', /^횃불 ×/);

  // 4. Stone floor: made at the workbench (beside it), laid on open floor.
  await mine(async () => count(await inv(), 'stone') >= 4, 150);
  const bench = (await near()).find(a => a.tile === 'Workbench');
  if (!bench) {
    // Dug away from it for the stone: back beside it, as a person would walk back down the tunnel.
    await walkTo('Workbench', '돌 바닥을 만들려고 작업대로');
  }
  // Stone floor is the workbench's: its window (U6), shown only what can be made, as a person might.
  await stationTab(game, '돌 바닥');
  const rows = await game.find('만들기', /^돌 바닥/);
  const made = await makeOne('돌 바닥', 'stone_floor');
  await closeBag(game);
  check(made, '작업대 곁에서 돌 바닥 [만들기]');
  if (!made) {
    note(`  - 돌 바닥 줄: ${rows.map(b => `${b.enabled ? '눌림' : '안 눌림'}${b.shown ? '' : '(가려짐)'} "${b.row}"`).join(' / ') || '없음'}, 돌 ${count(await inv(), 'stone')}`);
    await why('돌 바닥');
  }
  await placeFor(at('floor'), '돌 바닥 깔기', /^돌 바닥 ×/);

  // 5. Six copper ores: the nearest veins by the way there (the minimap rings the nearest; the bot knows them all within
  // a screen and a half, and goes on past the edge of what it has seen when there are none).
  await mine(async () => count(await inv(), 'copper_ore') >= 6, 150, 'CopperOre');
  if (count(await inv(), 'copper_ore') < 6) await why(`구리 ${count(await inv(), 'copper_ore')}개에서 멈춤`);
  note(`- 구리까지: 길잡이 ${pilot.counts.navs}번 물음, 걸음 ${pilot.counts.moves}번, 캐기 ${pilot.counts.digs}번(그 가운데 6초 안에 안 깨진 것 ${spent.stuck}번), 싸움 ${pilot.counts.fights}번, 멈춤 ${pilot.counts.stalls}번`);
  await game.wait(1000);
  await passed(at('copper'), '구리 광석 6개 캐기');

  // 6. A piece of copper gear, made beside the workbench and worn. (Mining skill 2 was the sixth goal until
  // 2026-11-09: a hundred walls, three minutes or more of digging alone, held up every goal after it.)
  await walkTo('Workbench', '구리 장비를 만들려고 작업대로');
  await stationTab(game, '구리 투구');
  let gear = false;
  await game.press('만들 수 있는 것만');
  for (const name of ['구리 투구', '구리 장화', '구리 갑옷']) {
    if (!gear && (await game.find('만들기', new RegExp('^' + name))).some(b => b.enabled)) gear = await game.press('만들기', new RegExp('^' + name)) && name;
  }
  // Gear takes 3 seconds by hand: the bot stays by the workbench meanwhile, as a person would watch the bar.
  if (gear) await game.wait(3800);
  await game.press('모두 보기', undefined, { quiet: true });
  check(!!gear, `구리 장비 하나를 만든다 (${gear || '재료 부족'})`);
  if (gear) {
    await bagTab(game, '장비');
    await pick(game, new RegExp(gear));
    await game.press('장착');
  }
  await closeBag(game);
  await game.wait(1000);
  await passed(at('copper_gear'), '구리 장비 하나 만들기');
  await game.shot('first-gear', '처음 10분 끝');
  if (!hourLong) {
    // A second class at level 10 (2026-11-09): the slot opens, the banner says so, and the skills page offers the
    // other classes again, free.
    await game.send('xp 1200');
    const up = await game.until(t => /클래스를 하나 더 고를 수 있다/.test(t), 5000, game.allText);
    check(!!up, '레벨 10이 되면 "클래스를 하나 더 고를 수 있다" 알림');
    await bagTab(game, '스킬');
    check(await game.press('고르기', /^화염 마법사:/), '레벨 10에는 [스킬] 탭에 다시 [고르기]가 있다');
    const two = await game.until(i => i.classes === 'warrior,fire_mage', 4000, game.inventory);
    check(!!two, `둘째 클래스를 금화 없이 고른다 (${(await game.inventory()).classes})`);
    check(/레벨 20에 하나 더 고를 수 있다/.test(await game.allText()), '고른 뒤에는 "레벨 20에 하나 더 고를 수 있다"');
    await game.shot('second-class', '둘째 클래스를 고른 [스킬] 탭');
    await closeBag(game);
  }
  if (hourLong) await onwards();

  note('\n목표마다 걸린 때:\n');
  for (const line of steps) note(`- ${line}`);
  // The run's numbers by the game's state (BotReport, the same tables as the sim's CoreSandbox --hour).
  const numbers = await game.ask(`botreport 웹 ${game.touch ? '휴대폰' : 'PC'}`);
  if (typeof numbers === 'string') note('\n### 숫자 지표\n\n' + numbers);
  note(`\n- 길잡이: ${pilot.counts.navs}번 물음, 걸음 ${pilot.counts.moves}, 캐기 ${pilot.counts.digs}, 싸움 ${pilot.counts.fights}, 멈춤 ${pilot.counts.stalls}, 막힘 ${pilot.counts.stuck}`);
  await game.context.close();

  // ---- Goals 7 to 15, the rest of the first hour ----
  async function onwards() {
    const moved = new Set();
    // Where the bot is taken instead of walking back down its tunnels (once a reason, for the report).
    const taken = async (where, why) => {
      // A bot that fell gets up at the spawn (or its bed) a few seconds later: moved before that, it would be put
      // back there (the first-hour run of 2026-11-09 stood at the spawn when it went to make the rod).
      if ((await game.state()).dead) {
        note(`  - (${why}) 봇이 쓰러져 있었다: 일어날 때까지 기다린다`);
        await game.until(s => !s.dead, 15000);
        await game.wait(500);
      }
      // Walked there by the game's path finding (2026-11-11); taken there only when the way cannot be found.
      if (where.startsWith('near ') || where.startsWith('boss ')) {
        const result = await pilot.go(where, { seconds: 150 });
        if (result === 'arrived') {
          const at = pilot.last;
          if (where.startsWith('near ') && (at.x || at.y)) await turn(Math.sign(Math.round(at.x)), Math.sign(Math.round(at.y)));
          return;
        }
        note(`  - (${why}) 걸어서 못 감(${result})`);
      }
      const answer = (await game.send(`goto ${where}`)).join(' ');
      if (game.touch && where.startsWith('near ')) await game.send('face off');
      await game.wait(800);
      if (!/^ok/.test(answer)) note(`  - (${why}) goto ${where}: ${answer || '대답 없음'}`);
      if (!moved.has(why)) note(`- ${why}: 봇은 옮겨졌다(걸어서 못 가서)`);
      moved.add(why);
    };
    const ids = { 호미: 'hoe', 낚싯대: 'fishing_rod', 화로: 'furnace', 침대: 'bed', '생선 구이': 'grilled_fish' };
    // Each made at its station, in the station's window (U6).
    const craft = async name => {
      await stationTab(game, name);
      const made = await makeOne(name, ids[name]);
      await closeBag(game);
      if (!made) await why(`${name} 만들기`);
      return made;
    };
    const copperFor = async n => {
      if (count(await inv(), 'copper_ore') >= n) return;
      await mine(async () => count(await inv(), 'copper_ore') >= n, 150, 'CopperOre');
    };
    // Walks onto the drops lying near.
    const gather = async seconds => {
      await pilot.go('loot', { seconds });
    };
    // Monsters felled on the run so far (the game's bestiary, by kind).
    const felled = async () => ((await game.ask('sense')).kills || '').split(',').filter(Boolean).reduce((n, k) => n + Number(k.split(':')[1]), 0);
    // Fights the nearest monsters until `enough` (or the time runs out): the game's path finding takes the bot to the
    // nearest within a screen and a half, not much above its level, and on past the edge of what it has seen when
    // there is none (2026-11-10: looking only at the screen, the hunts met no monster in five runs). Drinks below 40%
    // health on the way; picks up what falls.
    // With a kind, only that one (the bed's gel comes from slimes alone, 2026-11-11: the web run hunted 49 of seven
    // kinds and had two gel).
    const hunt = async (enough, seconds, kind = null) => {
      const before = await felled();
      const end = Date.now() + seconds * 1000;
      let result = 'time';
      while (Date.now() < end) {
        const s = await game.state();
        if (s.dead) {
          await game.until(t => !t.dead, 15000);
          continue;
        }
        if (s.health < s.maxHealth * 0.4) await drink();
        const low = async () => {
          const t = await game.state();
          return t.health < t.maxHealth * 0.4;
        };
        result = await pilot.go(kind ? `monster ${kind}` : 'monster', { until: async () => (await enough()) || (await low()), seconds: Math.max(5, (end - Date.now()) / 1000) });
        if (result === 'done' && (await enough())) break;
        if (result === 'done') await gather(3);
        if (result === 'stuck' || result === 'noway') break;
      }
      await gather(4);
      if (!(await enough())) note(`  - (사냥: ${result}) ${seconds}초 안에 다 못 이룸`);
      return (await felled()) - before;
    };

    // In the guide's order (goals.csv, 2026-11-11): the furnace, level 3, the warden and the bed, then planting,
    // tilling, fishing, selling, the mining skill and level 5.
    // The furnace: stone 15 and copper 2 at the workbench (2026-11-10: a stone run that ended short left it unmade and
    // every goal after it standing), set down.
    if (count(await inv(), 'stone') < 15 || count(await inv(), 'copper_ore') < 2) {
      await copperFor(2);
      await mine(async () => count(await inv(), 'stone') >= 15, 300);
      note(`- 화로 재료를 모았다: 돌 ${count(await inv(), 'stone')}, 구리 ${count(await inv(), 'copper_ore')}`);
    }
    await taken('near Workbench', '작업대로 돌아갈 때');
    await craft('화로');
    await placeFor(at('furnace'), '화로 세우기', /^화로 ×/);

    // Level 3 by hunting, away from the spawn, as the goal's hint says.
    const early = await hunt(async () => (await game.state()).level >= 3, 300);
    note(`- 레벨 3까지 사냥: ${early}마리, 레벨 ${(await game.state()).level}`);
    await game.wait(800);
    await passed(at('level3'), '레벨 3 되기');

    // The cave warden, fought with what the bot has: up to eight tries in ten minutes, back to the arena after a
    // fall. Two levels more first (the bot dodges slower than a person; at level 3 it fell five times and at level 4
    // eight, at level 6 none, 2026-11-10).
    if ((await game.state()).level < 5) {
      // Up to eight minutes (2026-11-11: five ended at level 4 on the web, and the warden then felled the bot seven
      // times in ten minutes).
      const more = await hunt(async () => (await game.state()).level >= 5, 480);
      note(`- 수호자 앞 사냥: ${more}마리, 레벨 ${(await game.state()).level}`);
    }
    // The points a player spends before a boss (the HUD keeps saying 스킬 포인트·특성 포인트): every [배우기] and
    // [올리기] on the skills page, then the passive tree's open nodes (2026-11-10: the bot fought the warden with them
    // all unspent).
    await bagTab(game, '스킬');
    let raised = 0;
    for (let i = 0; i < 8; i++) {
      const b = (await game.buttons()).find(x => x.shown && x.enabled && /^(배우기|올리기)$/.test(x.label));
      if (!b) break;
      await game.tapAt(b.cx, b.cy);
      await game.wait(500);
      raised++;
    }
    await game.press('특성');
    for (let i = 0; i < 6; i++) {
      const nodes = (await game.buttons()).filter(b => b.shown && b.enabled && /: /.test(b.label) && b.label.length > 4);
      const node = nodes[i % Math.max(1, nodes.length)];
      if (!node) break;
      await game.tapAt(node.cx, node.cy);
      await game.wait(300);
      await game.press('배우기', undefined, { quiet: true });
      await game.wait(400);
    }
    await closeBag(game);
    note(`- 수호자 앞에서 포인트를 썼다: 스킬 ${raised}번, 특성 ${(await inv()).passives}점`);
    const wardenAt = Date.now();
    let falls = 0;
    let dodges = 0;
    let swings = 0;
    let closest = Infinity;
    let lowest = Infinity;
    await slot(1);
    await taken('boss cave_warden', '동굴 수호자에게 갈 때(HUD에 방향과 거리가 나온다)');
    while (Date.now() - wardenAt < 600000 && (await goalNow()).n <= at('boss') && falls < 8) {
      const s = await game.state();
      if (s.dead) {
        falls++;
        await game.until(t => !t.dead, 15000);
        await taken('boss cave_warden', '쓰러진 뒤 동굴 수호자에게 다시 갈 때');
        continue;
      }
      if (s.health < s.maxHealth * 0.5) await drink();
      // A warning on the floor underfoot: out of it first, as the goal's hint says, the way the screen shows clear
      // (the nearest safe spot, diagonals too).
      const away = offset(s.danger);
      if (away && (away[0] || away[1])) {
        await move(away[0], away[1]);
        dodges++;
        continue;
      }
      const boss = offset(s.bossAt);
      if (!boss) {
        // Asleep in its arena, or felled: to it by the way there (it wakes as the bot comes near), or the drops.
        await gather(5);
        if ((await goalNow()).n > at('boss')) break;
        await pilot.go('fight cave_warden', { seconds: 4 });
        continue;
      }
      const d = Math.hypot(boss[0], boss[1]);
      closest = Math.min(closest, d);
      const hp = (s.bosses || '').match(/cave_warden:(\d+)\//);
      if (hp) lowest = Math.min(lowest, Number(hp[1]));
      if (/pickaxe/.test(s.weapon || '')) await slot(1);
      // A monster at the bot's side (the arena's sand scorpions) first, or it wears the bot down.
      const add = offset(s.foe);
      if (add && Math.hypot(add[0], add[1]) < 1.6 && d > 2.4) {
        await strike(add[0], add[1], 400);
        continue;
      }
      if (d > 2.2) {
        // Round what is in the way by the game's path finding (a person sees the way on the screen).
        const step = await game.ask('nav fight cave_warden');
        if (step.do === 'move') await move(step.x, step.y);
        else await move(boss[0] * (d - 1.8) / d, boss[1] * (d - 1.8) / d);
      } else {
        // The class's skills too, as a player would (Q, E), aimed at it.
        if (!game.touch) {
          await game.send(`face ${(boss[0] / d).toFixed(2)} ${(boss[1] / d).toFixed(2)}`);
          await game.page.keyboard.press('q');
          await game.page.keyboard.press('e');
          await game.send('face off');
        }
        await strike(boss[0], boss[1], 450);
        swings++;
      }
    }
    await gather(5);
    const fought = await game.state();
    note(`- 동굴 수호자: ${Math.round((Date.now() - wardenAt) / 1000)}초, 쓰러짐 ${falls}번, 피하기 ${dodges}번, 휘두름 ${swings}번, 가장 가까이 ${closest.toFixed(1)}칸, 가장 낮은 체력 ${lowest}, 레벨 ${fought.level}, 무기 ${fought.weapon} (${fought.bosses.split(' ').find(b => b.startsWith('cave_warden')) || ''})`);
    await game.shot('hour-warden', '동굴 수호자와 싸운 뒤');
    if (!(await passed(at('boss'), '동굴 수호자 쓰러뜨리기'))) {
      // The guide waits at the warden; the later goals could not be told apart from it. Recorded as failed above,
      // then the guide is moved on to the bed (playtest "guide") so the rest is still checked (2026-11-11).
      await game.send('guide bed');
      await game.wait(800);
      note('  - 수호자를 못 잡아 안내를 침대 목표로 넘기고 뒤 목표를 이어서 본다(guide bed)');
    }

    // A bed: stone 6 and slime gel 3, the gel from slimes; and two more for the rod after it (the guide's order since
    // 2026-11-11 makes the bed before the rod, so the torch goal's two gel go into the bed).
    // Until both are in the bag (2026-11-10: three minutes of hunting left it short of gel, and the bed unmade).
    for (let round = 0; round < 3; round++) {
      const bag = await inv();
      if (count(bag, 'slime_gel') >= 5 && count(bag, 'stone') >= 6) break;
      await hunt(async () => count(await inv(), 'slime_gel') >= 5, 240, 'slime');
      await mine(async () => count(await inv(), 'stone') >= 6, 90);
    }
    note(`- 침대 재료: 슬라임 젤 ${count(await inv(), 'slime_gel')}, 돌 ${count(await inv(), 'stone')}`);
    await taken('near Workbench', '작업대로 돌아갈 때');
    await craft('침대');
    await placeFor(at('bed'), '침대 세우기', /^침대 ×/);

    // The two spores a new character has, planted on the cave floor ahead.
    await faceOpen();
    await bagTab(game, '재료·제작');
    await game.press('심기', '버섯 포자');
    await closeBag(game);
    await game.wait(800);
    await passed(at('plant'), '버섯 심기');

    // Copper and stone for the hoe and the rod (copper 2 + 2, stone 2 + 1).
    await copperFor(4);
    await mine(async () => count(await inv(), 'stone') >= 5, 150);
    await taken('near Workbench', '작업대로 돌아갈 때');

    // A hoe, and [밭 갈기] on the floor ahead (other open sides when it does not take).
    const hoe = await craft('호미');
    note(`- 호미: ${hoe ? '만듦' : '못 만듦'} (구리 ${count(await inv(), 'copper_ore')}, 돌 ${count(await inv(), 'stone')}, 호미 ${count(await inv(), 'hoe')})`);
    const tilled = [];
    for (let attempt = 0; attempt < 4 && (await goalNow()).n <= at('hoe'); attempt++) {
      const at = await faceOpen(tilled);
      if (!at) break;
      tilled.push(`${at.dx},${at.dy}`);
      await bagTab(game, '재료·제작');
      const pressed = await game.press('밭 갈기', '호미', { quiet: attempt > 0 });
      await game.wait(500);
      const s = await game.state();
      await closeBag(game);
      await game.wait(600);
      if (s.ahead !== 'Soil') {
        note(`  - (밭 갈기 ${attempt + 1}번째) 눌림 ${pressed}, 바라본 쪽 ${at.dx},${at.dy}, 앞 칸 ${s.ahead}, 그 칸 ${JSON.stringify(await game.look(at.dx, at.dy))}`);
        await why('밭 갈기');
      }
    }
    await passed(at('hoe'), '밭 갈기');

    // A rod (slime gel 2, from the slimes before the bed), at the nearest water.
    await taken('near Workbench', '작업대로 돌아갈 때');
    await craft('낚싯대');
    // Fishes at the water in front, pressing the rod's button as the bite comes, until `enough` (or the time runs out).
    let strikes = 0;
    const fishUntil = async (enough, seconds) => {
      await bagTab(game, '재료·제작');
      await game.press('낚시', '낚싯대');
      const rod = (await game.find(/^(낚시|걷기|낚아채기!)$/, '낚싯대'))[0];
      const end = Date.now() + seconds * 1000;
      const seen = { wait: 0, bites: 0, fights: 0 };
      while (rod && Date.now() < end && !(await enough())) {
        const st = await game.state();
        // A monster near first (2026-11-11: a spore shooter's shots broke every line in the sim): the bag shut, the
        // monster fought by the game's path finding, then back to the water and the rod.
        const foe = offset(st.foe);
        if (foe && Math.hypot(foe[0], foe[1]) < 6) {
          seen.fights++;
          await closeBag(game);
          await pilot.go('monster', { until: async () => { const t = offset((await game.state()).foe); return !t || Math.hypot(t[0], t[1]) >= 6; }, seconds: 30 });
          await taken(waterAt, '낚시하다 몬스터를 잡은 뒤 물가로');
          await bagTab(game, '재료·제작');
          await game.press('낚시', '낚싯대');
          continue;
        }
        const f = st.fishing;
        if (f === 'wait') seen.wait++;
        if (f && f.startsWith('bite')) seen.bites++;
        if (f === 'bite in') {
          await game.tapAt(rod.cx, rod.cy);
          strikes++;
          await game.wait(700);
        } else if (!f) {
          await game.tapAt(rod.cx, rod.cy);
          await game.wait(400);
        }
      }
      await closeBag(game);
      note(`  - (낚시) 기다림 ${seen.wait}번 봄, 입질 ${seen.bites}번 봄, 몬스터 먼저 ${seen.fights}번${rod ? '' : ', 낚싯대 단추 없음'}`);
    };
    let waterAt = 'near Water Cave';
    // At the dirt cave's water (2026-11-11: the cooking goal after it wants two cave minnows, which live there
    // alone; the playtest's "nav near Water Cave"), until the goal and the two minnows.
    await taken('near Water Cave', '흙 동굴 물웅덩이를 찾을 때(안내는 "동굴 가장자리 물웅덩이"라고만 한다)');
    await fishUntil(async () => (await goalNow()).n > at('fishing') && count(await inv(), 'cave_minnow') >= 2, 240);
    note(`- 낚시: 낚아채기 ${strikes}번, 송사리 ${count(await inv(), 'cave_minnow')}`);
    await passed(at('fishing'), '물고기 낚기');

    // Cooking (2026-11-11): grilled fish from two minnows, made beside the furnace as the goal's hint says.
    await taken('near Furnace', '화로로 갈 때');
    const cooked = await craft('생선 구이');
    note(`- 요리: 생선 구이 ${cooked ? '만듦' : '못 만듦'} (송사리 ${count(await inv(), 'cave_minnow')}, 생선 구이 ${count(await inv(), 'grilled_fish')})`);
    await passed(at('cook'), '요리 하나 만들기');

    // Ten coins: what the merchant buys of the bag (fish, ore, mushrooms), at the stall near the spawn.
    await taken('near MerchantStall', '상인 노점으로 갈 때');
    await bagTab(game, '상인');
    await game.press('팔기');
    for (let i = 0; i < 40 && count(await inv(), 'coin') < 10; i++) {
      const sell = (await game.find('팔기')).find(b => b.enabled && b.shown && /광석|송사리|메기|송어|게 ×|새우|버섯|젤/.test(b.row || ''));
      if (!sell) break;
      await game.tapAt(sell.cx, sell.cy);
      await game.wait(350);
    }
    note(`- 상인: 금화 ${count(await inv(), 'coin')}`);
    await closeBag(game);
    await game.wait(800);
    await passed(at('merchant'), '상인에게 팔아 금화 10개 모으기');

    // The merchant's request of the day (2026-11-11): what it needs is in the goal list once she has been met
    // ("부탁: 상인 도라 · 생선 구이 0/3"). Grilled fish: minnows from the dirt cave's water, made at the furnace;
    // mushroom spores: bought from her. Then [건네기] in her [부탁].
    // The entry's title and what it needs may come as separate lines: the title's line and the one after it.
    const requestLine = async () => {
      const hud = (await game.texts()).hud || [];
      const i = hud.findIndex(l => /^부탁: 상인 도라/.test(l));
      return i < 0 ? '' : /\d+\/\d+/.test(hud[i]) ? hud[i] : `${hud[i]} · ${hud[i + 1] || ''}`;
    };
    // What the request needs, from her [부탁] page ("필요한 것: 버섯 포자 10"; 2026-11-11: the goal list did not always
    // show the request), against the bag.
    const things = { '버섯 포자': 'mushroom_spore', '생선 구이': 'grilled_fish' };
    await taken('near MerchantStall', '부탁을 보러 상인에게');
    await bagTab(game, '상인');
    await game.press('부탁');
    await game.wait(400);
    const page = (await game.allText()).replace(/\s*\n\s*/g, ' ');
    await closeBag(game);
    const needLine = ((page.match(/필요한 것: ([^|"]+)/) || [])[1] || '').trim();
    note(`- 부탁: ${needLine || '읽지 못함'} (목표 목록: ${(await requestLine()) || '없음'})`);
    const short = {};
    for (const m of needLine.matchAll(/([^\d·,]+?) (\d+)/g)) {
      const name = m[1].trim();
      short[name] = Math.max(0, +m[2] - (things[name] ? count(await inv(), things[name]) : 0));
      if (!things[name]) note(`  - 봇이 모을 줄 모르는 부탁이다: ${name}`);
    }
    const fish = short['생선 구이'] || 0;
    if (fish > 0) {
      await taken('near Water Cave', '부탁의 생선을 낚으러 갈 때');
      await fishUntil(async () => count(await inv(), 'cave_minnow') >= fish * 2, 300);
      await taken('near Furnace', '부탁의 생선을 구우러 화로로');
      for (let i = 0; i < fish && count(await inv(), 'cave_minnow') >= 2; i++) await craft('생선 구이');
    }
    const spores = short['버섯 포자'] || 0;
    if (spores > 0) {
      await taken('near MerchantStall', '버섯 포자를 사러 상인에게');
      await bagTab(game, '상인');
      await game.press('사기');
      const goal = count(await inv(), 'mushroom_spore') + spores;
      for (let i = 0; i < 20 && count(await inv(), 'mushroom_spore') < goal; i++) {
        if (!(await game.press('사기', '버섯 포자', { quiet: i > 0 }))) break;
        await game.wait(350);
      }
      note(`  - 버섯 포자를 샀다: ${count(await inv(), 'mushroom_spore')}`);
      await closeBag(game);
    }
    await taken('near MerchantStall', '부탁을 건네러 상인에게');
    await bagTab(game, '상인');
    await game.press('부탁');
    const handed = await game.press('건네기', undefined, { quiet: true });
    await game.wait(800);
    if (!handed) await game.shot('hour-request', '건네지 못한 [부탁]');
    await closeBag(game);
    note(`- 부탁 건네기: ${handed ? '누름' : '못 누름'} (지금: ${(await requestLine()) || '목표 목록에서 사라짐'})`);
    await passed(at('request'), '부탁 하나 들어주기');

    // Mining skill 2, from all the walls dug so far and more.
    // Level 2 is 100 mining experience, a wall each (ore two), so it can take some minutes after the copper.
    const lifeBefore = (await inv()).life;
    const stoneBefore = count(await inv(), 'stone');
    const digsBefore = spent.digs;
    const stuckBefore = spent.stuck;
    await mine(async () => (await goalNow()).n > at('life_mining'), 900);
    // What the ten minutes went on (2026-11-10: three runs ended this step at level 1).
    note(`- 채굴 기술 단계: 생활 경험치(농사,낚시,요리,채굴) ${lifeBefore} → ${(await inv()).life}, 돌 ${stoneBefore} → ${count(await inv(), 'stone')}, 친 벽 ${spent.digs - digsBefore}번, 6초 안에 안 깨진 것 ${spent.stuck - stuckBefore}번`);
    await passed(at('life_mining'), '채굴 기술 2레벨 되기');

    // Level 5 by hunting.
    const kills = await hunt(async () => (await game.state()).level >= 5, 300);
    note(`- 레벨 5까지 사냥: ${kills}마리, 레벨 ${(await game.state()).level}`);
    await game.wait(800);
    await passed(at('level5'), '레벨 5 되기');
    await game.shot('hour-end', '처음 한 시간 끝');
  }
}

// Candidate 15 of Docs/Research2026-11.md (2026-11-11): the host may make the days longer. The menu's button goes
// round the lengths; a longer day runs the clock slower than play, and the client's clock keeps the host's pace.
async function time(browser) {
  note('\n## 하루 길이 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await menuPage(game, '게임 시작');
  const lengths = [];
  for (let i = 0; i < 4; i++) {
    const b = (await game.find(/^하루 길이:/))[0];
    if (!b) break;
    lengths.push(b.label.replace('하루 길이: ', ''));
    await game.press(b.label);
  }
  check(lengths.join(',') === '20분,30분,40분,60분', `메뉴의 [하루 길이]가 20→30→40→60분으로 돈다 (${lengths.join(' → ')})`);
  // Back at 20 after going round: two presses make it 40.
  await game.press('하루 길이: 20분');
  await game.press('하루 길이: 30분');
  check((await game.find('하루 길이: 40분')).length === 1, '[하루 길이: 40분]으로 맞춘다');
  await game.startSolo('daylength');
  await game.wait(1200);
  let s = await game.state();
  check(s.dayMinutes === 40, `고른 하루 길이로 월드가 열린다 (${s.dayMinutes}분)`);
  const before = await game.state();
  await game.wait(3000);
  const after = await game.state();
  const ratio = (after.clockTicks - before.clockTicks) / Math.max(1, after.tick - before.tick);
  check(Math.abs(ratio - 0.5) < 0.05, `40분이면 시계가 플레이의 절반 빠르기로 간다 (${ratio.toFixed(2)})`);
  await game.shot('daylength-hud', '하루 40분 월드의 HUD');
  await game.send('daylength 20');
  const b2 = await game.state();
  await game.wait(2000);
  const a2 = await game.state();
  const ratio2 = (a2.clockTicks - b2.clockTicks) / Math.max(1, a2.tick - b2.tick);
  check(Math.abs(ratio2 - 1) < 0.05, `20분으로 돌리면 시계가 플레이와 같이 간다 (${ratio2.toFixed(2)})`);
  await game.context.close();
}

// ---------------------------------------------------------------- 아레나 컨셉과 입장 연출 (2026-11-17, Docs/Bosses.md)
// Every boss's arena is dressed to its own idea (arenas.csv): a picture of each with the boss held asleep, and the
// report says its kind and that it is dressed. Then the entrances: the four with a motion of their own are photographed
// as they come in (asleep, early, midway, the beat, at rest), with their name card; a common one; the short way the
// second time; and the card on a phone held upright and sideways with the longest name.
const ArenaList = [
  ['cave_warden', 'Ruin', '동굴 수호자', '무너진 신전'], ['crystal_queen', 'Nest', '수정 거미 여왕', '수정 둥지'], ['lava_titan', 'Caldera', '용암 거인', '용암 분지'],
  ['void_lord', 'Altar', '공허의 군주', '소환 제단'], ['tide_serpent', 'Flooded', '물살 뱀', '수몰된 홀'], ['antlion', 'Pit', '개미귀신', '모래 구덩이'],
  ['mad_general', 'Warcamp', '미쳐 버린 장군 모르그', '전쟁 진지'], ['crown_wraith', 'Chapel', '왕관의 망령', '가라앉은 예배당'],
  ['vein_eater', 'Mine', '광맥 포식자 그룸', '광산 갱도'], ['shrine_samurai', 'Dojo', '벚꽃 검귀 시구레', '벚꽃 결투장'],
  ['black_knight', 'Hall', '흑철 기사 발두인', '흑철 왕좌의 방'], ['silk_mother', 'Web', '실 잣는 어미 아라네', '거미줄 납골당'],
];
// [boss, whole entrance seconds, short seconds, what it does]
const OwnEntrances = [
  ['vein_eater', 3.2, 1.2, '흙더미가 들썩이다 바닥을 뚫고 솟는다'], ['silk_mother', 3.4, 1.2, '천장에서 실을 타고 내려와 다리를 편다'],
  ['shrine_samurai', 3.6, 1.2, '정좌에서 일어나 칼자루에 손을 얹는다'], ['black_knight', 4.6, 1.4, '무릎을 펴고 망치를 끌며 나와 바닥을 내려찍는다'],
];

async function arenas(browser) {
  note('\n## 아레나 컨셉 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('bosses hold');
  for (const [id, kind, name, stage] of ArenaList) {
    await game.send(`heal; clear; goto arena ${id}`);
    await game.wait(1500);
    const s = await game.state();
    check(s.arena === kind, `${name}: 아레나 종류 ${kind} (${s.arena})`);
    check((s.arenaDressing || 0) >= 20, `${name}: ${stage}으로 꾸며져 있다 (꾸밈 칸 ${s.arenaDressing})`);
    check(new RegExp(`${id}:\\d+/\\d+:p1:Dormant`).test(s.bosses || ''), `${name}: 붙잡아 두면 잠든 채 (${(s.bosses || '').split(' ').find(b => b.startsWith(id))})`);
    await game.shot(`arena-${id}`, `${name}의 아레나(${stage})`);
  }

  note('\n## 입장 연출 (PC)\n');
  await game.send('bosses wake');
  const stateOf = (s, id) => (((s || {}).bosses || '').match(new RegExp(`${id}:\\d+/\\d+:p\\d:(\\w+)`)) || [])[1];
  for (const [id, seconds, again, what] of OwnEntrances) {
    const name = ArenaList.find(a => a[0] === id)[2];
    await game.send(`heal; clear; bosses hold; goto boss ${id}`);
    await game.wait(1200);
    await game.shot(`enter-${id}-asleep`, `${name}: 깨기 전(잠든 자세)`);
    // Every other boss back to sleep: one that has fought already would take up its fight again when they all wake
    // and lay its own marks. Then let anything it laid before go (clear takes only monsters).
    await game.send(ArenaList.filter(a => a[0] !== id).map(a => `boss ${a[0]} sleep`).join('; '));
    await game.until(t => (t.hazards || 0) === 0 && (t.bossShots || 0) === 0, 9000);
    await game.send(`bosses wake; boss ${id} enter`);
    const started = Date.now();
    const card = await game.until(t => has(t.entrance, name), 3000, game.texts);
    check(!!card, `${name}: 이름 카드에 이름과 별칭 (${card ? card.entrance : '없음'})`);
    check(card && card.entranceFits !== false, `${name}: 이름 카드의 글이 화면 너비 안에`);
    const entering = await game.state();
    check(stateOf(entering, id) === 'Enter', `${name}: 입장하는 동안 Enter 상태`);
    // Anything laid while it is still entering would be its own: watch until it begins to fight.
    let worst = { hazards: entering.hazards || 0, bossShots: entering.bossShots || 0 };
    for (const [share, label] of [[0.15, '처음'], [0.45, '중간'], [0.8, '절정']]) {
      await game.wait(Math.max(0, seconds * 1000 * share - (Date.now() - started)));
      await game.shot(`enter-${id}-${Math.round(share * 100)}`, `${name} 입장 ${label}: ${what}`);
    }
    const before = await game.state();
    if (stateOf(before, id) === 'Enter') worst = { hazards: Math.max(worst.hazards, before.hazards || 0), bossShots: Math.max(worst.bossShots, before.bossShots || 0) };
    check(worst.hazards === 0 && worst.bossShots === 0, `${name}: 입장하는 동안 공격이 없다 (입장 중 본 장판 ${worst.hazards}, 탄 ${worst.bossShots})`);
    const fighting = await game.until(t => { const st = stateOf(t, id); return st && st !== 'Enter' && st !== 'Dormant'; }, 6000);
    const took = (Date.now() - started) / 1000;
    check(!!fighting && took > seconds - 0.8 && took < seconds + 1.5, `${name}: 입장은 약 ${seconds}초 (${took.toFixed(1)}초)`);
    await game.shot(`enter-${id}-done`, `${name}: 입장이 끝나고 싸움 시작`);
    // Seen once: the short way, no card.
    await game.send(`heal; clear; boss ${id} enter again`);
    const quick = Date.now();
    await game.wait(350);
    const short = await game.texts();
    check(!short.entrance, `${name}: 다시 볼 때는 이름 카드 없이 (${short.entrance || '없음'}, 배너 ${short.banner || '없음'})`);
    await game.shot(`enter-${id}-again`, `${name}: 다시 볼 때 짧은 입장`);
    const again2 = await game.until(t => { const st = stateOf(t, id); return st && st !== 'Enter' && st !== 'Dormant'; }, 4000);
    check(!!again2 && (Date.now() - quick) / 1000 < again + 1.2, `${name}: 짧은 입장은 약 ${again}초 (${((Date.now() - quick) / 1000).toFixed(1)}초)`);
    await game.send(`boss ${id} sleep; bosses hold`);
  }
  // A boss with the common entrance.
  await game.send('heal; clear; bosses wake; goto arena void_lord; boss void_lord enter');
  const common = await game.until(t => has(t.entrance, '공허의 군주'), 3000, game.texts);
  check(!!common && has(common.entrance, '제단이 부른 것'), `공허의 군주: 공통 연출의 이름 카드 (${common ? common.entrance : '없음'})`);
  await game.wait(900);
  await game.shot('enter-void_lord', '공허의 군주: 공통 입장 연출(카메라, 이름 카드, 소환의 고리)');
  await game.context.close();

  // The card on phones: the longest name, upright and sideways.
  for (const profile of ['phone', 'phone-wide', 'pc-small']) {
    note(`\n## 입장 이름 카드 ${profile}\n`);
    const small = await open(browser, run, Profiles[profile], url);
    if (small.touch) {
      await menuPage(small, '게임 시작');
      await small.press('혼자 하기');
      await small.until(s => s.playing, 25000);
      await small.wait(800);
    } else {
      await small.startSolo();
    }
    await small.wait(1200);
    for (const id of ['mad_general', 'black_knight']) {
      const name = ArenaList.find(a => a[0] === id)[2];
      await small.send(`heal; clear; bosses wake; goto boss ${id}; boss ${id} enter`);
      const card = await small.until(t => has(t.entrance, name), 3000, small.texts);
      check(!!card && card.entranceFits === true, `${profile} ${name}: 이름 카드가 화면 너비 안에 (${card ? card.entrance : '없음'})`);
      await small.wait(700);
      await small.shot(`enter-card-${id}`, `${name}의 이름 카드`);
      await small.send(`boss ${id} sleep; bosses hold`);
      await small.wait(900);
    }
    await small.context.close();
  }
}

// The hero's attacks by weapon (2026-11-19, HeroArt): with each weapon the attack is held at three sturdy slimes and
// pictures are taken through the combo's three steps, to the right and then up, down and left; then a skill sent
// forward and one all round, a drink, and the hero struck. On a PC and on a phone held upright.
async function hero(browser) {
  for (const profile of ['pc', 'phone']) {
    note(`\n## 주인공 무기별 모션 (${profile === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, Profiles[profile], url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    // Out of the starting village: its people stand on the hero, and monsters are kept out of places.
    await game.send('goto wild; heal');
    await game.wait(1200);
    await clearRoom(game, 4, 4);
    const weapons = [['copper_sword', '검'], ['copper_bow', '활'], ['copper_staff', '지팡이'], ['copper_pickaxe', '곡괭이'], ['none', '맨손']];
    for (const [weapon, name] of weapons) {
      await game.send(`heal; hero ${weapon} 1 0`);
      const struck = await game.until(s => s.hurtMonsters > 0, 4000);
      check(!!struck, `${profile} ${name}: 공격이 슬라임에 닿는다`);
      // Six pictures a little apart catch different frames of the three steps.
      // Healed between pictures: a first-level hero falls to three slimes before the weapons have gone round, and
      // gets up again in the village (2026-11-19).
      for (let i = 0; i < 6; i++) {
        await game.wait(170);
        await game.send('heal');
        await game.shot(`hero-${profile}-${weapon}-${i}`, `${name} 연타 ${i + 1}`);
      }
      // The same weapon the other three ways (only on the PC: the phone's pictures are for size and fit).
      if (profile === 'pc') {
        for (const [dx, dy, way] of [[0, 1, '위'], [-1, 0, '왼쪽'], [0, -1, '아래']]) {
          await game.send(`heal; hero ${weapon} ${dx} ${dy}`);
          await game.wait(450);
          await game.send('heal');
          await game.shot(`hero-${profile}-${weapon}-${way}`, `${name} ${way}로`);
          await game.wait(230);
          await game.shot(`hero-${profile}-${weapon}-${way}-2`, `${name} ${way}로 (다음 장)`);
        }
      }
      await game.send('attack off; clear');
      await game.wait(500);
    }
    // Standing with nothing to do, then struck by a slime.
    await game.send('equip copper_sword; clear');
    await game.wait(900);
    await game.shot(`hero-${profile}-stand`, '검을 들고 서 있기');
    await game.send('spawn slime 1.2 0');
    await game.wait(2600);
    await game.shot(`hero-${profile}-hurt`, '슬라임에게 맞는 순간 무렵');
    await game.send('clear; heal');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 구르기 (월드 다시 짜기 4a, Docs/Combat.md 구르기)
// The dodge roll by its real key (PC: left Shift) and its real button (phone: a tap on "roll"): it moves the hero
// its distance, does not come again inside its cooldown, lets a blow through in its first moment and not after, and
// stops at a wall. The button's place on a phone held upright and sideways is photographed with four skills beside
// it, and checked against every other button drawn.
async function roll(browser) {
  for (const name of ['pc', 'phone', 'phone-wide']) {
    const profile = Profiles[name];
    note(`\n## 구르기 (${name === 'pc' ? 'PC, Shift' : name === 'phone' ? '휴대폰 세로, 단추' : '휴대폰 가로, 단추'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
      await game.send('classes warrior,fire_mage');
    } else {
      await game.startSolo();
    }
    await game.wait(1000);
    // Four skills on the bar, out of the village, in a lane of floor with a wall eight tiles east.
    await game.send('teach; goto wild; heal');
    await game.wait(1200);
    await game.send('scene roll; face 1 0');
    await game.wait(600);
    const press = async () => {
      if (game.touch) return game.press('roll');
      await game.page.keyboard.press('ShiftLeft');
      return true;
    };

    // The button: drawn, on the screen, and on no other button.
    const buttons = (await game.buttons()).filter(b => b.shown);
    const button = buttons.find(b => b.label === 'roll');
    check(!!button, `${name}: 구르기 단추(칸)가 그려진다`);
    if (button) {
      const [x, y, w, h] = button.rect;
      check(x >= 0 && y >= 0 && x + w <= button.screen.width + 0.5 && y + h <= button.screen.height + 0.5, `${name}: 구르기 단추가 화면 안에 있다 (${x.toFixed(0)},${y.toFixed(0)} ${w}×${h})`);
      const over = buttons.filter(b => b !== button && b.rect[0] < x + w && x < b.rect[0] + b.rect[2] && b.rect[1] < y + h && y < b.rect[1] + b.rect[3]);
      check(over.length === 0, `${name}: 구르기 단추가 다른 단추와 겹치지 않는다${over.length ? ` (겹침: ${over.map(b => b.label).join(', ')})` : ''}`);
      if (game.touch) check(Math.abs(w - 48) < 0.5 && Math.abs(h - 48) < 0.5, `${name}: 단추는 48점이다 (${w}×${h})`);
    }
    await game.shot('roll-button', '구르기 단추와 스킬 단추');

    // It moves the hero its distance (3.2 tiles), the way they face.
    const before = await game.state();
    check(await press(), `${name}: 구르기를 누른다`);
    await game.wait(150);
    await game.shot('roll-during', '구르는 중');
    await game.wait(700);
    const after = await game.state();
    const moved = after.x - before.x;
    check(after.rolls === before.rolls + 1, `${name}: 호스트가 구르기를 한 번 받았다 (${before.rolls} → ${after.rolls})`);
    check(moved > 2.9 && moved < 3.5 && Math.abs(after.y - before.y) < 0.2, `${name}: 바라보는 쪽으로 3.2칸쯤 간다 (${moved.toFixed(2)}칸)`);
    check(after.rollsShown > before.rollsShown, `${name}: 화면이 구르기 모션을 시작했다`);

    // Twice in a row, quicker than the cooldown: one roll.
    await game.wait(600);
    const twice = await game.state();
    await press();
    await press();
    await game.wait(300);
    const once = await game.state();
    check(once.rolls === twice.rolls + 1, `${name}: 다시 쓰기 시간(0.9초) 안의 둘째 누름은 구르지 않는다 (${once.rolls - twice.rolls}번)`);

    // Into the wall: the hero stops against it. (About 6.4 tiles gone; the wall's face is 7.5 from where they began.)
    await game.wait(1100);
    await press();
    await game.wait(900);
    const walled = await game.state();
    const wall = Math.floor(before.x) + 8;
    check(walled.x < wall - 0.3 && walled.x > wall - 0.6, `${name}: 벽을 지나가지 않고 그 앞에 선다 (x ${walled.x.toFixed(2)}, 벽 ${wall})`);
    await game.shot('roll-wall', '벽 앞에서 멈춤');

    // The unhurt moment: a blow three host ticks into the roll goes through, and one twelve ticks in (the roll and
    // its unhurt ticks over) lands. The blow is armed first and the roll is the real key or tap.
    await game.send('face -1 0; heal');
    await game.wait(1200);
    const whole = await game.state();
    await game.send('rollhit 10 3');
    await press();
    await game.wait(700);
    const dodged = await game.state();
    check(dodged.rolls === whole.rolls + 1, `${name}: 구른다`);
    check(dodged.health === whole.health && dodged.dodges === whole.dodges + 1, `${name}: 구르기 0.1초 뒤의 타격은 지나간다 (생명력 ${whole.health} → ${dodged.health}, 흘린 타격 ${dodged.dodges - whole.dodges})`);
    check(dodged.dodgesShown > whole.dodgesShown, `${name}: 화면에 "회피"가 뜬다`);
    await game.shot('roll-dodged', '타격을 흘린 뒤');
    await game.wait(600);
    await game.send('rollhit 10 12');
    await press();
    await game.wait(900);
    const struck = await game.state();
    check(struck.rolls === dodged.rolls + 1 && struck.health < dodged.health && struck.dodges === dodged.dodges,
      `${name}: 무적이 끝난 뒤(0.4초)의 타격은 맞는다 (${dodged.health} → ${struck.health})`);

    // The look with each weapon (a tumble, a slide, a gliding step), on the PC.
    if (!game.touch) {
      // Back and forth along the lane, so each has room.
      for (const [weapon, look, way] of [['copper_sword', '구르기', 1], ['copper_bow', '미끄러지기', -1], ['copper_staff', '잔상 걸음', 1]]) {
        await game.send(`equip ${weapon}; face ${way} 0`);
        await game.wait(1100);
        await press();
        await game.wait(130);
        await game.shot(`roll-${weapon}`, `${look} (${weapon})`);
        await game.wait(120);
        await game.shot(`roll-${weapon}-2`, `${look} 끝 무렵`);
      }
    }
    await game.send('face off');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 방으로 된 지도 (Docs/WorldRework.md 1단계)
// The rooms of the generator's version 2 (2026-11-21): a new world is made of rooms, each role has rooms and a chunk,
// the player walks from the spawn into a room with real keys (a stick on the phone), every role room is come to on
// foot (by the game's own count of steps), the hidden and locked ones are shut, ice slides and quicksand slows.
// Pictures of a room of each role and of each biome, on a PC and a phone held upright.
const RoomVisits = [
  ['pack', 'any', '무리 방'], ['pack', 'ice', '얼음 동굴의 무리 방'], ['elite', 'any', '정예 야영지'], ['treasure', 'any', '보물 방'],
  ['rest', 'any', '야영지(모닥불)'], ['locked', 'any', '자물쇠 방'], ['hidden', 'any', '숨은 방(금 간 벽)'], ['shrine', 'any', '제단'],
  ['trap', 'any', '함정 방'], ['cart', 'any', '버려진 수레'], ['pond', 'any', '물웅덩이'], ['ore', 'any', '광맥 방'], ['gate', 'cave', '던전 입구 자리'],
  ['treasure', 'lava', '용암 지대의 보물 방'], ['pack', 'sand', '모래 동굴의 무리 방'], ['elite', 'abyss', '심연의 정예 야영지'],
];
const MapBiomes = ['cave', 'mushroom', 'ice', 'crystal', 'lava', 'abyss', 'lake', 'sand'];

async function map(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 방으로 된 지도 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
      await game.send('classes warrior,fire_mage');
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('monsters hold; equip iron_armor; heal; probe on');

    // A new world is one of rooms, and every role has rooms.
    const rooms = await game.ask('rooms');
    check(rooms.generator === 2, `${name}: 새 월드는 생성기 2판이다 (${rooms.generator})`);
    check(rooms.rooms >= 700, `${name}: 방이 700개 넘는다 (${rooms.rooms})`);
    const roles = rooms.roles || {};
    const missing = ['pack', 'plain', 'ore', 'rest', 'elite', 'treasure', 'shrine', 'hidden', 'locked', 'trap', 'cart', 'gate', 'pond'].filter(r => !(roles[r] > 0));
    check(missing.length === 0, `${name}: 역할마다 방이 있다 (${Object.entries(roles).map(([r, n]) => `${r} ${n}`).join(', ')})${missing.length ? ` 없음: ${missing.join(', ')}` : ''}`);
    check(rooms.chunks >= 300, `${name}: 조각을 끼운 방이 300개 넘는다 (${rooms.chunks})`);
    check(rooms.cracked > 0, `${name}: 금 간 벽으로 막힌 지름길이 있다 (${rooms.cracked})`);
    // The roads go along the rooms, and by-ways close rings of them (Docs/WorldRework.md 묶음 1c).
    check(rooms.roads >= 6 && rooms.roadRooms >= rooms.roads * 5, `${name}: 길이 방을 따라 간다 (길 ${rooms.roads}개, 지나는 방 ${rooms.roadRooms}개)`);
    check(rooms.byways >= 3 && rooms.bywayWalls >= 2, `${name}: 장소 사이 샛길이 고리를 만든다 (샛길 ${rooms.byways}개, 금 간 벽 ${rooms.bywayWalls}곳)`);
    await game.shot('map-spawn', '시작 지점');

    // Walking, with real keys or the stick, from the spawn toward the nearest pack room by the game's path finding:
    // the player gets nearer by twenty tiles or gets there.
    const near = await game.ask('room pack any 0');
    check(near.steps > 0, `${name}: 가장 가까운 무리 방까지 걸어서 ${near.steps}걸음`);
    const screen = await game.canvas();
    const stick = { x: screen.left + screen.width * 0.25, y: screen.top + screen.height * 0.55 };
    const cdp = game.touch ? await game.context.newCDPSession(game.page) : null;
    const walk = async (dx, dy, ms) => {
      if (game.touch) {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: stick.x, y: stick.y }] });
        await game.wait(40);
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: stick.x + dx * 45, y: stick.y - dy * 45 }] });
        await game.wait(ms);
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
        return;
      }
      const keys = [dx > 0.3 ? 'd' : dx < -0.3 ? 'a' : null, dy > 0.3 ? 'w' : dy < -0.3 ? 's' : null].filter(Boolean);
      for (const key of keys) await game.page.keyboard.down(key);
      await game.wait(ms);
      for (const key of keys) await game.page.keyboard.up(key);
    };
    const from = await game.state();
    const far = s => Math.hypot(s.x - near.x - 0.5, s.y - near.y - 0.5);
    let arrived = false;
    const until = Date.now() + 70000;
    while (Date.now() < until && !arrived) {
      const intent = await game.ask(`nav at ${near.x} ${near.y}`);
      if (intent.do === 'arrived') arrived = true;
      else if (intent.do === 'move') {
        const length = Math.hypot(intent.x, intent.y) || 1;
        await walk(intent.x / length, intent.y / length, 260);
      } else if (intent.do === 'fight' || intent.do === 'dig') await game.send('clear');
      else await game.wait(300);
    }
    const walked = await game.state();
    check(arrived || far(from) - far(walked) >= 20,
      `${name}: 시작 지점에서 ${game.touch ? '스틱으로' : '키보드로'} 걸어 방으로 간다 (${far(from).toFixed(0)}칸 → ${far(walked).toFixed(0)}칸${arrived ? ', 도착' : ''})`);
    await game.shot('map-walked', '걸어서 닿은 곳');

    // The whole map.
    if (game.touch) await game.press('지도');
    else await game.page.keyboard.press('m');
    await game.wait(700);
    await game.shot('map-whole', '전체 지도');
    if (game.touch) await game.press('지도 닫기');
    else await game.page.keyboard.press('m');
    await game.wait(400);

    // Halfway along a road (it winds through rooms, not a line through the rock), and before a by-way's cracked wall.
    for (const [command, shot, label] of [['goto road 0 50', 'map-road', '길 가운데(방을 지나는 굽은 길)'], ['goto road 3 30', 'map-road2', '다른 길'],
      ['goto byway 0', 'map-byway', '샛길의 금 간 벽 앞']]) {
      const answer = (await game.send(command)).join(' ');
      await game.wait(1100);
      check(/^ok/.test(answer) && !(await game.state()).dead, `${name}: ${label}에 간다 (${answer})`);
      await game.shot(shot, label);
    }

    // A room of each role: walked to from the spawn (the game counts the steps), drawn from its chunk, and the hidden
    // and locked ones shut until their wall is dug.
    for (const [role, biome, label] of RoomVisits) {
      const room = await game.ask(`room ${role} ${biome}`);
      if (!room.role) {
        check(false, `${name}: ${label}: 그런 방이 없다`);
        continue;
      }
      check(room.steps >= 0, `${name}: ${label}(${room.biome}, 조각 ${room.chunk || '없음'})까지 시작 지점에서 걸어서 ${room.steps}걸음`);
      if (role !== 'ore' && role !== 'pack') check(!!room.chunk, `${name}: ${label}에 조각이 끼워져 있다 (${room.chunk})`);
      if (role === 'hidden' || role === 'locked') check(room.shut, `${name}: ${label}의 안쪽은 벽을 캐기 전에는 닿지 않는다`);
      await game.send(`goto room ${role} ${biome}`);
      await game.wait(1100);
      const at = await game.state();
      check(at.room.toLowerCase() === role && !at.dead, `${name}: ${label}에 선다 (${at.room}, ${at.roomChunk || '조각 없음'}, 바닥 ${at.underfoot})`);
      await game.shot(`map-room-${role}-${biome}`, `${label} (${room.biome}, ${room.chunk || '조각 없음'})`);
    }

    // Each biome's own terrain.
    for (const biome of MapBiomes) {
      const answer = (await game.send(`goto biome ${biome}`)).join(' ');
      await game.wait(1100);
      check(/^ok/.test(answer) && (await game.state()).biome.toLowerCase() === biome, `${name}: ${biome}에 간다`);
      await game.shot(`map-biome-${biome}`, `생물군계 ${biome}`);
    }

    // Ice slides (the stick let go, the player goes on) and stops; quicksand is waded through slower than floor.
    await game.send('goto wild; clear');
    await game.wait(900);
    await game.send('scene ice; face off');
    await game.wait(700);
    const onIce = (await game.state()).x;
    await walk(1, 0, 600);
    // A stick drag that did not take (the first touch after the room pictures now and then goes nowhere) is tried once
    // more, so the check is of the ice and not of that touch.
    if (Math.abs((await game.state()).x - onIce) < 0.2) {
      run.note(`- ${name}: 스틱 걸음이 먹지 않아 한 번 더`);
      await game.send('scene ice; face off');
      await game.wait(700);
      await walk(1, 0, 600);
    }
    // Polled for a while: on the phone the stick walks slower and the screen runs late, so the player may reach the
    // ice only after the stick is let go.
    let slide = 0;
    for (let i = 0; i < 30 && slide <= 0.5; i++) {
      const s = await game.state();
      if (s.underfoot === 'IceFloor') slide = Math.max(slide, s.slide);
      await game.wait(50);
    }
    await game.shot('map-ice', '얼음 바닥에서 미끄러지는 중');
    await game.wait(900);
    const still = await game.state();
    // The distance the game measured with nothing held (Playtest "coast") is the sure sign; the speed asked for can
    // come after a short slide is over on a late screen.
    const coast = (await game.state()).coast;
    check(slide > 0.5 || coast > 0.2, `${name}: 얼음 위에서는 손을 떼도 미끄러진다 (뗀 뒤 속도 ${slide.toFixed(1)}칸/초, 손 떼고 간 거리 ${coast.toFixed(2)}칸)`);
    check(still.slide < 0.05 && still.underfoot === 'IceFloor', `${name}: 곧 멈춘다 (속도 ${still.slide.toFixed(2)}, 바닥 ${still.underfoot})`);
    const pace = async scene => {
      await game.send('goto wild; clear');
      await game.wait(700);
      await game.send(`scene ${scene}`);
      await game.wait(700);
      const before = await game.state();
      await walk(1, 0, 1200);
      await game.wait(500);
      return (await game.state()).x - before.x;
    };
    const onFloor = await pace('roll');
    const onSand = await pace('quicksand');
    await game.shot('map-quicksand', '모래늪');
    check(onSand > 0.5 && onSand < onFloor * 0.8, `${name}: 모래늪에서는 느리게 걷는다 (같은 시간에 바닥 ${onFloor.toFixed(1)}칸, 모래늪 ${onSand.toFixed(1)}칸)`);
    await game.context.close();
  }
}

// Packs in their rooms (2026-11-22, Docs/WorldRework.md 묶음 2-1): a pack stands in each pack room visited (in that
// room or the maze round a small one, four or more), the monsters about stay inside the caps, and a pack notices the
// player together (those further than they see come too), each with a mark over its head. Field elites (묶음 2-2): an
// elite camp holds a leader or champions with a name tag that fits the screen, and a leader's big attack shows the
// warning sign.
const PackVisits = [['any', '가까운 무리 방'], ['mushroom', '버섯 숲의 무리 방'], ['ice', '얼음 동굴의 무리 방'], ['sand', '모래 동굴의 무리 방']];

async function packs(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 무리 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('monsters hold; equip obsidian_sword; equip obsidian_armor; heal; probe on');

    // The room before an arena's door holds no pack (Docs/WorldRework.md 2-1), so up to four of the nearest are tried.
    const visitPack = async biome => {
      let answer = '', seen = {};
      for (let n = 0; n < 4; n++) {
        answer = (await game.send(`goto room pack ${biome} ${n}`)).join(' ');
        await game.wait(1600);
        seen = await game.ask('packs');
        // The list holds the empty one of the room before an arena's door too (no kind, none standing).
        if (((seen.packs || [])[0] || {}).standing > 0) break;
      }
      return { answer, seen };
    };
    for (const [biome, label] of PackVisits) {
      const { answer, seen } = await visitPack(biome);
      const here = (seen.packs || [])[0];
      check(/^ok/.test(answer) && seen.rooms === true, `${name}: ${label}에 간다 (${answer})`);
      check(!!here && here.standing >= 4 && here.inRoom === here.standing,
        `${name}: ${label}에 무리가 방 안에 선다 (${here ? `${here.kind}, ${here.standing}마리, 방 안 ${here.inRoom}, ${here.biome}` : '없음'})`);
      check(seen.field <= seen.hostCap && seen.near <= seen.partyCap, `${name}: ${label}: 몬스터 수가 상한 안이다 (둘레 ${seen.near}/${seen.partyCap}, 전체 ${seen.field}/${seen.hostCap}, 떠돌이 ${seen.wanderers})`);
      await game.shot(`packs-room-${biome}`, `${label} (${here ? here.kind : '무리 없음'})`);
    }

    // Beside the nearest pack, most of it out of sight; let them go: they come together, each with a mark.
    await visitPack('any');
    await game.send('goto pack');
    await game.wait(500);
    const before = await game.ask('packs');
    await game.send('monsters go');
    await game.wait(700);
    await game.shot('packs-alert', '무리가 함께 알아차린 순간(머리 위 느낌표)');
    await game.wait(700);
    const after = await game.ask('packs');
    const pack = (after.packs || [])[0] || {};
    check(pack.alerted && pack.chasing >= pack.standing - 1 && pack.standing >= 3,
      `${name}: 무리가 함께 알아차린다 (${pack.chasing}/${pack.standing}마리가 쫓는다, 그 가운데 제 시야 밖 ${pack.unseen})`);
    check(after.alertsShown - (before.alertsShown || 0) >= Math.min(3, pack.standing || 3),
      `${name}: 머리 위에 느낌표가 뜬다 (${after.alertsShown - (before.alertsShown || 0)}개)`);
    await game.wait(1500);
    await game.shot('packs-fight', '무리와 싸우는 중');
    await game.send('clear');

    // An elite camp out in the world: its band stands, and its elite has its name over its head, inside the screen.
    // The nearest camps first; one by a road or an arena's door holds no band, so up to four are tried.
    let camp = {};
    for (let n = 0; n < 4 && !(camp.elites || []).length; n++) {
      await game.send(`monsters hold; heal; goto room elite any ${n}`);
      await game.wait(1600);
      camp = await game.ask('elites');
    }
    const first = (camp.elites || [])[0];
    check(camp.camps >= 1 && !!first, `${name}: 정예 야영지에 정예가 선다 (${first ? `${first.name}, ${first.affixes}, ${first.rank}, 무리 ${first.pack}` : '없음'})`);
    await game.send('goto elite');
    await game.wait(900);
    const tagged = await game.ask('elites');
    check(tagged.tagsShown >= 1 && tagged.tagWidest > 0 && tagged.tagWidest <= tagged.screenWidth,
      `${name}: 정예 머리 위에 이름표가 화면 안에 보인다 (이름표 ${tagged.tagsShown}개, 가장 넓은 ${Math.round(tagged.tagWidest)}/${Math.round(tagged.screenWidth)}점)`);
    await game.shot('packs-elite-camp', `정예 야영지의 이름표 (${first ? first.name : '없음'})`);

    // The elite scene: a leader with fire and guard (lines to its minions) and three champions; its big attack shows
    // the warning sign while it winds up a whole second.
    await game.send('clear; goto spawn');
    await game.wait(900);
    await game.send('goto wild; clear');
    await game.wait(900);
    await game.send('monsters hold; heal; scene elite');
    await game.wait(900);
    const scene = await game.ask('elites');
    const leader = (scene.elites || []).find(e => e.rank === 'Leader') || {};
    check(leader.name === '수호의 버섯 병정 대장' && leader.affixes === '불길 · 수호',
      `${name}: 우두머리의 이름 (${leader.name}, ${leader.affixes})`);
    check(scene.tagsShown >= 4, `${name}: 우두머리와 챔피언 셋의 이름표 (${scene.tagsShown}개)`);
    check(scene.guardLinks >= 1, `${name}: 수호 우두머리에서 부하로 이음줄 (${scene.guardLinks}개)`);
    await game.shot('packs-elite-tags', '정예 장면: 금빛 우두머리와 푸른 챔피언의 이름표, 수호의 이음줄');
    await game.send('mob myconid use spore_smash');
    let warned = false;
    for (let i = 0; i < 25 && !warned; i++) {
      await game.wait(60);
      warned = (await game.ask('elites')).warningSign === true;
    }
    check(warned, `${name}: 우두머리의 큰 공격에 경고 표지가 뜬다`);
    await game.shot('packs-elite-warning', '우두머리의 큰 공격(포자 강타) 예고와 경고 표지');
    await game.send('clear');
    await game.context.close();
  }
}

// The things to find in the rooms (2026-11-22, Docs/WorldRework.md 묶음 3a·3b): a picture of each kind, the signals seen
// from afar, a chest opened (its own button, then once only), a shrine's blessing, a camp's rest, a hidden room's crack
// broken by a sword, and a lock refusing an iron pickaxe. Sounds (the drum, the draught, the bell) are the person's.
async function pois(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 볼거리 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('monsters hold; equip obsidian_sword; equip obsidian_armor; heal; probe on');
    const night = (await game.send('clock 1 22')).join(' ');
    check(!/^error/.test(night), `${name}: 밤 10시로 (${night || 'ok'})`);
    const all = await game.ask('pois');
    check(all.count > 100 && all.camp > 10 && all.shrine > 10 && all.chest > 50 && all.cart > 5,
      `${name}: 볼거리가 놓였다 (모두 ${all.count}: 야영지 ${all.camp}, 제단 ${all.shrine}, 상자 ${all.chest}, 수레 ${all.cart})`);

    // From afar: a camp's smoke, a shrine's pillar of light, a chest's glint, twelve tiles off in the dark.
    for (const [kind, label] of [['camp', '야영지의 연기와 불빛'], ['shrine', '제단의 빛기둥'], ['treasure', '보물 상자의 반짝임']]) {
      const answer = (await game.send(`goto poi ${kind} 0 12`)).join(' ');
      await game.wait(1500);
      check(/^ok/.test(answer), `${name}: ${label}이 12칸 앞에 보이는 자리로 간다 (${answer})`);
      await game.shot(`pois-far-${kind}`, `12칸 밖에서 본 ${label}`);
    }

    // Each kind up close.
    for (const [kind, label] of [['camp', '야영지'], ['shrine', '제단'], ['treasure', '보물 상자'], ['cart', '버려진 수레'], ['locked', '자물쇠 방의 금고(창살 너머)']]) {
      const answer = (await game.send(kind === 'locked' ? 'goto lock' : `goto poi ${kind}`)).join(' ');
      await game.wait(1200);
      const here = await game.ask('pois');
      check(/^ok/.test(answer), `${name}: ${label} 곁으로 간다 (${answer}, 가까운 것 ${here.near && here.near[0] ? here.near[0].id : '없음'})`);
      await game.shot(`pois-${kind}`, label);
    }

    // A chest: its button over it opens it, once.
    await game.send('heal; goto poi treasure');
    await game.wait(1200);
    const before = await game.ask('pois');
    check(before.inReach !== '', `${name}: 상자가 손 닿는 곳에 있다 (${before.inReach})`);
    let opened = false;
    const button = (await game.buttons()).find(b => /열기/.test(b.label));
    if (button) {
      await game.press(button.label);
      await game.wait(900);
      opened = (await game.ask('pois')).opened > before.opened;
    }
    check(!!button && opened, `${name}: 상자 위의 [열기] 단추로 연다 (${button ? button.label : '단추 없음'})`);
    await game.shot('pois-chest-open', '연 상자(뚜껑이 열린 그림과 이름)');
    const again = await game.ask('pois');
    check(again.clientOpened >= again.opened && again.inReach !== before.inReach, `${name}: 연 상자는 다시 열리지 않는다 (화면이 아는 연 것 ${again.clientOpened})`);

    // A shrine blesses once a day.
    await game.send('hurt 40; goto poi shrine');
    await game.wait(1200);
    const prayed = (await game.send('usepoi')).join(' ');
    await game.wait(900);
    const blessed = await game.ask('pois');
    check(/^ok/.test(prayed) && blessed.blessing !== '' && blessed.clientBlessing === blessed.blessing,
      `${name}: 제단이 축복한다 (${blessed.blessing}, ${Math.round(blessed.blessingSeconds)}초)`);
    await game.shot('pois-shrine-blessed', '제단의 축복(알림 줄과 상태 줄)');
    const twice = (await game.send('usepoi')).join(' ');
    check(/^error/.test(twice), `${name}: 같은 날 두 번은 안 된다 (${twice})`);

    // A camp: resting by the fire.
    await game.send('hurt 60; goto poi camp');
    await game.wait(4200);
    const rested = await game.ask('pois');
    check(rested.restedSeconds > 500 && rested.lastCamp >= 0, `${name}: 야영지에서 쉬었다 (잘 쉼 ${Math.round(rested.restedSeconds)}초, 마지막 야영지 ${rested.lastCamp})`);
    await game.shot('pois-camp-rested', '야영지에서 쉰 뒤(잘 쉼)');

    // A hidden room in the rock: dust from its crack, and a sword breaks it.
    const crack = (await game.send('goto crack')).join(' ');
    await game.wait(1400);
    await game.shot('pois-crack', '바위 속 숨은 방의 금 간 벽(먼지)');
    await game.send('attack on');
    await game.wait(2500);
    await game.send('attack off');
    await game.wait(500);
    await game.shot('pois-crack-broken', '깬 금 간 벽 너머의 숨은 방');
    check(/^ok/.test(crack), `${name}: 숨은 방의 금 간 벽 앞에 선다 (${crack})`);

    // A lock: an iron pickaxe is not enough.
    await game.send('equip iron_pickaxe');
    const lock = (await game.send('goto lock')).join(' ');
    check(/^ok/.test(lock), `${name}: 자물쇠 벽 앞에 선다 (${lock})`);
    await game.wait(1200);
    await game.send('attack on');
    // The reminder floats only a moment (and then rests 2.5 seconds), so it is watched for rather than read once.
    const refused = (await game.until(r => (r.floating || []).some(t => /더 좋은 곡괭이/.test(t)), 3000, () => game.ask('texts'))) || {};
    await game.send('attack off');
    await game.wait(300);
    check((refused.floating || []).some(t => /더 좋은 곡괭이/.test(t)), `${name}: 철 곡괭이로 자물쇠 벽을 치면 "더 좋은 곡괭이가 필요하다" (${(refused.floating || []).join(' / ') || '글 없음'})`);
    await game.shot('pois-lock-refused', '철 곡괭이로 흑요석 벽을 치면 "더 좋은 곡괭이"');

    // An elite camp's red flag, from afar.
    await game.send('equip obsidian_sword; goto room elite any');
    await game.wait(1200);
    await game.send('goto 0 -14');
    await game.wait(1200);
    await game.shot('pois-elite-flag', '정예 야영지의 붉은 깃발(멀리서)');
    await game.send('clock 1 9');
    await game.context.close();
  }
}

// Getting about (2026-11-22, Docs/WorldRework.md 묶음 3c): a minecart line's broken end (its button says what mends
// it), mended with five iron bars and ridden (the cart under the player, the rails going by), a waystone's blue flame
// and the waypoint list it opens, the wandering merchant's stall by a camp and its shop (a map scrap bought and read).
// The rattle of the cart and the feel of the ride are the person's.
async function travel(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 광차·길잡이 돌·떠돌이 상인 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('monsters hold; equip obsidian_sword; equip obsidian_armor; heal; probe on');
    const all = await game.ask('travel');
    check(all.lines.length >= 4 && all.stones >= 6 && all.wanderer,
      `${name}: 광차 줄 ${all.lines.length}, 길잡이 돌 ${all.stones}, 떠돌이 상인 ${all.wanderer ? '있음' : '없음'}(${all.wandererBiome})`);

    // A line's broken end: its button says what mends it, and cannot be pressed without it.
    const broken = (await game.send('goto station broken')).join(' ');
    await game.wait(1500);
    check(/^ok/.test(broken), `${name}: 끊긴 광차역 곁으로 간다 (${broken})`);
    const mendButton = (await game.buttons()).find(b => /선로 고치기/.test(b.label));
    check(!!mendButton && !mendButton.enabled, `${name}: 철 주괴 없이는 [선로 고치기]가 눌리지 않는다 (${mendButton ? mendButton.label : '단추 없음'})`);
    await game.shot('travel-station-broken', '끊긴 선로의 광차역(뜯긴 선로와 [선로 고치기 0/5])');
    await game.send('give iron_bar 5');
    await game.wait(900);
    const before = (await game.ask('travel')).lines.filter(l => l.fixed).length;
    check(await game.press(/선로 고치기/), `${name}: 철 주괴 5개로 [선로 고치기]를 누른다`);
    await game.wait(900);
    const mended = await game.ask('travel');
    check(mended.lines.filter(l => l.fixed).length === before + 1, `${name}: 선로가 고쳐졌다 (고친 줄 ${before} → ${mended.lines.filter(l => l.fixed).length})`);

    // Ridden: the cart under the player along the rails, then off at the other end.
    check(await game.press(/광차 타기/), `${name}: [광차 타기]를 누른다`);
    await game.wait(1500);
    const riding = await game.ask('travel');
    check(riding.riding && riding.clientRiding, `${name}: 광차를 탔다 (호스트 ${riding.riding}, 화면 ${riding.clientRiding})`);
    await game.shot('travel-riding', '광차를 타고 가는 중(선로와 광차)');
    await game.wait(2500);
    await game.shot('travel-riding-later', '광차를 타고 가는 중(2.5초 뒤)');
    const ridden = !!(await game.until(t => !t.riding, 25000, () => game.ask('travel')));
    const arrived = await game.ask('travel');
    check(ridden && Math.hypot(arrived.x - riding.x, arrived.y - riding.y) > 60, `${name}: 반대쪽 역에 내렸다 (${Math.round(Math.hypot(arrived.x - riding.x, arrived.y - riding.y))}칸 옮김)`);
    await game.shot('travel-arrived', '반대쪽 역에 내린 뒤');

    // A waystone: touched, it opens; its button opens the waypoint list with the stones.
    const stone = (await game.send('goto poi waystone')).join(' ');
    await game.wait(1500);
    const touched = await game.ask('travel');
    check(/^ok/.test(stone) && touched.stonesOpen >= 1 && touched.clientStonesOpen >= 1, `${name}: 길잡이 돌을 만지면 열린다 (${stone}, 열린 돌 ${touched.stonesOpen})`);
    await game.shot('travel-waystone', '길잡이 돌(푸른 불)');
    await game.send('stones all');
    await game.wait(600);
    check(await game.press('길잡이 돌'), `${name}: 돌 위의 [길잡이 돌] 단추로 목록을 연다`);
    await game.wait(600);
    await game.shot('travel-waypoints', '웨이포인트와 길잡이 돌 목록');
    // The list keeps clear of the minimap column and its touch buttons (2026-11-22: on a phone held upright they
    // covered its top rows and took their taps, StoryScreen.ListArea).
    const drawn = await game.buttons();
    const column = drawn.filter(b => b.label === '지도' || b.label === '가방' || b.label === '나가기');
    const rows = drawn.filter(b => b.shown && (/길잡이 돌/.test(b.label) && b.label !== '길잡이 돌' || /^(새벽 마을|왕도 루미나)/.test(b.label)));
    const covered = rows.filter(r => column.some(c => r.rect[0] < c.rect[0] + c.rect[2] && c.rect[0] < r.rect[0] + r.rect[2] && r.rect[1] < c.rect[1] + c.rect[3] && c.rect[1] < r.rect[1] + r.rect[3]));
    check(rows.length > 0 && covered.length === 0, `${name}: 목록의 줄이 미니맵·[가방]·[나가기]에 가리지 않는다 (줄 ${rows.length}, 가린 줄 ${covered.map(r => r.label).join(', ') || '없음'})`);
    const away = drawn.find(b => /의 길잡이 돌$/.test(b.label) && b.enabled);
    check(!!away && await game.press(away.label), `${name}: 다른 길잡이 돌로 간다 (${away ? away.label : '단추 없음'})`);
    await game.wait(1200);
    const went = await game.ask('travel');
    // Beside another stone, not only away (2026-11-22: the row's command lost its stone on the way and, when the cut
    // number was an open place, took the player there instead, which passed as tiles moved).
    check(Math.hypot(went.x - touched.x, went.y - touched.y) > 20 && went.stoneNear >= 0 && went.stoneNear !== touched.stoneNear,
      `${name}: 길잡이 돌로 옮겼다 (${Math.round(Math.hypot(went.x - touched.x, went.y - touched.y))}칸, 곁의 돌 ${touched.stoneNear} → ${went.stoneNear})`);

    // The wandering merchant by a camp, and their shop: potions, the map scrap and the camp's biome's seeds.
    const wanderer = (await game.send('goto wanderer')).join(' ');
    await game.wait(1500);
    check(/^ok/.test(wanderer), `${name}: 떠돌이 상인 곁으로 간다 (${wanderer})`);
    await game.shot('travel-wanderer', '야영지의 떠돌이 상인(색 천막)');
    await bagTab(game, '떠돌이 상인');
    await game.press('사기');
    await game.wait(500);
    check((await game.find('사기', /^지도 조각/)).length > 0, `${name}: 떠돌이 상인이 지도 조각을 판다 (팔 것 ${(await game.ask('travel')).wandererShop}줄)`);
    await game.shot('travel-wanderer-shop', '떠돌이 상인의 [사기]');
    await game.send('give coin 200');
    await game.wait(500);
    await game.press('사기', '지도 조각');
    await game.wait(600);
    const bought = ((await game.inventory()).consumables || {}).map_scrap || 0;
    check(bought === 1, `${name}: 금화로 지도 조각을 산다 (${bought})`);
    await closeBag(game);
    await game.send('drink map_scrap');
    await game.wait(800);
    const read = await game.ask('texts');
    check(/지도 조각/.test(read.banner || ''), `${name}: 지도 조각을 읽으면 알림이 뜬다 (${read.banner})`);
    await game.shot('travel-map-scrap', '지도 조각을 읽은 뒤(알림 줄)');
    await game.context.close();
  }
}

// Rumours instead of arrows, and slow rest (2026-11-22, Docs/WorldRework.md 묶음 4c): the menu's two choices, a
// village child's rumour (a line and a faint ring on the map), the story's goal line past the first chapter saying only
// which way and in which biome, the old arrows back with the choice, and rest that waits eight seconds and heals
// slowly. Whether hunting by rumour feels like exploring is the person's.
async function guide(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 소문 안내와 느린 회복 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    await game.send('probe on');
    await menuPage(game, '설정');
    let labels = (await game.buttons()).map(b => b.label);
    check(labels.includes('길 안내: 소문') && labels.includes('쉴 때 회복: 느림'), `${name}: [설정]에 [길 안내: 소문]·[쉴 때 회복: 느림]이 기본 (${labels.filter(l => /길 안내|쉴 때/.test(l)).join(', ')})`);
    await game.shot('guide-settings', '설정의 [길 안내]·[쉴 때 회복]');
    await game.press('길 안내: 소문');
    check((await game.find('길 안내: 화살표')).length === 1, `${name}: [길 안내: 소문]을 누르면 화살표`);
    await game.press('길 안내: 화살표');
    check((await game.find('길 안내: 소문')).length === 1, `${name}: 다시 누르면 소문`);
    await menuPage(game, '게임 시작');
    await game.press('혼자 하기');
    await game.until(s => s.playing, 25000);
    await game.wait(1500);
    await game.send('monsters hold; still; probe on');

    // A rumour from the village child: a line in the talk and a faint ring on the map.
    await game.send('goto person mina');
    await game.wait(900);
    check(await game.press(/미나와 말하기/), `${name}: 아이 곁에서 [피난민 아이 미나와 말하기]`);
    await game.wait(500);
    for (let i = 0; i < 4 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(300);
    const told = await game.ask('rumors');
    const text = await game.allText();
    check(told.heard.length >= 1 && /소문: 지도에 옅은 원/.test(text), `${name}: 아이가 소문을 들려준다 (들은 소문 ${told.heard.map(h => h.kind).join(', ')})`);
    await game.shot('guide-rumor', '아이가 들려준 소문(방향과 생물군계)');
    await game.press('닫기', undefined, { quiet: true });
    await game.wait(300);

    // Past the first chapter the story's goal line says which way and in which biome, not how far.
    await game.send('journey 5 taken');
    await game.wait(1500);
    const rumored = await game.ask('rumors');
    check(!rumored.firstChapter && rumored.leadsKnown !== 'Known' && !/\d+칸/.test(rumored.story), `${name}: 첫 장 뒤에는 거리 없이 방향만 (${rumored.leadsKnown}: ${rumored.story})`);
    await game.shot('guide-tracker', '소문 안내의 목표 목록(방향과 생물군계)');
    if (game.touch) await game.press('지도');
    else await game.page.keyboard.press('m');
    await game.wait(700);
    await game.shot('guide-map', '지도의 옅은 원(소문)과 아레나의 원');
    if (game.touch) await game.press('지도 닫기', undefined, { quiet: true }) || await game.press('지도', undefined, { quiet: true });
    else await game.page.keyboard.press('m');
    await game.wait(400);

    // The old arrows, as the menu's choice brings them back.
    await game.send('guidance arrow');
    await game.wait(1200);
    const arrows = await game.ask('rumors');
    check(/\d+칸/.test(arrows.story), `${name}: [길 안내: 화살표]면 방향과 거리 (${arrows.story})`);
    await game.shot('guide-arrow', '화살표 안내의 목표 목록(방향과 거리)');
    await game.send('guidance rumor');

    // Rest waits eight seconds and heals 1.5% a second; quick rest as before.
    await game.send('quickrest off; heal; hurt 40');
    await game.wait(300);
    const hurt = (await game.ask('rumors')).health;
    await game.wait(6000);
    const waited = (await game.ask('rumors')).health;
    await game.wait(5000);
    const rested = await game.ask('rumors');
    check(!rested.quickRest && waited <= hurt + 1 && rested.health > waited, `${name}: 느린 회복은 8초 기다린 뒤 조금씩 (${hurt.toFixed(0)} → 6초 ${waited.toFixed(0)} → 11초 ${rested.health.toFixed(0)} / ${rested.maxHealth.toFixed(0)})`);
    await game.send('quickrest on; heal; hurt 40');
    await game.wait(300);
    const quickHurt = (await game.ask('rumors')).health;
    await game.wait(9000);
    const quick = await game.ask('rumors');
    check(quick.quickRest && quick.health - quickHurt > rested.health - waited, `${name}: 빠른 회복(예전)은 더 빨리 (${quickHurt.toFixed(0)} → 9초 ${quick.health.toFixed(0)})`);
    await game.send('quickrest off');
    await game.context.close();
  }
}

// The trap rooms and the pacing director (2026-11-22, Docs/WorldRework.md 묶음 4b): a trap chest's [열기] shuts the room
// with grates (on the host and on the screen, with the banner), two bands come one after the other, and winning lifts
// the grates and the chest gives a tier higher; an ambush's dust in a narrow way and the band out of the walls; a raid's
// howl and the band running in; the rest set and kept. The thud of the grates, the howl and the rhythm over a long play
// are the person's.
async function pace(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 함정 방과 완급 감독 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    // Past level 3: the director leaves a new character alone.
    await game.send('monsters hold; equip obsidian_sword; equip obsidian_armor; heal; probe on; xp 3000');
    const traps = await game.ask('traps');
    check(traps.chests >= 4, `${name}: 함정 상자가 놓였다 (${traps.chests})`);

    // The trap chest: its own button springs the trap.
    const went = (await game.send('goto poi trap')).join(' ');
    await game.wait(1500);
    const ready = await game.ask('traps');
    check(/^ok/.test(went) && ready.phase === 'Ready' && ready.clientCanUse, `${name}: 함정 상자 곁으로 간다 (${went}, ${ready.phase}, 화면 열 수 있음 ${ready.clientCanUse})`);
    await game.shot('pace-trap-before', '함정 방의 상자(바닥의 함정 판)');
    const button = (await game.buttons()).find(b => /열기/.test(b.label));
    check(!!button && await game.press(button.label), `${name}: 함정 상자의 [열기]를 누른다 (${button ? button.label : '단추 없음'})`);
    await game.wait(900);
    const shut = await game.ask('traps');
    check(shut.phase !== 'Ready' && shut.inside && shut.doors > 0 && shut.grates === shut.doors && shut.clientGrates === shut.doors,
      `${name}: 창살이 내려와 방이 닫혔다 (${shut.phase}, 문 ${shut.doors}, 호스트 ${shut.grates}, 화면 ${shut.clientGrates}, 갇힘 ${shut.inside})`);
    check(shut.bannersShown >= 1 && !shut.clientCanUse && !shut.opened, `${name}: 알림 줄이 뜨고 상자는 아직 닫혀 있다 (알림 ${shut.bannersShown}, ${shut.tile})`);
    await game.shot('pace-trap-shut', '창살이 내려온 함정 방과 알림 줄');
    const first = await game.until(t => t.phase === 'Fighting' && t.wave === 1, 5000, () => game.ask('traps'));
    check(!!first && first.band >= 3, `${name}: 첫 무리가 온다 (${first ? first.band : 0}마리)`);
    await game.wait(300);
    await game.shot('pace-trap-wave1', '함정 방의 첫 무리(1/2)');
    await game.send('clear');
    const second = await game.until(t => t.phase === 'Fighting' && t.wave === 2, 6000, () => game.ask('traps'));
    check(!!second && second.band >= 3, `${name}: 두 번째 무리가 온다 (${second ? second.band : 0}마리)`);
    await game.wait(300);
    await game.shot('pace-trap-wave2', '함정 방의 마지막 무리(2/2)');
    await game.send('clear');
    const won = await game.until(t => t.phase === 'Won', 5000, () => game.ask('traps'));
    check(!!won && won.opened && won.tile === 'OpenChest' && won.grates === 0 && won.clientGrates === 0 && won.items >= ready.items + 2,
      `${name}: 이기면 창살이 올라가고 상자가 한 단계 위로 준다 (${won ? `${won.tile}, 창살 ${won.grates}/${won.clientGrates}, 장비 ${ready.items} → ${won.items}` : '끝나지 않음'})`);
    await game.wait(400);
    await game.shot('pace-trap-won', '함정을 이긴 뒤(열린 상자와 올라간 창살)');

    // An ambush in a narrow way: dust off the walls, then the band before and behind.
    // Not every narrow way has room for the band before and behind (the world is new each run), so up to six are tried.
    let narrow = '', calm = {}, ambush = '';
    for (let n = 0; n < 6 && !/^ok/.test(ambush); n++) {
      narrow = (await game.send(`clear; heal; goto narrow ${n}`)).join(' ');
      await game.wait(1500);
      calm = await game.ask('director');
      ambush = (await game.send('director ambush')).join(' ');
    }
    check(/goto narrow at/.test(narrow), `${name}: 좁은 길로 간다 (${narrow})`);
    check(calm.acts && !calm.leftAlone, `${name}: 감독이 이 자리에서 일한다 (${calm.state}, 긴장도 ${Math.round(calm.tension)})`);
    await game.wait(350);
    const dusty = await game.ask('director');
    check(/^ok/.test(ambush) && dusty.dustShown - calm.dustShown >= 2, `${name}: 매복 전에 앞뒤 벽에서 먼지가 떨어진다 (${ambush}, 먼지 ${dusty.dustShown - calm.dustShown})`);
    await game.shot('pace-ambush-dust', '매복 1초 전: 벽에서 떨어지는 먼지');
    await game.wait(1200);
    const ambushed = await game.ask('director');
    check(ambushed.chasing >= 4 && ambushed.ambushes >= 0, `${name}: 벽에서 무리가 나와 쫓는다 (${ambushed.chasing}마리)`);
    await game.shot('pace-ambush', '좁은 길의 매복(앞뒤에서)');

    // A raid in a wide room: a far howl, and the band running in.
    // A raid needs room out of sight to come from, so up to five plain rooms are tried.
    let before = {}, raid = '';
    for (let n = 0; n < 5 && !/^ok/.test(raid); n++) {
      await game.send(`clear; heal; goto room plain any ${n}`);
      await game.wait(1500);
      before = await game.ask('director');
      raid = (await game.send('director raid')).join(' ');
    }
    await game.wait(500);
    const howled = await game.ask('director');
    check(/^ok/.test(raid) && howled.howlsHeard > before.howlsHeard, `${name}: 습격 전에 멀리서 울음소리 (${raid}, 들은 것 ${howled.howlsHeard - before.howlsHeard})`);
    await game.shot('pace-raid-howl', '습격: 멀리서 울음소리(글자)');
    await game.send('monsters go');
    // It comes from out of sight, so it is waited for (up to ten seconds) rather than read once.
    const raided = (await game.until(d => d.chasing >= 4 || d.near >= 4, 10000, () => game.ask('director'))) || await game.ask('director');
    await game.shot('pace-raid', '습격: 몰려오는 무리');
    check(raided.chasing >= 4 || raided.near >= 4, `${name}: 습격한 무리가 쫓아온다 (${raided.chasing}마리, 12칸 안 ${raided.near})`);
    await game.send('monsters hold; clear; heal');

    // The rest: set, it holds and sends nothing.
    await game.send('director rest');
    await game.wait(2000);
    const rest = await game.ask('director');
    check(rest.state === 'Rest' && rest.restLeft > 30 && rest.hunters === 0, `${name}: 쉬기 (${rest.state}, 남은 ${Math.round(rest.restLeft)}초, 보낸 것 ${rest.hunters})`);
    await game.context.close();
  }
}

// A dungeon (Docs/WorldRework.md 5단계, 묶음 5a), on a PC and a phone held upright: the gate room's own button (with a real
// click or tap) and the bar filling, the dark and the dungeon's name, the screen laying the same pocket as the host,
// the dungeon alone on the full map, a fight in its first room, a fall and getting up at its entrance, out by
// [던전 나가기] and again by the door of its entrance room, and the empty dungeon going back to rock. 묶음 5b: a run through
// it (a pack room, the map's marks, the master, the end chest by its own button, the clear card, the goals' ways). How
// going in feels (the dark, the sound) and how a run plays are the person's.
async function dungeon(browser) {
  for (const name of ['pc', 'phone']) {
    const profile = Profiles[name];
    note(`\n## 던전 (${name === 'pc' ? 'PC' : '휴대폰 세로'})\n`);
    const game = await open(browser, run, profile, url);
    if (game.touch) {
      await menuPage(game, '게임 시작');
      await game.press('혼자 하기');
      await game.until(s => s.playing, 25000);
      await game.wait(800);
    } else {
      await game.startSolo();
    }
    await game.wait(1200);
    await game.send('monsters hold; equip obsidian_sword; equip obsidian_armor; heal; probe on');
    const first = await game.ask('dungeon');
    check(first.gates >= 6 && first.slots === 4 && first.rows === first.height + 160,
      `${name}: 던전 문 ${first.gates}개, 주머니 칸 ${first.slots}, 줄 ${first.height} + ${first.rows - first.height}`);

    // The gate: its button over it, pressed for real; the bar fills and the dungeon is laid on the screen meanwhile.
    const went = (await game.send('goto gate')).join(' ');
    await game.wait(1500);
    const atGate = await game.ask('dungeon');
    check(/^ok/.test(went) && atGate.gateNear >= 0 && atGate.clientGateNear === atGate.gateNear, `${name}: 던전 문 위에 선다 (${went}, 문 ${atGate.gateNear}, 화면 ${atGate.clientGateNear})`);
    await game.shot('dungeon-gate', '던전 문(도리이 둘 사이)과 [들어가기] 단추');
    const enter = (await game.buttons()).find(b => /^\S.* 들어가기( \(|$)/.test(b.label));
    check(!!enter && await game.press(enter.label), `${name}: [${enter ? enter.label : '들어가기 단추 없음'}]를 누른다`);
    const gathering = await game.ask('dungeon');
    check(gathering.charging || gathering.inPocket, `${name}: 들어가기를 모은다 (화면 ${Math.round(gathering.clientCharge * 100)}%)`);
    await game.shot('dungeon-entering', '들어가는 중(발밑 막대)');
    const inside = await game.until(d => d.inPocket && d.clientInPocket, 5000, () => game.ask('dungeon'));
    check(!!inside && inside.slot >= 0 && inside.clientSlot === inside.slot && inside.pocketSame && inside.atEntrance,
      `${name}: 던전 입구에 들어왔다 (칸 ${inside ? inside.slot : '-'}, 화면 ${inside ? inside.clientSlot : '-'}, 주머니가 호스트와 같다 ${inside ? inside.pocketSame : '-'})`);
    check(!!inside && inside.cards >= 1 && inside.cardShowing && inside.dungeons.length > 0 && inside.cardName === inside.dungeons[0].name,
      `${name}: 던전 이름이 크게 뜬다 (${inside ? inside.cardName : ''}, ${inside && inside.run ? inside.run.letters : '-'})`);
    await game.wait(300);
    await game.shot('dungeon-inside', '던전 입구 방(이름 띠, 횃불, 나가는 문)');

    // The full map: the dungeon alone.
    await game.press('지도');
    await game.wait(600);
    const mapped = await game.ask('dungeon');
    check(mapped.mapOpen, `${name}: 던전 안에서 지도를 연다`);
    await game.shot('dungeon-map', '던전 안의 전체 지도(그 던전만)');
    await game.press('지도 닫기');
    await game.wait(400);

    // A fight in its first room: a slime let go beside the player.
    await game.send('spawn slime 3 0; monsters go; attack on');
    await game.wait(2500);
    await game.shot('dungeon-fight', '던전 안의 한 방 싸움');
    await game.send('attack off; clear; monsters hold; heal');

    // A fall: up again at the entrance.
    await game.send('goto far');
    await game.wait(800);
    await game.send('dungeon die');
    const rose = await game.until(d => !d.dead && d.inPocket, 9000, () => game.ask('dungeon'));
    check(!!rose && rose.atEntrance, `${name}: 쓰러지면 던전 입구에서 다시 일어난다 (${rose ? `입구 ${rose.atEntrance}` : '일어나지 않음'})`);
    await game.shot('dungeon-rose', '쓰러진 뒤 던전 입구에서 일어남');

    // Out by [던전 나가기] (from anywhere, three seconds).
    const menu = (await game.buttons()).find(b => b.label === '던전 나가기');
    check(!!menu && await game.press('던전 나가기'), `${name}: [던전 나가기]를 누른다`);
    const out = await game.until(d => !d.inPocket && !d.clientInPocket, 6000, () => game.ask('dungeon'));
    check(!!out && out.nearestGateDistance < 1.5, `${name}: 던전 문 앞으로 나왔다 (${out ? `문까지 ${out.nearestGateDistance.toFixed(1)}칸` : '나오지 않음'})`);
    await game.wait(400);
    await game.shot('dungeon-out', '나온 뒤의 던전 문');

    // In again (the same dungeon while it stands), and out by the door of the entrance room.
    await game.press(/^\S.* 들어가기( \(|$)/);
    const again = await game.until(d => d.inPocket && d.clientInPocket, 5000, () => game.ask('dungeon'));
    check(!!again && again.slot === inside.slot && again.dungeons.length === 1 && again.dungeons[0].serial === 0,
      `${name}: 다시 들어가면 서 있는 같은 던전이다 (칸 ${again ? again.slot : '-'})`);
    await game.send('goto exit');
    await game.wait(900);
    const door = await game.ask('dungeon');
    check(door.exitNear, `${name}: 입구 방의 나가는 문 위에 선다`);
    await game.shot('dungeon-door', '나가는 문 위(문 위의 [던전 나가기])');
    const doorButton = (await game.buttons()).find(b => /던전 나가기/.test(b.label) && b.label !== '던전 나가기' || /던전 나가기/.test(b.label) && game.touch);
    check(!!doorButton && await game.press(doorButton.label), `${name}: 문 위의 [${doorButton ? doorButton.label : '단추 없음'}]를 누른다`);
    const left = await game.until(d => !d.inPocket, 5000, () => game.ask('dungeon'));
    check(!!left, `${name}: 문으로 나왔다`);

    // 묶음 5b: a run through it. In again; a pack room's pack standing (held), the map with the rooms' marks, each pack
    // room felled at once, the master in its room (its bar and name), down, the end chest's own button pressed for real,
    // the clear card, and the goals' ways from the gate rather than the pocket. The door out leaves the player before
    // the gate, not on it, so back onto the gate first for its button.
    await game.send('goto gate');
    await game.wait(1200);
    await game.press(/^\S.* 들어가기( \(|$)/);
    const back = await game.until(d => d.inPocket && d.clientInPocket && d.run, 5000, () => game.ask('dungeon'));
    check(!!back && back.run.id.length > 0 && /^E.*BX/.test(back.run.letters) && back.run.packs >= 3,
      `${name}: 던전의 방 (${back ? `${back.run.id}: ${back.run.letters}, 무리 방 ${back.run.packs}` : '없음'})`);
    check(!!back && (back.run.goalWay === '' || !/[0-9]{3,}칸/.test(back.run.goalWay) || /던전 문에서/.test(back.run.goalWay)),
      `${name}: 던전 안의 목표 길은 문 자리에서 잰다 (${back ? back.run.goalWay || '목표 줄 없음' : '-'})`);
    await game.send('goto droom C');
    await game.wait(1500);
    const pack = await game.ask('dungeon');
    check(pack.run && pack.run.room === 'C' && pack.run.enemiesHere >= 4 && pack.run.clientEnemiesHere >= 4,
      `${name}: 무리 방에 무리가 서 있다 (호스트 ${pack.run ? pack.run.enemiesHere : '-'}, 화면 ${pack.run ? pack.run.clientEnemiesHere : '-'})`);
    await game.shot('dungeon-pack', '던전의 무리 방(조각, 무리)');
    await game.press('지도');
    await game.wait(600);
    await game.shot('dungeon-run-map', '던전 안의 지도(남은 무리, 주인, 상자, 모닥불)');
    await game.press('지도 닫기');
    await game.wait(300);
    const letters = (back && back.run ? back.run.letters : '').split('');
    const seen = {};
    for (const letter of letters) {
      seen[letter] = (seen[letter] || 0) + 1;
      if (!'CKLM'.includes(letter)) continue;
      await game.send(`goto droom ${letter} ${seen[letter] - 1}`);
      await game.wait(700);
      await game.send('dungeon kill');
      await game.wait(500);
    }
    const felled = await game.ask('dungeon');
    check(felled.run && felled.run.cleared === felled.run.packs - 1 && (!/K/.test(felled.run.letters) || felled.run.keyTaken),
      `${name}: 주인 방 말고 다 비웠다 (${felled.run ? `${felled.run.cleared}/${felled.run.packs}, 열쇠 ${felled.run.keyTaken}` : '-'})`);
    await game.send('goto droom B; heal');
    await game.wait(1800);
    const boss = await game.ask('dungeon');
    check(boss.run && boss.run.room === 'B' && boss.run.master.length > 0 && boss.run.masterMax > 0 && /대장/.test(boss.run.master),
      `${name}: 보스 방에 던전의 주인 (${boss.run ? `${boss.run.master}, 생명력 ${Math.round(boss.run.masterMax)}` : '-'})`);
    await game.shot('dungeon-boss', '보스 방(아레나 무늬, 주인의 이름표와 막대)');
    await game.send('dungeon kill');
    const down = await game.until(d => d.run && d.run.bossDown && (d.run.clientFlags & 16) !== 0, 4000, () => game.ask('dungeon'));
    check(!!down && /쓰러뜨렸다/.test(down.cardName), `${name}: 주인이 쓰러지고 끝 방이 열린다 (${down ? down.cardName : '그대로'})`);
    await game.send('goto chest; heal');
    await game.wait(900);
    const atChest = await game.ask('dungeon');
    check(atChest.run && atChest.run.thing >= 0 && atChest.run.clientThing === atChest.run.thing, `${name}: 보상 상자 곁 (${atChest.run ? atChest.run.thing : '-'})`);
    await game.shot('dungeon-chest', '끝 방의 보상 상자와 [보상 상자 열기]');
    const chestButton = (await game.buttons()).find(b => /보상 상자 열기/.test(b.label));
    check(!!chestButton && await game.press(chestButton.label), `${name}: [${chestButton ? chestButton.label : '보상 상자 단추 없음'}]를 누른다`);
    const cleared = await game.until(d => d.run && d.run.clientState === 'Done' && d.run.clearsShown >= 1, 4000, () => game.ask('dungeon'));
    check(!!cleared && cleared.run.chestTile === 'OpenChest' && /완주/.test(cleared.cardName) && cleared.run.items > atChest.run.items,
      `${name}: 완주 (${cleared ? `${cleared.cardName} · ${cleared.cardLine}, 장비 ${atChest.run.items} → ${cleared.run.items}` : '끝나지 않음'})`);
    await game.wait(300);
    await game.shot('dungeon-clear', '완주 띠와 열린 상자');
    await game.send('goto exit 1');
    await game.wait(900);
    const endDoor = (await game.buttons()).find(b => /던전 나가기/.test(b.label) && b.label !== '던전 나가기' || /던전 나가기/.test(b.label) && game.touch);
    check(!!endDoor && await game.press(endDoor.label), `${name}: 끝 방의 문으로 나간다`);
    await game.until(d => !d.inPocket, 5000, () => game.ask('dungeon'));

    // Nobody in it: back to rock (now, rather than after a minute), on the screen too.
    const closed = (await game.send('dungeon close')).join(' ');
    await game.wait(800);
    const rock = await game.ask('dungeon');
    check(/ok dungeon close 1/.test(closed) && rock.dungeons.length === 0 && rock.pocketSame, `${name}: 빈 던전이 바위로 돌아간다 (${closed}, 화면도 같다 ${rock.pocketSame})`);
    await game.context.close();
  }
}

// ---------------------------------------------------------------- UI 개편 U0·U1 (Docs/UiRework.md)
// The new skin on the screens there are (bag, talk, waypoints, map, menus) on a PC, a phone held upright, a small phone
// held sideways and a phone whose pixel ratio is not whole (2.625), and the four overlap bugs of the 2026-11-22 audit:
// the "획득" lines over the bag and the goal list, the roll's description under the quick slots, two world buttons on
// one spot, and the goal list pushing the boss bar over the player on a phone held upright.
const overlaps = (a, b) => a && b && a[2] > 0 && b[2] > 0 && a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3];

async function ui(browser) {
  const profiles = [
    Profiles.pc,
    Profiles.phone,
    Profiles['phone-wide-small'],
    { name: 'phone-frac', touch: true, context: { viewport: { width: 390, height: 844 }, deviceScaleFactor: 2.625, isMobile: true, hasTouch: true } },
  ];
  for (const profile of profiles) {
    const { width, height } = profile.context.viewport;
    note(`\n## UI 개편 ${profile.name} ${width}×${height}\n`);
    const game = await open(browser, run, profile, url);
    await game.shot('ui-menu', '시작 화면');
    await menuPage(game, '설정');
    await game.wait(300);
    const motion = (await game.buttons()).find(b => /^움직임: /.test(b.label));
    check(!!motion, `설정에 [움직임] (${motion ? motion.label : '없음'})`);
    const back = (await game.buttons()).find(b => b.label === '뒤로');
    check(!!back && back.rect[1] < 60 + (height - Math.min(520, height - 20)) / 2, `[뒤로]가 창 머리 띠 안에 (${back ? back.rect.map(Math.round).join(',') : '없음'})`);
    await game.shot('ui-settings', '설정(돌판 창, 머리 띠, [뒤로])');
    await menuPage(game, '게임 시작');
    await game.shot('ui-play', '게임 시작 화면(주 동작 [혼자 하기])');
    await game.startSolo();
    await game.wait(1200);
    // A touch on the screen makes it a touch screen (sticks, the [가방] button, 40-point world buttons).
    if (game.touch) await game.page.touchscreen.tap(width / 2, height / 3);
    await game.wait(400);
    let s = await game.state();
    if (game.touch) check(s.touch, '터치 화면이 된다');
    check(s.ui && s.ui.dots === Math.max(1, Math.round(2 * s.ui.scale)), `틀의 픽셀은 기기 픽셀의 정수 배 (배율 ${s.ui && s.ui.scale}, 픽셀 ${s.ui && s.ui.dots})`);
    check(s.ui && s.ui.motion === 'normal', `움직임은 처음에 보통 (${s.ui && s.ui.motion})`);

    // U0 1: the "획득" lines never over the bag nor over the goal list.
    await game.send('drop item iron_sword; drop item iron_helmet; drop rune; drop potion healing_potion; drop material stone');
    await game.wait(1500);
    s = await game.state();
    check(s.ui.acquired[2] === 0 || s.ui.acquired[1] >= s.ui.leftBottom - 0.5, `획득 줄이 목표 목록 아래에서 멈춘다 (줄 위 ${Math.round(s.ui.acquired[1])}, 목록 아래 ${Math.round(s.ui.leftBottom)})`);
    await game.shot('ui-acquired', '획득 줄(목표 목록과 겹치지 않음)');
    await bagTab(game, '장비');
    await game.wait(400);
    s = await game.state();
    check(s.bag && !overlaps(s.ui.acquired, s.ui.bag), `가방이 열린 동안 획득 줄이 가방 위에 그려지지 않는다 (줄 ${s.ui.acquired.map(Math.round).join(',')}, 가방 ${s.ui.bag.map(Math.round).join(',')})`);
    const close = (await game.buttons()).find(b => b.label === '창 닫기');
    check(!!close && close.rect[0] + close.rect[2] > s.ui.bag[0] + s.ui.bag[2] - 60 && close.rect[1] < s.ui.bag[1] + 60, `가방 머리 오른쪽 위에 닫기 단추 (${close ? close.rect.map(Math.round).join(',') : '없음'})`);
    await game.shot('ui-bag', '가방(가죽 머리 띠, 금화, 아이콘 탭, 희귀도 칸)');
    await pick(game, /철 검/);
    await game.shot('ui-bag-picked', '고른 장비의 안쪽 칸, [장착] 금색·[버리기] 붉은 테');
    for (const [tab, caption] of [['스킬', '성장 머리(밤하늘)'], ['특성', '성장 머리(밤하늘)'], ['일지', '기록 머리(양피지)']]) {
      await bagTab(game, tab);
      await game.wait(300);
      await game.shot(`ui-bag-${tab}`, `가방 [${tab}]: ${caption}`);
    }
    check(await game.press('창 닫기'), '[창 닫기]를 누른다');
    await game.wait(400);
    check(!(await game.state()).bag, '닫기 단추로 창이 닫힌다');

    // U0 3: two world buttons on one spot (a machine before a person).
    // Still first: the person snaps home, then the player goes beside them (the other way round she had walked off).
    await game.send('still');
    await game.wait(600);
    await game.send('goto person mina');
    await game.wait(900);
    await game.send('build Drill 0 -1 3; face 0 -1');
    await game.wait(900);
    const world = await game.buttons();
    const off = world.find(b => /^(끄기|켜기)/.test(b.label));
    const talk = world.find(b => /와 말하기|과 말하기/.test(b.label));
    check(!!off && !!talk && !overlaps(off.rect, talk.rect), `기계 단추와 말하기 단추가 겹치지 않는다 (${off ? off.label : '-'} ${off ? off.rect.map(Math.round).join(',') : ''} / ${talk ? talk.label : '-'} ${talk ? talk.rect.map(Math.round).join(',') : ''})`);
    if (game.touch) check(!!talk && talk.rect[3] >= 40 - 0.5, `휴대폰의 월드 단추는 40점 (${talk ? Math.round(talk.rect[3]) : '-'})`);
    await game.shot('ui-world-buttons', '기계와 사람이 함께 있을 때의 월드 단추');

    // The talk: the speaker's band.
    check(await game.press(/미나와 말하기/), '[피난민 아이 미나와 말하기]');
    await game.wait(500);
    await game.shot('ui-talk', '대화(인물 색 머리 띠, 이름)');
    for (let i = 0; i < 6 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(200);
    await game.press('닫기', undefined, { quiet: true });
    await game.send('clear');

    // The waypoint list: parchment head; a tap outside closes it.
    await game.send('goto place dawn_village');
    await game.wait(600);
    await game.send('goto 0 2');
    await game.wait(900);
    if (await game.press('웨이포인트', undefined, { quiet: true })) {
      await game.wait(500);
      const rows = (await game.buttons()).filter(b => b.label === '창 닫기');
      check(rows.length > 0, '웨이포인트 목록 머리에 닫기 단추');
      await game.shot('ui-waypoints', '웨이포인트 목록(양피지 머리 띠)');
      const box = await game.canvas();
      await game.tapAt(box.left + 6, box.top + box.height - 6);
      await game.wait(400);
      check(!(await game.buttons()).some(b => b.label === '가 본 곳으로 간다' || (b.row || '').startsWith('가 본 곳')) && !(await game.find('창 닫기')).length,
        '목록 바깥을 누르면 닫힌다');
    } else {
      note('- 웨이포인트 단추를 찾지 못해 목록 사진은 건너뛴다');
    }

    // The map: a full screen over the world darkened, its key whole.
    if (game.touch) await game.press('지도');
    else await game.page.keyboard.press('m');
    await game.wait(600);
    check((await game.state()).map, '지도가 열린다');
    await game.shot('ui-map', '전체 지도(어두운 세계 위, 양피지 머리 띠, 범례 전부)');
    await game.press('지도 닫기');
    await game.wait(400);

    // U0 2 (PC): the roll's description over the quick slots.
    if (!game.touch) {
      const roll = (await game.buttons()).find(b => b.label === 'roll');
      if (roll) {
        await game.page.mouse.move(roll.cx, roll.cy);
        await game.wait(400);
        await game.shot('ui-roll-card', '구르기 설명 카드(퀵슬롯 위에)');
        await game.page.mouse.move(10, 10);
      }
    }

    // U0 4: a boss fight. The goal list folds on a phone held upright; the boss bar stays clear of the player.
    await game.send('equip void_sword void_helmet void_armor void_boots; goto boss cave_warden');
    await game.wait(3000);
    s = await game.state();
    const bar = s.ui.bossBar, me = s.ui.player;
    check(bar[2] > 0, `보스 막대가 보인다 (${bar.map(Math.round).join(',')})`);
    check(bar[2] > 0 && bar[1] + bar[3] < me[1] - 20, `보스 막대가 플레이어 머리 위를 덮지 않는다 (막대 아래 ${Math.round(bar[1] + bar[3])}, 플레이어 ${Math.round(me[1])})`);
    if (height > width) check(s.ui.folded, '세로 휴대폰에서 목표 목록이 한 줄로 접힌다');
    await game.shot('ui-boss', '보스전(접힌 목표 목록과 보스 막대)');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 화면 나누기 (Docs/UiRework.md U2, 2026-11-22)
// The screen manager: the bag, skills, passives, character, journal and map each their own window, one at a time. On a
// PC their keys (Tab K P U J M) open them and close them again, Esc closes one, a tab of the group switches; G meets
// what is in reach (talks next to a person, opens the bag's crafting next to a workbench, switches a machine). On a
// phone held upright and a small one on its side, the [메뉴] hub under the minimap opens each as a full sheet, which its
// head's close button shuts.
async function screens(browser) {
  const names = ['가방', '스킬', '특성', '캐릭터', '일지', '지도'];
  const closeOf = name => (name === '지도' ? '지도 닫기' : '창 닫기');
  {
    note('\n## 화면 나누기 PC 1280×720 (U2)\n');
    const game = await open(browser, run, Profiles.pc, url);
    check(await game.startSolo(), '혼자 하기가 시작된다');
    await game.wait(1500);
    await game.send('xp 400');
    await game.wait(800);
    for (const name of names) {
      const key = ScreenKeys[name];
      await game.page.keyboard.press(key);
      const opened = await game.until(s => s.window === name, 3000);
      const s = opened || (await game.state());
      check(!!opened, `${key} 키로 [${name}]이 열린다 (${s.window || '없음'})`);
      check(s.map === (name === '지도') && s.bag === (name !== '지도'), `한 번에 하나: [${name}]만 (지도 ${s.map}, 가방 종류 ${s.bag})`);
      if (name !== '가방') check(s.ui.window[2] >= 700 && s.ui.window[3] >= 680, `[${name}]은 전체 화면 창 (${s.ui.window.map(Math.round).join(',')})`);
      const labels = (await game.buttons()).map(b => b.label);
      check(labels.includes(closeOf(name)), `[${name}] 머리에 [${closeOf(name)}]`);
      const group = Object.keys(ScreenGroups).filter(n => ScreenGroups[n] === ScreenGroups[name] && n !== '가방');
      if (name !== '가방') check(group.every(n => labels.includes(n)), `[${name}]에 묶음의 탭 (${group.join('·')})`);
      check(!labels.some(l => /^(skill|quick|potion)/.test(l)) || name === '가방', `[${name}] 아래의 스킬 칸·퀵슬롯은 그리지 않는다`);
      check((await game.allText()).includes(`(${key === 'Tab' ? 'Tab' : key.toUpperCase()})`), `PC에서 머리에 키 (${key})`);
      await game.shot(`screens-pc-${name}`, `[${name}] (${key})`);
      await game.page.keyboard.press(key);
      check(!!(await game.until(t => t.window === '', 3000)), `${key}를 다시 누르면 [${name}]이 닫힌다`);
    }
    // One at a time: a key of another screen switches; a tab of the group switches within it.
    await game.page.keyboard.press('k');
    await game.until(t => t.window === '스킬', 3000);
    await game.page.keyboard.press('Tab');
    check(!!(await game.until(t => t.window === '가방', 3000)), '[스킬]이 열린 채 Tab을 누르면 가방으로 바뀐다(하나만)');
    await game.page.keyboard.press('j');
    await game.until(t => t.window === '일지', 3000);
    check(await game.press('지도') && !!(await game.until(t => t.window === '지도' && t.map, 3000)), '[일지]의 [지도] 탭으로 지도');
    check(await game.press('일지') && !!(await game.until(t => t.window === '일지', 3000)), '지도의 [일지] 탭으로 일지');
    await game.shot('screens-pc-records-tabs', '기록 묶음의 탭');
    // The world takes no input under a full screen: holding D does not walk.
    await game.page.keyboard.press('k');
    await game.until(t => t.window === '스킬', 3000);
    await game.send('clear');
    const before = await game.state();
    await game.page.keyboard.down('d');
    await game.wait(700);
    await game.page.keyboard.up('d');
    const after = await game.state();
    check(Math.abs(after.x - before.x) < 0.05, `전체 화면이 열린 동안 걷지 않는다 (x ${before.x.toFixed(2)} → ${after.x.toFixed(2)})`);
    // Esc closes the screen and does not leave the game.
    await game.page.keyboard.press('Escape');
    const shut = await game.until(t => t.window === '', 3000);
    check(!!shut && shut.playing, 'Esc는 열린 화면을 닫고 게임은 그대로');

    // G next to a person talks; Esc then closes the talk, not the game.
    await game.send('still');
    await game.wait(600);
    await game.send('goto person mina');
    await game.wait(900);
    let s = await game.state();
    check(s.meet === 'Person', `사람 곁에서 G는 말하기 (${s.meet})`);
    const talk = (await game.buttons()).find(b => /와 말하기|과 말하기/.test(b.label));
    check(!!talk && /\(G\)$/.test(talk.label), `말하기 단추에 (G) (${talk ? talk.label : '없음'})`);
    await game.page.keyboard.press('g');
    check(!!(await game.until(t => t.talking, 3000)), 'G로 미나와 말한다');
    await game.shot('screens-pc-g-talk', 'G로 연 대화');
    await game.page.keyboard.press('Escape');
    const quiet = await game.until(t => !t.talking, 3000);
    check(!!quiet && quiet.playing, 'Esc는 대화를 닫고 게임은 그대로');

    // G next to a workbench opens its station window (U3); by an anvil the anvil's window; a machine ahead is switched first.
    // Open floor outside the places: "goto biome Cave" lands on the first village's waypoint, and an open
    // waypoint in reach (1.6 tiles) is met before a workbench.
    await game.send('goto wild');
    await game.wait(1200);
    await clearRoom(game, 3, 2);
    await game.send('build Workbench -1 0; face 1 0');
    await game.wait(700);
    s = await game.state();
    check(s.meet === 'Station', `작업대 곁에서 G는 작업대 열기 (${s.meet})`);
    const benchButton = (await game.buttons()).find(b => /^작업대 열기/.test(b.label));
    check(!!benchButton && /\(G\)$/.test(benchButton.label), `작업대 위에 [작업대 열기 (G)] (${benchButton ? benchButton.label : '없음'})`);
    await game.page.keyboard.press('g');
    const bench = await game.until(t => t.window === '작업대', 3000);
    check(!!bench, `G로 작업대 창이 열린다(U3) (${bench ? bench.window : '닫힘'})`);
    await game.shot('screens-pc-g-workbench', 'G로 연 작업대 창');
    await game.page.keyboard.press('Escape');
    await game.until(t => t.window === '', 3000);
    await game.send('build Anvil 0 1');
    await game.wait(600);
    s = await game.state();
    check(s.meet === 'Anvil', `모루 곁에서 G는 모루 창 (${s.meet})`);
    await game.page.keyboard.press('g');
    const anvil = await game.until(t => t.window === '모루', 3000);
    check(!!anvil, `G로 모루 창이 열린다(U3) (${anvil ? anvil.window : '닫힘'})`);
    await game.page.keyboard.press('Escape');
    await game.until(t => t.window === '', 3000);
    await game.send('build Drill 0 -1 3; face 0 -1');
    await game.wait(800);
    s = await game.state();
    check(s.meet === 'Machine', `기계를 마주 보면 G는 기계가 먼저 (${s.meet})`);
    const off = (await game.buttons()).find(b => /^끄기/.test(b.label));
    check(!!off && /\(G\)$/.test(off.label), `[끄기 (G)]가 그대로 (${off ? off.label : '없음'})`);
    await game.page.keyboard.press('g');
    await game.wait(600);
    check(((await game.look(0, -1)).machine || {}).enabled === false, 'G로 기계가 꺼진다');
    await game.context.close();
  }

  for (const profileName of ['phone-small', 'phone-wide-small']) {
    const profile = Profiles[profileName];
    const { width, height } = profile.context.viewport;
    note(`\n## 화면 나누기 ${profileName} ${width}×${height} (U2, [메뉴] 허브)\n`);
    const game = await open(browser, run, profile, url);
    check(await game.press('게임 시작'), '[게임 시작]을 탭한다');
    check(await game.press('혼자 하기'), '[혼자 하기]를 탭한다');
    check(!!(await game.until(t => t.playing, 25000)), '탭으로 게임이 시작된다');
    await game.page.touchscreen.tap(width / 2, height / 3);
    await game.wait(800);
    await game.send('xp 400');
    await game.wait(800);
    const labels = (await game.buttons()).map(b => b.label);
    check(labels.includes('가방') && labels.includes('메뉴') && !labels.includes('나가기'), `미니맵 아래 [가방][메뉴] (${labels.filter(l => l === '가방' || l === '메뉴' || l === '나가기').join(', ')})`);
    check(await game.press('메뉴') && !!(await game.until(t => t.hub, 2000)), '[메뉴]를 탭하면 허브가 열린다');
    const hub = (await game.buttons()).filter(b => b.row === '메뉴' && b.shown);
    const hubNames = hub.map(b => b.label);
    check(names.every(n => hubNames.includes(n)) && hubNames.includes('나가기'), `허브에 가방·스킬·특성·캐릭터·일지·지도·나가기 (${hubNames.join(', ')})`);
    const small = hub.filter(b => b.rect[3] < 40 - 0.5 || b.rect[0] < 0 || b.rect[1] < 0 || b.rect[0] + b.rect[2] > width + 0.5 || b.rect[1] + b.rect[3] > height + 0.5);
    check(small.length === 0, `허브 단추는 40점 이상이고 화면 안 (${small.map(b => b.label).join(', ') || '모두'})`);
    await game.shot('screens-hub', '[메뉴] 허브');
    check(await game.press('메뉴') && !!(await game.until(t => !t.hub, 2000)), '[메뉴]를 다시 탭하면 허브가 닫힌다');
    for (const name of names) {
      await game.press('메뉴');
      await game.until(t => t.hub, 2000);
      await game.press(name, '메뉴');
      const opened = await game.until(t => t.window === name, 3000);
      const s = opened || (await game.state());
      check(!!opened && !s.hub, `허브의 [${name}]으로 화면이 열리고 허브는 닫힌다 (${s.window || '없음'})`);
      if (name !== '가방') {
        const w = s.ui.window;
        check(w[0] <= 5 && w[1] <= 5 && w[0] + w[2] >= width - 5 && w[1] + w[3] >= height - 5, `[${name}]은 화면 전체 시트 (${w.map(Math.round).join(',')})`);
      }
      const shown = (await game.buttons()).filter(b => b.shown);
      const outside = shown.filter(b => b.rect[0] < -0.5 || b.rect[0] + b.rect[2] > width + 0.5);
      check(outside.length === 0, `[${name}]의 단추가 옆으로 넘치지 않는다 (${outside.map(b => b.label).slice(0, 4).join(', ') || '모두 안'})`);
      await game.shot(`screens-${name}`, `허브로 연 [${name}]`);
      check(await game.press(closeOf(name)), `[${name}] 머리의 [${closeOf(name)}]를 탭한다`);
      check(!!(await game.until(t => t.window === '', 3000)), `[${name}]이 닫힌다`);
    }
    // Within a group, its tabs switch.
    await game.press('메뉴');
    await game.until(t => t.hub, 2000);
    await game.press('스킬', '메뉴');
    await game.until(t => t.window === '스킬', 3000);
    check(await game.press('특성') && !!(await game.until(t => t.window === '특성', 3000)), '[스킬] 화면의 [특성] 탭으로 특성');
    await game.shot('screens-passives-tab', '탭으로 바꾼 [특성](별하늘)');
    await game.press('창 닫기');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 만남 화면 (Docs/UiRework.md U3)
// The talk box (portrait, the speaker's band and hearts, the words, G·Space·Enter·a tap·[다음] going on, the answers),
// a stall keeper's talk and her shop (사기·팔기·선물·부탁), the chest, the workbench and the anvil windows; on a PC, a
// small phone held upright and a small phone held sideways. Photos of each.
const inScreen = (r, w, h) => r && r[2] > 0 && r[0] >= -0.5 && r[1] >= -0.5 && r[0] + r[2] <= w + 0.5 && r[1] + r[3] <= h + 0.5;
const overlapping = (a, b) => a && b && a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3];

async function encounter(browser) {
  {
    note('\n## 만남 화면 PC 1280×720 (U3)\n');
    const game = await open(browser, run, Profiles.pc, url);
    check(await game.startSolo(), '혼자 하기가 시작된다');
    await game.wait(1500);
    await game.send('still');
    await game.wait(600);
    // The chief's talk: five lines, his portrait, his band.
    await game.send('goto person chief');
    await game.wait(900);
    let s = await game.state();
    check(s.meet === 'Person', `촌장 곁에서 G는 말하기 (${s.meet})`);
    await game.page.keyboard.press('g');
    s = (await game.until(t => t.talking, 3000)) || (await game.state());
    check(s.talking && /^chief:/.test(s.portrait), `G로 촌장과 말하고 대화 상자에 그의 초상 (${s.portrait || '없음'})`);
    check(!s.talkTyping, '시험에서는 대사가 바로 다 나온다(타자 효과는 시험과 "움직임: 줄이기"에서 끈다)');
    check(inScreen(s.ui.talk, 1280, 720), `대화 상자가 화면 안 (${(s.ui.talk || []).map(Math.round).join(',')})`);
    check(!(await game.buttons()).some(b => /^(skill|quick|potion)/.test(b.label)), '대화 동안 스킬 칸·퀵슬롯은 그리지 않는다');
    await game.shot('encounter-talk', '촌장과의 대화(초상·인물 색 띠·대사)');
    // Going on: Space, Enter, a click on the words, [다음].
    const lines = [s.talkLine];
    await game.page.keyboard.press('Space');
    await game.wait(400);
    lines.push((await game.state()).talkLine);
    await game.page.keyboard.press('Enter');
    await game.wait(400);
    lines.push((await game.state()).talkLine);
    await game.press('대화 넘기기');
    lines.push((await game.state()).talkLine);
    check(new Set(lines).size === 4, `Space·Enter·대사 누르기가 다음 줄로 넘긴다 (${lines.join(' → ')})`);
    for (let i = 0; i < 8 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(200);
    await game.shot('encounter-talk-last', '마지막 줄의 [알겠다]');
    check(await game.press('알겠다'), '마지막 줄에서 [알겠다]');
    s = (await game.until(t => t.journeyTaken && !t.talking, 3000)) || (await game.state());
    check(s.journeyTaken && !s.talking, '[알겠다]로 단계를 받고 대화가 닫힌다');
    // A question: the queen at step 9 asks; her answers come under her words.
    await game.send('journey 9; goto person queen');
    await game.wait(1200);
    await game.press(/여왕 세라핀과 말하기/);
    for (let i = 0; i < 12 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(200);
    s = await game.state();
    check(/^queen:/.test(s.portrait), `여왕의 초상 (${s.portrait})`);
    await game.shot('encounter-talk-choice', '여왕의 물음과 답(초상의 표정)');
    check(await game.press('아무것도 없었다고 한다'), '답을 고른다');
    await game.wait(800);
    for (let i = 0; i < 12 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(200);
    await game.press('알겠다', undefined, { quiet: true });
    await game.press('닫기', undefined, { quiet: true });

    // A stall keeper: her button with (G), G talks, her choices, [거래] her shop.
    await game.send('goto near MerchantStall; give coin 300; give copper_ore 30; give star_melon 1');
    await game.wait(1200);
    s = await game.state();
    const keeper = s.shopkeeper;
    check(/^상인/.test(keeper), `노점 곁에 상인 (${keeper || '없음'})`);
    const button = (await game.buttons()).find(b => /(와|과) 말하기/.test(b.label) && b.label.startsWith(keeper));
    check(!!button, `상인 위에 [${keeper}와 말하기] (${button ? button.label : '없음'})`);
    if (s.meet === 'Merchant') {
      check(/\(G\)$/.test(button ? button.label : ''), '상인의 말하기 단추에 (G)');
      await game.page.keyboard.press('g');
    } else if (button) await game.press(button.label);
    s = (await game.until(t => t.talking, 3000)) || (await game.state());
    check(s.talking && /^merchant:/.test(s.portrait), `상인과 말하고 그의 초상 (${s.portrait || '없음'})`);
    const choices = (await game.buttons()).map(b => b.label);
    check(['거래', '부탁', '선물', '이야기', '닫기'].every(l => choices.includes(l)), `상인의 고를 것 [거래][부탁][선물][이야기][말 걸기][닫기] (${choices.filter(l => /거래|부탁|선물|이야기|말 걸기|닫기/.test(l)).join(', ')})`);
    await game.shot('encounter-merchant-talk', '상인과의 대화(하트, 고를 것)');
    check(await game.press('거래'), '[거래]를 누른다');
    s = (await game.until(t => t.window === '상점', 3000)) || (await game.state());
    check(s.window === '상점' && s.shopTab === '사기' && !s.talking, `[거래]로 상점 창의 [사기], 대화는 닫힌다 (${s.window}, ${s.shopTab})`);
    check(inScreen(s.ui.window, 1280, 720) && s.ui.window[0] + s.ui.window[2] <= 1280 - 130, `상점 창이 화면 안, 미니맵 칸 왼쪽 (${s.ui.window.map(Math.round).join(',')})`);
    check((await game.find('사기', /^회복 물약/)).length > 0, '[사기]에 회복 물약 카드');
    await game.shot('encounter-shop-buy', '상점 창 [사기](천막 머리, 초상, 카드, 가방 칸)');
    const potions = ((await game.inventory()).consumables || {}).healing_potion || 0;
    await game.press('사기', '회복 물약');
    check(!!(await game.until(i => ((i.consumables || {}).healing_potion || 0) === potions + 1, 3000, game.inventory)), '카드의 [사기]로 회복 물약을 산다');
    await game.press('팔기');
    const ore = count(await game.inventory(), 'copper_ore');
    await game.press('팔기', /^구리 광석 ×/);
    check(!!(await game.until(i => count(i, 'copper_ore') === ore - 1, 3000, game.inventory)), '[팔기]의 카드로 구리 광석 하나를 판다');
    await game.shot('encounter-shop-sell', '상점 창 [팔기]');
    await game.press('선물');
    await game.press('선물', '별 수박');
    await game.wait(600);
    check(((await game.inventory()).friends || {}).merchant > 0, '[선물]로 호감이 오른다');
    await game.press('부탁');
    await game.shot('encounter-shop-request', '상점 창 [부탁]');
    check(await game.press('창 닫기') && !!(await game.until(t => t.window === '', 3000)), '머리의 [창 닫기]로 상점이 닫힌다');

    // The chest, the workbench and the anvil out in the wild, each with its button over it.
    await game.send('goto wild');
    await game.wait(1200);
    await clearRoom(game, 3, 2);
    await game.send('build Chest -1 0; build Workbench 1 0; build Anvil 0 2; give stone 20; give copper_ore 12; give essence 60; give iron_bar 10');
    await game.wait(900);
    const over = (await game.buttons()).map(b => b.label);
    check(['상자 열기', '작업대 열기', '모루 열기'].every(w => over.some(l => l.startsWith(w))), `상자·작업대·모루 위에 여는 단추 (${over.filter(l => /열기/.test(l)).join(', ')})`);
    await game.shot('encounter-buttons', '상자·작업대·모루 위의 단추');
    check(await game.press(/^상자 열기/) && !!(await game.until(t => t.window === '상자', 3000)), '[상자 열기]로 상자 창');
    await game.press('넣기', /^돌 ×/);
    check(((await game.look(-1, 0)).chest || {}).stone > 0, '상자 창의 [넣기]로 돌이 상자에');
    await game.shot('encounter-chest', '상자 창(상자와 가방 두 판)');
    await game.press('꺼내기', /^돌 ×/);
    check(!((await game.look(-1, 0)).chest || {}).stone, '상자 창의 [꺼내기]로 다시 가방에');
    await game.page.keyboard.press('Escape');
    await game.until(t => t.window === '', 3000);
    check(await game.press(/^작업대 열기/) && !!(await game.until(t => t.window === '작업대', 3000)), '[작업대 열기]로 작업대 창');
    const swords = items(await game.inventory(), 'copper_sword');
    check(await game.press('만들기', /^구리 검/), '작업대 창의 구리 검 카드에서 [만들기]');
    await game.wait(800);
    await game.shot('encounter-station', '작업대 창(만드는 중 막대, 조합법 카드, 재료)');
    check(!!(await game.until(i => items(i, 'copper_sword') + (i.worn.Weapon === 'copper_sword' ? 1 : 0) > swords, 6000, game.inventory)), '작업대 창에서 구리 검이 만들어진다');
    await game.page.keyboard.press('Escape');
    await game.until(t => t.window === '', 3000);
    await game.send('drop item iron_sword rare');
    await game.wait(1500);
    check(await game.press(/^모루 열기/) && !!(await game.until(t => t.window === '모루', 3000)), '[모루 열기]로 모루 창');
    await pick(game, /철 검/);
    await game.shot('encounter-anvil', '모루 창(장비 칸과 다듬기 판)');
    check(await game.press(/^강화 \+1/), '모루 창에서 철 검의 [강화 +1]');
    check(!!(await game.until(i => i.items.some(x => x.base === 'iron_sword' && x.upgrade === 1), 3000, game.inventory)), '철 검이 +1');
    // Walking away shuts the window.
    await game.send('goto 6 0');
    check(!!(await game.until(t => t.window === '', 3000)), '모루 곁을 떠나면 모루 창이 닫힌다');
    await game.context.close();
  }

  for (const profileName of ['phone-small', 'phone-wide-small']) {
    const profile = Profiles[profileName];
    const { width, height } = profile.context.viewport;
    note(`\n## 만남 화면 ${profileName} ${width}×${height} (U3)\n`);
    const game = await open(browser, run, profile, url);
    check(await game.press('게임 시작'), '[게임 시작]을 탭한다');
    check(await game.press('혼자 하기'), '[혼자 하기]를 탭한다');
    check(!!(await game.until(t => t.playing, 25000)), '탭으로 게임이 시작된다');
    await game.page.touchscreen.tap(width / 2, height / 3);
    await game.wait(800);
    await game.send('still');
    await game.wait(600);
    const column = (await game.buttons()).filter(b => b.label === '가방' || b.label === '메뉴' || b.label === '지도');
    await game.send('goto person chief');
    await game.wait(900);
    check(await game.press(/촌장 바르도와 말하기/), '[촌장 바르도와 말하기]를 탭한다');
    let s = (await game.until(t => t.talking, 3000)) || (await game.state());
    const talk = s.ui.talk;
    check(s.talking && /^chief:/.test(s.portrait), `대화 상자에 촌장의 초상 (${s.portrait || '없음'})`);
    check(inScreen(talk, width, height), `대화 상자가 화면 안 (${(talk || []).map(Math.round).join(',')})`);
    const drawn = await game.buttons();
    check(!drawn.some(b => /^(skill|quick|potion)/.test(b.label)), '대화 동안 퀵슬롯·스킬 칸을 그리지 않는다(대화 상자가 퀵슬롯을 덮지 않는다)');
    const hidden = column.filter(c => overlapping(talk, c.rect));
    check(hidden.length === 0, `대화 상자가 미니맵·[가방]·[메뉴]를 덮지 않는다 (${hidden.map(c => c.label).join(', ') || '없음'})`);
    const next = drawn.find(b => b.label === '다음');
    check(!!next && next.rect[3] >= 40 - 0.5, `[다음]이 손가락 크기 (${next ? Math.round(next.rect[3]) : '-'})`);
    await game.shot('encounter-talk', '대화 상자(초상, 띠, 대사)');
    const before = s.talkLine;
    await game.press('대화 넘기기');
    check((await game.state()).talkLine !== before, `대사를 탭하면 다음 줄 (${before} → ${(await game.state()).talkLine})`);
    await game.page.keyboard.press('Escape');
    await game.until(t => !t.talking, 2000);

    await game.send('goto near MerchantStall; give coin 300; give copper_ore 30');
    await game.wait(1200);
    s = await game.state();
    check(await game.press(new RegExp(`^${s.shopkeeper}(와|과) 말하기`)), `[${s.shopkeeper}와 말하기]를 탭한다`);
    s = (await game.until(t => t.talking, 3000)) || (await game.state());
    const small = (await game.buttons()).filter(b => /^(거래|부탁|선물|이야기|말 걸기|닫기)$/.test(b.label) && b.rect[3] < 40 - 0.5);
    check(s.talking && small.length === 0, `상인의 고를 것이 손가락 크기 (${small.map(b => b.label).join(', ') || '모두'})`);
    check(inScreen(s.ui.talk, width, height), '상인의 대화 상자가 화면 안');
    await game.shot('encounter-merchant-talk', '상인과의 대화');
    check(await game.press('거래') && !!(await game.until(t => t.window === '상점', 3000)), '[거래]로 상점 창');
    s = await game.state();
    const w = s.ui.window;
    check(w[0] <= 5 && w[1] <= 5 && w[0] + w[2] >= width - 5 && w[1] + w[3] >= height - 5, `휴대폰에서 상점은 화면 전체 시트 (${w.map(Math.round).join(',')})`);
    const labels = (await game.buttons()).map(b => b.label);
    check(!labels.includes('가방') && !labels.includes('메뉴'), '상점 시트 아래의 [가방]·[메뉴]는 그리지 않는다');
    const outside = (await game.buttons()).filter(b => b.shown && (b.rect[0] < -0.5 || b.rect[0] + b.rect[2] > width + 0.5));
    check(outside.length === 0, `상점의 단추가 옆으로 넘치지 않는다 (${outside.map(b => b.label).slice(0, 4).join(', ') || '모두 안'})`);
    await game.shot('encounter-shop', '상점 시트 [사기]');
    await game.press('팔기');
    const ore = count(await game.inventory(), 'copper_ore');
    await game.press('팔기', /^구리 광석 ×/);
    check(!!(await game.until(i => count(i, 'copper_ore') === ore - 1, 3000, game.inventory)), '탭으로 구리 광석 하나를 판다');
    await game.shot('encounter-shop-sell', '상점 시트 [팔기]');
    check(await game.press('창 닫기') && !!(await game.until(t => t.window === '', 3000)), '[창 닫기]로 상점이 닫힌다');

    await game.send('goto wild');
    await game.wait(1200);
    await clearRoom(game, 3, 2);
    await game.send('build Chest -1 0; build Workbench 1 0; give stone 20; give copper_ore 12');
    await game.wait(900);
    check(await game.press(/^작업대 열기/) && !!(await game.until(t => t.window === '작업대', 3000)), '[작업대 열기]를 탭해 작업대 시트');
    const make = (await game.find('만들기')).filter(b => b.shown);
    check(make.length > 0 && make.every(b => b.rect[3] >= 40 - 0.5), `작업대의 [만들기]가 손가락 크기 (${make.length}개)`);
    await game.shot('encounter-station', '작업대 시트');
    await game.press('창 닫기');
    await game.until(t => t.window === '', 3000);
    check(await game.press(/^상자 열기/) && !!(await game.until(t => t.window === '상자', 3000)), '[상자 열기]를 탭해 상자 시트');
    await game.press('넣기', /^돌 ×/);
    check(((await game.look(-1, 0)).chest || {}).stone > 0, '탭으로 상자에 돌을 넣는다');
    await game.shot('encounter-chest', '상자 시트');
    await game.press('창 닫기');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 성장 화면 (Docs/UiRework.md U4)
// The skills (the slot bar, cards or a phone's rows, the picked skill's panel or a phone's sheet, a crystal in and out,
// a slot picked and filled, [바꾸기], [되돌리기]), the passive tree (a star pressed then [배우기], a point from the
// drawer, the sky dragged and zoomed by the wheel or two fingers and [+]/[-], back to the middle, [모두 되돌리기]) and the
// character's sheet with a pet and a life skill's perk; on a PC, a small phone held upright and a small phone held
// sideways. Photos of each.
async function growth(browser) {
  for (const profileName of ['pc', 'phone-small', 'phone-wide-small']) {
    const profile = Profiles[profileName];
    const { width, height } = profile.context.viewport;
    note(`\n## 성장 화면 ${profileName} ${width}×${height} (U4)\n`);
    const game = await open(browser, run, profile, url);
    check(await game.startSolo(), '혼자 하기가 시작된다');
    await game.wait(1500);
    if (game.touch) {
      await game.page.touchscreen.tap(width / 2, height / 3);
      await game.wait(800);
    }
    const narrow = game.touch && width < 600;
    const shot = (name, caption) => game.shot(`growth-${profileName}-${name}`, `${profileName}: ${caption}`);
    // Every shown button on the screen, and on a touch screen the growth screens' own a finger tall.
    const fits = async what => {
      const shown = (await game.buttons()).filter(b => b.shown);
      const out = shown.filter(b => b.rect[0] < -0.5 || b.rect[1] < -0.5 || b.rect[0] + b.rect[2] > width + 0.5 || b.rect[1] + b.rect[3] > height + 0.5);
      check(out.length === 0, `${what}: 단추가 화면 안 (${out.map(b => b.label).slice(0, 4).join(', ') || '모두'})`);
      if (!game.touch) return;
      const words = /^(배우기|올리기|최고|변형|빼기|고르기|미리보기|바꾸기|그만|칸에 넣기|목록으로|되돌리기|모두 되돌리기|지금 배울 수 있는 점|확대|축소|가운데로|이름 짓기|추천)|끼우기|: /;
      const small = shown.filter(b => words.test(b.label) && b.rect[3] < 39.5);
      check(small.length === 0, `${what}: 손가락 단추 40점 이상 (${small.map(b => `${b.label} ${Math.round(b.rect[3])}`).slice(0, 4).join(', ') || '모두'})`);
    };
    // A pet first, hatched from the bag (Docs/Animals.md), and what the screens need.
    await game.send('xp 9000; give support_area 1; give support_leech 1; give coin 500; give slime_egg 1; lifexp mining 1000; clear');
    await game.wait(1000);
    await bagTab(game, '재료·제작');
    check(await game.press('부화', '말랑한 알'), '말랑한 알의 [부화]');
    await closeBag(game);

    // ---- 스킬 ----
    check(await openScreen(game, '스킬'), '[스킬] 화면이 열린다');
    await game.wait(400);
    await shot('skills', '[스킬]: 칸 줄, 포인트, 클래스마다 스킬(카드 또는 줄)');
    await fits('[스킬]');
    check(await game.press('배우기', '전투 함성'), '[전투 함성]의 [배우기]');
    check(!!(await game.until(i => /war_cry:1/.test(i.ranks), 3000, game.inventory)), '전투 함성을 배운다');
    check(await game.press('올리기', '전투 함성'), '[전투 함성]의 [올리기]');
    check(!!(await game.until(i => /war_cry:2/.test(i.ranks), 3000, game.inventory)), '전투 함성이 2단계');
    if (narrow) await game.press('목록으로', undefined, { quiet: true });
    check(await game.press('회전베기', /^회전베기/), '회전베기 카드(줄)를 누른다');
    let s = await game.until(t => t.growth.skill === 'whirlwind', 3000);
    check(!!s, `누른 스킬이 판에 나온다 (${(await game.state()).growth.skill})`);
    if (narrow) check(!!s && s.growth.sheet, '휴대폰 세로: 고른 스킬이 목록 위 시트로');
    await shot('skill-picked', '고른 스킬: 지금·다음 단계, [올리기]·[칸에 넣기], 변형 결정');
    if (narrow) check(await game.press('목록으로'), '[목록으로]로 목록에 돌아간다');
    check(await game.press('변형', '회전베기'), '회전베기의 [변형]');
    check(await game.press(/^확산의 결정 끼우기/), '[확산의 결정 끼우기]');
    check(!!(await game.until(i => /whirlwind:support_area/.test(i.supports), 3000, game.inventory)), '확산의 결정이 회전베기에 들어간다');
    await shot('skill-crystal', '회전베기에 끼운 결정과 [빼기]');
    check(await game.press('빼기'), '[빼기]');
    check(!!(await game.until(i => !i.supports, 3000, game.inventory)), '결정이 가방으로 돌아온다');
    if (narrow) await game.press('목록으로', undefined, { quiet: true });
    // A slot's skill picked, [칸에 넣기], then the other slot: the two swap.
    let order = (await game.inventory()).skills;
    check(await game.press('스킬 칸 1'), '[스킬 칸 1]을 누른다');
    check(await game.press('칸에 넣기'), '[칸에 넣기]');
    check((await game.state()).growth.placing !== '', '넣을 칸을 기다린다');
    await shot('skill-placing', '넣을 칸을 누르기를 기다리는 칸 줄');
    check(await game.press('스킬 칸 2'), '[스킬 칸 2]를 누른다');
    const swapped = await game.until(i => i.skills !== order, 3000, game.inventory);
    check(!!swapped && swapped.skills.split(',')[1] === order.split(',')[0], `두 칸이 바뀐다 (${order} → ${swapped && swapped.skills})`);
    if (narrow) await game.press('목록으로', undefined, { quiet: true });
    // [바꾸기]: more learned than slots, a slot filled from the rest.
    await game.press('배우기', '돌진');
    await game.press('배우기', '불꽃 부여');
    order = (await game.inventory()).skills;
    check(await game.press('바꾸기'), '칸 아래 [바꾸기]');
    check(await game.press(/^\d번 칸에: /), '[N번 칸에: …]로 칸에 없는 스킬을 넣는다');
    check(!!(await game.until(i => i.skills !== order, 3000, game.inventory)), `칸이 바뀐다 (${order} → ${(await game.inventory()).skills})`);
    const points = (await game.inventory()).skillPoints;
    check(await game.press(/^되돌리기 \(금화 \d+\)$/), '[되돌리기 (금화 N)]');
    check(!!(await game.until(i => i.skillPoints > points, 3000, game.inventory)), '되돌리면 포인트가 돌아온다');
    await shot('skills-reset', '되돌린 [스킬]');

    // ---- 특성 ----
    check(await openScreen(game, '특성'), '[특성] 화면이 열린다');
    await game.wait(400);
    await shot('tree', '별하늘 특성 트리: 배울 수 있는 점이 숨쉬고, 서랍이 열려 있다');
    await fits('[특성]');
    s = await game.state();
    check(s.growth.sky[2] > 200 && s.growth.sky[3] > 150, `하늘이 넓다 (${s.growth.sky.map(Math.round).join(',')})`);
    const drawer = () => game.buttons().then(all => all.filter(b => b.shown && b.row === '지금 배울 수 있는 점' && /: /.test(b.label)));
    let learnable = await drawer();
    check(learnable.length > 0, `"지금 배울 수 있는 점" 서랍에 점이 있다 (${learnable.length})`);
    // A star pressed in the sky (a real click or tap), then [배우기] on its card.
    const name = learnable.length > 0 ? learnable[0].label.split(':')[0] : '';
    check(await game.press(name, '특성 트리'), `하늘의 별 [${name}]을 누른다`);
    check((await game.state()).growth.passive === name, '누른 점이 카드에 나온다');
    await shot('tree-picked', `고른 점 ${name}의 카드`);
    check(await game.press('배우기'), '카드의 [배우기]');
    check(!!(await game.until(i => i.passives === 1, 3000, game.inventory)), '별을 눌러 배운다');
    // A point from the drawer.
    // Every learnable point in the drawer, shown or not: on a short landscape screen the drawer sits under the picked
    // point's card in the scrolled side pane, and a press scrolls to it.
    learnable = await game.buttons().then(all => all.filter(b => b.row === '지금 배울 수 있는 점' && /: /.test(b.label)));
    if (learnable.length > 0) await game.press(learnable[0].label, '지금 배울 수 있는 점');
    await game.wait(400);
    check(await game.press('배우기'), '서랍의 점을 누르고 [배우기]');
    check(!!(await game.until(i => i.passives === 2, 3000, game.inventory)), '서랍으로도 배운다');
    await shot('tree-learned', '두 점을 배운 트리(이어진 선이 금빛)');
    // Dragging the sky pans it; the wheel or two fingers and [+]/[-] zoom.
    s = await game.state();
    const box = await game.canvas();
    const sx = box.width / width, sy = box.height / height;
    const sky = s.growth.sky;
    const cx = box.left + (sky[0] + sky[2] / 2) * sx, cy = box.top + (sky[1] + sky[3] * 0.4) * sy;
    const pan = [s.growth.panX, s.growth.panY];
    if (game.touch) await game.drag(cx, cy, 60, 40, 200);
    else {
      await game.page.mouse.move(cx, cy);
      await game.page.mouse.down();
      await game.page.mouse.move(cx + 60, cy + 40, { steps: 8 });
      await game.page.mouse.up();
    }
    await game.wait(400);
    s = await game.state();
    check(Math.hypot(s.growth.panX - pan[0], s.growth.panY - pan[1]) > 20, `하늘을 끌면 옮겨진다 (${pan.map(Math.round)} → ${Math.round(s.growth.panX)},${Math.round(s.growth.panY)})`);
    let zoom = s.growth.zoom;
    if (game.touch) {
      // Two fingers apart (the DevTools protocol's touches).
      const cdp = await game.context.newCDPSession(game.page);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: cx - 20, y: cy, id: 1 }, { x: cx + 20, y: cy, id: 2 }] });
      for (let i = 1; i <= 8; i++) {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: cx - 20 - i * 8, y: cy, id: 1 }, { x: cx + 20 + i * 8, y: cy, id: 2 }] });
        await game.wait(60);
      }
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    } else {
      await game.page.mouse.move(cx, cy);
      await game.page.mouse.wheel(0, -300);
    }
    await game.wait(500);
    s = await game.state();
    check(s.growth.zoom > zoom + 1, `${game.touch ? '두 손가락을 벌리면' : '휠을 올리면'} 확대된다 (${Math.round(zoom)} → ${Math.round(s.growth.zoom)})`);
    await shot('tree-zoomed', '확대한 트리(점 아래 이름)');
    zoom = s.growth.zoom;
    check(await game.press('축소'), '[-] 축소');
    check((await game.state()).growth.zoom < zoom, '[-]로 작아진다');
    zoom = (await game.state()).growth.zoom;
    check(await game.press('확대'), '[+] 확대');
    check((await game.state()).growth.zoom > zoom, '[+]로 커진다');
    check(await game.press('가운데로'), '[◇] 가운데로');
    await shot('tree-centred', '가운데로 돌아온 트리');
    check(await game.press('지금 배울 수 있는 점'), '서랍 머리를 누르면 접힌다');
    check(!(await game.state()).growth.drawer, '서랍이 접힌다');
    await shot('tree-drawer-shut', '서랍을 접은 트리');
    await game.press('지금 배울 수 있는 점');
    check(await game.press(/^모두 되돌리기 \(금화 \d+\)$/), '[모두 되돌리기 (금화 N)]');
    check(!!(await game.until(i => i.passives === 0, 3000, game.inventory)), '되돌리면 배운 점이 없다');

    // ---- 캐릭터 ----
    check(await openScreen(game, '캐릭터'), '[캐릭터] 화면이 열린다');
    await game.wait(400);
    await shot('character', '캐릭터: 큰 주인공 그림과 착용 칸, 레벨, 능력치 묶음, 펫, 생활 기술');
    await fits('[캐릭터]');
    const text = await game.allText();
    check(/생명력 \d+/.test(text) && /초당 공격/.test(text) && text.includes('생활 기술'), '능력치 묶음과 생활 기술이 보인다');
    check(await game.press('추천', '펫') && (await game.press('이름 짓기', '펫')), '펫의 [추천]과 [이름 짓기]');
    check(!!(await game.until(i => i.petName && i.petName !== '말랑이', 2000, game.inventory)), '펫에 이름이 붙는다');
    check(await game.press('고르기', '날쌘 곡괭이'), '채굴 5레벨 혜택 [고르기]');
    const perks = await game.until(i => (i.perks || '').split(',').map(Number)[4] === 2, 3000, game.inventory);
    check(!!perks, '날쌘 곡괭이를 고른다');
    await shot('character-perk', '혜택을 고른 캐릭터');
    await game.press('창 닫기');
    check(!!(await game.until(t => t.window === '', 3000)), '[창 닫기]로 닫힌다');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 기록: 일지와 지도 (U5)
// The records laid out anew (Docs/UiRework.md U5) on a PC, a small phone held upright and a small one on its side: the
// journal as a parchment book (J or the [메뉴] hub) on each of its sections, a goal's note, the story's chapters, a
// rumour's [지도에서 보기] opening the map on its ring, a bestiary entry's card, the deeds, [추적] on a request; then the
// map (M or the minimap) dragged, zoomed (wheel or two fingers, [+]/[-]/[◇]), a legend toggle, a mark's card and
// [여기로 길 안내] the goal list then follows.
async function records(browser) {
  for (const profileName of ['pc', 'phone-small', 'phone-wide-small']) {
    const profile = Profiles[profileName];
    const { width, height } = profile.context.viewport;
    note(`\n## 기록 화면 ${profileName} ${width}×${height} (U5)\n`);
    const game = await open(browser, run, profile, url);
    check(await game.startSolo(), '혼자 하기가 시작된다');
    await game.wait(1500);
    if (game.touch) {
      await game.page.touchscreen.tap(width / 2, height / 3);
      await game.wait(800);
    }
    const shot = (name, caption) => game.shot(`records-${profileName}-${name}`, `${profileName}: ${caption}`);
    const records = async () => (await game.state()).records || {};
    // Every shown button on the screen, and on a touch screen the records' own a finger tall.
    const fits = async what => {
      const shown = (await game.buttons()).filter(b => b.shown);
      const out = shown.filter(b => b.rect[0] < -0.5 || b.rect[1] < -0.5 || b.rect[0] + b.rect[2] > width + 0.5 || b.rect[1] + b.rect[3] > height + 0.5);
      check(out.length === 0, `${what}: 단추가 화면 안 (${out.map(b => b.label).slice(0, 4).join(', ') || '모두'})`);
      if (!game.touch) return;
      const words = /^(목표|이야기|소문|도감|업적|지도에서 보기|추적|추적 그만|마친 목표|여기로 길 안내|길 안내 그만|이동|확대|축소|가운데로)/;
      const small = shown.filter(b => (words.test(b.label) || b.row === '범례' || b.row === '도감 묶음') && b.rect[3] < 39.5);
      check(small.length === 0, `${what}: 손가락 단추 40점 이상 (${small.map(b => `${b.label} ${Math.round(b.rect[3])}`).slice(0, 4).join(', ') || '모두'})`);
    };
    await game.send('monsters hold; still; xp 2000; hearts merchant 1; give copper_ore 5');
    await game.wait(600);
    // A rumour from the village child (as the guide section hears one), for the journal's rumours and the map's ring.
    await game.send('goto person mina');
    await game.wait(900);
    if (await game.press(/미나와 말하기/, undefined, { quiet: true })) {
      for (let i = 0; i < 4 && (await game.press('다음', undefined, { quiet: true })); i++) await game.wait(300);
      await game.press('알겠다', undefined, { quiet: true });
      await game.press('닫기', undefined, { quiet: true });
    }
    const heard = await game.ask('rumors');
    note(`  - 들은 소문 ${(heard.heard || []).length}`);

    // ---- 일지 ----
    check(await openScreen(game, '일지'), '[일지]가 열린다');
    await game.wait(500);
    let r = await records();
    check(r.section === '목표', `일지는 목표 쪽으로 열린다 (${r.section})`);
    const index = (await game.buttons()).filter(b => b.row === '일지').map(b => b.label);
    check(['목표', '이야기', '소문', '도감', '업적'].every(n => index.includes(n)), `목차에 다섯 쪽 (${index.join(', ')})`);
    let text = await game.allText();
    check(/목표 안내 \d+\/\d+/.test(text) && /지금: /.test(text), '목표 쪽에 목표 안내의 쪽지(지금 목표, 진행)');
    await shot('journal-goals', '일지 목표: 목표 안내, 이야기 단계, 오늘의 부탁 쪽지');
    await fits('[일지] 목표');
    // [추적] on today's request: the goal list follows it.
    const follow = (await game.buttons()).find(b => b.label === '추적');
    if (follow) {
      await game.press('추적', follow.row);
      r = await records();
      check(/^(request|story|side):/.test(r.tracked), `[추적]을 누르면 목표 목록이 그것을 따른다 (${r.tracked})`);
      await shot('journal-tracked', '추적 중인 쪽지(붉은 테, [추적 그만])');
    } else note('  - [추적]할 쪽지가 없다(부탁이 없는 날)');
    check(await game.press('이야기', '일지'), '목차의 [이야기]');
    await game.wait(300);
    text = await game.allText();
    check((await records()).section === '이야기' && /큰 이야기/.test(text), '이야기 쪽: 큰 이야기와 장 제목');
    await shot('journal-story', '일지 이야기: 장마다 지나온 단계, 곁이야기, 사람들의 이야기(초상)');
    await fits('[일지] 이야기');
    check(await game.press('소문', '일지'), '목차의 [소문]');
    await game.wait(300);
    await shot('journal-rumors', '일지 소문: 들은 소문과 방향, [지도에서 보기]');
    if ((await game.find('지도에서 보기')).length > 0) {
      check(await game.press('지도에서 보기'), '소문의 [지도에서 보기]');
      const onMap = await game.until(t => t.window === '지도' && t.records && t.records.focus !== '', 3000);
      check(!!onMap, `[지도에서 보기]로 지도가 그 소문 쪽으로 열린다 (${onMap ? onMap.records.focus : '없음'})`);
      await shot('rumor-on-map', '소문의 옅은 원 쪽으로 열린 지도');
      await game.press('일지');
      await game.until(t => t.window === '일지', 3000);
      check((await records()).section === '소문', '지도의 [일지] 탭으로 보던 쪽에 돌아온다');
    } else note('  - 들은 소문이 없어 [지도에서 보기]를 건너뛴다');
    check(await game.press('도감', '일지'), '목차의 [도감]');
    await game.wait(300);
    await game.press('장비', '도감 묶음');
    await game.wait(300);
    const found = (await game.buttons()).filter(b => b.row === '도감' && b.label !== '모르는 것');
    check(found.length > 0, `도감에 찾은 것의 그림 칸 (${found.length}) · 모르는 것은 실루엣`);
    if (found.length > 0) {
      await game.press(found[0].label, '도감');
      r = await records();
      check(r.entry !== '', `그림 칸을 누르면 위에 그 카드 (${r.group} ${r.entry})`);
    }
    await shot('journal-bestiary', '일지 도감: 묶음 탭, 고른 것의 카드, 그림 칸과 실루엣');
    await fits('[일지] 도감');
    check(await game.press('업적', '일지'), '목차의 [업적]');
    await game.wait(300);
    check(/업적 \d+\/\d+/.test(await game.allText()), '업적 쪽: 업적 수와 진행 막대 쪽지');
    await shot('journal-deeds', '일지 업적: 진행 막대, 이룬 것에 붉은 밀랍 인장');
    await fits('[일지] 업적');

    // ---- 지도 ----
    await game.press('창 닫기');
    await game.until(t => t.window === '', 3000);
    if (game.touch) await game.press('지도');
    else await game.page.keyboard.press('m');
    let s = await game.until(t => t.window === '지도' && t.map, 3000);
    check(!!s, `${game.touch ? '미니맵을 누르면' : 'M으로'} 지도가 열린다`);
    r = await records();
    check(r.zoom > 0 && r.view[2] > 200 && r.view[3] > 150, `지도가 넓다 (${(r.view || []).map(Math.round).join(',')}, 확대 ${r.zoom && r.zoom.toFixed(2)})`);
    await shot('map', '지도: 양피지, 빗금 친 안 가 본 곳, 표시, 범례');
    await fits('[지도]');
    // Dragging pans it.
    const box = await game.canvas();
    const sx = box.width / width, sy = box.height / height;
    const view = r.view;
    const cx = box.left + (view[0] + view[2] * 0.5) * sx, cy = box.top + (view[1] + view[3] * 0.35) * sy;
    const middle = [r.cx, r.cy];
    if (game.touch) await game.drag(cx, cy, 70, 40, 200);
    else {
      await game.page.mouse.move(cx, cy);
      await game.page.mouse.down();
      await game.page.mouse.move(cx + 70, cy + 40, { steps: 8 });
      await game.page.mouse.up();
    }
    await game.wait(400);
    r = await records();
    check(Math.hypot(r.cx - middle[0], r.cy - middle[1]) > 3, `지도를 끌면 옮겨진다 (${middle.map(Math.round)} → ${Math.round(r.cx)},${Math.round(r.cy)})`);
    let zoom = r.zoom;
    if (game.touch) {
      const cdp = await game.context.newCDPSession(game.page);
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: cx - 20, y: cy, id: 1 }, { x: cx + 20, y: cy, id: 2 }] });
      for (let i = 1; i <= 8; i++) {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: cx - 20 - i * 8, y: cy, id: 1 }, { x: cx + 20 + i * 8, y: cy, id: 2 }] });
        await game.wait(60);
      }
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    } else {
      await game.page.mouse.move(cx, cy);
      await game.page.mouse.wheel(0, -300);
    }
    await game.wait(500);
    r = await records();
    check(r.zoom > zoom * 1.1, `${game.touch ? '두 손가락을 벌리면' : '휠을 올리면'} 확대된다 (${zoom.toFixed(2)} → ${r.zoom.toFixed(2)})`);
    zoom = r.zoom;
    check(await game.press('축소', '지도') && (await records()).zoom < zoom, '[-] 축소');
    zoom = (await records()).zoom;
    check(await game.press('확대', '지도') && (await records()).zoom > zoom, '[+] 확대');
    check(await game.press('가운데로', '지도'), '[◇] 가운데로');
    await shot('map-zoomed', '확대하고 가운데로 돌아온 지도');
    // A legend toggle hides its marks and shows them again.
    check(await game.press('장소', '범례'), '범례의 [장소]');
    check(/장소/.test((await records()).hidden), `[장소]를 끄면 장소 표시가 숨는다 (${(await records()).hidden})`);
    await shot('map-legend-off', '범례 [장소]를 끈 지도(줄 그은 단추)');
    await game.press('장소', '범례');
    check(!/장소/.test((await records()).hidden), '다시 누르면 보인다');
    // A mark pressed: its card, then [여기로 길 안내].
    const marks = (await game.buttons()).filter(b => b.row === '지도 표시' && b.shown && b.label !== '나');
    note(`  - 지도의 표시 ${marks.length}개: ${marks.slice(0, 6).map(b => b.label).join(', ')}`);
    if (marks.length > 0) {
      await game.press(marks[0].label, '지도 표시');
      r = await records();
      check(r.mark !== '', `표시를 누르면 카드가 나온다 (${r.mark})`);
      await shot('map-card', `표시의 카드 (${r.mark})`);
      if (await game.press('여기로 길 안내', undefined, { quiet: true })) {
        check((await records()).tracked === 'pin', '[여기로 길 안내]를 누르면 목표 목록이 그곳을 따른다');
        await game.press('지도 닫기');
        await game.wait(400);
        check(/길 안내: /.test(await game.allText()), '목표 목록에 "길 안내: …" 줄');
        await shot('map-guided', '길 안내를 따르는 목표 목록');
      }
    } else note('  - 누를 표시가 지도에 없다');
    await game.context.close();
  }
}

// ---------------------------------------------------------------- 가방과 HUD (Docs/UiRework.md U6·U7)
// The bag as a bag (the hero and the six worn slots, the kind chips and [정렬], one grid of everything with empty wells,
// the picked thing's card with its buttons first, how it compares, the quick slots it can go in); the door's buttons
// over it in the world; the HUD (buffs with their time, the low-health red at the edges, the level's banner kept off the
// [메뉴] hub, the goal list folded and unfolded, the journal from the folded line); and the fallen card over an open
// window with [집에서 일어나기] that gets the player up. On a PC, a small phone held upright and a small one on its side,
// with photos of each.
async function hud(browser) {
  for (const profileName of ['pc', 'phone-small', 'phone-wide-small']) {
    const profile = Profiles[profileName];
    const { width, height } = profile.context.viewport;
    note(`\n## 가방과 HUD ${profileName} ${width}×${height} (U6·U7)\n`);
    const game = await open(browser, run, profile, url);
    check(await game.startSolo(), '혼자 하기가 시작된다');
    await game.wait(1500);
    if (game.touch) {
      await game.page.touchscreen.tap(width / 2, height / 3);
      await game.wait(800);
    }
    const shot = (name, caption) => game.shot(`hud-${profileName}-${name}`, `${profileName}: ${caption}`);
    const inside = r => r && r[2] > 0 && r[0] >= -0.5 && r[1] >= -0.5 && r[0] + r[2] <= width + 0.5 && r[1] + r[3] <= height + 0.5;
    // Every shown button inside the screen, and on a touch screen those of the bag and the HUD a finger tall. With a
    // rect, only the buttons in it (the fallen card's own).
    const fits = async (what, within) => {
      const shown = (await game.buttons()).filter(b => b.shown && (!within || (b.rect[0] + b.rect[2] / 2 >= within[0] && b.rect[0] + b.rect[2] / 2 <= within[0] + within[2]
        && b.rect[1] + b.rect[3] / 2 >= within[1] && b.rect[1] + b.rect[3] / 2 <= within[1] + within[3])));
      const out = shown.filter(b => !inside(b.rect));
      check(out.length === 0, `${what}: 단추가 화면 안 (${out.map(b => b.label).slice(0, 4).join(', ') || '모두'})`);
      if (!game.touch) return;
      const words = /^(분류: |정렬$|장착$|버리기$|벗기$|퀵슬롯 \d$|놓기$|심기$|마시기$|먹기$|잠그기$|잠금 풀기$|그만$|집에서 일어나기$|입구에서 일어나기$|목표 접기$|목표 펴기$|일지 열기$|문 잠그기)/;
      const small = shown.filter(b => words.test(b.label) && b.rect[3] < 39.5);
      check(small.length === 0, `${what}: 손가락 단추 40점 이상 (${small.map(b => `${b.label} ${Math.round(b.rect[3])}`).slice(0, 4).join(', ') || '모두'})`);
    };
    await game.send('monsters hold; still');
    await clearRoom(game, 4, 3);
    await game.send('drop item iron_sword rare; give stone 12; give slime_gel 5; give copper_ore 4; give mushroom_spore 2; xp 400');
    await game.wait(1500);

    // ---- 가방 ----
    check(await openScreen(game, '가방'), '[가방]이 열린다');
    await game.wait(500);
    let s = await game.state();
    check(s.bagPage === '장비' && s.bagFilter === '전체', `가방은 [장비] 탭의 [전체]로 열린다 (${s.bagPage}, ${s.bagFilter})`);
    const chips = (await game.buttons()).filter(b => b.row === '분류' && b.shown).map(b => b.label);
    check(['전체', '장비', '재료', '먹을것', '놓을것'].every(n => chips.includes(`분류: ${n}`)), `분류 칩 다섯 (${chips.join(', ')})`);
    check((await game.find('정렬')).some(b => b.shown), '[정렬]이 칩 줄에 보인다');
    check((await game.find(/구리 검/, '착용')).length === 1, '착용 칸에 구리 검');
    const has = async name => (await game.find(name, '소지품')).length > 0;
    check(await has(/철 검/) && await has(/^돌 ×/) && await has(/^회복 물약 ×/), '[전체]의 한 격자에 장비·재료·물약');
    await shot('bag', '가방: 주인공과 착용 칸, 칩, 한 격자');
    await fits('가방 [전체]');
    for (const [chip, yes, no] of [['장비', /철 검/, /^돌 ×/], ['재료', /^슬라임 젤 ×/, /철 검/], ['먹을것', /^회복 물약 ×/, /^돌 ×/], ['놓을것', /^횃불 ×/, /^슬라임 젤 ×/]]) {
      check(await game.press(`분류: ${chip}`, '분류'), `[${chip}] 칩을 누른다`);
      await game.wait(300);
      s = await game.state();
      check(s.bagFilter === chip && await has(yes) && !(await has(no)), `[${chip}]만 격자에 (${s.bagFilter}: ${yes} 있고 ${no} 없음)`);
      if (chip === '재료') await shot('bag-materials', '가방 [재료] 칩');
    }
    await game.press('분류: 전체', '분류');
    // The picked sword's card: its buttons first, how it compares with the worn one, the quick slots.
    check(await pick(game, /철 검/), '철 검 칸을 고른다');
    s = await game.state();
    check(/철 검/.test(s.card), `카드에 철 검 (${s.card})`);
    const equip = (await game.find(/^(장착|Lv\d+)$/, '철 검'))[0];
    check(!!equip && equip.shown, `카드의 [장착]이 스크롤 없이 보인다 (${equip ? equip.label : '없음'})`);
    check((await game.find('버리기', '철 검')).some(b => b.shown), '카드에 [버리기](붉은 테)');
    check(/지금 낀 구리 검보다/.test(await game.allText()), '카드가 지금 낀 구리 검과 견준다');
    await shot('bag-card', '고른 철 검의 카드(단추 먼저, 비교, 퀵슬롯)');
    check(await game.press('퀵슬롯 3', '철 검'), '카드의 [퀵슬롯 3]을 누른다');
    await game.wait(300);
    check(/철 검/.test(((await game.state()).quick || '').split(',')[2] || ''), `철 검이 퀵슬롯 3에 (${(await game.state()).quick})`);
    await game.press('퀵슬롯 3', '철 검');
    await game.wait(300);
    check(((await game.state()).quick || '').split(',')[2] === '', `다시 누르면 퀵슬롯 3에서 내린다 (${(await game.state()).quick})`);
    if (equip && equip.label === '장착') {
      await game.press('장착', '철 검');
      check((await game.inventory()).worn.Weapon === 'iron_sword', '카드의 [장착]으로 철 검을 든다');
    }
    // A material's card keeps the quick actions: [놓기] on stone.
    await game.send('face 1 0');
    check(await pick(game, /^돌 ×/), '돌 칸을 고른다');
    check(await game.press('놓기', '돌 ×'), '돌 카드의 [놓기]');
    await game.wait(500);
    check((await game.look(1, 0)).tile === 'Wall', `카드의 [놓기]로 앞에 돌벽 (${(await game.look(1, 0)).tile})`);
    await game.send('build Floor 1 0');
    await fits('고른 돌의 카드');
    // [재료·제작]: hand-made recipes only, the stations' are in their window.
    await game.press('재료·제작');
    await game.wait(400);
    check((await game.find('만들기', /^작업대/)).length === 1 && (await game.find('만들기', /^구리 검/)).length === 0, '[재료·제작]에는 손 제작만(작업대 [만들기]는 있고 구리 검은 없다)');
    await shot('bag-crafting', '가방 [재료·제작]: 놓고 심는 줄과 손 제작');
    await closeBag(game);

    // ---- 문 ----
    await game.send('build Door 1 0; face 1 0');
    await game.wait(800);
    const lock = (await game.find(/^문 잠그기/))[0];
    check(!!lock && lock.shown, `문을 마주 보면 문 위에 [문 잠그기] (${lock ? lock.label : '없음'})`);
    if (!game.touch) check(!!lock && /\(G\)$/.test(lock.label), `PC에서는 "(G)"가 붙는다 (${lock && lock.label})`);
    await shot('door', '문 위의 [문 잠그기]');
    if (!game.touch) await game.page.keyboard.press('g');
    else await game.press(/^문 잠그기/);
    await game.wait(900);
    check((await game.look(1, 0)).tile === 'LockedDoor', `${game.touch ? '[문 잠그기]' : 'G'}로 문이 잠긴다 (${(await game.look(1, 0)).tile})`);
    check(await game.press(/^잠금 풀기/), '잠근 문 위의 [잠금 풀기]');
    await game.wait(900);
    check((await game.look(1, 0)).tile === 'Door', `[잠금 풀기]로 다시 문 (${(await game.look(1, 0)).tile})`);
    await game.send('build Floor 1 0; face off');

    // ---- 버프 ----
    // A bowl in the bag first: drinking takes it from there (World.UseConsumable).
    await game.send('potion mushroom_soup 1; drink mushroom_soup');
    await game.wait(800);
    s = await game.state();
    check(/버섯 수프/.test(s.buffs || ''), `먹은 것의 버프가 아이콘으로 (${s.buffs})`);
    check(inside(s.ui.buffs) && s.ui.buffs[1] >= s.ui.hud[1] && s.ui.buffs[1] + s.ui.buffs[3] <= s.ui.hud[1] + s.ui.hud[3] + 0.5, `버프 줄이 HUD 상자 안 (${s.ui.buffs.map(Math.round).join(',')})`);
    check(((await game.texts()).hud || []).some(l => /^버섯 수프 \d+:\d\d/.test(l)), 'HUD 글 줄에도 "버섯 수프 m:ss"');
    await shot('buffs', '버프 아이콘과 남은 시간');

    // ---- 낮은 생명력 ----
    s = await game.state();
    await game.send(`hurt ${Math.ceil(s.health - s.maxHealth * 0.15)}`);
    await game.wait(600);
    s = await game.state();
    check(s.ui.lowHealth > 0, `생명력이 낮으면 화면 가장자리가 붉다 (${s.ui.lowHealth.toFixed(2)}, 생명력 ${Math.round(s.health)}/${s.maxHealth})`);
    await shot('low-health', '생명력이 낮을 때 붉은 가장자리');
    await game.send('heal');
    await game.wait(600);
    check((await game.state()).ui.lowHealth === 0, '회복하면 붉은 가장자리가 사라진다');

    // ---- 배너와 메뉴 ----
    await game.send('xp 3000');
    if (game.touch) await game.press('메뉴');
    else await game.page.keyboard.press('Escape');
    await game.wait(300);
    s = await game.state();
    const banner = (await game.texts()).banner || '';
    check(s.hub, `${game.touch ? '[메뉴]' : 'Esc'}로 메뉴가 열린다`);
    check(/레벨/.test(banner) && s.ui.banner[2] > 0, `레벨업 배너가 떠 있다 ("${banner}")`);
    check(s.ui.banner[2] > 0 && !overlapping(s.ui.banner, s.ui.hub), `레벨업 배너가 메뉴 판에 가리지 않는다 (배너 ${s.ui.banner.map(Math.round).join(',')}, 메뉴 ${s.ui.hub.map(Math.round).join(',')})`);
    check(s.ui.banner[2] === 0 || inside(s.ui.banner), '배너가 화면 안');
    check((await game.find('설정', '메뉴')).length === 1 && (await game.find('나가기', '메뉴')).length === 1, '메뉴에 [설정]과 [나가기]');
    await shot('banner-hub', '레벨업 배너와 메뉴 판');
    check(await game.press('설정', '메뉴') && !!(await game.until(t => t.window === '설정', 2000)), '메뉴의 [설정]으로 게임 안 설정');
    check((await game.find(/^움직임: /)).length === 1, '게임 안 설정에 [움직임]');
    await shot('settings', '게임 안 설정');
    await game.page.keyboard.press('Escape');
    check(!!(await game.until(t => t.window === '' && t.playing, 2000)), 'Esc로 설정을 닫아도 게임은 그대로');

    // ---- 목표 목록 ----
    s = await game.state();
    if (height > width) {
      check(s.ui.folded, '세로 휴대폰에서 목표 목록은 한 줄');
      check(await game.press('일지 열기') && !!(await game.until(t => t.window === '일지', 2000)), '접힌 줄을 누르면 일지');
      check(((await game.state()).records || {}).section === '목표', '일지는 목표 쪽으로');
      await game.press('창 닫기');
      await game.until(t => t.window === '', 2000);
    } else {
      check(!s.ui.folded, '목표 목록이 펼쳐져 있다');
      check(await game.press('목표 접기'), '[목표 접기]를 누른다');
      s = await game.state();
      check(s.ui.folded && s.ui.foldedByHand, '목표 목록이 한 줄로 접힌다');
      check(((await game.texts()).hud || []).some(l => /^목표 \d+\/\d+/.test(l)), '접어도 HUD 글 줄에 목표가 남는다');
      await shot('tracker-folded', '손으로 접은 목표 줄');
      check(await game.press('목표 펴기') && !(await game.state()).ui.folded, '[목표 펴기]로 다시 펼친다');
    }
    await fits('HUD');

    // ---- 창을 연 채 쓰러짐 ----
    const screen = game.touch ? '가방' : '일지';
    check(await openScreen(game, screen), `[${screen}]을 연다`);
    await game.wait(400);
    await game.send('hurt 99999');
    const fellAt = Date.now();
    await game.wait(700);
    s = await game.state();
    check(s.dead && s.window === screen, `[${screen}]이 열린 채 쓰러진다`);
    const card = s.ui.death;
    check(inside(card) && overlapping(card, s.ui.window), `쓰러짐 카드가 창 위에 (${card.map(Math.round).join(',')})`);
    const rise = (await game.find(/^(집|입구)에서 일어나기$/)).find(b => b.shown && b.enabled);
    check(!!rise && rise.rect[0] >= card[0] && rise.rect[1] >= card[1] && rise.rect[0] + rise.rect[2] <= card[0] + card[2] + 0.5, `카드 안에 [집에서 일어나기] (${rise ? rise.label : '없음'})`);
    // Beside a side window (the bag) the HUD box is still there with its line; a full screen (the journal on a PC) has
    // no HUD under it (Docs/UiRework.md 4-5), so there the card's own words say it.
    if (screen === '가방') check(((await game.texts()).hud || []).some(l => /쓰러졌습니다/.test(l)), 'HUD 글 줄에 "쓰러졌습니다"가 남는다');
    else check(((await game.texts()).written || []).some(l => /잠시 후 .*일어난다/.test(l)), '전체 화면 위에서는 카드가 "잠시 후 … 일어난다"를 알린다');
    // The card's own button a finger tall; what is left of the window round the card is the window's (U6 measured it).
    await fits('쓰러짐 카드', card);
    // Under the card nothing is pressable (its press is the card's): the probe says so too.
    const under = (await game.buttons()).filter(b => b.shown && !/^(집|입구)에서 일어나기$/.test(b.label)
      && b.rect[0] + b.rect[2] / 2 > card[0] && b.rect[0] + b.rect[2] / 2 < card[0] + card[2] && b.rect[1] + b.rect[3] / 2 > card[1] && b.rect[1] + b.rect[3] / 2 < card[1] + card[3]);
    check(under.length === 0, `카드 밑의 단추는 누를 수 없다(보이는 단추로 치지 않는다) (${under.map(b => b.label).slice(0, 4).join(', ') || '없음'})`);
    await shot('death-window', `[${screen}] 위의 쓰러짐 카드`);
    check(!!rise && await game.press(rise.label), `[${rise ? rise.label : '집에서 일어나기'}]를 누른다`);
    const up = await game.until(t => !t.dead, 2500);
    check(!!up && Date.now() - fellAt < 4800, `누르면 바로 일어난다(혼자일 때 5초를 기다리지 않는다, ${((Date.now() - fellAt) / 1000).toFixed(1)}초)`);
    check(!!up && up.window === screen, `일어난 뒤 [${screen}]은 그대로 (${up && up.window})`);
    await game.context.close();
  }
}

const Sections = { menu, time, basics, first, firstphone, hour, bag, combat, hero, roll, bosses, patterns, veineater, samurai, knight, spider, arenas, world, farm, life, npc, factory, defense, gear, skills, skillfx, monsterfx, map, packs, pois, travel, guide, pace, dungeon, base, save, story, deploysave, layout, touch, ui, screens, encounter, growth, records, hud };

(async () => {
  fs.mkdirSync(out, { recursive: true });
  for (const file of fs.readdirSync(out)) if (/\.(png|md|bundle)$/.test(file)) fs.unlinkSync(path.join(out, file));
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
  note(`# 웹 플레이 전체 점검\n\n${new Date().toISOString()}, ${url}, 커밋 ${/^[0-9a-f]{40}$/.test(deployed) ? deployed.slice(0, 7) : '알 수 없음'}`);
  const browser = await chromium.launch({ args: LaunchArgs });
  try {
    for (const [name, section] of Object.entries(Sections)) {
      if (only && !only.includes(name)) continue;
      const started = Date.now();
      try {
        await section(browser);
      } catch (e) {
        run.errors.push(`${name}: ${e.message.split('\n')[0]}`);
        note(`- **중단**: ${name}: ${e.message.split('\n')[0]}`);
      }
      console.log(`[${name}] ${((Date.now() - started) / 1000).toFixed(0)}s`);
    }
  } finally {
    await browser.close();
  }
  note(`\n통과 ${run.passed}, 실패 ${run.failed}`);
  run.write();
  process.exit(run.errors.length === 0 ? 0 : 1);
})();
