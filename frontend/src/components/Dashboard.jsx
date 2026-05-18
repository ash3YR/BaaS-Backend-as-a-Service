import { useState } from 'react';

export default function Dashboard({ currentApiKey, onLogout }) {
  const [file, setFile] = useState(null);
  const [status, setStatus] = useState({ message: '', tone: 'hidden' });
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState(null);

  const handleUpload = async (e) => {
    e.preventDefault();
    if (!file) {
      setStatus({ message: "Please choose a CSV or XLSX file first.", tone: "warning" });
      return;
    }

    const formData = new FormData();
    formData.append("file", file);

    setIsLoading(true);
    setStatus({ message: "Uploading your file and provisioning the database...", tone: "info" });
    setResult(null);

    try {
      const response = await fetch("/api/portal/upload", {
        method: "POST",
        headers: { "x-api-key": currentApiKey },
        body: formData
      });

      const payload = await response.json();
      if (!response.ok) {
        setStatus({ message: payload.message || payload.error || "Upload failed.", tone: "error" });
        return;
      }

      setResult(payload);
      if ((payload.status || "").toLowerCase() === "success") {
        setStatus({ message: "Database table created successfully. Your generated APIs are ready below.", tone: "success" });
      } else {
        setStatus({ message: payload.error || payload.status || "The file was parsed, but the database is currently unavailable.", tone: "warning" });
      }
    } catch {
      setStatus({ message: "The service could not be reached. Please make sure the API is running.", tone: "error" });
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopy = async (text, e) => {
    const btn = e.target;
    try {
      await navigator.clipboard.writeText(text);
      btn.textContent = "Copied";
    } catch {
      btn.textContent = "Failed";
    }
    setTimeout(() => { btn.textContent = "Copy"; }, 1200);
  };

  const apiDefinitions = result?.api ? [
    ["GET all rows", result.api.getAll, "Returns all rows in the generated table."],
    ["GET with filtering", result.api.queryWithFiltering, "Supports page, pageSize, sortBy, sortDirection, search, and filter_<column> query parameters."],
    ["GET one row", result.api.getById, "Returns a single row by Id."],
    ["POST create row", result.api.create, "Creates a new row using a JSON body."],
    ["PUT update row", result.api.update, "Updates an existing row by Id."],
    ["DELETE remove row", result.api.delete, "Deletes a row by Id."],
    ["GET column metadata", result.api.columnMetadata, "Returns dynamic column metadata and database types."],
    ["GET OpenAPI spec", result.api.openApiSpec, "Returns an OpenAPI-style description for this generated table."]
  ] : [];

  const sampleQueries = result?.api ? [
    { title: "Sample query: Pagination", url: `${result.api.getAll}?page=1&pageSize=3` },
    { title: "Sample query: Sort by age desc", url: `${result.api.getAll}?sortBy=age&sortDirection=desc` },
    { title: "Sample query: Search", url: `${result.api.getAll}?search=Yash` }
  ].filter(item => item.url && !item.url.includes("undefined")) : [];

  return (
    <>
      <section className="hero-card">
        <div className="header-bar">
          <div className="eyebrow">Dynamic Backend-as-a-Service</div>
          <button className="small-button ghost" type="button" onClick={onLogout}>Log Out</button>
        </div>
        <h1>Upload your file. Get a Postgres-backed table and live APIs.</h1>
        <p className="hero-copy">
          This service ingests CSV and Excel files, infers data types, provisions PostgreSQL tables,
          inserts rows, and gives you ready-to-use CRUD API endpoints automatically.
        </p>

        <div className="api-keys-panel">
          <strong>Your Admin API Key:</strong> <code>{currentApiKey}</code>
          <p className="hint">Pass this in the <code>x-api-key</code> header for full access to your provisioned APIs in Postman.</p>
        </div>

        <form className="upload-panel" onSubmit={handleUpload}>
          <label className="upload-field">
            <span>Select file</span>
            <input
              name="file"
              type="file"
              accept=".csv,.txt,.xlsx"
              required
              onChange={(e) => setFile(e.target.files[0])}
            />
          </label>
          <p className="hint">Accepted formats: CSV, TXT, XLSX. Max size: 5 MB.</p>
          <button type="submit" disabled={isLoading}>
            {isLoading ? "Building your APIs..." : "Upload And Generate APIs"}
          </button>
        </form>

        <div className={`status-banner ${status.tone}`}>
          {status.message}
        </div>
      </section>

      {result && (
        <section className="results-grid">
          <article className="result-card">
            <h2>Provisioned Table</h2>
            <div className="meta-grid">
              <div>
                <span className="meta-label">Table name</span>
                <strong>{result.tableName || "Not created"}</strong>
              </div>
              <div>
                <span className="meta-label">Rows inserted</span>
                <strong>{result.rowsInserted ?? 0}</strong>
              </div>
              <div>
                <span className="meta-label">Status</span>
                <strong>{result.status || "Unknown"}</strong>
              </div>
            </div>
          </article>

          <article className="result-card">
            <h2>Detected Schema</h2>
            <div className="chip-grid">
              {(result.schema || []).map((col, idx) => (
                <div key={idx} className="chip">{col.name}: {col.type}</div>
              ))}
            </div>
          </article>

          <article className="result-card wide">
            <h2>Database Preview</h2>
            <div className="table-wrapper">
              {(result.previewRows && result.previewRows.length > 0) || (result.sampleData && result.sampleData.length > 0) ? (
                <table>
                  <thead>
                    <tr>
                      {Object.keys(result.previewRows?.[0] || result.sampleData?.[0] || {}).map((col) => (
                        <th key={col}>{col}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {(result.previewRows || result.sampleData).map((row, idx) => (
                      <tr key={idx}>
                        {Object.values(row).map((val, vIdx) => (
                          <td key={vIdx}>{val === null || val === undefined || val === "" ? "NULL" : String(val)}</td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p className="hint">No rows available to preview yet.</p>
              )}
            </div>
          </article>

          <article className="result-card wide">
            <h2>Generated API Operations</h2>
            <div className="api-list">
              {apiDefinitions.map(([label, url, desc], idx) => url && (
                <div key={idx} className="api-item">
                  <div className="api-item-header">
                    <strong>{label}</strong>
                    <div className="api-actions">
                      <button type="button" className="small-button secondary" onClick={(e) => handleCopy(url, e)}>Copy</button>
                      <a className="small-button ghost" href={encodeURI(url)} target="_blank" rel="noreferrer">Test</a>
                    </div>
                  </div>
                  <p className="api-description">{desc}</p>
                  <code>{url}</code>
                </div>
              ))}
              
              {sampleQueries.map((item, idx) => (
                <div key={`sample-${idx}`} className="api-item sample">
                  <div className="api-item-header">
                    <strong>{item.title}</strong>
                    <div className="api-actions">
                      <button type="button" className="small-button secondary" onClick={(e) => handleCopy(item.url, e)}>Copy</button>
                      <a className="small-button ghost" href={encodeURI(item.url)} target="_blank" rel="noreferrer">Test</a>
                    </div>
                  </div>
                  <code>{item.url}</code>
                </div>
              ))}
            </div>
          </article>
        </section>
      )}
    </>
  );
}
