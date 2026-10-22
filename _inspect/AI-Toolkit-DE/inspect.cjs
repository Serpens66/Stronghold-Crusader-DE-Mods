'use strict';
// Read-only feasibility probe: execute upstream normalization on local DE files.
// This checks serialized documents, not game execution or visual placement.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const {createRequire} = require('node:module');
const repo = path.resolve(__dirname, '../../../Fremde Mods/AI-Toolkit');
const fromRepo = createRequire(path.join(repo, 'package.json'));
const format = fromRepo('./src/js/castle-format');
const constants = fromRepo('./config/aiv_constants.json');
const template = fromRepo('./config/template.json');
const legacyKeyMap = fromRepo('./config/legacyKeyMap.json');
const source = fs.readFileSync(path.join(repo, 'src/js/castle-editor.js'), 'utf8');
function section(text, start, end) {
  const a = text.indexOf(start), b = text.indexOf(end, a);
  assert.ok(a >= 0 && b > a, `Upstream function boundaries missing: ${start}`);
  return text.slice(a, b);
}
const state = {format: 'aivjson'};
const castle = vm.createContext({state, window: {castleFormat: format},
  deepClone: x => JSON.parse(JSON.stringify(x)), isUnitType: id => constants[id]?.kind === 'unit'});
for (const [a, b] of [
  ['  function normalizeDocument(', '  function itemInfo('],
  ['  function renumberUnits(', '  function refExists('],
  ['  function outputDocument(', '  function outputContent(']
]) vm.runInContext(section(source, a, b), castle);
const root = path.join(process.env.USERPROFILE, 'AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition');
function files(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs.readdirSync(directory, {withFileTypes: true}).flatMap(e => {
    const file = path.join(directory, e.name);
    return e.isDirectory() ? files(file) : [file];
  });
}
const inputs = ['CustomLords', 'ExtendedLords'].flatMap(d => files(path.join(root, d)));
const failures = [], differences = [], unknownFrames = new Set(), unknownMarkers = new Set();
let aivs = 0, exactJson = 0, exactEditor = 0, emptySteps = 0, pauses = 0;
for (const file of inputs.filter(f => f.toLowerCase().endsWith('.aivjson'))) {
  aivs++;
  try {
    const doc = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
    for (const frame of doc.frames) {
      if (Object.keys(frame).length === 0) emptySteps++;
      if (frame.shouldPause) pauses++;
      if (frame.itemType != null && !constants[frame.itemType]) unknownFrames.add(frame.itemType);
    }
    for (const m of doc.miscItems || []) if (!constants[m.itemType]) unknownMarkers.add(m.itemType);
    assert.deepEqual(JSON.parse(format.stringify(doc)), doc);
    exactJson++;
    state.document = castle.normalizeUnitStorage(castle.normalizeDocument(structuredClone(doc)));
    const output = JSON.parse(JSON.stringify(castle.outputDocument()));
    try { assert.deepEqual(output, doc); exactEditor++; }
    catch { differences.push(path.relative(root, file)); }
  } catch (error) { failures.push({file: path.relative(root, file), error: error.message.slice(0, 400)}); }
}
const characterSource = fs.readFileSync(path.join(repo, 'src/js/character-editor.js'), 'utf8');
const character = vm.createContext({legacyKeyMap});
vm.runInContext(section(characterSource, 'function renameKeysPreserveOrder(', 'function findUnknownKeys('), character);
vm.runInContext(section(characterSource, 'function mergeDefaults(', 'function characterFieldLabel('), character);
const lordFiles = inputs.filter(f => f.toLowerCase().endsWith('.lordjson'));
const example = lordFiles.find(f => /ExtendedLords[\\/]Wolf[\\/]Wolf\.lordjson$/i.test(f)) || lordFiles[0];
const original = JSON.parse(fs.readFileSync(example, 'utf8').replace(/^\uFEFF/, ''));
const normalized = character.applyTemplateOrder(template, character.renameKeysPreserveOrder(structuredClone(original)));
character.mergeDefaults(normalized, template);
const result = {
  observedUtc: new Date().toISOString(), repo,
  scope: 'Upstream functions, local file schema and serialization only; no GUI or game launch',
  aiv: {files: aivs, exactJson, exactEditor, emptySteps, pausedSteps: pauses,
    unknownFrameTypes: [...unknownFrames], unknownMarkerTypes: [...unknownMarkers], failures, differences},
  character: {
    localLordFiles: lordFiles.length, example: path.relative(root, example),
    inputTopLevel: Object.keys(original), outputTopLevel: Object.keys(normalized),
    originalFarmArray: Array.isArray(original.lord.farm_types),
    normalizedFarmArray: Array.isArray(normalized.lord.farm_types),
    originalBuildRate: original.lord.build_rate,
    injectedUcpBuildInterval: normalized.aic.BuildInterval,
    originalTaxMin: original.lord.min_tax, injectedUcpTaxMin: normalized.aic.TaxesMin,
    directLordKeyOverlap: Object.keys(original.lord).filter(k => Object.hasOwn(template.lord, k)),
    directAicKeyOverlap: Object.keys(original.lord).filter(k => Object.hasOwn(template.aic, k))
  }
};
fs.writeFileSync(path.join(__dirname, 'result.json'), JSON.stringify(result, null, 2).replace(/\n/g, '\r\n') + '\r\n');
console.log(JSON.stringify(result, null, 2));
