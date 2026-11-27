// Reads the holder's name from an ID card / passport photo with
// Tesseract.js (free, open source, runs entirely in the browser).
// 1) Machine Readable Zone (the <<< lines) when present: most reliable.
// 2) Otherwise labelled lines such as "Surname" / "Given names" / "Name".
// The user always reviews and can edit the filled fields.
(function () {
    const input = document.getElementById('idScan');
    if (!input) return;

    const status = document.getElementById('ocrStatus');
    const first = document.getElementById('Input_FirstName');
    const last = document.getElementById('Input_LastName');

    function say(text, cls) {
        status.className = 'small mt-2 ' + (cls || 'text-muted');
        status.textContent = text;
    }

    function titleCase(s) {
        return s.toLowerCase().replace(/(^|[\s'-])\p{L}/gu, m => m.toUpperCase()).trim();
    }

    function clean(s) {
        return (s || '').replace(/[^\p{L}\s'-]/gu, ' ').replace(/\s+/g, ' ').trim();
    }

    function fromMrz(lines) {
        const mrz = lines.map(l => l.replace(/\s/g, '').toUpperCase().replace(/[«‹]/g, '<'))
                         .filter(l => l.length >= 25 && (l.match(/</g) || []).length >= 3);
        for (const line of mrz) {
            // Passport (TD3): P<CCCSURNAME<<GIVEN<NAMES<<<
            let m = line.match(/^P[A-Z<][A-Z<]{3}([A-Z<]+)$/);
            let namePart = m ? m[1] : null;
            // ID card (TD1) 3rd line: SURNAME<<GIVEN<NAMES<<<
            if (!namePart && /^[A-Z]+<<[A-Z<]+$/.test(line)) namePart = line;
            if (!namePart || namePart.indexOf('<<') < 0) continue;

            const [sur, given] = namePart.split('<<');
            const surname = clean(sur.replace(/</g, ' '));
            const givenNames = clean((given || '').replace(/</g, ' '));
            if (surname.length >= 2 && givenNames.length >= 2)
                return { first: titleCase(givenNames), last: titleCase(surname) };
        }
        return null;
    }

    function fromLabels(lines) {
        const result = {};
        const labels = [
            { key: 'last', re: /^(surname|last\s*name|family\s*name|nom|nachname|apellidos?)\b[\s:./]*(.*)$/i },
            { key: 'first', re: /^(given\s*names?|first\s*names?|forenames?|prénoms?|prenoms?|vorname[n]?|nombres?)\b[\s:./]*(.*)$/i },
            { key: 'full', re: /^(name)\b[\s:./]*(.*)$/i }
        ];
        for (let i = 0; i < lines.length; i++) {
            const line = lines[i].trim();
            for (const { key, re } of labels) {
                const m = line.match(re);
                if (!m || result[key]) continue;
                // Value on the same line, or on the next line
                let value = clean(m[2]);
                if (value.length < 2 && i + 1 < lines.length) value = clean(lines[i + 1]);
                if (value.length >= 2) result[key] = titleCase(value);
            }
        }
        if (!result.first && !result.last && result.full) {
            const parts = result.full.split(' ');
            if (parts.length >= 2) { result.first = parts.slice(0, -1).join(' '); result.last = parts[parts.length - 1]; }
        }
        return (result.first || result.last) ? result : null;
    }

    input.addEventListener('change', async function () {
        const file = input.files && input.files[0];
        if (!file) return;
        if (!file.type.startsWith('image/')) { say('Please choose an image file.', 'text-danger'); return; }
        if (typeof Tesseract === 'undefined') { say('The text reader could not load. Please type your name.', 'text-danger'); return; }

        say('Reading the document… this can take a few seconds.');
        try {
            const { data } = await Tesseract.recognize(file, 'eng', {
                logger: m => { if (m.status === 'recognizing text') say('Reading the document… ' + Math.round(m.progress * 100) + '%'); }
            });
            const lines = (data.text || '').split(/\r?\n/).filter(l => l.trim().length);
            const found = fromMrz(lines) || fromLabels(lines);

            if (!found) {
                say('We could not read a name from this picture. Please type it below, or try a sharper photo.', 'text-warning');
                return;
            }
            if (found.first) first.value = found.first;
            if (found.last) last.value = found.last;
            [first, last].forEach(el => { el.classList.add('border-success'); el.dispatchEvent(new Event('change')); });
            say('Name filled in from your document. Please check it and correct it if needed.', 'text-success');
        } catch (e) {
            say('Reading failed. Please type your name below.', 'text-danger');
        } finally {
            input.value = ''; // drop the reference to the picture
        }
    });
})();
