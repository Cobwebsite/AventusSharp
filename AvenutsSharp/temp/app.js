// Application State
let state = {
    oldSchema: null,
    newSchema: null,
    comparison: null,
    mappings: {
        tables: {}, // "OldTableName": "NewTableName"
        fields: {}  // "TableName": { "OldFieldName": "NewFieldName" } - Note: TableName refers to the NEW table name if renamed
    },
    generatedCode: ""
};

// Tabs Navigation
function switchTab(tabId) {
    // Update nav button class
    document.querySelectorAll('.nav-btn').forEach(btn => {
        btn.classList.remove('active');
    });
    const activeBtn = document.getElementById(`tab-${tabId}`);
    if (activeBtn) activeBtn.classList.add('active');

    // Update section class
    document.querySelectorAll('.content-section').forEach(sec => {
        sec.classList.remove('active');
    });
    const activeSec = document.getElementById(`sec-${tabId}`);
    if (activeSec) activeSec.classList.add('active');
}

// Drag & Drop / File Select Handlers
function handleDragOver(e, zoneId) {
    e.preventDefault();
    document.getElementById(`drag-${zoneId}`).classList.add('dragover');
}

function handleDragLeave(e, zoneId) {
    e.preventDefault();
    document.getElementById(`drag-${zoneId}`).classList.remove('dragover');
}

function handleDrop(e, zoneId) {
    e.preventDefault();
    document.getElementById(`drag-${zoneId}`).classList.remove('dragover');

    if (e.dataTransfer.files.length > 0) {
        const file = e.dataTransfer.files[0];
        readFileContent(file, zoneId);
    }
}

function handleFileSelect(e, zoneId) {
    if (e.target.files.length > 0) {
        const file = e.target.files[0];
        readFileContent(file, zoneId);
    }
}

function readFileContent(file, zoneId) {
    document.getElementById(`file-name-${zoneId}`).textContent = file.name;
    const reader = new FileReader();
    reader.onload = function (e) {
        document.getElementById(`json-${zoneId}`).value = e.target.result;
    };
    reader.readAsText(file);
}

// Load Demo Mockups
function loadDemoData() {
    const demoOld = {
        "name": "DemoSchemaA",
        "databaseType": "sqlite",
        "tables": [
            {
                "id": "t1",
                "name": "Club",
                "fields": [
                    { "id": "f1_1", "name": "Id", "type": { "id": "int", "name": "int" }, "primaryKey": true },
                    { "id": "f1_2", "name": "Name", "type": { "id": "varchar", "name": "varchar(255)" } },
                    { "id": "f1_3", "name": "Logo", "type": { "id": "varchar", "name": "varchar(255)" }, "nullable": true },
                    { "id": "f1_4", "name": "CreatedDate", "type": { "id": "datetime", "name": "datetime" } },
                    { "id": "f1_5", "name": "UpdatedDate", "type": { "id": "datetime", "name": "datetime" } }
                ]
            },
            {
                "id": "t2",
                "name": "Role",
                "fields": [
                    { "id": "f2_1", "name": "Id", "type": { "id": "int", "name": "int" }, "primaryKey": true },
                    { "id": "f2_2", "name": "Name", "type": { "id": "varchar", "name": "varchar(255)" } },
                    { "id": "f2_3", "name": "ClubId", "type": { "id": "int", "name": "int" } },
                    { "id": "f2_4", "name": "CreatedDate", "type": { "id": "datetime", "name": "datetime" } },
                    { "id": "f2_5", "name": "UpdatedDate", "type": { "id": "datetime", "name": "datetime" } }
                ]
            }
        ],
        "relationships": [
            {
                "id": "r1",
                "name": "Role_Club",
                "sourceTableId": "t2",
                "sourceFieldId": "f2_3",
                "targetTableId": "t1",
                "targetFieldId": "f1_1"
            }
        ]
    };

    const demoNew = {
        "name": "DemoSchemaB",
        "databaseType": "sqlite",
        "tables": [
            {
                "id": "t1",
                "name": "Club",
                "fields": [
                    { "id": "f1_1", "name": "Id", "type": { "id": "int", "name": "int" }, "primaryKey": true },
                    { "id": "f1_2_new", "name": "Nom", "type": { "id": "varchar", "name": "varchar(255)" } },
                    { "id": "f1_3", "name": "Logo", "type": { "id": "varchar", "name": "varchar(255)" }, "nullable": true },
                    { "id": "f1_4", "name": "CreatedDate", "type": { "id": "datetime", "name": "datetime" } },
                    { "id": "f1_5", "name": "UpdatedDate", "type": { "id": "datetime", "name": "datetime" } }
                ]
            },
            {
                "id": "t2",
                "name": "Role",
                "fields": [
                    { "id": "f2_1", "name": "Id", "type": { "id": "int", "name": "int" }, "primaryKey": true },
                    { "id": "f2_2", "name": "Name", "type": { "id": "varchar", "name": "varchar(255)" } },
                    { "id": "f2_3", "name": "ClubId", "type": { "id": "int", "name": "int" } },
                    { "id": "f2_4", "name": "CreatedDate", "type": { "id": "datetime", "name": "datetime" } },
                    { "id": "f2_5", "name": "UpdatedDate", "type": { "id": "datetime", "name": "datetime" } }
                ]
            },
            {
                "id": "t3",
                "name": "User",
                "fields": [
                    { "id": "f3_1", "name": "Id", "type": { "id": "int", "name": "int" }, "primaryKey": true },
                    { "id": "f3_2", "name": "Email", "type": { "id": "varchar", "name": "varchar(255)" }, "unique": true },
                    { "id": "f3_3", "name": "Password", "type": { "id": "varchar", "name": "varchar(255)" } },
                    { "id": "f3_4", "name": "RoleId", "type": { "id": "int", "name": "int" } },
                    { "id": "f3_5", "name": "CreatedDate", "type": { "id": "datetime", "name": "datetime" } },
                    { "id": "f3_6", "name": "UpdatedDate", "type": { "id": "datetime", "name": "datetime" } }
                ]
            }
        ],
        "relationships": [
            {
                "id": "r1",
                "name": "Role_Club",
                "sourceTableId": "t2",
                "sourceFieldId": "f2_3",
                "targetTableId": "t1",
                "targetFieldId": "f1_1"
            },
            {
                "id": "r2",
                "name": "User_Role",
                "sourceTableId": "t3",
                "sourceFieldId": "f3_4",
                "targetTableId": "t2",
                "targetFieldId": "f2_1"
            }
        ]
    };

    document.getElementById('json-old').value = JSON.stringify(demoOld, null, 2);
    document.getElementById('json-new').value = JSON.stringify(demoNew, null, 2);
    document.getElementById('file-name-old').textContent = "demo_schema_old.json";
    document.getElementById('file-name-new').textContent = "demo_schema_new.json";

    showToast("Données de démo chargées !");
}

