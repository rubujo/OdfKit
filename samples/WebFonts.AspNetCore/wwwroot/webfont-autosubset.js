(function (global) {
    "use strict";

    const loaderScript = document.currentScript;
    const ignoredParents = new Set(["SCRIPT", "STYLE", "NOSCRIPT"]);
    const segmenter = typeof Intl === "object" && typeof Intl.Segmenter === "function"
        ? new Intl.Segmenter(undefined, { granularity: "grapheme" })
        : null;
    const utf8 = typeof TextEncoder === "function" ? new TextEncoder() : null;
    const loadedFaces = new Map();

    function isIgnored(node, root) {
        for (let element = node.parentElement; element; element = element.parentElement) {
            if (ignoredParents.has(element.tagName) || element.hasAttribute("data-odf-ignore")) {
                return true;
            }
            if (element === root) {
                break;
            }
        }
        return false;
    }

    function isIgnoredElement(element, root) {
        for (let current = element; current; current = current.parentElement) {
            if (ignoredParents.has(current.tagName) || current.hasAttribute("data-odf-ignore")) {
                return true;
            }
            if (current === root) {
                break;
            }
        }
        return false;
    }

    function collectText(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        let text = "";
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (!isIgnored(node, root)) {
                text += node.nodeValue ?? "";
            }
        }

        const elements = root.querySelectorAll?.("*") ?? [];
        for (const element of elements) {
            if (element.shadowRoot && !isIgnoredElement(element, root)) {
                text += collectText(element.shadowRoot);
            }
            if (!isIgnoredElement(element, root)
                && (element.tagName === "INPUT" || element.tagName === "TEXTAREA")
                && element.type !== "password"
                && element.type !== "hidden") {
                text += element.value || element.placeholder || "";
            }
        }
        return text;
    }

    function segmentText(text) {
        if (segmenter) {
            return Array.from(segmenter.segment(text), item => item.segment);
        }

        const result = [];
        let cluster = "";
        let previousScalar = -1;
        let regionalCount = 0;
        for (const character of text) {
            const scalar = character.codePointAt(0);
            const isMark = /\p{Mark}/u.test(character);
            const isVariation = scalar >= 0xFE00 && scalar <= 0xFE0F
                || scalar >= 0xE0100 && scalar <= 0xE01EF;
            const isModifier = scalar >= 0x1F3FB && scalar <= 0x1F3FF;
            const isRegional = scalar >= 0x1F1E6 && scalar <= 0x1F1FF;
            const joinsCluster = cluster.length > 0
                && (isMark
                    || isVariation
                    || isModifier
                    || scalar === 0x200D
                    || previousScalar === 0x200D
                    || isRegional && regionalCount % 2 === 1);
            if (!joinsCluster && cluster.length > 0) {
                result.push(cluster);
                cluster = "";
                regionalCount = 0;
            }
            cluster += character;
            regionalCount = isRegional ? regionalCount + 1 : 0;
            previousScalar = scalar;
        }
        if (cluster.length > 0) {
            result.push(cluster);
        }
        return result;
    }

    function routeMatches(route, scalar) {
        if (Array.isArray(route.ranges)) {
            return route.ranges.some(range => scalar >= range.minimum && scalar <= range.maximum);
        }
        return scalar >= route.minimum && scalar <= route.maximum;
    }

    function partition(text, routes) {
        const groups = routes.map(route => ({ route, clusters: [], seen: new Set() }));
        for (const cluster of segmentText(text)) {
            const scalars = Array.from(cluster, character => character.codePointAt(0));
            if (scalars.some(scalar => scalar >= 0xD800 && scalar <= 0xDFFF)) {
                continue;
            }
            for (let index = 0; index < routes.length; index++) {
                const route = routes[index];
                const matches = typeof route.matches === "function"
                    ? route.matches(cluster, scalars)
                    : scalars.some(scalar => routeMatches(route, scalar));
                if (matches && !groups[index].seen.has(cluster)) {
                    groups[index].seen.add(cluster);
                    groups[index].clusters.push(cluster);
                }
            }
        }
        return groups
            .filter(group => group.clusters.length > 0)
            .map(group => ({ route: group.route, clusters: group.clusters, text: group.clusters.join("") }));
    }

    function createBatches(clusters, maximumScalars, maximumTextBytes) {
        const batches = [];
        let batch = "";
        let scalarCount = 0;
        for (const cluster of clusters) {
            const clusterScalars = Array.from(cluster).length;
            const clusterBytes = utf8Length(cluster);
            if (clusterScalars > maximumScalars || clusterBytes > maximumTextBytes) {
                throw new RangeError("One grapheme cluster exceeds the configured WebFont request limit.");
            }
            if (batch.length > 0
                && (scalarCount + clusterScalars > maximumScalars
                    || utf8Length(batch) + clusterBytes > maximumTextBytes)) {
                batches.push(batch);
                batch = "";
                scalarCount = 0;
            }
            batch += cluster;
            scalarCount += clusterScalars;
        }
        if (batch.length > 0) {
            batches.push(batch);
        }
        return batches;
    }

    function utf8Length(value) {
        return utf8 ? utf8.encode(value).length : unescape(encodeURIComponent(value)).length;
    }

    async function normalizeManifest(value) {
        if (value?.status === 204) {
            return null;
        }
        let data;
        if (typeof value?.json === "function") {
            if (value.ok === false) {
                throw new Error(`WebFont generation failed with HTTP ${value.status}.`);
            }
            data = await value.json();
        } else {
            data = value ?? null;
        }
        if (data === null) {
            return null;
        }

        const rawAssets = data.assets ?? data.Assets ?? [];
        if (!Array.isArray(rawAssets)) {
            throw new TypeError("The WebFont manifest assets value must be an array.");
        }
        const assets = rawAssets.map((asset, index) => {
            const fileName = asset?.fileName ?? asset?.FileName;
            const sha256 = asset?.sha256 ?? asset?.Sha256;
            const fontFamily = asset?.fontFamily ?? asset?.FontFamily;
            const format = asset?.format ?? asset?.Format;
            const unicodeRanges = asset?.unicodeRanges ?? asset?.UnicodeRanges ?? [];
            if (typeof fileName !== "string"
                || fileName.length === 0
                || typeof sha256 !== "string"
                || sha256.length === 0
                || typeof fontFamily !== "string"
                || fontFamily.length === 0
                || typeof format !== "string"
                || format.length === 0
                || !Array.isArray(unicodeRanges)
                || unicodeRanges.some(range => typeof range !== "string")) {
                throw new TypeError(`WebFont manifest asset ${index} is invalid.`);
            }
            return { fileName, sha256, fontFamily, format, unicodeRanges };
        });
        return { ...data, assets };
    }

    function createAssetSource(publicBaseUrl, asset) {
        const base = new URL(publicBaseUrl, document.baseURI);
        if (base.protocol !== "http:" && base.protocol !== "https:") {
            throw new TypeError("The WebFont public base URL must use HTTP or HTTPS.");
        }
        base.hash = "";
        base.search = "";
        if (!base.pathname.endsWith("/")) {
            base.pathname += "/";
        }
        const path = `${encodeURIComponent(asset.sha256)}/${encodeURIComponent(asset.fileName)}`;
        const assetUrl = new URL(path, base);
        const format = asset.format.toLowerCase();
        if (!/^[a-z0-9-]+$/.test(format)) {
            throw new TypeError("The WebFont asset format is invalid.");
        }
        return `url("${assetUrl.href}") format("${format}")`;
    }

    function quoteFontFamily(fontFamily) {
        return `"${fontFamily.replaceAll("\\", "\\\\").replaceAll("\"", "\\\"")}"`;
    }

    function renderGlyphFingerprint(font, text, fontSize) {
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(256, Math.ceil(fontSize * Math.max(1, Array.from(text).length) * 1.5));
        canvas.height = Math.max(256, Math.ceil(fontSize * 1.5));
        const context = canvas.getContext("2d", { willReadFrequently: true });
        if (!context) {
            return null;
        }

        context.clearRect(0, 0, canvas.width, canvas.height);
        context.fillStyle = "#000";
        context.font = `${fontSize}px ${font}`;
        context.textBaseline = "top";
        context.fillText(text, 8, 8);
        const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
        let hash = 2166136261;
        let ink = 0;
        for (let index = 3; index < pixels.length; index += 4) {
            const alpha = pixels[index];
            hash ^= alpha;
            hash = Math.imul(hash, 16777619);
            ink += alpha;
        }
        return { hash: hash >>> 0, ink };
    }

    function createSystemGlyphDetector(options = {}) {
        const fontFamily = options.fontFamily ?? "system-ui, sans-serif";
        const fontSize = options.fontSize ?? 48;
        const missingGlyphs = options.missingGlyphs ?? [
            "\u0378",
            "\uFFFF",
            String.fromCodePoint(0x10FFFF)
        ];
        const assumePrivateUseMissing = options.assumePrivateUseMissing === true;
        const cache = new Map();
        let missingFingerprints = null;
        return async cluster => {
            if (cache.has(cluster)) {
                return cache.get(cluster);
            }
            const detection = (async () => {
                if (!/\S/u.test(cluster)) {
                    return true;
                }
                if (assumePrivateUseMissing
                    && Array.from(cluster).every(character => {
                        const scalar = character.codePointAt(0);
                        return (scalar >= 0xE000 && scalar <= 0xF8FF)
                            || (scalar >= 0xF0000 && scalar <= 0xFFFFD)
                            || (scalar >= 0x100000 && scalar <= 0x10FFFD);
                    })) {
                    return false;
                }
                await document.fonts?.load(`${fontSize}px ${fontFamily}`, cluster);
                const target = renderGlyphFingerprint(fontFamily, cluster, fontSize);
                missingFingerprints ??= missingGlyphs.map(glyph =>
                    renderGlyphFingerprint(fontFamily, glyph, fontSize));
                return target !== null
                    && target.ink > 0
                    && missingFingerprints.every(control => control !== null
                        && (target.hash !== control.hash || target.ink !== control.ink));
            })();
            cache.set(cluster, detection);
            return detection;
        };
    }

    async function verifyGlyphRendering(
        fontFamily,
        text,
        { fallbackFamily = "serif", fontSize = 160 } = {}) {
        if (typeof fontFamily !== "string"
            || fontFamily.length === 0
            || typeof text !== "string"
            || text.length === 0
            || !document.fonts
            || typeof document.createElement !== "function") {
            return false;
        }

        const quotedFamily = quoteFontFamily(fontFamily);
        await document.fonts.load(`${fontSize}px ${quotedFamily}`, text);
        await document.fonts.ready;
        const loadedFace = Array.from(document.fonts).some(face =>
            face.family.replace(/^["']|["']$/g, "") === fontFamily
            && face.status === "loaded");
        if (!loadedFace) {
            return false;
        }

        return segmentText(text).every(cluster => {
            const target = renderGlyphFingerprint(
                `${quotedFamily}, ${fallbackFamily}`,
                cluster,
                fontSize);
            const fallback = renderGlyphFingerprint(
                `"OdfKit Missing Glyph Proof", ${fallbackFamily}`,
                cluster,
                fontSize);
            return target !== null
                && fallback !== null
                && target.ink > 0
                && (target.hash !== fallback.hash || target.ink !== fallback.ink);
        });
    }

    async function injectManifest(manifest, publicBaseUrl, route = {}) {
        if (!manifest?.assets?.length || typeof FontFace !== "function" || !document.fonts) {
            return;
        }
        await Promise.all(manifest.assets.map(async asset => {
            const key = [
                asset.sha256,
                asset.fileName,
                asset.fontFamily,
                route.fontDisplay ?? "swap",
                route.fontStyle ?? "normal",
                route.fontWeight ?? "normal",
                route.fontStretch ?? "normal",
                asset.unicodeRanges.join(",")
            ].join("/");
            if (loadedFaces.has(key)) {
                return;
            }
            const source = createAssetSource(publicBaseUrl, asset);
            const face = new FontFace(asset.fontFamily, source, {
                display: route.fontDisplay ?? "swap",
                style: route.fontStyle ?? "normal",
                weight: String(route.fontWeight ?? "normal"),
                stretch: route.fontStretch ?? "normal",
                unicodeRange: asset.unicodeRanges.join(", ")
            });
            await face.load();
            document.fonts.add(face);
            loadedFaces.set(key, face);
        }));
    }

    function clearLoadedFaces(fontFamily) {
        const expectedFamily = typeof fontFamily === "string"
            ? fontFamily.replace(/^["']|["']$/g, "")
            : null;
        let removed = 0;
        for (const [key, face] of loadedFaces) {
            const actualFamily = face.family.replace(/^["']|["']$/g, "");
            if (expectedFamily !== null && actualFamily !== expectedFamily) {
                continue;
            }
            document.fonts?.delete(face);
            loadedFaces.delete(key);
            removed++;
        }
        return removed;
    }

    function createController(options) {
        const root = options.root ?? document.body;
        const completed = new Map(options.routes.map((route, index) => [index, new Set()]));
        const pending = new Map(options.routes.map((route, index) => [index, new Set()]));
        const maximumScalars = options.maximumScalarsPerRequest ?? 512;
        const maximumTextBytes = options.maximumTextBytesPerRequest ?? 48 * 1024;
        const debounceMilliseconds = options.debounceMilliseconds ?? 100;
        const maximumConcurrentRoutes = Math.max(1, options.maximumConcurrentRoutes ?? 2);
        const systemCoverage = new Map(
            options.routes.map((route, index) => [index, new Map()]));
        let timer = 0;
        let active = Promise.resolve([]);
        let observer = null;
        let disconnected = false;

        async function scan() {
            if (disconnected) {
                return [];
            }
            const groups = partition(collectText(root), options.routes);
            const generated = [];
            let nextGroup = 0;
            async function processNextGroup() {
                while (nextGroup < groups.length) {
                    if (disconnected) {
                        return;
                    }
                    const group = groups[nextGroup++];
                    const routeIndex = options.routes.indexOf(group.route);
                    const candidates = group.clusters.filter(cluster =>
                        !completed.get(routeIndex).has(cluster)
                        && !pending.get(routeIndex).has(cluster));
                    const routeCoverage = systemCoverage.get(routeIndex);
                    const coverage = typeof options.isSystemGlyphAvailable === "function"
                        ? await Promise.all(candidates.map(cluster => {
                            if (!routeCoverage.has(cluster)) {
                                routeCoverage.set(
                                    cluster,
                                    Promise.resolve(
                                        options.isSystemGlyphAvailable(cluster, group.route)));
                            }
                            return routeCoverage.get(cluster);
                        }))
                        : candidates.map(() => false);
                    const unseen = candidates.filter((cluster, index) => {
                        if (coverage[index]) {
                            completed.get(routeIndex).add(cluster);
                            return false;
                        }
                        return true;
                    });
                    if (unseen.length === 0) {
                        continue;
                    }
                    unseen.forEach(cluster => pending.get(routeIndex).add(cluster));
                    try {
                        for (const text of createBatches(
                            unseen,
                            maximumScalars,
                            maximumTextBytes)) {
                            if (disconnected) {
                                return;
                            }
                            const manifest = await normalizeManifest(
                                await options.request(group.route, [text]));
                            if (disconnected) {
                                return;
                            }
                            await injectManifest(
                                manifest,
                                options.publicBaseUrl ?? "/_odf-fonts",
                                group.route);
                            generated.push(text);
                        }
                        unseen.forEach(cluster => completed.get(routeIndex).add(cluster));
                    } finally {
                        unseen.forEach(cluster => pending.get(routeIndex).delete(cluster));
                    }
                }
            }
            await Promise.all(Array.from(
                { length: Math.min(maximumConcurrentRoutes, groups.length) },
                processNextGroup));
            return generated;
        }

        function schedule() {
            if (disconnected) {
                return;
            }
            global.clearTimeout(timer);
            timer = global.setTimeout(() => {
                active = active.then(scan).catch(error => {
                    global.dispatchEvent(new CustomEvent("odfkitwebfonterror", { detail: error }));
                    return [];
                });
            }, debounceMilliseconds);
        }

        function observe() {
            if (typeof MutationObserver !== "function" || observer) {
                return;
            }
            observer = new MutationObserver(() => {
                observeOpenShadowRoots(root, observer);
                schedule();
            });
            observeOpenShadowRoots(root, observer);
            root.addEventListener?.("input", schedule, true);
            root.addEventListener?.("change", schedule, true);
        }

        return {
            scan: () => active = active.catch(() => []).then(scan),
            observe,
            disconnect: () => {
                disconnected = true;
                global.clearTimeout(timer);
                timer = 0;
                observer?.disconnect();
                root.removeEventListener?.("input", schedule, true);
                root.removeEventListener?.("change", schedule, true);
            }
        };
    }

    function observeOpenShadowRoots(root, observer) {
        observer.observe(root, {
            attributes: true,
            attributeFilter: ["data-odf-ignore", "placeholder", "value"],
            childList: true,
            characterData: true,
            subtree: true
        });
        const elements = root.querySelectorAll?.("*") ?? [];
        for (const element of elements) {
            if (element.shadowRoot && !isIgnoredElement(element, root)) {
                observeOpenShadowRoots(element.shadowRoot, observer);
            }
        }
    }

    async function scanAndGenerate(options) {
        const controller = createController(options);
        return await controller.scan();
    }

    global.OdfKitWebFontAutoSubset = {
        collectText,
        segmentText,
        partition,
        createBatches,
        normalizeManifest,
        injectManifest,
        clearLoadedFaces,
        createSystemGlyphDetector,
        verifyGlyphRendering,
        createController,
        scanAndGenerate
    };

    if (loaderScript?.hasAttribute("data-odf-auto")) {
        const start = () => {
            if (typeof global.odfKitRequestWebFonts !== "function") {
                global.addEventListener("odfkitwebfontrequestready", start, { once: true });
                return;
            }
            const minimum = Number(loaderScript.dataset.odfMinimum);
            const maximum = Number(loaderScript.dataset.odfMaximum);
            if (!loaderScript.dataset.odfFontSourceId
                || !Number.isInteger(minimum)
                || !Number.isInteger(maximum)
                || minimum < 0
                || maximum > 0x10FFFF
                || minimum > maximum) {
                return;
            }
            const configuredRoot = loaderScript.dataset.odfRoot
                ? document.querySelector(loaderScript.dataset.odfRoot)
                : document.body;
            if (!configuredRoot) {
                return;
            }
            const systemFontFamily = loaderScript.dataset.odfSystemFontFamily;
            const controller = createController({
                root: configuredRoot,
                routes: [{
                    fontSourceId: loaderScript.dataset.odfFontSourceId,
                    minimum,
                    maximum
                }],
                isSystemGlyphAvailable: systemFontFamily
                    ? createSystemGlyphDetector({
                        fontFamily: systemFontFamily,
                        assumePrivateUseMissing:
                            loaderScript.dataset.odfAssumePrivateUseMissing === "true"
                    })
                    : undefined,
                request: global.odfKitRequestWebFonts,
                publicBaseUrl: loaderScript.dataset.odfPublicBaseUrl,
                maximumScalarsPerRequest: Number(loaderScript.dataset.odfMaximumScalars) || 512
            });
            controller.scan().catch(error => global.dispatchEvent(
                new CustomEvent("odfkitwebfonterror", { detail: error })));
            if (loaderScript.dataset.odfObserve !== "false") {
                controller.observe();
            }
        };
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", start, { once: true });
        } else {
            start();
        }
    }
})(window);
