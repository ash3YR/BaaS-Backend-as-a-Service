const authSection = document.getElementById("auth-section");
const appSection = document.getElementById("app-section");
const tabLogin = document.getElementById("tab-login");
const tabRegister = document.getElementById("tab-register");
const authForm = document.getElementById("auth-form");
const authEmail = document.getElementById("auth-email");
const authPassword = document.getElementById("auth-password");
const authSubmitBtn = document.getElementById("auth-submit-button");
const authStatusBanner = document.getElementById("auth-status-banner");
const logoutBtn = document.getElementById("logout-button");
const displayAdminKey = document.getElementById("display-admin-key");

const form = document.getElementById("upload-form");
const fileInput = document.getElementById("file-input");
const submitButton = document.getElementById("submit-button");
const statusBanner = document.getElementById("status-banner");
const resultSection = document.getElementById("result-section");
const tableName = document.getElementById("table-name");
const rowsInserted = document.getElementById("rows-inserted");
const provisionStatus = document.getElementById("provision-status");
const schemaList = document.getElementById("schema-list");
const previewTable = document.getElementById("preview-table");
const apiList = document.getElementById("api-list");

let currentApiKey = localStorage.getItem("adminApiKey") || "";
let isLoginMode = true;

function checkAuth() {
  if (currentApiKey) {
    authSection.classList.add("hidden");
    appSection.classList.remove("hidden");
    displayAdminKey.textContent = currentApiKey;
  } else {
    authSection.classList.remove("hidden");
    appSection.classList.add("hidden");
  }
}
checkAuth();

tabLogin.addEventListener("click", () => {
  isLoginMode = true;
  tabLogin.classList.add("active");
  tabRegister.classList.remove("active");
  authSubmitBtn.textContent = "Log In";
});

tabRegister.addEventListener("click", () => {
  isLoginMode = false;
  tabRegister.classList.add("active");
  tabLogin.classList.remove("active");
  authSubmitBtn.textContent = "Register";
});

logoutBtn.addEventListener("click", () => {
  localStorage.removeItem("adminApiKey");
  currentApiKey = "";
  checkAuth();
  resultSection.classList.add("hidden");
});

function showAuthStatus(message, tone) {
  authStatusBanner.textContent = message;
  authStatusBanner.className = `status-banner ${tone}`;
}

authForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const endpoint = isLoginMode ? "/api/auth/login" : "/api/auth/register";
  
  authSubmitBtn.disabled = true;
  showAuthStatus("Processing...", "info");
  
  try {
    const response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: authEmail.value,
        password: authPassword.value
      })
    });
    
    const payload = await response.json();
    if (!response.ok) {
      showAuthStatus(payload.message || payload.error || "Authentication failed.", "error");
    } else {
      currentApiKey = payload.adminApiKey;
      localStorage.setItem("adminApiKey", currentApiKey);
      checkAuth();
      authStatusBanner.className = "status-banner hidden";
      authPassword.value = "";
    }
  } catch {
    showAuthStatus("The service could not be reached.", "error");
  } finally {
    authSubmitBtn.disabled = false;
  }
});

form.addEventListener("submit", async (event) => {
  event.preventDefault();

  const file = fileInput.files?.[0];
  if (!file) {
    showStatus("Please choose a CSV or XLSX file first.", "warning");
    return;
  }

  const formData = new FormData();
  formData.append("file", file);

  submitButton.disabled = true;
  submitButton.textContent = "Building your APIs...";
  showStatus("Uploading your file and provisioning the database...", "info");
  resultSection.classList.add("hidden");

  try {
    const response = await fetch("/api/portal/upload", {
      method: "POST",
      headers: {
        "x-api-key": currentApiKey
      },
      body: formData
    });

    const payload = await response.json();
    if (!response.ok) {
      showStatus(payload.message || payload.error || "Upload failed.", "error");
      return;
    }

    renderResult(payload);
  } catch {
    showStatus("The service could not be reached. Please make sure the API is running.", "error");
  } finally {
    submitButton.disabled = false;
    submitButton.textContent = "Upload And Generate APIs";
  }
});

function renderResult(payload) {
  tableName.textContent = payload.tableName || "Not created";
  rowsInserted.textContent = `${payload.rowsInserted ?? 0}`;
  provisionStatus.textContent = payload.status || "Unknown";

  schemaList.innerHTML = "";
  (payload.schema || []).forEach((column) => {
    const chip = document.createElement("div");
    chip.className = "chip";
    chip.textContent = `${column.name}: ${column.type}`;
    schemaList.appendChild(chip);
  });

  renderPreviewTable(payload.previewRows || payload.sampleData || []);
  renderApiList(payload.api || {});

  resultSection.classList.remove("hidden");

  if ((payload.status || "").toLowerCase() === "success") {
    showStatus("Database table created successfully. Your generated APIs are ready below.", "success");
  } else {
    showStatus(payload.error || payload.status || "The file was parsed, but the database is currently unavailable.", "warning");
  }
}

