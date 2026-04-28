function DOMContentLoadedEvent() {
    return new Promise(resolve => window.addEventListener("DOMContentLoaded", event => resolve(event), { once: true }));
}

async function fetchApplyLayout(layoutUrl, containerId) {
    var ps = await Promise.all([fetch(layoutUrl), DOMContentLoadedEvent()]);
    var parser = new DOMParser();
    var doc = parser.parseFromString(await ps[0].text(), 'text/html');
    var container = doc.getElementById(containerId);
    container.innerHTML = document.body.innerHTML;
    document.replaceChild(
        document.adoptNode(doc.documentElement),
        document.documentElement
    );
}

function tryRedirectToLower() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    }
}