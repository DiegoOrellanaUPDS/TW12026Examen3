const state = {
  configured: false,
  authenticated: false,
  provider: "Epic Games",
  user: null
};

const elements = {
  anonymousPanel: document.getElementById("anonymousPanel"),
  authenticatedPanel: document.getElementById("authenticatedPanel"),
  loginButton: document.getElementById("loginButton"),
  logoutButton: document.getElementById("logoutButton"),
  setupNotice: document.getElementById("setupNotice"),
  avatar: document.getElementById("avatar"),
  avatarFallback: document.getElementById("avatarFallback"),
  displayName: document.getElementById("displayName"),
  preferredUsername: document.getElementById("preferredUsername"),
  accountId: document.getElementById("accountId"),
  sessionProvider: document.getElementById("sessionProvider"),
  noteText: document.getElementById("noteText"),
  charCount: document.getElementById("charCount"),
  postButton: document.getElementById("postButton"),
  getButton: document.getElementById("getButton"),
  meButton: document.getElementById("meButton"),
  noCookieButton: document.getElementById("noCookieButton"),
  statusBadge: document.getElementById("statusBadge"),
  responseOutput: document.getElementById("responseOutput"),
  notesList: document.getElementById("notesList"),
  notesCount: document.getElementById("notesCount"),
  toast: document.getElementById("toast")
};

const authErrorMessages = {
  not_configured: "Epic Games no está configurado. Revisa Epic:ClientId y Epic:ClientSecret.",
  invalid_state: "La validación de estado falló. Inicia sesión nuevamente.",
  access_denied: "Cancelaste el acceso en Epic Games.",
  invalid_callback: "Epic Games no devolvió un código de autorización válido.",
  token_exchange_failed: "Epic Games rechazó el código de autorización.",
  provider_unavailable: "No se pudo contactar a Epic Games. Intenta de nuevo.",
  userinfo_failed: "No se pudo leer tu perfil de Epic Games.",
  login_failed: "No se pudo completar el inicio de sesión."
};

let toastTimer;

async function request(path, options = {}) {
  const headers = new Headers(options.headers || {});
  headers.set("Accept", "application/json");

  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, {
    ...options,
    headers,
    credentials: options.credentials ?? "same-origin"
  });

  const text = await response.text();
  let body = null;

  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  }

  return { response, body };
}

function showToast(message, kind = "ok") {
  elements.toast.textContent = message;
  elements.toast.className = `toast ${kind}`;
  elements.toast.hidden = false;

  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => {
    elements.toast.hidden = true;
  }, 5200);
}

function showResponse(label, response, body) {
  const status = response.status;
  const statusText = response.statusText ? ` ${response.statusText}` : "";

  elements.statusBadge.hidden = false;
  elements.statusBadge.textContent = `${status}${statusText}`;
  elements.statusBadge.className =
    status >= 200 && status < 300
      ? "status-badge ok"
      : status === 401 || status === 403
        ? "status-badge warn"
        : "status-badge error";

  const payload = body ?? { status, detail: "Respuesta sin cuerpo" };
  elements.responseOutput.textContent =
    `${label}\nHTTP ${status}${statusText}\n\n${JSON.stringify(payload, null, 2)}`;
}

async function run(button, action) {
  const originalText = button.textContent;
  button.disabled = true;
  button.setAttribute("aria-busy", "true");

  try {
    await action();
  } catch (error) {
    showToast(error?.message || "Error de red. Revisa la consola del servidor.", "error");
  } finally {
    button.disabled = false;
    button.removeAttribute("aria-busy");
    button.textContent = originalText;
  }
}

function setSetupState(configured) {
  state.configured = configured;
  elements.setupNotice.hidden = configured;

  if (configured) {
    elements.loginButton.classList.remove("disabled");
    elements.loginButton.removeAttribute("aria-disabled");
    elements.setupNotice.textContent = "";
    return;
  }

  elements.setupNotice.textContent =
    "Falta configurar Epic:ClientId y Epic:ClientSecret. El botón de login responderá 503 hasta completar el README.";
  elements.loginButton.classList.add("disabled");
  elements.loginButton.setAttribute("aria-disabled", "true");
}

