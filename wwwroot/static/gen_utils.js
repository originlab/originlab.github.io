function DOMContentLoadedEvent() {
    return new Promise(resolve => window.addEventListener("DOMContentLoaded", event => resolve(event), { once: true }));
}

async function fetchApplyLayout(layoutUrl, containerId, mainContentId) {
    var domLoadEvent = DOMContentLoadedEvent();
    var response = await fetch(layoutUrl);
    var parser = new DOMParser();
    var layout = parser.parseFromString(await response.text(), 'text/html');
    var container = layout.getElementById(containerId);
    var scripts = [...layout.scripts];

    await domLoadEvent;

    var title = document.title;

    container.replaceChildren(...document.getElementById(mainContentId).childNodes);
    document.replaceChild(
        document.adoptNode(layout.documentElement),
        document.documentElement
    );

    document.title = title;

    [...document.scripts].forEach(s => s.remove());
    scripts.forEach(s => {
        var script = document.createElement("script");
        script.async = s.async;
        script.defer = s.defer;
        if (s.src != "") {
            script.src = s.src;
        } else {
            script.innerHTML = s.innerHTML;
        }
        document.body.append(script);
    });
}

function tryRedirectToLowerOrEnglish() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    } else if (currentURL.lastIndexOf('/') == currentURL.length - 3) {
        location.replace(currentURL.substring(0, currentURL.lastIndexOf('/')));
    }
}