(function () {
    "use strict";

    const fontFamilies = {
        "cns-sung-plus": "OdfKit CNS Sung Plus",
        "cns-kai-plus": "OdfKit CNS Kai Plus"
    };
    const systemFontFamily =
        "\"Segoe UI\", \"Microsoft JhengHei\", \"PMingLiU-ExtB\", "
        + "\"MingLiU-ExtB\", PMingLiU, MingLiU, system-ui, sans-serif";
    const previewClasses = ["font-cns-sung-plus", "font-cns-kai-plus"];
    const commonPuaScalars =
        Array.from({ length: 800 }, (_, index) => 0xF04E1 + index);
    const commonPuaText =
        commonPuaScalars.map(scalar => String.fromCodePoint(scalar)).join("");
    const responseEvidence = new Map();

    window.odfKitRequestWebFonts = async function (route, sequences) {
        const formatSelect = document.getElementById("formatSelect");
        const fontSelect = document.getElementById("fontSelect");
        const requestedFormat = formatSelect?.value ?? "Woff2";
        const requestStartedAt = performance.now();
        const requestedScalars = sequences
            .flatMap(sequence => Array.from(sequence))
            .map(character => character.codePointAt(0));
        if (requestedScalars.some(scalar => scalar < 0xE000)) {
            document.documentElement.dataset.odfRequestedBasic = "true";
        }
        if (requestedScalars.some(scalar => scalar >= 0x20000 && scalar <= 0x2FFFF)) {
            document.documentElement.dataset.odfRequestedExtB = "true";
        }
        if (requestedScalars.some(scalar => scalar >= 0xF0000 && scalar <= 0xFFFFD)) {
            document.documentElement.dataset.odfRequestedPua = "true";
        }
        const response = await fetch("/sample/generate", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                fontSourceId: route.fontSourceId,
                format: requestedFormat,
                sequences
            })
        });
        if (!response.ok && response.status !== 204) {
            throw new Error(`WebFont generation failed with HTTP ${response.status}.`);
        }

        if ((formatSelect && formatSelect.value !== requestedFormat)
            || (fontSelect && fontSelect.value !== route.fontSourceId)) {
            return null;
        }

        const data = response.status === 204 ? null : await response.clone().json();
        const assets = data?.assets ?? data?.Assets ?? [];
        const evidenceKey = `${route.fontSourceId}/${requestedFormat}`;
        const previousEvidence = responseEvidence.get(evidenceKey);
        responseEvidence.set(evidenceKey, {
            formats: [...new Set([
                ...(previousEvidence?.formats ?? []),
                ...assets.map(asset => asset.format ?? asset.Format)
            ])],
            fontFamilies: [...new Set([
                ...(previousEvidence?.fontFamilies ?? []),
                ...assets.map(asset => asset.fontFamily ?? asset.FontFamily)
            ])],
            assetCount: (previousEvidence?.assetCount ?? 0) + assets.length,
            generationMilliseconds:
                (previousEvidence?.generationMilliseconds ?? 0)
                + performance.now() - requestStartedAt
        });
        document.documentElement.dataset.odfRequestCount = String(
            Number(document.documentElement.dataset.odfRequestCount || "0") + 1);
        return response;
    };

    document.addEventListener("DOMContentLoaded", function () {
        const fontSelect = document.getElementById("fontSelect");
        const formatSelect = document.getElementById("formatSelect");
        const rareInput = document.getElementById("rareInput");
        const previewBox = document.getElementById("previewBox");
        const status = document.getElementById("status");
        const detectSystemGlyph = OdfKitWebFontAutoSubset.createSystemGlyphDetector({
            fontFamily: systemFontFamily,
            assumePrivateUseMissing: true
        });
        let controller = null;
        let selectionVersion = 0;
        let systemCoveredScalars = 0;

        function renderPreview(selectedValue) {
            previewBox.replaceChildren();
            const systemProbe = document.createElement("p");
            systemProbe.id = "systemCoverageProbe";
            systemProbe.textContent =
                "系統字型覆蓋：一般文字 ABC 一二三；Ext-B：𠀀𠆩𪚥。";
            previewBox.append(systemProbe);
            if (selectedValue === "cns-sung-plus") {
                const target = document.createElement("p");
                target.textContent =
                    `CNS 17-2174／U+FFAE0：${String.fromCodePoint(0xFFAE0)}`;
                previewBox.append(target);
            }
            const title = document.createElement("p");
            title.textContent =
                `${fontSelect.selectedOptions[0].text}共通 PUA ${commonPuaScalars.length} 字：`;
            const common = document.createElement("div");
            common.id = "commonPuaProbe";
            common.textContent = commonPuaText;
            const liveInput = document.createElement("div");
            liveInput.id = "liveInputPreview";
            liveInput.textContent = rareInput.value;
            previewBox.append(title, common, liveInput);
        }

        async function updateFontSelection() {
            const currentVersion = ++selectionVersion;
            const selectedValue = fontSelect.value;
            const selectedFormat = formatSelect.value;
            const fontFamily = fontFamilies[selectedValue];
            const selectionStartedAt = performance.now();
            performance.clearResourceTimings();
            responseEvidence.delete(`${selectedValue}/${selectedFormat}`);
            systemCoveredScalars = 0;
            document.documentElement.dataset.odfRequestedBasic = "false";
            document.documentElement.dataset.odfRequestedExtB = "false";
            document.documentElement.dataset.odfRequestedPua = "false";
            document.body.dataset.internationalReady = "pending";
            window.__odfKitInternationalProof = { loadedCases: [] };
            status.textContent =
                `正在產生 ${fontSelect.selectedOptions[0].text} ${selectedFormat}…`;
            previewBox.classList.remove(...previewClasses);
            previewBox.classList.add(`font-${selectedValue}`);
            renderPreview(selectedValue);
            controller?.disconnect();
            OdfKitWebFontAutoSubset.clearLoadedFaces();
            controller = OdfKitWebFontAutoSubset.createController({
                root: previewBox,
                routes: [{
                    fontSourceId: selectedValue,
                    minimum: 0x20,
                    maximum: 0x10FFFF
                }],
                isSystemGlyphAvailable: async (cluster, route) => {
                    const available = await detectSystemGlyph(cluster, route);
                    if (available) {
                        systemCoveredScalars += Array.from(cluster).length;
                    }
                    return available;
                },
                request: window.odfKitRequestWebFonts
            });
            controller.observe();

            try {
                await controller.scan();
                if (currentVersion !== selectionVersion
                    || fontSelect.value !== selectedValue
                    || formatSelect.value !== selectedFormat) {
                    return;
                }
                const probeScalars = [
                    ...(selectedValue === "cns-sung-plus" ? [0xFFAE0] : []),
                    0xF04E1,
                    0xF0680,
                    0xF0800
                ];
                const glyphResults = await Promise.all(probeScalars.map(async scalar => ({
                    scalar,
                    rendered: await OdfKitWebFontAutoSubset.verifyGlyphRendering(
                        fontFamily,
                        String.fromCodePoint(scalar))
                })));
                const rendered = glyphResults.every(result => result.rendered);
                const evidence = responseEvidence.get(`${selectedValue}/${selectedFormat}`);
                const isExpectedFormat = evidence?.formats.length === 1
                    && evidence.formats[0] === selectedFormat
                    && evidence.fontFamilies.length === 1
                    && evidence.fontFamilies[0] === fontFamily;
                const ready = rendered && isExpectedFormat;
                document.documentElement.dataset.odfGlyphRendered = String(rendered);
                document.documentElement.dataset.odfFailedGlyphs = glyphResults
                    .filter(result => !result.rendered)
                    .map(result => `U+${result.scalar.toString(16).toUpperCase()}`)
                    .join(",");
                document.documentElement.dataset.odfExpectedFormat =
                    String(isExpectedFormat);
                const fontResources = performance.getEntriesByType("resource")
                    .filter(entry => entry.name.includes("/_odf-fonts/")
                        && !entry.name.endsWith("/generate"));
                const fontTransferBytes = fontResources.reduce(
                    (sum, entry) => sum + (entry.encodedBodySize || entry.transferSize || 0),
                    0);
                const elapsedMilliseconds = performance.now() - selectionStartedAt;
                document.documentElement.dataset.odfSelectedFontSource = selectedValue;
                document.documentElement.dataset.odfAssetFormat =
                    evidence?.formats.join(",") ?? "";
                document.documentElement.dataset.odfSystemCovered =
                    String(systemCoveredScalars);
                document.documentElement.dataset.odfElapsedMilliseconds =
                    elapsedMilliseconds.toFixed(1);
                document.documentElement.dataset.odfFontTransferBytes =
                    String(fontTransferBytes);
                document.body.dataset.internationalReady = String(ready);
                status.textContent = ready
                    ? `${fontSelect.selectedOptions[0].text} ${selectedFormat} 已載入`
                    : `${selectedFormat} 字形驗證失敗（${document.documentElement.dataset.odfFailedGlyphs}）`;
                window.__odfKitInternationalProof = {
                    loadedCases: ready ? [{
                        fontSourceId: selectedValue,
                        puaScalarCount: commonPuaScalars.length,
                        format: evidence.formats[0],
                        assetCount: evidence.assetCount,
                        systemCoveredScalars,
                        generationMilliseconds: evidence.generationMilliseconds,
                        elapsedMilliseconds,
                        fontTransferBytes
                    }] : []
                };
            } catch (error) {
                if (currentVersion !== selectionVersion) {
                    return;
                }
                document.body.dataset.internationalReady = "false";
                status.textContent = String(error);
            }
        }

        formatSelect.addEventListener("change", updateFontSelection);
        fontSelect.addEventListener("change", updateFontSelection);
        rareInput.addEventListener("input", updateFontSelection);
        updateFontSelection();
    });
}());
