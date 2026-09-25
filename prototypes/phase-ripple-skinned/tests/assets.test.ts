import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

function pngInfo(bytes: Buffer): { width: number; height: number; colorType: number } {
  assert.equal(bytes.subarray(1, 4).toString('ascii'), 'PNG');
  assert.equal(bytes.subarray(12, 16).toString('ascii'), 'IHDR');
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
    colorType: bytes[25] as number,
  };
}

test('the generated board is a tall static production bitmap sized for the phone slice', async () => {
  const bytes = await readFile(resolve('assets/tabletop-board.png'));
  const info = pngInfo(bytes);
  assert.ok(info.width >= 800 && info.height >= 1800);
  assert.ok(Math.abs(info.width / info.height - 390 / 844) < 0.01);
});

for (const name of ['piece-outbound.png', 'piece-returned.png']) {
  test(`${name} is an independent square RGBA cutout`, async () => {
    const bytes = await readFile(resolve('assets', name));
    const info = pngInfo(bytes);
    assert.equal(info.width, info.height);
    assert.equal(info.colorType, 6, 'PNG must include alpha');
    assert.ok(info.width >= 1024);
  });
}
