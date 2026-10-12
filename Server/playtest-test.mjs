// Starts the real backend with a fresh test DB; never touches rpg_game.
import assert from 'node:assert/strict';
import { randomBytes, createHash } from 'node:crypto';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';
import net from 'node:net';
import { MongoClient } from 'mongodb';

const mongo = new MongoClient(process.env.RPG_MONGO_URI || 'mongodb://127.0.0.1:27017');
await mongo.connect();
const dbName = 'rpg_playtest_test_' + randomBytes(8).toString('hex');
assert.match(dbName, /^rpg_playtest_test_[a-f0-9]{16}$/);
assert.equal((await mongo.db('admin').admin().listDatabases()).databases.some(db => db.name === dbName), false);
const probe = net.createServer();
probe.listen(0, '127.0.0.1');
await once(probe, 'listening');
const port = probe.address().port;
await new Promise(resolve => probe.close(resolve));
const base = 'http://127.0.0.1:' + port;
const invite = randomBytes(12).toString('hex');
const env = { ...process.env, RPG_API_PORT: String(port), RPG_MONGO_DATABASE: dbName,
  RPG_INVITE_SHA256: createHash('sha256').update(invite).digest('hex'), RPG_TRUST_PROXY: '0' };
let child;
let output = '';
async function start(hash) {
  output = '';
  child = spawn(process.execPath, [fileURLToPath(new URL('./server.mjs', import.meta.url))],
    { env: { ...env, RPG_INVITE_SHA256: hash }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.on('data', data => { output += data; });
  child.stderr.on('data', data => { output += data; });
  for (let i = 0; i < 100; i++) {
    if (child.exitCode != null) throw new Error('Test backend stopped: ' + output);
    try { const health = await (await fetch(base + '/health', { signal: AbortSignal.timeout(1000) })).json();
      if (health.database === dbName) return health; } catch { }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Test backend did not become ready.');
}
async function stop() {
  if (child && child.exitCode == null) { const done = once(child, 'exit'); child.kill(); await done; }
  child = null;
}
async function api(input) {
  const response = await fetch(base + '/api', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input), signal: AbortSignal.timeout(5000) });
  return { status: response.status, ...await response.json() };
}
try {
  const health = await start(env.RPG_INVITE_SHA256);
  assert.equal(health.registrationInviteRequired, true);
  assert.equal(health.registrationReady, true);
  assert.equal(health.registrationInviteHash, undefined);
  const credentials = { operation: 'register', username: 'invite_test', password: randomBytes(12).toString('hex') };
  for (const invitationCode of [undefined, '', 'wrong-code', 123, {}, 'x'.repeat(65)])
    assert.equal((await api({ ...credentials, invitationCode })).status, 403);
  assert.equal(await mongo.db(dbName).collection('accounts').countDocuments(), 0);
  const registered = await api({ ...credentials, invitationCode: ' ' + invite + ' ' });
  assert.equal(registered.ok, true);
  assert.equal(registered.account.passwordHash, undefined);
  assert.equal((await api({ ...credentials, invitationCode: invite })).status, 409);
  const login = await api({ ...credentials, operation: 'login' });
  assert.equal(login.ok, true); // Old accounts don't need invitation codes.
  assert.equal((await api({ operation: 'has', accountId: login.account.id, slot: 1 })).status, 401);
  const save = { version: 1, accountId: login.account.id, slot: 1, scene: 'SampleScene', x: 0, y: 0, z: 0,
    health: 10, level: 0, experience: 0, skillPoints: 0, sortingOrder: 0, skills: Array(15).fill(0),
    items: [], completedGreetings: [], terrainColliders: [], acceptedTownQuests: [], townQuestStates: [] };
  const common = { token: login.token, accountId: login.account.id, slot: 1 };
  assert.equal((await api({ ...common, operation: 'save', save, revision: 0, requestId: randomBytes(16).toString('hex') })).revision, 1);
  assert.equal((await api({ ...common, accountId: '0'.repeat(32), operation: 'delete', revision: 1 })).status, 403);
  assert.equal((await api({ ...common, operation: 'delete', revision: 0 })).status, 409);
  assert.equal((await api({ ...common, operation: 'has' })).exists, true);
  assert.equal((await api({ ...common, operation: 'delete', revision: 1 })).ok, true);
  assert.equal((await api({ ...common, operation: 'has' })).exists, false);
  assert.equal(await mongo.db(dbName).collection('deleted_saves').countDocuments({ accountId: login.account.id }), 1);
  assert.equal((await api([])).status, 400);
  // Existing upload/recovery regression suite, against the isolated server and DB.
  const smoke = spawn(process.execPath, [fileURLToPath(new URL('./smoke-test.mjs', import.meta.url))],
    { env: { ...env, RPG_TEST_API: base, RPG_TEST_INVITATION: invite }, windowsHide: true, stdio: 'inherit' });
  const [code] = await once(smoke, 'exit');
  assert.equal(code, 0);
  for (let i = 0; i < 41; i++) {
    const result = await api({ operation: 'login', username: 'missing_' + i, password: 'incorrect-password' });
    if (result.status === 429) break;
    assert.equal(result.status, 401);
    assert.ok(i < 40, 'Authentication rate limit must apply across usernames.');
  }
  await stop();
  assert.equal((await start('')).registrationReady, false);
  assert.equal((await api({ ...credentials, username: 'closed_test', invitationCode: invite })).status, 403);
  assert.equal((await api({ ...credentials, operation: 'login' })).ok, true);
  console.log('PASS: invitation required, invalid codes blocked, existing login, authenticated deletion/backups, input validation, auth throttling, closed registration.');
} finally {
  await stop();
  // Exact newly generated database, verified absent before creation; never a supplied/production name.
  assert.match(dbName, /^rpg_playtest_test_[a-f0-9]{16}$/);
  await mongo.db(dbName).dropDatabase();
  await mongo.close();
  console.log('Removed only the isolated test database and its temporary fixtures.');
}