// Toast System
function showToast(message) {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.classList.add('show');
    setTimeout(() => {
        toast.classList.remove('show');
    }, 2500);
}

// Compare and Analyze Schemas
function analyzeAndCompare() {
    const oldStr = document.getElementById('json-old').value.trim();
    const newStr = document.getElementById('json-new').value.trim();

    if (!oldStr || !newStr) {
        alert("Veuillez remplir les deux zones de texte C# avec des schémas valides.");
        return;
    }

    try {
        state.oldSchema = JSON.parse(oldStr);
    } catch (e) {
        alert("Erreur de parsing JSON dans l'ancien schéma : " + e.message);
        return;
    }

    try {
        state.newSchema = JSON.parse(newStr);
    } catch (e) {
        alert("Erreur de parsing JSON dans le nouveau schéma : " + e.message);
        return;
    }

    // Run comparison and update screen
    runSchemaComparison();
    switchTab('diff');
}

function runSchemaComparison() {
    state.comparison = compareSchemas(state.oldSchema, state.newSchema, state.mappings);
    renderDiffResolutionUI();
}

// Core Comparison Engine (JavaScript equivalent of backend logic)
function compareSchemas(oldSchema, newSchema, mappings) {
    const oldTables = oldSchema.tables || [];
    const newTables = newSchema.tables || [];

    const oldTablesMap = new Map(oldTables.map(t => [t.name, t]));
    const newTablesMap = new Map(newTables.map(t => [t.name, t]));

    const mappedOldTables = new Set(Object.keys(mappings.tables));
    const mappedNewTables = new Set(Object.values(mappings.tables));

    const addedTables = [];
    const deletedTables = [];
    const renamedTables = []; // array of { old, new, oldName, newName }
    const commonTables = [];  // array of { old, new, name }

    // Added tables
    for (const t of newTables) {
        if (!oldTablesMap.has(t.name) && !mappedNewTables.has(t.name)) {
            addedTables.push(t);
        }
    }

    // Deleted tables
    for (const t of oldTables) {
        if (!newTablesMap.has(t.name) && !mappedOldTables.has(t.name)) {
            deletedTables.push(t);
        }
    }

    // Reworked Table Mappings
    for (const [oldName, newName] of Object.entries(mappings.tables)) {
        const oldT = oldTablesMap.get(oldName);
        const newT = newTablesMap.get(newName);
        if (oldT && newT) {
            renamedTables.push({ old: oldT, new: newT, oldName, newName });
        }
    }

    // Common tables (with same name)
    for (const t of newTables) {
        if (oldTablesMap.has(t.name)) {
            commonTables.push({ old: oldTablesMap.get(t.name), new: t, name: t.name });
        }
    }

    // Compare fields
    const tableComparisons = [];

    const processFields = (oldTable, newTable, isRename, oldTableName, newTableName) => {
        const oldFields = oldTable.fields || [];
        const newFields = newTable.fields || [];

        const oldFieldsMap = new Map(oldFields.map(f => [f.name, f]));
        const newFieldsMap = new Map(newFields.map(f => [f.name, f]));

        const fieldMappings = mappings.fields[newTableName] || {}; // Key is newTableName
        const mappedOldFields = new Set(Object.keys(fieldMappings));
        const mappedNewFields = new Set(Object.values(fieldMappings));

        const addedFields = [];
        const deletedFields = [];
        const renamedFields = []; // { old, new, oldName, newName }
        const commonFields = [];
        const modifiedFields = []; // { old, new, name, changes }

        for (const f of newFields) {
            if (!oldFieldsMap.has(f.name) && !mappedNewFields.has(f.name)) {
                addedFields.push(f);
            }
        }

        for (const f of oldFields) {
            if (!newFieldsMap.has(f.name) && !mappedOldFields.has(f.name)) {
                deletedFields.push(f);
            }
        }

        for (const [oldFName, newFName] of Object.entries(fieldMappings)) {
            const oldF = oldFieldsMap.get(oldFName);
            const newF = newFieldsMap.get(newFName);
            if (oldF && newF) {
                renamedFields.push({ old: oldF, new: newF, oldName: oldFName, newName: newFName });
            }
        }

        for (const f of newFields) {
            if (oldFieldsMap.has(f.name)) {
                commonFields.push({ old: oldFieldsMap.get(f.name), new: f, name: f.name });
            }
        }

        const checkChanges = (oldF, newF, name) => {
            const changes = {};
            if (oldF.nullable !== newF.nullable) {
                changes.nullable = { old: oldF.nullable, new: newF.nullable };
            }
            if (oldF.unique !== newF.unique) {
                changes.unique = { old: oldF.unique, new: newF.unique };
            }
            if (oldF.type.name !== newF.type.name) {
                changes.type = { old: oldF.type.name, new: newF.type.name };
            }
            if (Object.keys(changes).length > 0) {
                modifiedFields.push({ old: oldF, new: newF, name, changes });
            }
        };

        for (const cf of commonFields) {
            checkChanges(cf.old, cf.new, cf.name);
        }
        for (const rf of renamedFields) {
            checkChanges(rf.old, rf.new, rf.newName);
        }

        return {
            oldTableName,
            newTableName,
            isRename,
            addedFields,
            deletedFields,
            renamedFields,
            modifiedFields,
            hasChanges: addedFields.length > 0 || deletedFields.length > 0 || renamedFields.length > 0 || modifiedFields.length > 0
        };
    };

    for (const ct of commonTables) {
        tableComparisons.push(processFields(ct.old, ct.new, false, ct.name, ct.name));
    }
    for (const rt of renamedTables) {
        tableComparisons.push(processFields(rt.old, rt.new, true, rt.oldName, rt.newName));
    }

    return {
        addedTables,
        deletedTables,
        renamedTables,
        tableComparisons
    };
}

