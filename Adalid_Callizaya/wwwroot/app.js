const sesion = document.getElementById('sesion');
const respuesta = document.getElementById('respuesta');
async function api(path, method = 'GET', body) {
  const headers = { 'X-Examen': '1' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const res = await fetch(path, { method, headers, credentials: 'same-origin', cache: 'no-store', body: body === undefined ? undefined : JSON.stringify(body) });
  const raw = await res.text();
  let data; try { data = JSON.parse(raw); } catch { data = raw; }
  return { res, data };
}
function mostrar(method, path, result) {
  respuesta.textContent = `${method} ${path}\nHTTP ${result.res.status}\n\n${JSON.stringify(result.data, null, 2)}`;
}
async function actualizar(mostrarRespuesta = false) {
  const r = await api('/api/sesion');
  sesion.textContent = r.res.ok ? `${r.data.nombre} | Proveedor: ${r.data.proveedor} | SteamID64: ${r.data.steamId}` : 'Sin sesión autenticada.';
  if (mostrarRespuesta) mostrar('GET', '/api/sesion', r);
}
function seguro(fn) { return async event => { try { await fn(event); } catch (e) { respuesta.textContent = 'Error de conexión: ' + e.message; } }; }
document.getElementById('refresh').onclick = seguro(() => actualizar(true));
document.getElementById('form').onsubmit = seguro(async e => {
  e.preventDefault();
  mostrar('POST', '/api/registros', await api('/api/registros', 'POST', { texto: document.getElementById('texto').value }));
});
document.getElementById('get').onclick = seguro(async () => mostrar('GET', '/api/registros', await api('/api/registros')));
document.getElementById('logout').onclick = seguro(async () => {
  mostrar('POST', '/auth/logout', await api('/auth/logout', 'POST'));
  await actualizar();
});
seguro(() => actualizar())();
if (new URLSearchParams(location.search).has('error')) respuesta.textContent = 'No se pudo completar el inicio de sesión con Steam. Revisa la terminal e inténtalo otra vez.';
