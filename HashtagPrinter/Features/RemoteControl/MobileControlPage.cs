namespace Skarynagram.Features.RemoteControl;

internal static class MobileControlPage
{
    public const string Html = """
<!doctype html>
<html lang="be">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <meta name="theme-color" content="#171713">
  <link rel="manifest" href="/manifest.webmanifest">
  <title>СКАРЫНАГРАМ Пульт</title>
  <style>
    :root{--paper:#eee7d7;--ink:#171713;--red:#78251e;--line:#302e29}
    *{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);
      font-family:Georgia,'Times New Roman',serif}
    main{max-width:680px;margin:auto;padding:28px 18px 54px}
    header{text-align:center;border-bottom:3px double var(--ink);padding:12px 0 22px}
    h1{font-size:clamp(30px,9vw,52px);letter-spacing:.12em;margin:0}
    .kicker{color:var(--red);font:700 12px system-ui;letter-spacing:.2em;margin-top:8px}
    section{border:2px solid var(--line);margin-top:20px;padding:18px;background:#f5f0e5}
    h2{font-size:20px;margin:0 0 14px;border-bottom:1px solid;padding-bottom:10px}
    label{display:block;font:600 13px system-ui;margin:12px 0 6px}
    input,select,button{width:100%;min-height:48px;border:1px solid var(--ink);border-radius:0;
      font:16px system-ui;padding:10px;background:#fbf8f0;color:var(--ink)}
    button{background:var(--ink);color:white;font-weight:700;letter-spacing:.08em;margin-top:14px}
    button:active{transform:translateY(2px)}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
    .stat{border-left:4px solid var(--red);padding:8px 12px}.stat b{display:block;font-size:28px}
    #state{font:14px system-ui;margin-top:12px}.ok{color:#28562d}.bad{color:var(--red)}
    .hidden{display:none}
  </style>
</head>
<body><main>
  <header><h1>СКАРЫНАГРАМ</h1><div class="kicker">ПУЛЬТ КІРАВАННЯ</div></header>
  <section>
    <h2>I — ЗЛУЧЭННЕ</h2>
    <label for="pin">PIN-код (пароль кліенцкага акна)</label>
    <input id="pin" type="password" inputmode="numeric" autocomplete="current-password">
    <button id="connect">ЗЛУЧЫЦЦА</button><div id="state">Няма злучэння</div>
  </section>
  <div id="panel" class="hidden">
    <section><h2>II — СТАН ДРУКАРНІ</h2>
      <div class="grid"><div class="stat">Сёння<b id="today">—</b></div>
      <div class="stat">Усяго<b id="total">—</b></div></div>
      <p>Хэштэг: <strong id="hashtag">—</strong></p>
      <p>Камп'ютар: <strong id="computer">—</strong></p>
    </section>
    <section><h2>III — ПРЫНТАР І ПАПЕРА</h2>
      <label for="printer">Прынтар</label><select id="printer"></select>
      <label for="paper">Фармат паперы</label><select id="paper"></select>
      <button id="save">ЗАХАВАЦЬ ВЫБАР</button>
    </section>
  </div>
</main>
<script>
const $=id=>document.getElementById(id);
const state=(text,ok=false)=>{$('state').textContent=text;$('state').className=ok?'ok':'bad'};
const headers=()=>({'X-Skarynagram-Pin':$('pin').value,'Content-Type':'application/json'});
async function request(path,options={}){
  const r=await fetch(path,{...options,headers:{...headers(),...(options.headers||{})}});
  const data=await r.json().catch(()=>({error:'Няправільны адказ камп’ютара.'}));
  if(!r.ok)throw new Error(data.error||`HTTP ${r.status}`);return data;
}
async function refresh(){
  const [s,o]=await Promise.all([request('/api/status'),request('/api/print-options')]);
  $('today').textContent=s.printedToday;$('total').textContent=s.printedTotal;
  $('hashtag').textContent='#'+(s.hashtag||'');$('computer').textContent=s.computerName;
  $('printer').innerHTML=o.printers.map(x=>`<option>${escapeHtml(x)}</option>`).join('');
  $('paper').innerHTML=o.papers.map(x=>`<option>${escapeHtml(x)}</option>`).join('');
  $('printer').value=o.selectedPrinter;$('paper').value=o.selectedPaper;
  $('panel').classList.remove('hidden');state('Злучэнне працуе',true);
}
function escapeHtml(v){const d=document.createElement('div');d.textContent=v;return d.innerHTML}
$('pin').value=localStorage.getItem('skarynagram-pin')||'';
$('connect').onclick=async()=>{try{localStorage.setItem('skarynagram-pin',$('pin').value);await refresh()}
  catch(e){$('panel').classList.add('hidden');state(e.message)}};
$('save').onclick=async()=>{try{await request('/api/print-options',{method:'PUT',
  body:JSON.stringify({printer:$('printer').value,paper:$('paper').value})});state('Налады захаваны',true)}
  catch(e){state(e.message)}};
if($('pin').value)refresh().catch(e=>state(e.message));
setInterval(()=>{if(!$('panel').classList.contains('hidden'))refresh().catch(()=>{})},30000);
</script></body></html>
""";

    public const string Manifest = """
{"name":"СКАРЫНАГРАМ Пульт","short_name":"СКАРЫНАГРАМ","start_url":"/","display":"standalone","background_color":"#eee7d7","theme_color":"#171713","lang":"be"}
""";
}
