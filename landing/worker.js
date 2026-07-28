// Cloudflare Worker for Gaming Keypress Overlay Landing Page
// This worker serves the static HTML/CSS/JS files

// Import the HTML, CSS, and JS files
import html from './index.html';
import css from './styles.css';
import js from './script.js';

export default {
  async fetch(request) {
    const url = new URL(request.url);
    let path = url.pathname;
    
    // Default to index.html for root
    if (path === '/' || path === '') {
      path = '/index.html';
    }
    
    // Remove leading slash
    path = path.substring(1);
    
    // Serve files based on path
    if (path === 'index.html' || path === '') {
      return new Response(html, {
        headers: {
          'Content-Type': 'text/html; charset=utf-8',
          'Cache-Control': 'public, max-age=3600'
        }
      });
    } else if (path === 'styles.css') {
      return new Response(css, {
        headers: {
          'Content-Type': 'text/css',
          'Cache-Control': 'public, max-age=86400'
        }
      });
    } else if (path === 'script.js') {
      return new Response(js, {
        headers: {
          'Content-Type': 'application/javascript',
          'Cache-Control': 'public, max-age=86400'
        }
      });
    }
    
    // 404 for unknown paths
    return new Response('Not Found', { 
      status: 404,
      headers: { 'Content-Type': 'text/plain' }
    });
  }
};
