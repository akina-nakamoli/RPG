import assert from 'node:assert/strict';
import { randomBytes, createHash } from 'node:crypto';
import { MongoClient } from 'mongodb';
const base = process.env.RPG_TEST_API;
if (!base) throw new Error('Set RPG_TEST_API explicitly before running integration tests.');
const dbName = process.env.RPG_MONGO_DATABASE || 'rpg_game';
const invitationCode = process.env.RPG_TEST_INVITATION;
if (!invitationCode) throw new Error('Set RPG_TEST_INVITATION in the test environment, not a command-line argument.');
const client = new MongoClient(process.env.RPG_MONGO_URI || 'mongodb://127.0.0.1:27017');
await client.connect();
let accountId;
const username = 'test_' + randomBytes(5).toString('hex');
const password = randomBytes(16).toString('hex');
async function api(input) {
  const response = await fetch(base + '/api', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) });
  return { status: response.status, ...await response.json() };
}
try {
  assert.equal((await (await fetch(base + '/health')).json()).database, dbName);
  const auth = await api({ operation: 'register', username, password, invitationCode });
  assert.equal(auth.ok, true);
  accountId = auth.account.id;
  assert.equal(auth.account.passwordHash, undefined);
  assert.equal((await api({ operation: 'login', username, password: 'incorrect-password' })).status, 401);
  const login = await api({ operation: 'login', username, password });
  assert.equal(login.account.id, accountId);
  const common = { token: login.token, accountId, slot: 1 };
  assert.equal((await api({ ...common, operation: 'has' })).exists, false);
  const save = { version: 1, accountId, slot: 1, scene: 'WildernessForest', x: -11, y: 0, z: 0,
    health: 10, level: 0, experience: 0, skillPoints: 0, sortingOrder: 10000, skills: Array(15).fill(0),
    items: [{ key: 'gold', amount: 2 }], completedGreetings: [], terrainColliders: [], acceptedTownQuests: [], townQuestStates: [] };
  const requestId = randomBytes(16).toString('hex');
  const first = await api({ ...common, operation: 'save', save, revision: 0, requestId });
  assert.equal(first.revision, 1);
  assert.equal((await api({ ...common, operation: 'save', save, revision: 0, requestId })).revision, 1);
  assert.equal((await api({ ...common, operation: 'has' })).exists, true);
  assert.equal((await api({ ...common, operation: 'load' })).save.scene, 'WildernessForest');
  assert.equal((await api({ ...common, operation: 'load' })).lastRequestId, requestId);
  save.health = 9;
  assert.equal((await api({ ...common, operation: 'save', save, revision: 1, requestId: randomBytes(16).toString('hex') })).revision, 2);
  assert.equal((await api({ ...common, operation: 'save', save, revision: 1, requestId: randomBytes(16).toString('hex') })).status, 409);
  const record = await client.db(dbName).collection('saves').findOne({ accountId, slot: 1 });
  assert.equal(record.previousData.health, 10);
  assert.equal(record.data.health, 9);
  const parallelId = randomBytes(16).toString('hex');
  const duplicates = await Promise.all([0, 1].map(() => api({ ...common, operation: 'save', save, revision: 2, requestId: parallelId })));
  assert.ok(duplicates.every(response => response.revision === 3));
  const competing = await Promise.all([0, 1].map(() => api({ ...common, operation: 'save', save, revision: 3, requestId: randomBytes(16).toString('hex') })));
  assert.equal(competing.filter(response => response.revision === 4).length, 1);
  assert.equal(competing.filter(response => response.status === 409).length, 1);
  assert.equal((await api({ ...common, accountId: '0'.repeat(32), operation: 'load' })).status, 403);
  assert.equal((await api({ ...common, operation: 'save', save: { ...save, health: 0 } })).ok, false);
  const logout = await api({ token: login.token, accountId, operation: 'logout' });
  assert.equal(logout.ok, true);
  assert.equal((await api({ ...common, operation: 'load' })).status, 401);
  const expired = await api({ operation: 'login', username, password });
  await client.db(dbName).collection('sessions').updateOne(
    { _id: createHash('sha256').update(expired.token).digest('hex'), accountId },
    { $set: { expiresAt: new Date(Date.now() - 1000) } });
  assert.equal((await api({ ...common, token: expired.token, operation: 'load' })).status, 401);
  console.log('PASS: auth, logout, expired sessions, authorization, upload receipts, revisions, retry idempotency, conflict rejection, backup and save/load.');
} finally {
  // Remove only this test's newly created account and saves; never user records.
  if (accountId) {
    await client.db(dbName).collection('saves').deleteMany({ accountId });
    await client.db(dbName).collection('accounts').deleteOne({ id: accountId, username });
    await client.db(dbName).collection('sessions').deleteMany({ accountId });
    console.log('Temporary smoke-test records removed.');
  }
  await client.close();
}
