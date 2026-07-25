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
        const sidecarEnabled = document.getElementById("sidecarEnabled");
        const formatSelect = document.getElementById("formatSelect");
        const fontSelect = document.getElementById("fontSelect");
        const requestedFormat = formatSelect?.value ?? "Woff2";
        const requestedBackend = sidecarEnabled?.checked === false ? "managed" : "sidecar";
        const fontFamily = fontFamilies[route.fontSourceId];
        const requestStartedAt = performance.now();
        const requestedScalars = sequences
            .flatMap(sequence => Array.from(sequence))
            .map(character => character.codePointAt(0));
        if (requestedScalars.some(scalar => scalar < 0xE000)) {
            document.documentElement.dataset.odfRequestedBasic = "true";
        }
        if (requestedScalars.some(scalar => scalar >= 0xF0000 && scalar <= 0xFFFFD)) {
            document.documentElement.dataset.odfRequestedPua = "true";
        }
        const response = await fetch("/WebFontGenerate.ashx", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-OdfKit-WebFont-Backend": requestedBackend
            },
            body: JSON.stringify({
                fontSourceId: route.fontSourceId,
                faceIndex: 0,
                profileId: "cns11643-euc-tw-2026-05-05",
                fontFamily,
                sequences,
                formats: [requestedFormat],
                requiredBrowserTargets: ["Chromium"]
            })
        });
        if (!response.ok && response.status !== 204) {
            throw new Error(`WebFont generation failed with HTTP ${response.status}.`);
        }
        if ((sidecarEnabled?.checked === false ? "managed" : "sidecar") !== requestedBackend
            || formatSelect?.value !== requestedFormat
            || fontSelect?.value !== route.fontSourceId) {
            return null;
        }

        const data = response.status === 204 ? null : await response.clone().json();
        const assets = data?.assets ?? data?.Assets ?? [];
        const evidenceKey = `${route.fontSourceId}/${requestedBackend}/${requestedFormat}`;
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
        const sidecarEnabled = document.getElementById("sidecarEnabled");
        const formatSelect = document.getElementById("formatSelect");
        const fontSelect = document.getElementById("fontSelect");
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

        function renderPreview() {
            previewBox.textContent =
                `【宋體／楷體 Plus 共通 PUA ${commonPuaScalars.length} 字】\n`
                + `${commonPuaText}\n\n${rareInput.value}`;
        }

        async function updatePreview() {
            const currentVersion = ++selectionVersion;
            const selectedSource = fontSelect.value;
            const selectedFormat = formatSelect.value;
            const selectedBackend = sidecarEnabled.checked ? "sidecar" : "managed";
            const expectedFormat =
                selectedBackend === "managed" && selectedFormat === "Woff2"
                    ? "Woff"
                    : selectedFormat;
            const fontFamily = fontFamilies[selectedSource];
            const selectionStartedAt = performance.now();
            performance.clearResourceTimings();
            responseEvidence.delete(`${selectedSource}/${selectedBackend}/${selectedFormat}`);
            systemCoveredScalars = 0;
            document.documentElement.dataset.odfRequestedBasic = "false";
            document.documentElement.dataset.odfRequestedPua = "false";
            document.body.dataset.internationalReady = "pending";
            window.__odfKitInternationalProof = { loadedCases: [] };
            status.textContent =
                `正在產生 ${selectedBackend}／${expectedFormat} 動態 WebFont…`;
            previewBox.classList.remove(...previewClasses);
            previewBox.classList.add(`font-${selectedSource}`);
            renderPreview();
            controller?.disconnect();
            OdfKitWebFontAutoSubset.clearLoadedFaces();
            controller = OdfKitWebFontAutoSubset.createController({
                root: previewBox,
                routes: [{
                    fontSourceId: selectedSource,
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
                if (currentVersion !== selectionVersion) {
                    return;
                }
                const probeScalars = [
                    ...(selectedSource === "cns-sung-plus" ? [0xFFAE0] : []),
                    0xF04E1,
                    0xF0680,
                    0xF0800
                ];
                const rendered = (await Promise.all(probeScalars.map(scalar =>
                    OdfKitWebFontAutoSubset.verifyGlyphRendering(
                        fontFamily,
                        String.fromCodePoint(scalar))))).every(Boolean);
                const evidence = responseEvidence.get(
                    `${selectedSource}/${selectedBackend}/${selectedFormat}`);
                const correctAsset = evidence?.formats.length === 1
                    && evidence.formats[0] === expectedFormat
                    && evidence.fontFamilies.length === 1
                    && evidence.fontFamilies[0] === fontFamily;
                const ready = rendered && correctAsset;
                const fontResources = performance.getEntriesByType("resource")
                    .filter(entry => entry.name.includes("/_odf-fonts/")
                        && !entry.name.endsWith("/generate"));
                const fontTransferBytes = fontResources.reduce(
                    (sum, entry) => sum + (entry.encodedBodySize || entry.transferSize || 0),
                    0);
                const elapsedMilliseconds = performance.now() - selectionStartedAt;
                document.documentElement.dataset.odfSelectedFontSource = selectedSource;
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
                    ? `${selectedBackend}／${expectedFormat} 已載入，目標字形像素驗證通過`
                    : `${selectedBackend}／${expectedFormat} 字形驗證失敗`;
                window.__odfKitInternationalProof = {
                    loadedCases: ready ? [{
                        fontSourceId: selectedSource,
                        puaScalarCount: commonPuaScalars.length,
                        format: expectedFormat,
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

        sidecarEnabled.addEventListener("change", updatePreview);
        formatSelect.addEventListener("change", updatePreview);
        fontSelect.addEventListener("change", updatePreview);
        rareInput.addEventListener("input", updatePreview);
        updatePreview();
    });
}());
