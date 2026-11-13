// Typing animation for the header's taglines, using typed.js (loaded in index.html).
// typed.js reads two things in a tagline as commands, even as plain text, and has no way to escape them:
// "^" followed by digits pauses for that many milliseconds, and text in backticks appears at once; both markers vanish.
// Only the animated line is affected; the screen reader copy shows the tagline as written.

import { whenLoaded } from '../js/deferred-scripts.js';

const TYPED_OPTIONS = {
    typeSpeed: 50,
    backSpeed: 30,
    backDelay: 1500,
    startDelay: 500,
    showCursor: false,
    // Plain text. typed.js defaults to 'html' and sets innerHTML, so a tagline from Storyblok would be read as markup.
    contentType: null
};

// Starts typing the taglines into the element and returns a handle whose stop() ends the animation,
// or null when the element is no longer on the page.
export async function start(element, texts) {
    if (!element?.isConnected) return null;

    // With reduced motion the first tagline simply stands there; screen readers get all of them from the hidden copy.
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) {
        element.textContent = texts[0] ?? '';
        return {
            stop() {
                element.textContent = '';
            }
        };
    }

    await whenLoaded('typed.js', () => typeof Typed !== 'undefined');
    if (!element.isConnected) return null;

    // Several taglines take turns forever. A single one is typed once and stays: typed.js only deletes
    // the last string when it loops, and retyping the same text would be motion without anything new.
    const typed = new Typed(element, { ...TYPED_OPTIONS, strings: texts, loop: texts.length > 1 });

    return {
        stop() {
            typed.destroy();
        }
    };
}