function renderAuth() {
  elements.anonymousPanel.hidden = state.authenticated;
  elements.authenticatedPanel.hidden = !state.authenticated;
  elements.sessionProvider.textContent = state.provider;

  if (!state.authenticated || !state.user) {
    elements.avatar.hidden = true;
    elements.avatar.removeAttribute("src");
    elements.avatarFallback.hidden = false;
    elements.displayName.textContent = "—";
    elements.preferredUsername.textContent = "";
    elements.accountId.textContent = "";
    return;
  }

  const user = state.user;
  const displayName = user.displayName || user.username || "Usuario de Epic Games";
  elements.displayName.textContent = displayName;
  elements.preferredUsername.textContent = user.preferredUsername ? `@${user.preferredUsername}` : "";
  elements.accountId.textContent = `ID: ${user.id}`;
  elements.avatarFallback.textContent = displayName.slice(0, 1).toUpperCase();

  const avatarUrl = typeof user.avatarUrl === "string" ? user.avatarUrl : "";
  let safeAvatar = false;

  if (avatarUrl) {
    try {
      safeAvatar = new URL(avatarUrl).protocol === "https:";
    } catch {
      safeAvatar = false;
    }
  }

  if (safeAvatar) {
    elements.avatar.src = avatarUrl;
    elements.avatar.hidden = false;
    elements.avatarFallback.hidden = true;
    elements.avatar.onerror = () => {
      elements.avatar.hidden = true;
      elements.avatarFallback.hidden = false;
    };
  } else {
    elements.avatar.hidden = true;
    elements.avatar.removeAttribute("src");
    elements.avatarFallback.hidden = false;
  }
}

function renderNotes(notes, count, emptyMessage = "Sin notas todavía.") {
  elements.notesList.replaceChildren();
  elements.notesCount.textContent =
    count > 0 ? `${count} ${count === 1 ? "nota" : "notas"}` : "";

  if (!Array.isArray(notes) || notes.length === 0) {
    const item = document.createElement("li");
    item.className = "empty";
    item.textContent = emptyMessage;
    elements.notesList.append(item);
    return;
  }

  for (const note of notes) {
    const item = document.createElement("li");
    item.className = "note";

    const text = document.createElement("p");
    text.className = "note-text";
    text.textContent = note.text;

    const meta = document.createElement("div");
    meta.className = "note-meta";

    const author = document.createElement("span");
    author.textContent = note.authorName;

    const date = document.createElement("time");
    date.dateTime = note.createdAt;
    date.textContent = formatDate(note.createdAt);

    const provider = document.createElement("span");
    provider.className = "chip";
    provider.textContent = note.provider;

    meta.append(author, date, provider);
    item.append(text, meta);
    elements.notesList.append(item);
  }
}

