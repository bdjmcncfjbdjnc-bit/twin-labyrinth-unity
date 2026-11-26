const fs = require('node:fs');
const path = require('node:path');

const directory = path.join(__dirname, 'Assets', 'ArtReview', 'Source', 'HauntAudio');
fs.mkdirSync(directory, { recursive: true });
const rate = 24000;
const tau = 2 * Math.PI;
let seed = 98273;
function random() {
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  return seed / 2147483648 - 1;
}
function tone(frequency, time) {
  return Math.sin(tau * frequency * time);
}
function wave(name, seconds, sound) {
  const size = Math.round(rate * seconds);
  const data = Buffer.alloc(44 + size * 2);
  data.write('RIFF', 0);
  data.writeUInt32LE(data.length - 8, 4);
  data.write('WAVEfmt ', 8);
  data.writeUInt32LE(16, 16);
  data.writeUInt16LE(1, 20);
  data.writeUInt16LE(1, 22);
  data.writeUInt32LE(rate, 24);
  data.writeUInt32LE(rate * 2, 28);
  data.writeUInt16LE(2, 32);
  data.writeUInt16LE(16, 34);
  data.write('data', 36);
  data.writeUInt32LE(size * 2, 40);
  let noise = 0;
  for (let sample = 0; sample < size; sample++) {
    const t = sample / rate;
    const white = random();
    noise = noise * .83 + white * .17;
    const fade = Math.min(1, t / .009, (seconds - t) / .025);
    const value = Math.max(-1, Math.min(1, sound(t, noise, white) * fade));
    data.writeInt16LE(Math.round(value * 32767), 44 + sample * 2);
  }
  fs.writeFileSync(path.join(directory, name + '.wav'), data);
}

wave('distant-vent', 16, (t, n) => {
  const swell = .65 + .28 * tone(.125, t) + .07 * tone(.3125, t);
  return swell * (.105 * tone(47.5, t) + .055 * tone(72.125, t) + .045 * tone(101.25, t)) + .032 * n;
});
wave('breathing', 1.7, (t, n, w) => {
  const inhale = Math.exp(-Math.pow((t - .32) / .18, 2)) * .7;
  const exhale = Math.exp(-Math.pow((t - .95) / .32, 2));
  return (inhale + exhale) * (.95 * n + .10 * w + .027 * tone(77, t));
});
for (let step = 0; step < 4; step++) {
  wave('foot-' + step, .245, (t, n, w) => (
    (.38 * tone(73 + step * 7, t) + .43 * n + .11 * w) * Math.exp(-23 * t)
    + .24 * n * Math.exp(-Math.pow((t - .082) / .032, 2))
  ));
}
wave('torch-click', .12, (t, n, w) => (.36 * w + .3 * tone(870, t)) * Math.exp(-57 * t));
wave('collect', .35, (t, n) => (.45 * n + .15 * tone(294, t)) * Math.exp(-14 * t)
  + .1 * tone(441, t) * Math.exp(-Math.pow((t - .115) / .042, 2)));
wave('metal-latch', .62, (t, n, w) => (
  (.46 * n + .16 * w + .20 * tone(326, t) + .12 * tone(588, t)) * Math.exp(-11 * t)
  + .09 * tone(445 + 70 * t, t) * Math.exp(-Math.pow((t - .29) / .18, 2))
));
wave('figure', 1.25, (t, n) => (
  (.16 * tone(171 - 31 * t, t) + .11 * tone(246 - 32 * t, t) + .35 * n)
  * (1 - Math.exp(-14 * t)) * Math.exp(-2.4 * t)
));
wave('anomaly', 1.8, (t, n) => (
  (.21 * tone(102 + 40 * t, t) + .13 * tone(274 - 35 * t, t) + .36 * n)
  * (1 - Math.exp(-18 * t)) * Math.exp(-1.72 * t)
  + .145 * tone(63, t) * Math.exp(-Math.pow((t - .52) / .26, 2))
));

function heartbeatThump(t, start) {
  const age = t - start;
  if (age < 0) return 0;
  const envelope = (1 - Math.exp(-92 * age)) * Math.exp(-19 * age);
  return envelope * (.62 * tone(56, age) + .20 * tone(92, age));
}

wave('heartbeat', .79, (t) => heartbeatThump(t, .012) + .52 * heartbeatThump(t, .17));

wave('guard-radio', .65, (t, n, w) => {
  const start = Math.exp(-Math.pow((t - .09) / .028, 2));
  const click = Math.exp(-Math.pow((t - .51) / .03, 2));
  return (.17 * n + .07 * w + .06 * tone(920, t)) * (start + click)
    + .085 * n * Math.exp(-Math.pow((t - .33) / .10, 2));
});

wave('partner-reply', .44, (t, n) => {
  const taps = Math.exp(-Math.pow((t - .08) / .027, 2)) + .7 * Math.exp(-Math.pow((t - .24) / .029, 2));
  return taps * (.17 * tone(287, t) + .12 * n);
});
