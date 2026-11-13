// The Storyblok Bridge is only needed inside the Visual Editor, which opens the site with a `_storyblok` query parameter.
// It reloads the page whenever the editor saves or publishes, so the preview shows the latest draft.
// Kept in a file rather than inline so the Content Security Policy needs no exception for inline scripts.
(() => {
    if (!new URLSearchParams(window.location.search).has('_storyblok')) return;

    const bridgeScript = document.createElement('script');
    bridgeScript.src = 'https://app.storyblok.com/f/storyblok-v2-latest.js';
    bridgeScript.onload = () => {
        const bridge = new StoryblokBridge();
        bridge.on(['change', 'published'], () => window.location.reload());
    };
    document.body.appendChild(bridgeScript);
})();
