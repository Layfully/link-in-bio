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

    // Until now CSS showed the first tagline (data-first). It becomes the element's own text, so nothing changes on
    // screen, and the typing carries on from it instead of starting over.
    element.textContent = texts[0] ?? '';
    element.dataset.started = '';

    const standingStill = { stop() { element.textContent = ''; } };

    // With reduced motion the first tagline simply stands there; screen readers get all of them from the hidden copy.
    // A single tagline stands there too: typing the text that is already shown would be motion without anything new.
    if (texts.length < 2 || matchMedia('(prefers-reduced-motion: reduce)').matches) return standingStill;

    await whenLoaded('typed.js', () => typeof Typed !== 'undefined');
    if (!element.isConnected) return null;

    // typed.js takes text already in the element as its first string and starts by deleting it, so it gets only the
    // other taglines: the first stays for the usual pause, goes, and the second is typed, as if it had all been typed.
    // It then remembers where that first text ended; reset that before each round, or the first tagline would come
    // back half-deleted. They take turns forever.
    const typed = new Typed(element, {
        ...TYPED_OPTIONS,
        startDelay: TYPED_OPTIONS.backDelay,
        strings: texts.slice(1),
        loop: true,
        onLastStringBackspaced: () => { typed.strPos = 0; }
    });

    return {
        stop() {
            typed.destroy();
        }
    };
}
