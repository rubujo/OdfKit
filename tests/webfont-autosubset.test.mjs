import assert from "node:assert/strict";

globalThis.window = globalThis;
globalThis.document = { currentScript: null };

async function verifyHelper(modulePath) {
    delete globalThis.OdfKitWebFontAutoSubset;
    await import(modulePath);

    const helper = globalThis.OdfKitWebFontAutoSubset;
    assert.equal(typeof helper.verifyGlyphRendering, "function");
    assert.equal(typeof helper.createSystemGlyphDetector, "function");
    assert.equal(typeof helper.clearLoadedFaces, "function");
    assert.equal(helper.clearLoadedFaces(), 0);
    const ivs = "𠆩󠄀";
    assert.deepEqual(helper.segmentText(`${ivs}一`), [ivs, "一"]);

    const route = { fontSourceId: "ext-b", minimum: 0x20000, maximum: 0x2FFFF };
    const partitioned = helper.partition(`${ivs}一𠆩`, [route]);
    assert.equal(partitioned.length, 1);
    assert.deepEqual(partitioned[0].clusters, [ivs, "𠆩"]);
    const coverageRoute = { fontSourceId: "custom", matches: cluster => cluster === "幹" };
    assert.deepEqual(helper.partition("一幹", [coverageRoute])[0].clusters, ["幹"]);
    const overlapping = helper.partition("𠆩", [route, route]);
    assert.equal(overlapping.length, 2);
    assert.deepEqual(overlapping.map(group => group.text), ["𠆩", "𠆩"]);
    assert.deepEqual(helper.partition("\uD800𠆩", [
        { fontSourceId: "invalid", minimum: 0xD800, maximum: 0xDFFF },
        route
    ]).map(group => group.text), ["𠆩"]);

    const clusters = Array.from({ length: 1200 }, (_, index) =>
        String.fromCodePoint(0x20000 + index));
    const batches = helper.createBatches(clusters, 512, 48 * 1024);
    assert.equal(batches.length, 3);
    assert.ok(batches.every(batch => Array.from(batch).length <= 512));

    const supplementary = Array.from({ length: 4080 }, (_, index) =>
        String.fromCodePoint(0x20000 + index));
    const mixed = `${supplementary.join("")}一二三丨ㄩ幹`;
    const largePartition = helper.partition(mixed, [route]);
    assert.equal(largePartition[0].clusters.length, 4080);
    assert.equal(helper.createBatches(largePartition[0].clusters, 512, 48 * 1024).length, 8);

    assert.equal(await helper.normalizeManifest({ status: 204 }), null);
    assert.deepEqual(
        await helper.normalizeManifest({ ok: true, status: 200, json: async () => ({ assets: [] }) }),
        { assets: [] });

    const originalCreateTreeWalker = document.createTreeWalker;
    const originalNodeFilter = globalThis.NodeFilter;
    globalThis.NodeFilter = { SHOW_TEXT: 4 };
    document.createTreeWalker = () => {
        let emitted = false;
        return {
            nextNode() {
                if (emitted) {
                    return null;
                }
                emitted = true;
                return {
                    nodeValue: `A${String.fromCodePoint(0xF04E1)}`,
                    parentElement: null
                };
            }
        };
    };
    const requested = [];
    const root = {
        addEventListener() {},
        querySelectorAll: () => [],
        removeEventListener() {}
    };
    const controller = helper.createController({
        root,
        routes: [{ fontSourceId: "missing", minimum: 0x20, maximum: 0x10FFFF }],
        isSystemGlyphAvailable: cluster => cluster === "A",
        request: async (_route, sequences) => {
            requested.push(...sequences);
            return { assets: [] };
        }
    });
    await controller.scan();
    assert.deepEqual(requested, [String.fromCodePoint(0xF04E1)]);
    controller.disconnect();
    document.createTreeWalker = originalCreateTreeWalker;
    globalThis.NodeFilter = originalNodeFilter;
}

await verifyHelper("../samples/WebFonts.AspNetCore/wwwroot/webfont-autosubset.js");
await verifyHelper("../samples/WebFonts.WebForms/webfont-autosubset.js");

console.log("PASS: both webfont helpers handle graphemes, mixed 4,080-scalar batches, and 204.");
