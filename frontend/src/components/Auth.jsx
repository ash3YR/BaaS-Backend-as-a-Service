import { useState } from 'react';

export default function Auth({ onLogin }) {
  const [isLoginMode, setIsLoginMode] = useState(true);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [status, setStatus] = useState({ message: '', tone: 'hidden' });
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const endpoint = isLoginMode ? "/api/auth/login" : "/api/auth/register";
    
    setIsLoading(true);
    setStatus({ message: 'Processing...', tone: 'info' });
    
    try {
      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password })
      });
      
      const payload = await response.json();
      if (!response.ok) {
        setStatus({ message: payload.message || payload.error || "Authentication failed.", tone: "error" });
      } else {
        setStatus({ message: '', tone: 'hidden' });
        setPassword('');
        onLogin(payload.adminApiKey);
      }
    } catch {
      setStatus({ message: "The service could not be reached.", tone: "error" });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <section className="hero-card">
      <div className="eyebrow">B-a-a-S</div>
      <h1>Sign In or Register</h1>
      <p className="hero-copy">
        Create an account or log in to get your API keys and start provisioning databases instantly.
      </p>

      <div className="auth-tabs">
        <button
          className={`tab-button ${isLoginMode ? 'active' : ''}`}
          type="button"
          onClick={() => setIsLoginMode(true)}
        >
          Login
        </button>
        <button
          className={`tab-button ${!isLoginMode ? 'active' : ''}`}
          type="button"
          onClick={() => setIsLoginMode(false)}
        >
          Register
        </button>
      </div>

      <form className="upload-panel" onSubmit={handleSubmit}>
        <label className="upload-field">
          <span>Email</span>
          <input
            type="email"
            required
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label className="upload-field">
          <span>Password</span>
          <input
            type="password"
            required
            minLength="6"
            placeholder="Min 6 characters"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <button type="submit" disabled={isLoading}>
          {isLoginMode ? "Log In" : "Register"}
        </button>
      </form>

      <div className={`status-banner ${status.tone}`}>
        {status.message}
      </div>
    </section>
  );
}
