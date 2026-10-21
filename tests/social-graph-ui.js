const assert = require('assert');
const fs = require('fs');
const vm = require('vm');

class Element {
  constructor(tag) {
    this.tag = tag;
    this.children = [];
    this.attrs = {};
    this.style = {};
    this.dataset = {};
    this.classList = { toggle() {} };
    this.clientWidth = 800;
    this.value = '';
  }
  get firstChild() { return this.children[0]; }
  append(...children) { this.children.push(...children); }
  removeChild(child) { this.children.splice(this.children.indexOf(child), 1); }
  setAttribute(key, value) { this.attrs[key] = String(value); }
  addEventListener(key, listener) { this[key] = listener; }
  querySelector(selector) { return this.children.find(x => selector === '.item.active' && x.className?.includes('active')); }
  scrollIntoView() {}
}
const elements = new Map();
const document = {
  getElementById(id) { if (!elements.has(id)) elements.set(id, new Element(id)); return elements.get(id); },
  createElement: tag => new Element(tag),
  createElementNS: (ns, tag) => new Element(tag),
  createTextNode: text => ({ textContent: text }),
};
const nodes = [
  { id: 'flow:audience', type: 'flow', label: 'Audience' },
  { id: 'flow:rumor', type: 'flow', label: 'Rumor flow' },
  { id: 'flow:after-event', type: 'flow', label: 'After event' },
  { id: 'event:sample', type: 'event', label: 'Sample rumor' },
  { id: 'event:handled', type: 'event', label: 'Handled event' },
  { id: 'handler:handled:after-event:0', type: 'handler', label: 'После события: handled', detail: 'Код: SampleModule (правило модуля)' },
  { id: 'trait:hermit', type: 'trait', label: 'Hermit' },
  { id: 'axis:Group', type: 'axis', label: 'Group' },
];
const edges = [
  { from: 'event:sample', to: 'flow:rumor', label: 'факт для слуха' },
  { from: 'event:handled', to: 'handler:handled:after-event:0', label: 'После события' },
  { from: 'handler:handled:after-event:0', to: 'flow:after-event', label: 'исполняется на этапе' },
  { from: 'event:sample', to: 'trait:hermit', label: 'возможный исход' },
  { from: 'trait:hermit', to: 'axis:Group', label: 'влияние -40' },
];
const residents = [{ id: 'center', name: 'Center', faction: 'Civilian', entries: Array.from({ length: 14 }, (_, i) => ({ turn: i, sequence: i, kind: 'sample', text: 'Met someone', subject: 'center', other: 'peer' + i, eventId: 0 })) }, ...Array.from({ length: 14 }, (_, i) => ({ id: 'peer' + i, name: 'Person ' + i, faction: 'Civilian', entries: [] }))];
const window = { SOCIAL_GRAPH_DATA: { schema: 1, nodes, edges, residents }, addEventListener(name, fn) { this[name] = fn; } };
const html = fs.readFileSync('docs/social-graph.html', 'utf8');
const script = html.match(/<script>\s*([\s\S]*?)\s*<\/script>/)[1];
vm.runInNewContext(script, { document, window, alert() {}, setTimeout() {} });
const el = id => document.getElementById(id);
const descendants = root => [root, ...root.children.filter(x => x instanceof Element).flatMap(descendants)];
el('systemTab').onclick();

el('auditFilter').value = 'rumor';
el('auditFilter').onchange({ target: { value: 'rumor' } });
assert.equal(el('graphTitle').textContent, 'Связи каталога');
assert.equal(el('listCount').textContent, 1);
el('list').children[0].onclick();
assert.equal(el('graphTitle').textContent, 'Sample rumor');
const graph = el('graph').children[0];
assert.equal(graph.style.width, '798px');
const paths = descendants(graph).filter(x => x.tag === 'path' && x.attrs.class === 'audit-edge');
assert.equal(paths.length, 2);
const outgoing = paths.map(x => x.attrs.d.match(/^M([\d.]+) ([\d.]+).* ([\d.]+) ([\d.]+)$/)).filter(Boolean);
assert.equal(outgoing.length, 2);
const middle = Number(graph.attrs.viewBox.split(' ')[2]) / 2;
assert(outgoing.every(p => Math.abs(Number(p[1]) - (middle + 95)) < .1 && Number(p[3]) > Number(p[1])), 'outgoing arrows start at the center and end at neighbor cards');
assert(outgoing.some(p => Number(p[2]) !== Number(p[4])), 'arrows follow the neighbors vertically');
const outgoingRects = descendants(graph).filter(x => x.tag === 'rect' && Number(x.attrs.x) > middle);
assert(outgoing.every(p => outgoingRects.some(r => Math.abs(Number(p[3]) - Number(r.attrs.x)) < .1)), 'each arrow ends at a card edge');
for (const rect of descendants(graph).filter(x => x.tag === 'rect')) {
  assert(Number(rect.attrs.x) >= 0 && Number(rect.attrs.x) + Number(rect.attrs.width) <= Number(graph.attrs.viewBox.split(' ')[2]));
  assert(Number(rect.attrs.y) >= 0 && Number(rect.attrs.y) + Number(rect.attrs.height) <= Number(graph.attrs.viewBox.split(' ')[3]));
}
const hermit = descendants(graph).find(x => x.attrs['aria-label']?.startsWith('Hermit'));
hermit.click();
assert.equal(el('graphTitle').textContent, 'Hermit');
assert.equal(el('auditFilter').value, 'all', 'following a node clears an incompatible filter');
assert(el('list').children.some(x => x.className?.includes('active')));
assert(!el('auditFilter').children.some(x => x.value === 'terminal'), 'handlers are not called dead ends');
el('typeFilter').onchange({ target: { value: 'handler' } });
assert.equal(el('listCount').textContent, 1);
el('list').children[0].onclick();
assert.equal(el('graphTitle').textContent, 'После события: handled');
assert.equal(descendants(el('graph').children[0]).filter(x => x.tag === 'path' && x.attrs.class === 'audit-edge').length, 2,
  'handler connects its trigger and execution phase');
el('graph').clientWidth = 340;
window.resize();
assert.equal(el('graph').children[0].style.width, '720px', 'narrow view scrolls without shrinking labels');
el('peopleTab').onclick();
const peopleCards = descendants(el('graph').children[0]).filter(x => x.tag === 'g' && x.attrs.class?.includes('audit-node'));
assert.equal(peopleCards.length, 13, 'the people graph limits visible contacts to twelve readable cards');
const columns = new Map();
for (const card of peopleCards) {
  const rect = card.children.find(x => x.tag === 'rect'), x = Number(rect.attrs.x);
  if (!columns.has(x)) columns.set(x, []);
  columns.get(x).push(Number(rect.attrs.y));
}
for (const ys of columns.values()) {
  ys.sort((a, b) => a - b);
  for (let i = 1; i < ys.length; i++) assert(ys[i] - ys[i - 1] >= 60, 'contact cards do not overlap');
}
console.log('Social graph UI checks passed');
