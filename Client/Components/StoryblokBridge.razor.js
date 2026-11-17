// Storyblok's bridge for the Visual Editor. It hands each edit to .NET as it is typed, so the preview follows the editor
// without saving (live preview), and reloads the page whenever the editor saves or publishes, so the preview then shows
// the saved draft. Started by the StoryblokBridge component, only in the editor build.

const BRIDGE_SCRIPT = 'https://app.storyblok.com/f/storyblok-v2-latest.js';
const BRIDGE_STYLESHEET_ID = 'storyblok-bridge-stylesheet';

// The bridge draws the outline around a block, and the boxes it shows on hover and click, with a <style> element it adds
// once the editor has started it. The Content Security Policy blocks that element: style-src allows only the inlined
// app.css, by hash, and the bridge's text changes with Storyblok's releases. A stylesheet made from script isn't inline
// markup, so the policy lets it apply; it takes the bridge's own text, and stays right when Storyblok changes it.
function adoptBridgeStyles() {
    const adopt = element => {
        const sheet = new CSSStyleSheet();
        sheet.replaceSync(element.textContent);
        document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet];
    };

    const existing = document.getElementById(BRIDGE_STYLESHEET_ID);
    if (existing) {
        adopt(existing);
        return;
    }

    const observer = new MutationObserver(() => {
        const element = document.getElementById(BRIDGE_STYLESHEET_ID);
        if (element) {
            observer.disconnect();
            adopt(element);
        }
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
}

// Resolves once the bridge listens for the editor's events; rejects when its script can't be loaded.
// dotNet is the StoryblokBridge component; the story goes to it as JSON text, which the Storyblok client reads with
// the site's component classes.
export function start(dotNet) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = BRIDGE_SCRIPT;
        script.onload = () => {
            adoptBridgeStyles();
            const bridge = new StoryblokBridge();
            bridge.on('input', event => {
                if (event?.story) {
                    dotNet.invokeMethodAsync('ReadEditedStory', JSON.stringify(event.story));
                }
            });
            bridge.on(['change', 'published'], () => window.location.reload());
            resolve();
        };
        script.onerror = () => reject(new Error(`${BRIDGE_SCRIPT} could not be loaded.`));
        document.head.appendChild(script);
    });
}