// Render Comparison Screen
function renderDiffResolutionUI() {
    const comp = state.comparison;

    // 1. Populate Table Dropdowns
    const oldTableSelect = document.getElementById('map-select-old-table');
    const newTableSelect = document.getElementById('map-select-new-table');

    oldTableSelect.innerHTML = '<option value="">-- Table Supprimée --</option>';
    newTableSelect.innerHTML = '<option value="">-- Table Ajoutée --</option>';

    comp.deletedTables.forEach(t => {
        oldTableSelect.innerHTML += `<option value="${t.name}">${t.name}</option>`;
    });
    comp.addedTables.forEach(t => {
        newTableSelect.innerHTML += `<option value="${t.name}">${t.name}</option>`;
    });

    // 2. Render Active Table Mappings
    const tableMappingList = document.getElementById('table-mapping-list');
    tableMappingList.innerHTML = '';

    if (Object.keys(state.mappings.tables).length === 0) {
        tableMappingList.innerHTML = '<p class="text-muted" style="font-size:0.8rem; font-style:italic;">Aucune association de table.</p>';
    } else {
        for (const [oldN, newN] of Object.entries(state.mappings.tables)) {
            tableMappingList.innerHTML += `
                <div class="mapping-item">
                    <span class="mapping-names">
                        <span>${oldN}</span>
                        <span class="mapping-arrow">➔</span>
                        <span>${newN}</span>
                    </span>
                    <button class="mapping-delete" onclick="removeTableMapping('${oldN}')">✕</button>
                </div>
            `;
        }
    }

    // 3. Populate Table Selector for Field Mapping
    const mappingTableSelect = document.getElementById('mapping-table-select');
    const currentSelectedVal = mappingTableSelect.value;

    mappingTableSelect.innerHTML = '<option value="">-- Choisir une table --</option>';

    // Options are unchanged or renamed tables
    comp.tableComparisons.forEach(tc => {
        const displayName = tc.isRename ? `${tc.oldTableName} ➔ ${tc.newTableName}` : tc.newTableName;
        mappingTableSelect.innerHTML += `<option value="${tc.newTableName}">${displayName}</option>`;
    });

    if (currentSelectedVal && Array.from(mappingTableSelect.options).some(o => o.value === currentSelectedVal)) {
        mappingTableSelect.value = currentSelectedVal;
        onMappingTableChanged();
    } else {
        document.getElementById('field-mapping-adder').style.display = 'none';
        document.getElementById('field-mapping-list').innerHTML = '<p class="text-muted" style="font-size:0.8rem; font-style:italic;">Sélectionnez une table ci-dessus.</p>';
    }

    // 4. Render Modifications Preview
    renderModificationsPreview();
}

