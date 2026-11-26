import { useEffect, useState } from "react";
import {
  obtenerSesion,
  obtenerMensajes,
  crearMensaje,
  cerrarSesion
} from "./api";
import "./App.css";

function App() {
  const [usuario, setUsuario] = useState(null);
  const [contenido, setContenido] = useState("");
  const [resultado, setResultado] = useState(null);
  const [cargando, setCargando] = useState(false);

  async function verificarSesion() {
    const sesion = await obtenerSesion();
    setUsuario(sesion);
  }

  useEffect(() => {
    verificarSesion();
  }, []);

  function login() {
    window.location.href = "https://localhost:7010/auth/vimeo/login";
  }

  async function logout() {
    await cerrarSesion();
    setUsuario(null);
    setResultado("Sesión cerrada correctamente.");
  }

  async function probarGET() {
    setCargando(true);
    const respuesta = await obtenerMensajes();
    setResultado(respuesta);
    await verificarSesion();
    setCargando(false);
  }

  async function probarPOST() {
    if (!contenido.trim()) return;

    setCargando(true);
    const respuesta = await crearMensaje(contenido);
    setResultado(respuesta);

    if (respuesta.status === 200) {
      setContenido("");
    }

    setCargando(false);
  }

  return (
    <main className="container">
      <header>
        <h1>Examen 3 - Autenticación OAuth</h1>
        <p>Integración real con Vimeo</p>
      </header>

      <section className="card">
        <h2>Autenticación</h2>

        {usuario ? (
          <>
            <p><strong>Usuario:</strong> {usuario.usuario}</p>
            <p><strong>Proveedor:</strong> {usuario.proveedor}</p>
            <p><strong>ID:</strong> {usuario.identificador}</p>

            <button className="logout" onClick={logout}>
              Cerrar sesión
            </button>
          </>
        ) : (
          <>
            <p>No hay ningún usuario autenticado.</p>
            <button onClick={login}>
              Iniciar sesión con Vimeo
            </button>
          </>
        )}
      </section>

      <section className="card">
        <h2>Probar endpoints protegidos</h2>

        <input
          type="text"
          placeholder="Escribe un mensaje"
          value={contenido}
          onChange={(e) => setContenido(e.target.value)}
        />

        <div className="actions">
          <button onClick={probarPOST} disabled={cargando}>
            Probar POST
          </button>

          <button onClick={probarGET} disabled={cargando}>
            Probar GET
          </button>
        </div>

        <h3>Respuesta del servidor</h3>

        <pre>
          {resultado
            ? JSON.stringify(resultado, null, 2)
            : "Todavía no hay resultados."}
        </pre>
      </section>
    </main>
  );
}

export default App;