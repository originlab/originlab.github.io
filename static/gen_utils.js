function DOMContentLoadedEvent() {
    return new Promise(resolve => window.addEventListener("DOMContentLoaded", event => resolve(event), { once: true }));
}

async function fetchApplyLayout(layoutUrl, containerId, mainContentId) {
    var ps = await Promise.all([fetch(layoutUrl), DOMContentLoadedEvent()]);
    var parser = new DOMParser();
    var layout = parser.parseFromString(await ps[0].text(), 'text/html');
    var container = layout.getElementById(containerId);
    container.replaceChildren(...document.getElementById(mainContentId).childNodes);
    var title = document.title;
    document.replaceChild(
        document.adoptNode(layout.documentElement),
        document.documentElement
    );
    document.title = title;
}

function tryRedirectToLower() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    }
}