(function (global) {
    "use strict";

    const loaderScript = document.currentScript;
    const ignoredParents = new Set(["SCRIPT", "STYLE", "NOSCRIPT", "TEXTAREA"]);

    function collectText(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        let text = "";
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (!ignoredParents.has(node.parentElement?.tagName)) {
                text += node.nodeValue ?? "";
            }
        }

        return text;
    }

    function partition(text, routes) {
        const groups = new Map(routes.map(route => [route.fontSourceId, new Set()]));
        for (const character of text) {
            const scalar = character.codePointAt(0);
            const route = routes.find(candidate => scalar >= candidate.minimum && scalar <= candidate.maximum);
            if (route) {
                groups.get(route.fontSourceId).add(character);
            }
        }

        return routes
            .map(route => ({ route, text: Array.from(groups.get(route.fontSourceId)).join("") }))
            .filter(group => group.text.length > 0);
    }

    async function injectManifest(manifest, publicBaseUrl) {
        if (!manifest?.assets?.length) {
            return;
        }

        await Promise.all(manifest.assets.map(async asset => {
            const format = String(asset.format).toLowerCase();
            const source = `url("${publicBaseUrl}/${asset.sha256}/${asset.fileName}") format("${format}")`;
            const face = new FontFace(asset.fontFamily, source, {
                display: "swap",
                unicodeRange: asset.unicodeRanges.join(", ")
            });
            document.fonts.add(face);
            await face.load();
        }));
    }

    async function scanAndGenerate(options) {
        const groups = partition(collectText(options.root ?? document.body), options.routes);
        await Promise.all(groups.map(async group => {
            const manifest = await options.request(group.route, [group.text]);
            await injectManifest(manifest, options.publicBaseUrl ?? "/_odf-fonts");
        }));
        return groups.map(group => group.text);
    }

    global.OdfKitWebFontAutoSubset = { collectText, partition, scanAndGenerate };

    if (loaderScript?.hasAttribute("data-odf-auto")) {
        const start = () => {
            if (typeof global.odfKitRequestWebFonts !== "function") {
                return;
            }

            const minimum = Number(loaderScript.dataset.odfMinimum);
            const maximum = Number(loaderScript.dataset.odfMaximum);
            if (!loaderScript.dataset.odfFontSourceId
                || !Number.isInteger(minimum)
                || !Number.isInteger(maximum)) {
                return;
            }

            scanAndGenerate({
                routes: [{
                    fontSourceId: loaderScript.dataset.odfFontSourceId,
                    minimum,
                    maximum
                }],
                request: global.odfKitRequestWebFonts,
                publicBaseUrl: loaderScript.dataset.odfPublicBaseUrl
            }).catch(error => global.dispatchEvent(new CustomEvent(
                "odfkitwebfonterror",
                { detail: error })));
        };

        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", start, { once: true });
        } else {
            start();
        }
    }
})(window);
