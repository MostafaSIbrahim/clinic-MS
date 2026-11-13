// M4d: the items on the weekly follow-up form: what was given this week (ticked from the patient's plan or the
// package, plus anything added from the injection and vitamin lists or typed by name) and the plan for the
// coming weeks. Rows are built here from the server's lines so the first view, a refused save and a kept draft
// (form-draft.js) all use the same code. The server checks everything again.
(function (global) {
    'use strict';
    let data, text, givenIndex = 0, planIndex = 0;
    const esc = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
    const num = v => (v === null || v === undefined || v === '' ? '' : Number(v).toFixed(2));

    function lookup(line) {
        if (line.packageItemId) {
            const p = data.packageItems.find(x => x.packageItemId === Number(line.packageItemId));
            return { name: p ? p.name : '—', kind: p ? p.kind : '', unit: p ? p.unit : '', week: p ? p.weekNumber : null, outside: false };
        }
        if (line.injectionId) {
            const i = data.injections.find(x => x.id === Number(line.injectionId));
            return { name: i ? i.name : '—', kind: 'Injection', unit: i ? i.unit : '', outside: true };
        }
        if (line.vitaminId) {
            const v = data.vitamins.find(x => x.id === Number(line.vitaminId));
            return { name: v ? v.name : '—', kind: 'Vitamin', unit: v ? v.unit : '', outside: true };
        }
        return { name: '', kind: 'Other', unit: '', outside: true, custom: true };
    }

    function describe(info, line, week) {
        const parts = [text.kinds[String(info.kind).toLowerCase()] || ''];
        if (info.outside) parts.push(text.outside);
        else if (info.week && week && info.week !== week) parts.push(text.plannedWeek.replace('{0}', info.week));
        return parts.filter(Boolean).map(esc).join(' · ');
    }

    // One row. prefix: AdministeredItems or PlannedItems. index: its number in the form.
    function row(prefix, index, line, week, removable) {
        const info = lookup(line);
        const id = n => `${prefix === 'AdministeredItems' ? 'given' : 'plan'}-${index}-${n}`;
        const name = n => `${prefix}[${index}].${n}`;
        const hidden = (n, v) => `<input type="hidden" name="${name(n)}" value="${esc(v ?? '')}" data-draft-keep />`;
        const isGiven = prefix === 'AdministeredItems';
        const nameCell = info.custom
            ? `<label class="form-label small mb-1" for="${id('name')}">${esc(text.drugName)}</label>
               <input class="form-control form-control-sm" id="${id('name')}" name="${name('CustomName')}" value="${esc(line.customName)}" maxlength="200" dir="auto" required />
               <span class="text-muted small d-block">${describe(info, line, week)}</span>`
            : (isGiven
                ? `<div class="form-check"><input class="form-check-input" type="checkbox" id="${id('given')}" name="${name('Given')}" value="true" ${line.given ? 'checked' : ''} />
                   <label class="form-check-label" for="${id('given')}"><span dir="auto">${esc(info.name)}</span>
                   <span class="text-muted small d-block">${describe(info, line, week)}</span></label></div>`
                : `<div><span dir="auto">${esc(info.name)}</span><span class="text-muted small d-block">${describe(info, line, week)}</span></div>`)
              + hidden('CustomName', '');
        const givenTick = info.custom && isGiven
            ? `<div class="form-check mt-1"><input class="form-check-input" type="checkbox" id="${id('given')}" name="${name('Given')}" value="true" ${line.given ? 'checked' : ''} />
               <label class="form-check-label small" for="${id('given')}">${esc(text.givenLabel)}</label></div>` : '';
        const html = `
            <div class="followup-item" data-prefix="${prefix}" data-index="${index}">
                <div>${nameCell}${givenTick}
                    ${hidden('PackageItemId', line.packageItemId)}${hidden('InjectionId', line.injectionId)}${hidden('VitaminId', line.vitaminId)}
                    ${isGiven ? '' : hidden('WeekNumber', week)}</div>
                <div><label class="form-label small mb-1" for="${id('qty')}">${esc(isGiven ? text.quantityGiven : text.quantity)}</label>
                    <input type="number" step="0.01" min="0" class="form-control form-control-sm" id="${id('qty')}" name="${name(isGiven ? 'ActualQuantity' : 'Quantity')}" value="${num(line.quantity)}" /></div>
                <div><label class="form-label small mb-1" for="${id('unit')}">${esc(text.unit)}</label>
                    <input class="form-control form-control-sm" id="${id('unit')}" name="${name('Unit')}" value="${esc(line.unit ?? info.unit)}" maxlength="50" /></div>
                <div><label class="form-label small mb-1" for="${id('notes')}">${esc(text.notes)}</label>
                    <input class="form-control form-control-sm" id="${id('notes')}" name="${name('Notes')}" value="${esc(line.notes)}" dir="auto" /></div>
                <div class="followup-item__remove">${removable
                    ? `<button type="button" class="btn btn-sm btn-link text-danger" data-remove-line><i class="bi bi-trash" aria-hidden="true"></i><span class="visually-hidden">${esc(text.remove)} ${esc(info.name || text.drugName)}</span></button>`
                    : ''}</div>
            </div>`;
        return html;
    }

    function addGiven(line, removable, index) {
        const i = index ?? givenIndex;
        document.getElementById('given-rows').insertAdjacentHTML('beforeend', row('AdministeredItems', i, line, data.week, removable));
        givenIndex = Math.max(givenIndex, i + 1);
    }

    function addPlanned(line, index) {
        const i = index ?? planIndex;
        const block = document.querySelector(`#plan-weeks [data-week="${line.weekNumber}"] .plan-rows`);
        if (!block) return;
        block.insertAdjacentHTML('beforeend', row('PlannedItems', i, line, Number(line.weekNumber), true));
        planIndex = Math.max(planIndex, i + 1);
    }

    // The server reads [0], [1]… and stops at a gap, so rows are renumbered (in page order) after a removal.
    function renumber(prefix) {
        const rows = document.querySelectorAll(`.followup-item[data-prefix="${prefix}"]`);
        const short = prefix === 'AdministeredItems' ? 'given' : 'plan';
        const pattern = new RegExp('^' + prefix + '\\[\\d+\\]');
        rows.forEach(function (r, n) {
            r.dataset.index = n;
            r.querySelectorAll('[name], [id], label[for]').forEach(function (el) {
                if (el.name) el.name = el.name.replace(pattern, prefix + '[' + n + ']');
                if (el.id) el.id = el.id.replace(new RegExp('^' + short + '-\\d+-'), short + '-' + n + '-');
                if (el.htmlFor) el.htmlFor = el.htmlFor.replace(new RegExp('^' + short + '-\\d+-'), short + '-' + n + '-');
            });
        });
        if (prefix === 'AdministeredItems') givenIndex = rows.length; else planIndex = rows.length;
    }

    function chooser(id, label) {
        const group = (title, list, kind) => list.length
            ? `<optgroup label="${esc(title)}">${list.map(x => `<option value="${kind}:${x.id}">${esc(x.name)}</option>`).join('')}</optgroup>` : '';
        return `<div class="followup-adder">
                <label class="form-label small mb-1" for="${id}">${esc(label)}</label>
                <div class="d-flex gap-2">
                    <select class="form-select form-select-sm" id="${id}" data-draft-ignore>
                        <option value="">${esc(text.choose)}</option>
                        ${group(text.kinds.injection, data.injections, 'inj')}${group(text.kinds.vitamin, data.vitamins, 'vit')}
                        <option value="other">${esc(text.otherDrug)}</option>
                    </select>
                    <button type="button" class="btn btn-sm btn-outline-secondary" data-add-for="${id}">${esc(text.add)}</button>
                </div></div>`;
    }

    function fromChoice(value) {
        const [kind, id] = value.split(':');
        const line = { quantity: 1, given: true };
        if (kind === 'inj') line.injectionId = Number(id);
        else if (kind === 'vit') line.vitaminId = Number(id);
        else line.customName = '';
        return line;
    }

    function init(formData, labels) {
        data = formData; text = labels;
        const given = document.getElementById('given-rows');
        data.lines.forEach(l => addGiven(l, l.outsidePackage));
        given.insertAdjacentHTML('afterend', chooser('add-given', text.addThisWeek));

        const weeks = document.getElementById('plan-weeks');
        if (weeks) {
            data.planWeeks.forEach(function (w) {
                weeks.insertAdjacentHTML('beforeend', `
                    <details class="plan-week" data-week="${w}" ${w === data.planWeeks[0] ? 'open' : ''}>
                        <summary>${esc(text.week.replace('{0}', w))}</summary>
                        <div class="plan-rows"></div>
                        ${chooser('add-plan-' + w, text.addToWeek.replace('{0}', w))}
                    </details>`);
            });
            data.plan.forEach(l => addPlanned(l));
        }

        document.addEventListener('click', function (e) {
            const add = e.target.closest('[data-add-for]');
            if (add) {
                const select = document.getElementById(add.dataset.addFor);
                if (!select.value) { select.focus(); return; }
                const line = fromChoice(select.value);
                if (add.dataset.addFor === 'add-given') addGiven(line, true);
                else { line.weekNumber = Number(add.dataset.addFor.replace('add-plan-', '')); addPlanned(line); }
                select.value = '';
                const form = select.closest('form');
                if (form) form.dispatchEvent(new Event('input', { bubbles: true }));
                return;
            }
            const remove = e.target.closest('[data-remove-line]');
            if (remove) {
                const r = remove.closest('.followup-item');
                const prefix = r.dataset.prefix;
                r.remove();
                renumber(prefix);
                const form = document.querySelector('form[data-draft-key]');
                if (form) form.dispatchEvent(new Event('input', { bubbles: true }));
            }
        });
    }

    // form-draft.js: before kept values are put back, rebuild the rows they belong to. Added rows of this week
    // are recreated at their own numbers; the plan is rebuilt entirely from the kept values.
    function prepare(names, values) {
        const lineFrom = (prefix, i) => ({
            packageItemId: values[`${prefix}[${i}].PackageItemId`] || null,
            injectionId: values[`${prefix}[${i}].InjectionId`] || null,
            vitaminId: values[`${prefix}[${i}].VitaminId`] || null,
            customName: values[`${prefix}[${i}].CustomName`] || null,
            weekNumber: values[`${prefix}[${i}].WeekNumber`],
            given: values[`${prefix}[${i}].Given`] === 'true',
        });
        const indices = prefix => [...new Set(names.map(n => (new RegExp('^' + prefix + '\\[(\\d+)\\]\\.')).exec(n))
            .filter(Boolean).map(m => Number(m[1])))].sort((a, b) => a - b);

        const existing = document.querySelectorAll('.followup-item[data-prefix="AdministeredItems"]').length;
        indices('AdministeredItems').filter(i => i >= existing).forEach(i => addGiven(lineFrom('AdministeredItems', i), true, i));

        const planned = indices('PlannedItems');
        if (planned.length && document.getElementById('plan-weeks')) {
            document.querySelectorAll('#plan-weeks .plan-rows').forEach(b => { b.innerHTML = ''; });
            planIndex = 0;
            planned.forEach(i => addPlanned(lineFrom('PlannedItems', i), i));
        }
    }

    global.FollowUpItems = { init: init, prepare: prepare };
})(typeof window !== 'undefined' ? window : globalThis);
