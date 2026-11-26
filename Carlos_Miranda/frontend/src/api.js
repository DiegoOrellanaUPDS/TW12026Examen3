const API = "https://localhost:7010";

export async function obtenerSesion() {
  const response = await fetch(`${API}/auth/vimeo/me`, {
    credentials: "include"
  });

  if (!response.ok) return null;
  return response.json();
}

export async function obtenerMensajes() {
  const response = await fetch(`${API}/api/protegido`, {
    credentials: "include"
  });

  const data = await response.json();
  return { status: response.status, data };
}

export async function crearMensaje(contenido) {
  const response = await fetch(`${API}/api/protegido`, {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ contenido })
  });

  const data = await response.json();
  return { status: response.status, data };
}

export async function cerrarSesion() {
  const response = await fetch(`${API}/auth/vimeo/logout`, {
    method: "POST",
    credentials: "include"
  });

  return response.ok;
}
