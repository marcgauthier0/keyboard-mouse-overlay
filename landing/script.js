// Smooth scroll for anchor links
document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
        e.preventDefault();
        const target = document.querySelector(this.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    });
});

// Download button handlers
document.getElementById('download-installer')?.addEventListener('click', function(e) {
    e.preventDefault();
    // Replace with actual download URL
    const downloadUrl = 'https://github.com/yourusername/keyboard_overlay_windows/releases/latest/download/GamingKeypressOverlay_Setup.exe';
    
    // Track download (optional)
    if (typeof gtag !== 'undefined') {
        gtag('event', 'download', {
            'event_category': 'Downloads',
            'event_label': 'Installer'
        });
    }
    
    // Open download in new tab
    window.open(downloadUrl, '_blank');
    
    // Show notification
    showNotification('Download started!', 'success');
});

document.getElementById('download-portable')?.addEventListener('click', function(e) {
    e.preventDefault();
    // Replace with actual download URL
    const downloadUrl = 'https://github.com/yourusername/keyboard_overlay_windows/releases/latest/download/GamingKeypressOverlay.exe';
    
    // Track download (optional)
    if (typeof gtag !== 'undefined') {
        gtag('event', 'download', {
            'event_category': 'Downloads',
            'event_label': 'Portable'
        });
    }
    
    // Open download in new tab
    window.open(downloadUrl, '_blank');
    
    // Show notification
    showNotification('Download started!', 'success');
});

// Notification system
function showNotification(message, type = 'info') {
    // Remove existing notification
    const existing = document.querySelector('.notification');
    if (existing) {
        existing.remove();
    }
    
    // Create notification
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.textContent = message;
    
    // Add styles
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: ${type === 'success' ? '#00d4ff' : '#ff00ff'};
        color: white;
        padding: 15px 25px;
        border-radius: 8px;
        box-shadow: 0 4px 20px rgba(0, 212, 255, 0.4);
        z-index: 10000;
        animation: slideIn 0.3s ease;
        font-weight: 600;
    `;
    
    document.body.appendChild(notification);
    
    // Remove after 3 seconds
    setTimeout(() => {
        notification.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

// Add animation keyframes
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from {
            transform: translateX(100%);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    
    @keyframes slideOut {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(100%);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);

// Intersection Observer for fade-in animations
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.style.opacity = '1';
            entry.target.style.transform = 'translateY(0)';
        }
    });
}, observerOptions);

// Observe all feature cards and technical items
document.addEventListener('DOMContentLoaded', () => {
    const animatedElements = document.querySelectorAll('.feature-card, .technical-item, .download-card');
    animatedElements.forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(20px)';
        el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        observer.observe(el);
    });
});

// Parallax effect for hero section
window.addEventListener('scroll', () => {
    const scrolled = window.pageYOffset;
    const hero = document.querySelector('.hero-background');
    if (hero) {
        hero.style.transform = `translateY(${scrolled * 0.5}px)`;
    }
});

// Add glow effect on hover for buttons
document.querySelectorAll('.btn').forEach(btn => {
    btn.addEventListener('mouseenter', function() {
        this.style.filter = 'brightness(1.1)';
    });
    
    btn.addEventListener('mouseleave', function() {
        this.style.filter = 'brightness(1)';
    });
});

// Console easter egg
console.log('%c🎮 Gaming Keypress Overlay', 'font-size: 20px; font-weight: bold; color: #00d4ff;');
console.log('%cBuilt with ❤️ for competitive gaming', 'font-size: 12px; color: #ff00ff;');
console.log('%cGitHub: https://github.com/yourusername/keyboard_overlay_windows', 'font-size: 12px; color: #9d4edd;');
