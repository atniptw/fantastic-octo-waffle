/**
 * Lazy loading utility for images
 * Implements Intersection Observer API for efficient image loading
 */

export function initializeLazyLoading() {
    if ('IntersectionObserver' in window) {
        const imageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    const src = img.dataset.src;
                    
                    if (src) {
                        // Create a new image to preload
                        const preloadImg = new Image();
                        
                        preloadImg.onload = () => {
                            img.src = src;
                            img.classList.remove('lazy-loading');
                            img.classList.add('lazy-loaded');
                            observer.unobserve(img);
                        };
                        
                        preloadImg.onerror = () => {
                            img.classList.remove('lazy-loading');
                            img.classList.add('lazy-error');
                            observer.unobserve(img);
                        };
                        
                        preloadImg.src = src;
                    }
                }
            });
        }, {
            rootMargin: '50px 0px', // Start loading 50px before entering viewport
            threshold: 0.01
        });

        // Observe all images with data-src attribute
        const lazyImages = document.querySelectorAll('img[data-src]');
        lazyImages.forEach(img => imageObserver.observe(img));

        return imageObserver;
    } else {
        // Fallback for browsers without Intersection Observer
        const lazyImages = document.querySelectorAll('img[data-src]');
        lazyImages.forEach(img => {
            const src = img.dataset.src;
            if (src) {
                img.src = src;
            }
        });
        return null;
    }
}

export function observeNewImages(observer) {
    if (observer && 'IntersectionObserver' in window) {
        const lazyImages = document.querySelectorAll('img[data-src]:not(.lazy-loading):not(.lazy-loaded):not(.lazy-error)');
        lazyImages.forEach(img => {
            img.classList.add('lazy-loading');
            observer.observe(img);
        });
    }
}

// Make functions available to Blazor
window.lazyLoadModule = {
    initializeLazyLoading,
    observeNewImages
};
