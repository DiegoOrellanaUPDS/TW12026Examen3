const mensaje = document.getElementById("mensaje");
const resultados = document.getElementById("resultados");

function aviso(texto, esCorrecto = false) {
    mensaje.textContent = texto;
    mensaje.classList.toggle("ok", esCorrecto);
}

function limpiarUsuario() {
    document.getElementById("nombre").textContent = "—";
    document.getElementById("identificador").textContent = "—";
    document.getElementById("proveedor").textContent = "—";
}

async function pedir(url, opciones = {}) {
    const respuesta = await fetch(url, { credentials: "same-origin", ...opciones });

    if (respuesta.status === 401 || respuesta.status === 403) {
        const error = await respuesta.json().catch(() => ({}));
        aviso(error.mensaje || "Acceso denegado. Inicia sesión con Vimeo.");
        limpiarUsuario();
        throw new Error("Acceso denegado");
    }

    return respuesta;
}

async function cargarUsuario() {
    try {
        const respuesta = await pedir("/usuario");
        if (!respuesta.ok) return;

        const usuario = await respuesta.json();
        document.getElementById("nombre").textContent = usuario.nombre ?? "—";
        document.getElementById("identificador").textContent = usuario.id ?? "—";
        document.getElementById("proveedor").textContent = usuario.proveedor ?? "—";
    } catch {
        // El aviso ya se mostró en pedir().
    }
}

async function registrar() {
    const titulo = document.getElementById("titulo").value.trim();
    const descripcion = document.getElementById("descripcion").value.trim();

    if (!titulo) {
        aviso("Escribe un título.");
        return;
    }

    try {
        const respuesta = await pedir("/api/melissa", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ titulo, descripcion })
        });
        if (!respuesta.ok) return;

        await respuesta.json();
        aviso("Registro guardado correctamente.", true);
        document.getElementById("titulo").value = "";
        document.getElementById("descripcion").value = "";
    } catch {
        // El aviso ya se mostró en pedir().
    }
}

async function verRegistros() {
    try {
        const respuesta = await pedir("/api/melissa");
        if (!respuesta.ok) return;

        const registros = await respuesta.json();
        resultados.textContent = registros.length
            ? JSON.stringify(registros, null, 2)
            : "No hay registros todavía.";
    } catch {
        resultados.textContent = "No se pudieron cargar los registros.";
    }
}

document.getElementById("btnLogin").addEventListener("click", () => location.href = "/login");
document.getElementById("btnLogout").addEventListener("click", () => location.href = "/logout");
document.getElementById("btnRegistrar").addEventListener("click", registrar);
document.getElementById("btnVer").addEventListener("click", verRegistros);

cargarUsuario();
