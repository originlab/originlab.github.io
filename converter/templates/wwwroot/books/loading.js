
async function applyLayout(layoutUrl) {
    var domLoadEvent = getPromise(window, 'DOMContentLoaded');
    var response = await fetch(layoutUrl);
    var parser = new DOMParser();
    var layout = parser.parseFromString(await response.text(), 'text/html');
    var placeholder = layout.getElementById('doc-content-placeholder');
    var scripts = [...layout.scripts];
    var links = [...layout.querySelectorAll('link[rel=stylesheet]')]

    for (var s of scripts) {
        s.remove();
    }

    await domLoadEvent;

    var title = document.title;

    if (location.hash) {
        let hash = location.hash;
        Promise.all(links.map(link => getPromise(link, 'load'))).then(() => {
            location.hash = '';
            location.hash = hash;
        });
    }

    placeholder.replaceWith(...document.getElementById('main-content').childNodes);
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

    function getPromise(obj, ev) { return new Promise(resolve => obj.addEventListener(ev, e => resolve(e), { once: true })); }
}