function onMappingTableChanged() {
    const newTableName = document.getElementById('mapping-table-select').value;
    const fieldMappingList = document.getElementById('field-mapping-list');
    const fieldAdder = document.getElementById('field-mapping-adder');

    if (!newTableName) {
        fieldMappingList.innerHTML = '<p class="text-muted" style="font-size:0.8rem; font-style:italic;">Sélectionnez une table ci-dessus.</p>';
        fieldAdder.style.display = 'none';
        return;
    }

    const tc = state.comparison.tableComparisons.find(t => t.newTableName === newTableName);
    if (!tc) return;

    // Populate Field Dropdowns
    const oldFieldSelect = document.getElementById('map-select-old-field');
    const newFieldSelect = document.getElementById('map-select-new-field');

    oldFieldSelect.innerHTML = '<option value="">-- Champ Supprimé --</option>';
    newFieldSelect.innerHTML = '<option value="">-- Champ Ajouté --</option>';

    tc.deletedFields.forEach(f => {
        oldFieldSelect.innerHTML += `<option value="${f.name}">${f.name}</option>`;
    });
    tc.addedFields.forEach(f => {
        newFieldSelect.innerHTML += `<option value="${f.name}">${f.name}</option>`;
    });

    fieldAdder.style.display = 'flex';

    // Render Active Mappings
    fieldMappingList.innerHTML = '';
    const tableFieldMappings = state.mappings.fields[newTableName] || {};

    if (Object.keys(tableFieldMappings).length === 0) {
        fieldMappingList.innerHTML = '<p class="text-muted" style="font-size:0.8rem; font-style:italic;">Aucune association pour cette table.</p>';
    } else {
        for (const [oldF, newF] of Object.entries(tableFieldMappings)) {
            fieldMappingList.innerHTML += `
                <div class="mapping-item">
                    <span class="mapping-names">
                        <span>${oldF}</span>
                        <span class="mapping-arrow">➔</span>
                        <span>${newF}</span>
                    </span>
                    <button class="mapping-delete" onclick="removeFieldMapping('${newTableName}', '${oldF}')">✕</button>
                </div>
            `;
        }
    }
}

