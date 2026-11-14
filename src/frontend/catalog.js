import {loadProcurements,escapeHtml as e,money,date,safeLink} from './data.js';
const form=document.querySelector('#filters'), results=document.querySelector('#results'), count=document.querySelector('#result-count');
let records=[];
export const hasMissing = p => ['buyer','contractor','contractAmount','address','startDate','endDate','sourceUrl'].some(key=>p[key]===null || p[key]===undefined || p[key]==='') || !Array.isArray(p.events) || !p.events.length;
function render() {
  const query=form.elements.q.value.trim().toLocaleLowerCase('uk');
  const shown=records.filter(p=>(!query || [p.title,p.buyer,p.id].some(v=>String(v??'').toLocaleLowerCase('uk').includes(query))) && (!form.elements.category.value || p.category===form.elements.category.value) && (!form.elements.status.value || p.status===form.elements.status.value) && (!form.elements.completeness.value || hasMissing(p)));
  count.textContent=`Знайдено: ${shown.length} із ${records.length}`;
  results.innerHTML=shown.length ? shown.map(p=>`<article class="panel procurement-card"><div class="card-top"><span class="eyebrow">${e(p.category || 'Категорія невідома')}</span><span class="badge">${e(p.status || 'Етап невідомий')}</span></div><h3><a href="./procurement.html?id=${encodeURIComponent(p.id)}">${e(p.title || 'Назва невідома')} <span aria-hidden="true">↗</span></a></h3><p class="muted record-id">${e(p.id)} · MOCK</p><p class="buyer">${e(p.buyer || 'Замовник невідомий')}</p><div class="card-facts"><div><span class="muted">Очікувана вартість</span><strong>${e(money(p.expectedAmount,p.currency))}</strong></div><div><span class="muted">Дата початку</span><strong>${e(date(p.startDate))}</strong></div></div><div class="card-bottom"><span class="${hasMissing(p)?'missing':'muted'}">${hasMissing(p)?'◌ Є неповні дані':'Демонстраційний запис'}</span>${safeLink(p.sourceUrl,'Джерело ↗')}</div></article>`).join('') : '<div class="panel empty-state"><h3>Нічого не знайдено</h3><p>Змініть запит або скиньте фільтри.</p><button class="button button-secondary" id="clear-results">Скинути фільтри</button></div>';
  document.querySelector('#clear-results')?.addEventListener('click',()=>{form.reset();render();});
  const params=new URLSearchParams(); for(const [k,v] of new FormData(form)) if(v) params.set(k,v); history.replaceState(null,'',`${location.pathname}${params.size?'?'+params:''}`);
}
async function init() {
  results.setAttribute('aria-busy','true');
  try {
    records=await loadProcurements();
    for(const key of ['category','status']) { const select=form.elements[key];select.length=1;[...new Set(records.map(p=>p[key]).filter(Boolean))].sort((a,b)=>a.localeCompare(b,'uk')).forEach(value=>select.add(new Option(value,value))); }
    const params=new URLSearchParams(location.search);for(const key of ['q','category','status','completeness']) if(params.has(key))form.elements[key].value=params.get(key);
    render();
  } catch {count.textContent='Дані недоступні';results.innerHTML='<div class="panel empty-state" role="alert"><h3>Не вдалося завантажити каталог</h3><p>Перевірте підключення та запустіть сайт через локальний HTTP-сервер.</p><button id="retry" class="button">Спробувати ще раз</button></div>';document.querySelector('#retry').addEventListener('click',init);}
  finally {results.setAttribute('aria-busy','false');}
}
form.addEventListener('input',render);form.addEventListener('submit',event=>{event.preventDefault();render();});form.addEventListener('reset',()=>setTimeout(render,0));init();
