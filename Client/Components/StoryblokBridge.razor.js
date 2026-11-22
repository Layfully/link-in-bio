const BRIDGE_SCRIPT = 'https://app.storyblok.com/f/storyblok-v2-latest.js';
const BRIDGE_STYLESHEET_ID = 'storyblok-bridge-stylesheet';

// The editor injects its block outline and hover styles during startup.
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

export async function start(dotNet) {
    await new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = BRIDGE_SCRIPT;
        script.onload = resolve;
        script.onerror = () => reject(new Error(`${BRIDGE_SCRIPT} could not be loaded.`));
        document.head.appendChild(script);
    });

    adoptBridgeStyles();
    const bridge = new StoryblokBridge();
    bridge.on('input', event => {
        const story = event?.story;
        if (story) {
            dotNet.invokeMethodAsync('ReadEditedStory', JSON.stringify(story));
        }
    });
    bridge.on(['change', 'published'], () => window.location.reload());
}
