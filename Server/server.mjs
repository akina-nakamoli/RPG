import http from 'node:http';
import { randomBytes, randomUUID, scrypt, timingSafeEqual, createHash } from 'node:crypto';
import { promisify } from 'node:util';
import { readFile } from 'node:fs/promises';
import { MongoClient } from 'mongodb';

const derive = promisify(scrypt);
if (process.env.NODE_ENV === 'production' && !process.env.RPG_MONGO_URI) throw new Error('Production requires RPG_MONGO_URI in the server environment.');
const port = Number(process.env.RPG_API_PORT || 5080);
const databaseName = process.env.RPG_MONGO_DATABASE || 'rpg_game';
if (!/^[a-zA-Z0-9_]{1,64}$/.test(databaseName)) throw new Error('Invalid RPG_MONGO_DATABASE.');
// Host-only configuration. Never bundle the invitation or its hash in Unity assets.
let registrationInviteHash = process.env.RPG_INVITE_SHA256;
if (registrationInviteHash === undefined) {
  try {
    const config = JSON.parse(await readFile(new URL('./.local/playtest.json', import.meta.url), 'utf8'));
    registrationInviteHash = config.registrationInviteHash;
  } catch (error) { if (error.code !== 'ENOENT') throw new Error('Invalid host playtest configuration.'); }
}
if (registrationInviteHash === '') registrationInviteHash = null; // Explicitly close registration; existing logins still work.
if (registrationInviteHash != null && !/^[a-f0-9]{64}$/.test(registrationInviteHash))
  throw new Error('Registration invitation hash must be a SHA-256 hex digest.');