// Render modifications preview cards
function renderModificationsPreview() {
    const comp = state.comparison;
    const container = document.getElementById('diff-results');
    container.innerHTML = '';

    let hasDifs = comp.addedTables.length > 0 || comp.deletedTables.length > 0 || comp.renamedTables.length > 0;

    comp.tableComparisons.forEach(tc => {
        if (tc.hasChanges) hasDifs = true;
    });

    if (!hasDifs) {
        container.innerHTML = `
            <div class="empty-state">
                <span class="empty-icon">✨</span>
                <p>Aucune différence structurelle détectée entre les deux schémas.</p>
            </div>
        `;
        return;
    }

    // Added Tables
    comp.addedTables.forEach(t => {
        let fieldsHtml = (t.fields || []).map(f => `
            <div class="diff-item">
                <span class="diff-tag diff-tag-add">+</span>
                <span>${f.name}</span>
                <span class="diff-item-detail">(${f.type.name}${f.nullable ? ', null' : ''}${f.primaryKey ? ', PK' : ''})</span>
            </div>
        `).join('');

        container.innerHTML += `
            <div class="diff-table-group" style="border-color: rgba(16, 185, 129, 0.4)">
                <div class="diff-table-header" style="background: rgba(16, 185, 129, 0.05)">
                    <span class="diff-table-title" style="color: var(--color-success)">Table Ajoutée : ${t.name}</span>
                    <span class="badge badge-new">Nouveau</span>
                </div>
                <div class="diff-table-body">${fieldsHtml}</div>
            </div>
        `;
    });

    // Deleted Tables
    comp.deletedTables.forEach(t => {
        let fieldsHtml = (t.fields || []).map(f => `
            <div class="diff-item">
                <span class="diff-tag diff-tag-del">-</span>
                <span class="diff-change-old">${f.name}</span>
            </div>
        `).join('');

        container.innerHTML += `
            <div class="diff-table-group" style="border-color: rgba(239, 68, 68, 0.4)">
                <div class="diff-table-header" style="background: rgba(239, 68, 68, 0.05)">
                    <span class="diff-table-title" style="color: var(--color-danger)">Table Supprimée : ${t.name}</span>
                    <span class="badge badge-old">Supprimé</span>
                </div>
                <div class="diff-table-body">${fieldsHtml}</div>
            </div>
        `;
    });

    // Table Comparisons for modifications and renames
    comp.tableComparisons.forEach(tc => {
        if (!tc.hasChanges) return;

        let headerTitle = "";
        let headerColor = "var(--text-primary)";
        let borderGlow = "var(--border-color)";
        let badgeHtml = "";

        if (tc.isRename) {
            headerTitle = `Table Renommée : ${tc.oldTableName} ➔ ${tc.newTableName}`;
            headerColor = "var(--accent-indigo)";
            borderGlow = "rgba(99, 102, 241, 0.4)";
            badgeHtml = `<span class="badge badge-new" style="background:rgba(99, 102, 241, 0.15); color: var(--accent-indigo); border-color: rgba(99, 102, 241, 0.3)">Renommé</span>`;
        } else {
            headerTitle = `Table Modifiée : ${tc.newTableName}`;
            headerColor = "var(--color-warning)";
            borderGlow = "rgba(245, 158, 11, 0.4)";
            badgeHtml = `<span class="badge" style="background:rgba(245, 158, 11, 0.15); color: var(--color-warning); border-color: rgba(245, 158, 11, 0.3)">Modifié</span>`;
        }

        let detailsHtml = [];

        // Renamed Fields
        tc.renamedFields.forEach(f => {
            detailsHtml.push(`
                <div class="diff-item">
                    <span class="diff-tag diff-tag-ren">Renommé</span>
                    <span><span class="diff-change-old">${f.oldName}</span> ➔ <span class="diff-change-new">${f.newName}</span></span>
                </div>
            `);
        });

        // Deleted Fields
        tc.deletedFields.forEach(f => {
            detailsHtml.push(`
                <div class="diff-item">
                    <span class="diff-tag diff-tag-del">Retiré</span>
                    <span class="diff-change-old">${f.name}</span>
                </div>
            `);
        });

        // Added Fields
        tc.addedFields.forEach(f => {
            detailsHtml.push(`
                <div class="diff-item">
                    <span class="diff-tag diff-tag-add">Ajouté</span>
                    <span class="diff-change-new">${f.name}</span>
                    <span class="diff-item-detail">(${f.type.name}${f.nullable ? ', null' : ''})</span>
                </div>
            `);
        });

        // Modified Fields
        tc.modifiedFields.forEach(f => {
            let changesDesc = [];
            if (f.changes.type) {
                changesDesc.push(`type: <span class="diff-change-old">${f.changes.type.old}</span> ➔ <span class="diff-change-new">${f.changes.type.new}</span>`);
            }
            if (f.changes.nullable) {
                changesDesc.push(`nullable: <span class="diff-change-old">${f.changes.nullable.old}</span> ➔ <span class="diff-change-new">${f.changes.nullable.new}</span>`);
            }
            if (f.changes.unique) {
                changesDesc.push(`unique: <span class="diff-change-old">${f.changes.unique.old}</span> ➔ <span class="diff-change-new">${f.changes.unique.new}</span>`);
            }

            detailsHtml.push(`
                <div class="diff-item">
                    <span class="diff-tag diff-tag-mod">Modifié</span>
                    <span>${f.name}</span>
                    <span class="diff-item-detail">(${changesDesc.join(', ')})</span>
                </div>
            `);
        });

        container.innerHTML += `
            <div class="diff-table-group" style="border-color: ${borderGlow}">
                <div class="diff-table-header" style="background: rgba(255, 255, 255, 0.01)">
                    <span class="diff-table-title" style="color: ${headerColor}">${headerTitle}</span>
                    ${badgeHtml}
                </div>
                <div class="diff-table-body">${detailsHtml.join('')}</div>
            </div>
        `;
    });
}

// Add/Remove Table Mappings
function addTableMapping() {
    const oldT = document.getElementById('map-select-old-table').value;
    const newT = document.getElementById('map-select-new-table').value;

    if (!oldT || !newT) {
        alert("Veuillez sélectionner une table supprimée et une table ajoutée à associer.");
        return;
    }

    state.mappings.tables[oldT] = newT;

    // Run comparison and update UI
    runSchemaComparison();
    showToast(`Table ${oldT} associée à ${newT} !`);
}

function removeTableMapping(oldT) {
    const newT = state.mappings.tables[oldT];
    delete state.mappings.tables[oldT];

    // Clean field mappings for this table too
    if (state.mappings.fields[newT]) {
        delete state.mappings.fields[newT];
    }

    // Run comparison and update UI
    runSchemaComparison();
    showToast(`Association de la table ${oldT} supprimée.`);
}

// Add/Remove Field Mappings
function addFieldMapping() {
    const newTableName = document.getElementById('mapping-table-select').value;
    const oldF = document.getElementById('map-select-old-field').value;
    const newF = document.getElementById('map-select-new-field').value;

    if (!newTableName || !oldF || !newF) {
        alert("Veuillez sélectionner un champ supprimé et un champ ajouté à associer.");
        return;
    }

    if (!state.mappings.fields[newTableName]) {
        state.mappings.fields[newTableName] = {};
    }

    state.mappings.fields[newTableName][oldF] = newF;

    // Run comparison and update UI
    runSchemaComparison();
    showToast(`Champ ${oldF} associé à ${newF} dans la table ${newTableName} !`);
}

function removeFieldMapping(newTableName, oldF) {
    if (state.mappings.fields[newTableName]) {
        delete state.mappings.fields[newTableName][oldF];
        if (Object.keys(state.mappings.fields[newTableName]).length === 0) {
            delete state.mappings.fields[newTableName];
        }
    }

    // Run comparison and update UI
    runSchemaComparison();
    showToast(`Association du champ ${oldF} supprimée.`);
}

