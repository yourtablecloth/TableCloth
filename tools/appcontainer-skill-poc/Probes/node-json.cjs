'use strict';
let text = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', part => { text += part; if (text.length > 8192) process.exit(2); });
process.stdin.on('end', () => {
  const value = JSON.parse(text);
  if (!Number.isFinite(value.a) || !Number.isFinite(value.b)) process.exit(2);
  process.stdout.write(JSON.stringify({ok: true, sum: value.a + value.b, runtime: process.version}));
});
