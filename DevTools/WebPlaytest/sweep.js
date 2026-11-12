#!/usr/bin/env node
// The long web playtest: goes through the checks LOCAL_TASKS.md asked people to make by hand (sections 2 to 7), on
// the deployed web demo, with real clicks, keys and taps on the game's own buttons (Ui.LastDrawn), and the game's
// playtest remote control (Client/Playtest.cs) to set scenes up and read the result. Writes pictures and report.md
// to out/sweep. Docs/Playtest.md.
//
//   node DevTools/WebPlaytest/sweep.js [--wait [SHA]] [--only menu,basics,...] [--url URL] [--out DIR]
//
// Sections: menu time basics first firstphone hour bag combat bosses patterns veineater samurai world farm life npc factory defense gear skills base save deploysave layout touch
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

// A tab of the bag window, opened.
async function bagTab(game, tab) {
  if (!(await game.state()).bag) {
    if (game.touch) await game.press('가방');
    else await game.page.keyboard.press('Tab');
    await game.wait(500);
  }
  if (tab) await game.press(tab);
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
  await game.page.keyboard.press('Escape');
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
    await game.page.keyboard.press('Escape');
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
  await game.press('만들기', /^구리 검/);
  // Gear takes time by hand (2026-11-09): a bar fills for 3 seconds while the player stays by the workbench.
  await game.wait(800);
  check(/만드는 중: 구리 검/.test(await game.allText()), '구리 검은 바로 나오지 않고 "만드는 중: 구리 검"과 막대가 보인다');
  await game.shot('bag-crafting', '만드는 중의 막대와 [그만]');
  const swordDone = await game.until(i => items(i, 'copper_sword') + (i.worn.Weapon === 'copper_sword' ? 1 : 0) > swordsBefore, 5000, game.inventory);
  check(!!swordDone, '작업대 곁에서 3초 뒤 구리 검이 나온다');
  await game.shot('bag-crafted', '작업대 곁 제작 목록');
  // [만들 수 있는 것만]: the list shrinks to what the bag and the stations near make now, and back.
  const all = (await game.find('만들기')).length;
  check(await game.press('만들 수 있는 것만'), '[만들 수 있는 것만]을 누른다');
  const some = (await game.find('만들기')).length;
  check(some > 0 && some < all, `만들 수 있는 것만 남는다 (${all} → ${some})`);
  await game.shot('bag-craftable', '만들 수 있는 것만 본 제작 목록');
  await game.press('모두 보기');

  // A chest beside it: the chest tab, put in and take out.
  await game.send('give chest 1; face -1 0');
  await game.wait(300);
  await game.press('놓기', '상자 ×');
  check((await game.look(-1, 0)).tile === 'Chest', '상자를 놓는다');
  check(await game.press('상자'), '상자 곁에서 [상자] 탭이 생긴다');
  await game.press('넣기', /^돌 ×/);
  check(((await game.look(-1, 0)).chest || {}).stone > 0, '[넣기]로 상자에 돌이 들어간다');
  await game.shot('bag-chest', '[상자] 탭');
  await game.press('꺼내기', /^돌 ×/);
  check(!((await game.look(-1, 0)).chest || {}).stone, '[꺼내기]로 다시 가방에');
  // [상자에 넣기] (2026-11-11, Docs/Research2026-11.md 후보 8): what a chest nearby holds already goes in whole;
  // what none holds, and what the player locked, stay in the bag.
  await game.send('fill -1 0 stone 1; fill -1 0 slime_gel 1; give slime_gel 4; give copper_ore 3');
  await game.wait(800);
  await bagTab(game, '재료·제작');
  check(await game.press(/^슬라임 젤 ×/), '재료 칸에서 슬라임 젤을 고른다');
  check(await game.press('잠그기'), '고른 재료의 [잠그기]를 누른다');
  check((await game.find('잠금 풀기')).length === 1, '잠그면 [잠금 풀기]로 바뀐다');
  await game.shot('bag-lock', '잠근 슬라임 젤');
  await bagTab(game, '상자');
  check(await game.press('상자에 넣기'), '[상자] 탭에 [상자에 넣기]가 있다');
  await game.wait(800);
  let stashed = await game.inventory();
  const inChest = (await game.look(-1, 0)).chest || {};
  check(count(stashed, 'stone') === 0 && inChest.stone > 1, `상자에 이미 있는 돌은 모두 상자로 (가방 ${count(stashed, 'stone')}, 상자 ${inChest.stone})`);
  check(count(stashed, 'slime_gel') >= 4, `잠근 슬라임 젤은 가방에 남는다 (${count(stashed, 'slime_gel')})`);
  check(count(stashed, 'copper_ore') >= 3, `상자에 없는 구리 광석은 남는다 (${count(stashed, 'copper_ore')})`);
  await game.shot('bag-stash', '[상자에 넣기] 뒤');
  await bagTab(game, '재료·제작');
  await game.press(/^슬라임 젤 ×/);
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
    const woke = await game.until(t => has(t.banner, '등장'), 6000, game.texts);
    check(!!woke, `${name}: "${name} 등장!" 배너`);
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
      await game.until(t => has(t.banner, '등장'), 6000, game.texts);
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
  const woke = await game.until(t => has(t.banner, '그룸'), 8000, game.texts);
  check(!!woke, `철곡 광산촌 곁 아레나에서 깨면 "광맥 포식자 그룸 등장" 배너 (${woke ? woke.banner : '없음'})`);
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
  const woke = await game.until(t => has(t.banner, '시구레'), 8000, game.texts);
  check(!!woke, `잊힌 신전 곁 아레나에서 깨면 "벚꽃 검귀 시구레 등장" 배너 (${woke ? woke.banner : '없음'})`);
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
  await bagTab(game);
  check(await game.press('상인'), '노점 곁에서 가방에 [상인] 탭');
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
  // The journal: the merchant's story one step along, and today's request.
  await game.press('일지');
  const journal = (await game.buttons()).length >= 0;
  await game.shot('journal', '[일지] 탭');
  // The bestiary and the achievements, two more pages of the journal (Docs/Guide.md 도감과 업적).
  check(await game.press('도감'), '[일지]의 [도감]을 누른다');
  await game.shot('journal-bestiary', '[일지] 도감');
  check(await game.press('업적'), '[일지]의 [업적]을 누른다');
  await game.shot('journal-achievements', '[일지] 업적');
  const seen = await game.inventory();
  check(seen.records > 0, `가진 재료와 장비가 도감에 든다 (${seen.records})`);
  await game.press('목표·이야기');
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
  await bagTab(game);
  check(journal && await game.press('캐릭터'), '[캐릭터] 탭을 연다');
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
  await bagTab(game);
  check(await game.press('연금술사'), '연금술사 곁에서 가방에 [연금술사] 탭');
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
    await bagTab(game);
    check((await game.find(role)).length === 1, `${role} 곁에서 가방 탭 이름이 "${role}"`);
    await game.press(role);
    await game.shot(`npc-${tile}`, `${role}의 탭`);
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
    '방에서도 [상인] 탭이 열리고 [이사 권하기]는 없다');
  await game.shot('moved-in-tab', '방에 사는 상인의 탭');
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
  await game.press('특성');
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
  const skills = (await game.inventory()).skills;
  await game.press('▼');
  check((await game.inventory()).skills !== skills, `▼로 스킬 칸 순서를 바꾼다 (${skills} → ${(await game.inventory()).skills})`);
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
  await bagTab(game, '재료·제작');
  const mended = await game.press(/^고치기/);
  check(mended, '금이 간 철벽을 마주 보면 가방 [재료·제작]에 [고치기 (철 주괴 1)]');
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
  await game.page.keyboard.press('Escape');
  await game.until(s => !s.playing, 8000);
  await game.shot('menu-saved', '나간 뒤 메뉴(이어하기)');
  // The menu remembers the character's classes (2026-11-09) instead of asking again.
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
  await game.page.keyboard.press('Escape');
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
  await game.page.keyboard.press('Escape');
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
    await game.press('재료·제작');
    await game.wait(500);
    // The first row showing, by its text and height: a drag that scrolls moves it.
    const firstRow = async () => {
      const shown = (await game.buttons()).filter(b => b.shown && (b.label === '만들기' || b.label === '놓기'));
      return shown.length ? `${shown[0].row.slice(0, 10)}@${Math.round(shown[0].cy)}` : '';
    };
    const before = await firstRow();
    const box = await game.canvas();
    const list = (await game.buttons()).filter(b => b.shown && (b.label === '놓기' || b.label === '만들기'));
    if (list.length > 0) await game.drag(list[0].cx - 120, box.top + box.height * 0.75, 0, -box.height * 0.3, 200);
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
    // The skill was just used above: its cooldown (about 12 seconds) runs out first.
    await game.wait(12000);
    await game.send('goto wild; face off; heal; mana 100; clear; spawn slime 0 3 5000');
    await game.wait(1500);
    await game.press('skill 1');
    await game.wait(600);
    // The aim the press sent (the host's facing is the client's again by the next input).
    const aimed = ((await game.state()).skillAim || '0,0').split(',').map(Number);
    check(aimed[1] > 0.6, `스틱 없이 스킬 단추만 탭해도 가장 가까운 적(위쪽)으로 쓴다 (조준 ${aimed.join(',')})`);
    await game.shot('touch-autoaim', '스틱 없이 쓴 스킬');
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
  await game.send('goto near Door');
  await game.wait(500);
  let [ddx, ddy] = await doorFrom();
  await game.send(`face ${ddx} ${ddy}`);
  await game.wait(300);
  await bagTab(game, '재료·제작');
  check(await game.press('문 잠그기'), `문을 마주 보면 가방 [재료·제작]에 [문 잠그기] (문이 ${ddx},${ddy})`);
  await game.wait(900);
  check((await game.look(ddx, ddy)).tile === 'LockedDoor', `[문 잠그기]로 문이 잠긴다 (${(await game.look(ddx, ddy)).tile})`);
  check((await game.find('잠금 풀기')).length > 0, '내가 잠근 문 앞에서는 [잠금 풀기]');
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
  await game.send(`goto ${doorX + side - Math.floor(past.x)} ${doorY - Math.floor(past.y)}; face ${-side} 0`);
  await game.wait(500);
  [ddx, ddy] = await doorFrom();
  await bagTab(game, '재료·제작');
  check(await game.press('잠금 풀기'), '안쪽에서도 [잠금 풀기]');
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
  // The journal's story so far (2026-11-11): the chapters passed, the answer given, and the side stories.
  await bagTab(game, '일지');
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
  await game.page.keyboard.press('Escape');
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
  await bagTab(game, '재료·제작');
  // The list is long by now; a person scrolls or shows only what can be made. The bot does the second.
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
  await bagTab(game, '재료·제작');
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
    const craft = async name => {
      await bagTab(game, '재료·제작');
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
    await bagTab(game);
    await game.press('상인');
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

const Sections = { menu, time, basics, first, firstphone, hour, bag, combat, bosses, patterns, veineater, samurai, world, farm, life, npc, factory, defense, gear, skills, base, save, story, deploysave, layout, touch };

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