// Migration Generation Step
function prepareMigrationGeneration() {
    if (!state.comparison) {
        alert("Veuillez d'abord comparer deux schémas valides.");
        return;
    }

    // Calculate Stats
    const statsCreated = state.comparison.addedTables.length;
    const statsRenamed = state.comparison.renamedTables.length;
    const statsDeleted = state.comparison.deletedTables.length;

    let statsModified = 0;
    state.comparison.tableComparisons.forEach(tc => {
        if (!tc.isRename && tc.hasChanges) statsModified++;
    });

    document.getElementById('stat-created-tables').textContent = statsCreated;
    document.getElementById('stat-renamed-tables').textContent = statsRenamed;
    document.getElementById('stat-deleted-tables').textContent = statsDeleted;
    document.getElementById('stat-modified-tables').textContent = statsModified;

    // Trigger update of generated code
    updateGeneratedCode();
    switchTab('output');
}

function updateGeneratedCode() {
    const migrationName = document.getElementById('migration-name').value.trim() || "0001_update";
    const className = document.getElementById('class-name').value.trim() || "Migration_0001_update";

    state.generatedCode = generateMigrationCode(
        state.comparison,
        migrationName,
        className,
        state.oldSchema,
        state.newSchema,
        state.mappings
    );

    document.getElementById('csharp-output').textContent = state.generatedCode;
}

// Code Copying
function copyCodeToClipboard() {
    navigator.clipboard.writeText(state.generatedCode).then(() => {
        showToast("Code copié dans le presse-papiers !");
    }).catch(err => {
        alert("Erreur lors de la copie : " + err);
    });
}

