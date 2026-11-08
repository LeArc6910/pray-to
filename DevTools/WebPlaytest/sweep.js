#!/usr/bin/env node
// The long web playtest: goes through the checks LOCAL_TASKS.md asked people to make by hand (sections 2 to 7), on
// the deployed web demo, with real clicks, keys and taps on the game's own buttons (Ui.LastDrawn), and the game's
// playtest remote control (Client/Playtest.cs) to set scenes up and read the result. Writes pictures and report.md
// to out/sweep. Docs/Playtest.md.
//
//   node DevTools/WebPlaytest/sweep.js [--wait [SHA]] [--only menu,basics,...] [--url URL] [--out DIR]
//
// Sections: menu basics bag combat bosses world farm life npc factory defense gear skills base save deploysave layout touch
'use strict';
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
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

const count = (inv, id) => (inv.materials || {})[id] || 0;
const items = (inv, base) => (inv.items || []).filter(i => i.base === base).length;

// ---------------------------------------------------------------- 메뉴와 설정 (6·14·17·21·24·36·37·46·50단계)
async function menu(browser) {
  note('\n## 메뉴와 설정 (PC 1280×720)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.shot('menu', '메뉴');
  let buttons = await game.buttons();
  const labels = buttons.map(b => b.label);
  check(labels.includes('혼자 하기'), '메뉴에 [혼자 하기]');
  for (const word of ['어둠과 빛:', '효과음:', '음악:', '개발 정보:', '글꼴:', '목표 안내:', '화면 흔들림:', '키 바꾸기', '온라인 방 만들기', '코드로 참가', '저장본: 고르기·내보내기·가져오기']) {
    check(labels.some(l => l.startsWith(word)), `메뉴에 [${word}…] 단추`);
  }
  const classes = labels.filter(l => /^\[[ \d]\] /.test(l));
  check(classes.length === 5, `클래스 단추가 다섯 (${classes.join(', ')})`);
  check(classes.includes('[1] 전사') && classes.includes('[2] 화염 마법사'), '처음 고른 클래스는 [1] 전사, [2] 화염 마법사');

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
  await game.shot('menu-smooth', '매끈한 글꼴의 메뉴');
  await game.press('글꼴: 매끈');
  await game.press('개발 정보: 꺼짐');
  await game.page.reload();
  await game.page.waitForFunction(() => !!window.unityInstance, null, { timeout: 240000 });
  await game.wait(2500);
  await game.send('probe on');
  await game.wait(300);
  check((await game.find('어둠과 빛: 꺼짐')).length === 1, '새로 고쳐도 [어둠과 빛: 꺼짐]이 남는다');
  check((await game.find('개발 정보: 켜짐')).length === 1, '새로 고쳐도 [개발 정보: 켜짐]이 남는다');
  await game.startSolo();
  await game.wait(1500);
  await game.shot('no-darkness-devinfo', '어둠 없이, 개발 정보 창(왼쪽 아래)');
  await game.page.keyboard.press('Escape');
  await game.until(s => !s.playing, 8000);
  await game.press('어둠과 빛: 꺼짐');
  await game.press('개발 정보: 켜짐');

  // Classes: a third one, then the first out; the numbers follow the order.
  await game.press('[ ] 궁수');
  check((await game.find('[3] 궁수')).length === 1, '[ ] 궁수를 누르면 [3] 궁수');
  await game.press('[1] 전사');
  buttons = (await game.buttons()).map(b => b.label);
  check(buttons.includes('[1] 화염 마법사') && buttons.includes('[2] 궁수') && buttons.includes('[ ] 전사'), `전사를 빼면 번호가 당겨진다 (${buttons.filter(l => /^\[/.test(l)).join(', ')})`);
  await game.shot('menu-classes', '클래스를 바꾼 메뉴');
  // Back to 전사, 화염 마법사 without ever leaving none picked.
  await game.press('[2] 궁수');
  await game.press('[ ] 전사');
  await game.press('[1] 화염 마법사');
  await game.press('[ ] 화염 마법사');
  check((await game.find('[1] 전사')).length === 1 && (await game.find('[2] 화염 마법사')).length === 1, '클래스를 처음대로 되돌린다');

  // Keys: skill 1 to the "1" key; W is refused; Esc gives up.
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
    check((await game.find('1', '스킬 1')).length === 1, 'Esc는 그만두기(1 그대로)');
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
    if ((await game.find('처음 키로')).length === 0) await game.press('키 바꾸기');
    await game.press('처음 키로');
    check((await game.find('Q', '스킬 1')).length === 1, '[처음 키로]로 Q로 돌아온다');
    await game.press('키 바꾸기 닫기', null, { quiet: true });
  }

  // Online without a cloud project: a plain message.
  await game.press('온라인 방 만들기');
  await game.wait(6000);
  const said = (await game.texts()).status || '';
  check(said.length > 0, `[온라인 방 만들기]가 알림을 띄운다 ("${said.slice(0, 80)}")`);
  await game.shot('online', '온라인 방 만들기 뒤');

  await game.press('저장본: 고르기·내보내기·가져오기');
  await game.shot('menu-saves', '저장본 목록');
  await game.context.close();

  // The first Tab after starting from the menu by a real click on [혼자 하기] (LOCAL_TASKS.md, 13: the first key
  // once went unheard). Typing in the name field first, as a person would.
  const fresh = await open(browser, run, Profiles.pc, url);
  // The menu may not be drawn yet with the probe on: look again for a few seconds.
  let solo = null;
  for (let i = 0; i < 20 && !solo; i++) {
    solo = (await fresh.find('혼자 하기'))[0];
    if (!solo) await fresh.wait(300);
  }
  if (solo) {
    // The name field sits above the buttons in the left column: a click there, a name typed, then [혼자 하기].
    await fresh.tapAt(solo.cx, solo.cy - 120);
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
    check(false, '메뉴에 [혼자 하기]');
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
  check(/목표 1\/\d+: 돌 10개 모으기 \(0\/10\)/.test(hud), `목표 목록에 첫 목표와 진행도 (${(hud.match(/목표[^|]*/) || [''])[0]})`);
  check(has(hud, '보상:'), '목표 아래에 보상');
  // The controls lines began "WASD 이동" (PC) or "왼쪽 스틱 이동" (touch); a goal's hint may still say "공격 스틱".
  check(!/WASD|왼쪽\s스틱|보스 아레나:/.test(hud), '조작법·아레나 방향 줄이 HUD에 없다');
  await game.shot('first', '새 캐릭터 첫 화면');

  let inv = await game.inventory();
  check(inv.worn.Weapon === 'copper_sword', `처음 무기는 구리 검 (${inv.worn.Weapon})`);
  check(items(inv, 'copper_pickaxe') === 1, '가방에 구리 곡괭이');
  check((inv.consumables || {}).healing_potion === 3, `회복 물약 3개 (${(inv.consumables || {}).healing_potion})`);
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
  check(/목표 2\//.test((t.hud || []).join(' ')), '다음 목표로 넘어간다');
  await game.shot('goal', '첫 목표를 이룬 뒤');

  // Placing a stone wall from the bag, then a workbench made and placed.
  await game.send('give stone 30; face 1 0');
  await bagTab(game, '재료·제작');
  await game.shot('bag-materials', '가방 [재료·제작]');
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
  check(items(await game.inventory(), 'copper_sword') + ((await game.inventory()).worn.Weapon === 'copper_sword' ? 1 : 0) > swordsBefore, '작업대 곁에서 구리 검을 만든다');
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
  // A click inside the bag does not swing: on the empty space under the stats button.
  const inBag = (await game.buttons()).find(b => b.shown && /^능력치·생활 기술/.test(b.label));
  await game.page.mouse.move(inBag.cx, inBag.cy + 80);
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

// ---------------------------------------------------------------- 보스 넷 (M3, 19·31·40단계)
async function bosses(browser) {
  note('\n## 보스 (PC)\n');
  const game = await open(browser, run, Profiles.pc, url);
  await game.startSolo();
  await game.wait(1200);
  await game.send('equip void_sword void_helmet void_armor void_boots; heal');
  let s = await game.state();
  check(/cave_warden/.test(s.bosses) && /crystal_queen/.test(s.bosses) && /lava_titan/.test(s.bosses) && /void_lord/.test(s.bosses), `보스 넷이 월드에 있다 (${s.bosses})`);

  // Leaving the arena puts the boss back as it was.
  await game.send('goto boss cave_warden');
  await game.wait(2500);
  await game.send('boss cave_warden hurt 0.2');
  await game.send('goto spawn');
  await game.wait(7000);
  s = await game.state();
  const warden = (s.bosses.match(/cave_warden:(\d+)\/(\d+):p(\d):(\w+)/) || []);
  check(warden[1] === warden[2], `아레나를 떠나 5초가 지나면 보스가 처음 상태로 (${warden.slice(1).join(' ')})`);

  for (const [id, name] of [['cave_warden', '동굴 수호자'], ['crystal_queen', '수정 거미 여왕'], ['lava_titan', '용암 거인'], ['void_lord', '공허의 군주']]) {
    await game.send(`heal; goto boss ${id}`);
    const woke = await game.until(t => has(t.banner, '등장'), 6000, game.texts);
    check(!!woke, `${name}: "${name} 등장!" 배너`);
    await game.wait(2500);
    await game.shot(`boss-${id}`, `${name} 싸움(예고 장판, 체력 막대)`);
    await game.send(`heal; boss ${id} hurt 0.36`);
    const two = await game.until(t => has(t.banner, '2페이즈'), 3000, game.texts);
    check(!!two, `${name}: 체력 66% 아래에서 "2페이즈!"`);
    await game.wait(2500);
    await game.send(`heal; boss ${id} hurt 0.34`);
    const three = await game.until(t => has(t.banner, '3페이즈'), 3000, game.texts);
    await game.wait(2500);
    await game.shot(`boss-${id}-3`, `${name} 3페이즈`);
    if (id === 'cave_warden' || id === 'crystal_queen') check(!!three, `${name}: 체력 33% 아래에서 "3페이즈!"`);
    await game.send(`heal; boss ${id} hurt 1`);
    const dead = await game.until(t => has(t.banner, '처치'), 4000, game.texts);
    check(!!dead, `${name}: "${name} 처치!" 배너`);
    await game.wait(1200);
    const after = await game.state();
    check(after.loot > 0, `${name}: 잡으면 떨어뜨린다 (바닥 ${after.loot}개)`);
    await game.shot(`boss-${id}-drops`, `${name}의 드랍`);
  }
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
  for (const [biome, name] of [['Mushroom', '버섯 숲'], ['Ice', '얼음 동굴'], ['Crystal', '수정 동굴'], ['Lava', '용암 지대'], ['Abyss', '공허의 심연']]) {
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
  check((coop.output || {}).egg > 0, `닭장에 하루 뒤 달걀 (${JSON.stringify(coop.output)})`);
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
  await game.shot('merchant', '상인 노점');
  await bagTab(game);
  check(await game.press('상인'), '노점 곁에서 가방에 [상인] 탭');
  await game.press('말 걸기');
  check(((await game.inventory()).friends || {}).merchant > 0, '[말 걸기]로 호감이 오른다');
  await game.press('사기');
  const potions = (await game.inventory()).consumables.healing_potion;
  await game.press('사기', '회복 물약');
  check((await game.inventory()).consumables.healing_potion === potions + 1, '금화로 회복 물약을 산다');
  await game.shot('shop-buy', '[사기]');
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
  await closeBag(game);
  await game.send('clock 2 7');
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
  await game.press('특성');
  await game.shot('passives', '[특성] 탭');
  const node = (await game.buttons()).find(b => b.shown && /: /.test(b.label) && b.label.length > 4);
  if (node) {
    await game.press(node.label);
    await game.press('배우기');
  }
  check((await game.inventory()).passives === 1, '점을 골라 [배우기]');
  await game.press('모두 되돌리기');
  check((await game.inventory()).passives === 0, '[모두 되돌리기]');

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
  const skills = (await game.inventory()).skills;
  await game.press('▼');
  check((await game.inventory()).skills !== skills, `▼로 스킬 칸 순서를 바꾼다 (${skills} → ${(await game.inventory()).skills})`);
  await game.press('[ ] 궁수');
  await game.press('이대로 바꾸기');
  check(/archer/.test((await game.inventory()).classes), `작업대 곁에서 클래스를 바꾼다 (${(await game.inventory()).classes})`);
  await closeBag(game);
  await game.shot('archer', '궁수가 된 스킬 칸');
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
  await game.press('저장본: 고르기·내보내기·가져오기');
  const [download] = await Promise.all([game.page.waitForEvent('download', { timeout: 15000 }).catch(() => null), game.press('전부 내보내기')]);
  check(!!download, '[전부 내보내기]가 파일을 내려받는다');
  if (!download) return game.context.close();
  const file = path.join(out, 'saves.bundle');
  await download.saveAs(file);
  note(`  - 내려받은 파일: ${download.suggestedFilename()} (${fs.statSync(file).size}바이트)`);
  await game.context.close();

  const other = await open(browser, run, Profiles.pc, url);
  await other.press('저장본: 고르기·내보내기·가져오기');
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
    await game.shot('menu', '메뉴');
    const menuButtons = await game.buttons();
    const solo = menuButtons.find(b => b.label === '혼자 하기');
    check(solo && solo.shown && solo.rect[1] + solo.rect[3] <= solo.screen.height, '[혼자 하기]가 처음부터 화면 안에 보인다');
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
    for (const tab of ['장비', '재료·제작', '스킬', '특성']) {
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
    check(await game.press('혼자 하기'), '[혼자 하기]를 탭한다');
    check(!!(await game.until(s => s.playing, 25000)), '탭으로 게임이 시작된다');
    await game.page.touchscreen.tap(width / 2, height / 3);
    await game.wait(800);
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
  const commands = ['build StoneFloor -3 2', 'build BrickFloor -2 2', 'build GlowFloor -1 2', 'build Table -3 -2', 'build Banner -1 -2', 'build Door 2 0'];
  for (let dy = -4; dy <= 4; dy++) if (dy !== 0) commands.push(`build Wall 2 ${dy}`);
  await game.send(commands.join('; '));
  await game.wait(1000);
  const tiles = [];
  for (const [dx, dy] of [[-3, 2], [-2, 2], [-1, 2], [-3, -2], [-1, -2], [2, 0]]) tiles.push((await game.look(dx, dy)).tile);
  check(tiles.join(',') === 'StoneFloor,BrickFloor,GlowFloor,Table,Banner,Door', `바닥 셋, 탁자, 깃발, 문이 놓인다 (${tiles.join(', ')})`);
  await game.shot('base', '돌·벽돌·빛 바닥, 문 달린 벽, 탁자와 깃발');
  // A monster on the far side stays behind the door; the player walks through it.
  await game.send('spawn slime 4 0 5000');
  await game.wait(2500);
  const start = await game.state();
  await game.page.keyboard.down('d');
  await game.wait(1400);
  await game.page.keyboard.up('d');
  const after = await game.state();
  check(after.x > start.x + 2.2, `플레이어는 문을 지나간다 (x ${start.x.toFixed(1)} → ${after.x.toFixed(1)})`);
  await game.shot('base-door', '문을 지나간 뒤');
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

const Sections = { menu, basics, bag, combat, bosses, world, farm, life, npc, factory, defense, gear, skills, base, save, deploysave, layout, touch };

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
