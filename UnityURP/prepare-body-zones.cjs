const fs = require('node:fs');
const path = require('node:path');

const source = path.resolve(__dirname, '..', 'ReferenceCharacters');
const target = path.join(__dirname, 'Assets', 'ArtReview', 'Source', 'Characters');

function openBody(name) {
  const file = fs.readFileSync(path.join(source, name));
  const jsonLength = file.readUInt32LE(12);
  const document = JSON.parse(file.toString('utf8', 20, 20 + jsonLength));
  const body = document.nodes.find(node => node.name === 'Body');
  const primitive = document.meshes[body.mesh].primitives[0];
  function read(index) {
    const accessor = document.accessors[index];
    const view = document.bufferViews[accessor.bufferView];
    if (accessor.byteOffset || view.byteStride || accessor.sparse) {
      throw new Error('Unexpected packing in the upstream body GLB.');
    }
    const offset = 28 + jsonLength + (view.byteOffset || 0);
    return file.subarray(offset, offset + view.byteLength);
  }
  return { primitive, read };
}

const neutral = openBody('base_body.glb');
const animated = openBody('base_body_anim.glb');
const positions = animated.read(animated.primitive.attributes.POSITION);
if (!positions.equals(neutral.read(neutral.primitive.attributes.POSITION)) ||
    !animated.read(animated.primitive.indices).equals(neutral.read(neutral.primitive.indices))) {
  throw new Error('The animated and neutral body meshes do not share vertex order.');
}
const zones = neutral.read(neutral.primitive.attributes._CCZONE);
const count = positions.length / 12;
if (zones.length !== count * 4) throw new Error('Invalid upstream body-zone data.');
const data = Buffer.alloc(4 + count * 14);
data.writeUInt32LE(count);
positions.copy(data, 4);
for (let vertex = 0; vertex < count; vertex++) {
  data.writeUInt16LE(zones.readFloatLE(vertex * 4), 4 + positions.length + vertex * 2);
}

const catalog = JSON.parse(fs.readFileSync(path.join(source, 'clothing.json'), 'utf8'));
const masks = ['tshirt', 'hoodie', 'shirt', 'jacket'].map(top => ({
  top,
  mask: catalog.items.filter(item => [top, 'jeans', 'shoes'].includes(item.id))
    .flatMap(item => item.hidesBodyZones)
    .reduce((mask, name) => mask | catalog.bodyZones[name], 0),
}));
if (masks.some(entry => !entry.mask)) throw new Error('Missing outfit coverage.');
fs.mkdirSync(target, { recursive: true });
fs.writeFileSync(path.join(target, 'body-zones.bytes'), data);
fs.writeFileSync(path.join(target, 'outfit-masks.json'), JSON.stringify({ outfits: masks }, null, 2) + '\n');
console.log(`Verified coverage for ${count} vertices and ${masks.length} outfits.`);