// Code Downloading
function downloadCodeFile() {
    const className = document.getElementById('class-name').value.trim() || "Migration_0001_update";
    const blob = new Blob([state.generatedCode], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = `${className}.cs`;
    document.body.appendChild(a);
    a.click();

    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

// C# Code Generator (JavaScript client-side port)
function generateMigrationCode(comp, migrationName, className, oldSchema, newSchema, mappings) {
    let sb = [];
    sb.push("using AventusSharp.Data.Migrations;");
    sb.push("using AventusSharp.Data.Attributes;");
    sb.push("using System;");
    sb.push("");
    sb.push(`public class ${className} : Migration`);
    sb.push("{");
    sb.push("    public override string GetName()");
    sb.push("    {");
    sb.push(`        return "${migrationName}";`);
    sb.push("    }");
    sb.push("");
    sb.push("    public override void Up()");
    sb.push("    {");

    // 1. Rename tables
    for (const rt of comp.renamedTables) {
        sb.push(`        RenameModel<${rt.newName}>("${rt.oldName}");`);
    }
    if (comp.renamedTables.length > 0) {
        sb.push("");
    }

    // 2. Create added tables
    for (const table of comp.addedTables) {
        const chainLines = [];
        chainLines.push(`CreateModel<${table.name}>()`);

        const fields = table.fields || [];
        const hasCreatedDate = fields.some(f => f.name.toLowerCase() === "createddate" || f.name.toLowerCase() === "created_at");
        const hasUpdatedDate = fields.some(f => f.name.toLowerCase() === "updateddate" || f.name.toLowerCase() === "updated_at");
        const hasTimestamps = hasCreatedDate && hasUpdatedDate;

        for (const field of fields) {
            if (hasTimestamps && (field.name.toLowerCase() === "createddate" || field.name.toLowerCase() === "created_at" || field.name.toLowerCase() === "updateddate" || field.name.toLowerCase() === "updated_at")) {
                continue;
            }

            if (field.primaryKey) {
                chainLines.push(`AddPrimary("${field.name}")`);
            } else {
                const referencedTable = getReferencedTableJS(table, field, newSchema);
                if (referencedTable) {
                    const refOptionsStr = formatRefOptionsJS(field.nullable);
                    chainLines.push(`AddRef<${referencedTable}>("${field.name}"${refOptionsStr})`);
                } else {
                    const { csharpType, sizeOption } = parseTypeAndSizeJS(field.type.name);
                    const optionsStr = formatOptionsJS(field.nullable, field.unique, sizeOption);
                    chainLines.push(`AddProperty<${csharpType}>("${field.name}"${optionsStr})`);
                }
            }
        }

        if (hasTimestamps) {
            chainLines.push("AddTimestamp()");
        }

        sb.push("        " + chainLines[0]);
        for (let i = 1; i < chainLines.length; i++) {
            sb.push("            ." + chainLines[i]);
        }
        sb[sb.length - 1] += ";"
        sb.push("");
    }

    // 3. Update existing tables
    for (const tc of comp.tableComparisons) {
        if (!tc.hasChanges) continue;

        const chainLines = [];
        chainLines.push(`SelectModel<${tc.newTableName}>()`);

        // Rename properties
        for (const rf of tc.renamedFields) {
            const { csharpType } = parseTypeAndSizeJS(rf.new.type.name);
            chainLines.push(`RenameProperty<${csharpType}>("${rf.oldName}", "${rf.newName}")`);
        }

        // Remove properties
        for (const df of tc.deletedFields) {
            const { csharpType } = parseTypeAndSizeJS(df.type.name);
            chainLines.push(`RemoveProperty<${csharpType}>("${df.name}")`);
        }

        // Add properties
        for (const af of tc.addedFields) {
            const newTableObj = newSchema.tables.find(t => t.name === tc.newTableName);
            const referencedTable = getReferencedTableJS(newTableObj, af, newSchema);
            if (referencedTable) {
                const refOptionsStr = formatRefOptionsJS(af.nullable);
                chainLines.push(`AddRef<${referencedTable}>("${af.name}"${refOptionsStr})`);
            } else {
                const { csharpType, sizeOption } = parseTypeAndSizeJS(af.type.name);
                const optionsStr = formatOptionsJS(af.nullable, af.unique, sizeOption);
                chainLines.push(`AddProperty<${csharpType}>("${af.name}"${optionsStr})`);
            }
        }

        // Modified properties
        for (const mf of tc.modifiedFields) {
            const newTableObj = newSchema.tables.find(t => t.name === tc.newTableName);
            const referencedTable = getReferencedTableJS(newTableObj, mf.new, newSchema);
            if (referencedTable) {
                const refOptionsStr = formatRefOptionsJS(mf.new.nullable);
                chainLines.push(`AddRef<${referencedTable}>("${mf.name}"${refOptionsStr})`);
            } else {
                const { csharpType, sizeOption } = parseTypeAndSizeJS(mf.new.type.name);
                const optionsStr = formatOptionsJS(mf.new.nullable, mf.new.unique, sizeOption);
                chainLines.push(`AddProperty<${csharpType}>("${mf.name}"${optionsStr})`);
            }
        }

        if (chainLines.length > 1) {
            sb.push("        " + chainLines[0]);
            for (let i = 1; i < chainLines.length; i++) {
                sb.push("            ." + chainLines[i]);
            }
            sb[sb.length - 1] += ";"
            sb.push("");
        }
    }

    // 4. Delete models
    for (const dt of comp.deletedTables) {
        sb.push(`        DeleteModel<${dt.name}>();`);
    }
    if (comp.deletedTables.length > 0) {
        sb.push("");
    }

    sb.push("    }");
    sb.push("");
    sb.push("    public override void Down()");
    sb.push("    {");

    // 1. Delete models created in Up
    for (const table of comp.addedTables) {
        sb.push(`        DeleteModel<${table.name}>();`);
    }
    if (comp.addedTables.length > 0) {
        sb.push("");
    }

    // 2. Recreate models deleted in Up
    for (const table of comp.deletedTables) {
        const chainLines = [];
        chainLines.push(`CreateModel<${table.name}>()`);

        const fields = table.fields || [];
        const hasCreatedDate = fields.some(f => f.name.toLowerCase() === "createddate" || f.name.toLowerCase() === "created_at");
        const hasUpdatedDate = fields.some(f => f.name.toLowerCase() === "updateddate" || f.name.toLowerCase() === "updated_at");
        const hasTimestamps = hasCreatedDate && hasUpdatedDate;

        for (const field of fields) {
            if (hasTimestamps && (field.name.toLowerCase() === "createddate" || field.name.toLowerCase() === "created_at" || field.name.toLowerCase() === "updateddate" || field.name.toLowerCase() === "updated_at")) {
                continue;
            }

            if (field.primaryKey) {
                chainLines.push(`AddPrimary("${field.name}")`);
            } else {
                const referencedTable = getReferencedTableJS(table, field, oldSchema);
                if (referencedTable) {
                    const refOptionsStr = formatRefOptionsJS(field.nullable);
                    chainLines.push(`AddRef<${referencedTable}>("${field.name}"${refOptionsStr})`);
                } else {
                    const { csharpType, sizeOption } = parseTypeAndSizeJS(field.type.name);
                    const optionsStr = formatOptionsJS(field.nullable, field.unique, sizeOption);
                    chainLines.push(`AddProperty<${csharpType}>("${field.name}"${optionsStr})`);
                }
            }
        }

        if (hasTimestamps) {
            chainLines.push("AddTimestamp()");
        }

        sb.push("        " + chainLines[0]);
        for (let i = 1; i < chainLines.length; i++) {
            sb.push("            ." + chainLines[i]);
        }
        sb[sb.length - 1] += ";"
        sb.push("");
    }

    // 3. Rename models back
    for (const rt of comp.renamedTables) {
        sb.push(`        RenameModel<${rt.oldName}>("${rt.newName}");`);
    }
    if (comp.renamedTables.length > 0) {
        sb.push("");
    }

    // 4. Revert modifications on existing tables
    for (const tc of comp.tableComparisons) {
        if (!tc.hasChanges) continue;

        const chainLines = [];
        chainLines.push(`SelectModel<${tc.oldTableName}>()`);

        // Invert rename properties (new to old)
        for (const rf of tc.renamedFields) {
            const { csharpType } = parseTypeAndSizeJS(rf.old.type.name);
            chainLines.push(`RenameProperty<${csharpType}>("${rf.newName}", "${rf.oldName}")`);
        }

        // Re-add removed properties
        for (const df of tc.deletedFields) {
            const oldTableObj = oldSchema.tables.find(t => t.name === tc.oldTableName);
            const referencedTable = getReferencedTableJS(oldTableObj, df, oldSchema);
            if (referencedTable) {
                const refOptionsStr = formatRefOptionsJS(df.nullable);
                chainLines.push(`AddRef<${referencedTable}>("${df.name}"${refOptionsStr})`);
            } else {
                const { csharpType, sizeOption } = parseTypeAndSizeJS(df.type.name);
                const optionsStr = formatOptionsJS(df.nullable, df.unique, sizeOption);
                chainLines.push(`AddProperty<${csharpType}>("${df.name}"${optionsStr})`);
            }
        }

        // Remove added properties
        for (const af of tc.addedFields) {
            const { csharpType } = parseTypeAndSizeJS(af.type.name);
            chainLines.push(`RemoveProperty<${csharpType}>("${af.name}")`);
        }

        // Revert modified properties back to old values
        for (const mf of tc.modifiedFields) {
            const oldTableObj = oldSchema.tables.find(t => t.name === tc.oldTableName);
            const referencedTable = getReferencedTableJS(oldTableObj, mf.old, oldSchema);
            if (referencedTable) {
                const refOptionsStr = formatRefOptionsJS(mf.old.nullable);
                chainLines.push(`AddRef<${referencedTable}>("${mf.name}"${refOptionsStr})`);
            } else {
                const { csharpType, sizeOption } = parseTypeAndSizeJS(mf.old.type.name);
                const optionsStr = formatOptionsJS(mf.old.nullable, mf.old.unique, sizeOption);
                chainLines.push(`AddProperty<${csharpType}>("${mf.name}"${optionsStr})`);
            }
        }

        if (chainLines.length > 1) {
            sb.push("        " + chainLines[0]);
            for (let i = 1; i < chainLines.length; i++) {
                sb.push("            ." + chainLines[i]);
            }
            sb[sb.length - 1] += ";"
            sb.push("");
        }
    }

    sb.push("    }");
    sb.push("}");

    return sb.join("\n");
}

// Helpers
function getReferencedTableJS(table, field, schema) {
    if (!schema.relationships) return null;
    for (const r of schema.relationships) {
        const sourceFieldName = r.sourceFieldId.includes('.') ? r.sourceFieldId.split('.').pop() : r.sourceFieldId;
        if (r.sourceTableId === table.id && (r.sourceFieldId === field.id || sourceFieldName === field.name)) {
            const targetTable = schema.tables.find(t => t.id === r.targetTableId);
            if (targetTable) return targetTable.name;
        }

        const targetFieldName = r.targetFieldId.includes('.') ? r.targetFieldId.split('.').pop() : r.targetFieldId;
        if (r.targetTableId === table.id && (r.targetFieldId === field.id || targetFieldName === field.name)) {
            const sourceTable = schema.tables.find(t => t.id === r.sourceTableId);
            if (sourceTable) {
                const srcFieldName = r.sourceFieldId.includes('.') ? r.sourceFieldId.split('.').pop() : r.sourceFieldId;
                const srcField = sourceTable.fields.find(f => f.id === r.sourceFieldId || f.name === srcFieldName);
                if (srcField && srcField.primaryKey) {
                    return sourceTable.name;
                }
            }
        }
    }
    return null;
}

function getCSharpTypeJS(dbType) {
    dbType = dbType.toLowerCase().trim();
    if (dbType.includes("int") || dbType === "integer" || dbType === "serial") return "int";
    if (dbType.includes("char") || dbType.includes("text") || dbType === "string" || dbType === "uuid") return "string";
    if (dbType === "bool" || dbType === "boolean" || dbType === "bit") return "bool";
    if (dbType === "datetime" || dbType === "timestamp" || dbType === "date" || dbType === "time") return "DateTime";
    if (dbType === "float" || dbType === "double" || dbType === "real") return "double";
    if (dbType === "decimal" || dbType === "numeric") return "decimal";
    return "string";
}

function parseTypeAndSizeJS(typeName) {
    typeName = typeName.toLowerCase().trim();
    const match = typeName.match(/^varchar\((\d+)\)$/);
    if (match) {
        const length = parseInt(match[1]);
        if (length === 255) return { csharpType: "string", sizeOption: null };
        return { csharpType: "string", sizeOption: `new Size(${length})` };
    }
    if (typeName === "text") return { csharpType: "string", sizeOption: "new Size(SizeEnum.Text)" };
    if (typeName === "mediumtext") return { csharpType: "string", sizeOption: "new Size(SizeEnum.MediumText)" };
    if (typeName === "longtext") return { csharpType: "string", sizeOption: "new Size(SizeEnum.LongText)" };

    return { csharpType: getCSharpTypeJS(typeName), sizeOption: null };
}

function formatOptionsJS(nullable, unique, sizeOption) {
    const parts = [];
    if (nullable) parts.push("Nullable = true");
    if (unique) parts.push("Unique = true");
    if (sizeOption) parts.push(`Size = ${sizeOption}`);

    if (parts.length === 0) return "";
    return ", new() { " + parts.join(", ") + " }";
}

function formatRefOptionsJS(nullable) {
    if (nullable) return ", new() { Nullable = true }";
    return "";
}
