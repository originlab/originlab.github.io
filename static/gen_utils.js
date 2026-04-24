function fetchApplyLayout(layoutUrl, containerId) {
    window.addEventListener("DOMContentLoaded", async () => {
        var layout = await fetch(layoutUrl);
        var parser = new DOMParser();
        var doc = parser.parseFromString(await layout.text(), 'text/html');
        var container = doc.getElementById(containerId);
        container.innerHTML = document.body.innerHTML;
        document.replaceChild(
            document.adoptNode(doc.documentElement),
            document.documentElement
        );
    })
}

function tryRedirectToLower() {
    var currentURL = window.location.href;
    var lowerCaseURL = currentURL.toLowerCase();
    if (currentURL != lowerCaseURL) {
        location.replace(lowerCaseURL);
    }
}