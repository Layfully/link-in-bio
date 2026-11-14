// Storyblok's bridge for the Visual Editor: reloads the page whenever the editor saves or publishes, so the preview
// shows the latest draft. Started by the StoryblokBridge component, only in the editor build.

const BRIDGE_SCRIPT = 'https://app.storyblok.com/f/storyblok-v2-latest.js';

// Resolves once the bridge listens for the editor's events; rejects when its script can't be loaded.
export function start() {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = BRIDGE_SCRIPT;
        script.onload = () => {
            const bridge = new StoryblokBridge();
            bridge.on(['change', 'published'], () => window.location.reload());
            resolve();
        };
        script.onerror = () => reject(new Error(`${BRIDGE_SCRIPT} could not be loaded.`));
        document.head.appendChild(script);
    });
}