function renderPreviewTable(rows) {
  if (!rows.length) {
    previewTable.innerHTML = "<p class=\"hint\">No rows available to preview yet.</p>";
    return;
  }

  const columns = Object.keys(rows[0]);
  const table = document.createElement("table");
  const thead = document.createElement("thead");
  const headerRow = document.createElement("tr");

  columns.forEach((column) => {
    const th = document.createElement("th");
    th.textContent = column;
    headerRow.appendChild(th);
  });

  thead.appendChild(headerRow);
  table.appendChild(thead);

  const tbody = document.createElement("tbody");
  rows.forEach((row) => {
    const tr = document.createElement("tr");

    columns.forEach((column) => {
      const td = document.createElement("td");
      const value = row[column];
      td.textContent = value === null || value === undefined || value === "" ? "NULL" : String(value);
      tr.appendChild(td);
    });

    tbody.appendChild(tr);
  });

  table.appendChild(tbody);
  previewTable.innerHTML = "";
  previewTable.appendChild(table);
}

function renderApiList(api) {
  apiList.innerHTML = "";

  const definitions = [
    ["GET all rows", api.getAll, "Returns all rows in the generated table."],
    ["GET with filtering", api.queryWithFiltering, "Supports page, pageSize, sortBy, sortDirection, search, and filter_<column> query parameters."],
    ["GET one row", api.getById, "Returns a single row by Id."],
    ["POST create row", api.create, "Creates a new row using a JSON body."],
    ["PUT update row", api.update, "Updates an existing row by Id."],
    ["DELETE remove row", api.delete, "Deletes a row by Id."],
    ["GET column metadata", api.columnMetadata, "Returns dynamic column metadata and database types."],
    ["GET OpenAPI spec", api.openApiSpec, "Returns an OpenAPI-style description for this generated table."]
  ];

  definitions.forEach(([label, url, description]) => {
    if (!url) {
      return;
    }

    const item = document.createElement("div");
    item.className = "api-item";
    item.innerHTML = `
      <div class="api-item-header">
        <strong>${label}</strong>
        <div class="api-actions">
          <button type="button" class="small-button secondary copy-button">Copy</button>
          <a class="small-button ghost" href="${encodeURI(url)}" target="_blank" rel="noreferrer">Test</a>
        </div>
      </div>
      <p class="api-description">${description}</p>
      <code>${url}</code>
    `;

    const copyButton = item.querySelector(".copy-button");
    copyButton?.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(url);
        copyButton.textContent = "Copied";
        setTimeout(() => {
          copyButton.textContent = "Copy";
        }, 1200);
      } catch {
        copyButton.textContent = "Failed";
        setTimeout(() => {
          copyButton.textContent = "Copy";
        }, 1200);
      }
    });

    apiList.appendChild(item);
  });

  const sampleQueries = [
    {
      title: "Sample query: Pagination",
      url: `${api.getAll}?page=1&pageSize=3`
    },
    {
      title: "Sample query: Sort by age desc",
      url: `${api.getAll}?sortBy=age&sortDirection=desc`
    },
    {
      title: "Sample query: Search",
      url: `${api.getAll}?search=Yash`
    }
  ].filter((item) => item.url && !item.url.includes("undefined"));

  sampleQueries.forEach((itemData) => {
    const item = document.createElement("div");
    item.className = "api-item sample";
    item.innerHTML = `
      <div class="api-item-header">
        <strong>${itemData.title}</strong>
        <div class="api-actions">
          <button type="button" class="small-button secondary copy-button">Copy</button>
          <a class="small-button ghost" href="${encodeURI(itemData.url)}" target="_blank" rel="noreferrer">Test</a>
        </div>
      </div>
      <code>${itemData.url}</code>
    `;

    const copyButton = item.querySelector(".copy-button");
    copyButton?.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(itemData.url);
        copyButton.textContent = "Copied";
        setTimeout(() => {
          copyButton.textContent = "Copy";
        }, 1200);
      } catch {
        copyButton.textContent = "Failed";
        setTimeout(() => {
          copyButton.textContent = "Copy";
        }, 1200);
      }
    });

    apiList.appendChild(item);
  });
}

function showStatus(message, tone) {
  statusBanner.textContent = message;
  statusBanner.className = `status-banner ${tone}`;
}
