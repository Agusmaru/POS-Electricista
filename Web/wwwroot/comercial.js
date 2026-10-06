(() => {
    const form = document.getElementById('comercial-form');
    if (!form) return;
    const model = JSON.parse(document.getElementById('comercial-inicial').value);
    const rows = document.getElementById('comercial-rows');
    const status = document.getElementById('comercial-status');
    const money = n => new Intl.NumberFormat('es-AR', {style:'currency',currency:'USD'}).format(n);
    const cents = n => Math.round((n + Number.EPSILON) * 100);
    let dirty = false;
    document.getElementById('comercial-nuevo').addEventListener('submit', event => {
        if (!window.confirm('¿Crear un nuevo presupuesto? Se conservará el último borrador guardado; los cambios sin guardar se perderán.')) event.preventDefault();
        else dirty = false;
    });
    function changed() { dirty = true; status.textContent = 'Cambios sin guardar.'; totals(); }
    function totals() {
        let net = 0, tax = 0;
        rows.querySelectorAll('.comercial-row').forEach(row => {
            const val = key => Number(row.querySelector(`[data-item="${key}"]`).value) || 0;
            const n = cents(val('Cantidad') * val('Precio'));
            const t = Math.round(n * val('Alicuota') / 100 + 1e-7);
            net += n; tax += t;
            row.querySelector('[data-row-total]').textContent = `Subtotal: ${money(n/100)} · IVA: ${money(t/100)} · Total: ${money((n+t)/100)}`;
        });
        document.getElementById('comercial-neto').textContent = money(net/100);
        document.getElementById('comercial-iva').textContent = money(tax/100);
        document.getElementById('comercial-total').textContent = money((net+tax+cents(Number(form.querySelector('[data-field="ManoObra"]').value)||0))/100);
    }
    function refreshOrder() {
        const items = [...rows.children];
        document.getElementById('comercial-empty').hidden = items.length > 0;
        items.forEach((row, index) => {
            row.querySelector('strong').textContent = `Renglón ${index + 1}`;
            row.querySelector('.comercial-order-handle').setAttribute('aria-label', `Ordenar renglón ${index + 1}. Usá las flechas arriba y abajo para moverlo.`);
            row.querySelector('[data-move="up"]').disabled = index === 0;
            row.querySelector('[data-move="down"]').disabled = index === items.length - 1;
        });
    }
    function moved(row) {
        refreshOrder(); changed();
        status.textContent = `Fila movida a la posición ${[...rows.children].indexOf(row) + 1}. Cambios sin guardar.`;
    }
    function move(row, direction) {
        const sibling = direction < 0 ? row.previousElementSibling : row.nextElementSibling;
        if (!sibling) return;
        rows.insertBefore(row, direction < 0 ? sibling : sibling.nextElementSibling);
        row.querySelector('.comercial-order-handle').focus();
        moved(row);
    }
    function enableDrag(row, handle) {
        handle.addEventListener('keydown', event => {
            if (!['ArrowUp', 'ArrowDown'].includes(event.key)) return;
            event.preventDefault(); move(row, event.key === 'ArrowUp' ? -1 : 1);
        });
        handle.addEventListener('pointerdown', event => {
            if (event.button !== 0 || !event.isPrimary || rows.children.length < 2) return;
            event.preventDefault(); handle.focus();
            const originalIndex = [...rows.children].indexOf(row);
            const others = [...rows.children].filter(item => item !== row);
            const startY = event.clientY;
            let destination = originalIndex, dragging = false;
            handle.setPointerCapture(event.pointerId);
            const clearMarkers = () => others.forEach(item => item.classList.remove('comercial-drop-before', 'comercial-drop-after'));
            const update = current => {
                if (current.pointerId !== event.pointerId) return;
                if (!dragging && Math.abs(current.clientY - startY) < 5) return;
                dragging = true;
                row.classList.add('is-dragging');
                destination = others.findIndex(item => {
                    const rect = item.getBoundingClientRect();
                    return current.clientY < rect.top + rect.height / 2;
                });
                if (destination < 0) destination = others.length;
                clearMarkers();
                if (destination < others.length) others[destination].classList.add('comercial-drop-before');
                else others[others.length - 1].classList.add('comercial-drop-after');
                if (current.clientY < 80) window.scrollBy(0, -24);
                else if (current.clientY > window.innerHeight - 80) window.scrollBy(0, 24);
            };
            const finish = current => {
                if (current.pointerId !== event.pointerId) return;
                handle.removeEventListener('pointermove', update);
                handle.removeEventListener('pointerup', finish);
                handle.removeEventListener('pointercancel', finish);
                handle.removeEventListener('lostpointercapture', finish);
                clearMarkers(); row.classList.remove('is-dragging');
                if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
                if (current.type === 'pointerup' && dragging && destination !== originalIndex) {
                    rows.insertBefore(row, others[destination] || null);
                    moved(row);
                }
            };
            handle.addEventListener('pointermove', update);
            handle.addEventListener('pointerup', finish);
            handle.addEventListener('pointercancel', finish);
            handle.addEventListener('lostpointercapture', finish);
        });
    }
    function add(item = {Articulo:'',Descripcion:'',Cantidad:1,Precio:0,Alicuota:21}) {
        if(rows.children.length >= 100) { status.textContent = 'Se permiten hasta 100 filas.'; return; }
        const row = document.createElement('article'); row.className = 'comercial-row';
        const head = document.createElement('div'); head.className = 'comercial-row-head';
        const title = document.createElement('strong'); title.textContent = 'Renglón';
        const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'dangertext'; remove.textContent = 'Eliminar fila';
        remove.addEventListener('click',()=>{row.remove();refreshOrder();changed();});
        const controls = document.createElement('div'); controls.className = 'comercial-order-controls';
        const handle = document.createElement('button'); handle.type = 'button'; handle.className = 'comercial-order-handle'; handle.textContent = '≡'; handle.title = 'Arrastrar para ordenar';
        enableDrag(row, handle); controls.append(handle, title);
        for (const [key, text, direction] of [['up', '↑ Subir', -1], ['down', '↓ Bajar', 1]]) {
            const button = document.createElement('button'); button.type = 'button'; button.dataset.move = key; button.textContent = text;
            button.addEventListener('click', () => move(row, direction)); controls.append(button);
        }
        head.append(controls,remove);row.append(head);
        const fields = document.createElement('div'); fields.className = 'comercial-row-fields';
        for(const [key,label] of [['Articulo','Artículo'],['Descripcion','Descripción'],['Cantidad','Cantidad'],['Precio','Precio unitario USD'],['Alicuota','IVA %']]) {
            const wrapper = document.createElement('label');wrapper.textContent = label;
            const input = document.createElement(key === 'Descripcion' ? 'textarea' : key === 'Alicuota' ? 'select' : 'input');input.dataset.item = key;
            if(key === 'Alicuota') [0,10.5,21,27].forEach(n=>{const option=document.createElement('option');option.value=n;option.textContent=n+' %';input.append(option);});
            else if(key === 'Cantidad' || key === 'Precio') { input.type='number';input.min=key==='Cantidad'?'0.001':'0';input.max=key==='Cantidad'?'1000000':'100000000';input.step=key==='Cantidad'?'0.001':'0.01';input.required=true; }
            else { input.maxLength=key==='Articulo'?100:500;input.required=key==='Articulo'; }
            input.value=item[key] ?? '';wrapper.append(input);fields.append(wrapper);
        }
        row.append(fields);const total=document.createElement('div');total.className='comercial-row-totals';total.dataset.rowTotal='';row.append(total);rows.append(row);refreshOrder();totals();
        return row;
    }
    form.querySelectorAll('[data-field]').forEach(input=>{input.value=input.dataset.field==='Fecha'?model.Fecha.slice(0,10):model[input.dataset.field] ?? '';});
    model.Items.forEach(add);refreshOrder();totals();
    form.addEventListener('input',changed);
    form.addEventListener('change',changed);
    form.addEventListener('submit',event=>event.preventDefault());
    document.getElementById('comercial-add').addEventListener('click',()=>{const row=add();if(row){changed();row.querySelector('input').focus();}});
    const collect = () => {
        const data={};form.querySelectorAll('[data-field]').forEach(input=>data[input.dataset.field]=input.dataset.field==='ManoObra'?Number(input.value):input.value);
        data.Items=[...rows.children].map(row=>{const item={};row.querySelectorAll('[data-item]').forEach(input=>item[input.dataset.item]=['Cantidad','Precio','Alicuota'].includes(input.dataset.item)?Number(input.value):input.value);return item;});return data;
    };
    form.querySelectorAll('[data-comercial-action]').forEach(button=>button.addEventListener('click',async()=>{
        if(!form.reportValidity())return;
        if(!rows.children.length){status.textContent='Agregá al menos una fila.';return;}
        const action=button.dataset.comercialAction;
        const preview=action==='preview'?window.open('about:blank','_blank'):null;
        const buttons=[...form.querySelectorAll('[data-comercial-action]')];buttons.forEach(b=>b.disabled=true);
        const content=JSON.stringify(collect());
        status.textContent=action==='guardar'?'Guardando borrador…':'Generando PDF…';
        try {
            const payload=new FormData();payload.set('csrf',form.querySelector('[name="csrf"]').value);payload.set('contenido',content);payload.set('accion',action==='guardar'?'guardar':'pdf');
            const response=await fetch(form.action,{method:'POST',body:payload,headers:{Accept:action==='guardar'?'application/json':'application/pdf','X-Requested-With':'XMLHttpRequest'}});
            if(!response.ok){let message='No se pudo completar la operación. Revisá la sesión y los datos.';try{message=(await response.json()).error||message;}catch{}throw new Error(message);}
            if(action==='guardar') {
                const result=await response.json();
                if(content===JSON.stringify(collect()))dirty=false;
                status.textContent=dirty?'Borrador guardado. Hay cambios posteriores sin guardar.':`Borrador guardado. Total: ${money(result.total)}.`;
                document.getElementById('comercial-neto').textContent=money(result.neto);document.getElementById('comercial-iva').textContent=money(result.iva);document.getElementById('comercial-total').textContent=money(result.total);
            } else {
                if(!response.headers.get('content-type')?.includes('application/pdf'))throw new Error('La sesión venció. Guardá tus cambios antes de volver a ingresar.');
                const url=URL.createObjectURL(await response.blob());
                if(preview)preview.location.href=url;
                else {const a=document.createElement('a');a.href=url;a.download='presupuesto-comercial.pdf';a.click();}
                setTimeout(()=>URL.revokeObjectURL(url),300000);
                status.textContent='PDF generado.'+(dirty?' Recordá guardar el borrador.':'');
            }
        } catch(error) { if(preview)preview.close();status.textContent=error.message; }
        finally {buttons.forEach(b=>b.disabled=false);}
    }));
    window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
})();
