import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from '@identity-base/sample-router'
import App from './App'
import './style.css'

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>,
)
