// OCR helper for case documents (spec §24). When the user picks a JPG/PNG,
// Tesseract.js reads the text in the browser; the important lines (names,
// numbers, dates, references) are put in a box the user checks and corrects
// before uploading. Nothing is sent to an external AI service.
(function () {
    "use strict";
    if (typeof Tesseract === "undefined") return;

    function keyLines(text) {
        const lines = text.split(/\r?\n/).map(l => l.replace(/\s+/g, " ").trim()).filter(l => l.length >= 3);
        // Prefer lines with digits (reference / ID / date) or "label: value" pairs.
        const useful = lines.filter(l => /\d{3,}|:|\bno\b|\bref/i.test(l));
        const picked = (useful.length >= 2 ? useful : lines).slice(0, 15);
        return picked.join("\n").slice(0, 2000);
    }

    document.querySelectorAll(".doc-upload-form").forEach(function (form) {
        const input = form.querySelector(".doc-file");
        const box = form.querySelector(".doc-ocr");
        const status = form.querySelector(".doc-ocr-status");
        const text = form.querySelector(".doc-ocr-text");
        const check = form.querySelector(".doc-ocr-check");
        const submit = form.querySelector("button[type=submit]");
        if (!input || !box) return;

        // The details are only saved once the user ticks "I checked these details".
        function needCheck() { if (check) check.required = text.value.trim() !== ""; }
        text.addEventListener("input", needCheck);

        let run = 0; // only the latest file choice may fill the box
        input.addEventListener("change", async function () {
            const mine = ++run;
            const file = input.files && input.files[0];
            text.value = "";
            if (check) check.checked = false;
            needCheck();
            submit.disabled = false;
            if (!file || !/^image\/(png|jpe?g)$/i.test(file.type)) {
                box.classList.add("d-none");
                return;
            }
            box.classList.remove("d-none");
            status.textContent = "Reading the text in the image… (this stays on your computer)";
            submit.disabled = true;
            try {
                const result = await Tesseract.recognize(file, "eng");
                if (mine !== run) return; // another file was picked meanwhile
                const found = keyLines(result.data.text || "");
                text.value = found;
                needCheck();
                status.textContent = found
                    ? "Text found. Check every line, correct mistakes, then tick the box below."
                    : "No readable text was found. You can type the key details yourself, or leave this empty.";
            } catch (e) {
                if (mine !== run) return;
                status.textContent = "The text could not be read. You can still upload the file.";
            } finally {
                if (mine === run) submit.disabled = false;
            }
        });
    });
})();