function formatDate(value) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat("es", {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(date);
}

async function loadStatus() {
  const { response, body } = await request("/api/auth/status");

  if (!response.ok || !body || typeof body.configured !== "boolean") {
    throw new Error("No se pudo consultar el estado del proveedor.");
  }

  setSetupState(body.configured);
}

async function loadMe() {
  const { response, body } = await request("/api/auth/me");

  if (!response.ok || !body) {
    throw new Error("No se pudo consultar la sesión actual.");
  }

  state.authenticated = Boolean(body.authenticated);
  state.provider =
    typeof body.provider === "string" && body.provider ? body.provider : "Epic Games";
  state.user = body.user ?? null;
  renderAuth();

  if (state.authenticated) {
    await loadNotes();
  } else {
    renderNotes([], 0, "Inicia sesión para ver tus notas.");
  }
}

async function loadNotes() {
  const { response, body } = await request("/api/notes");

  if (!response.ok) {
    if (response.status === 401) {
      renderNotes([], 0, "Sin sesión: GET /api/notes responde 401.");
    }
    return;
  }

  renderNotes(body?.notes ?? [], Number(body?.count ?? 0));
}

async function handleLogout() {
  await run(elements.logoutButton, async () => {
    const { response, body } = await request("/api/auth/logout", { method: "POST" });
    showResponse("POST /api/auth/logout", response, body);

    if (!response.ok) {
      showToast("No se pudo cerrar la sesión.", "error");
      return;
    }

    showToast("Sesión cerrada. La cookie anterior ya no es válida.", "ok");
    await loadMe();
  });
}

async function handlePostNote() {
  const text = elements.noteText.value.trim();

  if (!text) {
    showToast("Escribe un texto antes de probar POST.", "error");
    elements.noteText.focus();
    return;
  }

  await run(elements.postButton, async () => {
    const { response, body } = await request("/api/notes", {
      method: "POST",
      body: JSON.stringify({ text })
    });

    showResponse("POST /api/notes", response, body);

    if (response.status === 201) {
      elements.noteText.value = "";
      updateCharCount();
      showToast("Nota creada y guardada en el servidor.", "ok");
      await loadNotes();
    } else if (response.status === 401) {
      showToast("Sin sesión: POST /api/notes respondió 401.", "error");
    } else if (response.status === 403) {
      showToast("La sesión no fue creada con Epic Games (403).", "error");
    }
  });
}

async function handleGetNotes() {
  await run(elements.getButton, async () => {
    const { response, body } = await request("/api/notes");
    showResponse("GET /api/notes", response, body);

    if (response.ok) {
      renderNotes(body?.notes ?? [], Number(body?.count ?? 0));
    } else if (response.status === 401) {
      showToast("Sin sesión: GET /api/notes respondió 401.", "error");
    }
  });
}

async function handleMe() {
  await run(elements.meButton, async () => {
    const { response, body } = await request("/api/auth/me");
    showResponse("GET /api/auth/me", response, body);
  });
}

async function handleNoCookie() {
  await run(elements.noCookieButton, async () => {
    const { response, body } = await request("/api/notes", { credentials: "omit" });
    showResponse("GET /api/notes (sin cookie)", response, body);

    if (response.status === 401) {
      showToast("Correcto: sin cookie, el endpoint protegido responde 401.", "ok");
    }
  });
}

function updateCharCount() {
  elements.charCount.textContent = `${elements.noteText.value.length}/500`;
}

function handleQueryMessage() {
  const params = new URLSearchParams(window.location.search);
  const login = params.get("login");
  const authError = params.get("authError");

  if (login === "ok") {
    showToast("Sesión iniciada con Epic Games.", "ok");
  }

  if (authError) {
    showToast(authErrorMessages[authError] || "No se pudo iniciar sesión con Epic Games.", "error");
  }

  if (login || authError) {
    window.history.replaceState({}, "", window.location.pathname + window.location.hash);
  }
}

function wireEvents() {
  elements.loginButton.addEventListener("click", (event) => {
    if (!state.configured) {
      event.preventDefault();
      showToast("Configura Epic:ClientId y Epic:ClientSecret antes de iniciar sesión.", "error");
    }
  });

  elements.logoutButton.addEventListener("click", handleLogout);
  elements.postButton.addEventListener("click", handlePostNote);
  elements.getButton.addEventListener("click", handleGetNotes);
  elements.meButton.addEventListener("click", handleMe);
  elements.noCookieButton.addEventListener("click", handleNoCookie);
  elements.noteText.addEventListener("input", updateCharCount);
}

async function init() {
  wireEvents();
  updateCharCount();
  handleQueryMessage();

  try {
    await loadStatus();
  } catch (error) {
    showToast(error.message, "error");
  }

  try {
    await loadMe();
  } catch (error) {
    showToast(error.message, "error");
  }
}

init();
