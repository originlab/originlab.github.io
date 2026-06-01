function DOMContentLoadedEvent() {
    return new Promise(resolve => window.addEventListener("DOMContentLoaded", event => resolve(event), { once: true }));
}

async function fetchApplyLayout(layoutUrl, placeholderId, mainContentId) {
    var domLoadEvent = DOMContentLoadedEvent();
    var response = await fetch(layoutUrl);
    var parser = new DOMParser();
    var layout = parser.parseFromString(await response.text(), 'text/html');
    var placeholder = layout.getElementById(placeholderId);
    var scripts = [...layout.scripts];

    for (var s of scripts) {
        s.remove();
    }

    await domLoadEvent;

    var title = document.title;

    placeholder.replaceWith(...document.getElementById(mainContentId).childNodes);
    document.replaceChild(
        document.adoptNode(layout.documentElement),
        document.documentElement
    );

    document.title = title;

    for (var s of scripts) {
        var script = document.createElement("script");
        if (s.src != "") {
            script.async = false;
            script.integrity = s.integrity;
            script.crossOrigin = s.crossOrigin;
            script.src = s.src;
        } else {
            script.innerHTML = s.innerHTML;
        }
        document.body.append(script);
    };
}

function tryRedirectToLowerOrEnglish() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    } else if (/\/\w{2}\/?$/.test(currentURL)) {
        location.replace(currentURL.substring(0, currentURL.lastIndexOf('/', currentURL.length - 2)));
    }
}