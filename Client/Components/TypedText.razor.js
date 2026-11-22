// Typed.js interprets ^digits and backticks even in plain-text mode.

import { whenLoaded } from '../js/deferred-scripts.js';

const PAUSE_MS = 1500;

// Starts typing the taglines into the element and returns a handle whose stop() ends the animation,
// or null when the element is no longer on the page.
export async function start(element, texts) {
    if (!element?.isConnected) return null;

    // Transfer the first tagline from CSS to the element before animating.
    element.textContent = texts[0] ?? '';
    element.dataset.started = '';

    if (texts.length < 2 || matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return { stop() { element.textContent = ''; } };
    }

    await whenLoaded('typed.js', () => typeof Typed !== 'undefined');
    if (!element.isConnected) return null;

    // Typed.js erases the existing text before typing the remaining taglines.
    const typed = new Typed(element, {
        typeSpeed: 50,
        backSpeed: 30,
        backDelay: PAUSE_MS,
        startDelay: PAUSE_MS,
        showCursor: false,
        contentType: null, // Treat Storyblok taglines as plain text.
        strings: texts.slice(1),
        loop: true,
        // Start the next loop at the beginning of the string.
        onLastStringBackspaced: () => { typed.strPos = 0; }
    });

    return {
        stop() {
            typed.destroy();
        }
    };
}
