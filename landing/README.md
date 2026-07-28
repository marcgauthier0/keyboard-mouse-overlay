# Gaming Keypress Overlay - Landing Page

Modern, responsive landing page for the Gaming Keypress Overlay application.

## Features

- 🎨 Modern cyberpunk/neon design
- 📱 Fully responsive (mobile, tablet, desktop)
- ⚡ Fast loading with minimal dependencies
- 🌐 Ready for Cloudflare Workers deployment
- ✨ Smooth animations and transitions
- 🎯 SEO optimized

## Deployment to Cloudflare Workers

### Option 1: Direct HTML Serving

1. Upload all files (`index.html`, `styles.css`, `script.js`) to Cloudflare Workers
2. Use the following worker code:

```javascript
export default {
  async fetch(request) {
    const url = new URL(request.url);
    let path = url.pathname;
    
    // Default to index.html
    if (path === '/' || path === '') {
      path = '/index.html';
    }
    
    // Remove leading slash for file lookup
    path = path.substring(1);
    
    // Serve files
    if (path === 'index.html') {
      return new Response(HTML_CONTENT, {
        headers: { 'Content-Type': 'text/html; charset=utf-8' }
      });
    } else if (path === 'styles.css') {
      return new Response(CSS_CONTENT, {
        headers: { 'Content-Type': 'text/css' }
      });
    } else if (path === 'script.js') {
      return new Response(JS_CONTENT, {
        headers: { 'Content-Type': 'application/javascript' }
      });
    }
    
    return new Response('Not Found', { status: 404 });
  }
};
```

### Option 2: Using Wrangler (Recommended)

1. Install Wrangler CLI:
```bash
npm install -g wrangler
```

2. Create `wrangler.toml`:
```toml
name = "gaming-overlay-landing"
main = "worker.js"
compatibility_date = "2024-01-01"

[site]
bucket = "./landing"
```

3. Deploy:
```bash
wrangler deploy
```

### Option 3: Cloudflare Pages (Easiest)

1. Go to Cloudflare Dashboard → Pages
2. Connect your Git repository
3. Set build command: (none needed, static site)
4. Set output directory: `landing`
5. Deploy!

## Customization

### Update Download Links

Edit `script.js` and replace the download URLs:
```javascript
const downloadUrl = 'YOUR_ACTUAL_DOWNLOAD_URL';
```

### Update GitHub Link

Edit `index.html` and replace:
```html
<a href="https://github.com/yourusername/keyboard_overlay_windows" ...>
```

### Change Colors

Edit `styles.css` and modify CSS variables:
```css
:root {
    --primary-color: #00d4ff;
    --secondary-color: #ff00ff;
    /* ... */
}
```

## File Structure

```
landing/
├── index.html      # Main HTML file
├── styles.css      # All CSS styles
├── script.js       # JavaScript functionality
└── README.md       # This file
```

## Browser Support

- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)
- Mobile browsers

## License

MIT License - Same as the main project
