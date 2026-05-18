import { useState, useEffect } from 'react';
import Auth from './components/Auth';
import Dashboard from './components/Dashboard';

function App() {
  const [currentApiKey, setCurrentApiKey] = useState(localStorage.getItem("adminApiKey") || "");

  useEffect(() => {
    if (currentApiKey) {
      localStorage.setItem("adminApiKey", currentApiKey);
    } else {
      localStorage.removeItem("adminApiKey");
    }
  }, [currentApiKey]);

  const handleLogin = (apiKey) => {
    setCurrentApiKey(apiKey);
  };

  const handleLogout = () => {
    setCurrentApiKey("");
  };

  return (
    <main className="page-shell">
      {!currentApiKey ? (
        <Auth onLogin={handleLogin} />
      ) : (
        <Dashboard currentApiKey={currentApiKey} onLogout={handleLogout} />
      )}
    </main>
  );
}

export default App;
