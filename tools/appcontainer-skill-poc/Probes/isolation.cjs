'use strict';
const fs = require('node:fs');
const net = require('node:net');
const cp = require('node:child_process');
let text = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', part => { text += part; });
process.stdin.on('end', async () => {
  const input = JSON.parse(text); const result = {};
  try { result.outside_read = {allowed: true, content: fs.readFileSync(input.outside_read, 'utf8')}; }
  catch (error) { result.outside_read = {allowed: false, code: error.code}; }
  try { fs.writeFileSync(input.outside_write, 'unexpected'); result.outside_write = {allowed: true}; }
  catch (error) { result.outside_write = {allowed: false, code: error.code}; }
  result.network = await new Promise(resolve => {
    const client = net.connect({host: '1.1.1.1', port: 443});
    client.setTimeout(3000);
    client.once('connect', () => { client.destroy(); resolve({allowed: true}); });
    client.once('error', error => { client.destroy(); resolve({allowed: false, code: error.code}); });
    client.once('timeout', () => { client.destroy(); resolve({allowed: false, code: 'TIMEOUT'}); });
  });
  result.child = await new Promise(resolve => {
    let child;
    try { child = cp.spawn(process.execPath, ['--version'], {stdio: 'ignore'}); }
    catch (error) { resolve({allowed: false, code: error.code, errno: error.errno}); return; }
    let done = false;
    const finish = outcome => { if (done) return; done = true; clearTimeout(timer); resolve(outcome); };
    const timer = setTimeout(() => { child.kill(); finish({allowed: true, code: 'TIMEOUT'}); }, 3000);
    child.once('error', error => finish({allowed: false, code: error.code, errno: error.errno}));
    child.once('exit', code => finish({allowed: true, exit: code}));
  });
  result.inherited_secret = process.env.TABLECLOTH_POC_SYNTHETIC_SECRET ?? null;
  fs.writeFileSync('allowed-write.txt', 'allowed'); result.work_write = true;
  console.log(JSON.stringify(result));
});
