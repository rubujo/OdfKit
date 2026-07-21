(function (global) {
    "use strict";

    const loaderScript = document.currentScript;
    const ignoredParents = new Set(["SCRIPT", "STYLE", "NOSCRIPT"]);
    const segmenter = typeof Intl === "object" && typeof Intl.Segmenter === "function"
        ? new Intl.Segmenter(undefined, { granularity: "grapheme" })
        : null;
    const utf8 = typeof TextEncoder === "function" ? new TextEncoder() : null;
    const loadedFaces = new Set();

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
        if (typeof value?.json === "function") {
            if (value.ok === false) {
                throw new Error(`WebFont generation failed with HTTP ${value.status}.`);
            }
            return await value.json();
        }
        return value ?? null;
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
            const format = String(asset.format).toLowerCase();
            const base = publicBaseUrl.replace(/\/$/, "");
            const source = `url("${base}/${asset.sha256}/${asset.fileName}") format("${format}")`;
            const face = new FontFace(asset.fontFamily, source, {
                display: route.fontDisplay ?? "swap",
                style: route.fontStyle ?? "normal",
                weight: String(route.fontWeight ?? "normal"),
                stretch: route.fontStretch ?? "normal",
                unicodeRange: asset.unicodeRanges.join(", ")
            });
            await face.load();
            document.fonts.add(face);
            loadedFaces.add(key);
        }));
    }

    function createController(options) {
        const root = options.root ?? document.body;
        const completed = new Map(options.routes.map((route, index) => [index, new Set()]));
        const pending = new Map(options.routes.map((route, index) => [index, new Set()]));
        const maximumScalars = options.maximumScalarsPerRequest ?? 512;
        const maximumTextBytes = options.maximumTextBytesPerRequest ?? 48 * 1024;
        const debounceMilliseconds = options.debounceMilliseconds ?? 100;
        const maximumConcurrentRoutes = Math.max(1, options.maximumConcurrentRoutes ?? 2);
        let timer = 0;
        let active = Promise.resolve([]);
        let observer = null;

        async function scan() {
            const groups = partition(collectText(root), options.routes);
            const generated = [];
            let nextGroup = 0;
            async function processNextGroup() {
                while (nextGroup < groups.length) {
                    const group = groups[nextGroup++];
                const routeIndex = options.routes.indexOf(group.route);
                const unseen = group.clusters.filter(cluster =>
                    !completed.get(routeIndex).has(cluster) && !pending.get(routeIndex).has(cluster));
                if (unseen.length === 0) {
                    continue;
                }
                unseen.forEach(cluster => pending.get(routeIndex).add(cluster));
                try {
                    for (const text of createBatches(unseen, maximumScalars, maximumTextBytes)) {
                        const manifest = await normalizeManifest(await options.request(group.route, [text]));
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
            const controller = createController({
                root: configuredRoot,
                routes: [{
                    fontSourceId: loaderScript.dataset.odfFontSourceId,
                    minimum,
                    maximum
                }],
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
