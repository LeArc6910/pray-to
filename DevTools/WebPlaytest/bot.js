// The web playtest's bot by the game's state, not the screen (Docs/Playtest.md, 상태로 움직이는 봇, 2026-11-11). It asks
// the game where to go next ("nav <target>", Client/PlaytestBot.cs: the same path finding, memory and getting-nowhere
// as the sim's FirstHourBot on the host's Core) and does it the way a person would, with the hooks the caller gives:
// real keys and mouse on PC, the sticks on a phone. When the game says the bot is getting nowhere it is written down
// with a picture and the bot's small map; a target it gives up on comes back as "stuck".
//
//   const pilot = makePilot({ ask, wait, move, dig, strike, hold, note, shot });
//   await pilot.go('monster', { until: async () => level >= 3, seconds: 300 });   // 'arrived', 'done', 'time', 'stuck', 'noway'
//
// ask(command) gives the JSON of a playtest command; move(dx, dy) walks that way for a moment; dig(dx, dy) swings
// the pickaxe at the tile there until it breaks; strike(dx, dy, ms) swings the weapon that way; hold('pickaxe' |
// 'weapon') puts it in hand; note(line) and shot(name, caption) write the report.
'use strict';

function makePilot(hooks) {
  const { ask, wait, move, dig, strike, hold, note, shot } = hooks;
  const counts = { navs: 0, moves: 0, digs: 0, fights: 0, stalls: 0, stuck: 0 };
  let pictures = 0;
  // The game's last answer (its x, y face the thing arrived at).
  let last = {};

  // Writes where the bot got stuck: the target, what it did last, its small map, and a picture (the first few).
  const report = async (target, intent) => {
    counts.stuck++;
    const s = await ask('sense');
    note(`  - (막힘 ${counts.stuck}) ${target}: ${intent.why || ''}, 최근: ${s.recent || '-'}`);
    if (s.map) note('```\n' + s.map.replace(/\n$/, '') + '\n```');
    if (pictures < 3 && shot) {
      pictures++;
      await shot(`stuck-${pictures}`, `막힌 곳 ${pictures}: ${target}`);
    }
  };

  // Goes for the target until `until` says enough (or, with none, until there), the time runs out, or the game says
  // there is no way or the bot is stuck.
  const go = async (target, { until = null, seconds = 120 } = {}) => {
    const end = Date.now() + seconds * 1000;
    let noway = 0;
    while (Date.now() < end) {
      if (until && (await until())) return 'done';
      const intent = await ask(`nav ${target}`);
      last = intent;
      counts.navs++;
      if (intent.stalled) counts.stalls++;
      switch (intent.do) {
        case 'move':
          noway = 0;
          counts.moves++;
          await move(intent.x, intent.y);
          break;
        case 'dig': {
          noway = 0;
          counts.digs++;
          const [dx, dy] = (intent.tile || '0,0').split(',').map(Number);
          await hold('pickaxe');
          await dig(dx, dy);
          break;
        }
        case 'fight':
          noway = 0;
          counts.fights++;
          await hold('weapon');
          await strike(intent.x, intent.y, 400);
          break;
        case 'arrived':
          if (!until) return 'arrived';
          await wait(300);
          break;
        case 'wait':
          await wait(600);
          break;
        default:
          // No way: the game says the bot gave up (stalled), or there is nothing to go to just now.
          if (intent.stalled || /막힘/.test(intent.why || '')) {
            await report(target, intent);
            return 'stuck';
          }
          if (++noway >= 8) {
            await report(target, intent);
            return 'noway';
          }
          await wait(500);
      }
    }
    return until && (await until()) ? 'done' : 'time';
  };

  return { go, counts, get last() { return last; } };
}

module.exports = { makePilot };