const registrationReady = typeof registrationInviteHash === 'string';
let activeAuth = 0;
const client = new MongoClient(process.env.RPG_MONGO_URI || 'mongodb://127.0.0.1:27017', { serverSelectionTimeoutMS: 5000 });
await client.connect();
const db = client.db(databaseName);
const accounts = db.collection('accounts');
const saves = db.collection('saves');
const sessions = db.collection('sessions');
await sessions.createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 });
await accounts.createIndex({ usernameKey: 1 }, { unique: true });
await saves.createIndex({ accountId: 1, slot: 1 }, { unique: true });
if (process.argv.includes('--init-db')) {
  console.log(`Created/verified ${databaseName}: accounts, saves and unique indexes.`);
  await client.close();
  process.exit(0);
}
const attempts = new Map();
function reject(message, code = 400) { throw Object.assign(new Error(message), { code }); }
const tokenHash = token => createHash('sha256').update(token).digest('hex');
async function owner(token) {
  if (typeof token !== 'string' || !/^[a-f0-9]{64}$/.test(token)) reject('Please log in again.', 401);
  const session = await sessions.findOne({ _id: tokenHash(token), expiresAt: { $gt: new Date() } });
  if (!session) reject('Session expired. Please log in again.', 401);
  return session.accountId;
}
function slot(value) { if (!Number.isInteger(value) || value < 1 || value > 3) reject('Invalid save slot.'); return value; }
function validateSave(d, accountId) {
  if (d) d = { ...d, defeatedForestGoblins: d.defeatedForestGoblins ?? [], forestChestOpened: d.forestChestOpened ?? false, escortJameStage: d.escortJameStage ?? 0 };
  if (!d || d.accountId !== accountId || d.version !== 1 || !['SampleScene', 'WildernessForest'].includes(d.scene)) reject('Invalid save identity or scene.');
  slot(d.slot);
  d.escortJameHealth = d.escortJameHealth ?? 10;
  d.targetRangeBestScore = d.targetRangeBestScore ?? 0;
  d.targetRangeHeroUnlocked = d.targetRangeHeroUnlocked ?? false;
  d.archerSelected = d.archerSelected ?? false;
  if (typeof d.archerSelected !== 'boolean' || (d.archerSelected && !d.targetRangeHeroUnlocked)) reject('Invalid selected hero.');
  if (!Number.isInteger(d.targetRangeBestScore) || d.targetRangeBestScore < 0 || d.targetRangeBestScore > 1000000 || typeof d.targetRangeHeroUnlocked !== 'boolean') reject('Invalid target range progress.');
  if (!Number.isInteger(d.escortJameHealth) || d.escortJameHealth < 0 || d.escortJameHealth > 10) reject('Invalid escort health.');
  for (const key of ['x', 'y', 'z']) if (!Number.isFinite(d[key]) || Math.abs(d[key]) > 100000) reject('Invalid player position.');
  for (const key of ['health', 'level', 'experience', 'skillPoints', 'sortingOrder'])
    if (!Number.isSafeInteger(d[key])) reject('Invalid player stats.');
  if (d.health < 1 || d.health > 1000000 || d.level < 0 || d.level > 100000 || d.experience < 0 || d.skillPoints < 0) reject('Invalid progress.');
  if (d.experience > 2147483647 || d.skillPoints > 2147483647 || d.sortingOrder < -32768 || d.sortingOrder > 32767) reject('Stats exceed supported bounds.');
  if (!Array.isArray(d.skills) || d.skills.length !== 15) reject('Invalid skill tree.');
  d.skills.forEach((rank, i) => {
    if (!Number.isInteger(rank) || rank < 0 || rank > 5 || (i % 3 > 0 && rank > 0 && d.skills[i - 1] < 5)) reject('Invalid skill rank.');
  });
  for (const key of ['items', 'completedGreetings', 'terrainColliders', 'acceptedTownQuests', 'townQuestStates', 'defeatedForestGoblins'])
    if (!Array.isArray(d[key]) || d[key].length > 1000) reject('Invalid save collections.');
  const keys = new Set();
  for (const item of d.items) {
    if (!item || typeof item.key !== 'string' || !item.key.trim() || item.key.length > 128
      || !Number.isSafeInteger(item.amount) || item.amount < 0 || item.amount > 2147483647 || keys.has(item.key.toLowerCase())) reject('Invalid inventory.');
    keys.add(item.key.toLowerCase());
  }
  function strings(values, maxLength) {
    const seen = new Set();
    for (const value of values) {
      if (typeof value !== 'string' || !value.trim() || value.length > maxLength || seen.has(value)) reject('Invalid or duplicate save identifier.');
      seen.add(value);
    }
  }
  strings(d.completedGreetings, 2048);
  strings(d.defeatedForestGoblins, 128);
  if (typeof d.forestChestOpened !== 'boolean') reject('Invalid forest chest state.');
  if (!Number.isInteger(d.escortJameStage) || d.escortJameStage < 0 || d.escortJameStage > 4) reject('Invalid escort state.');
  strings(d.acceptedTownQuests, 128);
  for (const state of d.terrainColliders)
    if (!state || typeof state.key !== 'string' || !state.key || state.key.length > 2048 || typeof state.enabled !== 'boolean') reject('Invalid terrain state.');
  for (const state of d.townQuestStates)
    if (!state || typeof state.id !== 'string' || !state.id || state.id.length > 128 || !Number.isInteger(state.progress)
      || state.progress < 0 || state.progress > 1000000 || typeof state.completed !== 'boolean') reject('Invalid quest state.');
  strings(d.townQuestStates.map(state => state.id), 128);
  strings(d.terrainColliders.map(state => state.key), 2048);
  d = { ...d, items: d.items.map(item => ({ key: item.key, amount: item.amount })),
    terrainColliders: d.terrainColliders.map(state => ({ key: state.key, enabled: state.enabled })),
    townQuestStates: d.townQuestStates.map(state => ({ id: state.id, progress: state.progress, completed: state.completed })) };
  // Whitelist document fields rather than accepting arbitrary MongoDB keys.
  const fields = ['version', 'accountId', 'slot', 'scene', 'x', 'y', 'z', 'health', 'level', 'experience', 'skillPoints', 'sortingOrder',
    'skills', 'items', 'completedGreetings', 'terrainColliders', 'acceptedTownQuests', 'townQuestStates', 'defeatedForestGoblins', 'forestChestOpened', 'escortJameStage', 'escortJameHealth', 'targetRangeBestScore', 'targetRangeHeroUnlocked', 'archerSelected'];
  return Object.fromEntries(fields.map(key => [key, d[key]]));
}
async function dispatch(input) {
  if (['register', 'login'].includes(input.operation)) {
    const username = typeof input.username === 'string' ? input.username.trim() : '';
    const password = input.password;
    if (!/^[A-Za-z0-9_]{3,20}$/.test(username) || typeof password !== 'string' || password.length < 8 || password.length > 128)
      reject('Use a 3-20 character username and an 8-128 character password.');
    const key = username.toLowerCase();
    const bucket = attempts.get(key) || { count: 0, until: Date.now() + 60000 };
    if (bucket.until < Date.now()) { bucket.count = 0; bucket.until = Date.now() + 60000; }
    attempts.set(key, bucket);
    if (++bucket.count > 10) reject('Too many attempts. Wait one minute.', 429);
    let account;
    if (input.operation === 'register') {
      if (!registrationReady) reject('Registration is closed. Contact the game host.', 403);
      if (typeof input.invitationCode !== 'string' || input.invitationCode.length > 64
        || !timingSafeEqual(Buffer.from(tokenHash(input.invitationCode.trim()), 'hex'), Buffer.from(registrationInviteHash, 'hex')))
        reject('Invalid playtest invitation code. Ask the game host for the code.', 403);
      const salt = randomBytes(16).toString('hex');
      account = { id: randomUUID().replaceAll('-', ''), username, usernameKey: key, salt,
        passwordHash: (await derive(password, salt, 32)).toString('hex'), createdUtc: new Date().toISOString() };
      try { await accounts.insertOne(account); }
      catch (error) { if (error.code === 11000) reject('Username already exists.', 409); throw error; }
    } else {
      account = await accounts.findOne({ usernameKey: key });
      const computed = await derive(password, account?.salt || 'missing-account-salt', 32);
      const expected = account ? Buffer.from(account.passwordHash, 'hex') : Buffer.alloc(32);
      if (!account || computed.length !== expected.length || !timingSafeEqual(computed, expected)) reject('Incorrect username or password.', 401);
    }
    const token = randomBytes(32).toString('hex');
    await sessions.insertOne({ _id: tokenHash(token), accountId: account.id, expiresAt: new Date(Date.now() + 86400000) });
    return { token, account: { id: account.id, username: account.username } };
  }
  const accountId = await owner(input.token);
  if (input.accountId !== accountId) reject('Account access denied.', 403);
  if (input.operation === 'logout') { await sessions.deleteOne({ _id: tokenHash(input.token) }); return {}; }
  const filter = { accountId, slot: slot(input.slot) };
  if (input.operation === 'delete') {
    if (!Number.isSafeInteger(input.revision) || input.revision < 0) reject('Invalid delete revision.');
    const record = await saves.findOne(filter);
    if (!record) return {};
    if ((record.revision || 0) !== input.revision) reject('Cloud save changed. Sign in again before deleting.', 409);
    const { _id, ...backup } = record;
    await db.collection('deleted_saves').insertOne({ ...backup, deletedUtc: new Date() });
    const result = await saves.deleteOne({ ...filter, _id, ...(record.revision === undefined ? { revision: { $exists: false } } : { revision: record.revision }) });
    if (!result.deletedCount) reject('Cloud save changed during deletion. Original preserved.', 409);
    return {};
  }
  if (input.operation === 'has') return { exists: !!await saves.findOne(filter, { projection: { _id: 1 } }) };
  if (input.operation === 'load') {
    const record = await saves.findOne(filter);
    if (!record) reject('This slot is empty.', 404);
    return { save: record.data, revision: record.revision || 0, lastRequestId: record.lastRequestId || null };
  }
  if (input.operation === 'save') {
    const data = validateSave(input.save, accountId);
    if (data.slot !== filter.slot) reject('Save slot mismatch.');
    if (!Number.isSafeInteger(input.revision) || input.revision < 0 || typeof input.requestId !== 'string'
      || !/^[a-f0-9]{32}$/.test(input.requestId)) reject('Client must supply a revision and request ID.');
    const digest = createHash('sha256').update(JSON.stringify(data)).digest('hex');
    const existing = await saves.findOne(filter);
    if (existing?.lastRequestId === input.requestId) {
      if (existing.lastRequestHash !== digest) reject('Request ID cannot be reused for different data.', 409);
      return { revision: existing.revision };
    }
    if ((existing?.revision || 0) !== input.revision) reject('Cloud save changed on another device. Both copies preserved.', 409);
    data.savedUtc = new Date().toISOString();
    const versionFilter = { ...filter, ...(input.revision === 0
      ? { $or: [{ revision: 0 }, { revision: { $exists: false } }] } : { revision: input.revision }) };
    let updated;
    try { updated = await saves.updateOne(versionFilter, [{ $set: {
      accountId: { $literal: accountId }, slot: filter.slot,
      previousData: '$data', data: { $literal: data }, updatedUtc: '$$NOW', revision: input.revision + 1,
      lastRequestId: { $literal: input.requestId }, lastRequestHash: { $literal: digest }
    } }], { upsert: input.revision === 0 }); }
    catch (error) { if (error.code !== 11000) throw error; }
    if (!updated || (!updated.matchedCount && !updated.upsertedCount)) {
      const winner = await saves.findOne(filter);
      if (winner?.lastRequestId === input.requestId && winner.lastRequestHash === digest) return { revision: winner.revision };
      reject('Cloud save conflict. Both copies preserved.', 409);
    }
    return { revision: input.revision + 1 };
  }
  reject('Unknown operation.');
}
const server = http.createServer(async (req, res) => {
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  if (req.method === 'GET' && req.url === '/health') {
    try { await db.command({ ping: 1 }); res.end(JSON.stringify({ ok: true, database: databaseName,
      protocol: 2, registrationInviteRequired: true, registrationReady })); }
    catch { res.statusCode = 503; res.end(JSON.stringify({ ok: false })); }
    return;
  }
  if (req.method !== 'POST' || req.url !== '/api') { res.statusCode = 404; res.end('{}'); return; }
  try {
    const address = process.env.RPG_TRUST_PROXY === '1' && typeof req.headers['x-forwarded-for'] === 'string'
      ? req.headers['x-forwarded-for'].split(',').at(-1).trim() : req.socket.remoteAddress;
    const bucket = attempts.get('ip:' + address) || { count: 0, until: Date.now() + 60000 };
    if (bucket.until < Date.now()) { bucket.count = 0; bucket.until = Date.now() + 60000; }
    attempts.set('ip:' + address, bucket);
    if (++bucket.count > 240) reject('Too many requests. Wait a minute.', 429);
    let size = 0;
    const chunks = [];
    for await (const chunk of req) {
      size += chunk.length;
      if (size > 1048576) reject('Request too large.', 413);
      chunks.push(chunk);
    }
    let input;
    try { input = JSON.parse(Buffer.concat(chunks).toString('utf8')); }
    catch { reject('Invalid JSON.'); }
    if (!input || typeof input !== 'object' || Array.isArray(input)) reject('Invalid request.');
    const authentication = ['register', 'login'].includes(input.operation);
    if (authentication) {
      const authBucket = attempts.get('auth-ip:' + address) || { count: 0, until: Date.now() + 60000 };
      if (authBucket.until < Date.now()) { authBucket.count = 0; authBucket.until = Date.now() + 60000; }
      attempts.set('auth-ip:' + address, authBucket);
      if (++authBucket.count > 40) reject('Too many login attempts. Wait one minute.', 429);
    }
    if (authentication && activeAuth >= 4) reject('Authentication busy. Try again shortly.', 429);
    if (authentication) activeAuth++;
    try { res.end(JSON.stringify({ ok: true, ...await dispatch(input) })); }
    finally { if (authentication) activeAuth--; }
  } catch (error) {
    res.statusCode = error.code >= 400 && error.code <= 599 ? error.code : 503;
    res.end(JSON.stringify({ ok: false, error: res.statusCode === 503 ? 'Database unavailable. Previous save preserved.' : error.message }));
  }
});
server.requestTimeout = 10000;
server.listen(port, '127.0.0.1', () => console.log(`RPG API ready: http://127.0.0.1:${port} / ${databaseName} (invitation registration ${registrationReady ? 'ready' : 'closed'})`));
const cleanup = setInterval(() => {
  for (const [key, value] of attempts) if (value.until < Date.now()) attempts.delete(key);
}, 60000);
cleanup.unref();
async function stop() { clearInterval(cleanup); server.close(); await client.close(); }
process.on('SIGINT', () => stop().then(() => process.exit(0)));
process.on('SIGTERM', () => stop().then(() => process.exit(0)));
