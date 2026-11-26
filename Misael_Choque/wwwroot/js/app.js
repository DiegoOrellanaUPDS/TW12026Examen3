const statusText = document.getElementById("status");
const userInfo = document.getElementById("userInfo");
const loginBtn = document.getElementById("loginBtn");
const logoutBtn = document.getElementById("logoutBtn");
const result = document.getElementById("result");

async function request(url, options = {}) {
    const response = await fetch(url, {
        credentials: "same-origin",
        ...options,
        headers: {
            ...(options.headers || {})
        }
    });

    const text = await response.text();
    let data;

    try {
        data = JSON.parse(text);
    } catch {
        data = { message: text };
    }

    return { response, data };
}

async function checkSession() {
    try {
        const { response, data } = await request("/auth/me");

        if (response.ok && data.authenticated) {
            statusText.textContent = "Sesión iniciada correctamente.";
            userInfo.textContent =
                `Usuario: ${data.username} | Proveedor: ${data.provider}`;
            loginBtn.hidden = true;
            logoutBtn.hidden = false;
        } else {
            statusText.textContent = "No hay una sesión iniciada.";
            userInfo.textContent = "";
            loginBtn.hidden = false;
            logoutBtn.hidden = true;
        }
    } catch {
        statusText.textContent = "No se pudo consultar la sesión.";
    }
}

loginBtn.addEventListener("click", () => {
    window.location.href = "/auth/kick/login";
});

logoutBtn.addEventListener("click", async () => {
    const { response, data } = await request("/auth/logout", {
        method: "POST"
    });

    result.textContent = JSON.stringify(data, null, 2);
    if (response.ok) {
        await checkSession();
    }
});

document.getElementById("getBtn").addEventListener("click", async () => {
    const { response, data } = await request("/api/protegido");
    result.textContent =
        `HTTP ${response.status}\n${JSON.stringify(data, null, 2)}`;
});

document.getElementById("postBtn").addEventListener("click", async () => {
    const message = document.getElementById("messageInput").value.trim();

    const { response, data } = await request("/api/protegido", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message })
    });

    result.textContent =
        `HTTP ${response.status}\n${JSON.stringify(data, null, 2)}`;
});

checkSession();
